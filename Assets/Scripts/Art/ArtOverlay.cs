using UnityEngine;

// Screen effects drawn over everything for styles that want them: CRT scanlines and a vignette.
public class ArtOverlay : MonoBehaviour
{
    Texture2D scanlines, vignette;

    void OnGUI()
    {
        if (ArtStyle.Current != ArtStyleId.Crt || Event.current.type != EventType.Repaint) return;
        GUI.depth = -100;
        if (scanlines == null) scanlines = MakeScanlines();
        if (vignette == null) vignette = MakeVignette();
        float line = Mathf.Max(2f, Mathf.Round(Screen.height / 480f));
        GUI.DrawTextureWithTexCoords(new Rect(0, 0, Screen.width, Screen.height), scanlines, new Rect(0, 0, 1f, Screen.height / (line * 2f)));
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignette);
    }

    static Texture2D MakeScanlines()
    {
        var t = new Texture2D(1, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        t.SetPixels(new[] { new Color(0f, 0f, 0f, 0.28f), new Color(0f, 0f, 0f, 0f) });
        t.Apply();
        return t;
    }

    static Texture2D MakeVignette()
    {
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f) / n, new Vector2(0.5f, 0.5f)) * 1.41f;
            px[y * n + x] = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 0.6f, (d - 0.55f) / 0.45f));
        }
        t.SetPixels(px);
        t.Apply();
        return t;
    }
}
