using BorderValley.Core.Combat;
using BorderValley.Data.Items;
namespace BorderValley.UI.Inventory
{
    public static class InventoryTextKeys
    {
        public const string BagFull = BorderValley.Inventory.InventoryTextKeys.BagFull;
        public const string DuplicateInstance = BorderValley.Inventory.InventoryTextKeys.DuplicateInstance;
        public const string UnknownDefinition = BorderValley.Inventory.InventoryTextKeys.UnknownDefinition;
        public const string ItemMissing = BorderValley.Inventory.InventoryTextKeys.ItemMissing;
        public const string ClassRestricted = BorderValley.Inventory.InventoryTextKeys.ClassRestricted;
        public const string AlreadyEquipped = BorderValley.Inventory.InventoryTextKeys.AlreadyEquipped;
        public const string SlotEmpty = BorderValley.Inventory.InventoryTextKeys.SlotEmpty;
        public const string NotEnoughGold = BorderValley.Inventory.InventoryTextKeys.NotEnoughGold;
        public const string NotEnoughMaterials = BorderValley.Inventory.InventoryTextKeys.NotEnoughMaterials;
        public const string EquippedCannotDismantle = BorderValley.Inventory.InventoryTextKeys.EquippedCannotDismantle;
        public const string EquippedCannotReforge = BorderValley.Inventory.InventoryTextKeys.EquippedCannotReforge;
        public const string CraftingFailed = BorderValley.Inventory.InventoryTextKeys.CraftingFailed;
        public const string ReforgeFailed = BorderValley.Inventory.InventoryTextKeys.ReforgeFailed;
        public const string ReforgeNoOp = BorderValley.Inventory.InventoryTextKeys.ReforgeNoOp;
        public const string AutoSaveFailed = BorderValley.Inventory.InventoryTextKeys.AutoSaveFailed;
        public const string ServiceUnavailable = "inventory.ui.error.service_unavailable";
        public const string InventoryTitle = "inventory.ui.title";
        public const string CraftTitle = "inventory.craft.title";
        public const string EquipmentTitle = "inventory.ui.equipment";
        public const string Gold = "inventory.gold";
        public const string Ore = "inventory.material.ore";
        public const string Filter = "inventory.ui.filter";
        public const string Sort = "inventory.ui.sort";
        public const string FilterAll = "inventory.filter.all";
        public const string FilterWeapon = "inventory.filter.weapon";
        public const string FilterArmor = "inventory.filter.armor";
        public const string FilterAccessory = "inventory.filter.accessory";
        public const string FilterEquipped = "inventory.filter.equipped";
        public const string SortSlot = "inventory.sort.slot";
        public const string SortRarity = "inventory.sort.rarity";
        public const string SortItemLevel = "inventory.sort.item_level";
        public const string SortValue = "inventory.sort.value";
        public const string PreviousPage = "inventory.ui.previous_page";
        public const string NextPage = "inventory.ui.next_page";
        public const string Page = "inventory.ui.page";
        public const string Details = "inventory.ui.details";
        public const string BaseStats = "inventory.ui.base_stats";
        public const string AffixRanges = "inventory.ui.affix_ranges";
        public const string Equip = "inventory.ui.equip";
        public const string Unequip = "inventory.ui.unequip";
        public const string Dismantle = "inventory.ui.dismantle";
        public const string Reforge = "inventory.ui.reforge";
        public const string Craft = "inventory.ui.craft";
        public const string Cost = "inventory.ui.cost";
        public const string MaterialsInsufficient = "inventory.ui.materials_insufficient";
        public const string ItemLevel = "inventory.ui.item_level";
        public const string EquippedMarker = "inventory.ui.equipped";
        public const string Empty = "inventory.ui.empty";
        public const string OpenInventory = "world.ui.inventory";
        public const string OpenCraft = "world.ui.craft";
        public const string Rest = "world.ui.rest";
        public const string Battle = "battle.ui.enter_battle";
        public const string Retry = "world.ui.retry";
        public const string ActiveMember = "inventory.ui.active_member";
        public const string NextMember = "inventory.ui.next_member";
        public const string Locked = "inventory.ui.locked";
        public const string CycleLock = "inventory.ui.cycle_lock";
        public const string RetrySave = "inventory.ui.retry_save";

        public static string SlotKey(ItemSlot slot) =>
            "inventory.slot." + slot.ToString().ToLowerInvariant();

        public static string StatKey(CombatStat stat) =>
            "inventory.stat." + stat.ToString().ToLowerInvariant();

        public static string RarityKey(ItemRarity rarity) =>
            "inventory.rarity." + rarity.ToString().ToLowerInvariant();
    }
}
