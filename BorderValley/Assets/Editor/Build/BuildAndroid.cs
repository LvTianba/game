using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BorderValley.Editor.Build
{
    public static class BuildAndroid
    {
        private const string OutputPath = "../../Builds/Android/BorderValley.apk";

        [MenuItem("Tools/BorderValley/Build Android")]
        public static void PerformBuild()
        {
            PlayerSettings.companyName = "BorderValley";
            PlayerSettings.productName = "Border Valley";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.bordervalley.game");
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Medium);

            var output = Path.GetFullPath(Path.Combine(Application.dataPath, OutputPath));
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "Builds/Android");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[]
                {
                    "Assets/Scenes/Boot.unity",
                    "Assets/Scenes/MainMenu.unity",
                    "Assets/Scenes/World.unity"
                },
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.StrictMode
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Android build failed: {report.summary.result}");
        }
    }
}
