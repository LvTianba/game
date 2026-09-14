using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Data;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorderValley.Narrative.Tests
{
    public sealed class NarrativeStateServiceTests
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
        public void CaptureRestore_RestoresEveryNarrativeStateCategory()
        {
            var fixture = CreateFixture();
            var source = new NarrativeStateService(fixture.Definitions);
            Assert.That(source.Key, Is.EqualTo("narrative"));
            Assert.That(source.SetCurrentLocation(fixture.Area.Id, new Vector2(2.5f, -1.25f)), Is.True);
            Assert.That(source.SetEvent("event.beta"), Is.True);
            Assert.That(source.TryChangeFavor(fixture.Npc.Id, 2, out var favorError), Is.True, favorError);
            Assert.That(source.MarkInteractableResolved("interaction.chest"), Is.True);
            Assert.That(source.MarkDialogueNodeRead("node.hello"), Is.True);
            Assert.That(source.MarkOfferPurchased("shop.general", "offer.potion"), Is.True);
            Assert.That(source.MarkShopUnlocked("shop.blacksmith"), Is.True);
            Assert.That(source.TryAcceptQuest(fixture.Quest.Id, out var acceptError), Is.True, acceptError);
            Assert.That(
                source.TryAdvanceQuestObjective(fixture.Quest.Id, 0, 1, out var advanceError),
                Is.True,
                advanceError);

            var captured = source.Capture();
            var restored = new NarrativeStateService(fixture.Definitions);
            restored.Restore(captured);

            Assert.That(restored.GetCurrentAreaId(), Is.EqualTo(fixture.Area.Id));
            Assert.That(restored.GetCurrentPosition(), Is.EqualTo(new Vector2(2.5f, -1.25f)));
            Assert.That(restored.HasEvent("event.beta"), Is.True);
            Assert.That(restored.GetFavorTier(fixture.Npc.Id), Is.EqualTo(2));
            Assert.That(restored.IsInteractableResolved("interaction.chest"), Is.True);
            Assert.That(restored.IsDialogueNodeRead("node.hello"), Is.True);
            Assert.That(restored.IsOfferPurchased("shop.general", "offer.potion"), Is.True);
            Assert.That(restored.IsShopUnlocked("shop.blacksmith"), Is.True);
            Assert.That(restored.GetQuestState(fixture.Quest.Id), Is.EqualTo(QuestState.Active));
            Assert.That(restored.GetObjectiveProgress(fixture.Quest.Id, 0), Is.EqualTo(1));
        }

        [Test]
        public void Capture_SortsCollectionsAndPropertiesByOrdinal()
        {
            var fixture = CreateFixture();
            var state = new NarrativeStateService(fixture.Definitions);
            state.SetEvent("event.zeta");
            state.SetEvent("event.alpha");
            state.MarkInteractableResolved("interaction.zeta");
            state.MarkInteractableResolved("interaction.alpha");
            state.MarkDialogueNodeRead("node.zeta");
            state.MarkDialogueNodeRead("node.alpha");
            state.MarkOfferPurchased("shop.general", "offer.zeta");
            state.MarkOfferPurchased("shop.general", "offer.alpha");
            Assert.That(state.IsOfferPurchased("offer.alpha"), Is.True);
            state.MarkShopUnlocked("shop.zeta");
            state.MarkShopUnlocked("shop.alpha");
            state.TryChangeFavor("npc.zeta", 1, out _);
            state.TryChangeFavor("npc.alpha", 1, out _);

            var captured = state.Capture();

            Assert.That(Names(captured["events"]), Is.EqualTo(new[] { "event.alpha", "event.zeta" }));
            Assert.That(Names(captured["resolvedInteractables"]),
                Is.EqualTo(new[] { "interaction.alpha", "interaction.zeta" }));
            Assert.That(Names(captured["readDialogueNodes"]),
                Is.EqualTo(new[] { "node.alpha", "node.zeta" }));
            Assert.That(Names(captured["unlockedShops"]),
                Is.EqualTo(new[] { "shop.alpha", "shop.zeta" }));
            Assert.That(
                ((JObject)captured["favorTiers"]).Properties().Select(property => property.Name),
                Is.EqualTo(new[] { "npc.alpha", "npc.zeta" }));
            Assert.That(
                ((JArray)captured["purchasedOffers"])
                    .Select(value => (string)value["shopId"] + "/" + (string)value["offerId"]),
                Is.EqualTo(new[] { "shop.general/offer.alpha", "shop.general/offer.zeta" }));
        }

        [Test]
        public void Restore_WhenReferenceIsInvalid_ThrowsAndLeavesExistingStateUntouched()
        {
            var fixture = CreateFixture();
            var state = new NarrativeStateService(fixture.Definitions);
            state.SetCurrentLocation(fixture.Area.Id, new Vector2(4f, 5f));
            state.TryChangeFavor(fixture.Npc.Id, 1, out _);
            var malformed = (JObject)state.Capture().DeepClone();
            malformed["currentAreaId"] = "area.missing";

            Assert.Throws<InvalidOperationException>(() => state.Restore(malformed));

            Assert.That(state.GetCurrentAreaId(), Is.EqualTo(fixture.Area.Id));
            Assert.That(state.GetCurrentPosition(), Is.EqualTo(new Vector2(4f, 5f)));
            Assert.That(state.GetFavorTier(fixture.Npc.Id), Is.EqualTo(1));
        }

        private Fixture CreateFixture()
        {
            var npcAlpha = CreateNpc("npc.alpha");
            var npcZeta = CreateNpc("npc.zeta");
            var item = CreateItem("item.potion");
            var general = CreateShop(
                "shop.general",
                new[]
                {
                    Offer("offer.zeta", item),
                    Offer("offer.potion", item),
                    Offer("offer.alpha", item)
                });
            var blacksmith = CreateShop("shop.blacksmith", Array.Empty<ShopOfferDefinition>());
            var shopAlpha = CreateShop("shop.alpha", Array.Empty<ShopOfferDefinition>());
            var shopZeta = CreateShop("shop.zeta", Array.Empty<ShopOfferDefinition>());
            var dialogue = Track(ScriptableObject.CreateInstance<DialogueDefinition>());
            dialogue.EditorConfigure(
                "dialogue.elder",
                "node.hello",
                new[]
                {
                    new DialogueNodeDefinition(
                        "node.hello",
                        npcAlpha.Id,
                        "dialogue.elder.hello",
                        Array.Empty<DialogueConditionDefinition>(),
                        Array.Empty<DialogueActionDefinition>(),
                        Array.Empty<DialogueChoiceDefinition>(),
                        string.Empty),
                    new DialogueNodeDefinition("node.zeta", npcAlpha.Id, "dialogue.zeta", Array.Empty<DialogueConditionDefinition>(), Array.Empty<DialogueActionDefinition>(), Array.Empty<DialogueChoiceDefinition>(), string.Empty),
                    new DialogueNodeDefinition("node.alpha", npcAlpha.Id, "dialogue.alpha", Array.Empty<DialogueConditionDefinition>(), Array.Empty<DialogueActionDefinition>(), Array.Empty<DialogueChoiceDefinition>(), string.Empty)
                });
            var interactionAlpha = new WorldInteractableDefinition(
                "interaction.alpha",
                WorldInteractableKind.Investigate,
                "interaction.alpha.label",
                Vector2.zero,
                1f,
                "event.alpha",
                Vector2.zero,
                string.Empty);
            var interactionZeta = new WorldInteractableDefinition(
                "interaction.zeta",
                WorldInteractableKind.Investigate,
                "interaction.zeta.label",
                Vector2.zero,
                1f,
                "event.zeta",
                Vector2.zero,
                string.Empty);
            var chest = new WorldInteractableDefinition(
                "interaction.chest",
                WorldInteractableKind.Chest,
                "interaction.chest.label",
                Vector2.zero,
                1f,
                "loot.chest",
                Vector2.zero,
                string.Empty);
            var area = Track(ScriptableObject.CreateInstance<WorldAreaDefinition>());
            area.EditorConfigure(
                "area.village",
                new Rect(0f, 0f, 10f, 10f),
                Array.Empty<Rect>(),
                new[] { npcAlpha, npcZeta },
                Array.Empty<WorldEncounterDefinition>(),
                new[] { chest, interactionAlpha, interactionZeta },
                Array.Empty<ItemDropTableDefinition>(),
                new[] { "event.zeta", "event.beta", "event.alpha" },
                Array.Empty<string>());
            var quest = Track(ScriptableObject.CreateInstance<QuestDefinition>());
            quest.EditorConfigure(
                "quest.visit",
                "quest.visit.title",
                "quest.visit.description",
                Array.Empty<string>(),
                new[]
                {
                    new QuestObjectiveDefinition(
                        QuestObjectiveKind.ReachLocation,
                        area.Id,
                        2,
                        false,
                        "quest.visit.objective")
                },
                new[] { new QuestRewardDefinition(QuestRewardKind.Gold, string.Empty, 10) });

            var definitions = new ContentDefinition[]
            {
                npcAlpha,
                npcZeta,
                item,
                general,
                blacksmith,
                shopAlpha,
                shopZeta,
                dialogue,
                area,
                quest
            };
            return new Fixture(definitions, area, npcAlpha, quest);
        }

        private NpcDefinition CreateNpc(string id)
        {
            var npc = Track(ScriptableObject.CreateInstance<NpcDefinition>());
            npc.EditorConfigure(id, id + ".name", "dialogue.elder", string.Empty, 0);
            return npc;
        }

        private ItemDefinition CreateItem(string id)
        {
            var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
            item.EditorConfigure(
                id,
                id + ".name",
                ItemSlot.Accessory,
                Array.Empty<string>(),
                false,
                5,
                Array.Empty<StatValue>());
            return item;
        }

        private ShopDefinition CreateShop(string id, IEnumerable<ShopOfferDefinition> offers)
        {
            var shop = Track(ScriptableObject.CreateInstance<ShopDefinition>());
            shop.EditorConfigure(id, id + ".name", string.Empty, offers);
            return shop;
        }

        private static ShopOfferDefinition Offer(string offerId, ItemDefinition item) =>
            new(offerId, item, ItemRarity.Common, 1, Array.Empty<AffixDefinition>());

        private static IEnumerable<string> Names(JToken token) =>
            ((JArray)token).Values<string>();

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }

        private sealed class Fixture
        {
            public Fixture(
                ContentDefinition[] definitions,
                WorldAreaDefinition area,
                NpcDefinition npc,
                QuestDefinition quest)
            {
                Definitions = definitions;
                Area = area;
                Npc = npc;
                Quest = quest;
            }

            public ContentDefinition[] Definitions { get; }
            public WorldAreaDefinition Area { get; }
            public NpcDefinition Npc { get; }
            public QuestDefinition Quest { get; }
        }
    }
}
