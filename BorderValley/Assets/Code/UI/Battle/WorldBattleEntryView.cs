using BorderValley.Core;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorderValley.UI.Battle
{
    public sealed class WorldBattleEntryView : MonoBehaviour
    {
        private IBattleFlow flow;
        private bool battleStarted;
        private ISceneLoader loader;

        public Button BattleButton { get; private set; }
        public Text ResultLabel { get; private set; }

        private void Start()
        {
            flow = GameBootstrapper.Context == null
                ? new BattleFlowService()
                : GameBootstrapper.Context.Get<IBattleFlow>();
            loader = GameBootstrapper.Context == null
                ? new UnitySceneLoader()
                : GameBootstrapper.Context.Get<ISceneLoader>();

            CreateCanvas();
            CreateEventSystemIfMissing();

            if (flow.TryTakeResult(out var result))
            {
                ResultLabel.text = result.Outcome switch
                {
                    BattleFlowOutcome.PlayerVictory => "battle.result.player_victory",
                    BattleFlowOutcome.EnemyVictory => "battle.result.enemy_victory",
                    _ => "battle.result.in_progress"
                };
            }
        }

        private void OnDestroy()
        {
            if (BattleButton != null)
                BattleButton.onClick.RemoveAllListeners();
        }

        private void StartBattle()
        {
            if (battleStarted)
                return;

            battleStarted = true;
            BattleButton.interactable = false;
            flow.BeginBattle(new BattleRequest("core", "vertical-slice", "World"));
            _ = loader.LoadAsync("Battle");
        }

        private void CreateCanvas()
        {
            var canvasObject = new GameObject(
                "WorldCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            BattleButton = CreateButton(canvasObject.transform);
            BattleButton.onClick.AddListener(StartBattle);
            ResultLabel = CreateResultLabel(canvasObject.transform);
        }

        private static void CreateEventSystemIfMissing()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
                return;

            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
        }

        private static Button CreateButton(Transform parent)
        {
            var buttonObject = new GameObject(
                "Battle",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 80f);
            buttonObject.GetComponent<Image>().color = new Color(0.2f, 0.35f, 0.55f, 1f);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelObject.GetComponent<Text>();
            label.font = GetFont();
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = "battle.ui.enter_battle";

            return buttonObject.GetComponent<Button>();
        }

        private static Text CreateResultLabel(Transform parent)
        {
            var labelObject = new GameObject("Result", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 90f);
            rect.sizeDelta = new Vector2(640f, 60f);

            var label = labelObject.GetComponent<Text>();
            label.font = GetFont();
            label.fontSize = 24;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = string.Empty;
            return label;
        }

        private static Font GetFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
