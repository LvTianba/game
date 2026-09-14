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

        public EconomyService(
            InventoryService inventory,
            IReadOnlyDictionary<string, ItemDefinition> definitions)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
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
            return Math.Max(1, definition.BaseValue * item.ItemLevel * rarityMultiplier / 10 + affixValue);
        }

        public int GetSellPrice(ItemInstance item) => Math.Max(1, GetBuyPrice(item) * 2 / 5);

        public bool TrySell(string instanceId, out string error)
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

            var price = GetSellPrice(item);
            if (!inventory.TryRemove(instanceId))
            {
                error = InventoryTextKeys.ItemMissing;
                return false;
            }

            inventory.AddGold(price);
            error = string.Empty;
            return true;
        }

        public bool TryBuy(ItemInstance item, out string error)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!definitions.ContainsKey(item.ItemDefinitionId))
            {
                error = InventoryTextKeys.UnknownDefinition;
                return false;
            }

            var price = GetBuyPrice(item);
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

        private bool TryGetItem(string instanceId, out ItemInstance item)
        {
            item = inventory.Items.FirstOrDefault(value => value.InstanceId == instanceId);
            return item != null;
        }
    }
}
