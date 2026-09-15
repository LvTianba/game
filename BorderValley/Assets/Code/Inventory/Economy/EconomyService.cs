using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Data.Items;

namespace BorderValley.Inventory
{
    public sealed class EconomyService
    {
        private readonly InventoryService inventory;
        private readonly IReadOnlyDictionary<string, ItemDefinition> definitions;
        private readonly IReadOnlyDictionary<string, AffixDefinition> affixes;

        public EconomyService(
            InventoryService inventory,
            IReadOnlyDictionary<string, ItemDefinition> definitions)
            : this(inventory, definitions, null)
        {
        }

        public EconomyService(
            InventoryService inventory,
            IReadOnlyDictionary<string, ItemDefinition> definitions,
            IReadOnlyDictionary<string, AffixDefinition> affixes)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            this.affixes = affixes ?? new Dictionary<string, AffixDefinition>(StringComparer.Ordinal);
        }

        public int GetBuyPrice(ItemInstance item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!definitions.TryGetValue(item.ItemDefinitionId, out var definition))
                throw new KeyNotFoundException($"Unknown item definition: {item.ItemDefinitionId}");

            var rarityMultiplier = item.Rarity switch
            {
                ItemRarity.Common => 1,
                ItemRarity.Fine => 2,
                ItemRarity.Rare => 4,
                ItemRarity.Epic => 8,
                _ => 1
            };
            var affixValue = item.Affixes.Sum(affix => Math.Abs(affix.Value) * 2);
            return Math.Max(2, definition.BaseValue * item.ItemLevel * rarityMultiplier / 10 + affixValue);
        }

        public int GetBuyPrice(ItemInstance item, int modifierBps) =>
            Math.Max(2, ApplyModifier(GetBuyPrice(item), modifierBps));

        public int GetSellPrice(ItemInstance item) => Math.Max(1, GetBuyPrice(item) * 2 / 5);

        public int GetSellPrice(ItemInstance item, int modifierBps) =>
            Math.Max(1, ApplyModifier(GetSellPrice(item), modifierBps));

        public bool TrySell(string instanceId, out string error) =>
            TrySell(instanceId, 10000, out error);

        public bool TrySell(string instanceId, int modifierBps, out string error)
        {
            if (!TryGetItem(instanceId, out var item))
            {
                error = InventoryTextKeys.ItemMissing;
                return false;
            }

            if (!definitions.TryGetValue(item.ItemDefinitionId, out var definition))
            {
                error = InventoryTextKeys.UnknownDefinition;
                return false;
            }

            if (inventory.IsEquipped(instanceId))
            {
                error = InventoryTextKeys.EquippedCannotSell;
                return false;
            }

            if (definition.IsQuestItem)
            {
                error = InventoryTextKeys.QuestCannotSell;
                return false;
            }

            var price = GetSellPrice(item, modifierBps);
            if (!inventory.TryRemove(instanceId))
            {
                error = InventoryTextKeys.ItemMissing;
                return false;
            }

            inventory.AddGold(price);
            error = string.Empty;
            return true;
        }

        public bool TryBuy(ItemInstance item, out string error) =>
            TryBuy(item, 10000, out error);

        public bool TryBuy(ItemInstance item, int modifierBps, out string error)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!definitions.TryGetValue(item.ItemDefinitionId, out var definition))
            {
                error = InventoryTextKeys.UnknownDefinition;
                return false;
            }

            if (!ItemRules.TryValidate(item, definition, affixes, out _))
            {
                error = InventoryTextKeys.InvalidItem;
                return false;
            }

            var price = GetBuyPrice(item, modifierBps);
            if (!inventory.TrySpendGold(price))
            {
                error = InventoryTextKeys.NotEnoughGold;
                return false;
            }

            if (!inventory.TryAdd(item, out error))
            {
                inventory.AddGold(price);
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static int ApplyModifier(int price, int modifierBps)
        {
            if (modifierBps < 0) throw new ArgumentOutOfRangeException(nameof(modifierBps));
            var adjusted = (long)price * modifierBps / 10000L;
            return adjusted > int.MaxValue ? int.MaxValue : (int)adjusted;
        }

        private bool TryGetItem(string instanceId, out ItemInstance item)
        {
            item = inventory.Items.FirstOrDefault(value => value.InstanceId == instanceId);
            return item != null;
        }
    }
}
