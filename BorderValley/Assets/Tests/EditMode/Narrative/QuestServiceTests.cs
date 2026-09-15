using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Combat;
using BorderValley.Data;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;
using BorderValley.Inventory;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorderValley.Narrative.Tests
{
    public sealed class QuestServiceTests
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
        public void TryAccept_WhenPrerequisiteIsNotCompleted_ReturnsErrorAndDoesNotStart()
        {
            var content = CreateContent();
            var prerequisite = CreateQuest(
                "quest.prereq",
                Array.Empty<string>(),
                new[] { Objective("talk.ranger", QuestObjectiveKind.TalkToNpc, content.Ranger.Id, 1) },
                new[] { new QuestRewardDefinition(QuestRewardKind.Gold, string.Empty, 5) });
            var main = CreateQuest(
                "quest.main",
                new[] { prerequisite.Id },
                new[] { Objective("reach.forest", QuestObjectiveKind.ReachLocation, content.Area.Id, 1) },
                new[] { new QuestRewardDefinition(QuestRewardKind.Gold, string.Empty, 10) });
            var runtime = CreateRuntime(content, new[] { prerequisite, main });

            Assert.That(runtime.Quests.TryAccept(main.Id, out var blockedError), Is.False);
            Assert.That(blockedError, Is.Not.Empty);
            Assert.That(runtime.State.GetQuestState(main.Id), Is.EqualTo(QuestState.NotStarted));

            Assert.That(runtime.State.TryAcceptQuest(prerequisite.Id, out _), Is.True);
            Assert.That(runtime.Quests.RecordTalk(content.Ranger.Id, out var talkError), Is.True, talkError);
            Assert.That(runtime.State.GetQuestState(prerequisite.Id), Is.EqualTo(QuestState.ReadyToTurnIn));
            Assert.That(runtime.State.TryMarkQuestCompleted(prerequisite.Id, out _), Is.True);

            Assert.That(runtime.Quests.TryAccept(main.Id, out var acceptError), Is.True, acceptError);
            Assert.That(runtime.State.GetQuestState(main.Id), Is.EqualTo(QuestState.Active));
        }

        [Test]
        public void RecordBattleDefeat_AdvancesMatchingObjectiveAndBecomesReady()
        {
            var content = CreateContent();
            var quest = CreateQuest(
                "quest.hunt",
                Array.Empty<string>(),
                new[] { Objective("defeat.wolf", QuestObjectiveKind.DefeatEnemy, "enemy.wolf", 2) },
                new[] { new QuestRewardDefinition(QuestRewardKind.Gold, string.Empty, 10) });
            var runtime = CreateRuntime(content, new[] { quest });
            Assert.That(runtime.Quests.TryAccept(quest.Id, out var acceptError), Is.True, acceptError);

            Assert.That(runtime.Quests.RecordBattleDefeat("enemy.wolf", out var firstError), Is.True, firstError);
            Assert.That(runtime.State.GetObjectiveProgress(quest.Id, "defeat.wolf"), Is.EqualTo(1));
            Assert.That(runtime.State.GetQuestState(quest.Id), Is.EqualTo(QuestState.Active));

            Assert.That(runtime.Quests.RecordBattleDefeat("enemy.wolf", out var secondError), Is.True, secondError);
            Assert.That(runtime.State.GetObjectiveProgress(quest.Id, "defeat.wolf"), Is.EqualTo(2));
            Assert.That(runtime.State.GetQuestState(quest.Id), Is.EqualTo(QuestState.ReadyToTurnIn));
        }

        [Test]
        public void RecordBattleDefeat_WhenMultipleObjectivesMatch_DoesNotAdvanceAny()
        {
            var content = CreateContent();
            var quest = CreateQuest(
                "quest.ambiguous",
                Array.Empty<string>(),
                new[]
                {
                    Objective("defeat.wolf.first", QuestObjectiveKind.DefeatEnemy, "enemy.wolf", 1),
                    Objective("defeat.wolf.second", QuestObjectiveKind.DefeatEnemy, "enemy.wolf", 1)
                },
                new[] { new QuestRewardDefinition(QuestRewardKind.Gold, string.Empty, 10) });
            var runtime = CreateRuntime(content, new[] { quest });
            Assert.That(runtime.Quests.TryAccept(quest.Id, out var acceptError), Is.True, acceptError);

            Assert.That(runtime.Quests.RecordBattleDefeat("enemy.wolf", out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(runtime.State.GetObjectiveProgress(quest.Id, "defeat.wolf.first"), Is.EqualTo(0));
            Assert.That(runtime.State.GetObjectiveProgress(quest.Id, "defeat.wolf.second"), Is.EqualTo(0));
        }
        [Test]
        public void RecordTalkAndLocation_AdvanceOnlyMatchingObjectives()
        {
            var content = CreateContent();
            var quest = CreateQuest(
                "quest.explore",
                Array.Empty<string>(),
                new[]
                {
                    Objective("talk.ranger", QuestObjectiveKind.TalkToNpc, content.Ranger.Id, 1),
                    Objective("reach.forest", QuestObjectiveKind.ReachLocation, content.Area.Id, 1)
                },
                new[] { new QuestRewardDefinition(QuestRewardKind.Gold, string.Empty, 10) });
            var runtime = CreateRuntime(content, new[] { quest });
            Assert.That(runtime.Quests.TryAccept(quest.Id, out var acceptError), Is.True, acceptError);

            Assert.That(runtime.Quests.RecordTalk(content.Ranger.Id, out var talkError), Is.True, talkError);
            Assert.That(runtime.State.GetObjectiveProgress(quest.Id, "talk.ranger"), Is.EqualTo(1));
            Assert.That(runtime.State.GetObjectiveProgress(quest.Id, "reach.forest"), Is.EqualTo(0));
            Assert.That(runtime.Quests.RecordLocation(content.Area.Id, out var locationError), Is.True, locationError);
            Assert.That(runtime.State.GetQuestState(quest.Id), Is.EqualTo(QuestState.ReadyToTurnIn));
        }

        [Test]
        public void GetJournalAndTryTurnIn_ConsumeItemsApplyRewardsAndDoNotRepeat()
        {
            var content = CreateContent();
            var quest = CreateQuest(
                "quest.deliver",
                Array.Empty<string>(),
                new[] { Objective("submit.pelt", QuestObjectiveKind.SubmitItem, content.Pelt.Id, 2, true) },
                new[]
                {
                    new QuestRewardDefinition(QuestRewardKind.Gold, string.Empty, 25),
                    new QuestRewardDefinition(QuestRewardKind.Material, content.Ore.Id, 3),
                    new QuestRewardDefinition(QuestRewardKind.Equipment, content.Sword.Id, 1),
                    new QuestRewardDefinition(QuestRewardKind.UnlockShop, content.Blacksmith.Id, 1)
                });
            var runtime = CreateRuntime(content, new[] { quest });
            Assert.That(runtime.Inventory.TryAdd(Item("pelt.1", content.Pelt.Id), out var firstAdd), Is.True, firstAdd);
            Assert.That(runtime.Inventory.TryAdd(Item("pelt.2", content.Pelt.Id), out var secondAdd), Is.True, secondAdd);
            Assert.That(runtime.Quests.TryAccept(quest.Id, out var acceptError), Is.True, acceptError);

            var journal = runtime.Quests.GetJournal();
            var entry = journal.Single(value => value.QuestId == quest.Id);
            Assert.That(entry.State, Is.EqualTo(QuestState.ReadyToTurnIn));
            Assert.That(entry.Objectives.Single().CurrentCount, Is.EqualTo(2));
            Assert.That(entry.Rewards.Select(value => value.Kind),
                Is.EquivalentTo(new[]
                {
                    QuestRewardKind.Gold,
                    QuestRewardKind.Material,
                    QuestRewardKind.Equipment,
                    QuestRewardKind.UnlockShop
                }));

            Assert.That(runtime.Quests.TryTurnIn(quest.Id, out var turnInError), Is.True, turnInError);

            Assert.That(runtime.Inventory.Items.Any(item => item.ItemDefinitionId == content.Pelt.Id), Is.False);
            Assert.That(runtime.Inventory.Gold, Is.EqualTo(35));
            Assert.That(runtime.Inventory.Materials[content.Ore.Id], Is.EqualTo(3));
            var equipment = runtime.Inventory.Items.Single(item => item.ItemDefinitionId == content.Sword.Id);
            Assert.That(equipment.Rarity, Is.EqualTo(ItemRarity.Common));
            Assert.That(runtime.State.IsShopUnlocked(content.Blacksmith.Id), Is.True);
            Assert.That(runtime.State.GetQuestState(quest.Id), Is.EqualTo(QuestState.Completed));

            var goldAfterTurnIn = runtime.Inventory.Gold;
            var itemCountAfterTurnIn = runtime.Inventory.Items.Count;
            var materialAfterTurnIn = runtime.Inventory.Materials[content.Ore.Id];
            Assert.That(runtime.Quests.TryTurnIn(quest.Id, out var repeatError), Is.False);
            Assert.That(repeatError, Is.Not.Empty);
            Assert.That(runtime.Inventory.Gold, Is.EqualTo(goldAfterTurnIn));
            Assert.That(runtime.Inventory.Items.Count, Is.EqualTo(itemCountAfterTurnIn));
            Assert.That(runtime.Inventory.Materials[content.Ore.Id], Is.EqualTo(materialAfterTurnIn));
        }

        [Test]
        public void TryTurnIn_WhenEquipmentAmountExceedsFreeSlots_LeavesItemsAndRewardsUntouched()
        {
            var content = CreateContent();
            var quest = CreateQuest(
                "quest.no_space",
                Array.Empty<string>(),
                new[] { Objective("submit.pelt", QuestObjectiveKind.SubmitItem, content.Pelt.Id, 1, true) },
                new[]
                {
                    new QuestRewardDefinition(QuestRewardKind.Gold, string.Empty, 10),
                    new QuestRewardDefinition(QuestRewardKind.Equipment, content.Sword.Id, 2)
                });
            var runtime = CreateRuntime(content, new[] { quest }, capacity: 2);
            Assert.That(runtime.Inventory.TryAdd(Item("pelt.1", content.Pelt.Id), out var peltAdd), Is.True, peltAdd);
            Assert.That(runtime.Inventory.TryAdd(Item("filler.1", content.Ore.Id), out var fillerAdd), Is.True, fillerAdd);
            Assert.That(runtime.Quests.TryAccept(quest.Id, out var acceptError), Is.True, acceptError);

            Assert.That(runtime.Quests.TryTurnIn(quest.Id, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(runtime.Inventory.Items.Any(item => item.InstanceId == "pelt.1"), Is.True);
            Assert.That(runtime.Inventory.Gold, Is.EqualTo(10));
            Assert.That(runtime.Inventory.Materials, Is.Empty);
            Assert.That(runtime.State.GetQuestState(quest.Id), Is.Not.EqualTo(QuestState.Completed));
        }

        [Test]
        public void TryTurnIn_WithEquipmentRewardAmountTwo_AddsTwoUniqueInstances()
        {
            var content = CreateContent();
            var quest = CreateQuest(
                "quest.two_equipment",
                Array.Empty<string>(),
                new[] { Objective("submit.pelt", QuestObjectiveKind.SubmitItem, content.Pelt.Id, 1, true) },
                new[] { new QuestRewardDefinition(QuestRewardKind.Equipment, content.Sword.Id, 2) });
            var runtime = CreateRuntime(content, new[] { quest }, capacity: 4);
            Assert.That(runtime.Inventory.TryAdd(Item("pelt.1", content.Pelt.Id), out var addError), Is.True, addError);
            Assert.That(runtime.Quests.TryAccept(quest.Id, out var acceptError), Is.True, acceptError);

            Assert.That(runtime.Quests.TryTurnIn(quest.Id, out var turnInError), Is.True, turnInError);

            var equipment = runtime.Inventory.Items
                .Where(item => item.ItemDefinitionId == content.Sword.Id)
                .OrderBy(item => item.InstanceId, StringComparer.Ordinal)
                .ToArray();
            Assert.That(equipment, Has.Length.EqualTo(2));
            Assert.That(equipment.Select(item => item.InstanceId).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(2));
        }

        [TestCase(QuestRewardKind.Gold)]
        [TestCase(QuestRewardKind.Material)]
        [TestCase(QuestRewardKind.Equipment)]
        public void TryTurnIn_WhenRewardServiceFailsMidway_RollsBackConsumedItemsAndPartialRewards(
            QuestRewardKind failAfter)
        {
            var content = CreateContent();
            var quest = CreateQuest(
                "quest.partial_failure",
                Array.Empty<string>(),
                new[] { Objective("submit.pelt", QuestObjectiveKind.SubmitItem, content.Pelt.Id, 1, true) },
                new[]
                {
                    new QuestRewardDefinition(QuestRewardKind.Gold, string.Empty, 7),
                    new QuestRewardDefinition(QuestRewardKind.Material, content.Ore.Id, 3),
                    new QuestRewardDefinition(QuestRewardKind.Equipment, content.Sword.Id, 2)
                });
            var runtime = CreateRuntime(
                content,
                new[] { quest },
                capacity: 8,
                (inventory, _) => new FailingRewardService(inventory, failAfter));
            Assert.That(runtime.Inventory.TryAdd(Item("pelt.1", content.Pelt.Id), out var addError), Is.True, addError);
            Assert.That(runtime.Quests.TryAccept(quest.Id, out var acceptError), Is.True, acceptError);

            var inventoryBefore = InventorySnapshot.Capture(runtime.Inventory);
            var questStateBefore = runtime.State.GetQuestState(quest.Id);

            Assert.That(runtime.Quests.TryTurnIn(quest.Id, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);

            inventoryBefore.AssertMatches(runtime.Inventory);
            Assert.That(runtime.State.GetQuestState(quest.Id), Is.EqualTo(questStateBefore));
        }
        private Runtime CreateRuntime(
            Content content,
            IEnumerable<QuestDefinition> quests,
            int capacity = 8,
            Func<InventoryService, NarrativeStateService, IQuestRewardService> rewardFactory = null)
        {
            var questList = quests.ToArray();
            var definitions = content.Definitions
                .Concat(questList.Cast<ContentDefinition>())
                .ToArray();
            var state = new NarrativeStateService(definitions);
            var inventory = new InventoryService(capacity, content.Items, 10);
            var progression = new PartyProgressionService(
                new Dictionary<string, CharacterDefinition>(StringComparer.Ordinal),
                Array.Empty<PartyMemberState>());
            IQuestRewardService rewardService = rewardFactory != null
                ? rewardFactory(inventory, state)
                : new QuestRewardService(inventory, progression, content.Items, state);
            var questService = new QuestService(
                questList.ToDictionary(value => value.Id, StringComparer.Ordinal),
                state,
                inventory,
                rewardService);
            return new Runtime(state, inventory, questService);
        }

        private Content CreateContent()
        {
            var ranger = Track(ScriptableObject.CreateInstance<NpcDefinition>());
            ranger.EditorConfigure("npc.ranger", "npc.ranger.name", string.Empty, string.Empty, 0);
            var area = Track(ScriptableObject.CreateInstance<WorldAreaDefinition>());
            area.EditorConfigure(
                "area.forest",
                new Rect(0f, 0f, 20f, 20f),
                Array.Empty<Rect>(),
                new[] { ranger },
                Array.Empty<WorldEncounterDefinition>(),
                Array.Empty<WorldInteractableDefinition>(),
                Array.Empty<ItemDropTableDefinition>());
            var pelt = CreateItem("item.pelt", ItemSlot.Accessory);
            var ore = CreateItem("item.ore", ItemSlot.Accessory);
            var sword = CreateItem("item.sword", ItemSlot.Weapon);
            var axe = CreateItem("item.axe", ItemSlot.Weapon);
            var blacksmith = Track(ScriptableObject.CreateInstance<ShopDefinition>());
            blacksmith.EditorConfigure(
                "shop.blacksmith",
                "shop.blacksmith.name",
                string.Empty,
                Array.Empty<ShopOfferDefinition>());
            var items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal)
            {
                [pelt.Id] = pelt,
                [ore.Id] = ore,
                [sword.Id] = sword,
                [axe.Id] = axe
            };
            var definitions = new ContentDefinition[] { ranger, area, pelt, ore, sword, axe, blacksmith };
            return new Content(definitions, items, area, ranger, pelt, ore, sword, axe, blacksmith);
        }

        private QuestDefinition CreateQuest(
            string id,
            IEnumerable<string> prerequisites,
            IEnumerable<QuestObjectiveDefinition> objectives,
            IEnumerable<QuestRewardDefinition> rewards)
        {
            var quest = Track(ScriptableObject.CreateInstance<QuestDefinition>());
            quest.EditorConfigure(
                id,
                id + ".title",
                id + ".description",
                prerequisites,
                objectives,
                rewards);
            return quest;
        }

        private static QuestObjectiveDefinition Objective(
            string objectiveId,
            QuestObjectiveKind kind,
            string targetId,
            int requiredCount,
            bool consumeOnTurnIn = false) =>
            new(objectiveId, kind, targetId, requiredCount, consumeOnTurnIn, "quest.objective");

        private ItemDefinition CreateItem(string id, ItemSlot slot)
        {
            var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
            item.EditorConfigure(
                id,
                id + ".name",
                slot,
                Array.Empty<string>(),
                false,
                10,
                Array.Empty<StatValue>());
            return item;
        }

        private static ItemInstance Item(string instanceId, string definitionId) =>
            new(instanceId, definitionId, 1, ItemRarity.Common, Array.Empty<AffixInstance>());

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }

        private sealed class Content
        {
            public Content(
                ContentDefinition[] definitions,
                IReadOnlyDictionary<string, ItemDefinition> items,
                WorldAreaDefinition area,
                NpcDefinition ranger,
                ItemDefinition pelt,
                ItemDefinition ore,
                ItemDefinition sword,
                ItemDefinition axe,
                ShopDefinition blacksmith)
            {
                Definitions = definitions;
                Items = items;
                Area = area;
                Ranger = ranger;
                Pelt = pelt;
                Ore = ore;
                Sword = sword;
                Axe = axe;
                Blacksmith = blacksmith;
            }

            public ContentDefinition[] Definitions { get; }
            public IReadOnlyDictionary<string, ItemDefinition> Items { get; }
            public WorldAreaDefinition Area { get; }
            public NpcDefinition Ranger { get; }
            public ItemDefinition Pelt { get; }
            public ItemDefinition Ore { get; }
            public ItemDefinition Sword { get; }
            public ItemDefinition Axe { get; }
            public ShopDefinition Blacksmith { get; }
        }

        private sealed class FailingRewardService : IQuestRewardService
        {
            private readonly InventoryService inventory;
            private readonly QuestRewardKind failAfter;

            public FailingRewardService(InventoryService inventory, QuestRewardKind failAfter)
            {
                this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
                this.failAfter = failAfter;
            }

            public bool TryValidate(QuestDefinition quest, out string error)
            {
                error = string.Empty;
                return quest != null;
            }

            public bool TryApply(QuestDefinition quest, out string error)
            {
                for (var rewardIndex = 0; rewardIndex < quest.Rewards.Length; rewardIndex++)
                {
                    var reward = quest.Rewards[rewardIndex];
                    switch (reward.Kind)
                    {
                        case QuestRewardKind.Gold:
                            inventory.AddGold(reward.Amount);
                            break;
                        case QuestRewardKind.Material:
                            inventory.AddMaterial(reward.TargetId, reward.Amount);
                            break;
                        case QuestRewardKind.Equipment:
                            for (var itemIndex = 0; itemIndex < reward.Amount; itemIndex++)
                            {
                                var instanceId = $"failing-reward:{quest.Id}:{rewardIndex}:{itemIndex}";
                                if (!inventory.TryAdd(
                                        new ItemInstance(
                                            instanceId,
                                            reward.TargetId,
                                            1,
                                            ItemRarity.Common,
                                            Array.Empty<AffixInstance>()),
                                        out error))
                                    return false;
                            }
                            break;
                        default:
                            error = NarrativeTextKeys.QuestRewardInvalid;
                            return false;
                    }

                    if (reward.Kind == failAfter)
                    {
                        error = "test.partial_failure";
                        return false;
                    }
                }

                error = string.Empty;
                return true;
            }
        }

        private sealed class InventorySnapshot
        {
            private InventorySnapshot(
                int gold,
                IReadOnlyList<string> items,
                IReadOnlyList<KeyValuePair<string, int>> materials)
            {
                Gold = gold;
                Items = items;
                Materials = materials;
            }

            private int Gold { get; }
            private IReadOnlyList<string> Items { get; }
            private IReadOnlyList<KeyValuePair<string, int>> Materials { get; }

            public static InventorySnapshot Capture(InventoryService inventory) =>
                new(
                    inventory.Gold,
                    inventory.Items
                        .OrderBy(item => item.InstanceId, StringComparer.Ordinal)
                        .Select(item => item.InstanceId + "|" + item.ItemDefinitionId)
                        .ToArray(),
                    inventory.Materials
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .ToArray());

            public void AssertMatches(InventoryService inventory)
            {
                Assert.That(inventory.Gold, Is.EqualTo(Gold));
                Assert.That(
                    inventory.Items
                        .OrderBy(item => item.InstanceId, StringComparer.Ordinal)
                        .Select(item => item.InstanceId + "|" + item.ItemDefinitionId),
                    Is.EqualTo(Items));
                Assert.That(
                    inventory.Materials.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                    Is.EqualTo(Materials));
            }
        }
        private sealed class Runtime
        {
            public Runtime(
                NarrativeStateService state,
                InventoryService inventory,
                QuestService quests)
            {
                State = state;
                Inventory = inventory;
                Quests = quests;
            }

            public NarrativeStateService State { get; }
            public InventoryService Inventory { get; }
            public QuestService Quests { get; }
        }
    }
}
