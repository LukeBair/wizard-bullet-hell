using System.Collections.Generic;
using UnityEngine;

public enum Sound
{
    Missile, FireballCast, Explosion, Nova, Lightning, WispHit,
    EnemyHit, EnemyDie, EnemyShoot, PlayerHurt, Gem, LevelUp, Select,
    Warning, Teleport, Burrow, BossDie,
}

// Placeholder sound effects, synthesized at startup so there are no audio assets to manage.
// Each sound is rate limited so a screen full of enemies doesn't turn into noise.
public class Sfx : MonoBehaviour
{
    const int Rate = 44100;
    const int Voices = 20;

    static Sfx instance;
    readonly Dictionary<Sound, AudioClip> clips = new Dictionary<Sound, AudioClip>();
    readonly Dictionary<Sound, float> lastPlayed = new Dictionary<Sound, float>();
    AudioSource[] sources;
    int next;

    public static void Play(Sound s, float volume = 1f, float pitchJitter = 0.06f)
    {
        if (instance == null) return;
        instance.PlayInternal(s, volume, pitchJitter);
    }

    void Awake()
    {
        instance = this;
        sources = new AudioSource[Voices];
        for (int i = 0; i < Voices; i++)
        {
            sources[i] = gameObject.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
        }
        Build();
    }

    void PlayInternal(Sound s, float volume, float jitter)
    {
        float gap = MinGap(s);
        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(s, out float t) && now - t < gap) return;
        lastPlayed[s] = now;

        var src = sources[next];
        next = (next + 1) % Voices;
        src.pitch = 1f + Random.Range(-jitter, jitter);
        src.PlayOneShot(clips[s], volume);
    }

    static float MinGap(Sound s)
    {
        switch (s)
        {
            case Sound.EnemyHit: return 0.07f;
            case Sound.EnemyShoot: return 0.12f;
            case Sound.EnemyDie: return 0.05f;
            case Sound.Gem: return 0.04f;
            case Sound.WispHit: return 0.08f;
            case Sound.Explosion: return 0.08f;
            default: return 0.03f;
        }
    }

    enum Wave { Sine, Square, Triangle, Noise }

    void Build()
    {
        clips[Sound.Missile] = Tone(0.09f, 900f, 450f, Wave.Triangle, 0.35f);
        clips[Sound.FireballCast] = Tone(0.22f, 220f, 120f, Wave.Noise, 0.3f, attack: 0.05f);
        clips[Sound.Explosion] = Tone(0.4f, 90f, 40f, Wave.Noise, 0.55f, decay: 3f);
        clips[Sound.Nova] = Tone(0.4f, 1400f, 900f, Wave.Sine, 0.3f, shimmer: 0.3f);
        clips[Sound.Lightning] = Tone(0.16f, 1800f, 300f, Wave.Noise, 0.4f, crackle: true);
        clips[Sound.WispHit] = Tone(0.05f, 1600f, 1200f, Wave.Sine, 0.18f);
        clips[Sound.EnemyHit] = Tone(0.035f, 500f, 300f, Wave.Square, 0.08f);
        clips[Sound.EnemyDie] = Tone(0.12f, 380f, 90f, Wave.Sine, 0.3f);
        clips[Sound.EnemyShoot] = Tone(0.07f, 600f, 350f, Wave.Triangle, 0.12f);
        clips[Sound.PlayerHurt] = Tone(0.25f, 160f, 60f, Wave.Square, 0.4f, decay: 2f);
        clips[Sound.Gem] = Tone(0.06f, 1300f, 1900f, Wave.Sine, 0.15f);
        clips[Sound.LevelUp] = Arpeggio(new[] { 523f, 659f, 784f, 1046f }, 0.08f, 0.35f);
        clips[Sound.Select] = Tone(0.07f, 700f, 1100f, Wave.Triangle, 0.3f);
        clips[Sound.Warning] = Tone(0.7f, 300f, 600f, Wave.Square, 0.25f, attack: 0.1f, decay: 1f);
        clips[Sound.Teleport] = Tone(0.15f, 400f, 2000f, Wave.Sine, 0.2f);
        clips[Sound.Burrow] = Tone(0.3f, 120f, 60f, Wave.Noise, 0.25f, attack: 0.1f);
        clips[Sound.BossDie] = Tone(1.0f, 200f, 30f, Wave.Noise, 0.6f, decay: 1.5f);
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
