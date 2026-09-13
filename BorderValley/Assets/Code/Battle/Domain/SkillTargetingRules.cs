using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Battle.Domain
{
    internal static class SkillTargetingRules
    {
        internal static IReadOnlyList<BattleUnit> GetValidTargets(
            BattleState state,
            BattleUnit actor,
            GridPosition origin,
            SkillDefinition skill)
        {
            if (!actor.IsAlive) return Array.Empty<BattleUnit>();

            if (skill.Targeting == SkillTargeting.Ground)
            {
                var validPositions = GetValidGroundTargets(state, origin, skill).ToHashSet();
                return state.LivingUnits
                    .Where(unit => validPositions.Contains(unit.Position))
                    .ToArray();
            }

            if (skill.Targeting == SkillTargeting.Self)
                return new[] { actor };

            var candidates = skill.Targeting switch
            {
                SkillTargeting.Ally => state.LivingUnits.Where(unit =>
                    unit.Team == actor.Team && !ReferenceEquals(unit, actor)),
                SkillTargeting.Enemy => state.LivingUnits.Where(unit => unit.Team != actor.Team),
                _ => throw new ArgumentOutOfRangeException(nameof(skill.Targeting))
            };

            var requiresLineOfSight = RequiresLineOfSight(skill);
            return candidates
                .Where(unit =>
                    state.Map.InBounds(unit.Position) &&
                    ManhattanDistance(origin, unit.Position) <= skill.Range &&
                    (!requiresLineOfSight || HasLineOfSight(state.Map, origin, unit.Position)))
                .ToArray();
        }

        internal static IReadOnlyList<GridPosition> GetValidGroundTargets(
            BattleState state,
            GridPosition origin,
            SkillDefinition skill)
        {
            if (skill.Targeting != SkillTargeting.Ground)
                return Array.Empty<GridPosition>();

            var requiresLineOfSight = RequiresLineOfSight(skill);
            var valid = new List<GridPosition>();
            for (var y = 0; y < state.Map.Height; y++)
            {
                for (var x = 0; x < state.Map.Width; x++)
                {
                    var position = new GridPosition(x, y);
                    if (ManhattanDistance(origin, position) > skill.Range)
                        continue;
                    if (state.Map.GetTerrain(position) == TerrainType.Obstacle)
                        continue;
                    if (requiresLineOfSight &&
                        !HasLineOfSight(state.Map, origin, position))
                    {
                        continue;
                    }

                    valid.Add(position);
                }
            }

            return valid;
        }

        internal static IReadOnlyList<BattleUnit> GetEffectTargets(
            BattleState state,
            BattleUnit actor,
            SkillDefinition skill,
            GridPosition anchor,
            BattleUnit unitTarget,
            SkillEffectDefinition effect,
            GridPosition? actorPositionOverride = null)
        {
            if (skill.Radius == 0)
            {
                if (unitTarget != null)
                {
                    return MatchesEffectTarget(effect, actor.Team, unitTarget)
                        ? new[] { unitTarget }
                        : Array.Empty<BattleUnit>();
                }

                return state.LivingUnits
                    .Where(unit =>
                        PositionOf(unit, actor, actorPositionOverride) == anchor &&
                        MatchesEffectTarget(effect, actor.Team, unit))
                    .ToArray();
            }

            return state.LivingUnits
                .Where(unit =>
                    ManhattanDistance(
                        PositionOf(unit, actor, actorPositionOverride),
                        anchor) <= skill.Radius &&
                    MatchesEffectTarget(effect, actor.Team, unit))
                .ToArray();
        }

        internal static bool MatchesEffectTarget(
            SkillEffectDefinition effect,
            Team actorTeam,
            BattleUnit unit)
        {
            return effect.Kind switch
            {
                SkillEffectKind.Damage or SkillEffectKind.Push or SkillEffectKind.Pull =>
                    unit.Team != actorTeam,
                SkillEffectKind.Heal =>
                    unit.Team == actorTeam,
                SkillEffectKind.ApplyStatus when effect.StatusType == StatusType.Shielded =>
                    unit.Team == actorTeam,
                SkillEffectKind.ApplyStatus =>
                    unit.Team != actorTeam,
                _ => throw new ArgumentOutOfRangeException(nameof(effect.Kind))
            };
        }

        internal static int ManhattanDistance(GridPosition left, GridPosition right) =>
            Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);

        internal static bool HasLineOfSight(
            BattleMap map,
            GridPosition start,
            GridPosition end)
        {
            if (ManhattanDistance(start, end) <= 1) return true;

            var x = start.X;
            var y = start.Y;
            var deltaX = Math.Abs(end.X - start.X);
            var deltaY = -Math.Abs(end.Y - start.Y);
            var stepX = start.X < end.X ? 1 : -1;
            var stepY = start.Y < end.Y ? 1 : -1;
            var error = deltaX + deltaY;

            while (true)
            {
                var isEndpoint = (x == start.X && y == start.Y) || (x == end.X && y == end.Y);
                if (!isEndpoint)
                {
                    var terrain = map.GetTerrain(new GridPosition(x, y));
                    if (terrain is TerrainType.Obstacle or TerrainType.Bush)
                        return false;
                }

                if (x == end.X && y == end.Y) break;

                var doubledError = 2 * error;
                if (doubledError >= deltaY)
                {
                    error += deltaY;
                    x += stepX;
                }

                if (doubledError <= deltaX)
                {
                    error += deltaX;
                    y += stepY;
                }
            }

            return true;
        }

        private static GridPosition PositionOf(
            BattleUnit unit,
            BattleUnit actor,
            GridPosition? actorPositionOverride) =>
            ReferenceEquals(unit, actor) && actorPositionOverride.HasValue
                ? actorPositionOverride.Value
                : unit.Position;

        private static bool RequiresLineOfSight(SkillDefinition skill) => skill.Range > 1;
    }
}
