using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace BorderValley.Data.Narrative
{
    [Serializable]
    public sealed class ShopOfferDefinition
    {
        [SerializeField] private string offerId = string.Empty;
        [SerializeField] private Data.Items.ItemDefinition item;
        [SerializeField] private Data.Items.ItemRarity rarity;
        [SerializeField] private int itemLevel = 1;
        [SerializeField] private Data.Items.AffixDefinition[] affixes = Array.Empty<Data.Items.AffixDefinition>();

        public ShopOfferDefinition()
        {
        }

        public ShopOfferDefinition(
            string offerId,
            Data.Items.ItemDefinition item,
            Data.Items.ItemRarity rarity,
            int itemLevel,
            IEnumerable<Data.Items.AffixDefinition> affixes)
        {
            EditorConfigure(offerId, item, rarity, itemLevel, affixes);
        }

        public string OfferId => offerId;
        public string Id => offerId;
        public Data.Items.ItemDefinition Item => item;
        public Data.Items.ItemRarity Rarity => rarity;
        public int ItemLevel => itemLevel;
        public Data.Items.AffixDefinition[] Affixes => affixes;
        public Data.Items.AffixDefinition[] AffixDefinitions => affixes;

        public void EditorConfigure(
            string offerId,
            Data.Items.ItemDefinition item,
            Data.Items.ItemRarity rarity,
            int itemLevel,
            IEnumerable<Data.Items.AffixDefinition> affixes)
        {
            this.offerId = offerId ?? string.Empty;
            this.item = item;
            this.rarity = rarity;
            this.itemLevel = itemLevel;
            this.affixes = (affixes ?? Array.Empty<Data.Items.AffixDefinition>()).ToArray();
        }
    }
}
