using System.Linq;
using BorderValley.Battle.Domain;
using BorderValley.Core.Random;
using BorderValley.UI.Battle;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class BattleUiPresenterTests
    {
        [Test]
        public void TapCell_OnReachableCell_MovesActiveUnit()
        {
            var presenter = Create();
            var actor = presenter.ActiveUnit;

            var result = presenter.TapCell(new GridPosition(0, 2));

            Assert.That(result.Success, Is.True);
            Assert.That(actor.Position, Is.EqualTo(new GridPosition(0, 2)));
        }

        [Test]
        public void SelectBasicSkill_ThenTapEnemy_UsesSkill()
        {
            var presenter = Create();
            presenter.TapCell(new GridPosition(4, 2));
            var enemy = presenter.Engine.State.GetUnit("enemy.bandit");
            var health = enemy.Health;

            presenter.SelectSkill("skill.basic");
            var result = presenter.TapCell(enemy.Position);

            Assert.That(result.Success, Is.True);
            Assert.That(enemy.Health, Is.LessThan(health));
        }

        [Test]
        public void GetHighlight_SelectedSkill_MarksTargetCells()
        {
            var presenter = Create();
            presenter.SelectSkill("skill.piercing_shot");

            Assert.That(
                presenter.GetHighlight(new GridPosition(5, 2)),
                Is.EqualTo(BattleHighlightKind.Target));
        }

        [Test]
        public void GetHighlight_OriginCell_ReturnsNone()
        {
            var presenter = Create();

            Assert.That(
                presenter.GetHighlight(presenter.ActiveUnit.Position),
                Is.EqualTo(BattleHighlightKind.None));
        }

        [Test]
        public void GetHighlight_AfterMovement_ReturnsNone()
        {
            var presenter = Create();
            presenter.TapCell(new GridPosition(0, 2));

            Assert.That(
                presenter.GetHighlight(new GridPosition(0, 1)),
                Is.EqualTo(BattleHighlightKind.None));
        }

        [Test]
        public void GetHighlight_AfterBattleFinished_ReturnsNone()
        {
            var presenter = Create();
            foreach (var enemy in presenter.Engine.State.UnitsOf(Team.Enemy).ToArray())
                enemy.ApplyRawDamage(enemy.Health);

            var result = presenter.Execute(new EndTurnCommand(presenter.ActiveUnit.Id));

            Assert.That(result.Success, Is.True);
            Assert.That(presenter.IsFinished, Is.True);
            Assert.That(
                presenter.GetHighlight(new GridPosition(0, 2)),
                Is.EqualTo(BattleHighlightKind.None));
        }

        [Test]
        public void EndTurn_ClearsSelectedSkill()
        {
            var presenter = Create();
            var previousActive = presenter.ActiveUnit;
            presenter.SelectSkill("skill.piercing_shot");

            var result = presenter.EndTurn();

            Assert.That(result.Success, Is.True);
            Assert.That(presenter.ActiveUnit, Is.Not.SameAs(previousActive));
            Assert.That(presenter.SelectedSkillId, Is.Null);
        }

        [Test]
        public void Execute_EndTurnCommand_ClearsSelectedSkill()
        {
            var presenter = Create();
            var previousActive = presenter.ActiveUnit;
            presenter.SelectSkill("skill.piercing_shot");

            var result = presenter.Execute(new EndTurnCommand(previousActive.Id));

            Assert.That(result.Success, Is.True);
            Assert.That(presenter.ActiveUnit, Is.Not.SameAs(previousActive));
            Assert.That(presenter.SelectedSkillId, Is.Null);
        }

        [Test]
        public void EndTurn_WhenSkippedTurnsReturnToSameUnit_ClearsSelectedSkill()
        {
            var presenter = Create();
            var active = presenter.ActiveUnit;
            presenter.SelectSkill("skill.piercing_shot");
            foreach (var unit in presenter.Engine.State.Units
                         .Where(unit => !ReferenceEquals(unit, active))
                         .ToArray())
            {
                StatusSystem.Apply(unit, StatusType.Stunned, 1, 2, "test");
            }

            var result = presenter.EndTurn();

            Assert.That(result.Success, Is.True);
            Assert.That(presenter.ActiveUnit, Is.SameAs(active));
            Assert.That(presenter.SelectedSkillId, Is.Null);
        }

        [Test]
        public void GetHighlight_SelectedSkillAfterMovement_StillMarksTargetCells()
        {
            var presenter = Create();
            presenter.TapCell(new GridPosition(4, 2));
            presenter.SelectSkill("skill.basic");

            Assert.That(
                presenter.GetHighlight(new GridPosition(5, 2)),
                Is.EqualTo(BattleHighlightKind.Target));
        }

        private static BattleUiPresenter Create()
        {
            var presenter = new BattleUiPresenter(
                BattleScenarioFactory.CreateCoreScenario(),
                RandomSourceFactory.FromSeed("presenter"));
            presenter.Start();
            return presenter;
        }
    }
}
