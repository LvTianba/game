using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Random;
using BorderValley.Data.Items;

namespace BorderValley.Inventory
{
    public sealed class LootGenerator
    {
        private readonly IReadOnlyDictionary<string, ItemDefinition> items;
        private readonly IReadOnlyDictionary<string, AffixDefinition> affixes;

        public LootGenerator(
            IReadOnlyDictionary<string, ItemDefinition> items,
            IReadOnlyDictionary<string, AffixDefinition> affixes)
        {
            this.items = items ?? throw new ArgumentNullException(nameof(items));
            this.affixes = affixes ?? throw new ArgumentNullException(nameof(affixes));
        }

        public ItemInstance Generate(
            string instanceId,
            ItemDropTableDefinition table,
            int playerLevel,
            IRandomSource random)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var local = random.Fork("loot:" + instanceId + ":" + table.Id);
            var item = Weighted(
                table.Entries ?? Array.Empty<LootEntry>(),
                entry => entry.Weight,
                entry => ResolveItem(entry.Item),
                local);
            var itemLevel = Math.Clamp(
                playerLevel + local.Range(0, 3),
                table.MinItemLevel,
                table.MaxItemLevel);
            var rarity = RollRarity(table, local);

            return GenerateForItem(instanceId, item, itemLevel, rarity, local.Fork("affixes"));
        }

        public ItemInstance GenerateForItem(
            string instanceId,
            ItemDefinition item,
            int itemLevel,
            ItemRarity rarity,
            IRandomSource random)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var count = rarity switch
            {
                ItemRarity.Common => 0,
                ItemRarity.Fine => 1,
                ItemRarity.Rare => 2,
                ItemRarity.Epic => 3,
                _ => throw new ArgumentOutOfRangeException(nameof(rarity))
            };

            var chosen = new List<AffixDefinition>();
            var eligible = affixes.Values
                .Where(affix => affix != null && affix.Supports(item.Slot, rarity, itemLevel))
                .OrderBy(affix => affix.Id, StringComparer.Ordinal)
                .ToArray();

            for (var index = 0; index < count; index++)
            {
                var candidates = eligible
                    .Where(candidate => chosen.All(value => !Conflicts(value, candidate)))
                    .ToArray();
                if (candidates.Length == 0) break;
                chosen.Add(Weighted(candidates, value => value.Weight, value => value, random));
            }

            if (rarity == ItemRarity.Epic &&
                !chosen.Any(value => value.EffectKind is AffixEffectKind.SkillModifier or AffixEffectKind.Trigger))
            {
                var retained = chosen.Count == 0
                    ? Array.Empty<AffixDefinition>()
                    : chosen.Take(chosen.Count - 1).ToArray();
                var special = eligible
                    .Where(value => value.EffectKind is AffixEffectKind.SkillModifier or AffixEffectKind.Trigger)
                    .Where(value => retained.All(existing => !Conflicts(existing, value)))
                    .OrderBy(value => value.Id, StringComparer.Ordinal)
                    .ToArray();
                if (special.Length > 0)
                {
                    var value = Weighted(special, candidate => candidate.Weight, candidate => candidate, random);
                    if (chosen.Count == 0)
                        chosen.Add(value);
                    else
                        chosen[chosen.Count - 1] = value;
                }
            }

            return new ItemInstance(instanceId, item.Id, itemLevel, rarity, ApplyBudget(chosen, rarity, random));
        }

        private ItemDefinition ResolveItem(ItemDefinition item)
        {
            if (item == null) throw new InvalidOperationException("Loot entry has no item.");
            return items.TryGetValue(item.Id, out var definition) ? definition : item;
        }

        private static bool Conflicts(AffixDefinition existing, AffixDefinition candidate) =>
            existing.Id == candidate.Id ||
            existing.IsMutuallyExclusive(candidate.Id) ||
            candidate.IsMutuallyExclusive(existing.Id);

        private static ItemRarity RollRarity(ItemDropTableDefinition table, IRandomSource random) =>
            Weighted(
                table.RarityWeights ?? Array.Empty<RarityWeight>(),
                entry => entry.Weight,
                entry => entry.Rarity,
                random);

        private static TResult Weighted<TSource, TResult>(
            IEnumerable<TSource> values,
            Func<TSource, int> weight,
            Func<TSource, TResult> select,
            IRandomSource random)
        {
            var rows = values.Select(value => new
                {
                    Value = value,
                    Key = select(value),
                    Weight = weight(value)
                })
                .Where(row => row.Weight > 0)
                .ToArray();
            var total = rows.Sum(row => row.Weight);
            if (total <= 0) throw new InvalidOperationException("Weighted table has no positive entries.");

            var roll = random.Range(0, total);
            foreach (var row in rows)
            {
                if (roll < row.Weight) return row.Key;
                roll -= row.Weight;
            }

            return rows[rows.Length - 1].Key;
        }

        private static IReadOnlyList<AffixInstance> ApplyBudget(
            IReadOnlyList<AffixDefinition> chosen,
            ItemRarity rarity,
            IRandomSource random)
        {
            var remaining = rarity switch
            {
                ItemRarity.Common => 0,
                ItemRarity.Fine => 12,
                ItemRarity.Rare => 24,
                ItemRarity.Epic => 40,
                _ => throw new ArgumentOutOfRangeException(nameof(rarity))
            };
            var result = new List<AffixInstance>(chosen.Count);
            foreach (var affix in chosen)
            {
                var value = random.Range(affix.MinValue, affix.MaxValue + 1);
                while (value != affix.MinValue && Cost(value, affix) > remaining)
                    value += value > affix.MinValue ? -1 : 1;

                var cost = Cost(value, affix);
                if (cost > remaining)
                    throw new InvalidOperationException($"Affix budget exceeded by {affix.Id}.");
                remaining -= cost;
                result.Add(new AffixInstance(affix.Id, value));
            }

            return result.AsReadOnly();
        }

        private static int Cost(int value, AffixDefinition affix) =>
            Math.Max(1, Math.Abs(value)) * affix.BudgetCost;
    }
}