using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using BorderValley.Battle.Domain;
using BorderValley.UI.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BorderValley.PlayModeTests
{
    public sealed class BattleSceneFlowTests
    {
        [UnityTest]
        public IEnumerator BattleScene_RendersCoreScenario()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;

            var controller = Object.FindAnyObjectByType<BattleSceneController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.RenderedCellCount, Is.EqualTo(48));
            Assert.That(controller.RenderedUnitCount, Is.EqualTo(6));
            Assert.That(controller.EndTurnButton, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator BattleScene_HudDoesNotOverlapGridButtons()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;
            Canvas.ForceUpdateCanvases();

            var grid = Object.FindAnyObjectByType<BattleGridView>();
            var hud = Object.FindAnyObjectByType<BattleHudView>();
            Assert.That(grid, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);

            var cells = grid.GetComponentsInChildren<Button>(true)
                .Where(button => button.gameObject.activeInHierarchy)
                .Select(button => new
                {
                    button.name,
                    Rect = GetWorldRect(button.GetComponent<RectTransform>())
                })
                .ToArray();
            var panels = hud.GetComponentsInChildren<Image>(true)
                .Where(image => image.gameObject.activeInHierarchy &&
                                image.color.a > 0.9f &&
                                image.GetComponent<Button>() == null)
                .Select(image => new
                {
                    image.name,
                    Rect = GetWorldRect(image.GetComponent<RectTransform>())
                });
            var buttons = hud.GetComponentsInChildren<Button>(true)
                .Where(button => button.gameObject.activeInHierarchy)
                .Select(button => new
                {
                    button.name,
                    Rect = GetWorldRect(button.GetComponent<RectTransform>())
                });
            var obstacles = panels.Concat(buttons).ToArray();

            Assert.That(cells, Has.Length.EqualTo(48));
            foreach (var cell in cells)
            {
                foreach (var obstacle in obstacles)
                {
                    Assert.That(
                        Overlaps(cell.Rect, obstacle.Rect),
                        Is.False,
                        $"{obstacle.name} overlaps grid cell {cell.name}.");
                }
            }
        }

        [UnityTest]
        public IEnumerator BattleScene_EnemyTurnStartsSingleLoopAndOneActionPerStep()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;

            var controller = Object.FindAnyObjectByType<BattleSceneController>();
            Assert.That(controller, Is.Not.Null);

            controller.EndTurnButton.onClick.Invoke();
            for (var i = 0; i < 20 && controller.EnemyTurnLoopCount == 0; i++)
                yield return null;

            Assert.That(controller.EnemyTurnLoopCount, Is.EqualTo(1));
            Assert.That(controller.IsEnemyTurnLoopActive, Is.True);

            for (var i = 0; i < 20 && controller.EnemyActionCount == 0; i++)
                yield return null;

            Assert.That(controller.EnemyActionCount, Is.EqualTo(1));
            yield return null;
            Assert.That(controller.EnemyTurnLoopCount, Is.EqualTo(1));
            Assert.That(controller.EnemyActionCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BattleScene_HudTextUsesLocalizationKeys()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;

            var hud = Object.FindAnyObjectByType<BattleHudView>();
            Assert.That(hud, Is.Not.Null);

            var activeUnit = hud.GetComponentsInChildren<Text>(true)
                .Single(text => text.name == "ActiveUnit");
            var stats = hud.GetComponentsInChildren<Text>(true)
                .Single(text => text.name == "UnitStats");
            var turnOrder = hud.GetComponentsInChildren<Text>(true)
                .Single(text => text.name == "TurnOrder");

            Assert.That(activeUnit.text, Is.EqualTo("battle.hud.active_unit: battle.unit.ranger"));
            Assert.That(stats.text, Does.Contain("battle.ui.moved: battle.ui.no"));
            Assert.That(stats.text, Does.Contain("battle.ui.acted: battle.ui.no"));
            Assert.That(stats.text, Does.Not.Contain("True"));
            Assert.That(stats.text, Does.Not.Contain("False"));
            Assert.That(turnOrder.text, Does.Not.Contain(" unit."));
        }

        [Test]
        public void BattleTextKeys_FormatHudValuesAsLocalizationKeys()
        {
            Assert.That(BattleTextKeys.Unit("unit.ranger"), Is.EqualTo("battle.unit.ranger"));
            Assert.That(BattleTextKeys.Flag(false), Is.EqualTo("battle.ui.no"));
            Assert.That(BattleTextKeys.StatusKey(StatusType.Stunned), Is.EqualTo("battle.status.stunned"));
        }

        [UnityTest]
        public IEnumerator BattleScene_EnemyCommandFailure_FallsBackAndClearsLoop()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;

            var controller = Object.FindAnyObjectByType<BattleSceneController>();
            Assert.That(controller, Is.Not.Null);
            controller.EnemyCommandSelector = (_, unitId) =>
                new MoveCommand(unitId, new GridPosition(-1, -1));

            LogAssert.Expect(LogType.Error, new Regex("^battle.ui.error.ai_command_failed:"));
            controller.EndTurnButton.onClick.Invoke();

            for (var i = 0; i < 20 && controller.IsEnemyTurnLoopActive; i++)
                yield return new WaitForSeconds(0.05f);

            Assert.That(controller.EnemyTurnLoopCount, Is.EqualTo(1));
            Assert.That(controller.EnemyActionCount, Is.EqualTo(1));
            Assert.That(controller.IsEnemyTurnLoopActive, Is.False);
            Assert.That(controller.LastEnemyErrorKey, Is.EqualTo(BattleTextKeys.AiCommandFailed));
            Assert.That(controller.ActiveUnitId, Does.StartWith("player."));
        }

        [UnityTest]
        public IEnumerator BattleScene_EnemyCommandException_ClearsLoopAndLogsKey()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;

            var controller = Object.FindAnyObjectByType<BattleSceneController>();
            Assert.That(controller, Is.Not.Null);
            controller.EnemyCommandSelector = (_, _) =>
                throw new System.InvalidOperationException("selector failed");

            LogAssert.Expect(LogType.Error, new Regex("^battle.ui.error.ai_exception:"));
            controller.EndTurnButton.onClick.Invoke();

            for (var i = 0; i < 20 && controller.IsEnemyTurnLoopActive; i++)
                yield return new WaitForSeconds(0.05f);

            Assert.That(controller.EnemyTurnLoopCount, Is.EqualTo(1));
            Assert.That(controller.EnemyActionCount, Is.EqualTo(1));
            Assert.That(controller.IsEnemyTurnLoopActive, Is.False);
            Assert.That(controller.LastEnemyErrorKey, Is.EqualTo(BattleTextKeys.AiException));
        }

        private static Rect GetWorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(
                corners.Min(corner => corner.x),
                corners.Min(corner => corner.y),
                corners.Max(corner => corner.x),
                corners.Max(corner => corner.y));
        }

        private static bool Overlaps(Rect left, Rect right) =>
            left.xMin < right.xMax &&
            left.xMax > right.xMin &&
            left.yMin < right.yMax &&
            left.yMax > right.yMin;
    }
}
