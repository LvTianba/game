using System.Linq;
using BorderValley.Battle.Domain;
using BorderValley.Core.Random;
using BorderValley.UI.Battle;
using NUnit.Framework;

namespace BorderValley.UI.Tests
{
    public sealed class BattlePresentationTrackerTests
    {
        [Test]
        public void Observe_FirstState_ReturnsTurnChanged()
        {
            var presenter = CreateStartedPresenter();
            var tracker = new BattlePresentationTracker();

            var events = tracker.Observe(
                presenter.Engine.State,
                presenter.LastCommand,
                presenter.LastResult);

            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Kind, Is.EqualTo(BattlePresentationEventKind.TurnChanged));
        }

        [Test]
        public void Observe_PositionChanged_ReturnsMove()
        {
            var presenter = CreateStartedPresenter();
            var tracker = new BattlePresentationTracker();
            tracker.Observe(presenter.Engine.State, null, null);
            var actorId = presenter.ActiveUnit.Id;
            var destination = BattleMovement
                .FindReachableDestinations(presenter.Engine.State, presenter.ActiveUnit)
                .Keys
                .First();

            var result = presenter.TapCell(destination);
            var events = tracker.Observe(
                presenter.Engine.State,
                presenter.LastCommand,
                presenter.LastResult);

            Assert.That(result.Success, Is.True);
            Assert.That(
                events.Any(value =>
                    value.Kind == BattlePresentationEventKind.Move &&
                    value.UnitId == actorId),
                Is.True);
        }

        [Test]
        public void Observe_HealthDecreased_ReturnsHit()
        {
            var presenter = CreateStartedPresenter();
            var tracker = new BattlePresentationTracker();
            tracker.Observe(presenter.Engine.State, null, null);
            var target = presenter.Engine.State.GetUnit("enemy.bandit");
            target.SetCurrentResources(target.Health - 1, target.Mana);

            var events = tracker.Observe(presenter.Engine.State, null, null);

            Assert.That(
                events.Any(value =>
                    value.Kind == BattlePresentationEventKind.Hit &&
                    value.UnitId == target.Id),
                Is.True);
        }

        [Test]
        public void Observe_HealthReachedZero_ReturnsHitAndDown()
        {
            var presenter = CreateStartedPresenter();
            var tracker = new BattlePresentationTracker();
            tracker.Observe(presenter.Engine.State, null, null);
            var target = presenter.Engine.State.GetUnit("enemy.bandit");
            target.SetCurrentResources(0, target.Mana);

            var events = tracker.Observe(presenter.Engine.State, null, null);

            Assert.That(
                events.Any(value =>
                    value.Kind == BattlePresentationEventKind.Hit &&
                    value.UnitId == target.Id),
                Is.True);
            Assert.That(
                events.Any(value =>
                    value.Kind == BattlePresentationEventKind.Down &&
                    value.UnitId == target.Id),
                Is.True);
        }

        [Test]
        public void Observe_SuccessfulUseSkill_ReturnsAttackForCaster()
        {
            var presenter = CreateStartedPresenter();
            var tracker = new BattlePresentationTracker();
            tracker.Observe(presenter.Engine.State, null, null);
            var actorId = presenter.ActiveUnit.Id;
            presenter.TapCell(new GridPosition(4, 2));
            presenter.SelectSkill("skill.basic");

            var result = presenter.TapCell(
                presenter.Engine.State.UnitsOf(Team.Enemy).First().Position);
            var events = tracker.Observe(
                presenter.Engine.State,
                presenter.LastCommand,
                presenter.LastResult);

            Assert.That(result.Success, Is.True);
            Assert.That(presenter.LastCommand, Is.TypeOf<UseSkillCommand>());
            Assert.That(
                events.Any(value =>
                    value.Kind == BattlePresentationEventKind.Attack &&
                    value.UnitId == actorId),
                Is.True);
        }

        [Test]
        public void Observe_NextTurn_DoesNotRepeatPreviousAttack()
        {
            var presenter = CreateStartedPresenter();
            var tracker = new BattlePresentationTracker();
            tracker.Observe(presenter.Engine.State, null, null);
            presenter.TapCell(new GridPosition(4, 2));
            presenter.SelectSkill("skill.basic");
            var attackResult = presenter.TapCell(
                presenter.Engine.State.UnitsOf(Team.Enemy).First().Position);
            var attackEvents = tracker.Observe(
                presenter.Engine.State,
                presenter.LastCommand,
                presenter.LastResult);

            var result = presenter.EndTurn();
            var events = tracker.Observe(
                presenter.Engine.State,
                presenter.LastCommand,
                presenter.LastResult);

            Assert.That(attackResult.Success, Is.True);
            Assert.That(
                attackEvents.Any(value => value.Kind == BattlePresentationEventKind.Attack),
                Is.True);
            Assert.That(result.Success, Is.True);
            Assert.That(
                events.Any(value => value.Kind == BattlePresentationEventKind.Attack),
                Is.False);
            Assert.That(
                events.Any(value => value.Kind == BattlePresentationEventKind.TurnChanged),
                Is.True);
        }

        private static BattleUiPresenter CreateStartedPresenter()
        {
            var presenter = new BattleUiPresenter(
                BattleScenarioFactory.CreateCoreScenario(),
                RandomSourceFactory.FromSeed("presentation-tracker"));
            presenter.Start();
            return presenter;
        }
    }
}
