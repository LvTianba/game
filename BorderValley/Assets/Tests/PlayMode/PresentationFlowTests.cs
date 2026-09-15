using System.Collections;
using BorderValley.UI.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BorderValley.PlayModeTests
{
    public sealed class PresentationFlowTests
    {
        [UnityTest]
        public IEnumerator PlayerMovingRight_UsesWalkEastClip()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            Object.FindAnyObjectByType<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitForScene("World");
            yield return null;

            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.PlayerAnimator, Is.Not.Null);
            controller.Joystick.SetValue(Vector2.right);
            yield return null;

            Assert.That(
                controller.PlayerAnimator.CurrentClipId,
                Is.EqualTo("world.player.east.walk"));
        }

        [UnityTest]
        public IEnumerator PlayerStopped_AfterMovingEast_UsesIdleEastClip()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            Object.FindAnyObjectByType<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitForScene("World");
            yield return null;

            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            controller.Joystick.SetValue(Vector2.right);
            yield return null;
            controller.Joystick.SetValue(Vector2.zero);
            yield return null;

            Assert.That(
                controller.PlayerAnimator.CurrentClipId,
                Is.EqualTo("world.player.east.idle"));
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            for (var index = 0; index < 180 && SceneManager.GetActiveScene().name != sceneName; index++)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
        }
    }
}
