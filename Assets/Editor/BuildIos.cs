using UnityEditor;
using UnityEditor.Build.Reporting;

// Batch-mode iOS build: Unity -batchmode -buildTarget iOS -executeMethod BuildIos.Build
static class BuildIos
{
    public static void Build()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = "Builds/iOS",
            target = BuildTarget.iOS,
            options = BuildOptions.None,
        });
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
