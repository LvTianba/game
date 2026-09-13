using System.Collections.Generic;
using BorderValley.Battle.Domain;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class GridPathfinderTests
    {
        [Test]
        public void FindReachable_StopsAtObstacles()
        {
            var map = new BattleMap(3, 3, new[]
            {
                TerrainType.Plain, TerrainType.Obstacle, TerrainType.Plain,
                TerrainType.Plain, TerrainType.Plain, TerrainType.Plain,
                TerrainType.Plain, TerrainType.Plain, TerrainType.Plain
            });

            var reachable = GridPathfinder.FindReachable(
                map, new GridPosition(0, 0), 2, new HashSet<GridPosition>());

            Assert.That(reachable.ContainsKey(new GridPosition(2, 0)), Is.False);
            Assert.That(reachable[new GridPosition(0, 2)], Is.EqualTo(2));
        }

        [Test]
        public void FindReachable_MudCostsTwo()
        {
            var map = new BattleMap(3, 1, new[]
            {
                TerrainType.Plain, TerrainType.Mud, TerrainType.Plain
            });

            var reachable = GridPathfinder.FindReachable(
                map, new GridPosition(0, 0), 5, new HashSet<GridPosition>());

            Assert.That(reachable[new GridPosition(1, 0)], Is.EqualTo(2));
            Assert.That(reachable[new GridPosition(2, 0)], Is.EqualTo(3));
        }

        [Test]
        public void FindReachable_ExcludesOccupiedCells()
        {
            var map = BattleMap.CreatePlain(3, 1);
            var occupied = new HashSet<GridPosition> { new GridPosition(1, 0) };

            var reachable = GridPathfinder.FindReachable(
                map, new GridPosition(0, 0), 2, occupied);

            Assert.That(reachable.ContainsKey(new GridPosition(1, 0)), Is.False);
            Assert.That(reachable.ContainsKey(new GridPosition(2, 0)), Is.False);
        }
    }
}