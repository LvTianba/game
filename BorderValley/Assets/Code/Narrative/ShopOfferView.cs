using BorderValley.Inventory;

namespace BorderValley.Narrative
{
    public sealed class ShopOfferView
    {
        public ShopOfferView(string offerId, ItemInstance item, int buyPrice, bool isPurchased)
        {
            OfferId = offerId;
            Item = item;
            BuyPrice = buyPrice;
            IsPurchased = isPurchased;
        }

        public string OfferId { get; }
        public ItemInstance Item { get; }
        public int BuyPrice { get; }
        public bool IsPurchased { get; }
    }
}
