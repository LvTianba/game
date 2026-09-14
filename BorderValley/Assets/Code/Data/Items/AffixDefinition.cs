using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Combat;
using UnityEngine;

namespace BorderValley.Data.Items
{
    [CreateAssetMenu(menuName = "BorderValley/Items/Affix Definition", fileName = "AffixDefinition")]
    public sealed class AffixDefinition : ContentDefinition
    {
        [SerializeField] private string localizationKey = string.Empty;
        [SerializeField] private ItemSlot[] compatibleSlots = Array.Empty<ItemSlot>();
        [SerializeField] private ItemRarity minimumRarity;
        [SerializeField] private AffixEffectKind effectKind;
        [SerializeField] private CombatStat stat;
        [SerializeField] private SkillModifierKind skillModifier;
        [SerializeField] private string targetSkillId = string.Empty;
        [SerializeField] private PassiveEffectKind passiveEffect;
        [SerializeField] private int minValue;
        [SerializeField] private int maxValue;
        [SerializeField] private int minItemLevel;
        [SerializeField] private int duration;
        [SerializeField] private int weight;
        [SerializeField] private int budgetCost;
        [SerializeField] private string budgetCostKey = string.Empty;
        [SerializeField] private string[] mutuallyExclusiveAffixIds = Array.Empty<string>();

        public string LocalizationKey => localizationKey;
        public ItemSlot[] CompatibleSlots => compatibleSlots;
        public ItemRarity MinimumRarity => minimumRarity;
        public AffixEffectKind EffectKind => effectKind;
        public CombatStat Stat => stat;
        public SkillModifierKind SkillModifier => skillModifier;
        public string TargetSkillId => targetSkillId;
        public PassiveEffectKind PassiveEffect => passiveEffect;
        public int MinValue => minValue;
        public int MaxValue => maxValue;
        public int MinItemLevel => minItemLevel;
        public int Duration => duration;
        public int Weight => weight;
        public int BudgetCost => budgetCost;
        public string BudgetCostKey => budgetCostKey;
        public string[] MutuallyExclusiveAffixIds => mutuallyExclusiveAffixIds;

        public bool Supports(ItemSlot slot, ItemRarity rarity, int itemLevel) =>
            compatibleSlots.Contains(slot) && rarity >= minimumRarity && itemLevel >= minItemLevel;

        public bool IsMutuallyExclusive(string otherAffixId) =>
            !string.IsNullOrWhiteSpace(otherAffixId) &&
            mutuallyExclusiveAffixIds.Contains(otherAffixId, StringComparer.Ordinal);

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string localizationKey,
            ItemSlot[] compatibleSlots,
            ItemRarity minimumRarity,
            AffixEffectKind effectKind,
            CombatStat stat,
            SkillModifierKind skillModifier,
            PassiveEffectKind passiveEffect,
            int minValue,
            int maxValue,
            int minItemLevel,
            int duration,
            int weight,
            int budgetCost,
            string[] mutuallyExclusiveAffixIds)
        {
            EditorSetId(id);
            this.localizationKey = localizationKey ?? string.Empty;
            this.compatibleSlots = compatibleSlots ?? Array.Empty<ItemSlot>();
            this.minimumRarity = minimumRarity;
            this.effectKind = effectKind;
            this.stat = stat;
            this.skillModifier = skillModifier;
            this.passiveEffect = passiveEffect;
            this.minValue = minValue;
            this.maxValue = maxValue;
            this.minItemLevel = minItemLevel;
            this.duration = duration;
            this.weight = weight;
            this.budgetCost = budgetCost;
            this.budgetCostKey = string.Empty;
            this.mutuallyExclusiveAffixIds = mutuallyExclusiveAffixIds ?? Array.Empty<string>();
        }

        public void EditorConfigure(
            string id,
            string localizationKey,
            ItemSlot[] compatibleSlots,
            ItemRarity minimumRarity,
            AffixEffectKind effectKind,
            CombatStat stat,
            SkillModifierKind skillModifier,
            PassiveEffectKind passiveEffect,
            int minValue,
            int maxValue,
            int minItemLevel,
            int duration,
            string budgetCostKey,
            string[] mutuallyExclusiveAffixIds)
        {
            EditorConfigure(
                id,
                localizationKey,
                compatibleSlots,
                minimumRarity,
                effectKind,
                stat,
                skillModifier,
                passiveEffect,
                minValue,
                maxValue,
                minItemLevel,
                duration,
                1,
                1,
                mutuallyExclusiveAffixIds);
            this.budgetCostKey = budgetCostKey ?? string.Empty;
        }

        public void EditorSetTargetSkillId(string targetSkillId)
        {
            this.targetSkillId = targetSkillId ?? string.Empty;
        }
#endif
    }
}
