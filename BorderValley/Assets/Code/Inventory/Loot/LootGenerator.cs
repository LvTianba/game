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

            var eligible = affixes.Values
                .Where(affix => affix != null && affix.Weight > 0 &&
                                affix.Supports(item.Slot, rarity, itemLevel))
                .OrderBy(affix => affix.Id, StringComparer.Ordinal)
                .ToArray();
            var chosen = SelectAffixes(eligible, count, rarity, random);

            return new ItemInstance(instanceId, item.Id, itemLevel, rarity, ApplyBudget(chosen, rarity, random));
        }

        private static IReadOnlyList<AffixDefinition> SelectAffixes(
            IReadOnlyList<AffixDefinition> eligible,
            int count,
            ItemRarity rarity,
            IRandomSource random)
        {
            if (count == 0) return Array.Empty<AffixDefinition>();

            var requiresSpecial = rarity == ItemRarity.Epic && eligible.Any(IsSpecial);
            var chosen = new List<AffixDefinition>(count);
            if (TrySelectAffixes(eligible, count, requiresSpecial, chosen, random))
                return chosen.AsReadOnly();

            throw new InvalidOperationException(
                $"Unable to select {count} affixes for {rarity} from the eligible pool.");
        }

        private static bool TrySelectAffixes(
            IReadOnlyList<AffixDefinition> eligible,
            int count,
            bool requiresSpecial,
            List<AffixDefinition> chosen,
            IRandomSource random)
        {
            if (chosen.Count == count)
                return !requiresSpecial || chosen.Any(IsSpecial);

            var remaining = count - chosen.Count;
            var candidates = eligible
                .Where(candidate => chosen.All(existing => !Conflicts(existing, candidate)))
                .ToArray();
            if (candidates.Length < remaining) return false;
            if (requiresSpecial && !chosen.Any(IsSpecial) && !candidates.Any(IsSpecial))
                return false;

            foreach (var candidate in WeightedOrder(candidates, random))
            {
                chosen.Add(candidate);
                if (TrySelectAffixes(eligible, count, requiresSpecial, chosen, random))
                    return true;
                chosen.RemoveAt(chosen.Count - 1);
            }

            return false;
        }

        private static IEnumerable<AffixDefinition> WeightedOrder(
            IReadOnlyList<AffixDefinition> candidates,
            IRandomSource random)
        {
            var remaining = new List<AffixDefinition>(candidates);
            while (remaining.Count > 0)
            {
                var value = Weighted(remaining, candidate => candidate.Weight, candidate => candidate, random);
                yield return value;
                remaining.Remove(value);
            }
        }

        private static bool IsSpecial(AffixDefinition affix) =>
            affix.EffectKind is AffixEffectKind.SkillModifier or AffixEffectKind.Trigger;
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