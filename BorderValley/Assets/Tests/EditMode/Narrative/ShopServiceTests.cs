using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Data;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;
using BorderValley.Inventory;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorderValley.Narrative.Tests
{
    public sealed class ShopServiceTests
    {
        private const string ShopId = "shop.general";
        private const string OfferId = "offer.sword";
        private const string EventId = "event.merchant.arrived";
        private const string MerchantId = "npc.merchant";
        private const string SwordId = "item.sword";
        private const string QuestItemId = "item.quest";
        private const string FillerId = "item.filler";

        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                if (value != null)
                    Object.DestroyImmediate(value);
            created.Clear();
        }

        [Test]
        public void GetOffers_WhenShopLocked_HidesOffersAndUnlocksAfterEvent()
        {
            var fixture = CreateFixture();

            Assert.That(fixture.Shop.GetOffers(ShopId), Is.Empty);
            Assert.That(fixture.State.SetEvent(EventId), Is.True);

            var offers = fixture.Shop.GetOffers(ShopId);
            Assert.That(offers.Count, Is.EqualTo(1));
            Assert.That(offers[0].OfferId, Is.EqualTo(OfferId));
            Assert.That(offers[0].Item.InstanceId, Is.EqualTo(ShopOfferFactory.CreateInstanceId(ShopId, 0)));
            Assert.That(offers[0].BuyPrice, Is.EqualTo(20));
            Assert.That(offers[0].IsPurchased, Is.False);
        }

        [Test]
        public void TryBuy_AfterPurchase_HidesOfferAndRemainsPurchasedAfterSaveRestore()
        {
            var fixture = CreateFixture();
            Assert.That(fixture.State.SetEvent(EventId), Is.True);

            Assert.That(fixture.Shop.TryBuy(ShopId, OfferId, out var buyError), Is.True, buyError);
            Assert.That(fixture.Inventory.Gold, Is.EqualTo(80));
            Assert.That(fixture.Inventory.Items.Select(item => item.InstanceId),
                Is.EqualTo(new[] { ShopOfferFactory.CreateInstanceId(ShopId, 0) }));
            Assert.That(fixture.Shop.GetOffers(ShopId), Is.Empty);
            Assert.That(fixture.State.IsOfferPurchased(ShopId, OfferId), Is.True);

            var restoredState = new NarrativeStateService(fixture.Definitions);
            restoredState.Restore(fixture.State.Capture());
            var restoredShop = new ShopService(fixture.Definitions, restoredState, fixture.Economy);

            Assert.That(restoredShop.GetOffers(ShopId), Is.Empty);
            Assert.That(restoredShop.TryBuy(ShopId, OfferId, out var restoredError), Is.False);
            Assert.That(restoredError, Is.EqualTo(NarrativeTextKeys.UnknownOffer));
            Assert.That(fixture.Inventory.Gold, Is.EqualTo(80));
        }

        [Test]
        public void GetOffers_WithUnfamiliarFavor_UsesBasePrice()
        {
            var fixture = CreateFixture(favorTier: 0);
            Assert.That(fixture.State.SetEvent(EventId), Is.True);

            Assert.That(fixture.Shop.GetOffers(ShopId).Single().BuyPrice, Is.EqualTo(20));
        }

        [Test]
        public void TryBuy_WithFriendlyFavor_AppliesFivePercentDiscount()
        {
            var fixture = CreateFixture(favorTier: 1);
            Assert.That(fixture.State.SetEvent(EventId), Is.True);

            Assert.That(fixture.Shop.GetOffers(ShopId).Single().BuyPrice, Is.EqualTo(19));
            Assert.That(fixture.Shop.TryBuy(ShopId, OfferId, out var error), Is.True, error);
            Assert.That(fixture.Inventory.Gold, Is.EqualTo(81));
        }

        [Test]
        public void TryBuy_WithTrustedFavor_AppliesTenPercentDiscount()
        {
            var fixture = CreateFixture(favorTier: 2);
            Assert.That(fixture.State.SetEvent(EventId), Is.True);

            Assert.That(fixture.Shop.GetOffers(ShopId).Single().BuyPrice, Is.EqualTo(18));
            Assert.That(fixture.Shop.TryBuy(ShopId, OfferId, out var error), Is.True, error);
            Assert.That(fixture.Inventory.Gold, Is.EqualTo(82));
        }

        [Test]
        public void PriceModifiers_WithNonDivisiblePrices_UseDeterministicFloorRounding()
        {
            var fixture = CreateFixture(baseValue: 101, favorTier: 1);
            Assert.That(fixture.State.SetEvent(EventId), Is.True);

            Assert.That(fixture.Shop.GetOffers(ShopId).Single().BuyPrice, Is.EqualTo(9));
            Assert.That(fixture.Shop.TryBuy(ShopId, OfferId, out var buyError), Is.True, buyError);
            Assert.That(fixture.Inventory.Gold, Is.EqualTo(91));

            var instanceId = ShopOfferFactory.CreateInstanceId(ShopId, 0);
            Assert.That(fixture.Economy.TrySell(instanceId, 9500, out var sellError), Is.True, sellError);
            Assert.That(fixture.Inventory.Gold, Is.EqualTo(94));
        }

        [Test]
        public void TryBuy_WithInsufficientGold_DoesNotChangeGoldInventoryOrOffer()
        {
            var fixture = CreateFixture(startingGold: 19);
            Assert.That(fixture.State.SetEvent(EventId), Is.True);

            Assert.That(fixture.Shop.TryBuy(ShopId, OfferId, out var error), Is.False);
            Assert.That(error, Is.EqualTo(InventoryTextKeys.NotEnoughGold));
            Assert.That(fixture.Inventory.Gold, Is.EqualTo(19));
            Assert.That(fixture.Inventory.Items, Is.Empty);
            Assert.That(fixture.Shop.GetOffers(ShopId).Count, Is.EqualTo(1));
            Assert.That(fixture.State.IsOfferPurchased(ShopId, OfferId), Is.False);
        }
        [Test]
        public void TryBuy_WhenBagIsFull_RollsBackGoldAndKeepsOfferAvailable()
        {
            var fixture = CreateFixture(capacity: 1);
            Assert.That(fixture.State.SetEvent(EventId), Is.True);
            Assert.That(fixture.Inventory.TryAdd(Item("filler.0", FillerId), out var addError), Is.True, addError);

            Assert.That(fixture.Shop.TryBuy(ShopId, OfferId, out var error), Is.False);
            Assert.That(error, Is.EqualTo(InventoryTextKeys.BagFull));
            Assert.That(fixture.Inventory.Gold, Is.EqualTo(100));
            Assert.That(fixture.Inventory.Items.Select(item => item.InstanceId), Is.EqualTo(new[] { "filler.0" }));
            Assert.That(fixture.Shop.GetOffers(ShopId).Count, Is.EqualTo(1));
            Assert.That(fixture.State.IsOfferPurchased(ShopId, OfferId), Is.False);
        }

        [Test]
        public void TryBuy_WhenSuccessful_NotifiesSpendBeforeBagAdd()
        {
            var fixture = CreateFixture();
            Assert.That(fixture.State.SetEvent(EventId), Is.True);
            var observations = new List<string>();
            fixture.Inventory.Changed += () =>
                observations.Add($"{fixture.Inventory.Gold}:{fixture.Inventory.Items.Count}");

            Assert.That(fixture.Shop.TryBuy(ShopId, OfferId, out var error), Is.True, error);

            Assert.That(observations, Is.EqualTo(new[] { "80:0", "80:1" }));
        }

        [Test]
        public void TryBuy_WhenBagIsFull_NotifiesSpendThenRefund()
        {
            var fixture = CreateFixture(capacity: 1);
            Assert.That(fixture.State.SetEvent(EventId), Is.True);
            Assert.That(fixture.Inventory.TryAdd(Item("filler.0", FillerId), out var addError), Is.True, addError);
            var observations = new List<string>();
            fixture.Inventory.Changed += () =>
                observations.Add($"{fixture.Inventory.Gold}:{fixture.Inventory.Items.Count}");

            Assert.That(fixture.Shop.TryBuy(ShopId, OfferId, out var error), Is.False);

            Assert.That(error, Is.EqualTo(InventoryTextKeys.BagFull));
            Assert.That(observations, Is.EqualTo(new[] { "80:1", "100:1" }));
        }

        [Test]
        public void TryBuy_WhenCalledTwice_OnlyFirstPurchaseChangesState()
        {
            var fixture = CreateFixture();
            Assert.That(fixture.State.SetEvent(EventId), Is.True);
            Assert.That(fixture.Shop.TryBuy(ShopId, OfferId, out var firstError), Is.True, firstError);
            var stateAfterFirstPurchase = fixture.State.Capture();
            var changesAfterFirstPurchase = 0;
            fixture.Inventory.Changed += () => changesAfterFirstPurchase++;

            Assert.That(fixture.Shop.TryBuy(ShopId, OfferId, out var secondError), Is.False);
            Assert.That(secondError, Is.EqualTo(NarrativeTextKeys.UnknownOffer));
            Assert.That(fixture.Inventory.Gold, Is.EqualTo(80));
            Assert.That(fixture.Inventory.Items.Count, Is.EqualTo(1));
            Assert.That(fixture.Shop.GetOffers(ShopId), Is.Empty);
            Assert.That(changesAfterFirstPurchase, Is.EqualTo(0));
            Assert.That(JToken.DeepEquals(stateAfterFirstPurchase, fixture.State.Capture()), Is.True);
        }

        [Test]
        public void TrySell_WithQuestAndEquippedItems_RejectsWithoutMutation()
        {
            var fixture = CreateFixture(startingGold: 0);
            Assert.That(fixture.Inventory.TryAdd(Item("quest.0", QuestItemId), out var questAddError), Is.True,
                questAddError);
            Assert.That(fixture.Inventory.TryAdd(Item("sword.0", SwordId), out var swordAddError), Is.True,
                swordAddError);
            Assert.That(fixture.Inventory.TryEquip("sword.0", "class.warrior", out var equipError), Is.True,
                equipError);

            Assert.That(fixture.Shop.TrySell("quest.0", out var questError), Is.False);
            Assert.That(questError, Is.EqualTo(InventoryTextKeys.QuestCannotSell));
            Assert.That(fixture.Shop.TrySell("sword.0", out var equippedError), Is.False);
            Assert.That(equippedError, Is.EqualTo(InventoryTextKeys.EquippedCannotSell));
            Assert.That(fixture.Inventory.Gold, Is.EqualTo(0));
            Assert.That(fixture.Inventory.GetItem("quest.0"), Is.Not.Null);
            Assert.That(fixture.Inventory.GetItem("sword.0"), Is.Not.Null);
        }

        [Test]
        public void TrySell_WithOrdinaryItem_AddsSellPriceAndRemovesInstance()
        {
            var fixture = CreateFixture(startingGold: 0);
            Assert.That(fixture.Inventory.TryAdd(Item("sword.0", SwordId), out var addError), Is.True, addError);

            Assert.That(fixture.Shop.TrySell("sword.0", out var error), Is.True, error);

            Assert.That(fixture.Inventory.Gold, Is.EqualTo(8));
            Assert.That(fixture.Inventory.Items, Is.Empty);
        }
        [Test]
        public void InvalidIdentifiers_ReturnStableLocalizedErrorKeys()
        {
            var fixture = CreateFixture();
            Assert.That(fixture.State.SetEvent(EventId), Is.True);

            Assert.That(fixture.Shop.TryBuy("shop.missing", OfferId, out var shopError), Is.False);
            Assert.That(shopError, Is.EqualTo(NarrativeTextKeys.UnknownShop));
            Assert.That(fixture.Shop.TryBuy(ShopId, "offer.missing", out var offerError), Is.False);
            Assert.That(offerError, Is.EqualTo(NarrativeTextKeys.UnknownOffer));
            Assert.That(fixture.Shop.TrySell("instance.missing", out var itemError), Is.False);
            Assert.That(itemError, Is.EqualTo(InventoryTextKeys.ItemMissing));
        }

        [Test]
        public void GetOffers_WithInvalidOfferCombination_HidesItAndRejectsPurchase()
        {
            var invalidOffer = Offer("offer.invalid", null, itemLevel: 0);
            var fixture = CreateFixture(additionalOffers: new[] { invalidOffer });
            Assert.That(fixture.State.SetEvent(EventId), Is.True);

            Assert.That(fixture.Shop.GetOffers(ShopId).Select(offer => offer.OfferId),
                Is.EqualTo(new[] { OfferId }));
            Assert.That(fixture.Shop.TryBuy(ShopId, invalidOffer.OfferId, out var error), Is.False);
            Assert.That(error, Is.EqualTo(NarrativeTextKeys.UnknownOffer));
            Assert.That(fixture.Inventory.Gold, Is.EqualTo(100));
        }

        [Test]
        public void Constructor_WithMultipleShopOwners_ThrowsExplicitError()
        {
            var error = Assert.Throws<InvalidOperationException>(() => CreateFixture(ownerCount: 2));

            Assert.That(error.Message, Does.Contain("exactly one owner"));
        }

        [Test]
        public void Constructor_WithoutShopOwner_ThrowsExplicitError()
        {
            var error = Assert.Throws<InvalidOperationException>(() => CreateFixture(ownerCount: 0));

            Assert.That(error.Message, Does.Contain("exactly one owner"));
        }

        private Fixture CreateFixture(
            int startingGold = 100,
            int capacity = 8,
            int baseValue = 200,
            int favorTier = 0,
            int ownerCount = 1,
            IEnumerable<ShopOfferDefinition> additionalOffers = null)
        {
            var sword = CreateItem(SwordId, baseValue, false);
            var questItem = CreateItem(QuestItemId, 100, true);
            var filler = CreateItem(FillerId, 10, false);
            var owners = new List<NpcDefinition>();
            for (var index = 0; index < ownerCount; index++)
            {
                var owner = Track(ScriptableObject.CreateInstance<NpcDefinition>());
                var ownerId = index == 0 ? MerchantId : MerchantId + "." + index;
                owner.EditorConfigure(ownerId, ownerId + ".name", string.Empty, ShopId, favorTier);
                owners.Add(owner);
            }

            var area = Track(ScriptableObject.CreateInstance<WorldAreaDefinition>());
            area.EditorConfigure(
                "area.village",
                new Rect(0f, 0f, 20f, 20f),
                Array.Empty<Rect>(),
                owners,
                Array.Empty<WorldEncounterDefinition>(),
                Array.Empty<WorldInteractableDefinition>(),
                Array.Empty<ItemDropTableDefinition>(),
                new[] { EventId },
                Array.Empty<string>());

            var offers = new List<ShopOfferDefinition> { Offer(OfferId, sword) };
            if (additionalOffers != null)
                offers.AddRange(additionalOffers);
            var shop = Track(ScriptableObject.CreateInstance<ShopDefinition>());
            shop.EditorConfigure(ShopId, "shop.general.name", EventId, offers);

            var definitions = new List<ContentDefinition> { sword, questItem, filler, area, shop };
            definitions.AddRange(owners);
            var definitionArray = definitions.ToArray();
            var items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal)
            {
                [SwordId] = sword,
                [QuestItemId] = questItem,
                [FillerId] = filler
            };
            var affixes = new Dictionary<string, AffixDefinition>(StringComparer.Ordinal);
            var state = new NarrativeStateService(definitionArray);
            var inventory = new InventoryService(capacity, items, startingGold);
            var economy = new EconomyService(inventory, items, affixes);
            var shopService = new ShopService(definitionArray, state, economy);
            return new Fixture(definitionArray, items, affixes, state, inventory, economy, shopService);
        }

        private ItemDefinition CreateItem(string id, int baseValue, bool isQuestItem)
        {
            var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
            item.EditorConfigure(
                id,
                id + ".name",
                ItemSlot.Weapon,
                Array.Empty<string>(),
                isQuestItem,
                baseValue,
                Array.Empty<StatValue>());
            return item;
        }

        private static ShopOfferDefinition Offer(string offerId, ItemDefinition item, int itemLevel = 1) =>
            new(offerId, item, ItemRarity.Common, itemLevel, Array.Empty<AffixDefinition>());

        private static ItemInstance Item(string instanceId, string definitionId) =>
            new(instanceId, definitionId, 1, ItemRarity.Common, Array.Empty<AffixInstance>());

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }

        private sealed class Fixture
        {
            public Fixture(
                ContentDefinition[] definitions,
                IReadOnlyDictionary<string, ItemDefinition> items,
                IReadOnlyDictionary<string, AffixDefinition> affixes,
                NarrativeStateService state,
                InventoryService inventory,
                EconomyService economy,
                ShopService shop)
            {
                Definitions = definitions;
                Items = items;
                Affixes = affixes;
                State = state;
                Inventory = inventory;
                Economy = economy;
                Shop = shop;
            }

            public ContentDefinition[] Definitions { get; }
            public IReadOnlyDictionary<string, ItemDefinition> Items { get; }
            public IReadOnlyDictionary<string, AffixDefinition> Affixes { get; }
            public NarrativeStateService State { get; }
            public InventoryService Inventory { get; }
            public EconomyService Economy { get; }
            public ShopService Shop { get; }
        }
    }
}
