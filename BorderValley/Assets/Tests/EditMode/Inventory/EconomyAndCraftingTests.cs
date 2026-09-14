using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core;
using BorderValley.Core.Boot;
using BorderValley.Core.Combat;
using BorderValley.Core.Persistence;
using BorderValley.Core.Random;
using BorderValley.Data.Items;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorderValley.Inventory.Tests
{
    public sealed class EconomyAndCraftingTests
    {
        private readonly List<Object> created = new();
        private readonly Dictionary<string, ItemDefinition> items = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AffixDefinition> affixes = new(StringComparer.Ordinal);

        [SetUp]
        public void SetUp()
        {
            AddItem("item.sword", 100, false);
            AddItem("item.boundary", 0, false);
            AddItem("item.axe", 200, false);
            AddItem("item.quest", 100, true);
            AddAffix("affix.power", minValue: 1, maxValue: 5);
            AddAffix("affix.crit", minValue: 1, maxValue: 5);
            AddAffix("affix.armor", minValue: 1, maxValue: 5);
            AddAffix("affix.speed", minValue: 1, maxValue: 5, mutuallyExclusive: "affix.armor");
            AddAffix("affix.armor", minValue: 1, maxValue: 5, mutuallyExclusive: "affix.speed");
            AddAffix("affix.epic.special", minRarity: ItemRarity.Epic,
                effectKind: AffixEffectKind.SkillModifier, minValue: 1, maxValue: 1);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                if (value != null)
                    Object.DestroyImmediate(value);
            created.Clear();
        }

        [Test]
        public void Prices_UsePrescribedFormulaAndSellIsLowerThanBuy()
        {
            var inventory = new InventoryService(10, items, 0);
            var economy = new EconomyService(inventory, items);
            var baseItem = Item("i1", "item.sword", ItemRarity.Common, 1);
            var affixed = Item("i2", "item.sword", ItemRarity.Common, 1,
                new AffixInstance("affix.power", 5));

            Assert.That(economy.GetBuyPrice(baseItem), Is.EqualTo(10));
            Assert.That(economy.GetSellPrice(baseItem), Is.EqualTo(4));
            Assert.That(economy.GetBuyPrice(affixed), Is.EqualTo(20));
            Assert.That(economy.GetSellPrice(affixed), Is.EqualTo(8));
            Assert.That(economy.GetBuyPrice(affixed), Is.GreaterThan(economy.GetSellPrice(affixed)));
        }

        [Test]
        public void GetPrices_AtMinimumBoundary_KeepBuyStrictlyAboveSell()
        {
            var economy = new EconomyService(new InventoryService(10, items, 0), items, affixes);
            var boundary = Item("boundary", "item.boundary", ItemRarity.Common, 1);

            Assert.That(economy.GetBuyPrice(boundary), Is.EqualTo(2));
            Assert.That(economy.GetSellPrice(boundary), Is.EqualTo(1));
            Assert.That(economy.GetBuyPrice(boundary), Is.GreaterThan(economy.GetSellPrice(boundary)));
        }
        [Test]
        public void GetBuyPrice_IsMonotonicAcrossBaseValueLevelRarityAndAffixValue()
        {
            var economy = new EconomyService(new InventoryService(10, items, 0), items);

            Assert.That(
                economy.GetBuyPrice(Item("base.low", "item.sword", ItemRarity.Common, 1)),
                Is.LessThan(economy.GetBuyPrice(Item("base.high", "item.axe", ItemRarity.Common, 1))));
            Assert.That(
                economy.GetBuyPrice(Item("level.low", "item.sword", ItemRarity.Common, 1)),
                Is.LessThan(economy.GetBuyPrice(Item("level.high", "item.sword", ItemRarity.Common, 2))));
            Assert.That(
                economy.GetBuyPrice(Item("rarity.low", "item.sword", ItemRarity.Common, 1)),
                Is.LessThan(economy.GetBuyPrice(Item("rarity.high", "item.sword", ItemRarity.Fine, 1))));
            Assert.That(
                economy.GetBuyPrice(Item("affix.low", "item.sword", ItemRarity.Common, 1)),
                Is.LessThan(economy.GetBuyPrice(Item("affix.high", "item.sword", ItemRarity.Common, 1,
                    new AffixInstance("affix.power", 5)))));
        }

        [Test]
        public void Sell_RejectsEquippedAndQuestItems()
        {
            var inventory = InventoryWithItem("i1", "item.sword");
            Assert.That(inventory.TryEquip("i1", "class.warrior", out _), Is.True);
            var economy = new EconomyService(inventory, items);

            Assert.That(economy.TrySell("i1", out var equippedError), Is.False);
            Assert.That(equippedError, Is.EqualTo(InventoryTextKeys.EquippedCannotSell));
            Assert.That(inventory.GetItem("i1"), Is.Not.Null);

            var questInventory = InventoryWithItem("q1", "item.quest");
            var questEconomy = new EconomyService(questInventory, items);
            Assert.That(questEconomy.TrySell("q1", out var questError), Is.False);
            Assert.That(questError, Is.EqualTo(InventoryTextKeys.QuestCannotSell));
            Assert.That(questInventory.GetItem("q1"), Is.Not.Null);
        }

        [Test]
        public void Sell_RemovesItemAndAddsSellPrice()
        {
            var inventory = InventoryWithItem("i1", "item.sword", startingGold: 100);
            var economy = new EconomyService(inventory, items);

            Assert.That(economy.TrySell("i1", out var error), Is.True);
            Assert.That(error, Is.Empty);
            Assert.That(inventory.Items, Is.Empty);
            Assert.That(inventory.Gold, Is.EqualTo(104));
        }

        [Test]
        public void Buy_WithInsufficientGold_DoesNotMutateInventory()
        {
            var inventory = new InventoryService(10, items, 9);
            var economy = new EconomyService(inventory, items);

            Assert.That(economy.TryBuy(Item("i1", "item.sword", ItemRarity.Common, 1), out var error), Is.False);
            Assert.That(error, Is.EqualTo(InventoryTextKeys.NotEnoughGold));
            Assert.That(inventory.Gold, Is.EqualTo(9));
            Assert.That(inventory.Items, Is.Empty);
        }

        [Test]
        public void Buy_WhenInventoryAddFails_RefundsGold()
        {
            var inventory = new InventoryService(1, items, 100);
            Assert.That(inventory.TryAdd(Item("existing", "item.axe", ItemRarity.Common, 1), out _), Is.True);
            var economy = new EconomyService(inventory, items);

            Assert.That(economy.TryBuy(Item("i1", "item.sword", ItemRarity.Common, 1), out var error), Is.False);
            Assert.That(error, Is.EqualTo(InventoryTextKeys.BagFull));
            Assert.That(inventory.Gold, Is.EqualTo(100));
            Assert.That(inventory.Items.Select(value => value.InstanceId), Is.EqualTo(new[] { "existing" }));
        }

        [Test]
        public void Buy_WithValidItem_SpendsGoldAndAddsItem()
        {
            var inventory = new InventoryService(10, items, 100);
            var economy = new EconomyService(inventory, items, affixes);
            var item = Item("i1", "item.sword", ItemRarity.Fine, 1,
                new AffixInstance("affix.power", 1));

            Assert.That(economy.TryBuy(item, out var error), Is.True);
            Assert.That(error, Is.Empty);
            Assert.That(inventory.Gold, Is.EqualTo(78));
            Assert.That(inventory.GetItem("i1"), Is.SameAs(item));
        }

        [Test]
        public void Buy_RejectsCommonItemWithAffixes()
        {
            AssertRejectedBuy(Item("i1", "item.sword", ItemRarity.Common, 1,
                new AffixInstance("affix.power", 1)));
        }

        [Test]
        public void Buy_RejectsEpicItemWithTooFewAffixes()
        {
            AssertRejectedBuy(Item("i1", "item.sword", ItemRarity.Epic, 1,
                new AffixInstance("affix.power", 1),
                new AffixInstance("affix.armor", 1)));
        }

        [Test]
        public void Buy_RejectsDuplicateAffixes()
        {
            AssertRejectedBuy(Item("i1", "item.sword", ItemRarity.Rare, 1,
                new AffixInstance("affix.power", 1),
                new AffixInstance("affix.power", 2)));
        }

        [Test]
        public void Buy_RejectsMutuallyExclusiveAffixes()
        {
            AssertRejectedBuy(Item("i1", "item.sword", ItemRarity.Rare, 1,
                new AffixInstance("affix.speed", 1),
                new AffixInstance("affix.armor", 1)));
        }

        [Test]
        public void Buy_RejectsAffixesThatFailSlotRarityOrItemLevelEligibility()
        {
            var offhand = AddAffix("affix.offhand", slot: ItemSlot.Offhand);
            var highLevel = AddAffix("affix.high.level", minItemLevel: 5);

            AssertRejectedBuy(Item("slot", "item.sword", ItemRarity.Rare, 1,
                new AffixInstance("affix.power", 1),
                new AffixInstance(offhand.Id, 1)));
            AssertRejectedBuy(Item("rarity", "item.sword", ItemRarity.Rare, 1,
                new AffixInstance("affix.power", 1),
                new AffixInstance("affix.epic.special", 1)));
            AssertRejectedBuy(Item("level", "item.sword", ItemRarity.Rare, 1,
                new AffixInstance("affix.power", 1),
                new AffixInstance(highLevel.Id, 1)));
        }

        [Test]
        public void Buy_RejectsEpicItemWithoutRequiredSpecialAffix()
        {
            AssertRejectedBuy(Item("i1", "item.sword", ItemRarity.Epic, 1,
                new AffixInstance("affix.power", 1),
                new AffixInstance("affix.armor", 1),
                new AffixInstance("affix.crit", 1)));
        }

        [Test]
        public void Buy_RejectsAffixValueOutsideDefinitionRange()
        {
            AssertRejectedBuy(Item("i1", "item.sword", ItemRarity.Rare, 1,
                new AffixInstance("affix.power", 99),
                new AffixInstance("affix.armor", 1)));
        }

        [Test]
        public void Buy_RejectsAffixSetThatExceedsRarityBudget()
        {
            var expensive = AddAffix("affix.expensive", minValue: 1, maxValue: 30);
            AssertRejectedBuy(Item("i1", "item.sword", ItemRarity.Rare, 1,
                new AffixInstance(expensive.Id, 24),
                new AffixInstance("affix.armor", 1)));
        }
        [Test]
        public void CraftingCosts_UsePrescribedValues()
        {
            var costs = new CraftingCosts();
            var item = Item("i1", "item.sword", ItemRarity.Rare, 5);

            Assert.That(costs.CraftGold(5, ItemRarity.Rare), Is.EqualTo(105));
            Assert.That(costs.CraftMaterial(5, ItemRarity.Rare), Is.EqualTo(5));
            Assert.That(costs.DismantleMaterial(item), Is.EqualTo(4));
            Assert.That(costs.ReforgeGold(item, false), Is.EqualTo(80));
            Assert.That(costs.ReforgeGold(item, true), Is.EqualTo(140));
            Assert.That(costs.ReforgeMaterial(item, false), Is.EqualTo(3));
            Assert.That(costs.ReforgeMaterial(item, true), Is.EqualTo(6));
        }

        [Test]
        public void Craft_WithInsufficientMaterials_DoesNotSpendGold()
        {
            var inventory = new InventoryService(10, items, 100);
            var crafting = new CraftingService(inventory, items, affixes, Generator(), new CraftingCosts());

            Assert.That(crafting.Craft("craft.1", items["item.sword"], "class.warrior", 3,
                new FixedRandomSource(2)).Success, Is.False);
            Assert.That(inventory.Gold, Is.EqualTo(100));
            Assert.That(inventory.Items, Is.Empty);
        }

        [Test]
        public void Craft_WithInsufficientGold_DoesNotSpendMaterials()
        {
            var inventory = new InventoryService(10, items, 1);
            inventory.AddMaterial("material.ore", 10);
            var crafting = new CraftingService(inventory, items, affixes, Generator(), new CraftingCosts());

            Assert.That(crafting.Craft("craft.1", items["item.sword"], "class.warrior", 3,
                new FixedRandomSource(2)).Success, Is.False);
            Assert.That(inventory.Gold, Is.EqualTo(1));
            Assert.That(inventory.Materials["material.ore"], Is.EqualTo(10));
        }

        [Test]
        public void Craft_Success_SpendsCostsAndAddsDeterministicGeneratedItem()
        {
            var inventory = new InventoryService(10, items, 1000);
            inventory.AddMaterial("material.ore", 10);
            var crafting = new CraftingService(inventory, items, affixes, Generator(), new CraftingCosts());

            var result = crafting.Craft("craft.1", items["item.sword"], "class.warrior", 3,
                new FixedRandomSource(2));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Error, Is.Empty);
            Assert.That(result.Item, Is.Not.Null);
            Assert.That(result.Item.InstanceId, Is.EqualTo("craft.1"));
            Assert.That(result.Item.ItemDefinitionId, Is.EqualTo("item.sword"));
            Assert.That(result.Item.ItemLevel, Is.EqualTo(3));
            Assert.That(result.Item.Rarity, Is.EqualTo(ItemRarity.Rare));
            Assert.That(result.Item.Affixes, Has.Count.EqualTo(2));
            Assert.That(inventory.Gold, Is.EqualTo(905));
            Assert.That(inventory.Materials["material.ore"], Is.EqualTo(5));
            Assert.That(inventory.GetItem("craft.1"), Is.SameAs(result.Item));
        }

        [Test]
        public void Craft_WithItemLevelAboveTen_NormalizesBeforeCostAndGeneration()
        {
            var inventory = new InventoryService(10, items, 1000);
            inventory.AddMaterial("material.ore", 10);
            var crafting = new CraftingService(inventory, items, affixes, Generator(), new CraftingCosts());

            var result = crafting.Craft("craft.1", items["item.sword"], "class.warrior", 25,
                new FixedRandomSource(2));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Item.ItemLevel, Is.EqualTo(10));
            Assert.That(result.GoldCost, Is.EqualTo(130));
            Assert.That(result.MaterialCost, Is.EqualTo(7));
            Assert.That(inventory.Gold, Is.EqualTo(870));
            Assert.That(inventory.Materials["material.ore"], Is.EqualTo(3));
        }
        [Test]
        public void Craft_WhenGenerationFails_RollsBackGoldAndMaterials()
        {
            var inventory = new InventoryService(10, items, 1000);
            inventory.AddMaterial("material.ore", 10);
            var limitedAffixes = new Dictionary<string, AffixDefinition>(StringComparer.Ordinal)
            {
                ["affix.power"] = affixes["affix.power"]
            };
            var crafting = new CraftingService(
                inventory,
                items,
                limitedAffixes,
                new LootGenerator(items, limitedAffixes),
                new CraftingCosts());

            var result = crafting.Craft("craft.1", items["item.sword"], "class.warrior", 3,
                new FixedRandomSource(2));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(InventoryTextKeys.CraftingFailed));
            Assert.That(inventory.Gold, Is.EqualTo(1000));
            Assert.That(inventory.Materials["material.ore"], Is.EqualTo(10));
            Assert.That(inventory.Items, Is.Empty);
        }

        [Test]
        public void Craft_RejectsClassRestrictedItemBeforeSpending()
        {
            var inventory = new InventoryService(10, items, 1000);
            inventory.AddMaterial("material.ore", 10);
            var crafting = new CraftingService(inventory, items, affixes, Generator(), new CraftingCosts());

            var result = crafting.Craft("craft.1", items["item.sword"], "class.mage", 3,
                new FixedRandomSource(2));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(InventoryTextKeys.ClassRestricted));
            Assert.That(inventory.Gold, Is.EqualTo(1000));
            Assert.That(inventory.Materials["material.ore"], Is.EqualTo(10));
        }

        [Test]
        public void Dismantle_RejectsEquippedAndQuestItems()
        {
            var equippedInventory = InventoryWithItem("i1", "item.sword", ItemRarity.Fine);
            Assert.That(equippedInventory.TryEquip("i1", "class.warrior", out _), Is.True);
            var crafting = CreateCrafting(equippedInventory);

            var equippedResult = crafting.Dismantle("i1");
            Assert.That(equippedResult.Success, Is.False);
            Assert.That(equippedResult.Error, Is.EqualTo(InventoryTextKeys.EquippedCannotDismantle));
            Assert.That(equippedInventory.GetItem("i1"), Is.Not.Null);

            var questInventory = InventoryWithItem("q1", "item.quest", ItemRarity.Fine);
            var questResult = CreateCrafting(questInventory).Dismantle("q1");
            Assert.That(questResult.Success, Is.False);
            Assert.That(questResult.Error, Is.EqualTo(InventoryTextKeys.QuestCannotDismantle));
            Assert.That(questInventory.GetItem("q1"), Is.Not.Null);
        }

        [Test]
        public void Dismantle_RareItem_RemovesItAndAwardsOreAndEssence()
        {
            var inventory = InventoryWithItem("i1", "item.sword", ItemRarity.Rare, 3);
            var crafting = CreateCrafting(inventory);

            var result = crafting.Dismantle("i1");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Error, Is.Empty);
            Assert.That(result.MaterialCost, Is.EqualTo(4));
            Assert.That(inventory.Items, Is.Empty);
            Assert.That(inventory.Materials["material.ore"], Is.EqualTo(4));
            Assert.That(inventory.Materials["material.essence"], Is.EqualTo(1));
        }

        [Test]
        public void Dismantle_FineItem_DoesNotAwardEssence()
        {
            var inventory = InventoryWithItem("i1", "item.sword", ItemRarity.Fine, 2);
            var crafting = CreateCrafting(inventory);

            Assert.That(crafting.Dismantle("i1").Success, Is.True);
            Assert.That(inventory.Materials.TryGetValue("material.essence", out _), Is.False);
        }

        [Test]
        public void Reforge_WithOneLockedAffix_PreservesLockedAffixAndRerollsOthers()
        {
            var inventory = InventoryWithItem("i1", "item.sword", ItemRarity.Rare, 3, 1000,
                new AffixInstance("affix.power", 4), new AffixInstance("affix.armor", 3));
            inventory.AddMaterial("material.ore", 10);
            var crafting = CreateCrafting(inventory);

            var result = crafting.Reforge("i1", "affix.power", new FixedRandomSource(0));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Error, Is.Empty);
            Assert.That(result.GoldCost, Is.EqualTo(132));
            Assert.That(result.MaterialCost, Is.EqualTo(6));
            Assert.That(inventory.Gold, Is.EqualTo(868));
            Assert.That(inventory.Materials["material.ore"], Is.EqualTo(4));
            Assert.That(inventory.GetItem("i1").Affixes, Has.Count.EqualTo(2));
            Assert.That(inventory.GetItem("i1").Affixes.Any(value =>
                value.AffixId == "affix.power" && value.Value == 4), Is.True);
        }

        [Test]
        public void Reforge_RejectsEquippedItemWithoutSpending()
        {
            var inventory = InventoryWithItem("i1", "item.sword", ItemRarity.Rare, 3, 1000,
                new AffixInstance("affix.power", 4), new AffixInstance("affix.armor", 3));
            inventory.AddMaterial("material.ore", 10);
            Assert.That(inventory.TryEquip("i1", "class.warrior", out _), Is.True);
            var crafting = CreateCrafting(inventory);

            var result = crafting.Reforge("i1", "affix.power", new FixedRandomSource(0));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(InventoryTextKeys.EquippedCannotReforge));
            Assert.That(inventory.Gold, Is.EqualTo(1000));
            Assert.That(inventory.Materials["material.ore"], Is.EqualTo(10));
        }

        [Test]
        public void Reforge_WithUnknownLockedAffix_DoesNotSpend()
        {
            var inventory = InventoryWithItem("i1", "item.sword", ItemRarity.Rare, 3, 1000,
                new AffixInstance("affix.power", 4), new AffixInstance("affix.armor", 3));
            inventory.AddMaterial("material.ore", 10);
            var crafting = CreateCrafting(inventory);

            var result = crafting.Reforge("i1", "affix.missing", new FixedRandomSource(0));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(InventoryTextKeys.LockedAffixNotFound));
            Assert.That(inventory.Gold, Is.EqualTo(1000));
            Assert.That(inventory.Materials["material.ore"], Is.EqualTo(10));
        }

        [Test]
        public void Reforge_WhenAffixesConflict_RollsBackAndPreservesOriginalItem()
        {
            var power = AddAffix("affix.conflict.power", mutuallyExclusive: "affix.conflict.armor");
            var armor = AddAffix("affix.conflict.armor", mutuallyExclusive: "affix.conflict.power");
            var conflictAffixes = new Dictionary<string, AffixDefinition>(StringComparer.Ordinal)
            {
                [power.Id] = power,
                [armor.Id] = armor
            };
            var inventory = InventoryWithItem("i1", "item.sword", ItemRarity.Rare, 3, 1000,
                new AffixInstance("affix.conflict.power", 4),
                new AffixInstance("affix.conflict.armor", 3));
            inventory.AddMaterial("material.ore", 10);
            var crafting = new CraftingService(
                inventory,
                items,
                conflictAffixes,
                new LootGenerator(items, conflictAffixes),
                new CraftingCosts());

            var result = crafting.Reforge("i1", "affix.conflict.power", new FixedRandomSource(0));

            Assert.That(result.Success, Is.False);
            Assert.That(inventory.Gold, Is.EqualTo(1000));
            Assert.That(inventory.Materials["material.ore"], Is.EqualTo(10));
            Assert.That(inventory.GetItem("i1").Affixes.Select(value => (value.AffixId, value.Value)),
                Is.EqualTo(new[]
                {
                    ("affix.conflict.power", 4),
                    ("affix.conflict.armor", 3)
                }));
        }

        [Test]
        public void Reforge_WhenLockedOrGeneratedAffixesExceedBudget_RollsBack()
        {
            var inventory = InventoryWithItem("i1", "item.sword", ItemRarity.Rare, 3, 1000,
                new AffixInstance("affix.power", 24), new AffixInstance("affix.armor", 1));
            inventory.AddMaterial("material.ore", 10);
            var crafting = CreateCrafting(inventory);

            var result = crafting.Reforge("i1", "affix.power", new FixedRandomSource(0));

            Assert.That(result.Success, Is.False);
            Assert.That(inventory.Gold, Is.EqualTo(1000));
            Assert.That(inventory.Materials["material.ore"], Is.EqualTo(10));
            Assert.That(inventory.GetItem("i1").Affixes.Select(value => (value.AffixId, value.Value)),
                Is.EqualTo(new[] { ("affix.power", 24), ("affix.armor", 1) }));
        }

        [Test]
        public void Reforge_WithEligibilityFailure_DoesNotMutateOrSpend()
        {
            var highLevel = AddAffix("affix.high.level", minItemLevel: 5);
            var localAffixes = new Dictionary<string, AffixDefinition>(StringComparer.Ordinal)
            {
                [highLevel.Id] = highLevel
            };
            var inventory = InventoryWithItem("i1", "item.sword", ItemRarity.Fine, 1, 1000,
                new AffixInstance("affix.high.level", 1));
            inventory.AddMaterial("material.ore", 10);
            var crafting = new CraftingService(
                inventory,
                items,
                localAffixes,
                new LootGenerator(items, localAffixes),
                new CraftingCosts());

            var result = crafting.Reforge("i1", "affix.high.level", new FixedRandomSource(0));

            Assert.That(result.Success, Is.False);
            Assert.That(inventory.Gold, Is.EqualTo(1000));
            Assert.That(inventory.Materials["material.ore"], Is.EqualTo(10));
            Assert.That(inventory.GetItem("i1").Affixes.Single().AffixId, Is.EqualTo("affix.high.level"));
        }

        [Test]
        public void Reforge_WithSameSeedAndInputs_IsDeterministic()
        {
            var first = InventoryWithItem("i1", "item.sword", ItemRarity.Rare, 3, 1000,
                new AffixInstance("affix.power", 4), new AffixInstance("affix.armor", 3));
            var second = InventoryWithItem("i1", "item.sword", ItemRarity.Rare, 3, 1000,
                new AffixInstance("affix.power", 4), new AffixInstance("affix.armor", 3));
            first.AddMaterial("material.ore", 10);
            second.AddMaterial("material.ore", 10);

            Assert.That(CreateCrafting(first).Reforge("i1", "affix.power",
                RandomSourceFactory.FromSeed("reforge")).Success, Is.True);
            Assert.That(CreateCrafting(second).Reforge("i1", "affix.power",
                RandomSourceFactory.FromSeed("reforge")).Success, Is.True);

            CollectionAssert.AreEqual(
                first.GetItem("i1").Affixes.Select(value => (value.AffixId, value.Value)),
                second.GetItem("i1").Affixes.Select(value => (value.AffixId, value.Value)));
        }

        [Test]
        public void Reforge_WithLockOnCommonItem_RejectsMalformedItemWithoutSpending()
        {
            var inventory = InventoryWithItem("i1", "item.sword", ItemRarity.Common, 1, 1000,
                new AffixInstance("affix.power", 4));
            inventory.AddMaterial("material.ore", 10);
            var crafting = CreateCrafting(inventory);

            var result = crafting.Reforge("i1", "affix.power", new FixedRandomSource(0));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(InventoryTextKeys.ReforgeFailed));
            Assert.That(inventory.Gold, Is.EqualTo(1000));
            Assert.That(inventory.Materials["material.ore"], Is.EqualTo(10));
            Assert.That(inventory.GetItem("i1").Affixes.Single().AffixId, Is.EqualTo("affix.power"));
        }
        [Test]
        public void Installer_RegistersLootEconomyAndCraftingWithoutSaveParticipants()
        {
            var host = Track(new GameObject("installer-test"));
            var installer = host.AddComponent<InventoryBootstrapInstaller>();
            var context = new GameContext();
            var participants = new List<ISaveParticipant>();

            installer.Install(context, participants);

            Assert.That(context.TryGet(out LootGenerator _), Is.True);
            Assert.That(context.TryGet(out CraftingCosts _), Is.True);
            Assert.That(context.TryGet(out EconomyService _), Is.True);
            Assert.That(context.TryGet(out CraftingService _), Is.True);
            Assert.That(participants.OfType<EconomyService>(), Is.Empty);
            Assert.That(participants.OfType<CraftingService>(), Is.Empty);
        }

        private void AssertRejectedBuy(ItemInstance item)
        {
            var inventory = new InventoryService(10, items, 100);
            var economy = new EconomyService(inventory, items, affixes);

            Assert.That(economy.TryBuy(item, out var error), Is.False);
            Assert.That(error, Is.EqualTo(InventoryTextKeys.InvalidItem));
            Assert.That(inventory.Gold, Is.EqualTo(100));
            Assert.That(inventory.Items, Is.Empty);
        }
        private ItemDefinition AddItem(string id, int baseValue, bool isQuestItem)
        {
            var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
            item.EditorConfigure(
                id,
                id + ".name",
                ItemSlot.Weapon,
                new[] { "class.warrior" },
                isQuestItem,
                baseValue,
                Array.Empty<StatValue>());
            items[id] = item;
            return item;
        }

        private AffixDefinition AddAffix(
            string id,
            ItemRarity minRarity = ItemRarity.Common,
            AffixEffectKind effectKind = AffixEffectKind.FlatStat,
            int minValue = 1,
            int maxValue = 5,
            int minItemLevel = 1,
            string mutuallyExclusive = null,
            ItemSlot slot = ItemSlot.Weapon)
        {
            var affix = Track(ScriptableObject.CreateInstance<AffixDefinition>());
            affix.EditorConfigure(
                id,
                id + ".name",
                new[] { slot },
                minRarity,
                effectKind,
                CombatStat.Power,
                effectKind == AffixEffectKind.SkillModifier ? SkillModifierKind.Radius : default,
                default,
                minValue,
                maxValue,
                minItemLevel,
                0,
                1,
                1,
                string.IsNullOrWhiteSpace(mutuallyExclusive)
                    ? Array.Empty<string>()
                    : new[] { mutuallyExclusive });
            affixes[id] = affix;
            return affix;
        }

        private LootGenerator Generator() => new(items, affixes);

        private CraftingService CreateCrafting(InventoryService inventory) =>
            new(inventory, items, affixes, Generator(), new CraftingCosts());

        private InventoryService InventoryWithItem(
            string instanceId,
            string definitionId,
            ItemRarity rarity = ItemRarity.Common,
            int itemLevel = 1,
            int startingGold = 1000,
            params AffixInstance[] affixValues)
        {
            var inventory = new InventoryService(10, items, startingGold);
            Assert.That(inventory.TryAdd(Item(instanceId, definitionId, rarity, itemLevel, affixValues), out var error), Is.True,
                error);
            return inventory;
        }

        private static ItemInstance Item(
            string instanceId,
            string definitionId,
            ItemRarity rarity,
            int itemLevel,
            params AffixInstance[] affixValues) =>
            new(instanceId, definitionId, itemLevel, rarity, affixValues);

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }

        private sealed class FixedRandomSource : IRandomSource
        {
            private readonly int value;

            public FixedRandomSource(int value)
            {
                this.value = value;
            }

            public uint NextUInt() => (uint)value;

            public int Range(int minInclusive, int maxExclusive) =>
                Math.Clamp(value, minInclusive, maxExclusive - 1);

            public float Value01() => 0f;

            public IRandomSource Fork(string label) => this;
        }
    }
}
