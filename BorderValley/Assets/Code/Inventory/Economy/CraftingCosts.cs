using BorderValley.Data.Items;

namespace BorderValley.Inventory
{
    public sealed class CraftingCosts
    {
        public int CraftGold(int itemLevel, ItemRarity rarity) =>
            40 + itemLevel * 5 + (int)rarity * 20;

        public int CraftMaterial(int itemLevel, ItemRarity rarity) =>
            2 + itemLevel / 3 + (int)rarity;

        public int DismantleMaterial(ItemInstance item) =>
            1 + item.ItemLevel / 3 + (int)item.Rarity;

        public int ReforgeGold(ItemInstance item, bool locksAffix) =>
            30 + item.ItemLevel * 4 + (int)item.Rarity * 15 + (locksAffix ? 60 : 0);

        public int ReforgeMaterial(ItemInstance item, bool locksAffix) =>
            1 + (int)item.Rarity + (locksAffix ? 3 : 0);
    }
}
