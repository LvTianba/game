using System.Collections;
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
        private string returnScene = string.Empty;
        private Coroutine enemyTurnRoutine;
        private bool continueHandled;

        public int RenderedCellCount => gridView == null ? 0 : gridView.CellCount;
        public int RenderedUnitCount => gridView == null ? 0 : gridView.UnitCount;
        public Button EndTurnButton => hudView == null ? null : hudView.EndTurnButton;

        private void Start()
        {
            flow = GameBootstrapper.Context == null
                ? new BattleFlowService()
                : GameBootstrapper.Context.Get<IBattleFlow>();

            var seed = DebugSeed;
            if (flow.TryTakeRequest(out var request))
            {
                seed = request.Seed;
                returnScene = request.ReturnScene;
            }

            presenter = new BattleUiPresenter(
                BattleScenarioFactory.CreateCoreScenario(),
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
            flow.CompleteBattle(new BattleResult(
                MapOutcome(presenter.Outcome),
                presenter.Engine.State.Round));

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
            if (enemyTurnRoutine != null ||
                !isActiveAndEnabled ||
                presenter == null ||
                presenter.IsFinished ||
                presenter.ActiveUnit == null ||
                presenter.ActiveUnit.Team != Team.Enemy)
            {
                return;
            }

            enemyTurnRoutine = StartCoroutine(RunEnemyTurns());
        }

        private IEnumerator RunEnemyTurns()
        {
            while (!presenter.IsFinished &&
                   presenter.ActiveUnit != null &&
                   presenter.ActiveUnit.Team == Team.Enemy)
            {
                var actor = presenter.ActiveUnit;
                var command = BattleAi.ChooseCommand(
                    presenter.Engine,
                    actor.Id,
                    presenter.Engine.GetSkillsForUnitById(actor.Id));
                presenter.Execute(command);
                yield return new WaitForSeconds(0.35f);
            }

            enemyTurnRoutine = null;
        }

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