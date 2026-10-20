using System.Collections.Generic;
using UnityEngine;

// Painted spell effects (Resources/Art/Dusk/vfx_<element>_*), animated in code: projectiles get a
// streaking sprite that flies head first and flickers; impacts layer a quick flash with an
// expanding, spinning ring that fades. Only with 3D Character art; otherwise spells keep their orbs.
public static class SpellVfx
{
    static readonly Dictionary<string, Sprite[]> bolts = new Dictionary<string, Sprite[]>();

    static string Key(Element e) => e == Element.Arcane ? "arcane" : null;

    static Sprite[] Bolts(string key)
    {
        if (bolts.TryGetValue(key, out var list)) return list;
        var found = new List<Sprite>();
        for (int i = 0; i < 4; i++)
        {
            var s = ArtStyle.Get($"vfx_{key}_bolt_{i}");
            if (s != null) found.Add(s);
        }
        return bolts[key] = found.ToArray();
    }

    static bool On => Settings.Enemy3D && ArtStyle.Current == ArtStyleId.Dusk;

    // Skins a player projectile for its element. Length is in world units.
    public static void Skin(Projectile p, Element e, float length)
    {
        string key = Key(e);
        if (!On || key == null) return;
        var options = Bolts(key);
        if (options.Length > 0) p.SetSkin(options[Random.Range(0, options.Length)], length);
    }

    // The hit: a bright flash and a ring of runes that spins outward and fades.
    public static void Impact(Vector2 pos, Element e, float size)
    {
        string key = Key(e);
        if (!On || key == null) return;
        var burst = ArtStyle.Get($"vfx_{key}_burst");
        var ring = ArtStyle.Get($"vfx_{key}_ring");
        if (burst != null) Puff.Spawn(pos, burst, size * 0.9f, size * 1.4f, 0.18f, 0f);
        if (ring != null) Puff.Spawn(pos, ring, size * 0.4f, size * 1.6f, 0.32f, Random.Range(-240f, 240f));
    }

    class Puff : MonoBehaviour
    {
        SpriteRenderer sr;
        float from, to, life, t, spin;

        // Spent puffs are parked and reused: every spell hit makes one or two.
        static readonly Stack<Puff> pool = new Stack<Puff>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPool() => pool.Clear();

        public static void Spawn(Vector2 pos, Sprite sprite, float fromSize, float toSize, float life, float spin)
        {
            Puff p = null;
            while (p == null && pool.Count > 0) p = pool.Pop();   // skips any destroyed while parked
            if (p == null)
            {
                var go = new GameObject("SpellPuff");
                p = go.AddComponent<Puff>();
                p.sr = SpriteFactory.Add(go, sprite, Color.white, 86);
            }
            else
            {
                p.sr.sprite = sprite;
                p.sr.color = Color.white;
                p.gameObject.SetActive(true);
            }
            p.transform.position = pos;
            p.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            p.t = 0f;
            p.from = fromSize / (sprite.rect.width / sprite.pixelsPerUnit);
            p.to = toSize / (sprite.rect.width / sprite.pixelsPerUnit);
            p.life = life;
            p.spin = spin;
            p.Step(0f);
        }

        void Update() => Step(Time.deltaTime);

        void Step(float dt)
        {
            t += dt;
            float k = Mathf.Clamp01(t / life);
            float ease = 1f - (1f - k) * (1f - k);
            transform.localScale = Vector3.one * Mathf.Lerp(from, to, ease);
            transform.Rotate(0f, 0f, spin * dt);
            var c = sr.color; c.a = 1f - k * k; sr.color = c;
            if (k >= 1f)
            {
                gameObject.SetActive(false);
                pool.Push(this);
            }
        }
    }
}
