using System;
using System.Collections.Generic;

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

            return SkillTargetingRules.GetValidTargets(state, actor, actor.Position, skill);
        }

        public static IEnumerable<GridPosition> GetValidGroundTargets(
            BattleState state,
            BattleUnit actor,
            SkillDefinition skill)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            if (!actor.IsAlive) return Array.Empty<GridPosition>();

            return SkillTargetingRules.GetValidGroundTargets(state, actor.Position, skill);
        }
    }
}
