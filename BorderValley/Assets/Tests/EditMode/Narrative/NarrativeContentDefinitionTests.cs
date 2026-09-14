using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Combat;
using BorderValley.Data;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorderValley.Narrative.Tests
{
    public sealed class NarrativeContentDefinitionTests
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
        public void Validate_DuplicateDialogueNode_ReturnsDuplicateDialogueNode()
        {
            var dialogue = Track(ScriptableObject.CreateInstance<DialogueDefinition>());
            dialogue.EditorConfigure(
                "dialogue.test",
                "node.start",
                new[]
                {
                    Node("node.start"),
                    Node("node.start")
                });

            AssertIssue("duplicate_dialogue_node", dialogue);
        }

        [Test]
        public void Validate_MissingDialogueNodeTarget_ReturnsMissingDialogueNode()
        {
            var dialogue = Track(ScriptableObject.CreateInstance<DialogueDefinition>());
            dialogue.EditorConfigure(
                "dialogue.test",
                "node.start",
                new[] { Node("node.start", "node.missing") });

            AssertIssue("missing_dialogue_node", dialogue);
        }

        [Test]
        public void Validate_CyclicQuestPrerequisites_ReturnsCyclicQuestPrerequisite()
        {
            var first = Track(ScriptableObject.CreateInstance<QuestDefinition>());
            first.EditorConfigure(
                "quest.first",
                "quest.first.title",
                "quest.first.description",
                new[] { "quest.second" },
                new[] { new QuestObjectiveDefinition("objective.first", QuestObjectiveKind.ReachLocation, "area.test", 1, false, "quest.first.objective") },
                new[] { new QuestRewardDefinition(QuestRewardKind.Gold, string.Empty, 10) });
            var second = Track(ScriptableObject.CreateInstance<QuestDefinition>());
            second.EditorConfigure(
                "quest.second",
                "quest.second.title",
                "quest.second.description",
                new[] { "quest.first" },
                new[] { new QuestObjectiveDefinition("objective.second", QuestObjectiveKind.ReachLocation, "area.test", 1, false, "quest.second.objective") },
                new[] { new QuestRewardDefinition(QuestRewardKind.Experience, string.Empty, 10) });

            var issues = ContentValidator.Validate(new ContentDefinition[] { first, second }).ToList();

            Assert.That(issues.Any(issue => issue.Code == "cyclic_quest_prerequisite"), Is.True);
        }

        [Test]
        public void Validate_DuplicateQuestObjectiveId_ReturnsDuplicateId()
        {
            var area = CreateArea("area.test", System.Array.Empty<string>());
            var quest = Track(ScriptableObject.CreateInstance<QuestDefinition>());
            quest.EditorConfigure(
                "quest.duplicate",
                "quest.duplicate.title",
                "quest.duplicate.description",
                System.Array.Empty<string>(),
                new[]
                {
                    new QuestObjectiveDefinition("objective.duplicate", QuestObjectiveKind.ReachLocation, area.Id, 1, false, "quest.objective.first"),
                    new QuestObjectiveDefinition("objective.duplicate", QuestObjectiveKind.TalkToNpc, "npc.test", 1, false, "quest.objective.second")
                },
                System.Array.Empty<QuestRewardDefinition>());

            AssertIssue("duplicate_id", area, quest);
        }

        [Test]
        public void Validate_MissingQuestObjectiveId_ReturnsMissingId()
        {
            var area = CreateArea("area.test", System.Array.Empty<string>());
            var quest = Track(ScriptableObject.CreateInstance<QuestDefinition>());
            quest.EditorConfigure(
                "quest.missing_objective",
                "quest.missing_objective.title",
                "quest.missing_objective.description",
                System.Array.Empty<string>(),
                new[]
                {
                    new QuestObjectiveDefinition(string.Empty, QuestObjectiveKind.ReachLocation, area.Id, 1, false, "quest.objective")
                },
                System.Array.Empty<QuestRewardDefinition>());

            AssertIssue("missing_id", area, quest);
        }
        [Test]
        public void Validate_MissingShopItem_ReturnsMissingShopItem()
        {
            var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
            item.EditorConfigure(
                "item.missing",
                "item.missing.name",
                ItemSlot.Weapon,
                System.Array.Empty<string>(),
                false,
                10,
                System.Array.Empty<StatValue>());
            var shop = Track(ScriptableObject.CreateInstance<ShopDefinition>());
            shop.EditorConfigure(
                "shop.test",
                "shop.test.name",
                string.Empty,
                new[] { new ShopOfferDefinition("offer.test", item, ItemRarity.Common, 1, System.Array.Empty<AffixDefinition>()) });

            AssertIssue("missing_shop_item", shop);
        }

        [Test]
        public void Validate_MissingWorldTarget_ReturnsMissingWorldTarget()
        {
            var area = Track(ScriptableObject.CreateInstance<WorldAreaDefinition>());
            area.EditorConfigure(
                "area.test",
                new Rect(0f, 0f, 10f, 10f),
                System.Array.Empty<Rect>(),
                System.Array.Empty<NpcDefinition>(),
                System.Array.Empty<WorldEncounterDefinition>(),
                new[]
                {
                    new WorldInteractableDefinition(
                        "interaction.missing",
                        WorldInteractableKind.Npc,
                        "interaction.missing.label",
                        Vector2.zero,
                        1f,
                        "npc.missing",
                        Vector2.zero,
                        string.Empty)
                },
                System.Array.Empty<ItemDropTableDefinition>());

            AssertIssue("missing_world_target", area);
        }

        [Test]
        public void Validate_ResolvedNarrativeAndWorldReferences_ReturnsNoIssues()
        {
            var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
            item.EditorConfigure(
                "item.sword",
                "item.sword.name",
                ItemSlot.Weapon,
                System.Array.Empty<string>(),
                false,
                10,
                System.Array.Empty<StatValue>());
            var shop = Track(ScriptableObject.CreateInstance<ShopDefinition>());
            shop.EditorConfigure(
                "shop.test",
                "shop.test.name",
                string.Empty,
                new[] { new ShopOfferDefinition("offer.test", item, ItemRarity.Common, 1, System.Array.Empty<AffixDefinition>()) });
            var dialogue = Track(ScriptableObject.CreateInstance<DialogueDefinition>());
            dialogue.EditorConfigure("dialogue.test", "node.start", new[] { Node("node.start") });
            var npc = Track(ScriptableObject.CreateInstance<NpcDefinition>());
            npc.EditorConfigure("npc.test", "npc.test.name", "dialogue.test", "shop.test");
            var quest = Track(ScriptableObject.CreateInstance<QuestDefinition>());
            quest.EditorConfigure(
                "quest.test",
                "quest.test.title",
                "quest.test.description",
                System.Array.Empty<string>(),
                new[] { new QuestObjectiveDefinition("objective.test", QuestObjectiveKind.TalkToNpc, "npc.test", 1, false, "quest.test.objective") },
                new[] { new QuestRewardDefinition(QuestRewardKind.Gold, string.Empty, 10) });
            var encounter = Track(ScriptableObject.CreateInstance<WorldEncounterDefinition>());
            encounter.EditorConfigure(
                "encounter.test",
                "scenario.test",
                new[] { "enemy.test" },
                string.Empty,
                10,
                20,
                Vector2.zero,
                2f,
                false,
                string.Empty,
                "event.complete");
            var area = Track(ScriptableObject.CreateInstance<WorldAreaDefinition>());
            area.EditorConfigure(
                "area.test",
                new Rect(0f, 0f, 10f, 10f),
                System.Array.Empty<Rect>(),
                new[] { npc },
                new[] { encounter },
                new[]
                {
                    new WorldInteractableDefinition(
                        "interaction.npc",
                        WorldInteractableKind.Npc,
                        "interaction.npc.label",
                        Vector2.one,
                        1f,
                        "npc.test",
                        Vector2.zero,
                        string.Empty)
                },
                System.Array.Empty<ItemDropTableDefinition>());

            var issues = ContentValidator.Validate(new ContentDefinition[] { item, shop, dialogue, npc, quest, encounter, area }).ToList();

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void Validate_DuplicateWorldInteractableId_ReturnsDuplicateId()
        {
            var npc = Track(ScriptableObject.CreateInstance<NpcDefinition>());
            npc.EditorConfigure("npc.test", "npc.test.name", string.Empty, string.Empty);
            var area = Track(ScriptableObject.CreateInstance<WorldAreaDefinition>());
            area.EditorConfigure(
                "area.test",
                new Rect(0f, 0f, 10f, 10f),
                System.Array.Empty<Rect>(),
                new[] { npc },
                System.Array.Empty<WorldEncounterDefinition>(),
                new[]
                {
                    Interactable("interaction.duplicate", "npc.test"),
                    Interactable("interaction.duplicate", "npc.test")
                },
                System.Array.Empty<ItemDropTableDefinition>());

            var issues = ContentValidator.Validate(new ContentDefinition[] { npc, area }).ToList();

            Assert.That(issues.Count(issue => issue.Code == "duplicate_id"), Is.EqualTo(1));
        }

        [Test]
        public void Validate_SetEventTarget_RequiresDeclaredEvent()
        {
            var dialogue = Track(ScriptableObject.CreateInstance<DialogueDefinition>());
            dialogue.EditorConfigure(
                "dialogue.test",
                "node.start",
                new[]
                {
                    Node(
                        "node.start",
                        actions: new[]
                        {
                            new DialogueActionDefinition(DialogueActionKind.SetEvent, "event.missing")
                        })
                });

            AssertIssue("missing_dialogue_node", dialogue);
        }

        [Test]
        public void Validate_EventConditionTarget_RequiresDeclaredEvent()
        {
            var dialogue = Track(ScriptableObject.CreateInstance<DialogueDefinition>());
            dialogue.EditorConfigure(
                "dialogue.test",
                "node.start",
                new[]
                {
                    Node(
                        "node.start",
                        conditions: new[]
                        {
                            new DialogueConditionDefinition(DialogueConditionKind.Event, "event.circular")
                        }),
                    Node(
                        "node.declare",
                        actions: new[]
                        {
                            new DialogueActionDefinition(DialogueActionKind.SetEvent, "event.circular")
                        })
                });

            AssertIssue("missing_dialogue_node", dialogue);
        }

        [Test]
        public void Validate_WorldAreaEventIds_RejectEmptyValue()
        {
            var area = CreateArea("area.test", new[] { string.Empty });

            AssertIssue("missing_id", area);
        }

        [Test]
        public void Validate_WorldAreaEventIds_RejectOrdinalDuplicate()
        {
            var area = CreateArea("area.test", new[] { "event.test", "event.test" });

            AssertIssue("duplicate_id", area);
        }

        [Test]
        public void Validate_DeclaredEventRegistry_AllowsSetEventReferences()
        {
            var encounter = Track(ScriptableObject.CreateInstance<WorldEncounterDefinition>());
            encounter.EditorConfigure(
                "encounter.test",
                "scenario.test",
                new[] { "enemy.test" },
                string.Empty,
                0,
                0,
                Vector2.zero,
                1f,
                false,
                string.Empty,
                "event.encounter");
            var area = Track(ScriptableObject.CreateInstance<WorldAreaDefinition>());
            area.EditorConfigure(
                "area.test",
                new Rect(0f, 0f, 10f, 10f),
                System.Array.Empty<Rect>(),
                System.Array.Empty<NpcDefinition>(),
                new[] { encounter },
                new[]
                {
                    new WorldInteractableDefinition(
                        "interaction.investigate",
                        WorldInteractableKind.Investigate,
                        "interaction.investigate.label",
                        Vector2.zero,
                        1f,
                        "event.investigate",
                        Vector2.zero,
                        string.Empty)
                },
                System.Array.Empty<ItemDropTableDefinition>(),
                new[] { "event.area" },
                System.Array.Empty<string>());
            var dialogue = Track(ScriptableObject.CreateInstance<DialogueDefinition>());
            dialogue.EditorConfigure(
                "dialogue.test",
                "node.start",
                new[]
                {
                    Node(
                        "node.start",
                        actions: new[]
                        {
                            new DialogueActionDefinition(DialogueActionKind.SetEvent, "event.area"),
                            new DialogueActionDefinition(DialogueActionKind.SetEvent, "event.encounter"),
                            new DialogueActionDefinition(DialogueActionKind.SetEvent, "event.investigate")
                        })
                });

            var issues = ContentValidator.Validate(new ContentDefinition[] { encounter, area, dialogue }).ToList();

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void Validate_MissingShopRequiredEvent_ReturnsMissingWorldTarget()
        {
            var item = CreateItem("item.test");
            var shop = Track(ScriptableObject.CreateInstance<ShopDefinition>());
            shop.EditorConfigure(
                "shop.test",
                "shop.test.name",
                "event.missing",
                new[] { new ShopOfferDefinition("offer.test", item, ItemRarity.Common, 1, System.Array.Empty<AffixDefinition>()) });

            AssertIssue("missing_world_target", item, shop);
        }

        [Test]
        public void Validate_ShopCritChanceBudget_UsesHundredPointUnits()
        {
            var item = CreateItem("item.crit");
            var affix = CreateAffix("affix.crit", AffixEffectKind.FlatStat, CombatStat.CritChanceBps, 1200, 1200);
            var shop = CreateShop("shop.crit", ItemRarity.Fine, item, new[] { affix });

            var issues = ContentValidator.Validate(new ContentDefinition[] { item, affix, shop }).ToList();

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void Validate_ShopCritChanceOverBudget_ReturnsMissingShopItem()
        {
            var item = CreateItem("item.crit");
            var affix = CreateAffix("affix.crit", AffixEffectKind.FlatStat, CombatStat.CritChanceBps, 1300, 1300);
            var shop = CreateShop("shop.crit", ItemRarity.Fine, item, new[] { affix });

            AssertIssue("missing_shop_item", item, affix, shop);
        }

        [Test]
        public void Validate_EpicShopOfferWithoutEligibleSpecial_ReturnsMissingShopItem()
        {
            var item = CreateItem("item.epic");
            var first = CreateAffix("affix.first", AffixEffectKind.FlatStat, CombatStat.Power, 1, 1);
            var second = CreateAffix("affix.second", AffixEffectKind.FlatStat, CombatStat.Armor, 1, 1);
            var third = CreateAffix("affix.third", AffixEffectKind.FlatStat, CombatStat.Speed, 1, 1);
            var special = CreateAffix("affix.special", AffixEffectKind.SkillModifier, CombatStat.Power, 1, 1);
            var shop = CreateShop("shop.epic", ItemRarity.Epic, item, new[] { first, second, third });

            AssertIssue("missing_shop_item", item, first, second, third, special, shop);
        }

        [Test]
        public void Validate_EpicShopOfferWithEligibleSpecial_ReturnsNoIssues()
        {
            var item = CreateItem("item.epic");
            var first = CreateAffix("affix.first", AffixEffectKind.FlatStat, CombatStat.Power, 1, 1);
            var second = CreateAffix("affix.second", AffixEffectKind.FlatStat, CombatStat.Armor, 1, 1);
            var special = CreateAffix("affix.special", AffixEffectKind.SkillModifier, CombatStat.Power, 1, 1);
            var shop = CreateShop("shop.epic", ItemRarity.Epic, item, new[] { first, second, special });

            var issues = ContentValidator.Validate(new ContentDefinition[] { item, first, second, special, shop }).ToList();

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void Validate_AdvanceQuestWithoutObjectiveId_ReturnsMissingDialogueNode()
        {
            var quest = CreateQuestWithObjective("quest.advance", "objective.advance");
            var dialogue = CreateActionDialogue(
                "dialogue.advance",
                new DialogueActionDefinition(DialogueActionKind.AdvanceQuest, quest.Id));

            AssertIssue("missing_dialogue_node", quest, dialogue);
        }

        [Test]
        public void Validate_AdvanceQuestWithObjectiveFromOtherQuest_ReturnsMissingDialogueNode()
        {
            var quest = CreateQuestWithObjective("quest.advance", "objective.advance");
            var other = CreateQuestWithObjective("quest.other", "objective.other");
            var dialogue = CreateActionDialogue(
                "dialogue.advance",
                new DialogueActionDefinition(
                    DialogueActionKind.AdvanceQuest,
                    quest.Id,
                    other.Objectives[0].ObjectiveId));

            AssertIssue("missing_dialogue_node", quest, other, dialogue);
        }

        [Test]
        public void Validate_NonAdvanceQuestWithObjectiveId_ReturnsMissingDialogueNode()
        {
            var quest = CreateQuestWithObjective("quest.accept", "objective.accept");
            var dialogue = CreateActionDialogue(
                "dialogue.accept",
                new DialogueActionDefinition(
                    DialogueActionKind.AcceptQuest,
                    quest.Id,
                    quest.Objectives[0].ObjectiveId));

            AssertIssue("missing_dialogue_node", quest, dialogue);
        }

        [Test]
        public void Validate_AdvanceQuestWithOwnedObjectiveId_ReturnsNoIssues()
        {
            var quest = CreateQuestWithObjective("quest.advance", "objective.advance");
            var dialogue = CreateActionDialogue(
                "dialogue.advance",
                new DialogueActionDefinition(
                    DialogueActionKind.AdvanceQuest,
                    quest.Id,
                    quest.Objectives[0].ObjectiveId));

            var issues = ContentValidator.Validate(new ContentDefinition[] { quest, dialogue }).ToList();

            Assert.That(issues, Is.Empty);
        }
        private QuestDefinition CreateQuestWithObjective(string questId, string objectiveId)
        {
            var quest = Track(ScriptableObject.CreateInstance<QuestDefinition>());
            quest.EditorConfigure(
                questId,
                questId + ".title",
                questId + ".description",
                System.Array.Empty<string>(),
                new[]
                {
                    new QuestObjectiveDefinition(
                        objectiveId,
                        QuestObjectiveKind.DefeatEnemy,
                        "enemy.test",
                        1,
                        false,
                        "quest.objective")
                },
                System.Array.Empty<QuestRewardDefinition>());
            return quest;
        }

        private DialogueDefinition CreateActionDialogue(
            string dialogueId,
            DialogueActionDefinition action)
        {
            var dialogue = Track(ScriptableObject.CreateInstance<DialogueDefinition>());
            dialogue.EditorConfigure(
                dialogueId,
                "node.start",
                new[] { Node("node.start", actions: new[] { action }) });
            return dialogue;
        }
        private static DialogueNodeDefinition Node(
            string nodeId,
            string nextNodeId = "",
            IEnumerable<DialogueConditionDefinition> conditions = null,
            IEnumerable<DialogueActionDefinition> actions = null) =>
            new(
                nodeId,
                string.Empty,
                "dialogue.node.text",
                conditions ?? System.Array.Empty<DialogueConditionDefinition>(),
                actions ?? System.Array.Empty<DialogueActionDefinition>(),
                System.Array.Empty<DialogueChoiceDefinition>(),
                nextNodeId);

        private WorldInteractableDefinition Interactable(string id, string targetId) =>
            new(
                id,
                WorldInteractableKind.Npc,
                "interaction.label",
                Vector2.zero,
                1f,
                targetId,
                Vector2.zero,
                string.Empty);

        private WorldAreaDefinition CreateArea(string id, string[] eventIds)
        {
            var area = Track(ScriptableObject.CreateInstance<WorldAreaDefinition>());
            area.EditorConfigure(
                id,
                new Rect(0f, 0f, 10f, 10f),
                System.Array.Empty<Rect>(),
                System.Array.Empty<NpcDefinition>(),
                System.Array.Empty<WorldEncounterDefinition>(),
                System.Array.Empty<WorldInteractableDefinition>(),
                System.Array.Empty<ItemDropTableDefinition>(),
                eventIds,
                System.Array.Empty<string>());
            return area;
        }

        private ItemDefinition CreateItem(string id)
        {
            var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
            item.EditorConfigure(
                id,
                id + ".name",
                ItemSlot.Weapon,
                System.Array.Empty<string>(),
                false,
                10,
                System.Array.Empty<StatValue>());
            return item;
        }

        private AffixDefinition CreateAffix(
            string id,
            AffixEffectKind effectKind,
            CombatStat stat,
            int minValue,
            int maxValue,
            int budgetCost = 1)
        {
            var affix = Track(ScriptableObject.CreateInstance<AffixDefinition>());
            affix.EditorConfigure(
                id,
                id + ".name",
                new[] { ItemSlot.Weapon },
                ItemRarity.Common,
                effectKind,
                stat,
                SkillModifierKind.Radius,
                PassiveEffectKind.OnAttackApplySlow,
                minValue,
                maxValue,
                1,
                1,
                1,
                budgetCost,
                System.Array.Empty<string>());
            return affix;
        }

        private ShopDefinition CreateShop(
            string id,
            ItemRarity rarity,
            ItemDefinition item,
            IEnumerable<AffixDefinition> affixes)
        {
            var shop = Track(ScriptableObject.CreateInstance<ShopDefinition>());
            shop.EditorConfigure(
                id,
                id + ".name",
                string.Empty,
                new[] { new ShopOfferDefinition("offer." + id, item, rarity, 1, affixes) });
            return shop;
        }

        private void AssertIssue(string expectedCode, params ContentDefinition[] definitions)
        {
            var issues = ContentValidator.Validate(definitions).ToList();
            Assert.That(issues.Any(issue => issue.Code == expectedCode), Is.True,
                $"Expected issue '{expectedCode}', got: {string.Join(", ", issues.Select(issue => issue.Code))}");
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }
    }
}
