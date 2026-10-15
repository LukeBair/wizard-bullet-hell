using UnityEngine;

// Looping zone music. Each biome names one or more takes in Assets/Resources/Music/; one plays while a
// run is in that zone, straight through its boss fight, and crossfades on zone changes. The title,
// game over and victory screens are silent. Zones without a track are silent too.
public class Music : MonoBehaviour
{
    AudioSource[] sources;
    int current;         // index of the source playing (or fading in) the wanted track
    string playing;      // track name on sources[current], or null

    void Awake()
    {
        sources = new AudioSource[2];
        for (int i = 0; i < 2; i++)
        {
            sources[i] = gameObject.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
            sources[i].loop = true;
            sources[i].volume = 0f;
        }
    }

    void Update()
    {
        string wanted = WantedTrack();
        if (wanted != playing) Switch(wanted);

        float target = Tune.F("audio.volume", 1f) * Tune.F("audio.music.volume", 0.6f);
        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, Tune.F("audio.music.fade", 1.5f));
        for (int i = 0; i < sources.Length; i++)
        {
            var src = sources[i];
            bool on = i == current && playing != null;
            // Fade by fraction of full volume so fades take the same time whatever the volume.
            float v = on ? Mathf.MoveTowards(src.volume, target, step * Mathf.Max(target, 0.01f))
                         : Mathf.MoveTowards(src.volume, 0f, step * Mathf.Max(target, 0.01f));
            src.volume = v;
            if (!on && v <= 0f && src.isPlaying) src.Stop();
        }
    }

    static string WantedTrack()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Arena == null) return null;
        switch (gm.State)
        {
            case GameState.TomePick:
            case GameState.Playing:
            case GameState.LevelUp:
            case GameState.Paused:
                return gm.Arena.Biome.Track;
            default:
                return null;
        }
    }

    void Switch(string track)
    {
        playing = track;
        if (track == null) return;   // the current source just fades out
        current = 1 - current;
        var src = sources[current];
        src.Stop();
        src.volume = 0f;
        src.clip = Resources.Load<AudioClip>("Music/" + track);
        if (src.clip == null) Debug.LogWarning($"Music track not found: Resources/Music/{track}");
        else src.Play();
    }
}
