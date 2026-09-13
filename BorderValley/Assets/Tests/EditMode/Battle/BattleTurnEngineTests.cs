using System;
using System.Linq;
using BorderValley.Battle.Domain;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class BattleTurnEngineTests
    {
        [Test]
        public void Build_OrdersBySpeedThenOriginalOrder()
        {
            var fast = Unit("a", Team.Player, 9, 0);
            var slow = Unit("b", Team.Enemy, 3, 0);
            var tie = Unit("c", Team.Player, 3, 1);

            var order = TurnOrder.Build(new[] { slow, fast, tie }).ToArray();

            Assert.That(order.Select(unit => unit.Id), Is.EqualTo(new[] { "a", "b", "c" }));
        }

        [Test]
        public void EndTurn_AdvancesRoundAndSkipsDeadUnits()
        {
            var state = new BattleState(BattleMap.CreatePlain(3, 1));
            var first = Unit("a", Team.Player, 10, 0);
            var second = Unit("b", Team.Enemy, 5, 0);
            state.AddUnit(first);
            state.AddUnit(second);
            var engine = new BattleTurnEngine(state);
            engine.Start();
            second.ApplyRawDamage(999);

            engine.EndTurn();

            Assert.That(engine.ActiveUnit.Id, Is.EqualTo("a"));
            Assert.That(state.Round, Is.EqualTo(2));
        }

        [Test]
        public void Build_RejectsNullUnits()
        {
            Assert.Throws<ArgumentNullException>(() => TurnOrder.Build(null));
        }

        [Test]
        public void Build_RejectsNullUnitElements()
        {
            var units = new[] { Unit("a", Team.Player, 1, 0), null };

            Assert.Throws<ArgumentException>(() => TurnOrder.Build(units));
        }

        [Test]
        public void Constructor_RejectsNullState()
        {
            Assert.Throws<ArgumentNullException>(() => new BattleTurnEngine(null));
        }

        [Test]
        public void Start_RejectsBattleWithoutLivingUnits()
        {
            var state = new BattleState(BattleMap.CreatePlain(1, 1));
            var dead = Unit("a", Team.Player, 1, 0);
            dead.ApplyRawDamage(999);
            state.AddUnit(dead);
            var engine = new BattleTurnEngine(state);

            Assert.Throws<InvalidOperationException>(() => engine.Start());
            Assert.That(engine.ActiveUnit, Is.Null);
        }

        [Test]
        public void EndTurn_RejectsBattleThatHasNotStarted()
        {
            var state = new BattleState(BattleMap.CreatePlain(1, 1));
            state.AddUnit(Unit("a", Team.Player, 1, 0));
            var engine = new BattleTurnEngine(state);

            Assert.Throws<InvalidOperationException>(() => engine.EndTurn());
        }

        [Test]
        public void EndTurn_RejectsBattleWithoutLivingUnits()
        {
            var state = new BattleState(BattleMap.CreatePlain(1, 1));
            var unit = Unit("a", Team.Player, 1, 0);
            state.AddUnit(unit);
            var engine = new BattleTurnEngine(state);
            engine.Start();
            unit.ApplyRawDamage(999);

            Assert.Throws<InvalidOperationException>(() => engine.EndTurn());
        }

        private static BattleUnit Unit(string id, Team team, int speed, int x) =>
            new BattleUnit(id, "test.unit", team,
                new UnitStats(10, 0, 1, 0, speed, 0f, 0), new GridPosition(x, 0));
    }
}
