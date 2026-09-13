using BorderValley.Battle.Domain;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class StatusSystemTests
    {
        [TestCase(StatusType.Burning)]
        [TestCase(StatusType.Poisoned)]
        [TestCase(StatusType.Shielded)]
        public void Apply_StackingStatusesCombineMagnitude(StatusType type)
        {
            var unit = Unit();

            StatusSystem.Apply(unit, type, 2, 2, "a");
            StatusSystem.Apply(unit, type, 3, 3, "b");

            Assert.That(unit.Statuses.Count, Is.EqualTo(1));
            Assert.That(unit.Statuses[0].Magnitude, Is.EqualTo(5));
            Assert.That(unit.Statuses[0].RemainingTurns, Is.EqualTo(3));
        }

        [Test]
        public void Apply_ControlStatusUsesStrongestMagnitudeAndLongestDuration()
        {
            var unit = Unit();

            StatusSystem.Apply(unit, StatusType.Stunned, 1, 2, "a");
            StatusSystem.Apply(unit, StatusType.Stunned, 2, 1, "b");
            StatusSystem.Apply(unit, StatusType.Stunned, 1, 4, "c");

            Assert.That(unit.Statuses.Count, Is.EqualTo(1));
            Assert.That(unit.Statuses[0].Magnitude, Is.EqualTo(2));
            Assert.That(unit.Statuses[0].RemainingTurns, Is.EqualTo(4));
        }

        [Test]
        public void ResolveTurnStart_AppliesBurningAndPoisonDamage()
        {
            var unit = Unit();
            StatusSystem.Apply(unit, StatusType.Burning, 2, 2, "a");
            StatusSystem.Apply(unit, StatusType.Poisoned, 3, 2, "b");

            StatusSystem.ResolveTurnStart(unit);

            Assert.That(unit.Health, Is.EqualTo(15));
        }

        [Test]
        public void ResolveTurnEnd_DecrementsAndRemovesExpiredStatuses()
        {
            var unit = Unit();
            StatusSystem.Apply(unit, StatusType.Burning, 1, 1, "a");
            StatusSystem.Apply(unit, StatusType.Shielded, 3, 2, "a");

            StatusSystem.ResolveTurnEnd(unit);

            Assert.That(unit.Statuses.Count, Is.EqualTo(1));
            Assert.That(unit.Statuses[0].Type, Is.EqualTo(StatusType.Shielded));
            Assert.That(unit.Statuses[0].RemainingTurns, Is.EqualTo(1));
        }

        [Test]
        public void IsStunned_ReturnsTrueOnlyWhenStunned()
        {
            var unit = Unit();
            StatusSystem.Apply(unit, StatusType.Slowed, 2, 2, "a");

            Assert.That(StatusSystem.IsStunned(unit), Is.False);

            StatusSystem.Apply(unit, StatusType.Stunned, 1, 1, "a");

            Assert.That(StatusSystem.IsStunned(unit), Is.True);
        }

        [Test]
        public void MovementPenalty_SumsSlowMagnitudes()
        {
            var unit = Unit();
            unit.MutableStatuses.Add(new StatusInstance(StatusType.Slowed, 1, 2, "a"));
            unit.MutableStatuses.Add(new StatusInstance(StatusType.Slowed, 2, 2, "b"));

            Assert.That(StatusSystem.MovementPenalty(unit), Is.EqualTo(3));
        }

        [Test]
        public void ConsumeShield_SplitsAcrossStatusesAndRemovesDepletedShield()
        {
            var unit = Unit();
            unit.MutableStatuses.Add(new StatusInstance(StatusType.Shielded, 2, 2, "a"));
            unit.MutableStatuses.Add(new StatusInstance(StatusType.Shielded, 3, 2, "a"));

            var absorbed = StatusSystem.ConsumeShield(unit, 4);

            Assert.That(absorbed, Is.EqualTo(4));
            Assert.That(StatusSystem.GetShield(unit), Is.EqualTo(1));
            Assert.That(unit.Statuses.Count, Is.EqualTo(1));
        }

        [Test]
        public void Apply_TauntedRefreshesSourceAndDuration()
        {
            var unit = Unit();

            StatusSystem.Apply(unit, StatusType.Taunted, 1, 2, "source.a");
            StatusSystem.Apply(unit, StatusType.Taunted, 1, 4, "source.b");

            Assert.That(unit.Statuses.Count, Is.EqualTo(1));
            Assert.That(unit.Statuses[0].SourceUnitId, Is.EqualTo("source.b"));
            Assert.That(unit.Statuses[0].RemainingTurns, Is.EqualTo(4));
        }

        [Test]
        public void TryGetActiveTauntSource_ReturnsOnlyLivingSource()
        {
            var state = new BattleState(BattleMap.CreatePlain(3, 1));
            var target = new BattleUnit(
                "target",
                "test.unit",
                Team.Enemy,
                new UnitStats(20, 0, 1, 0, 5, 0f, 0),
                new GridPosition(0, 0));
            var source = new BattleUnit(
                "source",
                "test.unit",
                Team.Player,
                new UnitStats(20, 0, 1, 0, 5, 0f, 0),
                new GridPosition(1, 0));
            state.AddUnit(target);
            state.AddUnit(source);
            StatusSystem.Apply(target, StatusType.Taunted, 1, 2, source.Id);

            var found = StatusSystem.TryGetActiveTauntSource(state, target, out var activeSource);

            Assert.That(found, Is.True);
            Assert.That(activeSource, Is.SameAs(source));

            source.ApplyRawDamage(source.Health);

            Assert.That(StatusSystem.TryGetActiveTauntSource(state, target, out activeSource), Is.False);
            Assert.That(activeSource, Is.Null);
            Assert.That(StatusSystem.IsTaunted(target), Is.True);
        }

        [Test]
        public void ResolveTurnEnd_RemovesExpiredTaunt()
        {
            var unit = Unit();
            StatusSystem.Apply(unit, StatusType.Taunted, 1, 1, "source");

            StatusSystem.ResolveTurnEnd(unit);

            Assert.That(StatusSystem.IsTaunted(unit), Is.False);
        }
        private static BattleUnit Unit() =>
            new BattleUnit("b", "test.unit", Team.Enemy,
                new UnitStats(20, 0, 1, 0, 5, 0f, 0), new GridPosition(0, 0));
    }
}
