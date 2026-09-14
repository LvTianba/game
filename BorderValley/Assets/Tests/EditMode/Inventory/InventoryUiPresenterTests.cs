using System;
using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Combat;
using BorderValley.Core.Random;
using BorderValley.Data.Items;
using BorderValley.UI.Inventory;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorderValley.Inventory.Tests
{
    public sealed class InventoryUiPresenterTests
    {
        private readonly List<Object> created = new();
        private readonly Dictionary<string, ItemDefinition> items = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AffixDefinition> affixes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CharacterDefinition> characters = new(StringComparer.Ordinal);
        private InventoryService service;
        private CraftingService crafting;
        private PartyProgressionService progression;

        [SetUp]
        public void SetUp()
        {
            AddItem("item.sword", ItemSlot.Weapon, new[] { "class.warrior" }, 4);
            AddItem("item.staff", ItemSlot.Weapon, new[] { "class.mage" }, 4);
            AddItem("item.boots", ItemSlot.Boots, Array.Empty<string>(), 2);
            AddAffix("affix.flat_power", AffixEffectKind.FlatStat, CombatStat.Power, default, default, 1, 4);
            AddAffix("affix.crit", AffixEffectKind.PercentStat, CombatStat.CritChanceBps, default, default, 1, 5);
            AddAffix("affix.skill.radius", AffixEffectKind.SkillModifier, CombatStat.Power, SkillModifierKind.Radius, default, 1, 1, "skill.whirlwind");
            AddAffix("affix.trigger.slow", AffixEffectKind.Trigger, CombatStat.Power, default, PassiveEffectKind.OnAttackApplySlow, 1, 1);
            AddCharacter("class.warrior", CombatStat.MaxHealth, 24, CombatStat.Power, 8);
            AddCharacter("class.mage", CombatStat.MaxHealth, 14, CombatStat.Power, 5);

            service = new InventoryService(30, items, 1000);
            service.TryAddMaterial(CraftingService.OreMaterialId, 100);
            service.TryAdd(Item("warrior.item", "item.sword", ItemRarity.Common), out _);
            service.TryAdd(Item(
                "mage.item",
                "item.staff",
                ItemRarity.Rare,
                new AffixInstance("affix.flat_power", 2),
                new AffixInstance("affix.crit", 3)), out _);
            service.TryAdd(Item("boots.item", "item.boots", ItemRarity.Common), out _);

            progression = new PartyProgressionService(
                characters,
                new[] { new PartyMemberState("player.warrior", "class.warrior", 1, 0, 0, 1, 0) });
            crafting = new CraftingService(
                service,
                items,
                affixes,
                new LootGenerator(items, affixes),
                new CraftingCosts());
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
        public void SetFilter_AndSort_UsesServiceDataWithoutMutatingInventory()
        {
            var presenter = Presenter();
            presenter.SetFilter(new InventoryFilter { Rarity = ItemRarity.Rare });
            presenter.SetSort(InventorySort.ItemLevelThenName);

            Assert.That(presenter.VisibleItems.All(item => item.Rarity == ItemRarity.Rare), Is.True);
            Assert.That(presenter.VisibleItems.Single().InstanceId, Is.EqualTo("mage.item"));
            Assert.That(service.Items.Count, Is.EqualTo(3));
        }

        [Test]
        public void EquipSelected_WhenClassRestricted_ExposesLocalizedErrorKey()
        {
            var presenter = Presenter();
            presenter.SetSelectedForTests("mage.item");

            Assert.That(presenter.EquipSelected("class.warrior"), Is.False);
            Assert.That(presenter.LastErrorKey, Is.EqualTo(InventoryTextKeys.ClassRestricted));
        }

        [Test]
        public void EquipSelected_WhenSuccessful_ClearsErrorAndMarksItemEquipped()
        {
            var presenter = Presenter();
            presenter.Select("warrior.item");
            presenter.SetFilter(new InventoryFilter { Rarity = ItemRarity.Rare });
            Assert.That(presenter.EquipSelected("class.warrior"), Is.True);

            Assert.That(presenter.LastErrorKey, Is.Empty);
            Assert.That(service.IsEquipped("warrior.item"), Is.True);
        }

        [Test]
        public void Unequip_WhenMissing_ExposesLocalizedErrorKey()
        {
            var presenter = Presenter();
            Assert.That(presenter.Unequip(ItemSlot.Weapon), Is.False);
            Assert.That(presenter.LastErrorKey, Is.EqualTo(InventoryTextKeys.SlotEmpty));
        }

        [Test]
        public void DismantleSelected_WhenSuccessful_RemovesItemAndAddsMaterial()
        {
            var presenter = Presenter();
            presenter.Select("boots.item");
            var before = service.Materials[CraftingService.OreMaterialId];

            Assert.That(presenter.DismantleSelected(), Is.True);
            Assert.That(service.Items.Any(item => item.InstanceId == "boots.item"), Is.False);
            Assert.That(service.Materials[CraftingService.OreMaterialId], Is.GreaterThan(before));
            Assert.That(presenter.LastErrorKey, Is.Empty);
        }

        [Test]
        public void ReforgeSelected_WhenSuccessful_PreservesLockedAffix()
        {
            var presenter = Presenter();
            presenter.Select("mage.item");

            Assert.That(
                presenter.ReforgeSelected("affix.flat_power", RandomSourceFactory.FromSeed("reforge")),
                Is.True);
            Assert.That(
                service.GetItem("mage.item").Affixes.Any(affix => affix.AffixId == "affix.flat_power"),
                Is.True);
        }

        [Test]
        public void Craft_WhenMaterialAvailable_AddsItemAndInvokesSaveCallback()
        {
            var saves = 0;
            var presenter = Presenter(_ => saves++);
            Assert.That(
                presenter.Craft(
                    "crafted.item",
                    "item.sword",
                    "class.warrior",
                    2,
                    RandomSourceFactory.FromSeed("craft")),
                Is.True);

            Assert.That(service.GetItem("crafted.item"), Is.Not.Null);
            Assert.That(saves, Is.EqualTo(1));
            Assert.That(presenter.LastErrorKey, Is.Empty);
        }

        [Test]
        public void Craft_WhenAutosaveFails_ExposesDirtyStateAndCanRetry()
        {
            var attempts = 0;
            var presenter = PresenterWithSave(_ => attempts++ > 0);

            Assert.That(
                presenter.Craft(
                    "crafted.retry",
                    "item.sword",
                    "class.warrior",
                    1,
                    RandomSourceFactory.FromSeed("craft.retry")),
                Is.False);
            Assert.That(presenter.HasUnsavedChanges, Is.True);
            Assert.That(presenter.LastErrorKey, Is.EqualTo(InventoryTextKeys.AutoSaveFailed));

            Assert.That(presenter.RetrySave(), Is.True);
            Assert.That(presenter.HasUnsavedChanges, Is.False);
            Assert.That(presenter.LastErrorKey, Is.Empty);
            Assert.That(attempts, Is.EqualTo(2));
        }

        [Test]
        public void RestParty_RecoversHealthAndManaButCapsAtMaximum()
        {
            var presenter = Presenter();
            presenter.RestParty(10);
            var member = progression.Members.Single();

            Assert.That(member.CurrentHealth, Is.EqualTo(11));
            Assert.That(member.CurrentMana, Is.EqualTo(8));
            Assert.That(presenter.LastErrorKey, Is.Empty);
        }

        private InventoryUiPresenter Presenter(Action<string> save = null) =>
            new(service, crafting, progression, affixes, save);

        private InventoryUiPresenter PresenterWithSave(Func<string, bool> save) =>
            new(service, crafting, progression, null, null, affixes, save);

        private static ItemInstance Item(
            string instanceId,
            string definitionId,
            ItemRarity rarity,
            params AffixInstance[] values) =>
            new(instanceId, definitionId, 1, rarity, values);

        private void AddItem(
            string id,
            ItemSlot slot,
            IEnumerable<string> classes,
            int power)
        {
            var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
            item.EditorConfigure(
                id,
                id + ".name",
                slot,
                classes,
                false,
                100,
                new[] { new StatValue(CombatStat.Power, power) });
            items[id] = item;
        }

        private void AddAffix(
            string id,
            AffixEffectKind kind,
            CombatStat stat,
            SkillModifierKind modifier,
            PassiveEffectKind passive,
            int min,
            int max,
            string targetSkillId = null)
        {
            var affix = Track(ScriptableObject.CreateInstance<AffixDefinition>());
            affix.EditorConfigure(
                id,
                id + ".name",
                new[] { ItemSlot.Weapon, ItemSlot.Boots },
                ItemRarity.Common,
                kind,
                stat,
                modifier,
                passive,
                min,
                max,
                1,
                kind == AffixEffectKind.Trigger ? 2 : 0,
                1,
                1,
                Array.Empty<string>());
            if (!string.IsNullOrWhiteSpace(targetSkillId))
                affix.EditorSetTargetSkillId(targetSkillId);
            affixes[id] = affix;
        }

        private void AddCharacter(
            string id,
            CombatStat firstStat,
            int firstValue,
            CombatStat secondStat,
            int secondValue)
        {
            var character = Track(ScriptableObject.CreateInstance<CharacterDefinition>());
            character.EditorConfigure(
                id,
                id + ".name",
                new[]
                {
                    new StatValue(firstStat, firstValue),
                    new StatValue(secondStat, secondValue),
                    new StatValue(CombatStat.MaxMana, 8)
                },
                new[] { new StatValue(CombatStat.Power, 1) },
                new[] { "skill.basic" },
                new[] { new SkillUnlock("skill.whirlwind", 2) });
            characters[id] = character;
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }
    }
}
