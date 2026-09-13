using System;
using System.Collections.Generic;

namespace BorderValley.Battle.Domain
{
    public static class GridPathfinder
    {
        /// <summary>
        /// Finds all cells reachable from <paramref name="start"/> within the movement budget.
        /// The acting unit may be present in <paramref name="occupied"/>; its own start cell remains reachable.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="map"/> or <paramref name="occupied"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="movement"/> is negative, or <paramref name="start"/> is outside the map.</exception>
        /// <exception cref="ArgumentException"><paramref name="start"/> is an obstacle.</exception>
        public static IReadOnlyDictionary<GridPosition, int> FindReachable(
            BattleMap map,
            GridPosition start,
            int movement,
            ISet<GridPosition> occupied)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (occupied == null) throw new ArgumentNullException(nameof(occupied));
            if (movement < 0) throw new ArgumentOutOfRangeException(nameof(movement));
            if (!map.InBounds(start))
                throw new ArgumentOutOfRangeException(nameof(start), start, "Start position must be inside the map.");
            if (map.GetTerrain(start) == TerrainType.Obstacle)
                throw new ArgumentException("Start position cannot be an obstacle.", nameof(start));

            var costs = new Dictionary<GridPosition, int> { [start] = 0 };
            var queue = new Queue<GridPosition>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentCost = costs[current];
                foreach (var next in Neighbors(current))
                {
                    if (!map.InBounds(next) || map.GetTerrain(next) == TerrainType.Obstacle)
                        continue;

                    var nextCost = currentCost + map.GetMovementCost(next);
                    if (nextCost > movement || occupied.Contains(next))
                        continue;

                    if (costs.TryGetValue(next, out var known) && known <= nextCost)
                        continue;

                    costs[next] = nextCost;
                    queue.Enqueue(next);
                }
            }

            return costs;
        }

        private static IEnumerable<GridPosition> Neighbors(GridPosition position)
        {
            yield return new GridPosition(position.X + 1, position.Y);
            yield return new GridPosition(position.X - 1, position.Y);
            yield return new GridPosition(position.X, position.Y + 1);
            yield return new GridPosition(position.X, position.Y - 1);
        }
    }
}