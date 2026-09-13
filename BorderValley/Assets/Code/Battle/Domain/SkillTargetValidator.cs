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

            if (skill.Targeting == SkillTargeting.Ground)
            {
                var validPositions = GetValidGroundTargets(state, actor, skill).ToHashSet();
                return state.LivingUnits
                    .Where(unit => validPositions.Contains(unit.Position))
                    .ToArray();
            }

            var candidates = skill.Targeting switch
            {
                SkillTargeting.Self => new[] { actor },
                SkillTargeting.Ally => state.LivingUnits.Where(unit =>
                    unit.Team == actor.Team && !ReferenceEquals(unit, actor)),
                SkillTargeting.Enemy => state.LivingUnits.Where(unit => unit.Team != actor.Team),
                _ => throw new ArgumentOutOfRangeException(nameof(skill.Targeting))
            };

            var requiresLineOfSight = RequiresLineOfSight(skill);
            return candidates
                .Where(unit =>
                    state.Map.InBounds(unit.Position) &&
                    ManhattanDistance(actor.Position, unit.Position) <= skill.Range &&
                    (!requiresLineOfSight || HasLineOfSight(state.Map, actor.Position, unit.Position)))
                .ToArray();
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
            if (skill.Targeting != SkillTargeting.Ground) return Array.Empty<GridPosition>();

            var requiresLineOfSight = RequiresLineOfSight(skill);
            var valid = new List<GridPosition>();
            for (var y = 0; y < state.Map.Height; y++)
            {
                for (var x = 0; x < state.Map.Width; x++)
                {
                    var position = new GridPosition(x, y);
                    if (ManhattanDistance(actor.Position, position) > skill.Range)
                        continue;
                    if (state.Map.GetTerrain(position) == TerrainType.Obstacle)
                        continue;
                    if (requiresLineOfSight &&
                        !HasLineOfSight(state.Map, actor.Position, position))
                    {
                        continue;
                    }

                    valid.Add(position);
                }
            }

            return valid;
        }

        private static bool RequiresLineOfSight(SkillDefinition skill) => skill.Range > 1;

        private static bool HasLineOfSight(
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

        private static int ManhattanDistance(GridPosition left, GridPosition right) =>
            Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
    }
}
