using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChonkyMerge;

namespace ChonkyMerge.EditorTools
{
    /// <summary>
    /// Builds the Wobble Tower scene and sets the full scene order (menu → tower →
    /// legacy jar), plus the app name and icon. Run headless via -executeMethod.
    /// </summary>
    public static class TowerSceneBuilder
    {
        private const string MenuPath = "Assets/Scenes/MainMenu.unity";
        private const string TowerPath = "Assets/Scenes/Tower.unity";
        private const string GamePath = "Assets/Scenes/Prototype.unity";
        private const string IconPath = "Assets/Resources/Art/AppIcon.png";

        [MenuItem("Chonky/Build Tower + Configure App")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            cam.AddComponent<Camera>();
            cam.transform.position = new Vector3(0, 0, -10);

            var game = new GameObject("TowerGame");
            game.AddComponent<TowerGame>();
            game.AddComponent<TiltGravity>();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, TowerPath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuPath, true),
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

            Debug.Log("Wobble Tower scene built and app configured.");
        }
    }
}
