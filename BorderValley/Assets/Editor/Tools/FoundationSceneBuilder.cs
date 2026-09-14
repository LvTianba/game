using BorderValley.Core;
using BorderValley.Data;
using BorderValley.Inventory;
using BorderValley.UI;
using BorderValley.UI.Battle;
using BorderValley.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BorderValley.Editor
{
    public static class FoundationSceneBuilder
    {
        [MenuItem("Tools/BorderValley/Rebuild Foundation Scenes")]
        public static void Rebuild()
        {
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            System.IO.Directory.CreateDirectory("Assets/Resources");
            EquipmentContentBuilder.Build();

            const string catalogPath = "Assets/Resources/ContentCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<ContentCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ContentCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }

            var boot = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var services = new GameObject("BootServices");
            var validator = services.AddComponent<ContentRuntimeValidator>();
            var bootstrapper = services.AddComponent<GameBootstrapper>();
            var inventoryInstaller = services.AddComponent<InventoryBootstrapInstaller>();
            var bootSerialized = new SerializedObject(bootstrapper);
            var preflightProperty = bootSerialized.FindProperty("preflightChecks");
            preflightProperty.arraySize = 1;
            preflightProperty.GetArrayElementAtIndex(0).objectReferenceValue = validator;
            var serviceInstallersProperty = bootSerialized.FindProperty("serviceInstallers");
            serviceInstallersProperty.arraySize = 1;
            serviceInstallersProperty.GetArrayElementAtIndex(0).objectReferenceValue = inventoryInstaller;
            bootSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(boot, "Assets/Scenes/Boot.unity");

            var menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvas = CreateCanvas(menu);
            var button = CreateButton(canvas.transform);
            var eventSystem = CreateEventSystem(menu);
            var component = canvas.AddComponent<MainMenuView>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("newGameButton").objectReferenceValue = button;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(menu, "Assets/Scenes/MainMenu.unity");

            var world = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateMainCamera(world);
            var worldPlaceholder = new GameObject("WorldPlaceholder");
            worldPlaceholder.AddComponent<WorldPlaceholder>();
            var battleEntry = new GameObject("WorldBattleEntry");
            battleEntry.AddComponent<WorldBattleEntryView>();
            EditorSceneManager.SaveScene(world, "Assets/Scenes/World.unity");

            BattleSceneBuilder.BuildScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Boot.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/World.unity", true),
                new EditorBuildSettingsScene(BattleSceneBuilder.ScenePath, true)
            };
            AssetDatabase.SaveAssets();
        }

        private static GameObject CreateMainCamera(Scene scene)
        {
            var cameraGameObject = new GameObject("Main Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraGameObject, scene);
            cameraGameObject.tag = "MainCamera";

            var camera = cameraGameObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 1f);
            return cameraGameObject;
        }

        private static GameObject CreateCanvas(Scene scene)
        {
            var canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvas, scene);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static GameObject CreateEventSystem(Scene scene)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
            return eventSystem;
        }

        private static Button CreateButton(Transform parent)
        {
            var root = new GameObject("New Game", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 80f);
            root.GetComponent<Image>().color = new Color(0.2f, 0.35f, 0.55f, 1f);
            return root.GetComponent<Button>();
        }
    }
}
