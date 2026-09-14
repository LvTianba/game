using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Combat;
using BorderValley.Data;
using BorderValley.Core.Random;
using BorderValley.Data.Items;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorderValley.Inventory.Tests
{
    public sealed class LootGeneratorTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                if (value != null)
                    Object.DestroyImmediate(value);
            created.Clear();
        }

        [Test]
        public void Generate_WithSameSeedAndInstanceId_IsReproducible()
        {
            var generator = Generator();
            var first = generator.Generate("drop.1", Table(), 5, RandomSourceFactory.FromSeed("seed"));
            var second = generator.Generate("drop.1", Table(), 5, RandomSourceFactory.FromSeed("seed"));

            Assert.That(second.ItemDefinitionId, Is.EqualTo(first.ItemDefinitionId));
            Assert.That(second.Rarity, Is.EqualTo(first.Rarity));
            Assert.That(second.ItemLevel, Is.EqualTo(first.ItemLevel));
            CollectionAssert.AreEqual(
                first.Affixes.Select(value => (value.AffixId, value.Value)),
                second.Affixes.Select(value => (value.AffixId, value.Value)));
        }

        [Test]
        public void Generate_EpicAlwaysContainsSkillOrTriggerAffix()
        {
            var generator = Generator(Item(ItemRarity.Common), Item(ItemRarity.Epic));
            var table = EpicTable();
            for (var seed = 0; seed < 100; seed++)
            {
                var item = generator.Generate(
                    $"drop.{seed}",
                    table,
                    10,
                    RandomSourceFactory.FromSeed($"seed.{seed}"));
                if (item.Rarity != ItemRarity.Epic) continue;
                Assert.That(item.Affixes.Any(affix =>
                    affix.AffixId == "affix.skill.radius" || affix.AffixId == "affix.trigger.slow"), Is.True);
            }
        }

        [Test]
        public void Generate_NeverRepeatsOrSelectsMutuallyExclusiveAffixes()
        {
            var item = Generator().Generate("drop", Table(), 10, RandomSourceFactory.FromSeed("seed"));
            Assert.That(item.Affixes.Select(affix => affix.AffixId).Distinct().Count(), Is.EqualTo(item.Affixes.Count));
            Assert.That(item.Affixes.Any(affix => affix.AffixId == "affix.speed") &&
                        item.Affixes.Any(affix => affix.AffixId == "affix.armor"), Is.False);
        }

        [Test]
        public void Generate_UsesPlayerLevelPlusZeroToTwoAndClampsToTableBounds()
        {
            var random = new RecordingRandomSource(2);
            var table = Table(ItemRarity.Rare, minItemLevel: 1, maxItemLevel: 3);

            var item = Generator().Generate("drop", table, 5, random);

            Assert.That(random.Ranges.Any(range => range.Min == 0 && range.Max == 3), Is.True);
            Assert.That(item.ItemLevel, Is.EqualTo(3));
        }

        [Test]
        public void Generate_WithZeroRarityWeight_Throws()
        {
            var generator = Generator();
            var table = Table(ItemRarity.Common, rarityWeight: 0);

            Assert.Throws<InvalidOperationException>(
                () => generator.Generate("drop", table, 5, RandomSourceFactory.FromSeed("seed")));
        }
        [Test]
        public void GenerateForItem_QualityBudgetIsNeverExceeded()
        {
            var itemDefinition = Item(ItemRarity.Epic);
            var generator = Generator(itemDefinition);

            for (var seed = 0; seed < 100; seed++)
            {
                var item = generator.GenerateForItem(
                    $"crafted.{seed}",
                    itemDefinition,
                    10,
                    ItemRarity.Epic,
                    RandomSourceFactory.FromSeed($"budget.{seed}"));
                var spent = item.Affixes.Sum(affix => Math.Max(1, Math.Abs(affix.Value)));

                Assert.That(item.Affixes, Has.Count.EqualTo(3));
                Assert.That(spent, Is.LessThanOrEqualTo(40));
                Assert.That(item.Affixes.All(affix => affix.Value >= 4 && affix.Value <= 15), Is.True);
            }
        }

        [Test]
        public void GenerateForItem_ShippedContentMatrix_ProducesEveryRarityExactly()
        {
            var catalog = Resources.Load<ContentCatalog>("ContentCatalog");
            Assert.That(catalog, Is.Not.Null);
            var itemDefinitions = catalog.All.OfType<ItemDefinition>()
                .ToDictionary(value => value.Id, StringComparer.Ordinal);
            var affixDefinitions = catalog.All.OfType<AffixDefinition>()
                .ToDictionary(value => value.Id, StringComparer.Ordinal);
            var dropTable = catalog.All.OfType<ItemDropTableDefinition>()
                .Single(value => value.Id == "loot.bandit.core");
            var generator = new LootGenerator(itemDefinitions, affixDefinitions);

            Assert.That(
                itemDefinitions.Values.Select(value => value.Slot).Distinct(),
                Is.EquivalentTo(Enum.GetValues(typeof(ItemSlot)).Cast<ItemSlot>()));
            Assert.That(
                dropTable.Entries.Select(entry => entry.Item.Id),
                Is.SupersetOf(itemDefinitions.Keys));

            foreach (var itemDefinition in itemDefinitions.Values)
            foreach (var level in Enumerable.Range(1, PartyProgressionService.MaxLevel))
            foreach (var rarity in Enum.GetValues(typeof(ItemRarity)).Cast<ItemRarity>())
            {
                ItemInstance generated;
                try
                {
                    generated = generator.GenerateForItem(
                        $"matrix.{itemDefinition.Id}.{level}.{rarity}",
                        itemDefinition,
                        level,
                        rarity,
                        RandomSourceFactory.FromSeed($"matrix.{itemDefinition.Id}.{level}.{rarity}"));
                }
                catch (InvalidOperationException exception)
                {
                    Assert.Fail($"{itemDefinition.Id} level {level} {rarity}: {exception.Message}");
                    return;
                }

                Assert.That(
                    generated.Affixes,
                    Has.Count.EqualTo(ItemRules.AffixCount(rarity)),
                    $"{itemDefinition.Id} level {level} {rarity}");
                if (rarity == ItemRarity.Epic)
                {
                    Assert.That(
                        generated.Affixes.Any(value =>
                            ItemRules.IsSpecial(affixDefinitions[value.AffixId])),
                        Is.True,
                        $"{itemDefinition.Id} level {level} Epic must include a special affix");
                }
            }
        }
        [TestCase(ItemRarity.Rare)]
        [TestCase(ItemRarity.Epic)]
        public void GenerateForItem_WhenExactAffixCountCannotBeMet_Throws(ItemRarity rarity)
        {
            var item = Item(ItemRarity.Epic);
            var first = Affix("affix.first", ItemRarity.Common, AffixEffectKind.FlatStat, "affix.second");
            var second = Affix("affix.second", ItemRarity.Common, AffixEffectKind.FlatStat, "affix.first");
            var generator = GeneratorWithAffixes(item, first, second);

            Assert.Throws<InvalidOperationException>(() => generator.GenerateForItem(
                "drop", item, 10, rarity, RandomSourceFactory.FromSeed("seed")));
        }

        [Test]
        public void GenerateForItem_EpicBacktracksToCompatibleSpecial()
        {
            var item = Item(ItemRarity.Epic);
            var first = Affix("affix.a", ItemRarity.Common, AffixEffectKind.FlatStat);
            var second = Affix("affix.b", ItemRarity.Common, AffixEffectKind.FlatStat);
            var third = Affix("affix.c", ItemRarity.Common, AffixEffectKind.FlatStat);
            var fourth = Affix("affix.d", ItemRarity.Common, AffixEffectKind.FlatStat);
            var special = Affix(
                "affix.special",
                ItemRarity.Epic,
                AffixEffectKind.SkillModifier,
                "affix.a",
                "affix.b");
            var generator = GeneratorWithAffixes(item, first, second, third, fourth, special);

            var result = generator.GenerateForItem(
                "drop",
                item,
                10,
                ItemRarity.Epic,
                new RecordingRandomSource(0));

            Assert.That(result.Affixes, Has.Count.EqualTo(3));
            Assert.That(result.Affixes.Any(affix => affix.AffixId == "affix.special"), Is.True);
        }

        [TestCase(ItemRarity.Common, 0)]
        [TestCase(ItemRarity.Fine, 1)]
        [TestCase(ItemRarity.Rare, 2)]
        [TestCase(ItemRarity.Epic, 3)]
        public void GenerateForItem_ReturnsExplicitAffixCountForRarity(ItemRarity rarity, int expectedCount)
        {
            var item = Item(ItemRarity.Epic);
            var generator = GeneratorWithAffixes(
                item,
                Affix("affix.a", ItemRarity.Common, AffixEffectKind.FlatStat),
                Affix("affix.b", ItemRarity.Common, AffixEffectKind.FlatStat),
                Affix("affix.c", ItemRarity.Common, AffixEffectKind.FlatStat),
                Affix("affix.special", ItemRarity.Epic, AffixEffectKind.Trigger));

            var result = generator.GenerateForItem(
                "drop",
                item,
                10,
                rarity,
                RandomSourceFactory.FromSeed("count"));

            Assert.That(result.Affixes, Has.Count.EqualTo(expectedCount));
        }

        [Test]
        public void GenerateForItem_FiltersBySlotMinimumRarityAndItemLevel()
        {
            var item = Item(ItemRarity.Epic);
            var eligible = Affix(
                "affix.eligible",
                ItemRarity.Fine,
                AffixEffectKind.FlatStat,
                ItemSlot.Weapon,
                10);
            var wrongSlot = Affix(
                "affix.wrong-slot",
                ItemRarity.Fine,
                AffixEffectKind.FlatStat,
                ItemSlot.Offhand,
                1);
            var wrongRarity = Affix(
                "affix.wrong-rarity",
                ItemRarity.Epic,
                AffixEffectKind.FlatStat,
                ItemSlot.Weapon,
                1);
            var wrongLevel = Affix(
                "affix.wrong-level",
                ItemRarity.Fine,
                AffixEffectKind.FlatStat,
                ItemSlot.Weapon,
                11);
            var generator = GeneratorWithAffixes(item, eligible, wrongSlot, wrongRarity, wrongLevel);

            var result = generator.GenerateForItem(
                "drop",
                item,
                10,
                ItemRarity.Fine,
                RandomSourceFactory.FromSeed("filter"));

            Assert.That(result.Affixes.Single().AffixId, Is.EqualTo("affix.eligible"));
        }
        private LootGenerator GeneratorWithAffixes(ItemDefinition item, params AffixDefinition[] definitions)
        {
            return new LootGenerator(
                new Dictionary<string, ItemDefinition> { [item.Id] = item },
                definitions.ToDictionary(value => value.Id, StringComparer.Ordinal));
        }
        private LootGenerator Generator(params ItemDefinition[] items)
        {
            var definitions = items.Length == 0
                ? new[] { Item(ItemRarity.Common), Item(ItemRarity.Epic) }
                : items;
            var itemById = definitions.ToDictionary(value => value.Id, StringComparer.Ordinal);
            var affixById = new[]
            {
                Affix("affix.flat", ItemRarity.Common, AffixEffectKind.FlatStat),
                Affix("affix.percent", ItemRarity.Common, AffixEffectKind.PercentStat),
                Affix("affix.speed", ItemRarity.Common, AffixEffectKind.FlatStat, "affix.armor"),
                Affix("affix.armor", ItemRarity.Common, AffixEffectKind.FlatStat, "affix.speed"),
                Affix("affix.skill.radius", ItemRarity.Epic, AffixEffectKind.SkillModifier),
                Affix("affix.trigger.slow", ItemRarity.Epic, AffixEffectKind.Trigger)
            }.ToDictionary(value => value.Id, StringComparer.Ordinal);
            return new LootGenerator(itemById, affixById);
        }

        private ItemDefinition Item(ItemRarity rarity)
        {
            var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
            item.EditorConfigure(
                $"item.{rarity.ToString().ToLowerInvariant()}",
                $"item.{rarity.ToString().ToLowerInvariant()}.name",
                ItemSlot.Weapon,
                Array.Empty<string>(),
                false,
                10,
                Array.Empty<StatValue>());
            return item;
        }

        private AffixDefinition Affix(
            string id,
            ItemRarity minimumRarity,
            AffixEffectKind effectKind,
            params string[] mutuallyExclusive) =>
            Affix(id, minimumRarity, effectKind, ItemSlot.Weapon, 1, mutuallyExclusive);

        private AffixDefinition Affix(
            string id,
            ItemRarity minimumRarity,
            AffixEffectKind effectKind,
            ItemSlot slot,
            int minItemLevel,
            params string[] mutuallyExclusive)
        {
            var affix = Track(ScriptableObject.CreateInstance<AffixDefinition>());
            affix.EditorConfigure(
                id,
                id + ".name",
                new[] { slot },
                minimumRarity,
                effectKind,
                CombatStat.Power,
                effectKind == AffixEffectKind.SkillModifier ? SkillModifierKind.Radius : default,
                effectKind == AffixEffectKind.Trigger ? PassiveEffectKind.OnAttackApplySlow : default,
                4,
                15,
                minItemLevel,
                0,
                1,
                1,
                mutuallyExclusive);
            return affix;
        }
        private ItemDropTableDefinition Table(
            ItemRarity rarity = ItemRarity.Rare,
            int minItemLevel = 1,
            int maxItemLevel = 10,
            int rarityWeight = 1)
        {
            var table = Track(ScriptableObject.CreateInstance<ItemDropTableDefinition>());
            table.EditorConfigure(
                "loot.table",
                minItemLevel,
                maxItemLevel,
                new[]
                {
                    new LootEntry(Item(ItemRarity.Common), 1),
                    new LootEntry(Item(ItemRarity.Epic), 1)
                },
                new[] { new RarityWeight(rarity, rarityWeight) });
            return table;
        }

        private ItemDropTableDefinition EpicTable()
        {
            var table = Track(ScriptableObject.CreateInstance<ItemDropTableDefinition>());
            table.EditorConfigure(
                "loot.epic",
                1,
                10,
                new[]
                {
                    new LootEntry(Item(ItemRarity.Common), 1),
                    new LootEntry(Item(ItemRarity.Epic), 1)
                },
                new[] { new RarityWeight(ItemRarity.Epic, 1) });
            return table;
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }

        private sealed class RecordingRandomSource : IRandomSource
        {
            private readonly int result;
            private readonly List<(int Min, int Max)> ranges;

            public RecordingRandomSource(int result)
                : this(result, new List<(int Min, int Max)>())
            {
            }

            private RecordingRandomSource(int result, List<(int Min, int Max)> ranges)
            {
                this.result = result;
                this.ranges = ranges;
            }

            public List<(int Min, int Max)> Ranges => ranges;

            public uint NextUInt() => 0;

            public int Range(int minInclusive, int maxExclusive)
            {
                ranges.Add((minInclusive, maxExclusive));
                return result;
            }

            public float Value01() => 0f;

            public IRandomSource Fork(string label) => new RecordingRandomSource(result, ranges);
        }
    }
}
