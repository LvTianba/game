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
        private readonly Dictionary<string, int> reforgeActionCounters = new(StringComparer.Ordinal);

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

            var level = Math.Clamp(itemLevel, 1, 10);
            var local = random.Fork("craft:" + instanceId);
            var rarity = RollRarity(local);
            var goldCost = costs.CraftGold(level, rarity);
            var materialCost = costs.CraftMaterial(level, rarity);

            if (!CanSpend(goldCost, materialCost, out var spendError))
                return CraftingResult.Failure(spendError);

            if (!Spend(goldCost, materialCost))
                return CraftingResult.Failure(InventoryTextKeys.NotEnoughGold);

            try
            {
                var item = generator.GenerateForItem(
                    instanceId,
                    itemDefinition,
                    level,
                    rarity,
                    local.Fork("item"));
                if (!ItemRules.TryValidate(item, itemDefinition, affixes, out _))
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

            var count = ItemRules.AffixCount(item.Rarity);
            if (count == 0)
                return CraftingResult.Failure(InventoryTextKeys.ReforgeFailed);

            var locksAffix = !string.IsNullOrWhiteSpace(lockedAffixId);
            AffixInstance locked = null;
            if (locksAffix)
            {
                locked = item.Affixes.FirstOrDefault(value => value.AffixId == lockedAffixId);
                if (locked == null)
                    return CraftingResult.Failure(InventoryTextKeys.LockedAffixNotFound);
                if (!ItemRules.IsEligible(locked, definition.Slot, item.Rarity, item.ItemLevel, affixes))
                    return CraftingResult.Failure(InventoryTextKeys.ReforgeFailed);
            }

            if (locked != null && count == 1)
                return CraftingResult.Failure(InventoryTextKeys.ReforgeNoOp);

            IReadOnlyList<AffixInstance> values;
            try
            {
                values = GenerateReforgedAffixes(item, definition, locked, random);
            }
            catch (InvalidOperationException)
            {
                return CraftingResult.Failure(InventoryTextKeys.ReforgeFailed);
            }

            if (!ItemRules.TryValidateAffixes(
                    values,
                    definition.Slot,
                    item.Rarity,
                    item.ItemLevel,
                    affixes,
                    out _))
                return CraftingResult.Failure(InventoryTextKeys.ReforgeFailed);
            if (AffixSetsEqual(item.Affixes, values))
                return CraftingResult.Failure(InventoryTextKeys.ReforgeNoOp);

            var goldCost = costs.ReforgeGold(item, locksAffix);
            var materialCost = costs.ReforgeMaterial(item, locksAffix);
            if (!CanSpend(goldCost, materialCost, out var spendError))
                return CraftingResult.Failure(spendError);
            if (!Spend(goldCost, materialCost))
                return CraftingResult.Failure(InventoryTextKeys.NotEnoughGold);

            try
            {
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
            var count = ItemRules.AffixCount(item.Rarity);
            reforgeActionCounters.TryGetValue(item.InstanceId, out var actionIndex);
            reforgeActionCounters[item.InstanceId] = actionIndex + 1;
            var local = random.Fork(
                "reforge:" + item.InstanceId + ":" + (locked?.AffixId ?? string.Empty) + ":" + actionIndex);

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
                    if (values != null &&
                        !AffixSetsEqual(item.Affixes, values) &&
                        ItemRules.TryValidateAffixes(
                            values,
                            definition.Slot,
                            item.Rarity,
                            item.ItemLevel,
                            affixes,
                            out _))
                        return values;
                }
                catch (InvalidOperationException)
                {
                    // A deterministic stream can still exhaust this attempt. Try the next fork.
                }
            }

            throw new InvalidOperationException("Unable to generate a different valid reforge result.");
        }

        private static bool AffixSetsEqual(
            IReadOnlyList<AffixInstance> left,
            IReadOnlyList<AffixInstance> right)
        {
            if (left.Count != right.Count) return false;
            return left.OrderBy(value => value.AffixId, StringComparer.Ordinal)
                .Select(value => (value.AffixId, value.Value))
                .SequenceEqual(
                    right.OrderBy(value => value.AffixId, StringComparer.Ordinal)
                        .Select(value => (value.AffixId, value.Value)));
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
                    (value.AffixId == locked.AffixId ||
                     ItemRules.Conflicts(locked.AffixId, value.AffixId, affixes)))
                    continue;
                if (result.Any(existing =>
                        existing.AffixId == value.AffixId ||
                        ItemRules.Conflicts(existing.AffixId, value.AffixId, affixes)))
                    continue;

                result.Add(value);
                if (result.Count == count)
                    return result.AsReadOnly();
            }

            return result.Count == count ? result.AsReadOnly() : null;
        }

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
