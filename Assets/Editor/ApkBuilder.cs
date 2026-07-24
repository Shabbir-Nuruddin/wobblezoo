using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ChonkyMerge.EditorTools
{
    /// <summary>
    /// One-command Android build. Produces Builds/WobbleZoo.apk (debug-signed, so it
    /// installs directly on a phone for testing). Run via -executeMethod or the menu.
    /// </summary>
    public static class ApkBuilder
    {
        [MenuItem("Chonky/Build Android APK")]
        public static void BuildAndroid()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24; // Android 7.0+
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.wobblegames.wobblezoo");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            EditorUserBuildSettings.buildAppBundle = false; // .apk, not .aab

            System.IO.Directory.CreateDirectory("Builds");

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Prototype.unity" },
                locationPathName = "Builds/WobbleZoo.apk",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"APK build result: {s.result}, size: {s.totalSize} bytes, errors: {s.totalErrors}");
            if (s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
