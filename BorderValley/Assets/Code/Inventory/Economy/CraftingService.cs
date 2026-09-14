using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Random;
using BorderValley.Data.Items;

namespace BorderValley.Inventory
{
    public sealed class CraftingService
    {
        public const string OreMaterialId = "material.ore";
        public const string EssenceMaterialId = "material.essence";

        private const int MaxReforgeAttempts = 64;

        private readonly InventoryService inventory;
        private readonly IReadOnlyDictionary<string, ItemDefinition> items;
        private readonly IReadOnlyDictionary<string, AffixDefinition> affixes;
        private readonly LootGenerator generator;
        private readonly CraftingCosts costs;

        public CraftingService(
            InventoryService inventory,
            IReadOnlyDictionary<string, ItemDefinition> items,
            IReadOnlyDictionary<string, AffixDefinition> affixes,
            LootGenerator generator,
            CraftingCosts costs)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.items = items ?? throw new ArgumentNullException(nameof(items));
            this.affixes = affixes ?? throw new ArgumentNullException(nameof(affixes));
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
            this.costs = costs ?? throw new ArgumentNullException(nameof(costs));
        }

        public CraftingResult Craft(
            string instanceId,
            ItemDefinition itemDefinition,
            string classId,
            int itemLevel,
            IRandomSource random)
        {
            if (string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException(nameof(instanceId));
            if (itemDefinition == null) throw new ArgumentNullException(nameof(itemDefinition));
            if (random == null) throw new ArgumentNullException(nameof(random));

            if (!items.ContainsKey(itemDefinition.Id))
                return CraftingResult.Failure(InventoryTextKeys.UnknownDefinition);
            if (!itemDefinition.AllowsClass(classId))
                return CraftingResult.Failure(InventoryTextKeys.ClassRestricted);
            if (TryGetItem(instanceId, out _))
                return CraftingResult.Failure(InventoryTextKeys.DuplicateInstance);
            if (inventory.Items.Count >= inventory.Capacity)
                return CraftingResult.Failure(InventoryTextKeys.BagFull);

            var local = random.Fork("craft:" + instanceId);
            var rarity = RollRarity(local);
            var goldCost = costs.CraftGold(itemLevel, rarity);
            var materialCost = costs.CraftMaterial(itemLevel, rarity);

            if (!CanSpend(goldCost, materialCost, out var spendError))
                return CraftingResult.Failure(spendError);

            if (!Spend(goldCost, materialCost))
                return CraftingResult.Failure(InventoryTextKeys.NotEnoughGold);

            try
            {
                var item = generator.GenerateForItem(
                    instanceId,
                    itemDefinition,
                    itemLevel,
                    rarity,
                    local.Fork("item"));
                if (!TryValidateAffixes(item.Affixes, itemDefinition.Slot, rarity, itemLevel, out _))
                    return RollbackFailure(goldCost, materialCost, InventoryTextKeys.CraftingFailed);

                if (!inventory.TryAdd(item, out var addError))
                    return RollbackFailure(goldCost, materialCost, addError);

                return CraftingResult.Succeeded(item, goldCost, materialCost);
            }
            catch (InvalidOperationException)
            {
                return RollbackFailure(goldCost, materialCost, InventoryTextKeys.CraftingFailed);
            }
        }

        public CraftingResult Dismantle(string instanceId)
        {
            if (!TryGetItem(instanceId, out var item))
                return CraftingResult.Failure(InventoryTextKeys.ItemMissing);
            if (inventory.IsEquipped(instanceId))
                return CraftingResult.Failure(InventoryTextKeys.EquippedCannotDismantle);
            if (!items.TryGetValue(item.ItemDefinitionId, out var definition))
                return CraftingResult.Failure(InventoryTextKeys.UnknownDefinition);
            if (definition.IsQuestItem)
                return CraftingResult.Failure(InventoryTextKeys.QuestCannotDismantle);

            if (!inventory.TryRemove(instanceId))
                return CraftingResult.Failure(InventoryTextKeys.ItemMissing);

            var material = costs.DismantleMaterial(item);
            var essence = item.Rarity >= ItemRarity.Rare ? 1 : 0;
            inventory.AddMaterial(OreMaterialId, material);
            if (essence > 0)
                inventory.AddMaterial(EssenceMaterialId, essence);

            return CraftingResult.Succeeded(item, 0, material, essence);
        }

        public CraftingResult Reforge(string instanceId, string lockedAffixId, IRandomSource random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (!TryGetItem(instanceId, out var item))
                return CraftingResult.Failure(InventoryTextKeys.ItemMissing);
            if (inventory.IsEquipped(instanceId))
                return CraftingResult.Failure(InventoryTextKeys.EquippedCannotReforge);
            if (!items.TryGetValue(item.ItemDefinitionId, out var definition))
                return CraftingResult.Failure(InventoryTextKeys.UnknownDefinition);

            var locksAffix = !string.IsNullOrWhiteSpace(lockedAffixId);
            if (locksAffix && AffixCount(item.Rarity) == 0)
                return CraftingResult.Failure(InventoryTextKeys.ReforgeFailed);
            AffixInstance locked = null;
            if (locksAffix)
            {
                locked = item.Affixes.FirstOrDefault(value => value.AffixId == lockedAffixId);
                if (locked == null)
                    return CraftingResult.Failure(InventoryTextKeys.LockedAffixNotFound);
                if (!IsEligible(locked, definition.Slot, item.Rarity, item.ItemLevel))
                    return CraftingResult.Failure(InventoryTextKeys.ReforgeFailed);
            }

            var goldCost = costs.ReforgeGold(item, locksAffix);
            var materialCost = costs.ReforgeMaterial(item, locksAffix);
            if (!CanSpend(goldCost, materialCost, out var spendError))
                return CraftingResult.Failure(spendError);
            if (!Spend(goldCost, materialCost))
                return CraftingResult.Failure(InventoryTextKeys.NotEnoughGold);

            try
            {
                var values = GenerateReforgedAffixes(item, definition, locked, random);
                if (!TryValidateAffixes(values, definition.Slot, item.Rarity, item.ItemLevel, out _))
                    return RollbackFailure(goldCost, materialCost, InventoryTextKeys.ReforgeFailed);

                item.ReplaceAffixes(values);
                inventory.AddGold(0);
                return CraftingResult.Succeeded(item, goldCost, materialCost);
            }
            catch (InvalidOperationException)
            {
                return RollbackFailure(goldCost, materialCost, InventoryTextKeys.ReforgeFailed);
            }
        }

        private IReadOnlyList<AffixInstance> GenerateReforgedAffixes(
            ItemInstance item,
            ItemDefinition definition,
            AffixInstance locked,
            IRandomSource random)
        {
            var count = AffixCount(item.Rarity);
            if (count == 0)
                return Array.Empty<AffixInstance>();
            if (locked != null && count == 1)
                return new[] { locked };

            var local = random.Fork(
                "reforge:" + item.InstanceId + ":" + (locked?.AffixId ?? string.Empty));
            for (var attempt = 0; attempt < MaxReforgeAttempts; attempt++)
            {
                try
                {
                    var generated = generator.GenerateForItem(
                        item.InstanceId,
                        definition,
                        item.ItemLevel,
                        item.Rarity,
                        local.Fork("attempt:" + attempt));
                    var values = Combine(locked, generated.Affixes, count);
                    if (values != null && TryValidateAffixes(
                            values,
                            definition.Slot,
                            item.Rarity,
                            item.ItemLevel,
                            out _))
                        return values;
                }
                catch (InvalidOperationException)
                {
                    // A deterministic stream can still exhaust this attempt. Try the next fork.
                }
            }

            throw new InvalidOperationException("Unable to generate a valid reforge result.");
        }

        private IReadOnlyList<AffixInstance> Combine(
            AffixInstance locked,
            IReadOnlyList<AffixInstance> generated,
            int count)
        {
            var result = new List<AffixInstance>(count);
            if (locked != null)
                result.Add(locked);

            foreach (var value in generated)
            {
                if (locked != null &&
                    (value.AffixId == locked.AffixId || Conflicts(locked.AffixId, value.AffixId)))
                    continue;
                if (result.Any(existing =>
                        existing.AffixId == value.AffixId ||
                        Conflicts(existing.AffixId, value.AffixId)))
                    continue;

                result.Add(value);
                if (result.Count == count)
                    return result.AsReadOnly();
            }

            return result.Count == count ? result.AsReadOnly() : null;
        }

        private bool TryValidateAffixes(
            IReadOnlyList<AffixInstance> values,
            ItemSlot slot,
            ItemRarity rarity,
            int itemLevel,
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
            var requiresSpecial = rarity == ItemRarity.Epic && affixes.Values.Any(affix =>
                affix != null && affix.Weight > 0 &&
                affix.Supports(slot, rarity, itemLevel) &&
                IsSpecial(affix));

            foreach (var value in values)
            {
                if (value == null || !seen.Add(value.AffixId))
                {
                    error = "duplicate_or_null_affix";
                    return false;
                }

                if (!affixes.TryGetValue(value.AffixId, out var definition) ||
                    !IsEligible(value, slot, rarity, itemLevel))
                {
                    error = "ineligible_affix";
                    return false;
                }

                if (seen.Any(existing => existing != value.AffixId && Conflicts(existing, value.AffixId)))
                {
                    error = "mutually_exclusive_affix";
                    return false;
                }

                hasSpecial |= IsSpecial(definition);
                totalCost += Math.Max(1, Math.Abs(value.Value)) * definition.BudgetCost;
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

        private bool IsEligible(AffixInstance value, ItemSlot slot, ItemRarity rarity, int itemLevel)
        {
            if (value == null || !affixes.TryGetValue(value.AffixId, out var definition)) return false;
            return value.Value >= definition.MinValue &&
                   value.Value <= definition.MaxValue &&
                   IsEligible(definition, slot, rarity, itemLevel);
        }

        private static bool IsEligible(
            AffixDefinition definition,
            ItemSlot slot,
            ItemRarity rarity,
            int itemLevel) =>
            definition != null &&
            definition.Weight > 0 &&
            definition.Supports(slot, rarity, itemLevel);

        private bool Conflicts(string first, string second)
        {
            if (first == second) return true;
            if (!affixes.TryGetValue(first, out var firstDefinition)) return true;
            if (!affixes.TryGetValue(second, out var secondDefinition)) return true;
            return firstDefinition.IsMutuallyExclusive(second) ||
                   secondDefinition.IsMutuallyExclusive(first);
        }

        private static bool IsSpecial(AffixDefinition definition) =>
            definition.EffectKind is AffixEffectKind.SkillModifier or AffixEffectKind.Trigger;

        private static int AffixCount(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => 0,
            ItemRarity.Fine => 1,
            ItemRarity.Rare => 2,
            ItemRarity.Epic => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity))
        };

        private static int Budget(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => 0,
            ItemRarity.Fine => 12,
            ItemRarity.Rare => 24,
            ItemRarity.Epic => 40,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity))
        };

        private static ItemRarity RollRarity(IRandomSource random) =>
            (ItemRarity)random.Range(0, 4);

        private bool CanSpend(int goldCost, int materialCost, out string error)
        {
            if (inventory.Gold < goldCost)
            {
                error = InventoryTextKeys.NotEnoughGold;
                return false;
            }
            if (!inventory.Materials.TryGetValue(OreMaterialId, out var ore) || ore < materialCost)
            {
                error = InventoryTextKeys.NotEnoughMaterials;
                return false;
            }
            error = string.Empty;
            return true;
        }

        private bool Spend(int goldCost, int materialCost)
        {
            if (!inventory.TrySpendGold(goldCost))
                return false;
            if (inventory.TrySpendMaterial(OreMaterialId, materialCost))
                return true;

            inventory.AddGold(goldCost);
            return false;
        }

        private CraftingResult RollbackFailure(int goldCost, int materialCost, string error)
        {
            inventory.AddGold(goldCost);
            inventory.AddMaterial(OreMaterialId, materialCost);
            return CraftingResult.Failure(error);
        }

        private bool TryGetItem(string instanceId, out ItemInstance item)
        {
            item = inventory.Items.FirstOrDefault(value => value.InstanceId == instanceId);
            return item != null;
        }
    }

    public sealed class CraftingResult
    {
        private CraftingResult(
            bool success,
            string error,
            ItemInstance item,
            int goldCost,
            int materialCost,
            int essenceGained)
        {
            Success = success;
            Error = error ?? string.Empty;
            Item = item;
            GoldCost = goldCost;
            MaterialCost = materialCost;
            EssenceGained = essenceGained;
        }

        public bool Success { get; }
        public string Error { get; }
        public ItemInstance Item { get; }
        public int GoldCost { get; }
        public int MaterialCost { get; }
        public int EssenceGained { get; }

        public static CraftingResult Succeeded(ItemInstance item, int goldCost, int materialCost, int essenceGained = 0) =>
            new(true, string.Empty, item, goldCost, materialCost, essenceGained);

        public static CraftingResult Failure(string error) =>
            new(false, error, null, 0, 0, 0);
    }
}
