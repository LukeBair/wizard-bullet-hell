using System;
using System.Collections.Generic;
using UnityEngine;

// Generates simple placeholder shapes at runtime. Each sprite is white and 1 world unit wide; tint it with SpriteRenderer.color.
public static class SpriteFactory
{
    const int Size = 64;
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Circle => Get("circle", (x, y) =>
    {
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f));
        return Mathf.Clamp01((0.5f - d) * Size);
    });

    public static Sprite Glow => Get("glow", (x, y) =>
    {
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f)) * 2f;
        float a = Mathf.Clamp01(1f - d);
        return a * a;
    });

    // Points up.
    public static Sprite Triangle => Get("triangle", (x, y) =>
    {
        float halfWidth = (1f - y) * 0.5f;
        return Mathf.Abs(x - 0.5f) <= halfWidth ? 1f : 0f;
    });

    public static SpriteRenderer Add(GameObject go, Sprite sprite, Color color, int order)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    public static SpriteRenderer Child(Transform parent, string name, Sprite sprite, Color color, Vector2 localPos, float scale, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one * scale;
        return Add(go, sprite, color, order);
    }

    // alpha(x, y) takes normalized pixel-center coordinates in [0, 1].
    static Sprite Get(string key, Func<float, float, float> alpha)
    {
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float a = alpha((x + 0.5f) / Size, (y + 0.5f) / Size);
            pixels[y * Size + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        sprite.name = key;
        cache[key] = sprite;
        return sprite;
    }
}
