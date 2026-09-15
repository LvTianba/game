using System.Collections;
using System.Linq;
using BorderValley.Core;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Persistence;
using BorderValley.Data;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;
using BorderValley.Inventory;
using BorderValley.Narrative;
using BorderValley.UI.Battle;
using BorderValley.UI.World;
using BorderValley.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BorderValley.PlayModeTests
{
    public sealed class WorldNarrativeFlowTests
    {
        private const string VillageId = "area.village";
        private const string ForestId = "area.forest";
        private const string ElderId = "npc.elder";
        private const string MerchantId = "npc.merchant";
        private const string MainQuestId = "quest.main.crypt";

        [UnityTest]
        public IEnumerator Boot_ToWorld_CreatesTouchMapPlayerAndVillageContent()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));

            Object.FindAnyObjectByType<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitForScene("World");
            yield return null;

            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.CurrentAreaId, Is.EqualTo(VillageId));
            Assert.That(controller.Player, Is.Not.Null);
            Assert.That(controller.MapView, Is.Not.Null);
            Assert.That(controller.Joystick.Value, Is.EqualTo(Vector2.zero));

            PressJoystick(controller, Vector2.right);
            Assert.That(controller.Joystick.Value.x, Is.GreaterThan(0.95f));
            ReleaseJoystick(controller);
            Assert.That(controller.Joystick.Value, Is.EqualTo(Vector2.zero));

            Assert.That(controller.MapView.HasBackground, Is.True);
            Assert.That(controller.MapView.ObstacleCount, Is.GreaterThan(0));
            Assert.That(controller.MapView.MarkerCount(WorldInteractableKind.Npc), Is.GreaterThanOrEqualTo(4));
            Assert.That(controller.MapView.MarkerCount(WorldInteractableKind.Gather), Is.GreaterThan(0));
            Assert.That(controller.MapView.MarkerCount(WorldInteractableKind.Chest), Is.GreaterThan(0));
            Assert.That(controller.MapView.MarkerCount(WorldInteractableKind.Investigate), Is.GreaterThan(0));
            Assert.That(controller.MapView.EncounterMarkerCount, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator MovingToElder_EnablesInteraction_AndOpensDialogue()
        {
            yield return LoadWorld();
            var controller = GetController();
            var elder = FindNpcInteractable(VillageId, ElderId);

            yield return MoveNear(controller, elder.Position);

            Assert.That(controller.InteractButton.interactable, Is.True);
            ClickInteract(controller);
            Assert.That(controller.DialoguePresenter.IsOpen, Is.True);
            Assert.That(controller.LastInteractionId, Is.EqualTo(elder.Id));

            var dialogue = Object.FindAnyObjectByType<DialoguePanelView>();
            FindButton(dialogue, "Close").onClick.Invoke();
            yield return null;
            Assert.That(controller.DialoguePresenter.IsOpen, Is.False);
            Assert.That(controller.InteractButton.interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator AcceptingMainQuest_AddsObjectiveToQuestLog()
        {
            yield return LoadWorld();
            var controller = GetController();
            var quests = GameBootstrapper.Context.Get<QuestService>();
            var elder = FindNpcInteractable(VillageId, ElderId);

            yield return MoveNear(controller, elder.Position);
            ClickInteract(controller);
            Assert.That(controller.DialoguePresenter.SelectChoice(0), Is.True);

            var entry = quests.GetJournal().Single(value => value.QuestId == MainQuestId);
            Assert.That(entry.State, Is.EqualTo(QuestState.Active));
            Assert.That(entry.Objectives, Is.Not.Empty);
            Assert.That(entry.Objectives.Any(value => value.TargetId == "enemy.bandit"), Is.True);
        }

        [UnityTest]
        public IEnumerator Merchant_RealShopPanel_BuyAndSellAutosaveOnlyAfterSuccess()
        {
            yield return LoadWorld();
            var controller = GetController();
            var context = GameBootstrapper.Context;
            var inventory = context.Get<InventoryService>();
            var merchant = FindNpcInteractable(VillageId, MerchantId);

            yield return MoveNear(controller, merchant.Position);
            ClickInteract(controller);
            Assert.That(controller.ShopPresenter.IsOpen, Is.True);
            Assert.That(controller.ShopPresenter.CurrentShopId, Is.EqualTo("shop.general"));
            Assert.That(controller.InteractButton.interactable, Is.False);

            var shopView = Object.FindAnyObjectByType<ShopPanelView>();
            var goldBeforeBuy = inventory.Gold;
            var itemsBeforeBuy = inventory.Items.Count;
            var attemptsBeforeBuy = controller.AutosaveAttemptCount;
            FindButton(shopView, "Buy_0").onClick.Invoke();
            Assert.That(inventory.Gold, Is.LessThan(goldBeforeBuy));
            Assert.That(inventory.Items.Count, Is.EqualTo(itemsBeforeBuy + 1));
            Assert.That(controller.AutosaveAttemptCount, Is.EqualTo(attemptsBeforeBuy + 1));

            var attemptsBeforeFailedBuy = controller.AutosaveAttemptCount;
            Assert.That(inventory.TrySpendGold(Mathf.Max(0, inventory.Gold - 1)), Is.True);
            FindButton(shopView, "Buy_0").onClick.Invoke();
            Assert.That(inventory.Gold, Is.EqualTo(1));
            Assert.That(controller.AutosaveAttemptCount, Is.EqualTo(attemptsBeforeFailedBuy));

            inventory.AddGold(100);
            FindButton(shopView, "Buy_0").onClick.Invoke();
            Assert.That(inventory.Items.Count, Is.EqualTo(itemsBeforeBuy + 2));
            Assert.That(controller.AutosaveAttemptCount, Is.EqualTo(attemptsBeforeBuy + 2));

            FindButton(shopView, "SellTab").onClick.Invoke();
            var goldBeforeSell = inventory.Gold;
            var itemsBeforeSell = inventory.Items.Count;
            var attemptsBeforeSell = controller.AutosaveAttemptCount;
            FindButton(shopView, "Sell_0").onClick.Invoke();
            Assert.That(inventory.Gold, Is.GreaterThan(goldBeforeSell));
            Assert.That(inventory.Items.Count, Is.EqualTo(itemsBeforeSell - 1));
            Assert.That(controller.AutosaveAttemptCount, Is.EqualTo(attemptsBeforeSell + 1));

            var remaining = inventory.Items.FirstOrDefault();
            Assert.That(remaining, Is.Not.Null);
            var goldBeforeFailedSell = inventory.Gold;
            var attemptsBeforeFailedSell = controller.AutosaveAttemptCount;
            Assert.That(inventory.TryRemove(remaining.InstanceId), Is.True);
            FindButton(shopView, "Sell_0").onClick.Invoke();
            Assert.That(inventory.Gold, Is.EqualTo(goldBeforeFailedSell));
            Assert.That(controller.AutosaveAttemptCount, Is.EqualTo(attemptsBeforeFailedSell));

            FindButton(shopView, "Close").onClick.Invoke();
            yield return null;
            Assert.That(controller.ShopPresenter.IsOpen, Is.False);
            Assert.That(controller.InteractButton.interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator AreaExit_DoesNotRetriggerUntilPlayerLeavesArrivalExit()
        {
            yield return LoadWorld();
            var controller = GetController();
            var context = GameBootstrapper.Context;
            var state = context.Get<NarrativeStateService>();
            var save = context.Get<SaveService>();
            var exit = FindAreaExit(VillageId, ForestId);

            yield return MoveNear(controller, exit.Position);
            ClickInteract(controller);

            Assert.That(controller.CurrentAreaId, Is.EqualTo(ForestId));
            Assert.That(Vector2.Distance(controller.PlayerPosition, exit.ArrivalPosition), Is.LessThan(0.05f));
            Assert.That(save.HasSave(0), Is.True);
            Assert.That(controller.InteractButton.interactable, Is.False);
            Assert.That(controller.Interact(), Is.False);
            Assert.That(controller.CurrentAreaId, Is.EqualTo(ForestId));

            yield return MoveNear(controller, new Vector2(6f, 6f));
            var returnExit = FindAreaExit(ForestId, VillageId);
            yield return MoveNear(controller, returnExit.Position);
            Assert.That(controller.InteractButton.interactable, Is.True);
            ClickInteract(controller);
            Assert.That(controller.CurrentAreaId, Is.EqualTo(VillageId));

            Assert.That(state.SetCurrentLocation(ForestId, returnExit.ArrivalPosition), Is.True);
            Assert.That(save.Load(0), Is.True);
            Assert.That(state.GetCurrentAreaId(), Is.EqualTo(VillageId));
        }

        [UnityTest]
        public IEnumerator BanditEncounter_VictoryReturnsToWorld_AndAdvancesQuestAndLoot()
        {
            yield return LoadWorld();
            var controller = GetController();
            var context = GameBootstrapper.Context;
            var quests = context.Get<QuestService>();
            var state = context.Get<NarrativeStateService>();
            var inventory = context.Get<InventoryService>();
            var flow = context.Get<IBattleFlow>();

            yield return AcceptMainQuest(controller);
            var objective = quests.GetJournal()
                .Single(value => value.QuestId == MainQuestId)
                .Objectives.Single(value => value.TargetId == "enemy.bandit");
            var objectiveId = objective.ObjectiveId;

            var villageExit = FindAreaExit(VillageId, ForestId);
            yield return MoveNear(controller, villageExit.Position);
            ClickInteract(controller);
            Assert.That(controller.CurrentAreaId, Is.EqualTo(ForestId));

            var encounter = FindEncounterInteractable(ForestId, "encounter.forest.bandits");
            var goldBefore = inventory.Gold;
            var itemsBefore = inventory.Items.Count;
            yield return MoveNear(controller, encounter.Position);
            ClickInteract(controller);
            yield return WaitForScene("Battle");
            yield return null;

            var battle = Object.FindAnyObjectByType<BattleSceneController>();
            Assert.That(battle, Is.Not.Null);
            KillAllEnemies(battle);
            Object.FindAnyObjectByType<BattleHudView>().ContinueButton.onClick.Invoke();
            yield return WaitForScene("World");
            yield return null;
            controller = GetController();

            Assert.That(flow.TryTakeResult(out _), Is.False);
            Assert.That(controller.LastBattleResultKey, Is.EqualTo("battle.result.player_victory"));
            Assert.That(
                state.GetObjectiveProgress(MainQuestId, objectiveId),
                Is.GreaterThanOrEqualTo(1));
            Assert.That(inventory.Gold, Is.GreaterThan(goldBefore));
            Assert.That(inventory.Items.Count, Is.GreaterThan(itemsBefore));
        }

        [UnityTest]
        public IEnumerator QuestLog_RealOpenAndClose_RefreshesInteractButton()
        {
            yield return LoadWorld();
            var controller = GetController();
            yield return AcceptMainQuest(controller);

            var questLog = Object.FindAnyObjectByType<QuestLogPanelView>();
            controller.QuestLogButton.onClick.Invoke();
            Assert.That(controller.QuestLogPresenter.IsOpen, Is.True);
            Assert.That(controller.InteractButton.interactable, Is.False);
            Assert.That(
                questLog.GetComponentsInChildren<Text>(true).Any(value => value.text.Contains(MainQuestId)),
                Is.True);

            FindButton(questLog, "Close").onClick.Invoke();
            yield return null;
            Assert.That(controller.QuestLogPresenter.IsOpen, Is.False);
            Assert.That(controller.InteractButton.interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator CompletedNonRepeatableEncounter_IsHiddenFromMapAndResolver()
        {
            yield return LoadWorld();
            var controller = GetController();
            var state = GameBootstrapper.Context.Get<NarrativeStateService>();
            var crypt = FindArea("area.crypt");
            var boss = crypt.Encounters.Single(value => value.EncounterId == "encounter.crypt.boss");

            controller.MapView.Render(crypt, state);
            var visibleBefore = controller.MapView.EncounterMarkerCount;
            Assert.That(WorldInteractionResolver.FindNearest(
                boss.Position,
                crypt,
                state,
                out _), Is.True);

            Assert.That(state.SetEvent(boss.CompletionEventId), Is.True);
            controller.MapView.Render(crypt, state);
            Assert.That(controller.MapView.EncounterMarkerCount, Is.EqualTo(visibleBefore - 1));
            var foundAfter = WorldInteractionResolver.FindNearest(
                boss.Position,
                crypt,
                state,
                out var afterResult);
            Assert.That(foundAfter && afterResult.TargetId == boss.EncounterId, Is.False);
        }
        [UnityTest]
        public IEnumerator ApplicationPause_AutosavesCurrentWorldState()
        {
            yield return LoadWorld();
            var controller = GetController();
            var context = GameBootstrapper.Context;
            var inventory = context.Get<InventoryService>();
            var save = context.Get<SaveService>();
            save.Delete(0);

            inventory.AddGold(5);
            var savedGold = inventory.Gold;
            controller.HandleApplicationPause(true);
            Assert.That(save.HasSave(0), Is.True);

            inventory.AddGold(5);
            Assert.That(save.Load(0), Is.True);
            Assert.That(inventory.Gold, Is.EqualTo(savedGold));
        }

        private static IEnumerator LoadWorld()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            yield return SceneManager.LoadSceneAsync("World");
            yield return null;
        }

        private static IEnumerator AcceptMainQuest(WorldExplorationController controller)
        {
            var elder = FindNpcInteractable(VillageId, ElderId);
            yield return MoveNear(controller, elder.Position);
            ClickInteract(controller);
            var dialogue = Object.FindAnyObjectByType<DialoguePanelView>();
            FindButton(dialogue, "Choice_0").onClick.Invoke();
            FindButton(dialogue, "Close").onClick.Invoke();
        }

        private static IEnumerator MoveNear(WorldExplorationController controller, Vector2 target)
        {
            for (var index = 0; index < 240; index++)
            {
                var offset = target - controller.PlayerPosition;
                if (offset.magnitude <= 0.55f)
                    break;
                PressJoystick(controller, offset.normalized);
                yield return null;
            }

            ReleaseJoystick(controller);
            yield return null;
            Assert.That(Vector2.Distance(controller.PlayerPosition, target), Is.LessThan(0.7f));
        }

        private static void ClickInteract(WorldExplorationController controller)
        {
            Assert.That(controller.InteractButton.interactable, Is.True);
            controller.InteractButton.onClick.Invoke();
            Assert.That(controller.InteractButton.interactable, Is.False);
        }

        private static void PressJoystick(WorldExplorationController controller, Vector2 direction)
        {
            var rect = controller.Joystick.GetComponent<RectTransform>();
            var localPoint = direction.normalized * 90f;
            var worldPoint = rect.TransformPoint(localPoint);
            var screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPoint);
            var eventData = new PointerEventData(EventSystem.current) { position = screenPoint };
            controller.Joystick.OnPointerDown(eventData);
        }

        private static void ReleaseJoystick(WorldExplorationController controller)
        {
            controller.Joystick.OnPointerUp(new PointerEventData(EventSystem.current));
        }

        private static Button FindButton(Component root, string name)
        {
            var button = root.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(value => value.name == name && value.gameObject.activeInHierarchy);
            Assert.That(button, Is.Not.Null, "Missing button: " + name);
            return button;
        }

        private static WorldExplorationController GetController()
        {
            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            Assert.That(controller, Is.Not.Null);
            return controller;
        }

        private static WorldInteractableDefinition FindNpcInteractable(string areaId, string npcId) =>
            FindArea(areaId).Interactables.Single(value =>
                value.Kind == WorldInteractableKind.Npc && value.TargetId == npcId);

        private static WorldInteractableDefinition FindAreaExit(string sourceAreaId, string targetAreaId) =>
            FindArea(sourceAreaId).Interactables.Single(value =>
                value.Kind == WorldInteractableKind.AreaExit && value.TargetId == targetAreaId);

        private static WorldInteractableDefinition FindEncounterInteractable(string areaId, string encounterId) =>
            FindArea(areaId).Interactables.Single(value =>
                value.Kind == WorldInteractableKind.Encounter && value.TargetId == encounterId);

        private static WorldAreaDefinition FindArea(string areaId)
        {
            var catalog = Resources.Load<ContentCatalog>("ContentCatalog");
            Assert.That(catalog, Is.Not.Null);
            return catalog.All.OfType<WorldAreaDefinition>().Single(value => value.Id == areaId);
        }

        private static void KillAllEnemies(BattleSceneController controller)
        {
            foreach (var enemy in controller.EngineForTests.State.Units
                         .Where(unit => unit.Team == BorderValley.Battle.Domain.Team.Enemy)
                         .ToArray())
            {
                enemy.ApplyRawDamage(int.MaxValue);
            }

            controller.EngineForTests.Execute(null);
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            for (var index = 0; index < 180 && SceneManager.GetActiveScene().name != sceneName; index++)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
        }
    }
}
