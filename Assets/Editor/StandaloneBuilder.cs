using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChonkyMerge.EditorTools
{
    /// <summary>
    /// Windows build, for looking at the game on this machine. Not a shipping
    /// target — the store build is the APK (see ApkBuilder). It exists because the
    /// UI is all IMGUI drawn in code, and the only honest way to check a screen
    /// looks right is to render it.
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod
    ///             ChonkyMerge.EditorTools.StandaloneBuilder.BuildWin
    ///
    /// Then run the exe with `-shots &lt;folder&gt;` for an automatic screenshot tour of
    /// every screen (see MainMenu.ShotTour).
    /// </summary>
    public static class StandaloneBuilder
    {
        public static void BuildWin()
        {
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Puzzle.unity" },
                locationPathName = "Builds/Win/WobbleZoo.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("Windows build result: " + report.summary.result);
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
