using System.Collections.Generic;
using UnityEngine;

// Painted spell effects (Resources/Art/Dusk/vfx_<element>_*), animated in code: projectiles get a
// streaking sprite that flies head first and flickers; impacts layer a quick flash with an
// expanding, spinning ring that fades. Only with 3D Character art; otherwise spells keep their orbs.
public static class SpellVfx
{
    static readonly Dictionary<string, Sprite[]> bolts = new Dictionary<string, Sprite[]>();

    static string Key(Element e) => e == Element.Arcane ? "arcane" : e == Element.Fire ? "fireball" : e == Element.Frost ? "frost" : e == Element.Storm ? "lightning" : e == Element.Nature ? "nature" : null;

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

    public static bool On => Settings.Enemy3D && ArtStyle.Current == ArtStyleId.Dusk;

    // Skins a player projectile for its element: an animated painted bolt, head first.
    // Length is in world units.
    public static void Skin(Projectile p, Element e, float length)
    {
        string key = Key(e);
        if (!On || key == null) return;
        var head = Frames($"vfx_{key}_bolt", 12);
        if (head.Length > 0) p.SetSkin(PixelDensity.Fit(head, length / (head[0].rect.width / head[0].pixelsPerUnit), false), length);
    }

    static readonly Dictionary<Sprite, (Vector2 head, float tilt)> heads = new Dictionary<Sprite, (Vector2, float)>();

    // Where the bolt's head (the left quarter of its painted extent) sits from the sprite's centre, in sprite
    // units, and how far the head-to-tail axis is tilted off the image's horizontal (degrees), averaged over
    // the frames. The projectile pivots about the head and cancels the tilt so it flies down its own line.
    public static (Vector2 head, float tilt) Axis(Sprite[] frames)
    {
        if (frames.Length == 0) return (Vector2.zero, 0f);
        if (heads.TryGetValue(frames[0], out var cached)) return cached;
        Vector2 sum = Vector2.zero, tail = Vector2.zero;
        foreach (var f in frames) { var (h, t) = Ends(f); sum += h; tail += t; }
        sum /= frames.Length; tail /= frames.Length;
        var d = tail - sum;
        return heads[frames[0]] = (sum, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    // Centroids of the opaque pixels in the left and right quarters of the sprite's extent, from its pivot, in sprite units.
    static (Vector2, Vector2) Ends(Sprite s)
    {
        var r = s.textureRect;
        int w = (int)r.width, h = (int)r.height;
        var rt = RenderTexture.GetTemporary(s.texture.width, s.texture.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(s.texture, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels32();
        Object.Destroy(tex);

        int minX = w, maxX = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 100) { if (x < minX) minX = x; if (x > maxX) maxX = x; }
        if (maxX < 0) return (Vector2.zero, Vector2.right);
        int q = Mathf.Max(1, (maxX - minX + 1) / 4);
        Vector2 End(int from, int to)
        {
            float sx = 0, sy = 0, n = 0;
            for (int y = 0; y < h; y++)
                for (int x = from; x <= to; x++)
                    if (px[y * w + x].a > 100) { sx += x + 0.5f; sy += y + 0.5f; n++; }
            return n < 1 ? Vector2.zero : new Vector2(sx / n - s.pivot.x, sy / n - s.pivot.y) / s.pixelsPerUnit;
        }
        return (End(minX, minX + q), End(maxX - q, maxX));
    }

    // The hit: an animated burst, played frame by frame.
    // Returns false when there's no painted effect, so callers can fall back to the plain flash.
    public static bool Impact(Vector2 pos, Element e, float size, float life = 0.3f, float fade = 0.3f)
    {
        string key = Key(e);
        return key != null && Play($"vfx_{key}_hit", pos, size * 1.6f * Tune.F($"vfx.{key}HitScale", 1f), life, fade);   // tuner: vfx.<element>HitScale
    }

    // The frost nova's expanding ring, `radius` world units out at its widest.
    public static bool Nova(Vector2 pos, float radius) => Play("vfx_frostnova_hit", pos, radius * 2f / 0.9f, 0.4f, 0.4f);

    static bool Play(string name, Vector2 pos, float full, float life, float fade)
    {
        if (!On) return false;
        var frames = Frames(name, 12);
        if (frames.Length == 0) return false;
        // the burst ends about 0.9 of its full size; that's the scale its pixels are judged at
        Puff.Spawn(pos, PixelDensity.Fit(frames, full * 0.9f / (frames[0].rect.width / frames[0].pixelsPerUnit), false), full, life, fade);
        return true;
    }

    // A lightning arc between two points: the painted line stretched from a to b, re-forking frame by frame.
    // Returns false when there's no painted line, so callers can fall back to the plain one.
    public static bool Arc(Vector2 a, Vector2 b, float life = 0.22f)
    {
        if (!On) return false;
        var frames = Frames("vfx_lightning_bolt", 12);
        if (frames.Length == 0) return false;
        float dist = Vector2.Distance(a, b);
        if (dist < 0.05f) return false;
        float unitsLong = frames[0].rect.width / frames[0].pixelsPerUnit, unitsHigh = frames[0].rect.height / frames[0].pixelsPerUnit;
        // one uniform scale, so the texels stay the game's size: long arcs tile the line end to end, short ones shrink the whole line
        float sy = Mathf.Min(0.8f / unitsHigh, dist / unitsLong), seg = unitsLong * sy;
        var dir = (b - a) / dist;
        var rot = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        var fit = PixelDensity.Fit(frames, sy, false);
        int n = Mathf.Max(1, Mathf.CeilToInt(dist / seg - 0.001f));
        for (int k = 0; k < n; k++)
        {
            var go = new GameObject("SpellArc");
            go.transform.position = k < n - 1 ? a + dir * (seg * (k + 0.5f)) : b - dir * (seg * 0.5f);   // the last tile is flush with b and overlaps its neighbour
            go.transform.rotation = rot;
            go.transform.localScale = new Vector3(sy, sy, 1f);
            var arc = go.AddComponent<Puff>();
            arc.frames = fit;
            arc.sr = SpriteFactory.Add(go, fit[0], Color.white, 90);
            arc.life = life; arc.fade = 0.5f; arc.line = true; arc.phase = k * 5;   // each tile forks differently
        }
        return true;
    }

    // ---- Forest Wisps: the painted orb, recoloured per absorbed element (reaction bit: 1 fire, 2 water, 4 frost, 8 storm, 16 earth) ----
    static readonly Dictionary<(int, int), (Sprite[] frames, Vector2 orb, float scale)> wisps = new Dictionary<(int, int), (Sprite[], Vector2, float)>();

    // hue, saturation and brightness multipliers that turn the green wisp into each element's colour
    static (float hue, float sat, float val) WispShift(int reaction) =>
        reaction == 1 ? (0.05f, 1f, 1f) : reaction == 2 ? (0.60f, 1f, 1f) : reaction == 4 ? (0.50f, 0.55f, 1.05f)
        : reaction == 8 ? (0.16f, 1f, 1.05f) : reaction == 16 ? (0.09f, 0.7f, 0.8f) : (-1f, 1f, 1f);

    static (Sprite[] frames, Vector2 orb, float scale) WispFrames(int reaction, float width)
    {
        var key = (reaction, Mathf.RoundToInt(width * 100f) * 1000 + PixelDensity.Key);
        if (wisps.TryGetValue(key, out var hit)) return hit;
        var src = Frames("vfx_nature_bolt", 12);
        if (src.Length == 0) return wisps[key] = (src, Vector2.zero, 1f);
        float scale = width / (src[0].rect.width / src[0].pixelsPerUnit);
        var (hue, sat, val) = WispShift(reaction);
        var shifted = new Sprite[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            if (hue < 0f) { shifted[i] = src[i]; continue; }
            var r = src[i].textureRect; int w = (int)r.width, h = (int)r.height;
            var rt = RenderTexture.GetTemporary(src[i].texture.width, src[i].texture.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src[i].texture, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            var px = tex.GetPixels32();
            for (int k = 0; k < px.Length; k++)
            {
                Color.RGBToHSV(px[k], out _, out float sv, out float vv);
                var c = Color.HSVToRGB(hue, Mathf.Clamp01(sv * sat), Mathf.Clamp01(vv * val));
                px[k] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), px[k].a);
            }
            tex.SetPixels32(px); tex.Apply();
            shifted[i] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), src[i].pixelsPerUnit);
        }
        Vector2 orb = Ends(src[0]).Item2 * scale;   // the orb is the right end; the tail streams out to the left
        return wisps[key] = (PixelDensity.Fit(shifted, scale, false), orb, scale);
    }

    // Swaps a wisp's body for the painted one, `width` world units from head to tail tip. It's drawn at that size and
    // resampled for it, so its texels match the rest of the art. False when there's no art, so the caller keeps its plain orb.
    public static bool WispBody(Transform orb, float width)
    {
        if (!On || Frames("vfx_nature_bolt", 12).Length == 0) return false;
        var go = new GameObject("Painted");
        go.transform.SetParent(orb, false);
        var fx = go.AddComponent<WispBodyFx>();
        fx.width = width;
        fx.sr = SpriteFactory.Add(go, Frames("vfx_nature_bolt", 12)[0], Color.white, 57);
        fx.Set(0);
        return true;
    }

    public static void WispTint(Transform orb, int reaction)
    {
        var fx = orb.GetComponentInChildren<WispBodyFx>(true);
        if (fx != null) fx.Set(reaction);
    }

    class WispBodyFx : MonoBehaviour
    {
        internal SpriteRenderer sr;
        internal float width;
        Sprite[] frames; Vector2 orb; float scale = 1f, angle; Vector2 last; bool has; readonly int phase = Random.Range(0, 12);

        internal void Set(int reaction) { (frames, orb, scale) = WispFrames(reaction, width); Place(); }

        void Place()
        {
            float k = 1f / Mathf.Max(0.0001f, transform.parent.lossyScale.x);   // orb and scale are in world units
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            transform.localScale = new Vector3(scale * k, scale * k, 1f);   // the size the frames were resampled for
            transform.localPosition = -(transform.localRotation * (Vector3)orb) * k;   // the orb itself sits on the wisp's spot
        }

        void Update()
        {
            if (frames == null || frames.Length == 0) return;
            Vector2 p = transform.parent.localPosition, v = has ? p - last : Vector2.zero;
            last = p; has = true;
            if (v.sqrMagnitude > 1e-6f) angle = Mathf.LerpAngle(angle, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg, 12f * Time.deltaTime);   // the tail trails behind its motion
            sr.sprite = frames[(int)(Time.time * 12f + phase) % frames.Length];
            Place();
        }
    }

    class Puff : MonoBehaviour
    {
        internal SpriteRenderer sr;
        internal Sprite[] frames;
        internal float life, fade;
        internal int phase;
        float t, full;
        public bool line;   // an arc: keeps its scale and just flickers through its frames

        public static void Spawn(Vector2 pos, Sprite[] frames, float size, float life, float fade)
        {
            var go = new GameObject("SpellPuff");
            go.transform.position = pos;
            var p = go.AddComponent<Puff>();
            p.frames = frames;
            p.sr = SpriteFactory.Add(go, frames[0], Color.white, 86);
            p.full = size / (frames[0].rect.width / frames[0].pixelsPerUnit);
            go.transform.localScale = Vector3.one * p.full * 0.9f;   // fixed: the frames already grow, and a changing scale would break the pixel grid
            p.life = life;
            p.fade = fade;
        }

        void Update()
        {
            t += Time.deltaTime;
            int i = (int)(t / life * frames.Length);
            if (i >= frames.Length) { Destroy(gameObject); return; }
            sr.sprite = frames[(i + phase) % frames.Length];
            if (line) { sr.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t / life) / fade))); return; }
            float k = t / life;
            sr.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - k) / fade)));   // fades out over the last `fade` of its life
        }
    }
}
