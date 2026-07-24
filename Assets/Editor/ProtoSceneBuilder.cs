using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChonkyMerge;

namespace ChonkyMerge.EditorTools
{
    /// <summary>
    /// One-click builder for the playable prototype scene. Run from the Unity menu
    /// (Chonky > Build Prototype Scene) or headless via -executeMethod for CI.
    /// </summary>
    public static class ProtoSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Prototype.unity";

        [MenuItem("Chonky/Build Prototype Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            cam.AddComponent<Camera>();
            cam.transform.position = new Vector3(0, 0, -10);

            var game = new GameObject("Game");
            game.AddComponent<GameManager>();
            game.AddComponent<CritterSpawner>();
            game.AddComponent<TiltGravity>();
            game.AddComponent<Bootstrap>();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Chonky prototype scene built at " + ScenePath);
        }
    }
}
