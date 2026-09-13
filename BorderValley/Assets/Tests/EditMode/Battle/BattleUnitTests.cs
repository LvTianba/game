using BorderValley.Battle.Domain;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class BattleUnitTests
    {
        [Test]
        public void NewUnit_StartsAtFullResources()
        {
            var stats = new UnitStats(20, 10, 6, 3, 5, 0.1f, 2);
            var unit = new BattleUnit("p1", "class.warrior", Team.Player, stats, new GridPosition(1, 1));

            Assert.That(unit.Health, Is.EqualTo(20));
            Assert.That(unit.Mana, Is.EqualTo(10));
            Assert.That(unit.IsAlive, Is.True);
            Assert.That(unit.HasMoved, Is.False);
            Assert.That(unit.HasActed, Is.False);
        }

        [Test]
        public void ApplyRawDamage_DoesNotGoBelowZero()
        {
            var unit = new BattleUnit("p1", "class.warrior",
                Team.Player, new UnitStats(5, 0, 2, 0, 1, 0f, 0), new GridPosition(0, 0));

            unit.ApplyRawDamage(99);

            Assert.That(unit.Health, Is.Zero);
            Assert.That(unit.IsAlive, Is.False);
        }

        [Test]
        public void BattleState_OccupiedPositions_ContainsOnlyLivingUnits()
        {
            var state = new BattleState(BattleMap.CreatePlain(2, 1));
            var living = new BattleUnit("p1", "class.warrior", Team.Player,
                new UnitStats(5, 0, 2, 0, 1, 0f, 0), new GridPosition(0, 0));
            var dead = new BattleUnit("e1", "enemy.slime", Team.Enemy,
                new UnitStats(1, 0, 1, 0, 1, 0f, 0), new GridPosition(1, 0));
            state.AddUnit(living);
            state.AddUnit(dead);
            dead.ApplyRawDamage(1);

            Assert.That(state.OccupiedPositions.Count, Is.EqualTo(1));
            Assert.That(state.OccupiedPositions.Contains(new GridPosition(0, 0)), Is.True);
        }
    }
}

