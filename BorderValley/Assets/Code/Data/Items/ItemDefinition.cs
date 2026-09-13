using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Combat;
using UnityEngine;

namespace BorderValley.Data.Items
{
    [CreateAssetMenu(menuName = "BorderValley/Items/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ContentDefinition
    {
        [SerializeField] private string localizationKey = string.Empty;
        [SerializeField] private ItemSlot slot;
        [SerializeField] private string[] allowedClassIds = Array.Empty<string>();
        [SerializeField] private bool isQuestItem;
        [SerializeField] private int baseValue;
        [SerializeField] private StatValue[] stats = Array.Empty<StatValue>();

        public string LocalizationKey => localizationKey;
        public ItemSlot Slot => slot;
        public string[] AllowedClassIds => allowedClassIds;
        public bool IsQuestItem => isQuestItem;
        public int BaseValue => baseValue;
        public StatValue[] Stats => stats;

        public bool AllowsClass(string classId) =>
            allowedClassIds.Length == 0 ||
            (!string.IsNullOrWhiteSpace(classId) && allowedClassIds.Contains(classId, StringComparer.Ordinal));

        public int GetBaseStat(CombatStat stat, int itemLevel)
        {
            var found = false;
            var value = 0;
            foreach (var entry in stats)
            {
                if (entry.Stat != stat) continue;
                found = true;
                value += entry.Value;
            }

            return found ? value + Math.Max(0, itemLevel - 1) : 0;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string localizationKey,
            ItemSlot slot,
            IEnumerable<string> allowedClassIds,
            bool isQuestItem,
            int baseValue,
            IEnumerable<StatValue> stats)
        {
            EditorSetId(id);
            this.localizationKey = localizationKey ?? string.Empty;
            this.slot = slot;
            this.allowedClassIds = (allowedClassIds ?? Array.Empty<string>()).ToArray();
            this.isQuestItem = isQuestItem;
            this.baseValue = baseValue;
            this.stats = (stats ?? Array.Empty<StatValue>()).ToArray();
        }
#endif
    }
}
