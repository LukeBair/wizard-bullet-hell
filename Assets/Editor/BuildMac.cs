using UnityEditor;
using UnityEditor.Build.Reporting;

// Batch-mode Mac build: Unity -batchmode -buildTarget OSXUniversal -executeMethod BuildMac.Build
// Makes Builds/Mac/Wizard Bullet Hell.app (Intel and Apple silicon). On a Mac the game steers with WASD
// and aims with the mouse (Joystick.MouseAim).
static class BuildMac
{
    public static void Build()
    {
        // a phone-shaped window (the game is laid out for portrait), resizable; these only touch desktop builds
        PlayerSettings.fullScreenMode = UnityEngine.FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 600;
        PlayerSettings.defaultScreenHeight = 1000;
        PlayerSettings.resizableWindow = true;
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
