using BorderValley.Battle.Domain;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class BattleMovementTests
    {
        [Test]
        public void FindReachableDestinations_ExcludesOriginAndOccupiedCells()
        {
            var state = new BattleState(BattleMap.CreatePlain(3, 2));
            var actor = CreateUnit("actor", Team.Player, 2, new GridPosition(0, 0));
            var enemy = CreateUnit("enemy", Team.Enemy, 0, new GridPosition(1, 0));
            state.AddUnit(actor);
            state.AddUnit(enemy);

            var reachable = BattleMovement.FindReachableDestinations(state, actor);

            Assert.That(reachable.ContainsKey(actor.Position), Is.False);
            Assert.That(reachable.ContainsKey(enemy.Position), Is.False);
            Assert.That(reachable.ContainsKey(new GridPosition(0, 1)), Is.True);
        }

        [Test]
        public void FindReachableDestinations_AppliesMovementPenalty()
        {
            var state = new BattleState(BattleMap.CreatePlain(2, 1));
            var actor = CreateUnit("actor", Team.Player, 2, new GridPosition(0, 0));
            state.AddUnit(actor);
            StatusSystem.Apply(actor, StatusType.Slowed, 2, 2, "test");

            var reachable = BattleMovement.FindReachableDestinations(state, actor);

            Assert.That(reachable, Is.Empty);
        }

        private static BattleUnit CreateUnit(
            string id,
            Team team,
            int speed,
            GridPosition position)
        {
            return new BattleUnit(
                id,
                id,
                team,
                new UnitStats(10, 0, 1, 0, speed, 0f, 0),
                position);
        }
    }
}
