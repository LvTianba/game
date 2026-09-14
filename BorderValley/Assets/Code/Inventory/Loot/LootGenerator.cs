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
            var rarity = RollRarity(table, item, itemLevel, local);

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

            var count = ItemRules.AffixCount(rarity);

            var eligible = affixes.Values
                .Where(affix => ItemRules.IsEligible(affix, item.Slot, rarity, itemLevel))
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

            var requiresSpecial = rarity == ItemRarity.Epic && eligible.Any(ItemRules.IsSpecial);
            var chosen = new List<AffixDefinition>(count);
            if (TrySelectAffixes(
                    eligible,
                    count,
                    requiresSpecial,
                    ItemRules.Budget(rarity),
                    chosen,
                    random))
                return chosen.AsReadOnly();

            throw new InvalidOperationException(
                $"Unable to select {count} affixes for {rarity} from the eligible pool.");
        }

        private static bool TrySelectAffixes(
            IReadOnlyList<AffixDefinition> eligible,
            int count,
            bool requiresSpecial,
            int budget,
            List<AffixDefinition> chosen,
            IRandomSource random)
        {
            if (chosen.Sum(affix => ItemRules.ValueCost(affix.MinValue, affix)) > budget)
                return false;
            if (chosen.Count == count)
                return !requiresSpecial || chosen.Any(ItemRules.IsSpecial);

            var remaining = count - chosen.Count;
            var candidates = eligible
                .Where(candidate => chosen.All(existing => !ItemRules.Conflicts(existing, candidate)))
                .ToArray();
            if (candidates.Length < remaining) return false;
            if (requiresSpecial && !chosen.Any(ItemRules.IsSpecial) && !candidates.Any(ItemRules.IsSpecial))
                return false;

            foreach (var candidate in WeightedOrder(candidates, random))
            {
                chosen.Add(candidate);
                if (TrySelectAffixes(
                    eligible,
                    count,
                    requiresSpecial,
                    budget,
                    chosen,
                    random))
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

        private ItemDefinition ResolveItem(ItemDefinition item)
        {
            if (item == null) throw new InvalidOperationException("Loot entry has no item.");
            return items.TryGetValue(item.Id, out var definition) ? definition : item;
        }

        private ItemRarity RollRarity(
            ItemDropTableDefinition table,
            ItemDefinition item,
            int itemLevel,
            IRandomSource random)
        {
            var feasible = (table.RarityWeights ?? Array.Empty<RarityWeight>())
                .Where(entry => entry.Weight > 0 && HasFeasibleAffixSet(item, itemLevel, entry.Rarity))
                .ToArray();
            if (feasible.Length == 0)
                throw new InvalidOperationException(
                    $"No feasible rarity exists for {item.Id} at item level {itemLevel}.");

            return Weighted(feasible, entry => entry.Weight, entry => entry.Rarity, random);
        }

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

        public IReadOnlyList<string> ValidateContentMatrix(ItemDropTableDefinition table = null)
        {
            var issues = new List<string>();
            var definitions = items.Values
                .Where(definition => definition != null)
                .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                .ToArray();

            var representedSlots = definitions.Select(definition => definition.Slot).ToHashSet();
            foreach (ItemSlot slot in Enum.GetValues(typeof(ItemSlot)))
                if (!representedSlots.Contains(slot))
                    issues.Add($"No item definition covers slot {slot}.");

            foreach (var itemDefinition in definitions)
            foreach (var level in Enumerable.Range(1, PartyProgressionService.MaxLevel))
            foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
                if (!HasFeasibleAffixSet(itemDefinition, level, rarity))
                    issues.Add($"{itemDefinition.Id} cannot produce {rarity} affixes at level {level}.");

            if (table != null)
            {
                var entries = table.Entries ?? Array.Empty<LootEntry>();
                if (entries.Length == 0)
                    issues.Add($"Drop table {table.Id} has no entries.");
                var dropIds = entries
                    .Where(entry => entry?.Item != null)
                    .Select(entry => entry.Item.Id)
                    .ToHashSet(StringComparer.Ordinal);
                foreach (var itemDefinition in definitions)
                    if (!dropIds.Contains(itemDefinition.Id))
                        issues.Add($"Drop table {table.Id} does not contain {itemDefinition.Id}.");
                foreach (var weight in table.RarityWeights ?? Array.Empty<RarityWeight>())
                    if (weight.Weight <= 0)
                        issues.Add($"Drop table {table.Id} has a non-positive {weight.Rarity} weight.");
            }

            return issues.AsReadOnly();
        }

        private bool HasFeasibleAffixSet(ItemDefinition item, int itemLevel, ItemRarity rarity)
        {
            if (item == null) return false;
            var count = ItemRules.AffixCount(rarity);
            if (count == 0) return true;

            var eligible = affixes.Values
                .Where(affix => ItemRules.IsEligible(affix, item.Slot, rarity, itemLevel))
                .OrderBy(affix => affix.Id, StringComparer.Ordinal)
                .ToArray();
            var chosen = new List<AffixDefinition>(count);
            if (!TrySelectFeasibleAffixes(
                    eligible,
                    count,
                    rarity == ItemRarity.Epic,
                    ItemRules.Budget(rarity),
                    chosen))
                return false;

            return chosen.Sum(affix => ItemRules.ValueCost(affix.MinValue, affix)) <= ItemRules.Budget(rarity);
        }

        private static bool TrySelectFeasibleAffixes(
            IReadOnlyList<AffixDefinition> eligible,
            int count,
            bool requiresSpecial,
            int budget,
            List<AffixDefinition> chosen)
        {
            if (chosen.Sum(affix => ItemRules.ValueCost(affix.MinValue, affix)) > budget)
                return false;
            if (chosen.Count == count)
                return !requiresSpecial || chosen.Any(ItemRules.IsSpecial);

            var remaining = count - chosen.Count;
            var candidates = eligible
                .Where(candidate => chosen.All(existing => !ItemRules.Conflicts(existing, candidate)))
                .ToArray();
            if (candidates.Length < remaining) return false;
            if (requiresSpecial && !chosen.Any(ItemRules.IsSpecial) && !candidates.Any(ItemRules.IsSpecial))
                return false;

            foreach (var candidate in candidates)
            {
                chosen.Add(candidate);
                if (TrySelectFeasibleAffixes(eligible, count, requiresSpecial, budget, chosen))
                    return true;
                chosen.RemoveAt(chosen.Count - 1);
            }

            return false;
        }

        private static IReadOnlyList<AffixInstance> ApplyBudget(
            IReadOnlyList<AffixDefinition> chosen,
            ItemRarity rarity,
            IRandomSource random)
        {
            var remaining = ItemRules.Budget(rarity);
            var result = new List<AffixInstance>(chosen.Count);
            for (var index = 0; index < chosen.Count; index++)
            {
                var affix = chosen[index];
                var reserved = 0;
                for (var later = index + 1; later < chosen.Count; later++)
                    reserved += ItemRules.ValueCost(chosen[later].MinValue, chosen[later]);

                var available = remaining - reserved;
                var value = random.Range(affix.MinValue, affix.MaxValue + 1);
                while (value != affix.MinValue && ItemRules.ValueCost(value, affix) > available)
                    value += value > affix.MinValue ? -1 : 1;

                var cost = ItemRules.ValueCost(value, affix);
                if (cost > available)
                    throw new InvalidOperationException($"Affix budget exceeded by {affix.Id}.");
                remaining -= cost;
                result.Add(new AffixInstance(affix.Id, value));
            }

            return result.AsReadOnly();
        }
    }
}