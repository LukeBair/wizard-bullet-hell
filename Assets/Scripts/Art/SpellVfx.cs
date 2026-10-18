using System.Collections.Generic;
using UnityEngine;

// Painted spell effects (Resources/Art/Dusk/vfx_<element>_*), animated in code: projectiles get a
// streaking sprite that flies head first and flickers; impacts layer a quick flash with an
// expanding, spinning ring that fades. Only with 3D Character art; otherwise spells keep their orbs.
public static class SpellVfx
{
    static readonly Dictionary<string, Sprite[]> bolts = new Dictionary<string, Sprite[]>();

    static string Key(Element e) => e == Element.Arcane ? "arcane" : null;

    static Sprite[] Frames(string name, int max)
    {
        if (bolts.TryGetValue(name, out var list)) return list;
        var found = new List<Sprite>();
        for (int i = 0; i < max; i++)
        {
            var s = ArtStyle.Get($"{name}_{i}");
            if (s != null) found.Add(s);
        }
        return bolts[name] = found.ToArray();
    }

    static bool On => Settings.Enemy3D && ArtStyle.Current == ArtStyleId.Dusk;

    // Skins a player projectile for its element: an animated painted bolt, head first.
    // Length is in world units.
    public static void Skin(Projectile p, Element e, float length)
    {
        string key = Key(e);
        if (!On || key == null) return;
        var head = Frames($"vfx_{key}_bolt", 8);
        if (head.Length > 0) p.SetSkin(head, length);
    }

    // The hit: an animated burst, played frame by frame.
    public static void Impact(Vector2 pos, Element e, float size)
    {
        string key = Key(e);
        if (!On || key == null) return;
        var frames = Frames($"vfx_{key}_hit", 12);
        if (frames.Length > 0) Puff.Spawn(pos, frames, size * 1.8f, 0.5f);
    }

    class Puff : MonoBehaviour
    {
        SpriteRenderer sr;
        Sprite[] frames;
        float life, t, full;

        public static void Spawn(Vector2 pos, Sprite[] frames, float size, float life)
        {
            var go = new GameObject("SpellPuff");
            go.transform.position = pos;
            var p = go.AddComponent<Puff>();
            p.frames = frames;
            p.sr = SpriteFactory.Add(go, frames[0], Color.white, 86);
            p.full = size / (frames[0].rect.width / frames[0].pixelsPerUnit);
            go.transform.localScale = Vector3.one * p.full * 0.05f;
            p.life = life;
        }

        void Update()
        {
            t += Time.deltaTime;
            int i = (int)(t / life * frames.Length);
            if (i >= frames.Length) { Destroy(gameObject); return; }
            sr.sprite = frames[i];
            // explodes out from a point: bursts to a slight overshoot in the first ~15%, settles, then drifts
            float k = t / life, grow = Mathf.Clamp01(k / 0.15f);
            float burst = 1f - (1f - grow) * (1f - grow) * (1f - grow);
            float overshoot = 0.15f * Mathf.Sin(grow * Mathf.PI);
            transform.localScale = Vector3.one * full * (0.05f + 0.8f * burst + overshoot + 0.15f * k);
        }
    }
}
