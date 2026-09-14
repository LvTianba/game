using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Combat;
using UnityEngine;

namespace BorderValley.Data.Items
{
    [CreateAssetMenu(menuName = "BorderValley/Items/Character Definition", fileName = "CharacterDefinition")]
    public sealed class CharacterDefinition : ContentDefinition
    {
        [SerializeField] private string localizationKey = string.Empty;
        [SerializeField] private StatValue[] baseStats = Array.Empty<StatValue>();
        [SerializeField] private StatValue[] growthStats = Array.Empty<StatValue>();
        [SerializeField] private string[] startingSkillIds = Array.Empty<string>();
        [SerializeField] private SkillUnlock[] skillUnlocks = Array.Empty<SkillUnlock>();

        public string LocalizationKey => localizationKey;
        public StatValue[] BaseStats => baseStats;
        public StatValue[] GrowthStats => growthStats;
        public StatValue[] PerLevelGrowth => growthStats;
        public string[] StartingSkillIds => startingSkillIds;
        public string[] StartingSkills => startingSkillIds;
        public SkillUnlock[] SkillUnlocks => skillUnlocks;

        public int GetBaseStat(CombatStat stat)
        {
            var value = 0;
            foreach (var entry in baseStats)
            {
                if (entry.Stat == stat) value += entry.Value;
            }

            return value;
        }

        public int GetBaseStat(CombatStat stat, int level)
        {
            var growth = 0;
            foreach (var entry in growthStats)
            {
                if (entry.Stat == stat) growth += entry.Value;
            }

            return GetBaseStat(stat) + growth * Math.Max(0, level - 1);
        }

        public int GetGrowthStat(CombatStat stat)
        {
            var value = 0;
            foreach (var entry in growthStats)
            {
                if (entry.Stat == stat) value += entry.Value;
            }

            return value;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string localizationKey,
            IEnumerable<StatValue> baseStats,
            IEnumerable<StatValue> growthStats,
            IEnumerable<string> startingSkillIds,
            IEnumerable<SkillUnlock> skillUnlocks)
        {
            EditorSetId(id);
            this.localizationKey = localizationKey ?? string.Empty;
            this.baseStats = (baseStats ?? Array.Empty<StatValue>()).ToArray();
            this.growthStats = (growthStats ?? Array.Empty<StatValue>()).ToArray();
            this.startingSkillIds = (startingSkillIds ?? Array.Empty<string>()).ToArray();
            this.skillUnlocks = (skillUnlocks ?? Array.Empty<SkillUnlock>()).ToArray();
        }
#endif
    }
}
