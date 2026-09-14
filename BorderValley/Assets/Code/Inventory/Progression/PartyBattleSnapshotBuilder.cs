using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Combat;
using BorderValley.Data.Items;

namespace BorderValley.Inventory
{
    public sealed class PartyBattleSnapshotBuilder
    {
        private readonly PartyProgressionService progression;
        private readonly InventoryService inventory;
        private readonly IReadOnlyDictionary<string, ItemDefinition> items;
        private readonly IReadOnlyDictionary<string, AffixDefinition> affixes;

        public PartyBattleSnapshotBuilder(
            PartyProgressionService progression,
            InventoryService inventory,
            IReadOnlyDictionary<string, ItemDefinition> items,
            IReadOnlyDictionary<string, AffixDefinition> affixes)
        {
            this.progression = progression ?? throw new ArgumentNullException(nameof(progression));
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.items = items ?? throw new ArgumentNullException(nameof(items));
            this.affixes = affixes ?? throw new ArgumentNullException(nameof(affixes));
        }

        public BattleRequest Build(BattleRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return new BattleRequest(
                request.ScenarioId,
                request.Seed,
                request.ReturnScene,
                BuildPartySnapshot());
        }

        public BattlePartySnapshot BuildPartySnapshot()
        {
            var aggregator = new EquipmentStatAggregator(inventory, items, affixes);
            var members = progression.Members
                .Select(member => BuildMember(aggregator, member))
                .Where(member => member != null)
                .ToArray();
            return new BattlePartySnapshot(members);
        }

        private BattleCombatantSnapshot BuildMember(
            EquipmentStatAggregator aggregator,
            PartyMemberState member)
        {
            var character = progression.GetCharacter(member.CharacterId);
            if (character == null) return null;
            var equipment = aggregator.Aggregate(character, member.Level);
            var skillIds = progression.GetUnlockedSkillIds(member);
            var modifiers = equipment.SkillModifiers.ToDictionary(
                modifier => (modifier.SkillId, modifier.Kind),
                modifier => modifier.Value);
            foreach (var skillId in skillIds)
            {
                if (!member.SkillRanks.TryGetValue(skillId, out var rank) || rank <= 0) continue;
                var key = (skillId, SkillModifierKind.Range);
                modifiers.TryGetValue(key, out var current);
                modifiers[key] = current + rank - 1;
            }

            return new BattleCombatantSnapshot(
                member.MemberId,
                character.Id,
                character.Id,
                equipment.MaxHealth,
                equipment.MaxMana,
                equipment.Power,
                equipment.Armor,
                equipment.Speed,
                equipment.CritChanceBps,
                equipment.Resistance,
                member.CurrentHealth,
                member.CurrentMana,
                skillIds,
                modifiers.OrderBy(pair => pair.Key.Item1, StringComparer.Ordinal)
                    .ThenBy(pair => pair.Key.Item2)
                    .Select(pair => new BattleSkillModifierSnapshot(pair.Key.Item1, pair.Key.Item2, pair.Value)),
                equipment.Passives);
        }
    }
}
