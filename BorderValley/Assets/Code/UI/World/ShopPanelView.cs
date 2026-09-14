using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BorderValley.UI.World
{
    public interface IShopPanelView
    {
        event Action<string> BuyRequested;
        event Action<string> SellRequested;
        event Action CloseRequested;
        void SetVisible(bool visible);
        void Render(ShopPanelViewData data);
    }

    public sealed class ShopOfferBinding
    {
        public ShopOfferBinding(string offerId, string itemKey, int buyPrice)
        {
            OfferId = offerId ?? string.Empty;
            ItemKey = itemKey ?? string.Empty;
            BuyPrice = buyPrice;
        }

        public string OfferId { get; }
        public string ItemKey { get; }
        public int BuyPrice { get; }
    }

    public sealed class ShopSellBinding
    {
        public ShopSellBinding(string instanceId, string itemKey, int sellPrice)
        {
            InstanceId = instanceId ?? string.Empty;
            ItemKey = itemKey ?? string.Empty;
            SellPrice = sellPrice;
        }

        public string InstanceId { get; }
        public string ItemKey { get; }
        public int SellPrice { get; }
    }

    public sealed class ShopPanelViewData
    {
        public ShopPanelViewData(
            string titleKey,
            int gold,
            IEnumerable<ShopOfferBinding> buyOffers,
            IEnumerable<ShopSellBinding> sellItems,
            string errorKey)
        {
            TitleKey = titleKey ?? string.Empty;
            Gold = gold;
            BuyOffers = (buyOffers ?? Array.Empty<ShopOfferBinding>()).ToArray();
            SellItems = (sellItems ?? Array.Empty<ShopSellBinding>()).ToArray();
            ErrorKey = errorKey ?? string.Empty;
        }

        public string TitleKey { get; }
        public int Gold { get; }
        public IReadOnlyList<ShopOfferBinding> BuyOffers { get; }
        public IReadOnlyList<ShopSellBinding> SellItems { get; }
        public string ErrorKey { get; }
    }

    public sealed class ShopPanelView : MonoBehaviour, IShopPanelView
    {
        private enum ShopTab
        {
            Buy,
            Sell
        }

        private GameObject panelRoot;
        private Text titleLabel;
        private Text goldLabel;
        private Text errorLabel;
        private RectTransform listRoot;
        private Button buyTabButton;
        private Button sellTabButton;
        private Button closeButton;
        private readonly List<Button> listButtons = new();
        private ShopTab activeTab;
        private ShopPanelViewData data;

        public event Action<string> BuyRequested;
        public event Action<string> SellRequested;
        public event Action CloseRequested;

        private void Awake()
        {
            EnsureBuilt();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            ClearListButtonListeners();
            RemoveButtonListeners(buyTabButton);
            RemoveButtonListeners(sellTabButton);
            RemoveButtonListeners(closeButton);
        }

        public void SetVisible(bool visible)
        {
            EnsureBuilt();
            panelRoot.SetActive(visible);
        }

        public void Render(ShopPanelViewData value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            EnsureBuilt();
            data = value;
            titleLabel.text = value.TitleKey;
            goldLabel.text = WorldTextKeys.ShopGold + ": " + value.Gold;
            errorLabel.text = value.ErrorKey;
            RebuildList();
        }

        private void SelectBuyTab()
        {
            activeTab = ShopTab.Buy;
            RebuildList();
        }

        private void SelectSellTab()
        {
            activeTab = ShopTab.Sell;
            RebuildList();
        }

        private void RebuildList()
        {
            if (listRoot == null) return;
            ClearListButtonListeners();
            for (var index = listRoot.childCount - 1; index >= 0; index--)
            {
                var child = listRoot.GetChild(index).gameObject;
                child.SetActive(false);
                WorldPanelViewFactory.DestroyForMode(child);
            }

            if (data == null) return;
            if (activeTab == ShopTab.Buy)
            {
                if (data.BuyOffers.Count == 0)
                {
                    WorldPanelViewFactory.CreateText(
                        listRoot,
                        "Empty",
                        WorldTextKeys.ShopEmpty,
                        18,
                        Vector2.zero,
                        Vector2.one,
                        TextAnchor.MiddleCenter);
                    return;
                }

                for (var index = 0; index < data.BuyOffers.Count; index++)
                {
                    var offer = data.BuyOffers[index];
                    var button = WorldPanelViewFactory.CreateButton(
                        listRoot,
                        "Buy_" + index,
                        offer.ItemKey + "  " + WorldTextKeys.ShopBuyPrice + ": " + offer.BuyPrice,
                        new Vector2(0f, 1f),
                        new Vector2(1f, 1f),
                        () => BuyRequested?.Invoke(offer.OfferId));
                    var rect = button.GetComponent<RectTransform>();
                    listButtons.Add(button);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.offsetMin = new Vector2(0f, -((index + 1) * 42f));
                    rect.offsetMax = new Vector2(0f, -(index * 42f));
                }
                return;
            }

            if (data.SellItems.Count == 0)
            {
                WorldPanelViewFactory.CreateText(
                    listRoot,
                    "Empty",
                    WorldTextKeys.ShopEmpty,
                    18,
                    Vector2.zero,
                    Vector2.one,
                    TextAnchor.MiddleCenter);
                return;
            }

            for (var index = 0; index < data.SellItems.Count; index++)
            {
                var item = data.SellItems[index];
                var button = WorldPanelViewFactory.CreateButton(
                    listRoot,
                    "Sell_" + index,
                    item.ItemKey + "  " + WorldTextKeys.ShopSellPrice + ": " + item.SellPrice,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    () => SellRequested?.Invoke(item.InstanceId));
                var rect = button.GetComponent<RectTransform>();
                listButtons.Add(button);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(0f, -((index + 1) * 42f));
                rect.offsetMax = new Vector2(0f, -(index * 42f));
            }
        }

        private void EnsureBuilt()
        {
            if (panelRoot != null) return;

            panelRoot = WorldPanelViewFactory.CreatePanel(transform, "ShopPanel");
            titleLabel = WorldPanelViewFactory.CreateText(
                panelRoot.transform,
                "Title",
                WorldTextKeys.ShopTitle,
                22,
                new Vector2(0.08f, 0.86f),
                new Vector2(0.92f, 0.96f),
                TextAnchor.MiddleLeft);
            goldLabel = WorldPanelViewFactory.CreateText(
                panelRoot.transform,
                "Gold",
                WorldTextKeys.ShopGold,
                18,
                new Vector2(0.08f, 0.78f),
                new Vector2(0.45f, 0.85f),
                TextAnchor.MiddleLeft);
            errorLabel = WorldPanelViewFactory.CreateText(
                panelRoot.transform,
                "Error",
                string.Empty,
                16,
                new Vector2(0.08f, 0.7f),
                new Vector2(0.92f, 0.77f),
                TextAnchor.MiddleLeft);
            errorLabel.color = new Color(1f, 0.45f, 0.45f, 1f);
            buyTabButton = WorldPanelViewFactory.CreateButton(
                panelRoot.transform,
                "BuyTab",
                WorldTextKeys.ShopBuyTab,
                new Vector2(0.08f, 0.64f),
                new Vector2(0.28f, 0.71f),
                SelectBuyTab);
            sellTabButton = WorldPanelViewFactory.CreateButton(
                panelRoot.transform,
                "SellTab",
                WorldTextKeys.ShopSellTab,
                new Vector2(0.31f, 0.64f),
                new Vector2(0.51f, 0.71f),
                SelectSellTab);
            closeButton = WorldPanelViewFactory.CreateButton(
                panelRoot.transform,
                "Close",
                WorldTextKeys.ShopClose,
                new Vector2(0.78f, 0.64f),
                new Vector2(0.92f, 0.71f),
                () => CloseRequested?.Invoke());

            var list = new GameObject("List", typeof(RectTransform));
            list.transform.SetParent(panelRoot.transform, false);
            listRoot = list.GetComponent<RectTransform>();
            listRoot.anchorMin = new Vector2(0.08f, 0.08f);
            listRoot.anchorMax = new Vector2(0.92f, 0.6f);
            listRoot.offsetMin = Vector2.zero;
            listRoot.offsetMax = Vector2.zero;
            activeTab = ShopTab.Buy;
            panelRoot.SetActive(false);
        }

        private static void RemoveButtonListeners(Button button)
        {
            if (button != null)
                button.onClick.RemoveAllListeners();
        }

        private void ClearListButtonListeners()
        {
            foreach (var button in listButtons)
                RemoveButtonListeners(button);
            listButtons.Clear();
        }
    }
}
