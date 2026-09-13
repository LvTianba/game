namespace BorderValley.Inventory
{
    public sealed class InventoryFilter
    {
        public Data.Items.ItemSlot? Slot { get; set; }
        public Data.Items.ItemRarity? Rarity { get; set; }
        public bool? Equipped { get; set; }
        public Data.Items.AffixEffectKind? AffixKind { get; set; }
    }
}
