using System;
using System.Collections.Generic;
using System.Globalization;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Inventory;

namespace BorderValley.Narrative
{
    public static class ShopOfferFactory
    {
        public static string CreateInstanceId(string shopId, int offerIndex)
        {
            if (string.IsNullOrWhiteSpace(shopId)) throw new ArgumentException(nameof(shopId));
            if (offerIndex < 0) throw new ArgumentOutOfRangeException(nameof(offerIndex));
            return shopId + ".offer." + offerIndex.ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryCreate(
            string shopId,
            int offerIndex,
            ShopOfferDefinition offer,
            IReadOnlyDictionary<string, ItemDefinition> items,
            IReadOnlyDictionary<string, AffixDefinition> affixes,
            out ItemInstance item,
            out string error)
        {
            item = null;
            error = NarrativeTextKeys.UnknownOffer;
            if (string.IsNullOrWhiteSpace(shopId) ||
                offerIndex < 0 ||
                offer == null ||
                string.IsNullOrWhiteSpace(offer.OfferId) ||
                offer.Item == null ||
                items == null ||
                affixes == null ||
                !items.TryGetValue(offer.Item.Id, out var itemDefinition) ||
                offer.ItemLevel < 1 ||
                offer.ItemLevel > 10 ||
                !Enum.IsDefined(typeof(ItemRarity), offer.Rarity))
            {
                return false;
            }

            var affixDefinitions = offer.Affixes ?? Array.Empty<AffixDefinition>();
            if (affixDefinitions.Length != ItemRules.AffixCount(offer.Rarity))
                return false;

            var affixValues = new AffixInstance[affixDefinitions.Length];
            for (var index = 0; index < affixDefinitions.Length; index++)
            {
                var definition = affixDefinitions[index];
                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.Id) ||
                    !affixes.TryGetValue(definition.Id, out var registeredDefinition))
                {
                    return false;
                }

                affixValues[index] = new AffixInstance(registeredDefinition.Id, registeredDefinition.MinValue);
            }

            var candidate = new ItemInstance(
                CreateInstanceId(shopId, offerIndex),
                itemDefinition.Id,
                offer.ItemLevel,
                offer.Rarity,
                affixValues);
            if (!ItemRules.TryValidate(candidate, itemDefinition, affixes, out _))
                return false;

            item = candidate;
            error = string.Empty;
            return true;
        }
    }
}
