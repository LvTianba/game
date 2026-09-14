using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using BorderValley.Battle.Domain;
using BorderValley.Core;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.SceneManagement;
using BorderValley.Data;
using BorderValley.Data.World;
using BorderValley.UI.Battle;
using BorderValley.UI.World;
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
        public IEnumerator MainMenu_ToWorld_ToBattle()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;

            var menuButton = Object.FindAnyObjectByType<Button>();
            Assert.That(menuButton, Is.Not.Null);
            menuButton.onClick.Invoke();
            yield return WaitForScene("World");

            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.BeginEncounter(FindEncounter("encounter.forest.bandits")), Is.True);
            yield return WaitForScene("Battle");

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Battle"));
            Assert.That(Object.FindAnyObjectByType<BattleSceneController>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator World_ConsumesLegacyBattleResultOnce()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            yield return WaitForScene("MainMenu");

            var flow = GameBootstrapper.Context.Get<IBattleFlow>();
            flow.CompleteBattle(new BattleResult(BattleFlowOutcome.PlayerVictory, 3));

            yield return SceneManager.LoadSceneAsync("World");
            yield return null;

            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.LastBattleResultKey, Is.EqualTo("battle.result.player_victory"));
            Assert.That(controller.SettlementCountForTests, Is.EqualTo(1));

            yield return SceneManager.LoadSceneAsync("World");
            yield return null;

            controller = Object.FindAnyObjectByType<WorldExplorationController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.LastBattleResultKey, Is.Empty);
            Assert.That(flow.TryTakeResult(out _), Is.False);
        }

        [UnityTest]
        public IEnumerator World_EncounterDoubleTrigger_BeginsOneBattleAndLoad()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            yield return WaitForScene("MainMenu");

            var originalLoader = GameBootstrapper.Context.Get<ISceneLoader>();
            var loader = new RecordingSceneLoader();
            GameBootstrapper.Context.Register<ISceneLoader>(loader);

            yield return SceneManager.LoadSceneAsync("World");
            yield return null;

            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            Assert.That(controller, Is.Not.Null);
            var encounter = FindEncounter("encounter.forest.bandits");

            Assert.That(controller.BeginEncounter(encounter), Is.True);
            Assert.That(controller.BeginEncounter(encounter), Is.False);
            Assert.That(controller.InteractButton.interactable, Is.False);
            Assert.That(loader.LoadCount, Is.EqualTo(1));

            var flow = GameBootstrapper.Context.Get<IBattleFlow>();
            Assert.That(flow.TryTakeRequest(out var request), Is.True);
            Assert.That(request.ScenarioId, Is.EqualTo("core"));
            Assert.That(flow.TryTakeRequest(out _), Is.False);

            GameBootstrapper.Context.Register<ISceneLoader>(originalLoader);
        }

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
            Assert.That(BattleTextKeys.Unit("enemy.bandit"), Is.EqualTo("battle.unit.bandit"));
            Assert.That(BattleTextKeys.Flag(false), Is.EqualTo("battle.ui.no"));
            Assert.That(BattleTextKeys.StatusKey(StatusType.Stunned), Is.EqualTo("battle.status.stunned"));
        }

        [UnityTest]
        public IEnumerator BattleScene_EnemyCommandFailure_FallsBackAndAdvancesTurn()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;

            var controller = Object.FindAnyObjectByType<BattleSceneController>();
            Assert.That(controller, Is.Not.Null);
            string enemyUnitId = null;
            controller.EnemyCommandSelector = (_, unitId) =>
            {
                enemyUnitId = unitId;
                return new MoveCommand(unitId, new GridPosition(-1, -1));
            };

            LogAssert.Expect(LogType.Error, new Regex("^battle.ui.error.ai_command_failed:"));
            controller.EndTurnButton.onClick.Invoke();

            for (var i = 0; i < 20 && controller.IsEnemyTurnLoopActive; i++)
                yield return new WaitForSeconds(0.05f);

            Assert.That(controller.EnemyTurnLoopCount, Is.EqualTo(1));
            Assert.That(controller.EnemyActionCount, Is.EqualTo(1));
            Assert.That(controller.IsEnemyTurnLoopActive, Is.False);
            Assert.That(controller.LastEnemyErrorKey, Is.EqualTo(BattleTextKeys.AiCommandFailed));
            Assert.That(enemyUnitId, Does.StartWith("enemy."));
            Assert.That(controller.ActiveUnitId, Does.StartWith("player."));
            Assert.That(controller.ActiveUnitId, Is.Not.EqualTo(enemyUnitId));
        }

        [UnityTest]
        public IEnumerator BattleScene_EnemyCommandException_FallsBackAndAdvancesTurn()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;

            var controller = Object.FindAnyObjectByType<BattleSceneController>();
            Assert.That(controller, Is.Not.Null);
            string enemyUnitId = null;
            controller.EnemyCommandSelector = (_, unitId) =>
            {
                enemyUnitId = unitId;
                throw new System.InvalidOperationException("selector failed");
            };

            LogAssert.Expect(LogType.Error, new Regex("^battle.ui.error.ai_exception:"));
            controller.EndTurnButton.onClick.Invoke();

            for (var i = 0; i < 20 && controller.IsEnemyTurnLoopActive; i++)
                yield return new WaitForSeconds(0.05f);

            Assert.That(controller.EnemyTurnLoopCount, Is.EqualTo(1));
            Assert.That(controller.EnemyActionCount, Is.EqualTo(1));
            Assert.That(controller.IsEnemyTurnLoopActive, Is.False);
            Assert.That(controller.LastEnemyErrorKey, Is.EqualTo(BattleTextKeys.AiException));
            Assert.That(enemyUnitId, Does.StartWith("enemy."));
            Assert.That(controller.ActiveUnitId, Does.StartWith("player."));
            Assert.That(controller.ActiveUnitId, Is.Not.EqualTo(enemyUnitId));
        }

        private static WorldEncounterDefinition FindEncounter(string encounterId)
        {
            var catalog = Resources.Load<ContentCatalog>("ContentCatalog");
            Assert.That(catalog, Is.Not.Null);
            return catalog.All.OfType<WorldEncounterDefinition>().Single(value => value.EncounterId == encounterId);
        }

        private sealed class RecordingSceneLoader : ISceneLoader
        {
            public int LoadCount { get; private set; }
            public string ActiveSceneName => SceneManager.GetActiveScene().name;

            public System.Threading.Tasks.Task LoadAsync(string sceneName)
            {
                LoadCount++;
                return System.Threading.Tasks.Task.CompletedTask;
            }
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

        private static IEnumerator WaitForScene(string sceneName)
        {
            for (var i = 0; i < 180 && SceneManager.GetActiveScene().name != sceneName; i++)
                yield return null;
        }

        private static bool Overlaps(Rect left, Rect right) =>
            left.xMin < right.xMax &&
            left.xMax > right.xMin &&
            left.yMin < right.yMax &&
            left.yMax > right.yMin;
    }
}
