using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Combat;
using BorderValley.Data.Items;

namespace BorderValley.Inventory
{
    public sealed class EquipmentStatAggregator
    {
        private readonly InventoryService inventory;
        private readonly IReadOnlyDictionary<string, ItemDefinition> items;
        private readonly IReadOnlyDictionary<string, AffixDefinition> affixes;

        public EquipmentStatAggregator(
            InventoryService inventory,
            IReadOnlyDictionary<string, ItemDefinition> items,
            IReadOnlyDictionary<string, AffixDefinition> affixes)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.items = items ?? throw new ArgumentNullException(nameof(items));
            this.affixes = affixes ?? throw new ArgumentNullException(nameof(affixes));
        }

        public AggregateResult Aggregate(CharacterDefinition character, int level, string memberId = null)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            level = Math.Clamp(level, 1, PartyProgressionService.MaxLevel);

            var raw = Stats();
            var flat = Stats();
            var percent = Stats();
            var modifiers = new Dictionary<(string SkillId, SkillModifierKind Kind), int>();
            var passives = new List<BattlePassiveSnapshot>();

            foreach (var stat in AllStats())
                raw[stat] = character.GetBaseStat(stat, level);

            foreach (var pair in inventory.GetEquipped(memberId)
                         .OrderBy(pair => pair.Key)
                         .ThenBy(pair => pair.Value, StringComparer.Ordinal))
            {
                var item = inventory.GetItem(pair.Value);
                if (!items.TryGetValue(item.ItemDefinitionId, out var itemDefinition)) continue;
                if (!itemDefinition.AllowsClass(character.Id)) continue;

                foreach (var stat in AllStats())
                    raw[stat] += itemDefinition.GetBaseStat(stat, item.ItemLevel);

                foreach (var affixInstance in item.Affixes)
                {
                    if (!affixes.TryGetValue(affixInstance.AffixId, out var affix)) continue;
                    switch (affix.EffectKind)
                    {
                        case AffixEffectKind.FlatStat:
                            flat[affix.Stat] += affixInstance.Value;
                            break;
                        case AffixEffectKind.PercentStat:
                            percent[affix.Stat] += affixInstance.Value;
                            break;
                        case AffixEffectKind.SkillModifier:
                            if (string.IsNullOrWhiteSpace(affix.TargetSkillId))
                                throw new InvalidOperationException("Skill modifier affix " + affix.Id + " must define TargetSkillId.");
                            var key = (affix.TargetSkillId, affix.SkillModifier);
                            modifiers.TryGetValue(key, out var current);
                            modifiers[key] = current + affixInstance.Value;
                            break;
                        case AffixEffectKind.Trigger:
                        case AffixEffectKind.Conditional:
                            passives.Add(new BattlePassiveSnapshot(
                                affix.PassiveEffect,
                                affixInstance.Value,
                                affix.Duration));
                            break;
                    }
                }
            }

            var stats = Stats();
            foreach (var stat in AllStats())
            {
                var value = raw[stat] + flat[stat];
                var multiplier = 1f + percent[stat] / 10000f;
                stats[stat] = Math.Max(0, (int)Math.Floor(value * multiplier));
            }

            return new AggregateResult(stats, modifiers, passives);
        }

        public static AggregateResult Aggregate(
            InventoryService inventory,
            CharacterDefinition character,
            int level,
            IReadOnlyDictionary<string, ItemDefinition> items,
            IReadOnlyDictionary<string, AffixDefinition> affixes,
            string memberId = null) =>
            new EquipmentStatAggregator(inventory, items, affixes).Aggregate(character, level, memberId);

        private static Dictionary<CombatStat, int> Stats() =>
            AllStats().ToDictionary(stat => stat, _ => 0);

        private static CombatStat[] AllStats() =>
            (CombatStat[])Enum.GetValues(typeof(CombatStat));

        public sealed class AggregateResult
        {
            private readonly IReadOnlyDictionary<CombatStat, int> stats;

            internal AggregateResult(
                IReadOnlyDictionary<CombatStat, int> stats,
                IReadOnlyDictionary<(string SkillId, SkillModifierKind Kind), int> modifiers,
                IEnumerable<BattlePassiveSnapshot> passives)
            {
                this.stats = new ReadOnlyDictionary<CombatStat, int>(
                    new Dictionary<CombatStat, int>(stats));
                SkillModifiers = Array.AsReadOnly(modifiers
                    .OrderBy(pair => pair.Key.SkillId, StringComparer.Ordinal)
                    .ThenBy(pair => pair.Key.Kind)
                    .Select(pair => new BattleSkillModifierSnapshot(
                        pair.Key.SkillId,
                        pair.Key.Kind,
                        pair.Value))
                    .ToArray());
                Passives = Array.AsReadOnly((passives ?? Array.Empty<BattlePassiveSnapshot>()).ToArray());
            }

            public IReadOnlyDictionary<CombatStat, int> Stats => stats;
            public IReadOnlyList<BattleSkillModifierSnapshot> SkillModifiers { get; }
            public IReadOnlyList<BattlePassiveSnapshot> Passives { get; }
            public int MaxHealth => Get(CombatStat.MaxHealth);
            public int MaxMana => Get(CombatStat.MaxMana);
            public int Power => Get(CombatStat.Power);
            public int Armor => Get(CombatStat.Armor);
            public int Speed => Get(CombatStat.Speed);
            public int CritChanceBps => Get(CombatStat.CritChanceBps);
            public int Resistance => Get(CombatStat.Resistance);
            public int Get(CombatStat stat) => stats.TryGetValue(stat, out var value) ? value : 0;
        }
    }
}
