using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BorderValley.Data.Items
{
    [Serializable]
    public sealed class LootEntry
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField] private int weight;

        public LootEntry()
        {
        }

        public LootEntry(ItemDefinition item, int weight)
        {
            this.item = item;
            this.weight = weight;
        }

        public ItemDefinition Item => item;
        public int Weight => weight;
    }

    [Serializable]
    public sealed class RarityWeight
    {
        [SerializeField] private ItemRarity rarity;
        [SerializeField] private int weight;

        public RarityWeight()
        {
        }

        public RarityWeight(ItemRarity rarity, int weight)
        {
            this.rarity = rarity;
            this.weight = weight;
        }

        public ItemRarity Rarity => rarity;
        public int Weight => weight;
    }

    [CreateAssetMenu(menuName = "BorderValley/Items/Item Drop Table", fileName = "ItemDropTableDefinition")]
    public sealed class ItemDropTableDefinition : ContentDefinition
    {
        [SerializeField] private int minItemLevel = 1;
        [SerializeField] private int maxItemLevel = 10;
        [SerializeField] private LootEntry[] entries = Array.Empty<LootEntry>();
        [SerializeField] private RarityWeight[] rarityWeights = Array.Empty<RarityWeight>();

        public int MinItemLevel => minItemLevel;
        public int MaxItemLevel => maxItemLevel;
        public LootEntry[] Entries => entries;
        public RarityWeight[] RarityWeights => rarityWeights;

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            int minItemLevel,
            int maxItemLevel,
            IEnumerable<LootEntry> entries,
            IEnumerable<RarityWeight> rarityWeights)
        {
            EditorSetId(id);
            this.minItemLevel = minItemLevel;
            this.maxItemLevel = maxItemLevel;
            this.entries = (entries ?? Array.Empty<LootEntry>()).ToArray();
            this.rarityWeights = (rarityWeights ?? Array.Empty<RarityWeight>()).ToArray();
        }
#endif
    }
}
