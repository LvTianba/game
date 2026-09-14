using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Combat;
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
            params string[] mutuallyExclusive)
        {
            var affix = Track(ScriptableObject.CreateInstance<AffixDefinition>());
            affix.EditorConfigure(
                id,
                id + ".name",
                new[] { ItemSlot.Weapon },
                minimumRarity,
                effectKind,
                CombatStat.Power,
                effectKind == AffixEffectKind.SkillModifier ? SkillModifierKind.Radius : default,
                effectKind == AffixEffectKind.Trigger ? PassiveEffectKind.OnAttackApplySlow : default,
                4,
                15,
                1,
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