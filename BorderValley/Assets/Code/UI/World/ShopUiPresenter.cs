using System;
using System.Linq;
using BorderValley.Data.Items;
using BorderValley.Inventory;
using BorderValley.Narrative;

namespace BorderValley.UI.World
{
    public sealed class ShopUiPresenter
    {
        private readonly ShopService shop;
        private readonly InventoryService inventory;
        private readonly EconomyService economy;
        private readonly NarrativeStateService state;
        private readonly IShopPanelView view;
        private string currentShopId = string.Empty;

        public ShopUiPresenter(
            ShopService shop,
            InventoryService inventory,
            EconomyService economy,
            NarrativeStateService state,
            IShopPanelView view)
        {
            this.shop = shop ?? throw new ArgumentNullException(nameof(shop));
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.economy = economy ?? throw new ArgumentNullException(nameof(economy));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.view.BuyRequested += offerId => Buy(offerId);
            this.view.SellRequested += instanceId => Sell(instanceId);
            this.view.CloseRequested += Close;
        }

        public event Action<string> BuySucceeded;
        public event Action<string> SellSucceeded;
        public bool IsOpen { get; private set; }
        public string CurrentShopId => currentShopId;
        public string LastErrorKey { get; private set; } = string.Empty;

        public bool Open(string shopId)
        {
            currentShopId = shopId ?? string.Empty;
            LastErrorKey = string.Empty;
            if (!CanOpenShop(currentShopId))
            {
                currentShopId = string.Empty;
                return Fail(NarrativeTextKeys.UnknownShop, false);
            }

            IsOpen = true;
            view.SetVisible(true);
            Refresh();
            return true;
        }

        public bool Buy(string offerId)
        {
            if (!IsOpen || string.IsNullOrWhiteSpace(currentShopId))
                return Fail(NarrativeTextKeys.UnknownShop);
            if (!shop.TryBuy(currentShopId, offerId, out var error))
                return Fail(error);

            LastErrorKey = string.Empty;
            Refresh();
            BuySucceeded?.Invoke(offerId);
            return true;
        }

        public bool Sell(string instanceId)
        {
            if (!IsOpen || string.IsNullOrWhiteSpace(currentShopId))
                return Fail(NarrativeTextKeys.UnknownShop);
            if (!shop.TrySell(instanceId, out var error))
                return Fail(error);

            LastErrorKey = string.Empty;
            Refresh();
            SellSucceeded?.Invoke(instanceId);
            return true;
        }

        public void Close()
        {
            currentShopId = string.Empty;
            LastErrorKey = string.Empty;
            IsOpen = false;
            view.SetVisible(false);
            view.Render(EmptyData());
        }

        public void Refresh()
        {
            if (!CanOpenShop(currentShopId))
            {
                view.Render(EmptyData());
                return;
            }

            var offers = shop.GetOffers(currentShopId)
                .Select(offer => new ShopOfferBinding(
                    offer.OfferId,
                    GetItemKey(offer.Item.ItemDefinitionId),
                    offer.BuyPrice))
                .ToArray();
            var sellItems = inventory.Items
                .Select(item => new ShopSellBinding(
                    item.InstanceId,
                    GetItemKey(item.ItemDefinitionId),
                    economy.GetSellPrice(item)))
                .ToArray();
            view.Render(new ShopPanelViewData(
                WorldTextKeys.ShopTitle,
                inventory.Gold,
                offers,
                sellItems,
                LastErrorKey));
        }

        private ShopPanelViewData EmptyData() =>
            new(
                WorldTextKeys.ShopTitle,
                inventory.Gold,
                Array.Empty<ShopOfferBinding>(),
                Array.Empty<ShopSellBinding>(),
                LastErrorKey);

        private bool Fail(string error, bool keepOpen = true)
        {
            LastErrorKey = string.IsNullOrWhiteSpace(error)
                ? WorldTextKeys.ServiceUnavailable
                : error;
            IsOpen = keepOpen;
            view.SetVisible(keepOpen);
            Refresh();
            return false;
        }

        private bool CanOpenShop(string shopId) =>
            !string.IsNullOrWhiteSpace(shopId) &&
            state.HasShop(shopId) &&
            state.IsShopUnlocked(shopId);

        private string GetItemKey(string definitionId)
        {
            if (inventory.Definitions.TryGetValue(definitionId, out var definition) &&
                !string.IsNullOrWhiteSpace(definition.LocalizationKey))
            {
                return definition.LocalizationKey;
            }
            return string.IsNullOrWhiteSpace(definitionId) ? WorldTextKeys.UnknownItem : definitionId;
        }
    }
}
