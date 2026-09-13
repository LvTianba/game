using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Battle.Domain
{
    public static class SkillTargetValidator
    {
        public static IEnumerable<BattleUnit> GetValidTargets(
            BattleState state,
            BattleUnit actor,
            SkillDefinition skill)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            if (!actor.IsAlive) return Array.Empty<BattleUnit>();

            var candidates = skill.Targeting switch
            {
                SkillTargeting.Self => new[] { actor },
                SkillTargeting.Ally => state.LivingUnits.Where(unit =>
                    unit.Team == actor.Team && !ReferenceEquals(unit, actor)),
                SkillTargeting.Enemy => state.LivingUnits.Where(unit => unit.Team != actor.Team),
                SkillTargeting.Ground => state.LivingUnits.Where(unit =>
                    state.Map.InBounds(unit.Position) &&
                    state.Map.GetTerrain(unit.Position) != TerrainType.Obstacle),
                _ => throw new ArgumentOutOfRangeException(nameof(skill.Targeting))
            };

            return candidates.Where(unit =>
                state.Map.InBounds(unit.Position) &&
                Distance(actor.Position, unit.Position) <= skill.Range);
        }

        private static int Distance(GridPosition left, GridPosition right) =>
            Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
    }
}
