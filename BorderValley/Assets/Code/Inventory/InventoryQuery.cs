using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Data.Items;

namespace BorderValley.Inventory
{
    public static class InventoryQuery
    {
        public static IReadOnlyList<ItemInstance> Apply(
            IEnumerable<ItemInstance> items,
            IReadOnlyDictionary<string, ItemDefinition> definitions,
            InventoryFilter filter = null,
            InventorySort sort = InventorySort.SlotThenRarity,
            Func<string, bool> isEquipped = null,
            IReadOnlyDictionary<string, AffixDefinition> affixDefinitions = null)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            filter ??= new InventoryFilter();
            var result = items
                .Where(item => item != null &&
                    definitions.TryGetValue(item.ItemDefinitionId, out var definition) &&
                    Matches(item, definition, filter, isEquipped, affixDefinitions))
                .OrderBy(item => item, Comparer<ItemInstance>.Create((left, right) =>
                    Compare(left, right, definitions, sort)))
                .ThenBy(item => item.InstanceId, StringComparer.Ordinal)
                .ToArray();

            return result.Length == 0 ? Array.Empty<ItemInstance>() : result;
        }

        public static IReadOnlyList<ItemInstance> Apply(
            IEnumerable<ItemInstance> items,
            InventoryFilter filter,
            InventorySort sort,
            IReadOnlyDictionary<string, ItemDefinition> definitions,
            Func<string, bool> isEquipped = null,
            IReadOnlyDictionary<string, AffixDefinition> affixDefinitions = null) =>
            Apply(items, definitions, filter, sort, isEquipped, affixDefinitions);

        public static IReadOnlyList<ItemInstance> Apply(
            InventoryService service,
            InventoryFilter filter = null,
            InventorySort sort = InventorySort.SlotThenRarity)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            return Apply(service.Items, service.Definitions, filter, sort, service.IsEquipped);
        }

        private static bool Matches(
            ItemInstance item,
            ItemDefinition definition,
            InventoryFilter filter,
            Func<string, bool> isEquipped,
            IReadOnlyDictionary<string, AffixDefinition> affixDefinitions)
        {
            if (filter.Slot.HasValue && definition.Slot != filter.Slot.Value) return false;
            if (filter.Rarity.HasValue && item.Rarity != filter.Rarity.Value) return false;
            if (filter.Equipped.HasValue &&
                ((isEquipped != null && isEquipped(item.InstanceId)) != filter.Equipped.Value))
            {
                return false;
            }

            if (!filter.AffixKind.HasValue) return true;
            if (affixDefinitions == null) return false;

            return item.Affixes.Any(affix =>
                affix != null &&
                affixDefinitions.TryGetValue(affix.AffixId, out var definition) &&
                definition != null &&
                definition.EffectKind == filter.AffixKind.Value);
        }

        private static int Compare(
            ItemInstance left,
            ItemInstance right,
            IReadOnlyDictionary<string, ItemDefinition> definitions,
            InventorySort sort)
        {
            var leftDefinition = definitions[left.ItemDefinitionId];
            var rightDefinition = definitions[right.ItemDefinitionId];

            switch (sort)
            {
                case InventorySort.SlotThenRarity:
                    return CompareThen(
                        leftDefinition.Slot.CompareTo(rightDefinition.Slot),
                        left.Rarity.CompareTo(right.Rarity),
                        string.Compare(leftDefinition.LocalizationKey, rightDefinition.LocalizationKey, StringComparison.Ordinal));
                case InventorySort.RarityThenItemLevel:
                    return CompareThen(
                        left.Rarity.CompareTo(right.Rarity),
                        left.ItemLevel.CompareTo(right.ItemLevel),
                        string.Compare(leftDefinition.LocalizationKey, rightDefinition.LocalizationKey, StringComparison.Ordinal));
                case InventorySort.ItemLevelThenName:
                    return CompareThen(
                        left.ItemLevel.CompareTo(right.ItemLevel),
                        string.Compare(leftDefinition.LocalizationKey, rightDefinition.LocalizationKey, StringComparison.Ordinal));
                case InventorySort.ValueThenName:
                    return CompareThen(
                        leftDefinition.BaseValue.CompareTo(rightDefinition.BaseValue),
                        string.Compare(leftDefinition.LocalizationKey, rightDefinition.LocalizationKey, StringComparison.Ordinal));
                default:
                    return 0;
            }
        }

        private static int CompareThen(params int[] comparisons)
        {
            foreach (var comparison in comparisons)
                if (comparison != 0) return comparison;
            return 0;
        }
    }
}
