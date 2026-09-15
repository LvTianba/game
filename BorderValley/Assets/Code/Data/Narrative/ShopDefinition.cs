using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace BorderValley.Data.Narrative
{
    [CreateAssetMenu(menuName = "BorderValley/Narrative/Shop Definition", fileName = "ShopDefinition")]
    public sealed class ShopDefinition : ContentDefinition
    {
        [SerializeField] private string localizationKey = string.Empty;
        [SerializeField] private string requiredEventId = string.Empty;
        [SerializeField] private ShopOfferDefinition[] offers = Array.Empty<ShopOfferDefinition>();

        public string LocalizationKey => localizationKey;
        public string DisplayNameKey => localizationKey;
        public string RequiredEventId => requiredEventId;
        public ShopOfferDefinition[] Offers => offers;

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string localizationKey,
            string requiredEventId,
            IEnumerable<ShopOfferDefinition> offers)
        {
            EditorSetId(id);
            this.localizationKey = localizationKey ?? string.Empty;
            this.requiredEventId = requiredEventId ?? string.Empty;
            this.offers = (offers ?? Array.Empty<ShopOfferDefinition>()).ToArray();
        }
#endif
    }
}
