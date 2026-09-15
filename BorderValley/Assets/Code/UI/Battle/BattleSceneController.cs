using System;
using System.Collections;
using System.Linq;
using BorderValley.Battle.Domain;
using BorderValley.Core;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Random;
using BorderValley.Core.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BorderValley.UI.Battle
{
    public sealed class BattleSceneController : MonoBehaviour
    {
        private const string DebugSeed = "battle-scene-debug";

        private IBattleFlow flow;
        private BattleUiPresenter presenter;
        private BattleGridView gridView;
        private BattleHudView hudView;
        private BattleContext battleContext;
        private string returnScene = string.Empty;
        private Coroutine enemyTurnRoutine;
        private bool enemyTurnLoopActive;
        private bool continueHandled;

        public int RenderedCellCount => gridView == null ? 0 : gridView.CellCount;
        public int RenderedUnitCount => gridView == null ? 0 : gridView.UnitCount;
        public Button EndTurnButton => hudView == null ? null : hudView.EndTurnButton;
        public BattleEngine EngineForTests => presenter?.Engine;
        public int EnemyTurnLoopCount { get; private set; }
        public int EnemyActionCount { get; private set; }
        public bool IsEnemyTurnLoopActive => enemyTurnLoopActive;
        public Func<BattleEngine, string, BattleCommand> EnemyCommandSelector { get; set; }
        public string LastEnemyErrorKey { get; private set; } = string.Empty;
        public string ActiveUnitId => presenter == null || presenter.ActiveUnit == null
            ? string.Empty
            : presenter.ActiveUnit.Id;

        private void Start()
        {
            flow = GameBootstrapper.Context == null
                ? new BattleFlowService()
                : GameBootstrapper.Context.Get<IBattleFlow>();

            BattlePartySnapshot partySnapshot = null;
            var seed = DebugSeed;
            string scenarioId = null;
            if (flow.TryTakeRequest(out var request))
            {
                seed = request.Seed;
                scenarioId = request.ScenarioId;
                returnScene = request.ReturnScene;
                partySnapshot = request.PartySnapshot;
                battleContext = request.Context;
            }

            presenter = new BattleUiPresenter(
                BattleScenarioFactory.CreateScenario(partySnapshot, battleContext, scenarioId),
                RandomSourceFactory.FromSeed(seed));

            var root = new GameObject(
                "BattleCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            ConfigureCanvas(root.GetComponent<Canvas>(), root.GetComponent<CanvasScaler>());

            gridView = CreateGridView(root.transform);
            hudView = CreateHudView(root.transform);
            gridView.Initialize(OnCellSelected);
            hudView.Initialize(OnSkillSelected, OnEndTurn, OnContinue);

            presenter.Changed += Refresh;
            presenter.Start();
            Refresh();
        }

        private void OnDestroy()
        {
            if (presenter != null)
                presenter.Changed -= Refresh;

            if (enemyTurnRoutine != null)
                StopCoroutine(enemyTurnRoutine);
        }

        private void OnCellSelected(GridPosition position)
        {
            if (presenter == null || presenter.IsFinished)
                return;

            presenter.TapCell(position);
        }

        private void OnSkillSelected(string skillId)
        {
            if (presenter == null)
                return;

            presenter.SelectSkill(skillId);
        }

        private void OnEndTurn()
        {
            if (presenter == null)
                return;

            presenter.EndTurn();
        }

        private void OnContinue()
        {
            if (presenter == null || !presenter.IsFinished || continueHandled)
                return;

            continueHandled = true;
            var unitStates = presenter.Engine.State.Units
                .Where(unit => unit.Team == Team.Player)
                .Select(unit => new BattleUnitResult(unit.Id, unit.DefinitionId, unit.Health, unit.Mana))
                .ToArray();
            var outcome = MapOutcome(presenter.Outcome);
            var defeatedEnemyIds = outcome == BattleFlowOutcome.PlayerVictory
                ? presenter.Engine.State.Units
                    .Where(unit => unit.Team == Team.Enemy && !unit.IsAlive)
                    .Select(unit => unit.DefinitionId)
                    .ToArray()
                : Array.Empty<string>();
            flow.CompleteBattle(new BattleResult(
                outcome,
                presenter.Engine.State.Round,
                unitStates,
                battleContext,
                defeatedEnemyIds));

            if (string.IsNullOrWhiteSpace(returnScene) || GameBootstrapper.Context == null)
                return;

            if (GameBootstrapper.Context.TryGet<ISceneLoader>(out var loader))
                _ = loader.LoadAsync(returnScene);
        }

        private void Refresh()
        {
            if (presenter == null || gridView == null || hudView == null)
                return;

            gridView.Render(presenter);
            hudView.Render(presenter);
            TryStartEnemyTurns();
        }

        private void TryStartEnemyTurns()
        {
            if (enemyTurnLoopActive ||
                !isActiveAndEnabled ||
                presenter == null ||
                presenter.IsFinished ||
                presenter.ActiveUnit == null ||
                presenter.ActiveUnit.Team != Team.Enemy)
            {
                return;
            }

            enemyTurnLoopActive = true;
            enemyTurnRoutine = StartCoroutine(RunEnemyTurns());
        }

        private IEnumerator RunEnemyTurns()
        {
            EnemyTurnLoopCount++;
            try
            {
                yield return null;

                while (!presenter.IsFinished &&
                       presenter.ActiveUnit != null &&
                       presenter.ActiveUnit.Team == Team.Enemy)
                {
                    EnemyActionCount++;
                    if (!TryExecuteEnemyAction())
                        yield break;

                    yield return new WaitForSeconds(0.35f);
                }
            }
            finally
            {
                enemyTurnLoopActive = false;
                enemyTurnRoutine = null;
            }
        }

        private bool TryExecuteEnemyAction()
        {
            var actor = presenter.ActiveUnit;
            try
            {
                var command = EnemyCommandSelector == null
                    ? BattleAi.ChooseCommand(
                        presenter.Engine,
                        actor.Id,
                        presenter.Engine.GetSkillsForUnitById(actor.Id))
                    : EnemyCommandSelector(presenter.Engine, actor.Id);

                var result = presenter.Execute(command);
                if (result.Success)
                    return true;

                LastEnemyErrorKey = BattleTextKeys.AiCommandFailed;
                Debug.LogError(Key(LastEnemyErrorKey) + ": " + result.ErrorCode);

                return TryEndTurnFallback(actor);
            }
            catch (Exception exception)
            {
                LastEnemyErrorKey = BattleTextKeys.AiException;
                Debug.LogError(Key(LastEnemyErrorKey) + ": " + exception.GetType().Name);
                return TryEndTurnFallback(actor);
            }
        }

        private bool TryEndTurnFallback(BattleUnit actor)
        {
            if (actor == null)
            {
                LastEnemyErrorKey = BattleTextKeys.AiFallbackFailed;
                Debug.LogError(Key(LastEnemyErrorKey) + ": no_active_unit");
                return false;
            }

            var fallback = presenter.Execute(new EndTurnCommand(actor.Id));
            if (fallback.Success)
                return true;

            LastEnemyErrorKey = BattleTextKeys.AiFallbackFailed;
            Debug.LogError(Key(LastEnemyErrorKey) + ": " + fallback.ErrorCode);
            return false;
        }

        private static string Key(string localizationKey) => localizationKey;

        private static void ConfigureCanvas(Canvas canvas, CanvasScaler scaler)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static BattleGridView CreateGridView(Transform parent)
        {
            var root = new GameObject("BattleGrid", typeof(RectTransform), typeof(BattleGridView));
            root.transform.SetParent(parent, false);
            return root.GetComponent<BattleGridView>();
        }

        private static BattleHudView CreateHudView(Transform parent)
        {
            var root = new GameObject("BattleHud", typeof(RectTransform), typeof(BattleHudView));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return root.GetComponent<BattleHudView>();
        }

        private static BattleFlowOutcome MapOutcome(BattleOutcome outcome) =>
            outcome switch
            {
                BattleOutcome.PlayerVictory => BattleFlowOutcome.PlayerVictory,
                BattleOutcome.EnemyVictory => BattleFlowOutcome.EnemyVictory,
                _ => BattleFlowOutcome.InProgress
            };
    }
}
