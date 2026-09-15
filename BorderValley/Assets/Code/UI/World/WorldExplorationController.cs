using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Persistence;
using BorderValley.Core.Random;
using BorderValley.Core.SceneManagement;
using BorderValley.Data;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;
using BorderValley.Inventory;
using BorderValley.Narrative;
using BorderValley.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using InventoryTextKeys = BorderValley.UI.Inventory.InventoryTextKeys;

namespace BorderValley.UI.World
{
    public sealed class WorldExplorationController : MonoBehaviour
    {
        [SerializeField] private float movementSpeed = 6f;
        [SerializeField] private float maxMovementStep = 0.05f;
        [SerializeField] private float playerRadius = 0.35f;
        private const string DefaultAreaId = "area.village";

        private GameContext context;
        private NarrativeStateService narrative;
        private QuestService quests;
        private DialogueService dialogueService;
        private ShopService shopService;
        private InventoryService inventory;
        private EconomyService economy;
        private PartyBattleSnapshotBuilder snapshotBuilder;
        private LootGenerator lootGenerator;
        private IBattleFlow flow;
        private ISceneLoader loader;
        private SaveService save;
        private ContentCatalog catalog;
        private IReadOnlyDictionary<string, WorldAreaDefinition> areas;
        private IReadOnlyDictionary<string, WorldEncounterDefinition> encounters;
        private IReadOnlyDictionary<string, ItemDropTableDefinition> rewardTables;
        private WorldAreaDefinition currentArea;
        private WorldInteractionResult nearestInteraction;
        private Camera worldCamera;
        private bool initialized;
        private bool battleStarted;
        private BattleResult pendingBattleResult;
        private WorldBattleSettlementResult pendingSettlement;
        private bool processingPendingResult;
        private readonly HashSet<string> suppressedExitIds = new(StringComparer.Ordinal);
        private QuestLogPanelView questLogView;
        private string pendingBattleResultKey = string.Empty;

        public GameObject Player { get; private set; }
        public VirtualJoystick Joystick { get; private set; }
        public WorldMapView MapView { get; private set; }
        public Button InteractButton { get; private set; }
        public Button QuestLogButton { get; private set; }
        public DialogueUiPresenter DialoguePresenter { get; private set; }
        public ShopUiPresenter ShopPresenter { get; private set; }
        public QuestLogPresenter QuestLogPresenter { get; private set; }
        public string CurrentAreaId => currentArea == null ? string.Empty : currentArea.Id;
        public Vector2 PlayerPosition => Player == null ? Vector2.zero : new Vector2(Player.transform.position.x, Player.transform.position.y);
        public string LastInteractionId { get; private set; } = string.Empty;
        public string LastErrorKey { get; private set; } = string.Empty;
        public string LastBattleResultKey => pendingBattleResultKey;
        public bool HasPendingSettlement => pendingBattleResult != null || pendingSettlement != null;
        public int AutosaveAttemptCount { get; private set; }
        public int SettlementCount { get; private set; }

        private void Start() => Initialize();

        private void Update()
        {
            if (initialized)
                Tick(Time.deltaTime);
        }

        private void OnApplicationPause(bool pauseStatus) => HandleApplicationPause(pauseStatus);

        public void Initialize()
        {
            if (initialized)
                return;

            context = GameBootstrapper.Context;
            if (context == null)
            {
                LastErrorKey = "world.ui.error.service_unavailable";
                return;
            }

            try
            {
                narrative = context.Get<NarrativeStateService>();
                quests = context.Get<QuestService>();
                dialogueService = context.Get<DialogueService>();
                shopService = context.Get<ShopService>();
                inventory = context.Get<InventoryService>();
                economy = context.Get<EconomyService>();
                snapshotBuilder = context.Get<PartyBattleSnapshotBuilder>();
                lootGenerator = context.Get<LootGenerator>();
                flow = context.Get<IBattleFlow>();
                loader = context.Get<ISceneLoader>();
                save = context.Get<SaveService>();
                catalog = context.Get<ContentCatalog>();
                areas = catalog.All
                    .OfType<WorldAreaDefinition>()
                    .ToDictionary(area => area.Id, StringComparer.Ordinal);
                encounters = catalog.All
                    .OfType<WorldEncounterDefinition>()
                    .ToDictionary(encounter => encounter.EncounterId, StringComparer.Ordinal);
                rewardTables = catalog.All
                    .OfType<ItemDropTableDefinition>()
                    .ToDictionary(table => table.Id, StringComparer.Ordinal);
            }
            catch (InvalidOperationException)
            {
                LastErrorKey = "world.ui.error.service_unavailable";
                return;
            }

            worldCamera = Camera.main;
            CreateMapAndPlayer();
            CreateUi();
            var startArea = ResolveStartArea();
            var startPosition = ResolveStartPosition(startArea);
            EnterArea(startArea, startPosition, false);
            TrySaveWorld();
            AutosaveAttemptCount = 0;
            initialized = true;
            ProcessPendingBattleResult();
            RefreshInteractionState();
        }

        private void CreateMapAndPlayer()
        {
            var mapObject = new GameObject("WorldMapView", typeof(WorldMapView));
            mapObject.transform.SetParent(transform, false);
            MapView = mapObject.GetComponent<WorldMapView>();

            Player = new GameObject("Player", typeof(SpriteRenderer));
            Player.transform.SetParent(transform, false);
            var renderer = Player.GetComponent<SpriteRenderer>();
            renderer.sprite = CreateWhiteSprite();
            renderer.color = new Color(0.95f, 0.82f, 0.25f, 1f);
            renderer.sortingOrder = 10;
        }

        private void CreateUi()
        {
            EnsureEventSystem();
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

            var joystickObject = new GameObject("VirtualJoystick", typeof(RectTransform), typeof(Image));
            joystickObject.transform.SetParent(canvas.transform, false);
            var joystickRect = joystickObject.GetComponent<RectTransform>();
            joystickRect.anchorMin = new Vector2(0f, 0f);
            joystickRect.anchorMax = new Vector2(0f, 0f);
            joystickRect.pivot = new Vector2(0.5f, 0.5f);
            joystickRect.anchoredPosition = new Vector2(170f, 170f);
            joystickRect.sizeDelta = new Vector2(220f, 220f);
            joystickObject.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.22f, 0.82f);
            Joystick = joystickObject.AddComponent<VirtualJoystick>();

            InteractButton = CreateButton(
                canvas.transform,
                "Interact",
                "world.ui.interact",
                new Vector2(-170f, 170f),
                new Vector2(240f, 90f),
                () => Interact());
            InteractButton.interactable = false;

            QuestLogButton = CreateButton(
                canvas.transform,
                "QuestLog",
                "world.ui.quest_log.open",
                new Vector2(-170f, -170f),
                new Vector2(240f, 80f),
                OpenQuestLog);

            var dialogueView = CreatePanel<DialoguePanelView>(canvas.transform, "DialoguePanel");
            var shopView = CreatePanel<ShopPanelView>(canvas.transform, "ShopPanel");
            questLogView = CreatePanel<QuestLogPanelView>(canvas.transform, "QuestLogPanel");
            DialoguePresenter = new DialogueUiPresenter(dialogueService, dialogueView);
            ShopPresenter = new ShopUiPresenter(shopService, inventory, economy, narrative, shopView);
            QuestLogPresenter = new QuestLogPresenter(quests, questLogView);
            dialogueView.ChoiceSelected += OnDialogueChoiceSelected;
            dialogueView.CloseRequested += OnDialogueClosed;
            ShopPresenter.BuySucceeded += OnShopBuySucceeded;
            ShopPresenter.SellSucceeded += OnShopSellSucceeded;
            shopView.CloseRequested += OnShopClosed;
            questLogView.CloseRequested += OnQuestLogClosed;
        }



        public void Tick(float deltaTime)
        {
            if (!initialized)
                return;

            RetryPendingSettlementIfSafe();
            RefreshInteractionState();
            if (battleStarted || IsUiOpen)
                return;

            var remaining = Mathf.Clamp(deltaTime, 0f, 0.25f);
            while (remaining > 0f)
            {
                var step = Mathf.Min(remaining, Mathf.Max(0.001f, maxMovementStep));
                var delta = Joystick.Value * movementSpeed * step;
                if (currentArea != null && WorldMovementSolver.TryMove(
                        PlayerPosition,
                        delta,
                        playerRadius,
                        currentArea,
                        out var next))
                {
                    SetPlayerPosition(next);
                }

                remaining -= step;
            }

            FollowCamera();
            RefreshInteractionState();
        }

        public bool Interact()
        {
            if (!initialized || battleStarted || !RefreshInteractionState() || nearestInteraction == null)
                return false;

            LastInteractionId = nearestInteraction.DefinitionId;
            switch (nearestInteraction.Kind)
            {
                case WorldInteractableKind.Npc:
                    quests.RecordTalk(nearestInteraction.TargetId, out _);
                    return OpenDialogue(nearestInteraction.TargetId);
                case WorldInteractableKind.AreaExit:
                    return SwitchArea(nearestInteraction.TargetId, nearestInteraction.ArrivalPosition, nearestInteraction.SourceId);
                case WorldInteractableKind.Chest:
                case WorldInteractableKind.Gather:
                    return GrantInteractable(nearestInteraction);
                case WorldInteractableKind.Investigate:
                    return Investigate(nearestInteraction);
                case WorldInteractableKind.Encounter:
                    return encounters.TryGetValue(nearestInteraction.TargetId, out var encounter) &&
                           BeginEncounter(encounter);
                default:
                    return false;
            }
        }

        public bool OpenDialogue(string npcId)
        {
            if (!initialized || !DialoguePresenter.Open(npcId))
                return false;

            var shopId = DialoguePresenter.ConsumeOpenedShopId();
            if (!string.IsNullOrWhiteSpace(shopId))
                return OpenShop(shopId);
            RefreshInteractionState();
            return true;
        }

        public bool OpenShop(string shopId)
        {
            if (!initialized || !ShopPresenter.Open(shopId))
            {
                LastErrorKey = ShopPresenter.LastErrorKey;
                return false;
            }

            DialoguePresenter.Close();
            RefreshInteractionState();
            return true;
        }


        public bool BeginEncounter(WorldEncounterDefinition encounter)
        {
            if (!initialized || battleStarted)
                return false;
            if (HasPendingSettlement)
            {
                LastErrorKey = WorldTextKeys.PendingSettlement;
                return false;
            }
            if (encounter == null)
                return false;
            if (!encounter.Repeatable &&
                !string.IsNullOrWhiteSpace(encounter.CompletionEventId) &&
                narrative.HasEvent(encounter.CompletionEventId))
            {
                return false;
            }
            if (!TrySaveWorld())
                return false;

            var request = WorldEncounterService.BuildRequest(
                encounter,
                snapshotBuilder.BuildPartySnapshot(),
                "world:" + encounter.EncounterId + ":" + SettlementCount,
                "World");
            flow.BeginBattle(request);
            battleStarted = true;
            InteractButton.interactable = false;
            _ = loader.LoadAsync("Battle");
            return true;
        }

        public bool ProcessPendingBattleResult()
        {
            if (!initialized || processingPendingResult)
                return false;
            processingPendingResult = true;
            try
            {
                // pendingSettlement is only assigned after rewards have been applied.
                // Retrying it below therefore only retries the autosave.
                if (pendingSettlement != null)
                    return RetryPendingAutosave();
                if (pendingBattleResult == null && !flow.TryTakeResult(out pendingBattleResult))
                    return false;

                return SettlePendingBattleResult();
            }
            finally
            {
                processingPendingResult = false;
            }
        }

        public void HandleApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus || !initialized)
                return;

            if (pendingBattleResult != null)
            {
                ProcessPendingBattleResult();
                return;
            }

            if (pendingSettlement != null)
                RetryPendingAutosave();
            else
                TrySaveWorld();
        }

        public bool RetryPendingAutosave()
        {
            if (pendingSettlement == null)
                return pendingBattleResult == null;
            if (!TrySaveWorld())
                return false;

            pendingSettlement = null;
            RefreshMap();
            return true;
        }

        private void RetryPendingSettlementIfSafe()
        {
            if (!HasPendingSettlement || inventory == null)
                return;
            if (pendingSettlement == null && inventory.Items.Count >= inventory.Capacity)
                return;

            ProcessPendingBattleResult();
        }

        private bool SettlePendingBattleResult()
        {
            var result = pendingBattleResult;
            if (result == null)
                return false;

            WorldEncounterDefinition encounter = null;
            if (result.Context != null)
                encounters.TryGetValue(result.Context.EncounterId, out encounter);

            var settlementService = new WorldBattleSettlementService(
                inventory,
                context.Get<PartyProgressionService>(),
                lootGenerator,
                quests,
                narrative,
                ResolveRewardTable,
                RandomSourceFactory.FromSeed(
                    "world-settlement:" + (encounter?.EncounterId ?? "legacy") + ":" + SettlementCount),
                context.Get<ItemDropTableDefinition>());
            if (!settlementService.Settle(result, encounter, out var settlement))
            {
                LastErrorKey = settlement.ErrorKey;
                return false;
            }

            pendingBattleResult = null;
            pendingBattleResultKey = result.Outcome switch
            {
                BattleFlowOutcome.PlayerVictory => "battle.result.player_victory",
                BattleFlowOutcome.EnemyVictory => "battle.result.enemy_victory",
                _ => "battle.result.in_progress"
            };
            SettlementCount++;
            RefreshMap();
            if (!settlement.RequiresAutosave)
                return true;

            pendingSettlement = settlement;
            return RetryPendingAutosave();
        }

        private bool RefreshInteractionState()
        {
            if (InteractButton == null)
                return false;

            UpdateExitSuppression();
            if (IsUiOpen || battleStarted)
            {
                nearestInteraction = null;
                InteractButton.interactable = false;
                return false;
            }

            var found = WorldInteractionResolver.FindNearest(
                PlayerPosition,
                currentArea,
                narrative,
                out nearestInteraction,
                suppressedExitIds,
                !HasPendingSettlement);
            InteractButton.interactable = found;
            return found;
        }

        private bool IsUiOpen =>
            DialoguePresenter != null && DialoguePresenter.IsOpen ||
            ShopPresenter != null && ShopPresenter.IsOpen ||
            QuestLogPresenter != null && QuestLogPresenter.IsOpen;

        private void OnDialogueChoiceSelected(int index)
        {
            var shopId = DialoguePresenter.ConsumeOpenedShopId();
            if (!string.IsNullOrWhiteSpace(shopId))
            {
                OpenShop(shopId);
                return;
            }

            if (quests.GetJournal().Any(entry => entry.State == QuestState.Completed))
                TrySaveWorld();
        }

        private void OnDialogueClosed()
        {
            if (ShopPresenter != null && ShopPresenter.IsOpen)
                ShopPresenter.Close();
            RefreshInteractionState();
        }

        private void OnShopBuySucceeded(string offerId) => TrySaveWorld();

        private void OnShopSellSucceeded(string instanceId) => TrySaveWorld();

        private void OnShopClosed() => RefreshInteractionState();

        private void OpenQuestLog()
        {
            QuestLogPresenter.Open();
            RefreshInteractionState();
        }

        private void OnQuestLogClosed() => RefreshInteractionState();

        private bool SwitchArea(string areaId, Vector2 arrivalPosition, string sourceAreaId)
        {
            if (!areas.TryGetValue(areaId, out var area))
                return false;

            EnterArea(area, ClampToArea(area, arrivalPosition), true);
            SuppressArrivalExit(currentArea, PlayerPosition, sourceAreaId);
            RefreshInteractionState();
            return true;
        }

        private void EnterArea(WorldAreaDefinition area, Vector2 position, bool saveAfter)
        {
            currentArea = area;
            SetPlayerPosition(ClampToArea(area, position));
            narrative.SetCurrentLocation(area.Id, PlayerPosition);
            MapView.Render(area, narrative);
            FollowCamera();
            if (saveAfter)
                TrySaveWorld();
        }

        private void UpdateExitSuppression()
        {
            if (suppressedExitIds.Count == 0 || currentArea == null)
                return;

            foreach (var interactable in currentArea.Interactables)
            {
                if (!suppressedExitIds.Contains(interactable.Id))
                    continue;
                if (Vector2.Distance(PlayerPosition, interactable.Position) <= interactable.Radius)
                    continue;
                suppressedExitIds.Remove(interactable.Id);
            }
        }

        private void SuppressArrivalExit(WorldAreaDefinition area, Vector2 arrival, string sourceAreaId)
        {
            suppressedExitIds.Clear();
            if (area == null || string.IsNullOrWhiteSpace(sourceAreaId))
                return;

            foreach (var interactable in area.Interactables)
            {
                if (interactable == null ||
                    interactable.Kind != WorldInteractableKind.AreaExit ||
                    !string.Equals(interactable.TargetId, sourceAreaId, StringComparison.Ordinal) ||
                    Vector2.Distance(arrival, interactable.Position) > interactable.Radius)
                {
                    continue;
                }

                suppressedExitIds.Add(interactable.Id);
            }
        }

        private void RefreshMap()
        {
            if (currentArea != null)
                MapView.Render(currentArea, narrative);
        }

        private bool GrantInteractable(WorldInteractionResult interaction)
        {
            if (inventory.Definitions.ContainsKey(interaction.TargetId))
            {
                var item = new ItemInstance(
                    "world:" + interaction.DefinitionId,
                    interaction.TargetId,
                    1,
                    ItemRarity.Common,
                    Array.Empty<AffixInstance>());
                if (!inventory.TryAdd(item, out var error))
                {
                    LastErrorKey = error;
                    return false;
                }
            }
            else
            {
                var table = ResolveRewardTable(interaction.TargetId);
                if (table == null)
                {
                    LastErrorKey = WorldBattleSettlementResult.MissingRewardTableKey;
                    return false;
                }

                var reward = lootGenerator.Generate(
                    "world:" + interaction.DefinitionId,
                    table,
                    context.Get<PartyProgressionService>().HighestLevel,
                    RandomSourceFactory.FromSeed("world-reward:" + interaction.DefinitionId));
                if (!inventory.TryAdd(reward, out var error))
                {
                    LastErrorKey = error;
                    return false;
                }
            }

            if (!narrative.MarkInteractableResolved(interaction.DefinitionId))
                return false;
            TrySaveWorld();
            RefreshInteractionState();
            return true;
        }

        private bool Investigate(WorldInteractionResult interaction)
        {
            if (!narrative.SetEvent(interaction.TargetId))
            {
                LastErrorKey = NarrativeTextKeys.UnknownEvent;
                return false;
            }
            if (!narrative.MarkInteractableResolved(interaction.DefinitionId))
                return false;

            TrySaveWorld();
            RefreshInteractionState();
            return true;
        }

        private ItemDropTableDefinition ResolveRewardTable(string rewardTableId)
        {
            if (!string.IsNullOrWhiteSpace(rewardTableId) &&
                rewardTables.TryGetValue(rewardTableId, out var table))
            {
                return table;
            }
            return context.Get<ItemDropTableDefinition>();
        }

        private static Vector2 ClampToArea(WorldAreaDefinition area, Vector2 position)
        {
            if (area == null)
                return position;
            return WorldMovementSolver.TryMove(area.Bounds.center, position - area.Bounds.center, 0f, area, out var clamped)
                ? clamped
                : area.Bounds.center;
        }

        private WorldAreaDefinition ResolveStartArea()
        {
            var savedAreaId = narrative.GetCurrentAreaId();
            if (!string.IsNullOrWhiteSpace(savedAreaId) && areas.TryGetValue(savedAreaId, out var savedArea))
                return savedArea;
            return areas.TryGetValue(DefaultAreaId, out var defaultArea)
                ? defaultArea
                : areas.Values.OrderBy(value => value.Id, StringComparer.Ordinal).First();
        }

        private Vector2 ResolveStartPosition(WorldAreaDefinition area)
        {
            if (string.Equals(narrative.GetCurrentAreaId(), area.Id, StringComparison.Ordinal))
            {
                var saved = narrative.GetCurrentPosition();
                if (!float.IsNaN(saved.x) && !float.IsInfinity(saved.x) &&
                    !float.IsNaN(saved.y) && !float.IsInfinity(saved.y))
                    return saved;
            }
            return string.Equals(area.Id, DefaultAreaId, StringComparison.Ordinal)
                ? new Vector2(2f, 2f)
                : area.Bounds.center;
        }

        private void SetPlayerPosition(Vector2 position)
        {
            if (Player != null)
                Player.transform.position = new Vector3(position.x, position.y, 0f);
        }

        private void FollowCamera()
        {
            if (worldCamera == null)
                worldCamera = Camera.main;
            if (worldCamera == null)
                return;
            worldCamera.transform.position = new Vector3(PlayerPosition.x, PlayerPosition.y, -10f);
        }

        private bool TrySaveWorld()
        {
            if (save == null)
                return false;
            AutosaveAttemptCount++;
            try
            {
                save.Save(0, "World");
                LastErrorKey = string.Empty;
                return true;
            }
            catch (Exception)
            {
                LastErrorKey = InventoryTextKeys.AutoSaveFailed;
                return false;
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string labelKey,
            Vector2 anchoredPosition,
            Vector2 size,
            UnityEngine.Events.UnityAction onClick)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            var anchor = new Vector2(anchoredPosition.x < 0f ? 1f : 0f, anchoredPosition.y < 0f ? 1f : 0f);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = root.GetComponent<Image>();
            image.color = new Color(0.20f, 0.35f, 0.55f, 0.94f);
            image.raycastTarget = true;
            var button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(root.transform, false);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var text = label.GetComponent<Text>();
            text.font = GetFont();
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = labelKey;
            return button;
        }

        private static T CreatePanel<T>(Transform parent, string name) where T : MonoBehaviour
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return root.AddComponent<T>();
        }

        private static Font GetFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static Sprite CreateWhiteSprite() => Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
    }
}
