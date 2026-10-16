using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// One-time switch from the built-in renderer to URP with the 2D Renderer, so sprites can be lit by
// Light2D and the camera gets bloom. Creates Assets/Settings/URP2D(.asset, _Renderer.asset), points
// Graphics and every quality level at it, and writes the post-processing profile and the lit/unlit
// sprite materials into Resources for the game to load at runtime. Safe to run again.
// Batch: Unity -batchmode -quit -executeMethod SetupUrp2D.Run
static class SetupUrp2D
{
    const string Dir = "Assets/Settings";
    const string AssetPath = Dir + "/URP2D.asset";

    [MenuItem("Tools/Set Up URP 2D Lighting")]
    public static void Run()
    {
        Directory.CreateDirectory(Dir);
        Directory.CreateDirectory("Assets/Resources/Render");

        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
        if (asset == null)
        {
            // Renderer2DMenus.CreateRendererAsset is what the Create menu uses; it fills in the 2D
            // renderer's default materials and shaders, which a bare CreateInstance leaves empty.
            var menus = Type.GetType("UnityEditor.Rendering.Universal.Renderer2DMenus, Unity.RenderPipelines.Universal.Editor");
            var create = menus.GetMethod("CreateRendererAsset", BindingFlags.NonPublic | BindingFlags.Static);
            var rendererType = create.GetParameters()[1].ParameterType;
            var data = (ScriptableRendererData)create.Invoke(null,
                new object[] { Dir + "/URP2D_Renderer.asset", Enum.Parse(rendererType, "_2DRenderer"), false, "Renderer" });
            asset = UniversalRenderPipelineAsset.Create(data);
            asset.supportsHDR = false;      // mobile: LDR bloom is enough
            asset.msaaSampleCount = 1;
            AssetDatabase.CreateAsset(asset, AssetPath);
        }

        GraphicsSettings.defaultRenderPipeline = asset;
        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = asset;
        }
        QualitySettings.SetQualityLevel(current, false);

        // Bloom so spells and crystals glow; a light vignette to pull the eye to the middle.
        const string profilePath = "Assets/Resources/Render/PostFX.asset";
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath) == null)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.8f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.6f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.5f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
        }

        // Materials the game assigns at runtime: lit for characters and the world, unlit for effects.
        CopyMaterial("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat", "Assets/Resources/Render/SpriteLit.mat");
        CopyMaterial("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat", "Assets/Resources/Render/SpriteUnlit.mat");

        AssetDatabase.SaveAssets();
        Debug.Log("[SetupUrp2D] URP 2D renderer active: " + AssetPath);
    }

    static void CopyMaterial(string from, string to)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(to) != null) return;
        var src = AssetDatabase.LoadAssetAtPath<Material>(from);
        if (src == null) { Debug.LogError("[SetupUrp2D] missing " + from); return; }
        AssetDatabase.CreateAsset(new Material(src), to);
    }
}
