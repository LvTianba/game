using System.Collections;
using BorderValley.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BorderValley.PlayModeTests
{
    public sealed class FoundationFlowTests
    {
        [UnityTest]
        public IEnumerator Boot_EntersMainMenu()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            Assert.That(GameBootstrapper.Context, Is.Not.Null);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0));
            Assert.That(Application.targetFrameRate, Is.EqualTo(60));
        }

        [UnityTest]
        public IEnumerator Boot_MainMenuButton_EntersWorld()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            Assert.That(Object.FindFirstObjectByType<EventSystem>(), Is.Not.Null);

            var button = Object.FindFirstObjectByType<Button>();
            Assert.That(button, Is.Not.Null);
            button.onClick.Invoke();

            for (int i = 0; i < 120 && SceneManager.GetActiveScene().name != "World"; i++)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("World"));
        }

        [UnityTest]
        public IEnumerator World_HasMainCamera_And_NonBlackBackground()
        {
            yield return SceneManager.LoadSceneAsync("World");
            yield return null;

            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "World scene should contain a Main Camera.");
            Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(camera.orthographic, Is.True);
            Assert.That(camera.backgroundColor, Is.Not.EqualTo(Color.black), "World camera background must not be black.");
        }
    }
}
