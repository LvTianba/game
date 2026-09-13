using System.Collections.Generic;

namespace BorderValley.Battle.Domain
{
    public static class GridPathfinder
    {
        public static IReadOnlyDictionary<GridPosition, int> FindReachable(
            BattleMap map,
            GridPosition start,
            int movement,
            ISet<GridPosition> occupied)
        {
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