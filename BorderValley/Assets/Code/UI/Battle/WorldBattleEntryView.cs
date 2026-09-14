using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Persistence;
using BorderValley.Core.Random;
using BorderValley.Core.SceneManagement;
using BorderValley.Data.Items;
using BorderValley.Inventory;
using BorderValley.UI.Inventory;
using InventoryTextKeys = BorderValley.UI.Inventory.InventoryTextKeys;
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
        private InventoryService inventory;
        private PartyProgressionService progression;
        private PartyBattleSnapshotBuilder snapshotBuilder;
        private CraftingService crafting;
        private CraftingCosts costs;
        private IReadOnlyDictionary<string, AffixDefinition> affixDefinitions;
        private LootGenerator lootGenerator;
        private EconomyService economy;
        private ItemDropTableDefinition banditDropTable;
        private SaveService saveService;
        private PendingBattleReward pendingReward;

        public Button InventoryButton { get; private set; }
        public Button CraftButton { get; private set; }
        public Button RestButton { get; private set; }
        public Button BattleButton { get; private set; }
        public Button RetryButton { get; private set; }
        public Text ResultLabel { get; private set; }
        public Text PartyHealthLabel { get; private set; }
        public InventoryPanelView InventoryPanel { get; private set; }
        public bool HasPendingRewardForTests => pendingReward != null;
        public int SettlementCountForTests { get; private set; }

        private void Start()
        {
            if (GameBootstrapper.Context == null)
            {
                flow = new BattleFlowService();
                loader = new UnitySceneLoader();
            }
            else
            {
                flow = GameBootstrapper.Context.Get<IBattleFlow>();
                loader = GameBootstrapper.Context.Get<ISceneLoader>();
                GameBootstrapper.Context.TryGet(out inventory);
                GameBootstrapper.Context.TryGet(out progression);
                GameBootstrapper.Context.TryGet(out snapshotBuilder);
                GameBootstrapper.Context.TryGet(out crafting);
                GameBootstrapper.Context.TryGet(out costs);
                GameBootstrapper.Context.TryGet(out affixDefinitions);
                GameBootstrapper.Context.TryGet(out lootGenerator);
                GameBootstrapper.Context.TryGet(out economy);
                GameBootstrapper.Context.TryGet(out banditDropTable);
                GameBootstrapper.Context.TryGet(out saveService);
            }

            var canvasObject = CreateCanvas();
            CreateEventSystemIfMissing();
            CreateButtons(canvasObject.transform);
            CreateInventoryPanel(canvasObject.transform);
            RefreshPartyLabel();
            ConsumePendingResult();
        }

        private void OnEnable()
        {
            if (flow != null)
                ConsumePendingResult();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus || GameBootstrapper.Context == null)
                return;

            if (saveService == null && !GameBootstrapper.Context.TryGet(out saveService))
                return;

            if (!TrySaveWorld() && ResultLabel != null)
                ResultLabel.text = "save.error.autosave_failed";
        }

        private void OnDestroy()
        {
            if (InventoryButton != null) InventoryButton.onClick.RemoveAllListeners();
            if (CraftButton != null) CraftButton.onClick.RemoveAllListeners();
            if (RestButton != null) RestButton.onClick.RemoveAllListeners();
            if (BattleButton != null) BattleButton.onClick.RemoveAllListeners();
            if (RetryButton != null) RetryButton.onClick.RemoveAllListeners();
        }

        private void CreateButtons(Transform parent)
        {
            InventoryButton = CreateButton(
                parent,
                "Inventory",
                InventoryTextKeys.OpenInventory,
                new Vector2(-720f, -420f),
                () => InventoryPanel?.Open(InventoryPanelMode.Inventory));
            CraftButton = CreateButton(
                parent,
                "Craft",
                InventoryTextKeys.OpenCraft,
                new Vector2(-360f, -420f),
                () => InventoryPanel?.Open(InventoryPanelMode.Craft));
            RestButton = CreateButton(
                parent,
                "Rest",
                InventoryTextKeys.Rest,
                new Vector2(0f, -420f),
                Rest);
            BattleButton = CreateButton(
                parent,
                "Battle",
                InventoryTextKeys.Battle,
                new Vector2(360f, -420f),
                StartBattle);
            RetryButton = CreateButton(
                parent,
                "Retry",
                InventoryTextKeys.Retry,
                new Vector2(720f, -420f),
                () => ConsumePendingResult());
            ResultLabel = CreateLabel(parent, "Result", new Vector2(0f, 100f), 24);
            PartyHealthLabel = CreateLabel(parent, "PartyHealth", new Vector2(0f, 20f), 20);
        }

        private void CreateInventoryPanel(Transform parent)
        {
            var panelObject = new GameObject("InventoryPanel", typeof(RectTransform));
            panelObject.transform.SetParent(parent, false);
            var rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            InventoryPanel = panelObject.AddComponent<InventoryPanelView>();
            InventoryPanel.Initialize(
                inventory,
                progression,
                crafting,
                costs,
                affixDefinitions,
                economy,
                _ => TrySaveWorld());
        }

        public bool ConsumePendingResultForTests() => ConsumePendingResult();

        private bool ConsumePendingResult()
        {
            if (flow == null || progression == null)
                return false;

            if (pendingReward == null)
            {
                if (!flow.TryTakeResult(out var result))
                    return false;
                pendingReward = new PendingBattleReward(result);
            }

            return TrySettlePendingReward();
        }

        private bool TrySettlePendingReward()
        {
            var pending = pendingReward;
            if (pending == null)
                return false;

            try
            {
                if (pending.Result.Outcome == BattleFlowOutcome.PlayerVictory)
                {
                    if (!pending.RewardsPrepared)
                    {
                        if (inventory == null || lootGenerator == null || banditDropTable == null)
                            throw new InvalidOperationException("Reward services are unavailable.");
                        var rewardSeed = $"reward:{progression.SafePointId}:{pending.Result.Rounds}:{progression.TotalExperience}";
                        var random = RandomSourceFactory.FromSeed(rewardSeed);
                        pending.Loot = lootGenerator.Generate(
                            "loot." + System.Guid.NewGuid().ToString("N"),
                            banditDropTable,
                            progression.HighestLevel,
                            random);
                        pending.RewardsPrepared = true;
                    }

                    if (!pending.LootAdded)
                    {
                        if (inventory.Items.Count >= inventory.Capacity)
                        {
                            ShowResult(InventoryTextKeys.BagFull);
                            return false;
                        }

                        if (!inventory.TryAdd(pending.Loot, out var addError))
                        {
                            ShowResult(addError);
                            return false;
                        }

                        pending.LootAdded = true;
                    }

                    if (!pending.RewardsApplied)
                    {
                        progression.ApplyBattleUnitStates(pending.Result.UnitStates);
                        inventory.AddGold(25 + pending.Result.Rounds * 5);
                        progression.AwardExperience(35 + pending.Result.Rounds * 5);
                        pending.RewardsApplied = true;
                    }
                }
                else if (pending.Result.Outcome == BattleFlowOutcome.EnemyVictory)
                {
                    if (!pending.RewardsApplied)
                    {
                        progression.ApplyBattleUnitStates(pending.Result.UnitStates);
                        progression.ReturnToSafePoint();
                        pending.RewardsApplied = true;
                    }
                }
                else
                {
                    pendingReward = null;
                    return false;
                }

                if (!TrySaveWorld())
                {
                    ShowResult(InventoryTextKeys.AutoSaveFailed);
                    return false;
                }

                pendingReward = null;
                SettlementCountForTests++;
                RefreshResultAndPartyLabels(pending.Result, false);
                return true;
            }
            catch (System.Exception)
            {
                ShowResult(InventoryTextKeys.ServiceUnavailable);
                return false;
            }
        }

        private void ShowResult(string key)
        {
            if (ResultLabel != null)
                ResultLabel.text = key;
            RefreshPartyLabel();
        }

        private sealed class PendingBattleReward
        {
            public PendingBattleReward(BattleResult result)
            {
                Result = result;
            }

            public BattleResult Result { get; }
            public ItemInstance Loot { get; set; }
            public bool RewardsPrepared { get; set; }
            public bool LootAdded { get; set; }
            public bool RewardsApplied { get; set; }
        }

        private bool TrySaveWorld()
        {
            if (saveService == null)
                return false;

            try
            {
                saveService.Save(0, "World");
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private void RefreshResultAndPartyLabels(BattleResult result, bool autosaveFailed)
        {
            ResultLabel.text = autosaveFailed
                ? "save.error.autosave_failed"
                : result.Outcome switch
                {
                    BattleFlowOutcome.PlayerVictory => "battle.result.player_victory",
                    BattleFlowOutcome.EnemyVictory => "battle.result.enemy_victory",
                    _ => "battle.result.in_progress"
                };
            RefreshPartyLabel();
        }

        private void StartBattle()
        {
            if (battleStarted || flow == null || snapshotBuilder == null)
            {
                ShowResult(InventoryTextKeys.ServiceUnavailable);
                return;
            }

            if (!TrySaveWorld())
            {
                ShowResult(InventoryTextKeys.AutoSaveFailed);
                return;
            }

            BattlePartySnapshot partySnapshot;
            try
            {
                partySnapshot = snapshotBuilder.BuildPartySnapshot();
            }
            catch (System.Exception)
            {
                ShowResult(InventoryTextKeys.ServiceUnavailable);
                return;
            }

            battleStarted = true;
            BattleButton.interactable = false;
            flow.BeginBattle(new BattleRequest("core", "vertical-slice", "World", partySnapshot));
            if (loader != null)
                _ = loader.LoadAsync("Battle");
        }

        private void Rest()
        {
            if (progression == null)
            {
                ResultLabel.text = InventoryTextKeys.ServiceUnavailable;
                return;
            }

            progression.RecoverOutOfCombat(10);
            RefreshPartyLabel();
        }

        private void RefreshPartyLabel()
        {
            if (PartyHealthLabel == null)
                return;
            if (progression == null)
            {
                PartyHealthLabel.text = "inventory.ui.error.service_unavailable";
                return;
            }

            PartyHealthLabel.text =
                progression.SafePointId + "  " +
                string.Join(
                    "  ",
                    progression.Members.Select(member => member.MemberId + " " + member.CurrentHealth));
        }

        private GameObject CreateCanvas()
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
            return canvasObject;
        }

        private static void CreateEventSystemIfMissing()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
                return;

            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string key,
            Vector2 position,
            UnityEngine.Events.UnityAction action)
        {
            var buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(300f, 76f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.2f, 0.35f, 0.55f, 1f);
            image.raycastTarget = true;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            var label = CreateLabel(buttonObject.transform, "Label", Vector2.zero, 20);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.text = key;
            return button;
        }

        private static Text CreateLabel(Transform parent, string name, Vector2 position, int size)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(900f, 60f);

            var label = labelObject.GetComponent<Text>();
            label.font = GetFont();
            label.fontSize = size;
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
