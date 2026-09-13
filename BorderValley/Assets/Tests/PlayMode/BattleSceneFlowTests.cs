using System.Collections;
using BorderValley.UI.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BorderValley.PlayModeTests
{
    public sealed class BattleSceneFlowTests
    {
        [UnityTest]
        public IEnumerator BattleScene_RendersCoreScenario()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;

            var controller = Object.FindFirstObjectByType<BattleSceneController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.RenderedCellCount, Is.EqualTo(48));
            Assert.That(controller.RenderedUnitCount, Is.EqualTo(6));
            Assert.That(controller.EndTurnButton, Is.Not.Null);
        }
    }
}