using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 2D lighting on URP's 2D Renderer (Assets/Editor/SetupUrp2D.cs switches the project over).
// Characters, enemies and the world use the lit sprite material, so a dim ambient light plus point
// lights on the wizard, spells, crystals and candles shade them; effects, bullets and glows stay unlit
// and bloom carries their glow. With no render pipeline (the built-in renderer) everything here is a
// no-op and sprites draw as before. Tuning: lighting.* in tuning.json.
public static class Lighting
{
    static Material lit, unlit;
    static VolumeProfile profile;

    public static bool Active => GraphicsSettings.currentRenderPipeline != null && Tune.B("lighting.enabled", true);

    public static Material Unlit => unlit != null ? unlit : unlit = Resources.Load<Material>("Render/SpriteUnlit");
    static Material Lit => lit != null ? lit : lit = Resources.Load<Material>("Render/SpriteLit");

    // Every sprite starts unlit (see SpriteFactory.Add); this opts characters and the world in.
    public static void MakeLit(SpriteRenderer sr)
    {
        if (sr != null && Active && Lit != null) sr.sharedMaterial = Lit;
    }

    public static void MakeLit(Transform root)
    {
        if (!Active) return;
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true)) MakeLit(sr);
    }

    // Bloom and vignette on the game camera, from Resources/Render/PostFX.
    public static void SetupCamera(Camera cam)
    {
        if (GraphicsSettings.currentRenderPipeline == null) return;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = Active && Tune.B("lighting.bloom", true);
        if (profile == null) profile = Resources.Load<VolumeProfile>("Render/PostFX");
        if (profile != null && Object.FindAnyObjectByType<Volume>() == null)
        {
            var v = new GameObject("PostFX").AddComponent<Volume>();
            v.isGlobal = true;
            v.sharedProfile = profile;
        }
    }

    // The ambient light every lit sprite gets, tinted per biome.
    public static Light2D Ambient(Transform parent, Color tint)
    {
        if (!Active) return null;
        var l = new GameObject("Ambient").AddComponent<Light2D>();
        l.transform.SetParent(parent, false);
        l.lightType = Light2D.LightType.Global;
        l.color = Color.Lerp(Color.white, tint, 0.35f);
        l.intensity = Tune.F("lighting.ambient", 0.55f);
        return l;
    }

    // A soft round light. Radius is in world units whatever the parent's scale.
    public static Light2D Point(Transform parent, Color color, float radius, float intensity, Vector2 offset = default)
    {
        if (!Active) return null;
        var l = new GameObject("Light").AddComponent<Light2D>();
        l.transform.SetParent(parent, false);
        l.transform.localPosition = offset;
        l.lightType = Light2D.LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.falloffIntensity = 0.7f;
        SetRadius(l, radius);
        return l;
    }

    public static void SetRadius(Light2D l, float radius)
    {
        if (l == null) return;
        float s = Mathf.Max(0.0001f, l.transform.lossyScale.x);
        l.pointLightOuterRadius = radius / s;
        l.pointLightInnerRadius = radius * 0.15f / s;
    }
}
