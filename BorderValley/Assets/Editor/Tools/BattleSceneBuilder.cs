using BorderValley.UI.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace BorderValley.Editor
{
    public static class BattleSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Battle.unity";

        [MenuItem("Tools/BorderValley/Rebuild Battle Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateMainCamera(scene);
            CreateEventSystem(scene);

            var root = new GameObject("BattleSceneRoot");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<BattleSceneController>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        private static void CreateMainCamera(Scene scene)
        {
            var cameraGameObject = new GameObject("Main Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraGameObject, scene);
            cameraGameObject.tag = "MainCamera";

            var camera = cameraGameObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 1f);
        }

        private static void CreateEventSystem(Scene scene)
        {
            var eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
        }
    }
}