using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BorderValley.Battle.Domain;
using BorderValley.Core;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Persistence;
using BorderValley.Data;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;
using BorderValley.Inventory;
using BorderValley.Narrative;
using BorderValley.UI.Battle;
using BorderValley.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using InventoryTextKeys = BorderValley.UI.Inventory.InventoryTextKeys;
using Object = UnityEngine.Object;

namespace BorderValley.PlayModeTests
{
    public sealed class WorldBattleSettlementFlowTests
    {
        private readonly List<Object> created = new();
        private ContentCatalog catalog;
        private IReadOnlyList<ContentDefinition> originalDefinitions;

        [TearDown]
        public void TearDown()
        {
            if (catalog != null && originalDefinitions != null)
                catalog.EditorSetDefinitions(originalDefinitions);

            foreach (var value in created)
                Object.DestroyImmediate(value);
            created.Clear();
        }

        [UnityTest]
        public IEnumerator ContextfulBattleVictory_ReturnsToWorld_AndAdvancesEnemyQuest()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;

            var fixture = PrepareContextfulBattle("seed.context.success");
            var context = GameBootstrapper.Context;
            var flow = context.Get<IBattleFlow>();
            var inventory = context.Get<InventoryService>();
            var beforeGold = inventory.Gold;
            var beforeItems = inventory.Items.Count;

            flow.BeginBattle(fixture.Request);
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;

            var controller = Object.FindAnyObjectByType<BattleSceneController>();
            Assert.That(controller, Is.Not.Null);
            KillAllEnemies(controller);
            Object.FindAnyObjectByType<BattleHudView>().ContinueButton.onClick.Invoke();

            yield return WaitForScene("World");
            yield return null;

            var entry = Object.FindAnyObjectByType<WorldBattleEntryView>();
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.ResultLabel.text, Is.EqualTo("battle.result.player_victory"));
            Assert.That(entry.HasPendingRewardForTests, Is.False);
            Assert.That(entry.SettlementCountForTests, Is.EqualTo(1));
            Assert.That(inventory.Gold, Is.GreaterThan(beforeGold));
            Assert.That(inventory.Items.Count, Is.GreaterThan(beforeItems));
            Assert.That(
                fixture.State.GetObjectiveProgress(fixture.Quest.Id, fixture.ObjectiveId),
                Is.EqualTo(1));

            context.Get<SaveService>().Delete(0);
        }

        [UnityTest]
        public IEnumerator ContextfulBattleVictory_AutosaveFailure_RetriesSaveWithoutRepeatedSettlement()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;

            var fixture = PrepareContextfulBattle("seed.context.autosave");
            var context = GameBootstrapper.Context;
            var flow = context.Get<IBattleFlow>();
            var inventory = context.Get<InventoryService>();
            var progression = context.Get<PartyProgressionService>();
            var save = context.Get<SaveService>();
            save.Delete(0);
            save.Save(0, "World");
            var beforeGold = inventory.Gold;

            flow.BeginBattle(fixture.Request);
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;

            var controller = Object.FindAnyObjectByType<BattleSceneController>();
            Assert.That(controller, Is.Not.Null);
            KillAllEnemies(controller);
            var hud = Object.FindAnyObjectByType<BattleHudView>();

            WorldBattleEntryView pendingEntry;
            int settledGold;
            int settledItems;
            int settledExperience;
            using (new FileStream(save.GetPrimaryPathForTests(0), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                hud.ContinueButton.onClick.Invoke();
                yield return WaitForScene("World");
                yield return null;

                pendingEntry = Object.FindAnyObjectByType<WorldBattleEntryView>();
                Assert.That(pendingEntry, Is.Not.Null);
                Assert.That(pendingEntry.HasPendingRewardForTests, Is.True);
                Assert.That(pendingEntry.ResultLabel.text, Is.EqualTo(InventoryTextKeys.AutoSaveFailed));
                Assert.That(pendingEntry.SettlementCountForTests, Is.EqualTo(1));
                Assert.That(pendingEntry.SaveAttemptCountForTests, Is.EqualTo(1));
                Assert.That(inventory.Gold, Is.GreaterThan(beforeGold));
                Assert.That(
                    fixture.State.GetObjectiveProgress(fixture.Quest.Id, fixture.ObjectiveId),
                    Is.EqualTo(1));

                settledGold = inventory.Gold;
                settledItems = inventory.Items.Count;
                settledExperience = progression.TotalExperience;
            }

            Assert.That(pendingEntry.ConsumePendingResultForTests(), Is.True);
            Assert.That(pendingEntry.HasPendingRewardForTests, Is.False);
            Assert.That(pendingEntry.SettlementCountForTests, Is.EqualTo(1));
            Assert.That(pendingEntry.SaveAttemptCountForTests, Is.EqualTo(2));
            Assert.That(inventory.Gold, Is.EqualTo(settledGold));
            Assert.That(inventory.Items.Count, Is.EqualTo(settledItems));
            Assert.That(progression.TotalExperience, Is.EqualTo(settledExperience));
            Assert.That(
                fixture.State.GetObjectiveProgress(fixture.Quest.Id, fixture.ObjectiveId),
                Is.EqualTo(1));

            save.Delete(0);
        }

        private SettlementFixture PrepareContextfulBattle(string seed)
        {
            var context = GameBootstrapper.Context;
            Assert.That(context, Is.Not.Null);

            catalog = Resources.Load<ContentCatalog>("ContentCatalog");
            Assert.That(catalog, Is.Not.Null);
            originalDefinitions = catalog.All.ToArray();

            var rewardTable = catalog.All
                .OfType<ItemDropTableDefinition>()
                .Single(table => table.Id == "loot.bandit.core");
            var encounter = Track(ScriptableObject.CreateInstance<WorldEncounterDefinition>());
            encounter.EditorConfigure(
                "encounter.integration.bandits",
                "scenario.integration.bandits",
                new[] { "enemy.bandit" },
                rewardTable.Id,
                30,
                45,
                Vector2.zero,
                1f,
                false,
                string.Empty,
                "event.integration.bandits.defeated");
            var quest = Track(ScriptableObject.CreateInstance<QuestDefinition>());
            quest.EditorConfigure(
                "quest.integration.bandits",
                "quest.integration.bandits.title",
                "quest.integration.bandits.description",
                System.Array.Empty<string>(),
                new[]
                {
                    new QuestObjectiveDefinition(
                        "objective.integration.bandit",
                        QuestObjectiveKind.DefeatEnemy,
                        "enemy.bandit",
                        1,
                        false,
                        "quest.integration.bandits.objective")
                },
                System.Array.Empty<QuestRewardDefinition>());
            var area = Track(ScriptableObject.CreateInstance<WorldAreaDefinition>());
            area.EditorConfigure(
                "area.integration",
                new Rect(0f, 0f, 10f, 10f),
                System.Array.Empty<Rect>(),
                System.Array.Empty<NpcDefinition>(),
                new[] { encounter },
                System.Array.Empty<WorldInteractableDefinition>(),
                new[] { rewardTable },
                new[] { encounter.CompletionEventId },
                new[] { rewardTable.Id });
            catalog.EditorSetDefinitions(catalog.All.Concat(new ContentDefinition[] { encounter, quest, area }).ToArray());

            var inventory = context.Get<InventoryService>();
            var progression = context.Get<PartyProgressionService>();
            var items = context.Get<IReadOnlyDictionary<string, ItemDefinition>>();
            var state = new NarrativeStateService(catalog.All);
            var rewards = new QuestRewardService(inventory, progression, items, state);
            var quests = new QuestService(
                new Dictionary<string, QuestDefinition>
                {
                    [quest.Id] = quest
                },
                state,
                inventory,
                rewards);
            context.Register(state);
            context.Register(quests);
            Assert.That(quests.TryAccept(quest.Id, out var error), Is.True, error);

            var request = WorldEncounterService.BuildRequest(
                encounter,
                context.Get<PartyBattleSnapshotBuilder>().BuildPartySnapshot(),
                seed,
                "World");
            return new SettlementFixture(encounter, quest, "objective.integration.bandit", state, request);
        }

        private static void KillAllEnemies(BattleSceneController controller)
        {
            foreach (var enemy in controller.EngineForTests.State.Units
                         .Where(unit => unit.Team == Team.Enemy)
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
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }

        private sealed class SettlementFixture
        {
            public SettlementFixture(
                WorldEncounterDefinition encounter,
                QuestDefinition quest,
                string objectiveId,
                NarrativeStateService state,
                BattleRequest request)
            {
                Encounter = encounter;
                Quest = quest;
                ObjectiveId = objectiveId;
                State = state;
                Request = request;
            }

            public WorldEncounterDefinition Encounter { get; }
            public QuestDefinition Quest { get; }
            public string ObjectiveId { get; }
            public NarrativeStateService State { get; }
            public BattleRequest Request { get; }
        }
    }
}
