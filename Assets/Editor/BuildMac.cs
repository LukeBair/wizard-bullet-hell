using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Batch-mode Mac build: Unity -batchmode -buildTarget OSXUniversal -executeMethod BuildMac.Build
// Makes Builds/Mac/Wizard Bullet Hell.app (Intel and Apple silicon). On a Mac the game steers with WASD
// and aims with the mouse (Joystick.MouseAim).
static class BuildMac
{
    public static void Build()
    {
        // a phone-shaped window (the game is laid out for portrait), resizable; these only touch desktop builds
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 600;
        PlayerSettings.defaultScreenHeight = 1000;
        PlayerSettings.resizableWindow = true;
        // the app icon: the wizard's face (Assets/AppIcon), at every size the Mac asks for
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AppIcon/icon_1024.png");
        if (icon != null)
        {
            var sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Application);
            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < icons.Length; i++) icons[i] = icon;
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Application);
        }
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = "Builds/Mac/Wizard Bullet Hell.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None,
        });
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
