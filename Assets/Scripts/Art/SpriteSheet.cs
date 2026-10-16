using System;
using System.Collections.Generic;
using UnityEngine;

// An animated sprite sheet exported from Aseprite (File > Export Sprite Sheet, JSON array data with
// tags): Resources/Art/<Style>/<name>.png plus <name>.json. Each Aseprite tag becomes a named clip.
public class SpriteSheet
{
    public class Clip
    {
        public Sprite[] Frames;
        public float[] Durations;   // seconds
        public float Length;
    }

    readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();

    public Clip this[string name] => clips.TryGetValue(name, out var c) ? c : null;

    [Serializable] class Rect { public int x, y, w, h; }
    [Serializable] class Frame { public Rect frame; public int duration; }
    [Serializable] class Tag { public string name; public int from, to; }
    [Serializable] class Meta { public Tag[] frameTags; }
    [Serializable] class Data { public Frame[] frames; public Meta meta; }

    static readonly Dictionary<string, SpriteSheet> cache = new Dictionary<string, SpriteSheet>();

    // The current style's sheet with this name, or null (Classic, or no such sheet).
    public static SpriteSheet Load(string name)
    {
        if (ArtStyle.Current == ArtStyleId.Classic) return null;
        string style = ArtStyle.Current.ToString();
        string path = $"Art/{style}/{name}";
        if (cache.TryGetValue(path, out var sheet)) return sheet;

        var tex = Resources.Load<Texture2D>(path);
        var json = Resources.Load<TextAsset>(path);
        // <name>_n: a matching normal map (3D-rendered sheets), for the 2D lights.
        var normals = Resources.Load<Texture2D>(path + "_n");
        var secondary = normals != null ? new[] { new SecondarySpriteTexture { name = "_NormalMap", texture = normals } } : null;
        if (tex != null && json != null)
        {
            tex.filterMode = ArtStyle.Current == ArtStyleId.Vector ? FilterMode.Bilinear : FilterMode.Point;
            var data = JsonUtility.FromJson<Data>(json.text);
            float ppu = ArtStyle.PixelsPerUnit(style, name, tex.width);
            sheet = new SpriteSheet();
            foreach (var tag in data.meta.frameTags)
            {
                int n = tag.to - tag.from + 1;
                var clip = new Clip { Frames = new Sprite[n], Durations = new float[n] };
                for (int i = 0; i < n; i++)
                {
                    var f = data.frames[tag.from + i];
                    // Aseprite counts y from the top, Unity from the bottom.
                    var rect = new UnityEngine.Rect(f.frame.x, tex.height - f.frame.y - f.frame.h, f.frame.w, f.frame.h);
                    clip.Frames[i] = secondary == null
                        ? Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), ppu)
                        : Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, Vector4.zero, false, secondary);
                    clip.Durations[i] = f.duration / 1000f;
                    clip.Length += clip.Durations[i];
                }
                sheet.clips[tag.name] = clip;
            }
        }
        cache[path] = sheet;
        return sheet;
    }
}
