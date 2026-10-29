using UnityEditor;
using UnityEditor.Build.Reporting;

// Batch-mode iOS build: Unity -batchmode -buildTarget iOS -executeMethod BuildIos.Build
static class BuildIos
{
    public static void Build()
    {
        const string path = "Builds/iOS";
        // "Append" keeps the Xcode project and its signing between builds; the first build has nothing to append to
        bool append = BuildPipeline.BuildCanBeAppended(BuildTarget.iOS, path) == CanAppendBuild.Yes;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = path,
            target = BuildTarget.iOS,
            options = append ? BuildOptions.AcceptExternalModificationsToPlayer : BuildOptions.None,
        });
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
