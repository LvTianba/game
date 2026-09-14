using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Data;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Inventory;

namespace BorderValley.Narrative
{
    public sealed class ShopService
    {
        private readonly IReadOnlyDictionary<string, ShopDefinition> shops;
        private readonly IReadOnlyDictionary<string, NpcDefinition> shopOwners;
        private readonly IReadOnlyDictionary<string, ItemDefinition> items;
        private readonly IReadOnlyDictionary<string, AffixDefinition> affixes;
        private readonly NarrativeStateService state;
        private readonly EconomyService economy;

        public ShopService(
            IEnumerable<ContentDefinition> definitions,
            NarrativeStateService state,
            EconomyService economy)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.economy = economy ?? throw new ArgumentNullException(nameof(economy));

            var all = definitions.ToArray();
            shops = all
                .OfType<ShopDefinition>()
                .Where(shop => !string.IsNullOrWhiteSpace(shop.Id))
                .GroupBy(shop => shop.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            items = all
                .OfType<ItemDefinition>()
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            affixes = all
                .OfType<AffixDefinition>()
                .Where(affix => !string.IsNullOrWhiteSpace(affix.Id))
                .GroupBy(affix => affix.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            shopOwners = all
                .OfType<NpcDefinition>()
                .Where(npc => !string.IsNullOrWhiteSpace(npc.Id) && !string.IsNullOrWhiteSpace(npc.OpenShopId))
                .GroupBy(npc => npc.OpenShopId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(npc => npc.Id, StringComparer.Ordinal).First(),
                    StringComparer.Ordinal);
        }

        public IReadOnlyList<ShopOfferView> GetOffers(string shopId)
        {
            if (!TryGetUnlockedShop(shopId, out var shop))
                return Array.Empty<ShopOfferView>();

            var modifierBps = GetPriceModifierBps(shopId);
            var offers = new List<ShopOfferView>();
            for (var index = 0; index < shop.Offers.Length; index++)
            {
                var offer = shop.Offers[index];
                if (offer == null ||
                    state.IsOfferPurchased(shopId, offer.OfferId) ||
                    !ShopOfferFactory.TryCreate(shopId, index, offer, items, affixes, out var item, out _))
                {
                    continue;
                }

                offers.Add(new ShopOfferView(
                    offer.OfferId,
                    item,
                    economy.GetBuyPrice(item, modifierBps),
                    false));
            }
            return offers;
        }

        public bool TryBuy(string shopId, string offerId, out string error)
        {
            if (!TryGetUnlockedShop(shopId, out var shop))
            {
                error = NarrativeTextKeys.UnknownShop;
                return false;
            }

            if (!TryFindOffer(shop, offerId, out var offer, out var offerIndex) ||
                state.IsOfferPurchased(shopId, offerId))
            {
                error = NarrativeTextKeys.UnknownOffer;
                return false;
            }

            if (!ShopOfferFactory.TryCreate(
                    shopId,
                    offerIndex,
                    offer,
                    items,
                    affixes,
                    out var item,
                    out error))
            {
                return false;
            }

            var modifierBps = GetPriceModifierBps(shopId);
            if (!economy.TryBuy(item, modifierBps, out error))
                return false;

            if (!state.MarkOfferPurchased(shopId, offerId))
                throw new InvalidOperationException("Validated shop offer could not be marked purchased.");

            error = string.Empty;
            return true;
        }

        public bool TrySell(string instanceId, out string error) =>
            economy.TrySell(instanceId, 10000, out error);

        private bool TryGetUnlockedShop(string shopId, out ShopDefinition shop)
        {
            shop = null;
            return !string.IsNullOrWhiteSpace(shopId) &&
                   shops.TryGetValue(shopId, out shop) &&
                   state.IsShopUnlocked(shopId);
        }

        private static bool TryFindOffer(
            ShopDefinition shop,
            string offerId,
            out ShopOfferDefinition offer,
            out int offerIndex)
        {
            offer = null;
            offerIndex = -1;
            if (string.IsNullOrWhiteSpace(offerId)) return false;
            for (var index = 0; index < shop.Offers.Length; index++)
            {
                var candidate = shop.Offers[index];
                if (candidate == null ||
                    !string.Equals(candidate.OfferId, offerId, StringComparison.Ordinal))
                {
                    continue;
                }

                offer = candidate;
                offerIndex = index;
                return true;
            }
            return false;
        }

        private int GetPriceModifierBps(string shopId)
        {
            var favorTier = shopOwners.TryGetValue(shopId, out var owner)
                ? state.GetFavorTier(owner.Id)
                : 0;
            return 10000 - 500 * Math.Clamp(favorTier, 0, 2);
        }
    }
}
