using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Battle.Domain
{
    public static class BattleMovement
    {
        public static IReadOnlyDictionary<GridPosition, int> FindReachableDestinations(
            BattleState state,
            BattleUnit unit)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (unit == null) throw new ArgumentNullException(nameof(unit));

            var reachable = new Dictionary<GridPosition, int>();
            if (!unit.IsAlive) return reachable;

            var occupied = state.LivingUnits
                .Where(candidate => !ReferenceEquals(candidate, unit))
                .Select(candidate => candidate.Position)
                .ToHashSet();
            var movement = Math.Max(
                0,
                unit.Stats.Speed - StatusSystem.MovementPenalty(unit));
            foreach (var pair in GridPathfinder.FindReachable(
                         state.Map,
                         unit.Position,
                         movement,
                         occupied))
            {
                if (pair.Key != unit.Position)
                    reachable.Add(pair.Key, pair.Value);
            }

            return reachable;
        }
    }
}
