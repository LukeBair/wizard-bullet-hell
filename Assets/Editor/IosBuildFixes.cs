#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

// Newer Xcode versions turn on two checks that Unity's generated project fails:
// the module verifier rejects UnityFramework's umbrella header, and user script sandboxing
// stops the IL2CPP build phase from loading its own tools. Turn both off after every iOS build.
static class IosBuildFixes
{
    [PostProcessBuild(100)]
    static void OnPostprocessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;

        string projPath = PBXProject.GetPBXProjectPath(path);
        var proj = new PBXProject();
        proj.ReadFromFile(projPath);

        foreach (var guid in new[] { proj.ProjectGuid(), proj.GetUnityMainTargetGuid(), proj.GetUnityFrameworkTargetGuid() })
        {
            proj.SetBuildProperty(guid, "ENABLE_MODULE_VERIFIER", "NO");
            proj.SetBuildProperty(guid, "ENABLE_USER_SCRIPT_SANDBOXING", "NO");
        }

        proj.WriteToFile(projPath);
    }
}
#endif
