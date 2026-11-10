using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Random = UnityEngine.Random;

public enum Sound
{
    Missile, FireballCast, Explosion, Nova, Lightning, WispHit,
    EnemyHit, EnemyDie, EnemyShoot, PlayerHurt, Gem, LevelUp, Select,
    Warning, Teleport, Burrow, BossDie,
    PlayerDie, Victory, BossRoar, Comet, Combo, WardBlock, WardReady, LowHealth,
    RuneSocket, Evolve, Feat, Coin, Sizzle, Chill, Splash, Wither, Spark,
    KillBurst, Heal, Magnet, Lunge, Split, DieSlime, DieBat, DieBeast, BossHit,
}

// Sound effects. Every sound is synthesized at startup (a few ms per frame, never in one frame) so the
// game needs no audio files, but a file named after the sound in Assets/Resources/Sfx/ replaces the
// synth: "Fireball.wav" is not needed, name it after the Sound enum: EnemyHit.wav, or EnemyHit_1.wav,
// EnemyHit_2.wav... for round-robin variants. No code changes needed.
//
// Voice management (see docs/audio/PLAN.md): a fixed pool of AudioSources with priority stealing, a
// per-sound voice cap and cooldown (dropped plays make the next one louder), pitch and volume
// variation, round-robin variants, a gain curve that shrinks as voices pile up, ducking of quiet sounds
// and the music under big moments, kill-chain grouping and screen-position pan/attenuation.
// Every place that plays a sound names itself (e.g. "spells.Fireball.cast") and gets its own volume in
// the tuner under audio.sfx.<that name>, on top of audio.volume, the bus volume and the Settings volume.
//
// Tuner keys (all optional): audio.volume, audio.bus.sfx, audio.bus.ui, audio.voices, audio.headroom,
// audio.duck.sfx / .music / .time, audio.pan, audio.spatial, audio.buildBudgetMs, audio.kill.*,
// and per sound audio.sound.<Sound>.gap / .max / .priority (0 low, 1 normal, 2 high) / .volume.
public class Sfx : MonoBehaviour
{
    const int Rate = 44100;
    const int MaxVoices = 32;

    public enum Bus { Sfx, Ui }
    enum Prio { Low = 0, Normal = 1, High = 2 }

    struct Cfg { public float gap; public int max; public Prio prio; public Bus bus; public float volJit; public int variants; }

    static Sfx instance;
    public static float MusicDuck { get; private set; } = 1f;   // Music multiplies its volume by this

    readonly List<AudioClip>[] clips = new List<AudioClip>[Enum.GetValues(typeof(Sound)).Length];
    readonly int[] lastVariant;
    readonly float[] lastPlayed, pending, gap, soundVol;
    readonly int[] max;
    readonly Prio[] prio;
    readonly Cfg[] cfg;
    readonly string[] names;
    int tuneVersion = -1;

    AudioSource[] sources;
    readonly Sound[] voiceSound = new Sound[MaxVoices];
    readonly float[] voiceStart = new float[MaxVoices], voiceEnd = new float[MaxVoices];
    readonly Prio[] voicePrio = new Prio[MaxVoices];
    int voices;

    float sfxDuck = 1f, duckUntil, musicDuckUntil;
    float killRate, killLast = -10f, burstLast = -10f;
    float gemChain, gemLast = -10f;
    Camera cam;
    static readonly Dictionary<string, string> volumeKeys = new Dictionary<string, string>();   // source -> tuner key

    public static void Play(string source, Sound s, float volume = 1f, float pitchJitter = 0.06f)
    {
        if (instance == null || Settings.Volume <= 0f) return;
        float own = SourceVolume(source);
        if (own <= 0f) return;
        instance.PlayInternal(s, volume * own, pitchJitter, false, default);
    }

    // Same, but quieter and panned by where it happened on screen; far off-screen quiet sounds are skipped.
    public static void Play(string source, Sound s, Vector2 worldPos, float volume = 1f, float pitchJitter = 0.06f)
    {
        if (instance == null || Settings.Volume <= 0f) return;
        float own = SourceVolume(source);
        if (own <= 0f) return;
        instance.PlayInternal(s, volume * own, pitchJitter, true, worldPos);
    }

    static float SourceVolume(string source)
    {
        if (!volumeKeys.TryGetValue(source, out string key)) volumeKeys[source] = key = "audio.sfx." + source;
        return Tune.F(key, 1f);
    }

    // Which death sound suits an enemy; the rest fall back to the generic pop.
    public static Sound DeathFor(string enemyName)
    {
        if (enemyName.Contains("Slime")) return Sound.DieSlime;
        if (enemyName.Contains("Bat") || enemyName.Contains("Pixie")) return Sound.DieBat;
        if (enemyName.Contains("Wolf")) return Sound.DieBeast;
        return Sound.EnemyDie;
    }

    public Sfx()
    {
        int n = clips.Length;
        lastVariant = new int[n]; lastPlayed = new float[n]; pending = new float[n];
        gap = new float[n]; soundVol = new float[n]; max = new int[n]; prio = new Prio[n]; cfg = new Cfg[n];
        names = Enum.GetNames(typeof(Sound));
        for (int i = 0; i < n; i++) { clips[i] = new List<AudioClip>(); lastPlayed[i] = -10f; cfg[i] = DefaultCfg((Sound)i); }
    }

    void Awake()
    {
        instance = this;
        sources = new AudioSource[MaxVoices];
        for (int i = 0; i < MaxVoices; i++)
        {
            sources[i] = gameObject.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
            sources[i].spatialBlend = 0f;
        }
        AudioSettings.OnAudioConfigurationChanged += OnConfigChanged;
        StartCoroutine(Build());
    }

    void OnDestroy() { AudioSettings.OnAudioConfigurationChanged -= OnConfigChanged; if (instance == this) instance = null; }

    // Phone call, Siri, headphones unplugged: stop everything and let voices be reused.
    void OnConfigChanged(bool deviceWasChanged) { StopAll(); }
    void OnApplicationPause(bool paused) { AudioListener.pause = paused; if (!paused) StopAll(); }

    void StopAll()
    {
        if (sources == null) return;
        for (int i = 0; i < sources.Length; i++) { sources[i].Stop(); voiceEnd[i] = 0f; }
    }

    void Update()
    {
        float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        sfxDuck = Mathf.MoveTowards(sfxDuck, now < duckUntil ? Tune.F("audio.duck.sfx", 0.55f) : 1f, dt * 5f);
        MusicDuck = Mathf.MoveTowards(MusicDuck, now < musicDuckUntil ? Tune.F("audio.duck.music", 0.45f) : 1f, dt * 4f);
    }

    // Default behaviour of each sound; the tuner can override gap, max, priority and volume.
    static Cfg DefaultCfg(Sound s)
    {
        var c = new Cfg { gap = 0.03f, max = 4, prio = Prio.Normal, bus = Bus.Sfx, volJit = 0.08f, variants = 1 };
        switch (s)
        {
            case Sound.EnemyHit: c.gap = 0.06f; c.max = 3; c.prio = Prio.Low; c.volJit = 0.15f; c.variants = 3; break;
            case Sound.BossHit: c.gap = 0.08f; c.max = 2; c.variants = 2; break;
            case Sound.EnemyDie: case Sound.DieSlime: case Sound.DieBat: case Sound.DieBeast:
                c.gap = 0.04f; c.max = 4; c.prio = Prio.Low; c.volJit = 0.15f; c.variants = 3; break;
            case Sound.KillBurst: c.gap = 0.5f; c.max = 1; break;
            case Sound.EnemyShoot: c.gap = 0.1f; c.max = 2; c.prio = Prio.Low; c.variants = 2; break;
            case Sound.Gem: c.gap = 0.03f; c.max = 3; c.prio = Prio.Low; c.volJit = 0.05f; break;
            case Sound.WispHit: c.gap = 0.08f; c.max = 2; c.prio = Prio.Low; c.variants = 2; break;
            case Sound.Missile: c.gap = 0.04f; c.max = 3; c.variants = 3; break;
            case Sound.Explosion: c.gap = 0.08f; c.max = 3; c.variants = 2; break;
            case Sound.Lightning: c.gap = 0.05f; c.max = 3; c.variants = 2; break;
            case Sound.Comet: c.gap = 0.3f; c.max = 1; c.prio = Prio.Low; break;
            case Sound.Combo: c.gap = 0.15f; c.max = 2; break;
            case Sound.Coin: c.gap = 0.06f; c.max = 2; break;
            case Sound.Spark: c.gap = 0.1f; c.max = 2; c.prio = Prio.Low; break;
            case Sound.Sizzle: case Sound.Chill: case Sound.Splash: case Sound.Wither:
                c.gap = 0.12f; c.max = 2; c.prio = Prio.Low; break;
            case Sound.Heal: c.gap = 0.25f; c.max = 1; c.prio = Prio.Low; break;
            case Sound.Lunge: case Sound.Split: c.gap = 0.1f; c.max = 2; c.prio = Prio.Low; break;
            case Sound.LowHealth: c.gap = 2f; c.max = 1; break;
            case Sound.PlayerHurt: c.gap = 0.12f; c.max = 1; c.prio = Prio.High; break;
            case Sound.Select: c.bus = Bus.Ui; c.gap = 0.05f; c.max = 2; c.prio = Prio.High; c.volJit = 0.03f; break;
            case Sound.PlayerDie: case Sound.BossDie: case Sound.BossRoar: case Sound.LevelUp:
            case Sound.Victory: case Sound.Evolve: c.gap = 0.1f; c.max = 1; c.prio = Prio.High; break;
            case Sound.Warning: case Sound.WardBlock: case Sound.Feat: case Sound.RuneSocket: c.max = 2; break;
        }
        return c;
    }

    void RefreshTuning()
    {
        tuneVersion = Tune.Version;
        voices = Mathf.Clamp(Tune.I("audio.voices", 24), 4, MaxVoices);
        for (int i = 0; i < cfg.Length; i++)
        {
            string k = "audio.sound." + names[i] + ".";
            gap[i] = Tune.F(k + "gap", cfg[i].gap);
            max[i] = Tune.I(k + "max", cfg[i].max);
            prio[i] = (Prio)Mathf.Clamp(Tune.I(k + "priority", (int)cfg[i].prio), 0, 2);
            soundVol[i] = Tune.F(k + "volume", 1f);
        }
    }

    void PlayInternal(Sound s, float volume, float jitter, bool spatial, Vector2 pos)
    {
        int si = (int)s;
        var list = clips[si];
        if (list.Count == 0) return;   // not built yet (or nothing to play)
        if (tuneVersion != Tune.Version) RefreshTuning();

        float now = Time.unscaledTime;
        if (now - lastPlayed[si] < gap[si]) { pending[si] = Mathf.Min(pending[si] + 1f, 8f); return; }

        // Spatial: fade and pan by distance from the middle of the screen.
        float pan = 0f, att = 1f;
        if (spatial && Tune.B("audio.spatial", true))
        {
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                Vector2 c = cam.transform.position;
                float hh = Mathf.Max(0.1f, cam.orthographicSize), hw = hh * cam.aspect;
                float dx = (pos.x - c.x) / hw, dy = (pos.y - c.y) / hh;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1.6f && prio[si] == Prio.Low) return;
                att = Mathf.Clamp(1.25f - d * 0.5f, 0.3f, 1f);
                pan = Mathf.Clamp(dx, -1f, 1f) * Tune.F("audio.pan", 0.5f);
            }
        }

        // Kill chains: deaths get quieter as the rate climbs, and a big pile-up adds one deep thump.
        bool isDeath = s == Sound.EnemyDie || s == Sound.DieSlime || s == Sound.DieBat || s == Sound.DieBeast;
        if (isDeath)
        {
            killRate = killRate * Mathf.Exp(-(now - killLast) / Tune.F("audio.kill.window", 0.6f)) + 1f;
            killLast = now;
            volume *= Mathf.Max(Tune.F("audio.kill.floor", 0.5f), 1f / (1f + 0.07f * killRate));
            if (killRate >= Tune.F("audio.kill.burstAt", 7f) && now - burstLast > 0.6f)
            {
                burstLast = now;
                PlayInternal(Sound.KillBurst, 0.7f, 0.05f, false, default);
            }
        }

        // Voice accounting: count what's playing, find a free voice, the oldest of this sound, and the weakest.
        int active = 0, same = 0, free = -1, oldestSame = -1, weakest = -1;
        for (int i = 0; i < voices; i++)
        {
            if (now >= voiceEnd[i]) { if (free < 0) free = i; continue; }
            active++;
            if (voiceSound[i] == s && (oldestSame < 0 || voiceStart[i] < voiceStart[oldestSame])) { same++; oldestSame = i; }
            else if (voiceSound[i] == s) same++;
            if (weakest < 0 || voicePrio[i] < voicePrio[weakest] || (voicePrio[i] == voicePrio[weakest] && voiceStart[i] < voiceStart[weakest])) weakest = i;
        }
        var p = prio[si];
        int v;
        if (same >= Mathf.Max(1, max[si]))
        {
            if (p == Prio.Low) { pending[si] = Mathf.Min(pending[si] + 1f, 8f); return; }   // drop, don't restart
            v = oldestSame;                                                                   // steal our own oldest
        }
        else if (free >= 0) v = free;
        else if (weakest >= 0 && voicePrio[weakest] <= p) v = weakest;                       // steal the weakest
        else return;                                                                          // everything louder: drop

        // Gem pickups climb in pitch while they keep coming.
        float pitch = 1f + Random.Range(-jitter, jitter);
        if (s == Sound.Gem)
        {
            gemChain = now - gemLast < 0.4f ? Mathf.Min(gemChain + 1f, Tune.F("audio.gemChain.steps", 12f)) : 0f;
            gemLast = now;
            pitch *= 1f + gemChain * Tune.F("audio.gemChain.step", 0.035f);
        }

        // Pick a variant that isn't the same as last time.
        int vi = 0;
        if (list.Count > 1)
        {
            vi = Random.Range(0, list.Count - 1);
            if (vi >= lastVariant[si]) vi++;
            lastVariant[si] = vi;
        }
        var clip = list[vi];

        // Loudness: bus, master, headroom, fewer-louder-voices curve, ducking of quiet sounds, dropped-play boost.
        float bus = Tune.F(cfg[si].bus == Bus.Ui ? "audio.bus.ui" : "audio.bus.sfx", 1f);
        float crowd = active <= 6 ? 1f : Mathf.Sqrt(6f / active);
        float boost = 1f + pending[si] * 0.06f;
        pending[si] = 0f;
        float duck = p == Prio.Low ? sfxDuck : 1f;
        float vol = volume * soundVol[si] * bus * Tune.F("audio.volume", 1f) * Settings.Volume * Tune.F("audio.headroom", 0.8f)
                    * crowd * boost * duck * att * (1f + Random.Range(-cfg[si].volJit, cfg[si].volJit));

        var src = sources[v];
        src.clip = clip;
        src.volume = Mathf.Clamp01(vol);
        src.pitch = pitch;
        src.panStereo = pan;
        src.Play();
        voiceSound[v] = s; voiceStart[v] = now; voicePrio[v] = p;
        voiceEnd[v] = now + clip.length / Mathf.Max(0.1f, pitch);
        lastPlayed[si] = now;

        if (p == Prio.High)
        {
            float len = Mathf.Min(clip.length, 1.2f);
            duckUntil = Mathf.Max(duckUntil, now + Tune.F("audio.duck.time", 0.35f));
            if (clip.length > 0.5f) musicDuckUntil = Mathf.Max(musicDuckUntil, now + len * 0.8f);
        }
    }

    enum Wave { Sine, Square, Triangle, Noise }

    struct Job { public Sound sound; public int variants; public Func<float, AudioClip> make; }
    readonly List<Job> jobs = new List<Job>();

    // k scales the pitch of each variant (about 0.92 to 1.08) so round-robin takes differ.
    void Add(Sound s, Func<float, AudioClip> make, int variants = 0)
    {
        jobs.Add(new Job { sound = s, variants = variants > 0 ? variants : cfg[(int)s].variants, make = make });
    }

    // Sounds are made a little at a time (audio.buildBudgetMs per frame) so the first Play doesn't hitch.
    // Frequent and essential ones come first; a sound requested before it exists is skipped.
    IEnumerator Build()
    {
        // Imported files win: Resources/Sfx/<Sound>.wav or <Sound>_1.wav, <Sound>_2.wav ...
        var imported = new HashSet<Sound>();
        var files = Resources.LoadAll<AudioClip>("Sfx");
        System.Array.Sort(files, (a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (var f in files)
        {
            string n = f.name;
            int us = n.IndexOf('_');
            string baseName = us > 0 && int.TryParse(n.Substring(us + 1), out _) ? n.Substring(0, us) : n;
            if (Enum.TryParse(baseName, out Sound snd)) { clips[(int)snd].Add(f); imported.Add(snd); }
            else UnityEngine.Debug.LogWarning($"Resources/Sfx/{n}: no Sound named {baseName}");
        }

        DefineSounds();
        var sw = new Stopwatch();
        sw.Start();
        long budget = (long)Tune.F("audio.buildBudgetMs", 3f);
        foreach (var j in jobs)
        {
            if (imported.Contains(j.sound)) continue;
            for (int i = 0; i < Mathf.Max(1, j.variants); i++)
            {
                float k = j.variants <= 1 ? 1f : 1f + (i - (j.variants - 1) * 0.5f) * 0.1f;
                clips[(int)j.sound].Add(j.make(k));
                if (sw.ElapsedMilliseconds >= budget) { yield return null; sw.Restart(); }
            }
        }
        jobs.Clear();
    }

    void DefineSounds()
    {
        // UI and the busiest sounds first.
        Add(Sound.Select, k => Tone(0.07f, 700f * k, 1100f * k, Wave.Triangle, 0.3f));
        Add(Sound.EnemyHit, k => Mix(Tone(0.035f, 520f * k, 300f * k, Wave.Square, 0.07f),
                                     Tone(0.02f, 2400f, 1200f, Wave.Noise, 0.07f)));
        Add(Sound.EnemyDie, k => Tone(0.12f, 380f * k, 90f * k, Wave.Sine, 0.3f));
        Add(Sound.DieSlime, k => Mix(Tone(0.15f, 330f * k, 70f * k, Wave.Sine, 0.3f, decay: 2f),
                                     Tone(0.08f, 900f * k, 200f, Wave.Noise, 0.07f)));
        Add(Sound.DieBat, k => Tone(0.1f, 1900f * k, 700f * k, Wave.Triangle, 0.2f));
        Add(Sound.DieBeast, k => Mix(Tone(0.17f, 260f * k, 85f * k, Wave.Square, 0.16f, decay: 2.5f),
                                     Tone(0.1f, 500f, 120f, Wave.Noise, 0.1f)));
        Add(Sound.KillBurst, k => Mix(Tone(0.28f, 120f, 45f, Wave.Noise, 0.45f, decay: 3f),
                                      Tone(0.14f, 320f, 90f, Wave.Sine, 0.4f)));
        Add(Sound.Gem, k => Tone(0.06f, 1300f * k, 1900f * k, Wave.Sine, 0.15f));
        Add(Sound.PlayerHurt, k => Tone(0.25f, 160f, 60f, Wave.Square, 0.4f, decay: 2f));
        Add(Sound.Missile, k => Tone(0.09f, 900f * k, 450f * k, Wave.Triangle, 0.35f));
        Add(Sound.WispHit, k => Tone(0.05f, 1600f * k, 1200f * k, Wave.Sine, 0.18f));
        Add(Sound.EnemyShoot, k => Tone(0.07f, 600f * k, 350f * k, Wave.Triangle, 0.12f));
        Add(Sound.FireballCast, k => Tone(0.22f, 220f, 120f, Wave.Noise, 0.3f, attack: 0.05f), 1);
        Add(Sound.Explosion, k => Tone(0.4f, 90f * k, 40f * k, Wave.Noise, 0.55f, decay: 3f));
        Add(Sound.Nova, k => Tone(0.4f, 1400f, 900f, Wave.Sine, 0.3f, shimmer: 0.3f), 1);
        Add(Sound.Lightning, k => Tone(0.16f, 1800f * k, 300f, Wave.Noise, 0.4f, crackle: true));
        Add(Sound.BossHit, k => Mix(Tone(0.09f, 200f * k, 100f, Wave.Square, 0.22f),
                                    Tone(0.07f, 700f, 300f, Wave.Noise, 0.18f)));
        Add(Sound.Spark, k => Tone(0.06f, 2400f, 900f, Wave.Noise, 0.18f, crackle: true), 1);
        Add(Sound.Sizzle, k => Tone(0.14f, 3000f, 1500f, Wave.Noise, 0.12f, attack: 0.02f), 1);
        Add(Sound.Chill, k => Tone(0.1f, 2600f, 3200f, Wave.Sine, 0.12f, shimmer: 0.4f), 1);
        Add(Sound.Splash, k => Tone(0.12f, 700f, 250f, Wave.Sine, 0.18f), 1);
        Add(Sound.Wither, k => Tone(0.25f, 260f, 150f, Wave.Triangle, 0.14f, attack: 0.05f, decay: 1.5f), 1);
        Add(Sound.Comet, k => Tone(0.18f, 300f, 140f, Wave.Noise, 0.2f, attack: 0.03f, crackle: true), 1);
        Add(Sound.Combo, k => Arpeggio(new[] { 880f, 1175f, 1568f }, 0.045f, 0.3f), 1);
        Add(Sound.Coin, k => Arpeggio(new[] { 1318f, 1975f }, 0.05f, 0.25f), 1);
        Add(Sound.Heal, k => Arpeggio(new[] { 659f, 880f, 1175f }, 0.06f, 0.2f), 1);
        Add(Sound.Magnet, k => Tone(0.5f, 300f, 1500f, Wave.Sine, 0.2f, attack: 0.1f, shimmer: 0.3f), 1);
        Add(Sound.Lunge, k => Tone(0.18f, 250f, 700f, Wave.Noise, 0.22f, attack: 0.06f), 1);
        Add(Sound.Split, k => Mix(Tone(0.14f, 500f, 200f, Wave.Sine, 0.28f), Delay(Tone(0.1f, 380f, 150f, Wave.Sine, 0.22f), 0.04f)), 1);
        Add(Sound.WardBlock, k => Tone(0.3f, 1800f, 1500f, Wave.Sine, 0.35f, decay: 3f, shimmer: 0.5f), 1);
        Add(Sound.WardReady, k => Tone(0.18f, 900f, 1350f, Wave.Sine, 0.18f, shimmer: 0.3f), 1);
        Add(Sound.LowHealth, k => Mix(Tone(0.12f, 220f, 200f, Wave.Sine, 0.45f, decay: 1f),
                                      Delay(Tone(0.16f, 200f, 180f, Wave.Sine, 0.4f, decay: 1f), 0.2f)), 1);
        Add(Sound.Teleport, k => Tone(0.15f, 400f, 2000f, Wave.Sine, 0.2f), 1);
        Add(Sound.Burrow, k => Tone(0.3f, 120f, 60f, Wave.Noise, 0.25f, attack: 0.1f), 1);
        Add(Sound.LevelUp, k => Arpeggio(new[] { 523f, 659f, 784f, 1046f }, 0.08f, 0.35f), 1);
        Add(Sound.RuneSocket, k => Tone(0.25f, 1046f, 1568f, Wave.Triangle, 0.3f, shimmer: 0.4f), 1);
        Add(Sound.Feat, k => Arpeggio(new[] { 784f, 988f, 1175f, 1568f }, 0.06f, 0.3f), 1);
        Add(Sound.Warning, k => Tone(0.7f, 300f, 600f, Wave.Square, 0.25f, attack: 0.1f, decay: 1f), 1);
        Add(Sound.BossDie, k => Tone(1.0f, 200f, 30f, Wave.Noise, 0.6f, decay: 1.5f), 1);
        Add(Sound.PlayerDie, k => Mix(Tone(1.1f, 440f, 70f, Wave.Square, 0.3f, decay: 1.2f),
                                      Tone(0.8f, 180f, 40f, Wave.Noise, 0.3f, decay: 2f)), 1);
        Add(Sound.Victory, k => Arpeggio(new[] { 523f, 659f, 784f, 659f, 784f, 1046f, 1318f }, 0.11f, 0.35f), 1);
        Add(Sound.BossRoar, k => Mix(Tone(1.2f, 70f, 140f, Wave.Noise, 0.5f, attack: 0.15f, decay: 1.5f),
                                     Tone(1.2f, 110f, 82f, Wave.Square, 0.22f, attack: 0.2f, decay: 1f)), 1);
        Add(Sound.Evolve, k => Mix(Arpeggio(new[] { 392f, 523f, 659f, 784f, 1046f, 1318f }, 0.07f, 0.3f),
                                   Tone(0.9f, 60f, 200f, Wave.Noise, 0.15f, attack: 0.3f, decay: 1f)), 1);
    }

    // Two clips layered (the longer one sets the length).
    static AudioClip Mix(AudioClip a, AudioClip b)
    {
        var da = new float[a.samples];
        var db = new float[b.samples];
        a.GetData(da, 0);
        b.GetData(db, 0);
        var data = new float[Mathf.Max(da.Length, db.Length)];
        for (int i = 0; i < data.Length; i++)
            data[i] = (i < da.Length ? da[i] : 0f) + (i < db.Length ? db[i] : 0f);
        var clip = AudioClip.Create("mix", data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // The clip with silence in front of it.
    static AudioClip Delay(AudioClip a, float seconds)
    {
        int pad = Mathf.CeilToInt(seconds * Rate);
        var src = new float[a.samples];
        a.GetData(src, 0);
        var data = new float[pad + src.Length];
        System.Array.Copy(src, 0, data, pad, src.Length);
        var clip = AudioClip.Create("delay", data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // A sweep from f0 to f1 with a fast attack and an exponential-ish decay.
    static AudioClip Tone(float duration, float f0, float f1, Wave wave, float volume,
                          float attack = 0.005f, float decay = 2.5f, float shimmer = 0f, bool crackle = false)
    {
        int n = Mathf.CeilToInt(duration * Rate);
        var data = new float[n];
        double phase = 0;
        float hold = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float u = t / duration;
            float f = Mathf.Lerp(f0, f1, u);
            phase += f / Rate;
            float p = (float)(phase - System.Math.Floor(phase));

            float s;
            switch (wave)
            {
                case Wave.Square: s = p < 0.5f ? 1f : -1f; break;
                case Wave.Triangle: s = 1f - 4f * Mathf.Abs(p - 0.5f); break;
                case Wave.Noise:
                    // Sample-and-hold noise at the sweep frequency gives pitched rumble/hiss.
                    if (p < f / Rate || i == 0) hold = Random.Range(-1f, 1f);
                    s = crackle && Random.value < 0.1f ? Random.Range(-1f, 1f) : hold;
                    break;
                default: s = Mathf.Sin(p * Mathf.PI * 2f); break;
            }
            if (shimmer > 0f) s += shimmer * Mathf.Sin(t * f * 2.01f * Mathf.PI * 2f);

            float env = Mathf.Min(1f, t / attack) * Mathf.Pow(1f - u, decay);
            data[i] = s * env * volume;
        }
        var clip = AudioClip.Create("sfx", n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Arpeggio(float[] notes, float noteLength, float volume)
    {
        int per = Mathf.CeilToInt(noteLength * Rate);
        int n = per * notes.Length + per * 2;
        var data = new float[n];
        for (int k = 0; k < notes.Length; k++)
        {
            int len = k == notes.Length - 1 ? per * 3 : per;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Min(1f, t / 0.005f) * Mathf.Pow(1f - i / (float)len, 1.5f);
                data[k * per + i] += Mathf.Sin(t * notes[k] * Mathf.PI * 2f) * env * volume;
            }
        }
        var clip = AudioClip.Create("arp", n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
