using System.Linq;
using BorderValley.Core.Combat;
using BorderValley.Core.Persistence;
using BorderValley.Data.Items;
using NUnit.Framework;
using UnityEngine;

namespace BorderValley.Inventory.Tests
{
    public sealed class InventoryServiceTests
    {
        [Test]
        public void TryEquip_ReplacesExistingItemAndReturnsItToBag()
        {
            var definitions = Definitions();
            var service = new InventoryService(2, definitions, 100);
            var sword = Item("i1", "item.sword", ItemRarity.Common);
            var axe = Item("i2", "item.axe", ItemRarity.Common);
            Assert.That(service.TryAdd(sword, out _), Is.True);
            Assert.That(service.TryAdd(axe, out _), Is.True);

            Assert.That(service.TryEquip("i1", "class.warrior", out _), Is.True);
            Assert.That(service.TryEquip("i2", "class.warrior", out _), Is.True);

            Assert.That(service.Equipped[ItemSlot.Weapon], Is.EqualTo("i2"));
            Assert.That(service.Items.Any(item => item.InstanceId == "i1"), Is.True);
        }

        [Test]
        public void SaveParticipant_RestoresItemsEquipmentGoldAndMaterials()
        {
            var root = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "BorderValleyTests", System.Guid.NewGuid().ToString("N"));
            var service = new InventoryService(3, Definitions(), 25);
            service.TryAdd(Item("i1", "item.sword", ItemRarity.Fine), out _);
            service.TryEquip("i1", "class.warrior", out _);
            service.TryAddMaterial("material.ore", 4);
            var save = new SaveService(root, new ISaveParticipant[] { service });
            save.Save(0, "World");

            var restored = new InventoryService(3, Definitions(), 0);
            new SaveService(root, new ISaveParticipant[] { restored }).Load(0);

            Assert.That(restored.Gold, Is.EqualTo(25));
            Assert.That(restored.Items.Single().InstanceId, Is.EqualTo("i1"));
            Assert.That(restored.Equipped[ItemSlot.Weapon], Is.EqualTo("i1"));
            Assert.That(restored.Materials["material.ore"], Is.EqualTo(4));
            System.IO.Directory.Delete(root, true);
        }

        [Test]
        public void TryAddMaterial_RejectsBlankIdsAndNonPositiveAmounts()
        {
            var service = new InventoryService(3, Definitions(), 0);

            Assert.That(service.TryAddMaterial(string.Empty, 1), Is.False);
            Assert.That(service.TryAddMaterial("material.ore", 0), Is.False);
            Assert.That(service.TryAddMaterial("material.ore", 2), Is.True);
            Assert.That(service.TryAddMaterial("material.ore", 3), Is.True);
            Assert.That(service.Materials["material.ore"], Is.EqualTo(5));
        }

        [Test]
        public void InventoryQuery_FiltersByDefinitionEquipmentAndAffixKind()
        {
            var definitions = Definitions();
            var affixes = AffixDefinitions();
            var source = new[]
            {
                Item("i1", "item.sword", ItemRarity.Fine, 1, new AffixInstance("affix.flat", 1)),
                Item("i2", "item.axe", ItemRarity.Fine, 1, new AffixInstance("affix.percent", 5)),
                Item("i3", "item.sword", ItemRarity.Rare, 2, new AffixInstance("affix.percent", 5)),
                Item("i4", "item.sword", ItemRarity.Fine, 2, new AffixInstance("affix.flat", 2))
            };

            var result = InventoryQuery.Apply(
                source,
                definitions,
                new InventoryFilter
                {
                    Slot = ItemSlot.Weapon,
                    Rarity = ItemRarity.Fine,
                    Equipped = false,
                    AffixKind = AffixEffectKind.PercentStat
                },
                InventorySort.ItemLevelThenName,
                instanceId => instanceId == "i1",
                affixes);

            Assert.That(result.Select(item => item.InstanceId), Is.EqualTo(new[] { "i2" }));
            Assert.That(source, Has.Length.EqualTo(4));
        }

        [Test]
        public void InventoryQuery_SortsByItemLevelThenName()
        {
            var source = new[]
            {
                Item("i1", "item.axe", ItemRarity.Common, 2),
                Item("i2", "item.sword", ItemRarity.Common, 2),
                Item("i3", "item.sword", ItemRarity.Common, 1)
            };

            var result = InventoryQuery.Apply(
                source,
                Definitions(),
                new InventoryFilter(),
                InventorySort.ItemLevelThenName);

            Assert.That(result.Select(item => item.InstanceId), Is.EqualTo(new[] { "i3", "i1", "i2" }));
        }

        [Test]
        public void InventoryQuery_ReturnsEmptyArrayForNoMatches()
        {
            var result = InventoryQuery.Apply(
                new[] { Item("i1", "item.sword", ItemRarity.Common) },
                Definitions(),
                new InventoryFilter { Slot = ItemSlot.Offhand },
                InventorySort.SlotThenRarity);

            Assert.That(result, Is.Empty);
            Assert.That(result.GetType(), Is.EqualTo(typeof(ItemInstance[])));
        }

        private static System.Collections.Generic.Dictionary<string, ItemDefinition> Definitions()
        {
            var sword = ScriptableObject.CreateInstance<ItemDefinition>();
            sword.EditorConfigure("item.sword", "item.sword.name", ItemSlot.Weapon,
                new[] { "class.warrior" }, false, 20, System.Array.Empty<StatValue>());
            var axe = ScriptableObject.CreateInstance<ItemDefinition>();
            axe.EditorConfigure("item.axe", "item.axe.name", ItemSlot.Weapon,
                new[] { "class.warrior" }, false, 20, System.Array.Empty<StatValue>());
            return new System.Collections.Generic.Dictionary<string, ItemDefinition>
            {
                [sword.Id] = sword,
                [axe.Id] = axe
            };
        }

        private static System.Collections.Generic.Dictionary<string, AffixDefinition> AffixDefinitions()
        {
            var flat = ScriptableObject.CreateInstance<AffixDefinition>();
            flat.EditorConfigure("affix.flat", "affix.flat.name", new[] { ItemSlot.Weapon },
                ItemRarity.Common, AffixEffectKind.FlatStat, CombatStat.Power,
                default, default, 1, 1, 1, 0, "budget.flat", System.Array.Empty<string>());
            var percent = ScriptableObject.CreateInstance<AffixDefinition>();
            percent.EditorConfigure("affix.percent", "affix.percent.name", new[] { ItemSlot.Weapon },
                ItemRarity.Common, AffixEffectKind.PercentStat, CombatStat.Power,
                default, default, 1, 1, 1, 0, "budget.percent", System.Array.Empty<string>());
            return new System.Collections.Generic.Dictionary<string, AffixDefinition>
            {
                [flat.Id] = flat,
                [percent.Id] = percent
            };
        }

        private static ItemInstance Item(
            string id,
            string definitionId,
            ItemRarity rarity,
            int itemLevel = 1,
            params AffixInstance[] affixes) =>
            new(id, definitionId, itemLevel, rarity, affixes);
    }
}
