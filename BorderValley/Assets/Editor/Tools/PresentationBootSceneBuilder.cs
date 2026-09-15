using BorderValley.Core;
using BorderValley.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorderValley.Editor.Tools
{
    public static class PresentationBootSceneBuilder
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [MenuItem("BorderValley/Presentation/Wire Boot Scene")]
        public static void WireBootScene()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var services = GameObject.Find("BootServices");
            if (services == null)
                throw new System.InvalidOperationException("BootServices object is missing from Boot.unity.");

            var installer = services.GetComponent<PresentationBootstrapInstaller>();
            if (installer == null)
                installer = services.AddComponent<PresentationBootstrapInstaller>();

            var bootstrapper = services.GetComponent<GameBootstrapper>();
            if (bootstrapper == null)
                throw new System.InvalidOperationException("GameBootstrapper is missing from BootServices.");

            var serialized = new SerializedObject(bootstrapper);
            var installers = serialized.FindProperty("serviceInstallers");
            if (installers == null)
                throw new System.InvalidOperationException("GameBootstrapper.serviceInstallers is missing.");

            var alreadyRegistered = false;
            for (var index = 0; index < installers.arraySize; index++)
            {
                if (installers.GetArrayElementAtIndex(index).objectReferenceValue != installer)
                    continue;

                alreadyRegistered = true;
                break;
            }

            if (!alreadyRegistered)
            {
                var index = installers.arraySize;
                installers.arraySize++;
                installers.GetArrayElementAtIndex(index).objectReferenceValue = installer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
