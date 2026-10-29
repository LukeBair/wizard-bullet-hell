using System.Collections.Generic;
using UnityEngine;

// Painted spell effects (Resources/Art/Dusk/vfx_<element>_*), animated in code: projectiles get a
// streaking sprite that flies head first and flickers; impacts layer a quick flash with an
// expanding, spinning ring that fades. Only with 3D Character art; otherwise spells keep their orbs.
// A painted effect: plays `intro` once, then cycles `loop` (or holds intro's last frame). With a life it fades
// over its last quarter and removes itself; without one it runs until its owner turns it off or destroys it.
public class PaintedLoop : MonoBehaviour
{
    public SpriteRenderer Sr { get; private set; }
    Sprite[] intro, loop;
    float fps, loopFps, t, life, alpha;

    public static PaintedLoop Attach(Transform parent, Vector2 at, Sprite[] intro, Sprite[] loop, float scale, float fps, int order, float life = 0f, float alpha = 1f)
    {
        var go = new GameObject("Painted");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = at;
        go.transform.localScale = Vector3.one * scale;   // the frames are fitted for this scale
        var p = go.AddComponent<PaintedLoop>();
        p.intro = intro;
        p.loop = loop;
        p.fps = p.loopFps = fps;
        p.life = life;
        p.alpha = alpha;
        p.Sr = SpriteFactory.Add(go, (intro ?? loop)[0], new Color(1f, 1f, 1f, alpha), order);
        p.Sr.flipX = Random.value < 0.5f;
        return p;
    }

    // The loop's own speed, when it differs from the intro's.
    public PaintedLoop Hold(float fps) { loopFps = fps; return this; }

    // Plays the intro again from the start.
    public void Restart() => t = 0f;

    // Starts `seconds` into the effect, so copies side by side don't move in step.
    public PaintedLoop Skip(float seconds) { t = seconds; return this; }

    void Update()
    {
        t += Time.deltaTime;
        int n = intro?.Length ?? 0;
        float introTime = n / fps;
        if (t < introTime) Sr.sprite = intro[(int)(t * fps)];
        else if (loop != null) Sr.sprite = loop[(int)((t - introTime) * loopFps) % loop.Length];
        else Sr.sprite = intro[n - 1];
        if (life <= 0f) return;
        if (t >= life) { Destroy(gameObject); return; }
        Sr.color = new Color(1f, 1f, 1f, alpha * Mathf.Clamp01((life - t) / (life * 0.25f)));
    }
}

public static class SpellVfx
{
    static readonly Dictionary<string, Sprite[]> bolts = new Dictionary<string, Sprite[]>();

    static string Key(Element e) => e == Element.Arcane ? "arcane" : e == Element.Fire ? "fireball" : e == Element.Frost ? "frost" : e == Element.Storm ? "lightning" : e == Element.Nature ? "nature" : e == Element.Water ? "water" : null;

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

    // Banish: the shadow vortex opening and collapsing, `size` world units across at its widest.
    public static bool Banish(Vector2 pos, float size) => Play("vfx_banish_hit", pos, size * Tune.F("vfx.banishSize", 1f), 0.9f, 0.15f);

    // The frost nova's expanding ring, `radius` world units out at its widest.
    public static bool Nova(Vector2 pos, float radius) => Play("vfx_frostnova_hit", pos, radius * 2f / 0.9f, 0.4f, 0.4f);

    static bool Play(string name, Vector2 pos, float full, float life, float fade, float angle = 0f)
    {
        if (!On) return false;
        var frames = Frames(name, 12);
        if (frames.Length == 0) return false;
        // the burst ends about 0.9 of its full size; that's the scale its pixels are judged at
        var p = Puff.Spawn(pos, PixelDensity.Fit(frames, full * 0.9f / (frames[0].rect.width / frames[0].pixelsPerUnit), false), full, life, fade);
        if (angle != 0f) p.transform.rotation = Quaternion.Euler(0f, 0f, angle);
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
            var arc = Puff.Take(fit, 90);
            var t = arc.transform;
            t.position = k < n - 1 ? a + dir * (seg * (k + 0.5f)) : b - dir * (seg * 0.5f);   // the last tile is flush with b and overlaps its neighbour
            t.rotation = rot;
            t.localScale = new Vector3(sy, sy, 1f);
            arc.life = life; arc.fade = 0.5f; arc.line = true; arc.phase = k * 5;   // each tile forks differently
        }
        return true;
    }

    // A quick lightning jump between two foes (Static sparks, wet arcs): the painted arc and a small hit,
    // or the plain line when there's no painted art.
    public static void Spark(Vector2 a, Vector2 b, Color fallback)
    {
        if (Arc(a, b, 0.18f)) Impact(b, Element.Storm, 0.2f);
        else Fx.Bolt(a, b, fallback);
    }

    // ---- Tidal Lance: the painted jet, whole tiles laid end to end along the beam ----

    // Frames and scale for a beam `length` long; the scale is nudged so whole tiles fill it exactly.
    // The first tile takes the start frames (narrowing to the wizard), the last the tip frames (pointed).
    public static bool BeamArt(float length, float width, out Sprite[] frames, out Sprite[] start, out Sprite[] tip, out float scale, out int tiles)
    {
        frames = start = tip = null; scale = 0f; tiles = 0;
        if (!On) return false;
        var src = Frames("vfx_water_beam", 12);
        var s0 = Frames("vfx_water_beamstart", 12);
        var s1 = Frames("vfx_water_beamtip", 12);
        if (src.Length == 0 || s0.Length != src.Length || s1.Length != src.Length) return false;
        float unitsLong = src[0].rect.width / src[0].pixelsPerUnit, unitsHigh = src[0].rect.height / src[0].pixelsPerUnit;
        float want = width * Tune.F("vfx.waterBeamWidth", 2.2f) / unitsHigh;   // the strip includes the spray around the jet
        tiles = Mathf.Max(2, Mathf.RoundToInt(length / (unitsLong * want)));
        scale = length / (tiles * unitsLong);
        frames = PixelDensity.Fit(src, scale, false);
        start = PixelDensity.Fit(s0, scale, false);
        tip = PixelDensity.Fit(s1, scale, false);
        return true;
    }

    // ---- Spectral Blades: the painted crescent (one turn in steps, picked by the blade's spin) and its slash ----

    public static Sprite[] BladeFrames(float size)
    {
        if (!On) return null;
        var src = Frames("vfx_blades_spin", 12);
        if (src.Length == 0) return null;
        return PixelDensity.Fit(src, BladeScale(src, size), false);
    }

    public static float BladeScale(Sprite[] src, float size) => size * Tune.F("vfx.bladeSize", 3f) / (src[0].rect.width / src[0].pixelsPerUnit);
    public static float BladeScale(float size) { var src = Frames("vfx_blades_spin", 12); return src.Length == 0 ? 1f : BladeScale(src, size); }

    // Each slash at its own random angle, so several on one foe cross instead of stacking into one line.
    public static bool Slash(Vector2 pos, float size) => Play("vfx_blades_hit", pos, size * Tune.F("vfx.bladesHitScale", 1f), 0.3f, 0.6f, Random.Range(0f, 360f));

    // ---- Comet Trail: a looping patch of flame ----

    public static Sprite[] FlameFrames(float radius, out float scale)
    {
        scale = 1f;
        if (!On) return null;
        var src = Frames("vfx_flame", 12);
        if (src.Length == 0) return null;
        scale = radius * Tune.F("vfx.flameSize", 3f) / (src[0].rect.width / src[0].pixelsPerUnit);
        return PixelDensity.Fit(src, scale, false);
    }

    // ---- The utility spells: painted loops that sit on the ground, a foe or the wizard ----

    // `name`'s frames fitted so a frame is `size` world units across; null without painted art.
    public static Sprite[] Sized(string name, float size, out float scale)
    {
        scale = 1f;
        if (!On) return null;
        var src = Frames(name, 24);
        if (src.Length == 0) return null;
        scale = size / (src[0].rect.width / src[0].pixelsPerUnit);
        return PixelDensity.Fit(src, scale, false);
    }

    // Rejuvenate: the heal burst spreading out on the ground around the wizard's feet (the frames are baked
    // as a flat oval, so they lie on the floor in the game's perspective).
    public static bool Heal(Transform wizard)
    {
        var f = Sized("vfx_rejuv", Tune.F("vfx.rejuvSize", 3f), out float s);
        if (f == null) return false;
        PaintedLoop.Attach(wizard, new Vector2(0f, -0.45f), f, null, s, f.Length / 0.9f, -46, 0.9f);
        return true;
    }

    // Overgrowth: vines bursting up on the ground in a ring `radius` across, gone again quickly (the vines that
    // hold each trapped foe are its own, small, VineTrap).
    public static bool Vines(Vector2 at, float radius)
    {
        var rise = Sized("vfx_overgrowth_rise", radius * 2f * Tune.F("vfx.overgrowthSize", 1.15f), out float s);
        if (rise == null) return false;
        var hold = Sized("vfx_overgrowth_hold", radius * 2f * Tune.F("vfx.overgrowthSize", 1.15f), out _);
        PaintedLoop.Attach(null, at, rise, hold, s, 20f, -47, Tune.F("vfx.overgrowthTime", 0.8f)).Hold(8f);
        return true;
    }

    // Overgrowth: small vines wrapped round a rooted foe of body size `size` (its owner shows, hides and restarts it).
    public static PaintedLoop VineTrap(Transform foe, float size, int order)
    {
        float across = FoeSize(size, Tune.F("vfx.vineTrapSize", 1.6f));
        var rise = Sized("vfx_overgrowth_rise", across, out float s);
        if (rise == null) return null;
        return PaintedLoop.Attach(foe, Vector2.zero, rise, Sized("vfx_overgrowth_hold", across, out _), s, 20f, order).Hold(8f);
    }

    // Veil: the shimmering veil wrapping the wizard as it phases out; shown for vfx.veilTime at most.
    public static bool Veil(Transform wizard, float life)
    {
        var f = Sized("vfx_veil", Tune.F("vfx.veilSize", 2.2f), out float s);
        if (f == null) return false;
        PaintedLoop.Attach(wizard, Vector2.zero, null, f, s, Tune.F("vfx.veilFps", 18f), 61, Mathf.Min(life, Tune.F("vfx.veilTime", 0.6f)), Tune.F("vfx.veilAlpha", 0.55f));
        return true;
    }

    // Cyclone: the whirlwind on the ground, `radius` out, for its whole life.
    public static bool Whirlwind(Transform vortex, float radius, float life)
    {
        var f = Sized("vfx_cyclone", radius * 2f * Tune.F("vfx.cycloneSize", 1f), out float s);
        if (f == null) return false;
        PaintedLoop.Attach(vortex, Vector2.zero, null, f, s, 16f, -47, life);
        return true;
    }

    // Sizes for foe-sized art are rounded to a quarter unit so foes share atlases.
    static float FoeSize(float size, float k) => Mathf.Max(0.5f, Mathf.Round(size * k * 4f) / 4f);

    // Enthrall: the charm sigil turning once under a foe (body size `size`) as it's charmed, then fading.
    public static void CharmSigil(Transform foe, float size, int order)
    {
        var f = Sized("vfx_enthrall", FoeSize(size, 2f * Tune.F("vfx.enthrallSize", 1.2f)), out float s);
        if (f != null) PaintedLoop.Attach(foe, Vector2.zero, f, null, s, f.Length / 1.2f, order, 1.2f);
    }

    // Enthrall: hearts rising from a charmed foe for as long as it stays charmed (its owner shows and hides it).
    public static PaintedLoop CharmHearts(Transform foe, float size, int order)
    {
        var f = Sized("vfx_hearts", FoeSize(size, Tune.F("vfx.heartsSize", 1.2f)), out float s);
        if (f == null) return null;
        float tall = f[0].rect.height / f[0].pixelsPerUnit * s;
        return PaintedLoop.Attach(foe, new Vector2(0f, tall * 0.5f - size * 0.4f), null, f, s, Tune.F("vfx.heartsFps", 24f), order);   // rising from its feet
    }

    // Mirror Ward: one crystal, `across` world units wide; kept upright, its owner moves it round the ring.
    public static PaintedLoop MirrorCrystal(Transform wizard, float across)
    {
        var f = Sized("vfx_mirror", across, out float s);
        return f == null ? null : PaintedLoop.Attach(wizard, Vector2.zero, null, f, s, 10f, 58).Skip(Random.Range(0f, 2f));
    }

    // ---- Sunspots: the painted pool of light, its ring `radius` world units across ----

    public static Sprite[] SunFrames(float radius, out float scale)
    {
        scale = 1f;
        if (!On) return null;
        var src = Frames("vfx_sun", 24);
        if (src.Length == 0) return null;
        scale = radius * Tune.F("vfx.sunSize", 2.7f) / (src[0].rect.width / src[0].pixelsPerUnit);
        return PixelDensity.Fit(src, scale, false);
    }

    // ---- Stone Spikes: the painted spike erupting, standing with glowing cracks, and crumbling ----

    // The three parts for a spike whose frame is `height` world units tall, at the game's pixel size.
    public static bool SpikeArt(float height, out Sprite[] rise, out Sprite[] hold, out Sprite[] crumble, out float scale)
    {
        rise = hold = crumble = null; scale = 0f;
        if (!On) return false;
        var r = Frames("vfx_rockspike_rise", 8);
        var h = Frames("vfx_rockspike_hold", 6);
        var c = Frames("vfx_rockspike_crumble", 8);
        if (r.Length == 0 || h.Length == 0 || c.Length == 0) return false;
        scale = height / (r[0].rect.height / r[0].pixelsPerUnit);
        rise = PixelDensity.Fit(r, scale, false);
        hold = PixelDensity.Fit(h, scale, false);
        crumble = PixelDensity.Fit(c, scale, false);
        return true;
    }

    // The crumble plays out on its own once the spike is gone.
    public static void SpikeCrumble(Vector2 centre, Sprite[] frames, float scale, float life)
    {
        var p = Puff.Take(frames, 62);
        p.transform.position = centre;
        p.transform.localScale = new Vector3(scale, scale, 1f);
        p.life = life;
        p.fade = 0.35f;
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

        // Spent puffs and arc tiles are parked and reused: every spell hit makes one or two.
        static readonly Stack<Puff> pool = new Stack<Puff>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPool() => pool.Clear();

        // A live puff showing these frames, from the pool when one is parked.
        internal static Puff Take(Sprite[] frames, int order)
        {
            Puff p = null;
            while (p == null && pool.Count > 0) p = pool.Pop();   // skips any destroyed while parked
            if (p == null)
            {
                var go = new GameObject("SpellPuff");
                p = go.AddComponent<Puff>();
                p.sr = SpriteFactory.Add(go, frames[0], Color.white, order);
            }
            else
            {
                p.sr.sprite = frames[0];
                p.sr.color = Color.white;
                p.sr.sortingOrder = order;
                p.transform.rotation = Quaternion.identity;
                p.gameObject.SetActive(true);
            }
            p.frames = frames;
            p.t = 0f;
            p.phase = 0;
            p.line = false;
            return p;
        }

        public static Puff Spawn(Vector2 pos, Sprite[] frames, float size, float life, float fade)
        {
            var p = Take(frames, 86);
            p.transform.position = pos;
            p.full = size / (frames[0].rect.width / frames[0].pixelsPerUnit);
            p.transform.localScale = Vector3.one * p.full * 0.9f;   // fixed: the frames already grow, and a changing scale would break the pixel grid
            p.life = life;
            p.fade = fade;
            return p;
        }

        void Update()
        {
            t += Time.deltaTime;
            int i = (int)(t / life * frames.Length);
            if (i >= frames.Length) { gameObject.SetActive(false); pool.Push(this); return; }
            sr.sprite = frames[(i + phase) % frames.Length];
            if (line) { sr.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t / life) / fade))); return; }
            float k = t / life;
            sr.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - k) / fade)));   // fades out over the last `fade` of its life
        }
    }
}
