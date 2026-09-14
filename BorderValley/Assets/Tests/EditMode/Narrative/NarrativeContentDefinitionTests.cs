using System.Collections.Generic;
using System.Linq;
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
                new[] { new QuestObjectiveDefinition(QuestObjectiveKind.ReachLocation, "area.test", 1, false, "quest.first.objective") },
                new[] { new QuestRewardDefinition(QuestRewardKind.Gold, string.Empty, 10) });
            var second = Track(ScriptableObject.CreateInstance<QuestDefinition>());
            second.EditorConfigure(
                "quest.second",
                "quest.second.title",
                "quest.second.description",
                new[] { "quest.first" },
                new[] { new QuestObjectiveDefinition(QuestObjectiveKind.ReachLocation, "area.test", 1, false, "quest.second.objective") },
                new[] { new QuestRewardDefinition(QuestRewardKind.Experience, string.Empty, 10) });

            var issues = ContentValidator.Validate(new ContentDefinition[] { first, second }).ToList();

            Assert.That(issues.Any(issue => issue.Code == "cyclic_quest_prerequisite"), Is.True);
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
                new[] { new QuestObjectiveDefinition(QuestObjectiveKind.TalkToNpc, "npc.test", 1, false, "quest.test.objective") },
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

        private static DialogueNodeDefinition Node(string nodeId, string nextNodeId = "") =>
            new(
                nodeId,
                string.Empty,
                "dialogue.node.text",
                System.Array.Empty<DialogueConditionDefinition>(),
                System.Array.Empty<DialogueActionDefinition>(),
                System.Array.Empty<DialogueChoiceDefinition>(),
                nextNodeId);

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
