using System.Collections.Generic;
using UnityEngine;

// "x12" tallies, with the effect's name above, that pop up where a combo or bug burst goes off,
// counting how many times that effect has fired this run. One label per effect: a new trigger while it is still showing
// moves it to the new spot and bumps the number instead of stacking another label.
public class EffectCounter : MonoBehaviour
{
    static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    static readonly Dictionary<string, EffectCounter> live = new Dictionary<string, EffectCounter>();

    string key;
    float life, maxLife;
    TextMesh text;
    Color color;

    public static void Reset()
    {
        counts.Clear();
        live.Clear();
    }

    public static void Count(string key, Vector2 at, Color color)
    {
        counts.TryGetValue(key, out int n);
        counts[key] = ++n;
        Telemetry.Effect(key);
        if (!Tune.B("hud.effectCounters.enabled", true)) return;

        if (!live.TryGetValue(key, out var c) || c == null)
        {
            var go = new GameObject("EffectCount");
            c = go.AddComponent<EffectCounter>();
            c.key = key;
            c.text = go.AddComponent<TextMesh>();
            var font = UI.Font != null ? UI.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            c.text.font = font;
            c.text.fontSize = 48;
            c.text.anchor = TextAnchor.LowerCenter;   // the name grows upward, the tally stays put
            c.text.alignment = TextAlignment.Center;
            c.text.richText = true;
            c.text.fontStyle = UI.Font != null ? FontStyle.Normal : FontStyle.Bold;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = 96;
            live[key] = c;
        }
        c.transform.position = at + new Vector2(0f, Tune.F("hud.effectCounters.offset", 0.6f));
        c.text.characterSize = Tune.F("hud.effectCounters.size", 0.061f) * UI.FontScale;
        int nameSize = Mathf.RoundToInt(c.text.fontSize * Tune.F("hud.effectCounters.nameSize", 0.7f));
        c.text.text = $"<size={nameSize}>{key}</size>\nx{n}";
        c.color = color;
        c.maxLife = c.life = Tune.F("hud.effectCounters.life", 0.9f);
    }

    void OnDestroy()
    {
        if (key != null && live.TryGetValue(key, out var c) && c == this) live.Remove(key);
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }
        transform.position += Vector3.up * Tune.F("hud.effectCounters.rise", 0.6f) * Time.deltaTime;
        // Quick pop on each bump, then fade out over the last third.
        float age = maxLife - life;
        transform.localScale = Vector3.one * (1f + 0.35f * Mathf.Clamp01(1f - age / 0.12f));
        var col = color;
        col.a = Mathf.Clamp01(life / (maxLife * 0.33f));
        text.color = col;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Reset();
}
