using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ChonkyMerge.EditorTools
{
    /// <summary>
    /// The Google Play build: an .aab, targeting a current API level, signed with the
    /// developer's own upload key.
    ///
    /// Play will not accept the .apk that ApkBuilder makes. That one exists so the game
    /// can be side-loaded onto a phone for testing; this one is the shipping artifact.
    /// The differences that actually matter:
    ///
    ///   * App Bundle, not APK — Play has required .aab for new apps since 2021.
    ///   * An explicit target API. The project was set to "automatic", which silently
    ///     means "whatever SDK happens to be installed on this machine" — a build that
    ///     passes here and gets rejected by Play on a different machine.
    ///   * A real upload key. Debug-signed builds are rejected.
    ///
    /// SIGNING CREDENTIALS ARE NEVER STORED IN THIS REPO. The keystore path and its
    /// passwords are read from environment variables at build time, so nothing secret
    /// is ever committed, logged, or written into ProjectSettings:
    ///
    ///     WOBBLEZOO_KEYSTORE        full path to the .keystore file
    ///     WOBBLEZOO_KEYSTORE_PASS   its password
    ///     WOBBLEZOO_KEYALIAS        the key alias inside it
    ///     WOBBLEZOO_KEYALIAS_PASS   that alias's password
    ///
    /// Optional:
    ///     WOBBLEZOO_VERSION         version name shown on the store, e.g. 1.0.1
    ///     WOBBLEZOO_BUILD           version code, an integer that must increase every
    ///                               single upload — Play rejects a repeat
    ///
    /// With no keystore set it still builds, debug-signed, so the pipeline can be
    /// verified. That bundle CANNOT be uploaded, and the log says so loudly.
    ///
    ///     Unity.exe -batchmode -quit -projectPath . \
    ///               -executeMethod ChonkyMerge.EditorTools.StoreBuilder.BuildAab
    /// </summary>
    public static class StoreBuilder
    {
        // Play requires new apps to target a recent API. 36 is the newest this editor
        // installs; targeting the newest is always accepted, targeting an old one isn't.
        private const AndroidSdkVersions TargetApi = AndroidSdkVersions.AndroidApiLevel36;

        [MenuItem("Chonky/Build Play Store Bundle (.aab)")]
        public static void BuildAab()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;   // 64-bit, as Play requires
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = TargetApi;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.wobblegames.wobblezoo");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Debugging; // Play wants symbols for crash reports

            var version = Environment.GetEnvironmentVariable("WOBBLEZOO_VERSION");
            if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;
            var build = Environment.GetEnvironmentVariable("WOBBLEZOO_BUILD");
            if (!string.IsNullOrEmpty(build) && int.TryParse(build, out int code))
                PlayerSettings.Android.bundleVersionCode = code;

            bool signed = ApplySigning();

            System.IO.Directory.CreateDirectory("Builds");
            var scenes = Array.ConvertAll(
                Array.FindAll(EditorBuildSettings.scenes, s => s.enabled), s => s.path);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Builds/WobbleZoo.aab",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            });

            var s = report.summary;
            Debug.Log($"AAB build result: {s.result}, size: {s.totalSize} bytes, errors: {s.totalErrors}, "
                      + $"version {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode}), "
                      + $"target API {(int)TargetApi}");
            if (!signed)
                Debug.LogWarning("AAB is DEBUG-SIGNED and cannot be uploaded to Google Play. "
                                 + "Set WOBBLEZOO_KEYSTORE / _PASS / WOBBLEZOO_KEYALIAS / _PASS and build again.");

            if (s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded && Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        /// Wires up the upload key from the environment. Returns false (and leaves the
        /// build debug-signed) if anything is missing, rather than half-configuring it.
        private static bool ApplySigning()
        {
            string ks = Environment.GetEnvironmentVariable("WOBBLEZOO_KEYSTORE");
            string ksPass = Environment.GetEnvironmentVariable("WOBBLEZOO_KEYSTORE_PASS");
            string alias = Environment.GetEnvironmentVariable("WOBBLEZOO_KEYALIAS");
            string aliasPass = Environment.GetEnvironmentVariable("WOBBLEZOO_KEYALIAS_PASS");

            if (string.IsNullOrEmpty(ks) || string.IsNullOrEmpty(ksPass)
                || string.IsNullOrEmpty(alias) || string.IsNullOrEmpty(aliasPass))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                return false;
            }
            if (!System.IO.File.Exists(ks))
            {
                Debug.LogError($"Keystore not found at '{ks}' — building unsigned instead.");
                PlayerSettings.Android.useCustomKeystore = false;
                return false;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = ks;
            PlayerSettings.Android.keystorePass = ksPass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = aliasPass;
            // Never let a password reach ProjectSettings.asset on disk.
            EditorApplication.quitting += ClearSigning;
            Debug.Log($"Signing with key alias '{alias}' from the keystore in WOBBLEZOO_KEYSTORE.");
            return true;
        }

        /// Scrub the credentials back out of PlayerSettings when the editor exits, so a
        /// password can never be committed by a later `git add ProjectSettings`.
        private static void ClearSigning()
        {
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasPass = "";
            PlayerSettings.Android.useCustomKeystore = false;
        }
    }
}
