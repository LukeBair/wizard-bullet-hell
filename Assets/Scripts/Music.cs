using System.Collections.Generic;
using UnityEngine;

// Music for every screen, from tracks in Assets/Resources/Music/. Which file plays where comes from
// audio.music.tracks.* in the tuner (a file name without extension, "" = none), so a new track only
// needs its file added and its name typed into the tuner:
//   title, grimoire        the title and Grimoire screens (the Grimoire falls back to the title track)
//   zone.<Zone>            a zone's track (WhisperingForest, ArcaneAcademy, CrystalCaves, WitchsHollow);
//                          without one the zone plays its own chiptune takes (Biome.Music)
//   boss.<Boss>            during that boss fight (ElderTreant, HeadmasterVexley, CrystalGolem,
//                          TheHedgeWitch), else boss.any, else the zone track keeps going
//   victory, gameOver      played once on those screens
// Settings and Credits keep whatever was playing. Tracks crossfade over audio.music.fade seconds.
// audio.music.crush bitcrushes the music (not the sound effects) for a chunkier, retro sound.
public class Music : MonoBehaviour
{
    // CC BY 4.0 tracks shipped in Resources/Music, for the Credits screen: file name -> title.
    public static readonly Dictionary<string, string> Buckley = new Dictionary<string, string>
    {
        { "music_title_world_of_magic", "World of Magic" },
        { "music_zone_caves_shadows_and_dust", "Shadows and Dust" },
        { "music_boss_treant_primal_carnage", "Primal Carnage" },
        { "music_boss_vexley_fate_and_fortune", "Fate and Fortune" },
        { "music_boss_golem_juggernaut", "Juggernaut" },
    };

    AudioSource[] sources;
    Crusher[] crushers;
    int current;         // index of the source playing (or fading in) the wanted track
    string playing;      // track name on sources[current], or null
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    void Awake()
    {
        sources = new AudioSource[2];
        crushers = new Crusher[2];
        for (int i = 0; i < 2; i++)
        {
            // Each source on its own child so the crusher filters only the music.
            var go = new GameObject("Music " + i);
            go.transform.SetParent(transform, false);
            sources[i] = go.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
            sources[i].volume = 0f;
            crushers[i] = go.AddComponent<Crusher>();
        }
    }

    void Update()
    {
        var (wanted, loop) = WantedTrack();
        if (wanted != playing) Switch(wanted, loop);

        float target = Tune.F("audio.volume", 1f) * Tune.F("audio.music.volume", 0.6f) * Settings.Volume;
        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, Tune.F("audio.music.fade", 1.5f));
        float crush = Mathf.Clamp01(Tune.F("audio.music.crush", 0f));
        float mix = Mathf.Clamp01(Tune.F("audio.music.crushMix", 1f));
        for (int i = 0; i < sources.Length; i++)
        {
            var src = sources[i];
            bool on = i == current && playing != null;
            // Fade by fraction of full volume so fades take the same time whatever the volume.
            float v = on ? Mathf.MoveTowards(src.volume, target, step * Mathf.Max(target, 0.01f))
                         : Mathf.MoveTowards(src.volume, 0f, step * Mathf.Max(target, 0.01f));
            src.volume = v;
            if (!on && v <= 0f && src.isPlaying) src.Stop();
            crushers[i].Set(crush, mix);
        }
    }

    (string track, bool loop) WantedTrack()
    {
        var gm = GameManager.Instance;
        if (gm == null) return (null, true);
        switch (gm.State)
        {
            case GameState.Title:
                return (Track("title"), true);
            case GameState.Grimoire:
                return (Track("grimoire") ?? Track("title"), true);
            case GameState.Settings:
            case GameState.Credits:
                return (playing, sources[current].loop);   // keep the title or zone music going
            case GameState.TomePick:
            case GameState.Playing:
            case GameState.LevelUp:
            case GameState.Paused:
                if (gm.Arena == null) return (null, true);
                var boss = gm.Director != null ? gm.Director.Boss : null;
                string zone = Track("zone." + Key(gm.Arena.Biome.Name)) ?? gm.Arena.Biome.Track;
                if (boss != null && boss.IsAlive) return (Track("boss." + Key(boss.Def.Name)) ?? Track("boss.any") ?? zone, true);
                return (zone, true);
            case GameState.Victory:
                return (Track("victory"), false);
            case GameState.GameOver:
                return (Track("gameOver"), false);
            default:
                return (null, true);
        }
    }

    // "Witch's Hollow" -> "WitchsHollow"
    static readonly Dictionary<string, string> keys = new Dictionary<string, string>();
    static string Key(string name)
    {
        if (keys.TryGetValue(name, out var k)) return k;
        var sb = new System.Text.StringBuilder();
        foreach (char c in name) if (char.IsLetterOrDigit(c)) sb.Append(c);
        return keys[name] = sb.ToString();
    }

    // The file for a slot, or null when the slot is empty or its file isn't in the build.
    string Track(string slot)
    {
        string name = Tune.S("audio.music.tracks." + slot, "");
        return name.Length > 0 && Clip(name) != null ? name : null;
    }

    AudioClip Clip(string name)
    {
        if (clips.TryGetValue(name, out var clip)) return clip;
        clip = Resources.Load<AudioClip>("Music/" + name);
        if (clip == null) Debug.LogWarning($"Music track not found: Resources/Music/{name}");
        return clips[name] = clip;
    }

    void Switch(string track, bool loop)
    {
        playing = track;
        if (track == null) return;   // the current source just fades out
        current = 1 - current;
        var src = sources[current];
        src.Stop();
        src.volume = 0f;
        src.loop = loop;
        src.clip = Clip(track);
        if (src.clip != null) src.Play();
    }
}

// Bit depth and sample-rate reduction on one music source. Values come in from the main thread;
// OnAudioFilterRead runs on the audio thread and only reads these fields.
public class Crusher : MonoBehaviour
{
    volatile float bits = 16f;   // quantization depth
    volatile int hold = 1;       // keep every Nth sample frame
    volatile float mix;          // 0 = bypass
    float[] held = new float[8];
    int counter;

    // crush 0..1: 16 bits / every sample (clean) -> 4 bits / every 16th sample.
    public void Set(float crush, float wet)
    {
        bits = Mathf.Lerp(16f, 4f, crush);
        hold = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1f, 16f, crush)));
        mix = crush > 0f ? wet : 0f;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        float m = mix;
        if (m <= 0f) return;
        int h = hold;
        float levels = Mathf.Pow(2f, bits - 1f);
        if (held.Length < channels) held = new float[channels];
        for (int i = 0; i < data.Length; i += channels)
        {
            if (counter == 0)
                for (int c = 0; c < channels; c++)
                    held[c] = Mathf.Round(data[i + c] * levels) / levels;
            counter = (counter + 1) % h;
            for (int c = 0; c < channels; c++)
                data[i + c] += (held[c] - data[i + c]) * m;
        }
    }
}
