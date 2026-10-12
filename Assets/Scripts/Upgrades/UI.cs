using UnityEngine;

// Tiny immediate-mode UI kit on top of OnGUI: panels, cards and text, all scaled for portrait phones.
public static class UI
{
    static GUIStyle label;
    static Texture2D white;
    static Font pixelFont;

    public static float S => GameManager.UiScale;

    // The pixel styles use Pixelify Sans (SIL OFL, Assets/Resources/Fonts); the others keep the default font.
    public static Font Font =>
        ArtStyle.Current == ArtStyleId.Dusk || ArtStyle.Current == ArtStyleId.Crt
            ? pixelFont != null ? pixelFont : pixelFont = Resources.Load<Font>("Fonts/PixelifySans")
            : null;

    static GUIStyle Style(int size, TextAnchor anchor, Color color, bool bold = true)
    {
        if (label == null)
            label = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true, clipping = TextClipping.Overflow };
        var font = Font;
        label.font = font;
        label.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * S));
        label.alignment = anchor;
        // The pixel font is already chunky; synthetic bold just smears it.
        label.fontStyle = bold && font == null ? FontStyle.Bold : FontStyle.Normal;
        label.normal.textColor = color;
        return label;
    }

    public static void Text(Rect r, string text, int size, TextAnchor anchor = TextAnchor.UpperLeft, Color? color = null, bool bold = true)
    {
        var c = color ?? Color.white;
        // Cheap drop shadow for readability over the busy playfield.
        GUI.Label(new Rect(r.x + 2f * S, r.y + 2f * S, r.width, r.height), text, Style(size, anchor, new Color(0f, 0f, 0f, 0.7f * c.a), bold));
        GUI.Label(r, text, Style(size, anchor, c, bold));
    }

    public static void Box(Rect r, Color c)
    {
        if (white == null) white = Texture2D.whiteTexture;
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, white);
        GUI.color = old;
    }

    // Pixel-art UI textures for the current art style (null in styles without them).
    static Texture2D Skin(string name) => ArtStyle.Get("ui_" + name)?.texture;

    // Draws a 9-slice texture with crisp, whole-number texel scaling. Returns the size of one texel in
    // screen pixels so callers can line things up with the frame.
    static float NineSlice(Rect r, Texture2D t, int border, Color? tint = null)
    {
        r = new Rect(Mathf.Round(r.x), Mathf.Round(r.y), Mathf.Round(r.width), Mathf.Round(r.height));
        float k = Mathf.Max(1f, Mathf.Min(Mathf.Round(4f * S), Mathf.Floor(Mathf.Min(r.width, r.height) / (border * 2 + 2))));
        float b = border * k;
        float u = border / (float)t.width, v = border / (float)t.height;
        float[] xs = { r.x, r.x + b, r.xMax - b, r.xMax };
        float[] ys = { r.y, r.y + b, r.yMax - b, r.yMax };
        float[] us = { 0f, u, 1f - u, 1f };
        float[] vs = { 1f, 1f - v, v, 0f };   // GUI y runs down, texture v runs up
        var old = GUI.color;
        GUI.color = tint ?? Color.white;
        for (int j = 0; j < 3; j++)
            for (int i = 0; i < 3; i++)
                GUI.DrawTextureWithTexCoords(new Rect(xs[i], ys[j], xs[i + 1] - xs[i], ys[j + 1] - ys[j]), t,
                    new Rect(us[i], vs[j + 1], us[i + 1] - us[i], vs[j] - vs[j + 1]));
        GUI.color = old;
        return k;
    }

    // A horizontal meter (health, XP, boss). Uses the style's bar frame when it has one.
    public static void Bar(Rect r, float fraction, Color fill)
    {
        fraction = Mathf.Clamp01(fraction);
        var t = Skin("bar");
        if (t == null)
        {
            Box(r, new Color(0f, 0f, 0f, 0.6f));
            Box(new Rect(r.x, r.y, r.width * fraction, r.height), fill);
            return;
        }
        float k = NineSlice(r, t, 3);
        var inner = new Rect(Mathf.Round(r.x) + 2f * k, Mathf.Round(r.y) + 2f * k, Mathf.Round(r.width) - 4f * k, Mathf.Round(r.height) - 4f * k);
        if (fraction <= 0f) return;
        var bar = new Rect(inner.x, inner.y, Mathf.Round(inner.width * fraction), inner.height);
        Box(bar, fill);
        Box(new Rect(bar.x, bar.y, bar.width, k), new Color(1f, 1f, 1f, 0.3f));   // top shine
    }

    // A square backing for an icon (spell slots). Returns the inner area.
    public static Rect Slot(Rect r)
    {
        var t = Skin("slot");
        if (t == null) { Box(r, new Color(0f, 0f, 0f, 0.55f)); return r; }
        float k = NineSlice(r, t, 4);
        return new Rect(r.x + 2f * k, r.y + 2f * k, r.width - 4f * k, r.height - 4f * k);
    }

    public static void Dim(float alpha = 0.65f) => Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, alpha));

    public static void Circle(Vector2 center, float radius, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), SpriteFactory.Circle.texture);
        GUI.color = old;
    }

    // A tappable card with a coloured edge. Returns true on tap.
    public static bool Card(Rect r, string title, string body, Color accent, string tag = null, bool enabled = true)
    {
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        var panel = Skin("panel");
        if (panel != null)
        {
            float k = NineSlice(r, panel, 5, enabled ? Color.white : new Color(1f, 1f, 1f, 0.6f));
            Box(new Rect(r.x + 2f * k, r.y + 5f * k, 2f * k, r.height - 10f * k), enabled ? accent : accent * 0.4f);
        }
        else
        {
            Box(r, new Color(0.08f, 0.06f, 0.14f, enabled ? 0.95f : 0.6f));
            Box(new Rect(r.x, r.y, 10f * S, r.height), enabled ? accent : accent * 0.4f);
        }
        if (hover) Box(r, new Color(1f, 1f, 1f, 0.06f));

        float pad = 22f * S;
        var inner = new Rect(r.x + pad, r.y + 14f * S, r.width - pad * 1.5f, r.height - 20f * S);
        if (tag != null)
            Text(new Rect(inner.x, inner.y, inner.width, 30f * S), tag, 20, TextAnchor.UpperRight, new Color(accent.r, accent.g, accent.b, 0.9f));
        Text(new Rect(inner.x, inner.y, inner.width, 44f * S), title, 32, TextAnchor.UpperLeft, enabled ? accent : Color.gray);
        Text(new Rect(inner.x, inner.y + 46f * S, inner.width, inner.height - 46f * S), body, 24, TextAnchor.UpperLeft, enabled ? new Color(0.9f, 0.9f, 0.95f) : Color.gray, false);

        return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    public static bool Button(Rect r, string text, Color accent)
    {
        var tex = Skin("button");
        if (tex != null)
        {
            // Accent shows as a strip on the button's lip; the label sits above the lip.
            float k = NineSlice(r, tex, 5);
            Box(new Rect(r.x + 3f * k, r.yMax - 4f * k, r.width - 6f * k, k), accent);
            Text(new Rect(r.x, r.y, r.width, r.height - 2f * k), text, 30, TextAnchor.MiddleCenter, Color.white);
        }
        else
        {
            Box(r, new Color(accent.r * 0.3f, accent.g * 0.3f, accent.b * 0.3f, 0.95f));
            Box(new Rect(r.x, r.yMax - 6f * S, r.width, 6f * S), accent);
            Text(r, text, 30, TextAnchor.MiddleCenter, Color.white);
        }
        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    // Layout helper: a centred column of the safe area.
    public static Rect Column(float topFraction, float heightFraction)
    {
        Rect safe = Screen.safeArea;
        float w = Mathf.Min(safe.width - 40f * S, 680f * S);
        float x = safe.x + (safe.width - w) * 0.5f;
        float top = Screen.height - safe.yMax;
        return new Rect(x, top + safe.height * topFraction, w, safe.height * heightFraction);
    }
}
