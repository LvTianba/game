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
