using System.Linq;
using BorderValley.Battle.Domain;
using BorderValley.Core.Random;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class SkillExecutorTests
    {
        [Test]
        public void GetValidTargets_EnemySkill_RespectsRange()
        {
            var state = StateWithThreeUnits();
            var actor = state.GetUnit("p1");
            var skill = Skill("slash", SkillTargeting.Enemy, range: 1, mana: 0);

            var targets = SkillTargetValidator.GetValidTargets(state, actor, skill).ToArray();

            Assert.That(targets.Select(unit => unit.Id), Is.EquivalentTo(new[] { "e1" }));
        }

        [Test]
        public void GetValidTargets_SelfSkill_ReturnsActor()
        {
            var state = StateWithThreeUnits();
            var actor = state.GetUnit("p1");
            var skill = Skill("guard", SkillTargeting.Self, range: 0, mana: 0);

            var targets = SkillTargetValidator.GetValidTargets(state, actor, skill).ToArray();

            Assert.That(targets.Select(unit => unit.Id), Is.EqualTo(new[] { "p1" }));
        }

        [Test]
        public void GetValidTargets_AllySkill_ReturnsOnlyLivingAlliesInRange()
        {
            var state = new BattleState(BattleMap.CreatePlain(5, 1));
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("a1", Team.Player, 2));
            state.AddUnit(Unit("a2", Team.Player, 4));
            state.AddUnit(Unit("e1", Team.Enemy, 1));
            var actor = state.GetUnit("p1");
            var skill = Skill("ward", SkillTargeting.Ally, range: 2, mana: 0);

            var targets = SkillTargetValidator.GetValidTargets(state, actor, skill).ToArray();

            Assert.That(targets.Select(unit => unit.Id), Is.EqualTo(new[] { "a1" }));
        }

        [Test]
        public void GetValidTargets_GroundSkill_ExcludesObstacleCells()
        {
            var map = new BattleMap(4, 1, new[]
            {
                TerrainType.Plain, TerrainType.Plain, TerrainType.Obstacle, TerrainType.Plain
            });
            var state = new BattleState(map);
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("e1", Team.Enemy, 1));
            state.AddUnit(Unit("e2", Team.Enemy, 2));
            var actor = state.GetUnit("p1");
            var skill = Skill("quake", SkillTargeting.Ground, range: 3, mana: 0);

            var targets = SkillTargetValidator.GetValidTargets(state, actor, skill).ToArray();

            Assert.That(targets.Select(unit => unit.Id), Is.EquivalentTo(new[] { "p1", "e1" }));
        }

        [Test]
        public void Execute_DamageSkill_SpendsManaAndMarksAction()
        {
            var state = StateWithThreeUnits();
            var actor = state.GetUnit("p1");
            var target = state.GetUnit("e1");
            var skill = Skill("fireball", SkillTargeting.Enemy, range: 2, mana: 2,
                new SkillEffectDefinition(SkillEffectKind.Damage, 1.5f, StatusType.Burning, 1, 2));

            var result = SkillExecutor.Execute(
                state, actor, skill, target, RandomSourceFactory.FromSeed("fireball"));

            Assert.That(result.Success, Is.True);
            Assert.That(actor.Mana, Is.EqualTo(8));
            Assert.That(actor.HasActed, Is.True);
            Assert.That(target.Health, Is.LessThan(target.Stats.MaxHealth));
            Assert.That(target.Statuses.Any(status => status.Type == StatusType.Burning), Is.True);
            Assert.That(result.DamageDealt, Is.GreaterThan(0));
            Assert.That(result.AffectedUnits, Does.Contain(target));
        }

        [Test]
        public void Execute_HealSkill_ReportsActualHealing()
        {
            var state = StateWithThreeUnits();
            var actor = state.GetUnit("p1");
            var target = state.GetUnit("p1");
            target.ApplyRawDamage(7);
            var skill = Skill("heal", SkillTargeting.Self, range: 0, mana: 0,
                new SkillEffectDefinition(SkillEffectKind.Heal, 0f, default, 5, 0));

            var result = SkillExecutor.Execute(
                state, actor, skill, target, RandomSourceFactory.FromSeed("heal"));

            Assert.That(result.Success, Is.True);
            Assert.That(target.Health, Is.EqualTo(18));
            Assert.That(result.HealingDone, Is.EqualTo(5));
        }

        [Test]
        public void Execute_ApplyStatusSkill_AppliesStatus()
        {
            var state = StateWithThreeUnits();
            var actor = state.GetUnit("p1");
            var target = state.GetUnit("e1");
            var skill = Skill("snare", SkillTargeting.Enemy, range: 2, mana: 0,
                new SkillEffectDefinition(SkillEffectKind.ApplyStatus, 0f, StatusType.Slowed, 2, 3));

            var result = SkillExecutor.Execute(
                state, actor, skill, target, RandomSourceFactory.FromSeed("snare"));

            Assert.That(result.Success, Is.True);
            var status = target.Statuses.Single(item => item.Type == StatusType.Slowed);
            Assert.That(status.Magnitude, Is.EqualTo(2));
            Assert.That(status.RemainingTurns, Is.EqualTo(3));
            Assert.That(status.SourceUnitId, Is.EqualTo("p1"));
        }

        [Test]
        public void Execute_PushSkill_MovesAwayAndStopsAtObstacle()
        {
            var map = new BattleMap(5, 1, new[]
            {
                TerrainType.Plain, TerrainType.Plain, TerrainType.Plain,
                TerrainType.Obstacle, TerrainType.Plain
            });
            var state = new BattleState(map);
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("e1", Team.Enemy, 1));
            var actor = state.GetUnit("p1");
            var target = state.GetUnit("e1");
            var skill = Skill("push", SkillTargeting.Enemy, range: 1, mana: 0,
                new SkillEffectDefinition(SkillEffectKind.Push, 0f, default, 2, 0));

            var result = SkillExecutor.Execute(
                state, actor, skill, target, RandomSourceFactory.FromSeed("push"));

            Assert.That(result.Success, Is.True);
            Assert.That(target.Position, Is.EqualTo(new GridPosition(2, 0)));
            Assert.That(target.HasMoved, Is.False);
        }

        [Test]
        public void Execute_PullSkill_StopsBeforeOccupyingActor()
        {
            var state = new BattleState(BattleMap.CreatePlain(4, 1));
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("e1", Team.Enemy, 2));
            var actor = state.GetUnit("p1");
            var target = state.GetUnit("e1");
            var skill = Skill("pull", SkillTargeting.Enemy, range: 2, mana: 0,
                new SkillEffectDefinition(SkillEffectKind.Pull, 0f, default, 2, 0));

            var result = SkillExecutor.Execute(
                state, actor, skill, target, RandomSourceFactory.FromSeed("pull"));

            Assert.That(result.Success, Is.True);
            Assert.That(target.Position, Is.EqualTo(new GridPosition(1, 0)));
        }

        [Test]
        public void Execute_TargetOutOfRange_FailsWithoutMutation()
        {
            var state = StateWithThreeUnits();
            var actor = state.GetUnit("p1");
            var target = state.GetUnit("e2");
            var skill = Skill("slash", SkillTargeting.Enemy, range: 1, mana: 2);

            var result = SkillExecutor.Execute(
                state, actor, skill, target, RandomSourceFactory.FromSeed("slash"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.Not.Empty);
            Assert.That(actor.Mana, Is.EqualTo(10));
            Assert.That(actor.HasActed, Is.False);
        }

        [Test]
        public void Execute_InsufficientMana_FailsWithoutMutation()
        {
            var state = StateWithThreeUnits();
            var actor = state.GetUnit("p1");
            var target = state.GetUnit("e1");
            var skill = Skill("expensive", SkillTargeting.Enemy, range: 1, mana: 11);

            var result = SkillExecutor.Execute(
                state, actor, skill, target, RandomSourceFactory.FromSeed("expensive"));

            Assert.That(result.Success, Is.False);
            Assert.That(actor.Mana, Is.EqualTo(10));
            Assert.That(actor.HasActed, Is.False);
            Assert.That(actor.Cooldowns, Is.Empty);
        }

        [Test]
        public void Execute_SkillOnCooldown_FailsWithoutMutation()
        {
            var state = StateWithThreeUnits();
            var actor = state.GetUnit("p1");
            var target = state.GetUnit("e1");
            var skill = SkillWithCooldown("slash", SkillTargeting.Enemy, 1, 2, 2);
            actor.Cooldowns[skill.Id] = 1;

            var result = SkillExecutor.Execute(
                state, actor, skill, target, RandomSourceFactory.FromSeed("slash"));

            Assert.That(result.Success, Is.False);
            Assert.That(actor.Mana, Is.EqualTo(10));
            Assert.That(actor.HasActed, Is.False);
            Assert.That(actor.Cooldowns[skill.Id], Is.EqualTo(1));
        }

        [Test]
        public void Execute_EffectThrowsAfterMutation_DoesNotRollback()
        {
            var state = StateWithThreeUnits();
            var actor = state.GetUnit("p1");
            var target = state.GetUnit("e1");
            var skill = SkillWithCooldown("broken", SkillTargeting.Enemy, 1, 2, 3,
                new SkillEffectDefinition(SkillEffectKind.Damage, 1f, default, 0, 0),
                new SkillEffectDefinition((SkillEffectKind)999, 0f, default, 0, 0));

            Assert.Throws<System.ArgumentOutOfRangeException>(() => SkillExecutor.Execute(
                state, actor, skill, target, RandomSourceFactory.FromSeed("broken")));

            Assert.That(actor.Mana, Is.EqualTo(8));
            Assert.That(actor.HasActed, Is.True);
            Assert.That(actor.Cooldowns[skill.Id], Is.EqualTo(3));
            Assert.That(target.Health, Is.LessThan(target.Stats.MaxHealth));
        }

        [Test]
        public void TurnStart_DecrementsCooldownsToMinimumZero()
        {
            var state = new BattleState(BattleMap.CreatePlain(2, 1));
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("e1", Team.Enemy, 1));
            var actor = state.GetUnit("p1");
            var target = state.GetUnit("e1");
            var skill = SkillWithCooldown("guard", SkillTargeting.Self, 0, 0, 2);
            var engine = new BattleTurnEngine(state);
            engine.Start();

            var result = SkillExecutor.Execute(
                state, actor, skill, actor, RandomSourceFactory.FromSeed("guard"));
            Assert.That(result.Success, Is.True);
            Assert.That(actor.Cooldowns[skill.Id], Is.EqualTo(2));

            engine.EndTurn();
            engine.EndTurn();
            Assert.That(engine.ActiveUnit.Id, Is.EqualTo("p1"));
            Assert.That(actor.Cooldowns[skill.Id], Is.EqualTo(1));

            engine.EndTurn();
            engine.EndTurn();
            Assert.That(engine.ActiveUnit.Id, Is.EqualTo("p1"));
            Assert.That(actor.Cooldowns[skill.Id], Is.Zero);
        }

        [Test]
        public void Execute_DeadTarget_FailsBeforeResourceMutation()
        {
            var state = StateWithThreeUnits();
            var actor = state.GetUnit("p1");
            var target = state.GetUnit("e1");
            target.ApplyRawDamage(999);
            var skill = SkillWithCooldown("slash", SkillTargeting.Enemy, 1, 2, 2);

            var result = SkillExecutor.Execute(
                state, actor, skill, target, RandomSourceFactory.FromSeed("slash"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.Not.Empty);
            Assert.That(actor.Mana, Is.EqualTo(10));
            Assert.That(actor.HasActed, Is.False);
            Assert.That(actor.Cooldowns, Is.Empty);
        }

        private static BattleState StateWithThreeUnits()
        {
            var state = new BattleState(BattleMap.CreatePlain(4, 1));
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("e1", Team.Enemy, 1));
            state.AddUnit(Unit("e2", Team.Enemy, 3));
            return state;
        }

        private static BattleUnit Unit(string id, Team team, int x) =>
            new BattleUnit(id, "test.unit", team,
                new UnitStats(20, 10, 8, 1, 5, 0f, 0), new GridPosition(x, 0));

        private static SkillDefinition Skill(
            string id,
            SkillTargeting targeting,
            int range,
            int mana,
            params SkillEffectDefinition[] effects) =>
            new SkillDefinition(id, "skill." + id, targeting, range, 0, mana, 0, effects);

        private static SkillDefinition SkillWithCooldown(
            string id,
            SkillTargeting targeting,
            int range,
            int mana,
            int cooldown,
            params SkillEffectDefinition[] effects) =>
            new SkillDefinition(id, "skill." + id, targeting, range, 0, mana, cooldown, effects);
    }
}
