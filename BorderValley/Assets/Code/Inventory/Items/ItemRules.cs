using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Data.Items;

namespace BorderValley.Inventory
{
    public static class ItemRules
    {
        public static int AffixCount(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => 0,
            ItemRarity.Fine => 1,
            ItemRarity.Rare => 2,
            ItemRarity.Epic => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity))
        };

        public static int Budget(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => 0,
            ItemRarity.Fine => 12,
            ItemRarity.Rare => 24,
            ItemRarity.Epic => 40,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity))
        };

        public static int ValueCost(int value, AffixDefinition definition)
        {
            var units = definition.Stat == BorderValley.Core.Combat.CombatStat.CritChanceBps
                ? Math.Max(1, (Math.Abs(value) + 99) / 100)
                : Math.Max(1, Math.Abs(value));
            return units * Math.Max(1, definition.BudgetCost);
        }

        public static bool IsSpecial(AffixDefinition definition) =>
            definition != null &&
            definition.EffectKind is AffixEffectKind.SkillModifier or AffixEffectKind.Trigger;

        public static bool Conflicts(AffixDefinition first, AffixDefinition second) =>
            first != null &&
            second != null &&
            (first.Id == second.Id ||
             first.IsMutuallyExclusive(second.Id) ||
             second.IsMutuallyExclusive(first.Id));

        public static bool Conflicts(
            string firstId,
            string secondId,
            IReadOnlyDictionary<string, AffixDefinition> affixes)
        {
            if (firstId == secondId) return true;
            if (affixes == null ||
                !affixes.TryGetValue(firstId, out var first) ||
                !affixes.TryGetValue(secondId, out var second))
                return true;
            return Conflicts(first, second);
        }

        public static bool IsEligible(
            AffixDefinition definition,
            ItemSlot slot,
            ItemRarity rarity,
            int itemLevel) =>
            definition != null &&
            definition.Weight > 0 &&
            definition.Supports(slot, rarity, itemLevel);

        public static bool IsEligible(
            AffixInstance value,
            ItemSlot slot,
            ItemRarity rarity,
            int itemLevel,
            IReadOnlyDictionary<string, AffixDefinition> affixes)
        {
            if (value == null || affixes == null || !affixes.TryGetValue(value.AffixId, out var definition))
                return false;
            return value.Value >= definition.MinValue &&
                   value.Value <= definition.MaxValue &&
                   IsEligible(definition, slot, rarity, itemLevel);
        }

        public static bool TryValidate(
            ItemInstance item,
            ItemDefinition itemDefinition,
            IReadOnlyDictionary<string, AffixDefinition> affixes,
            out string error)
        {
            if (item == null || itemDefinition == null ||
                !string.Equals(item.ItemDefinitionId, itemDefinition.Id, StringComparison.Ordinal))
            {
                error = "invalid_item_definition";
                return false;
            }

            return TryValidateAffixes(
                item.Affixes,
                itemDefinition.Slot,
                item.Rarity,
                item.ItemLevel,
                affixes,
                out error);
        }

        public static bool TryValidateAffixes(
            IReadOnlyList<AffixInstance> values,
            ItemSlot slot,
            ItemRarity rarity,
            int itemLevel,
            IReadOnlyDictionary<string, AffixDefinition> affixes,
            out string error)
        {
            var expected = AffixCount(rarity);
            if (values == null || values.Count != expected)
            {
                error = "invalid_affix_count";
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var totalCost = 0;
            var hasSpecial = false;
            var requiresSpecial = rarity == ItemRarity.Epic &&
                                  affixes != null &&
                                  affixes.Values.Any(definition =>
                                      IsEligible(definition, slot, rarity, itemLevel) &&
                                      IsSpecial(definition));

            foreach (var value in values)
            {
                if (value == null || !seen.Add(value.AffixId))
                {
                    error = "duplicate_or_null_affix";
                    return false;
                }

                if (!IsEligible(value, slot, rarity, itemLevel, affixes))
                {
                    error = "ineligible_affix";
                    return false;
                }

                if (seen.Any(existing =>
                        existing != value.AffixId &&
                        Conflicts(existing, value.AffixId, affixes)))
                {
                    error = "mutually_exclusive_affix";
                    return false;
                }

                var definition = affixes[value.AffixId];
                hasSpecial |= IsSpecial(definition);
                totalCost += ValueCost(value.Value, definition);
            }

            if (requiresSpecial && !hasSpecial)
            {
                error = "missing_special_affix";
                return false;
            }

            if (totalCost > Budget(rarity))
            {
                error = "affix_budget_exceeded";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
