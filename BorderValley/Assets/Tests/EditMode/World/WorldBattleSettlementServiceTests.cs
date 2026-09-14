using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Combat;
using BorderValley.Core.Random;
using BorderValley.Data;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;
using BorderValley.Inventory;
using BorderValley.Narrative;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorderValley.World.Tests
{
    public sealed class WorldBattleSettlementServiceTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                Object.DestroyImmediate(value);
            created.Clear();
        }

        [Test]
        public void Settle_PlayerVictory_AppliesRewardsAndQuestProgressOnlyOnce()
        {
            var fixture = CreateFixture();
            Assert.That(fixture.Quests.TryAccept(fixture.Quest.Id, out var acceptError), Is.True, acceptError);
            var service = CreateService(fixture, "seed.reward");
            var result = CreateResult(fixture, BattleFlowOutcome.PlayerVictory);

            Assert.That(service.Settle(result, fixture.Encounter, out var settlement), Is.True);

            Assert.That(settlement.Success, Is.True);
            Assert.That(settlement.RequiresAutosave, Is.True);
            Assert.That(settlement.LootAdded, Is.True);
            Assert.That(settlement.GoldAwarded, Is.EqualTo(fixture.Encounter.GoldReward));
            Assert.That(settlement.ExperienceAwarded, Is.EqualTo(fixture.Encounter.ExperienceReward));
            Assert.That(settlement.DefeatedEnemyIds, Is.EqualTo(new[] { "enemy.bandit" }));
            Assert.That(settlement.ErrorKey, Is.Empty);
            Assert.That(fixture.Inventory.Items.Count, Is.EqualTo(1));
            Assert.That(fixture.Inventory.Gold, Is.EqualTo(130));
            Assert.That(fixture.Progression.TotalExperience, Is.EqualTo(45));
            Assert.That(fixture.Progression.Members[0].CurrentHealth, Is.EqualTo(25));
            Assert.That(fixture.State.GetObjectiveProgress(fixture.Quest.Id, "objective.bandit"), Is.EqualTo(1));
            Assert.That(fixture.State.HasEvent(fixture.Encounter.CompletionEventId), Is.True);

            Assert.That(service.Settle(result, fixture.Encounter, out var repeated), Is.True);

            Assert.That(repeated.Success, Is.True);
            Assert.That(repeated.RequiresAutosave, Is.False);
            Assert.That(repeated.LootAdded, Is.False);
            Assert.That(repeated.GoldAwarded, Is.Zero);
            Assert.That(repeated.ExperienceAwarded, Is.Zero);
            Assert.That(repeated.DefeatedEnemyIds, Is.Empty);
            Assert.That(fixture.Inventory.Items.Count, Is.EqualTo(1));
            Assert.That(fixture.Inventory.Gold, Is.EqualTo(130));
            Assert.That(fixture.Progression.TotalExperience, Is.EqualTo(45));
            Assert.That(fixture.State.GetObjectiveProgress(fixture.Quest.Id, "objective.bandit"), Is.EqualTo(1));
        }

        [Test]
        public void Settle_PlayerVictory_WhenBagFull_DoesNotApplyAnyRewardOrCompleteEncounter()
        {
            var fixture = CreateFixture(capacity: 1);
            Assert.That(fixture.Quests.TryAccept(fixture.Quest.Id, out var acceptError), Is.True, acceptError);
            Assert.That(
                fixture.Inventory.TryAdd(
                    new ItemInstance("inventory.filler", fixture.Item.Id, 1, ItemRarity.Common, Array.Empty<AffixInstance>()),
                    out var addError),
                Is.True,
                addError);
            var service = CreateService(fixture, "seed.full");
            var result = CreateResult(fixture, BattleFlowOutcome.PlayerVictory);

            Assert.That(service.Settle(result, fixture.Encounter, out var settlement), Is.False);

            Assert.That(settlement.ErrorKey, Is.EqualTo(BorderValley.Inventory.InventoryTextKeys.BagFull));
            Assert.That(settlement.RequiresAutosave, Is.False);
            Assert.That(settlement.LootAdded, Is.False);
            Assert.That(fixture.Inventory.Items.Count, Is.EqualTo(1));
            Assert.That(fixture.Inventory.Gold, Is.EqualTo(100));
            Assert.That(fixture.Progression.TotalExperience, Is.Zero);
            Assert.That(fixture.Progression.Members[0].CurrentHealth, Is.EqualTo(100));
            Assert.That(fixture.State.GetObjectiveProgress(fixture.Quest.Id, "objective.bandit"), Is.Zero);
            Assert.That(fixture.State.HasEvent(fixture.Encounter.CompletionEventId), Is.False);
        }

        [Test]
        public void Settle_PlayerVictory_WhenLaterQuestProgressFails_RollsBackEveryAppliedChange()
        {
            var fixture = CreateFixture(brokenMultiEnemyQuest: true);
            Assert.That(fixture.Quests.TryAccept(fixture.Quest.Id, out var acceptError), Is.True, acceptError);
            var service = CreateService(fixture, "seed.rollback");
            var result = CreateResult(
                fixture,
                BattleFlowOutcome.PlayerVictory,
                new BattleUnitResult("player.warrior", "enemy.bandit", 25, 5),
                new BattleUnitResult("player.warrior", "enemy.wolf", 25, 5));

            Assert.That(service.Settle(result, fixture.Encounter, out var settlement), Is.False);

            Assert.That(settlement.ErrorKey, Is.EqualTo(NarrativeTextKeys.QuestObjectiveInvalid));
            Assert.That(settlement.RequiresAutosave, Is.False);
            Assert.That(settlement.LootAdded, Is.False);
            Assert.That(settlement.GoldAwarded, Is.Zero);
            Assert.That(settlement.ExperienceAwarded, Is.Zero);
            Assert.That(settlement.DefeatedEnemyIds, Is.Empty);
            Assert.That(fixture.Inventory.Items, Is.Empty);
            Assert.That(fixture.Inventory.Gold, Is.EqualTo(100));
            Assert.That(fixture.Progression.TotalExperience, Is.Zero);
            Assert.That(fixture.Progression.Members[0].CurrentHealth, Is.EqualTo(100));
            Assert.That(fixture.State.GetObjectiveProgress(fixture.Quest.Id, "objective.bandit"), Is.Zero);
            Assert.That(fixture.State.GetObjectiveProgress(fixture.Quest.Id, "objective.wolf.first"), Is.Zero);
            Assert.That(fixture.State.GetObjectiveProgress(fixture.Quest.Id, "objective.wolf.second"), Is.Zero);
            Assert.That(fixture.State.HasEvent(fixture.Encounter.CompletionEventId), Is.False);
        }

        [Test]
        public void Settle_PlayerVictory_WhenCompletionEventCannotApply_RollsBackAllAppliedChanges()
        {
            var fixture = CreateFixture();
            Assert.That(fixture.Quests.TryAccept(fixture.Quest.Id, out var acceptError), Is.True, acceptError);
            fixture.Encounter.EditorConfigure(
                fixture.Encounter.EncounterId,
                fixture.Encounter.ScenarioId,
                fixture.Encounter.EnemyDefinitionIds,
                fixture.Encounter.RewardTableId,
                fixture.Encounter.GoldReward,
                fixture.Encounter.ExperienceReward,
                fixture.Encounter.Position,
                fixture.Encounter.TriggerRadius,
                fixture.Encounter.Repeatable,
                fixture.Encounter.RequiredEventId,
                "event.test.unknown");
            var service = CreateService(fixture, "seed.completion-failure");
            var result = CreateResult(fixture, BattleFlowOutcome.PlayerVictory);

            Assert.That(service.Settle(result, fixture.Encounter, out var settlement), Is.False);

            Assert.That(settlement.ErrorKey, Is.EqualTo(NarrativeTextKeys.UnknownEvent));
            Assert.That(fixture.Inventory.Items, Is.Empty);
            Assert.That(fixture.Inventory.Gold, Is.EqualTo(100));
            Assert.That(fixture.Progression.TotalExperience, Is.Zero);
            Assert.That(fixture.Progression.Members[0].CurrentHealth, Is.EqualTo(100));
            Assert.That(fixture.State.GetObjectiveProgress(fixture.Quest.Id, "objective.bandit"), Is.Zero);
            Assert.That(fixture.State.HasEvent("event.test.unknown"), Is.False);
        }
        [Test]
        public void Settle_EnemyVictory_AppliesUnitStatesReturnsToSafePointAndSkipsRewards()
        {
            var fixture = CreateFixture();
            var snapshot = fixture.Progression.Capture();
            snapshot["safePointId"] = "world.deep";
            fixture.Progression.Restore(snapshot);
            var service = CreateService(fixture, "seed.defeat");
            var result = CreateResult(fixture, BattleFlowOutcome.EnemyVictory, new BattleUnitResult("player.warrior", "enemy.bandit", 0, 0));

            Assert.That(service.Settle(result, fixture.Encounter, out var settlement), Is.True);

            Assert.That(settlement.Success, Is.True);
            Assert.That(settlement.RequiresAutosave, Is.True);
            Assert.That(settlement.LootAdded, Is.False);
            Assert.That(settlement.GoldAwarded, Is.Zero);
            Assert.That(settlement.ExperienceAwarded, Is.Zero);
            Assert.That(settlement.DefeatedEnemyIds, Is.Empty);
            Assert.That(fixture.Inventory.Items, Is.Empty);
            Assert.That(fixture.Inventory.Gold, Is.EqualTo(100));
            Assert.That(fixture.Progression.TotalExperience, Is.Zero);
            Assert.That(fixture.Progression.Members[0].CurrentHealth, Is.EqualTo(1));
            Assert.That(fixture.Progression.HasPendingWipeReturn, Is.False);
            Assert.That(fixture.Progression.SafePointId, Is.EqualTo(PartyProgressionService.DefaultSafePointId));
            Assert.That(fixture.State.GetObjectiveProgress(fixture.Quest.Id, "objective.bandit"), Is.Zero);
            Assert.That(fixture.State.HasEvent(fixture.Encounter.CompletionEventId), Is.False);
        }

        [Test]
        public void Settle_PlayerVictory_RepeatableEncounter_DoesNotMarkCompletionEvent()
        {
            var fixture = CreateFixture(repeatable: true);
            Assert.That(fixture.Quests.TryAccept(fixture.Quest.Id, out var acceptError), Is.True, acceptError);
            var service = CreateService(fixture, "seed.repeatable");
            var result = CreateResult(fixture, BattleFlowOutcome.PlayerVictory);

            Assert.That(service.Settle(result, fixture.Encounter, out var settlement), Is.True);

            Assert.That(settlement.Success, Is.True);
            Assert.That(settlement.RequiresAutosave, Is.True);
            Assert.That(fixture.State.HasEvent(fixture.Encounter.CompletionEventId), Is.False);
            Assert.That(fixture.State.GetObjectiveProgress(fixture.Quest.Id, "objective.bandit"), Is.EqualTo(1));
        }

        [Test]
        public void Settle_PlayerVictory_SameSeedAndInput_ProducesSameLootAndDifferentSeedCanDiffer()
        {
            var first = SettleWithSeed("seed.alpha");
            var same = SettleWithSeed("seed.alpha");
            var different = SettleWithSeed("seed.beta");

            Assert.That(LootSignature(same), Is.EqualTo(LootSignature(first)));
            Assert.That(LootSignature(different), Is.Not.EqualTo(LootSignature(first)));
        }

        private ItemInstance SettleWithSeed(string seed)
        {
            var fixture = CreateFixture();
            var service = CreateService(fixture, seed);
            var result = CreateResult(fixture, BattleFlowOutcome.PlayerVictory);
            Assert.That(service.Settle(result, fixture.Encounter, out var settlement), Is.True, settlement.ErrorKey);
            return fixture.Inventory.Items.Single();
        }

        private static string LootSignature(ItemInstance item) =>
            string.Join(
                "|",
                item.InstanceId,
                item.ItemDefinitionId,
                item.ItemLevel,
                item.Rarity,
                string.Join(
                    ";",
                    item.Affixes
                        .OrderBy(value => value.AffixId, StringComparer.Ordinal)
                        .Select(value => value.AffixId + ":" + value.Value)));

        private Fixture CreateFixture(
            int capacity = 10,
            bool repeatable = false,
            bool brokenMultiEnemyQuest = false)
        {
            var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
            item.EditorConfigure(
                "item.test",
                "item.test.name",
                ItemSlot.Weapon,
                Array.Empty<string>(),
                false,
                10,
                Array.Empty<StatValue>());
            var table = Track(ScriptableObject.CreateInstance<ItemDropTableDefinition>());
            table.EditorConfigure(
                "loot.test",
                1,
                10,
                new[] { new LootEntry(item, 1) },
                new[] { new RarityWeight(ItemRarity.Common, 1) });
            var encounter = Track(ScriptableObject.CreateInstance<WorldEncounterDefinition>());
            encounter.EditorConfigure(
                "encounter.test",
                "scenario.test",
                brokenMultiEnemyQuest
                    ? new[] { "enemy.bandit", "enemy.wolf" }
                    : new[] { "enemy.bandit" },
                table.Id,
                30,
                45,
                Vector2.zero,
                1f,
                repeatable,
                string.Empty,
                "event.test.completed");
            var area = Track(ScriptableObject.CreateInstance<WorldAreaDefinition>());
            area.EditorConfigure(
                "area.test",
                new Rect(0f, 0f, 10f, 10f),
                Array.Empty<Rect>(),
                Array.Empty<NpcDefinition>(),
                new[] { encounter },
                Array.Empty<WorldInteractableDefinition>(),
                new[] { table },
                new[] { encounter.CompletionEventId },
                new[] { table.Id });
            var quest = Track(ScriptableObject.CreateInstance<QuestDefinition>());
            quest.EditorConfigure(
                "quest.test",
                "quest.test.title",
                "quest.test.description",
                Array.Empty<string>(),
                brokenMultiEnemyQuest
                    ? new[]
                    {
                        new QuestObjectiveDefinition(
                            "objective.bandit",
                            QuestObjectiveKind.DefeatEnemy,
                            "enemy.bandit",
                            1,
                            false,
                            "quest.test.objective.bandit"),
                        new QuestObjectiveDefinition(
                            "objective.wolf.first",
                            QuestObjectiveKind.DefeatEnemy,
                            "enemy.wolf",
                            1,
                            false,
                            "quest.test.objective.wolf"),
                        new QuestObjectiveDefinition(
                            "objective.wolf.second",
                            QuestObjectiveKind.DefeatEnemy,
                            "enemy.wolf",
                            1,
                            false,
                            "quest.test.objective.wolf")
                    }
                    : new[]
                    {
                        new QuestObjectiveDefinition(
                            "objective.bandit",
                            QuestObjectiveKind.DefeatEnemy,
                            "enemy.bandit",
                            1,
                            false,
                            "quest.test.objective.bandit")
                    },
                Array.Empty<QuestRewardDefinition>());

            var character = Track(ScriptableObject.CreateInstance<CharacterDefinition>());
            character.EditorConfigure(
                "class.warrior",
                "class.warrior.name",
                new[]
                {
                    new StatValue(CombatStat.MaxHealth, 100),
                    new StatValue(CombatStat.MaxMana, 20)
                },
                Array.Empty<StatValue>(),
                Array.Empty<string>(),
                Array.Empty<SkillUnlock>());
            var characters = new Dictionary<string, CharacterDefinition>(StringComparer.Ordinal)
            {
                [character.Id] = character
            };
            var progression = new PartyProgressionService(
                characters,
                new[]
                {
                    new PartyMemberState("player.warrior", character.Id, 1, 0, 0, 100, 20)
                });
            var items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal)
            {
                [item.Id] = item
            };
            var inventory = new InventoryService(capacity, items, 100);
            var state = new NarrativeStateService(new ContentDefinition[] { quest, area });
            var quests = new QuestService(
                new Dictionary<string, QuestDefinition>(StringComparer.Ordinal) { [quest.Id] = quest },
                state,
                inventory,
                new NoOpQuestRewardService());
            var lootGenerator = new LootGenerator(items, new Dictionary<string, AffixDefinition>());

            return new Fixture(
                item,
                table,
                encounter,
                quest,
                inventory,
                progression,
                state,
                quests,
                lootGenerator);
        }

        private static WorldBattleSettlementService CreateService(Fixture fixture, string seed) =>
            new(
                fixture.Inventory,
                fixture.Progression,
                fixture.LootGenerator,
                fixture.Quests,
                fixture.State,
                tableId => string.Equals(tableId, fixture.Table.Id, StringComparison.Ordinal)
                    ? fixture.Table
                    : null,
                RandomSourceFactory.FromSeed(seed));

        private static BattleResult CreateResult(
            Fixture fixture,
            BattleFlowOutcome outcome,
            params BattleUnitResult[] unitStates)
        {
            var states = unitStates.Length == 0
                ? new[] { new BattleUnitResult("player.warrior", "enemy.bandit", 25, 5) }
                : unitStates;
            return new BattleResult(
                outcome,
                3,
                states,
                new BattleContext(
                    fixture.Encounter.EncounterId,
                    fixture.Encounter.RewardTableId,
                    fixture.Encounter.GoldReward,
                    fixture.Encounter.ExperienceReward,
                    fixture.Encounter.EnemyDefinitionIds,
                    fixture.Encounter.Repeatable));
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }

        private sealed class Fixture
        {
            public Fixture(
                ItemDefinition item,
                ItemDropTableDefinition table,
                WorldEncounterDefinition encounter,
                QuestDefinition quest,
                InventoryService inventory,
                PartyProgressionService progression,
                NarrativeStateService state,
                QuestService quests,
                LootGenerator lootGenerator)
            {
                Item = item;
                Table = table;
                Encounter = encounter;
                Quest = quest;
                Inventory = inventory;
                Progression = progression;
                State = state;
                Quests = quests;
                LootGenerator = lootGenerator;
            }

            public ItemDefinition Item { get; }
            public ItemDropTableDefinition Table { get; }
            public WorldEncounterDefinition Encounter { get; }
            public QuestDefinition Quest { get; }
            public InventoryService Inventory { get; }
            public PartyProgressionService Progression { get; }
            public NarrativeStateService State { get; }
            public QuestService Quests { get; }
            public LootGenerator LootGenerator { get; }
        }

        private sealed class NoOpQuestRewardService : IQuestRewardService
        {
            public bool TryValidate(QuestDefinition quest, out string error)
            {
                error = string.Empty;
                return true;
            }

            public bool TryApply(QuestDefinition quest, out string error)
            {
                error = string.Empty;
                return true;
            }
        }
    }
}
