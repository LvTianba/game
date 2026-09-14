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
using NUnit.Framework;
using UnityEngine;
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

            controller.Joystick.SetValue(new Vector2(100f, 0f));
            Assert.That(controller.Joystick.Value, Is.EqualTo(Vector2.right));

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
            Assert.That(controller.Interact(), Is.True);
            Assert.That(controller.DialoguePresenter.IsOpen, Is.True);
            Assert.That(controller.LastInteractionId, Is.EqualTo(elder.Id));
        }

        [UnityTest]
        public IEnumerator AcceptingMainQuest_AddsObjectiveToQuestLog()
        {
            yield return LoadWorld();
            var controller = GetController();
            var quests = GameBootstrapper.Context.Get<QuestService>();
            var elder = FindNpcInteractable(VillageId, ElderId);

            yield return MoveNear(controller, elder.Position);
            Assert.That(controller.Interact(), Is.True);
            Assert.That(controller.DialoguePresenter.SelectChoice(0), Is.True);

            var entry = quests.GetJournal().Single(value => value.QuestId == MainQuestId);
            Assert.That(entry.State, Is.EqualTo(QuestState.Active));
            Assert.That(entry.Objectives, Is.Not.Empty);
            Assert.That(entry.Objectives.Any(value => value.TargetId == "enemy.bandit"), Is.True);
        }

        [UnityTest]
        public IEnumerator TalkingToMerchant_OpensGeneralShop_AndPurchaseSpendsGoldAndAddsItem()
        {
            yield return LoadWorld();
            var controller = GetController();
            var context = GameBootstrapper.Context;
            var shops = context.Get<ShopService>();
            var inventory = context.Get<InventoryService>();
            var merchant = FindNpcInteractable(VillageId, MerchantId);
            var goldBefore = inventory.Gold;
            var itemsBefore = inventory.Items.Count;

            yield return MoveNear(controller, merchant.Position);
            Assert.That(controller.Interact(), Is.True);
            Assert.That(controller.ShopPresenter.IsOpen, Is.True);
            Assert.That(controller.ShopPresenter.CurrentShopId, Is.EqualTo("shop.general"));

            var offer = shops.GetOffers("shop.general").First();
            Assert.That(controller.BuyFromOpenShop(offer.OfferId), Is.True);
            Assert.That(inventory.Gold, Is.LessThan(goldBefore));
            Assert.That(inventory.Items.Count, Is.EqualTo(itemsBefore + 1));
            Assert.That(inventory.Items.Any(item => item.ItemDefinitionId == offer.Item.ItemDefinitionId), Is.True);
            Assert.That(context.Get<SaveService>().HasSave(0), Is.True);
        }

        [UnityTest]
        public IEnumerator AreaExit_SwitchesAreaAtArrivalPosition_AndSurvivesSaveReload()
        {
            yield return LoadWorld();
            var controller = GetController();
            var context = GameBootstrapper.Context;
            var state = context.Get<NarrativeStateService>();
            var save = context.Get<SaveService>();
            var exit = FindAreaExit(VillageId, ForestId);

            yield return MoveNear(controller, exit.Position);
            Assert.That(controller.Interact(), Is.True);

            Assert.That(controller.CurrentAreaId, Is.EqualTo(ForestId));
            Assert.That(Vector2.Distance(controller.PlayerPosition, exit.ArrivalPosition), Is.LessThan(0.05f));
            Assert.That(save.HasSave(0), Is.True);

            Assert.That(state.SetCurrentLocation(VillageId, new Vector2(1f, 1f)), Is.True);
            Assert.That(save.Load(0), Is.True);
            Assert.That(state.GetCurrentAreaId(), Is.EqualTo(ForestId));
            Assert.That(Vector2.Distance(state.GetCurrentPosition(), exit.ArrivalPosition), Is.LessThan(0.05f));
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
            Assert.That(controller.Interact(), Is.True);
            Assert.That(controller.CurrentAreaId, Is.EqualTo(ForestId));

            var encounter = FindEncounterInteractable(ForestId, "encounter.forest.bandits");
            var goldBefore = inventory.Gold;
            var itemsBefore = inventory.Items.Count;
            yield return MoveNear(controller, encounter.Position);
            Assert.That(controller.Interact(), Is.True);
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
            Assert.That(controller.Interact(), Is.True);
            Assert.That(controller.DialoguePresenter.SelectChoice(0), Is.True);
            controller.DialoguePresenter.Close();
        }

        private static IEnumerator MoveNear(WorldExplorationController controller, Vector2 target)
        {
            for (var index = 0; index < 160; index++)
            {
                var offset = target - controller.PlayerPosition;
                if (offset.magnitude <= 0.55f)
                    break;
                controller.Joystick.SetValue(offset.normalized);
                controller.StepForTests(0.05f);
                yield return null;
            }

            controller.Joystick.SetValue(Vector2.zero);
            controller.StepForTests(0f);
            Assert.That(Vector2.Distance(controller.PlayerPosition, target), Is.LessThan(0.7f));
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
