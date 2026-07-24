using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using SleepyZoo;

namespace ChonkyMerge.EditorTools
{
    /// <summary>
    /// Builds the cozy puzzle scene and makes it the Play target, keeping the older
    /// scenes available. Also (re)applies app name + icon. Run via -executeMethod.
    /// </summary>
    public static class PuzzleSceneBuilder
    {
        private const string MenuPath = "Assets/Scenes/MainMenu.unity";
        private const string PuzzlePath = "Assets/Scenes/Puzzle.unity";
        private const string TowerPath = "Assets/Scenes/Tower.unity";
        private const string GamePath = "Assets/Scenes/Prototype.unity";
        private const string IconPath = "Assets/Resources/Art/AppIcon.png";

        [MenuItem("Chonky/Build Puzzle + Configure App")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            cam.AddComponent<Camera>();
            cam.transform.position = new Vector3(0, 0, -10);

            var game = new GameObject("PuzzleGame");
            game.AddComponent<PuzzleGame>();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, PuzzlePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(PuzzlePath, true),
                new EditorBuildSettingsScene(TowerPath, true),
                new EditorBuildSettingsScene(GamePath, true),
            };

            PlayerSettings.productName = "Wobble Zoo";
            PlayerSettings.companyName = "Wobble Games";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.wobblegames.wobblezoo");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
                PlayerSettings.SetIcons(NamedBuildTarget.Android, new[] { icon }, IconKind.Application);

            Debug.Log("Cozy puzzle scene built and app configured.");
        }
    }
}
