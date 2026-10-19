using System.Collections.Generic;
using UnityEngine;

// Holds the 3D-rendered art (wizard, enemies, props) to one on-screen pixel size. Each sprite covers a
// fixed world size, so its texel size depends on how many texels the render has; this resamples every
// sprite to the texel count that makes one texel art.pxPerTexel screen pixels wide (tuner: art.*).
// Only the 3D art; the hand-drawn Original art keeps its own pixels. Downscales box-filter in halving
// steps from the 128px renders, then snap alpha so edges stay hard.
public static class PixelDensity
{
    public static float PxPerTexel => Settings.Enemy3D ? Tune.F("art.pxPerTexel", 2f) : 0f;
    public static bool On => PxPerTexel > 0.01f;

    // Texels per world unit that gives PxPerTexel screen pixels. refHeight 0 = the actual screen height.
    public static float TexelsPerUnit
    {
        get
        {
            float h = Tune.F("art.refHeight", 1080f);
            if (h <= 0f) h = Mathf.Max(1, Screen.height);
            return h / (2f * GameManager.CameraSize) / Mathf.Max(0.01f, PxPerTexel);
        }
    }

    static readonly Dictionary<(Sprite, int), Sprite> cache = new Dictionary<(Sprite, int), Sprite>();

    // Which resample a sprite of this pixels-per-unit, drawn at this scale, needs: output texels / input texels.
    public static float Factor(float ppu, float scale) => TexelsPerUnit / (ppu / Mathf.Max(0.0001f, scale));

    // Key for caches of things built from Fit results: changes whenever the target density does.
    public static int Key => On ? Mathf.RoundToInt(TexelsPerUnit * 10f) : 0;

    public static Sprite Fit(Sprite src, float scale, bool hardEdges = true) => src == null ? null : Fit(new[] { src }, scale, hardEdges)[0];

    // Resamples sprites that are all drawn at this scale into one shared atlas (so they still batch).
    // hardEdges: alpha snaps to all-or-nothing (bodies and props); soft things like baked shadows keep theirs.
    public static Sprite[] Fit(IList<Sprite> src, float scale, bool hardEdges = true)
    {
        var result = new Sprite[src.Count];
        if (!On || src.Count == 0 || src[0] == null) { src.CopyTo(result, 0); return result; }

        float f = Factor(src[0].pixelsPerUnit, scale);
        if (Mathf.Abs(f - 1f) < 0.02f) { src.CopyTo(result, 0); return result; }
        int key = Mathf.RoundToInt(f * 1000f) * 2 + (hardEdges ? 1 : 0);

        var todo = new List<int>();
        for (int i = 0; i < src.Count; i++)
        {
            if (cache.TryGetValue((src[i], key), out var hit)) result[i] = hit;
            else todo.Add(i);
        }
        if (todo.Count == 0) return result;

        int n = todo.Count;
        var sizes = new Vector2Int[n];
        int cellW = 0, cellH = 0;
        for (int k = 0; k < n; k++)
        {
            var r = src[todo[k]].textureRect;
            sizes[k] = new Vector2Int(Mathf.Max(2, Mathf.RoundToInt(r.width * f)), Mathf.Max(2, Mathf.RoundToInt(r.height * f)));
            cellW = Mathf.Max(cellW, sizes[k].x + 2);
            cellH = Mathf.Max(cellH, sizes[k].y + 2);
        }
        int cols = Mathf.CeilToInt(Mathf.Sqrt(n)), rows = Mathf.CeilToInt(n / (float)cols);

        var first = src[todo[0]];
        var main = Atlas(first.texture, cols * cellW, rows * cellH);
        Texture2D normals = null;
        var secondary = new SecondarySpriteTexture[first.GetSecondaryTextureCount()];
        if (secondary.Length > 0)
        {
            first.GetSecondaryTextures(secondary);
            normals = Atlas(secondary[0].texture, main.width, main.height);
        }

        var rects = new Rect[n];
        for (int k = 0; k < n; k++)
        {
            var s = src[todo[k]];
            var rect = new Rect((k % cols) * cellW + 1, (k / cols) * cellH + 1, sizes[k].x, sizes[k].y);
            rects[k] = rect;
            Blit(s.texture, s.textureRect, main, rect);
            if (normals != null)
            {
                var sec = new SecondarySpriteTexture[secondary.Length];
                s.GetSecondaryTextures(sec);
                Blit(sec[0].texture, s.textureRect, normals, rect);
            }
        }
        if (hardEdges) SnapAlpha(main, normals);
        main.Apply(false);
        if (normals != null) { normals.Apply(false); secondary[0].texture = normals; }

        for (int k = 0; k < n; k++)
        {
            var s = src[todo[k]];
            var pivot = new Vector2(s.pivot.x / s.textureRect.width, s.pivot.y / s.textureRect.height);
            float ppu = s.pixelsPerUnit * sizes[k].x / s.textureRect.width;
            var made = secondary.Length == 0
                ? Sprite.Create(main, rects[k], pivot, ppu)
                : Sprite.Create(main, rects[k], pivot, ppu, 0, SpriteMeshType.FullRect, Vector4.zero, false, secondary);
            made.name = s.name;
            cache[(s, key)] = result[todo[k]] = made;
        }
        return result;
    }

    static Texture2D Atlas(Texture2D like, int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false, !like.isDataSRGB) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var clear = new Color32[w * h];
        t.SetPixels32(clear);
        t.name = like.name + "_fit";
        return t;
    }

    // Copies src's region into dest at out (pixels), box-filtering down in halving steps, nearest when up.
    static void Blit(Texture2D tex, Rect region, Texture2D dest, Rect outRect)
    {
        var rw = tex.isDataSRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear;
        int w = Mathf.RoundToInt(outRect.width), h = Mathf.RoundToInt(outRect.height);
        int cw = Mathf.RoundToInt(region.width), ch = Mathf.RoundToInt(region.height);

        // exact crop at native size
        var cur = RenderTexture.GetTemporary(cw, ch, 0, RenderTextureFormat.ARGB32, rw);
        cur.filterMode = FilterMode.Point;
        var oldFilter = tex.filterMode;
        tex.filterMode = FilterMode.Point;
        Graphics.Blit(tex, cur, new Vector2(region.width / tex.width, region.height / tex.height), new Vector2(region.x / tex.width, region.y / tex.height));
        tex.filterMode = oldFilter;

        while (cw > w * 2 || ch > h * 2)
        {
            int nw = Mathf.Max(w, cw / 2), nh = Mathf.Max(h, ch / 2);
            var next = RenderTexture.GetTemporary(nw, nh, 0, RenderTextureFormat.ARGB32, rw);
            cur.filterMode = FilterMode.Bilinear;
            Graphics.Blit(cur, next);
            RenderTexture.ReleaseTemporary(cur);
            cur = next; cw = nw; ch = nh;
        }
        var fin = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, rw);
        cur.filterMode = (w < cw || h < ch) ? FilterMode.Bilinear : FilterMode.Point;
        Graphics.Blit(cur, fin);
        RenderTexture.ReleaseTemporary(cur);

        var prev = RenderTexture.active;
        RenderTexture.active = fin;
        dest.ReadPixels(new Rect(0, 0, w, h), Mathf.RoundToInt(outRect.x), Mathf.RoundToInt(outRect.y));
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(fin);
    }

    // Hard pixel edges: alpha is all or nothing. The normal map keeps its colour but takes the same mask.
    static void SnapAlpha(Texture2D main, Texture2D normals)
    {
        var px = main.GetPixels32();
        Color32[] nx = normals != null ? normals.GetPixels32() : null;
        for (int i = 0; i < px.Length; i++)
        {
            bool solid = px[i].a >= 128;
            px[i].a = (byte)(solid ? 255 : 0);
            if (nx != null) nx[i].a = (byte)(solid ? 255 : 0);
        }
        main.SetPixels32(px);
        if (nx != null) normals.SetPixels32(nx);
    }
}
