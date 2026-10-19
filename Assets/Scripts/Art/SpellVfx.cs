using System.Collections.Generic;
using UnityEngine;

// Painted spell effects (Resources/Art/Dusk/vfx_<element>_*), animated in code: projectiles get a
// streaking sprite that flies head first and flickers; impacts layer a quick flash with an
// expanding, spinning ring that fades. Only with 3D Character art; otherwise spells keep their orbs.
public static class SpellVfx
{
    static readonly Dictionary<string, Sprite[]> bolts = new Dictionary<string, Sprite[]>();

    static string Key(Element e) => e == Element.Arcane ? "arcane" : e == Element.Fire ? "fireball" : null;

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
        var head = Frames($"vfx_{key}_bolt", 12);
        if (head.Length > 0) p.SetSkin(head, length);
    }

    // The hit: an animated burst, played frame by frame.
    // Returns false when there's no painted effect, so callers can fall back to the plain flash.
    public static bool Impact(Vector2 pos, Element e, float size)
    {
        string key = Key(e);
        if (!On || key == null) return false;
        var frames = Frames($"vfx_{key}_hit", 12);
        if (frames.Length == 0) return false;
        Puff.Spawn(pos, frames, size * 1.6f, 0.3f);
        return true;
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
            go.transform.localScale = Vector3.one * p.full * 0.06f;
            p.life = life;
        }

        void Update()
        {
            t += Time.deltaTime;
            int i = (int)(t / life * frames.Length);
            if (i >= frames.Length) { Destroy(gameObject); return; }
            sr.sprite = frames[i];
            // starts as a speck and grows over most of its life, fast at first (ease-out), so the growth reads
            float k = t / life, grow = Mathf.Clamp01(k / 0.7f);
            float ease = 1f - (1f - grow) * (1f - grow);
            transform.localScale = Vector3.one * full * Mathf.Lerp(0.06f, 0.9f, ease);
            sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - k) / 0.3f));   // fade out over the last 30%
        }
    }
}
