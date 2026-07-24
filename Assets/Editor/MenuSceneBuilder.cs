using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChonkyMerge;

namespace ChonkyMerge.EditorTools
{
    /// <summary>
    /// Builds the MainMenu scene, sets scene order (menu first), and applies the
    /// app name + icon so the built APK shows "Wobble Zoo" with the cat logo.
    /// </summary>
    public static class MenuSceneBuilder
    {
        private const string MenuPath = "Assets/Scenes/MainMenu.unity";
        private const string GamePath = "Assets/Scenes/Prototype.unity";
        private const string IconPath = "Assets/Resources/Art/AppIcon.png";

        [MenuItem("Chonky/Build Menu + Configure App")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            cam.AddComponent<Camera>();
            cam.transform.position = new Vector3(0, 0, -10);

            var menu = new GameObject("Menu");
            menu.AddComponent<MainMenu>();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, MenuPath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(GamePath, true),
            };

            PlayerSettings.productName = "Wobble Zoo";
            PlayerSettings.companyName = "Wobble Games";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.wobblegames.wobblezoo");

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
            {
                var icons = new[] { icon };
                PlayerSettings.SetIcons(NamedBuildTarget.Android, icons, IconKind.Application);
                Debug.Log("Applied app icon from " + IconPath);
            }
            else Debug.LogWarning("App icon not found at " + IconPath);

            Debug.Log("Wobble Zoo menu built and app configured.");
        }
    }
}
