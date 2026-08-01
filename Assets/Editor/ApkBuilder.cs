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
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25; // Android 7.1+ (Unity's floor)
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.wobblegames.wobblezoo");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            EditorUserBuildSettings.buildAppBundle = false; // .apk, not .aab

            System.IO.Directory.CreateDirectory("Builds");

            // Build exactly the scenes enabled in Build Settings (Menu -> Tower -> jar).
            var scenes = System.Array.ConvertAll(
                System.Array.FindAll(EditorBuildSettings.scenes, s => s.enabled),
                s => s.path);

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Builds/WobbleZoo.apk",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"APK build result: {s.result}, size: {s.totalSize} bytes, errors: {s.totalErrors}");
            if (s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded && Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
