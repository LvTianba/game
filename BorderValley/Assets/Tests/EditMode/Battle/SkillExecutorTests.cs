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

        [Test]
        public void GetValidGroundTargets_EmptyCellsWithinRange_AreValid()
        {
            var state = new BattleState(BattleMap.CreatePlain(4, 1));
            state.AddUnit(Unit("p1", Team.Player, 0));
            var actor = state.GetUnit("p1");
            var skill = SkillWithRadius("quake", SkillTargeting.Ground, 2, 0, 0);

            var targets = SkillTargetValidator.GetValidGroundTargets(state, actor, skill).ToArray();

            Assert.That(targets.Contains(new GridPosition(1, 0)), Is.True);
            Assert.That(targets.Contains(new GridPosition(2, 0)), Is.True);
            Assert.That(targets.Contains(new GridPosition(3, 0)), Is.False);
        }

        [Test]
        public void GetValidGroundTargets_BlocksObstacleAndLineOfSight()
        {
            var map = new BattleMap(4, 1, new[]
            {
                TerrainType.Plain, TerrainType.Obstacle, TerrainType.Plain, TerrainType.Plain
            });
            var state = new BattleState(map);
            state.AddUnit(Unit("p1", Team.Player, 0));
            var actor = state.GetUnit("p1");
            var skill = SkillWithRadius("quake", SkillTargeting.Ground, 3, 0, 0);

            var targets = SkillTargetValidator.GetValidGroundTargets(state, actor, skill).ToArray();

            Assert.That(targets.Contains(new GridPosition(1, 0)), Is.False);
            Assert.That(targets.Contains(new GridPosition(2, 0)), Is.False);
        }

        [Test]
        public void GetValidTargets_ObstacleBetweenRangedTargets_IsBlocked()
        {
            var map = new BattleMap(3, 1, new[]
            {
                TerrainType.Plain, TerrainType.Obstacle, TerrainType.Plain
            });
            var state = new BattleState(map);
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("e1", Team.Enemy, 2));
            var actor = state.GetUnit("p1");
            var skill = Skill("blast", SkillTargeting.Enemy, 2, 0);

            var targets = SkillTargetValidator.GetValidTargets(state, actor, skill).ToArray();

            Assert.That(targets, Is.Empty);
        }

        [Test]
        public void GetValidTargets_BushBetweenRangedTargets_IsBlocked()
        {
            var map = new BattleMap(3, 1, new[]
            {
                TerrainType.Plain, TerrainType.Bush, TerrainType.Plain
            });
            var state = new BattleState(map);
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("e1", Team.Enemy, 2));
            var actor = state.GetUnit("p1");
            var skill = Skill("blast", SkillTargeting.Enemy, 2, 0);

            var targets = SkillTargetValidator.GetValidTargets(state, actor, skill).ToArray();

            Assert.That(targets, Is.Empty);
        }

        [Test]
        public void GetValidTargets_ClearRangedTarget_IsValid()
        {
            var state = new BattleState(BattleMap.CreatePlain(3, 1));
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("e1", Team.Enemy, 2));
            var actor = state.GetUnit("p1");
            var skill = Skill("blast", SkillTargeting.Enemy, 2, 0);

            var targets = SkillTargetValidator.GetValidTargets(state, actor, skill).ToArray();

            Assert.That(targets.Select(unit => unit.Id), Is.EqualTo(new[] { "e1" }));
        }

        [Test]
        public void GetValidTargets_AdjacentTarget_IgnoresLineOfSight()
        {
            var map = new BattleMap(3, 1, new[]
            {
                TerrainType.Plain, TerrainType.Plain, TerrainType.Obstacle
            });
            var state = new BattleState(map);
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("e1", Team.Enemy, 1));
            var actor = state.GetUnit("p1");
            var skill = Skill("blast", SkillTargeting.Enemy, 2, 0);

            var targets = SkillTargetValidator.GetValidTargets(state, actor, skill).ToArray();

            Assert.That(targets.Select(unit => unit.Id), Is.EqualTo(new[] { "e1" }));
        }

        [Test]
        public void Execute_GroundSkillOnEmptyCell_Succeeds()
        {
            var state = new BattleState(BattleMap.CreatePlain(4, 1));
            state.AddUnit(Unit("p1", Team.Player, 0));
            var actor = state.GetUnit("p1");
            var skill = SkillWithRadius("quake", SkillTargeting.Ground, 3, 0, 2,
                new SkillEffectDefinition(SkillEffectKind.Damage, 1f, default, 0, 0));

            var result = SkillExecutor.Execute(
                state, actor, skill, new GridPosition(3, 0), RandomSourceFactory.FromSeed("quake"));

            Assert.That(result.Success, Is.True);
            Assert.That(actor.Mana, Is.EqualTo(8));
            Assert.That(actor.HasActed, Is.True);
            Assert.That(result.DamageDealt, Is.Zero);
            Assert.That(result.AffectedUnits, Is.Empty);
        }

        [Test]
        public void Execute_DamageArea_HitsOnlyEnemiesWithinRadius()
        {
            var state = new BattleState(BattleMap.CreatePlain(5, 1));
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("p2", Team.Player, 1));
            state.AddUnit(Unit("e1", Team.Enemy, 2));
            state.AddUnit(Unit("e2", Team.Enemy, 3));
            state.AddUnit(Unit("e3", Team.Enemy, 4));
            var actor = state.GetUnit("p1");
            var target = state.GetUnit("e1");
            var skill = SkillWithRadius("fireball", SkillTargeting.Enemy, 4, 1, 0,
                new SkillEffectDefinition(SkillEffectKind.Damage, 1f, default, 0, 0));

            var result = SkillExecutor.Execute(
                state, actor, skill, target, RandomSourceFactory.FromSeed("fireball"));

            Assert.That(result.Success, Is.True);
            Assert.That(state.GetUnit("p2").Health, Is.EqualTo(20));
            Assert.That(state.GetUnit("e1").Health, Is.EqualTo(13));
            Assert.That(state.GetUnit("e2").Health, Is.EqualTo(13));
            Assert.That(state.GetUnit("e3").Health, Is.EqualTo(20));
            Assert.That(result.DamageDealt, Is.EqualTo(14));
            Assert.That(result.AffectedUnitIds, Is.EquivalentTo(new[] { "e1", "e2" }));
        }

        [Test]
        public void Execute_UnitTargetWithZeroRadius_HitsOnlyAnchorUnit()
        {
            var state = new BattleState(BattleMap.CreatePlain(4, 1));
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("e1", Team.Enemy, 1));
            state.AddUnit(Unit("e2", Team.Enemy, 2));
            var actor = state.GetUnit("p1");
            var target = state.GetUnit("e1");
            var skill = Skill("bolt", SkillTargeting.Enemy, 3, 0,
                new SkillEffectDefinition(SkillEffectKind.Damage, 1f, default, 0, 0));

            var result = SkillExecutor.Execute(
                state, actor, skill, target, RandomSourceFactory.FromSeed("bolt"));

            Assert.That(state.GetUnit("e1").Health, Is.EqualTo(13));
            Assert.That(state.GetUnit("e2").Health, Is.EqualTo(20));
            Assert.That(result.AffectedUnitIds, Is.EquivalentTo(new[] { "e1" }));
        }

        [Test]
        public void Execute_HealArea_IncludesActorAndAlliesInRadius()
        {
            var state = new BattleState(BattleMap.CreatePlain(5, 1));
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("p2", Team.Player, 1));
            state.AddUnit(Unit("e1", Team.Enemy, 2));
            state.AddUnit(Unit("p3", Team.Player, 3));
            state.GetUnit("p1").ApplyRawDamage(10);
            state.GetUnit("p2").ApplyRawDamage(10);
            state.GetUnit("p3").ApplyRawDamage(10);
            var actor = state.GetUnit("p1");
            var target = state.GetUnit("p2");
            var skill = SkillWithRadius("heal", SkillTargeting.Ally, 3, 1, 0,
                new SkillEffectDefinition(SkillEffectKind.Heal, 0f, default, 5, 0));

            var result = SkillExecutor.Execute(
                state, actor, skill, target, RandomSourceFactory.FromSeed("heal"));

            Assert.That(state.GetUnit("p1").Health, Is.EqualTo(15));
            Assert.That(state.GetUnit("p2").Health, Is.EqualTo(15));
            Assert.That(state.GetUnit("p3").Health, Is.EqualTo(10));
            Assert.That(state.GetUnit("e1").Health, Is.EqualTo(20));
            Assert.That(result.HealingDone, Is.EqualTo(10));
            Assert.That(result.AffectedUnitIds, Is.EquivalentTo(new[] { "p1", "p2" }));
        }

        [Test]
        public void Execute_PhysicalDamage_UsesArmor()
        {
            var state = new BattleState(BattleMap.CreatePlain(2, 1));
            state.AddUnit(Unit("p1", Team.Player, 0, 10, 0, 0));
            state.AddUnit(Unit("e1", Team.Enemy, 1, 1, 8, 2));
            var actor = state.GetUnit("p1");
            var target = state.GetUnit("e1");
            var skill = Skill("strike", SkillTargeting.Enemy, 1, 0,
                new SkillEffectDefinition(SkillEffectKind.Damage, 1f, default, 0, 0));

            var result = SkillExecutor.Execute(
                state, actor, skill, target, RandomSourceFactory.FromSeed("strike"));

            Assert.That(result.Success, Is.True);
            Assert.That(target.Health, Is.EqualTo(18));
            Assert.That(result.DamageDealt, Is.EqualTo(2));
        }

        [Test]
        public void Execute_MagicalDamage_UsesResistance()
        {
            var state = new BattleState(BattleMap.CreatePlain(2, 1));
            state.AddUnit(Unit("p1", Team.Player, 0, 10, 0, 0));
            state.AddUnit(Unit("e1", Team.Enemy, 1, 1, 8, 2));
            var actor = state.GetUnit("p1");
            var target = state.GetUnit("e1");
            var skill = Skill("magic_bolt", SkillTargeting.Enemy, 1, 0,
                new SkillEffectDefinition(
                    SkillEffectKind.Damage, 1f, default, 0, 0, DamageType.Magical));

            var result = SkillExecutor.Execute(
                state, actor, skill, target, RandomSourceFactory.FromSeed("magic_bolt"));

            Assert.That(result.Success, Is.True);
            Assert.That(target.Health, Is.EqualTo(12));
            Assert.That(result.DamageDealt, Is.EqualTo(8));
        }

        [Test]
        public void Execute_PushArea_MovesMultipleEnemiesDeterministically()
        {
            var state = new BattleState(BattleMap.CreatePlain(4, 4));
            state.AddUnit(UnitAt("p1", Team.Player, 1, 1));
            state.AddUnit(UnitAt("e1", Team.Enemy, 2, 1));
            state.AddUnit(UnitAt("e2", Team.Enemy, 1, 2));
            var actor = state.GetUnit("p1");
            var skill = SkillWithRadius("wind", SkillTargeting.Self, 0, 1, 0,
                new SkillEffectDefinition(SkillEffectKind.Push, 0f, default, 1, 0));

            var result = SkillExecutor.Execute(
                state, actor, skill, actor, RandomSourceFactory.FromSeed("wind"));

            Assert.That(state.GetUnit("e1").Position, Is.EqualTo(new GridPosition(3, 1)));
            Assert.That(state.GetUnit("e2").Position, Is.EqualTo(new GridPosition(1, 3)));
            Assert.That(result.AffectedUnitIds, Is.EquivalentTo(new[] { "e1", "e2" }));
        }
        [Test]
        public void Execute_StatusArea_UsesShieldedForAlliesAndOtherStatusesForEnemies()
        {
            var state = new BattleState(BattleMap.CreatePlain(5, 1));
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("p2", Team.Player, 1));
            state.AddUnit(Unit("e1", Team.Enemy, 2));
            state.AddUnit(Unit("e2", Team.Enemy, 3));
            var actor = state.GetUnit("p1");
            var skill = SkillWithRadius("ward", SkillTargeting.Ground, 4, 1, 0,
                new SkillEffectDefinition(
                    SkillEffectKind.ApplyStatus, 0f, StatusType.Shielded, 3, 2),
                new SkillEffectDefinition(
                    SkillEffectKind.ApplyStatus, 0f, StatusType.Slowed, 2, 2));

            var result = SkillExecutor.Execute(
                state, actor, skill, new GridPosition(2, 0), RandomSourceFactory.FromSeed("ward"));

            Assert.That(StatusSystem.GetShield(state.GetUnit("p2")), Is.EqualTo(3));
            Assert.That(state.GetUnit("e1").Statuses.Any(status => status.Type == StatusType.Slowed), Is.True);
            Assert.That(state.GetUnit("e2").Statuses.Any(status => status.Type == StatusType.Slowed), Is.True);
            Assert.That(result.AffectedUnitIds, Is.EquivalentTo(new[] { "p2", "e1", "e2" }));
        }
        [Test]
        public void Execute_GroundZeroRadiusDamageOnAllyCell_DoesNotDamageAlly()
        {
            var state = new BattleState(BattleMap.CreatePlain(3, 1));
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("p2", Team.Player, 1));
            state.AddUnit(Unit("e1", Team.Enemy, 2));
            var actor = state.GetUnit("p1");
            var ally = state.GetUnit("p2");
            var skill = Skill("burst", SkillTargeting.Ground, 2, 0,
                new SkillEffectDefinition(SkillEffectKind.Damage, 1f, default, 0, 0));

            var result = SkillExecutor.Execute(
                state, actor, skill, new GridPosition(1, 0), RandomSourceFactory.FromSeed("burst"));

            Assert.That(result.Success, Is.True);
            Assert.That(ally.Health, Is.EqualTo(20));
            Assert.That(result.DamageDealt, Is.Zero);
            Assert.That(result.AffectedUnits, Is.Empty);
        }

        [Test]
        public void Execute_GroundZeroRadiusDamageOnEnemyCell_DamagesEnemy()
        {
            var state = new BattleState(BattleMap.CreatePlain(3, 1));
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("e1", Team.Enemy, 2));
            var actor = state.GetUnit("p1");
            var enemy = state.GetUnit("e1");
            var skill = Skill("burst", SkillTargeting.Ground, 2, 0,
                new SkillEffectDefinition(SkillEffectKind.Damage, 1f, default, 0, 0));

            var result = SkillExecutor.Execute(
                state, actor, skill, new GridPosition(2, 0), RandomSourceFactory.FromSeed("burst"));

            Assert.That(result.Success, Is.True);
            Assert.That(enemy.Health, Is.EqualTo(13));
            Assert.That(result.DamageDealt, Is.EqualTo(7));
            Assert.That(result.AffectedUnitIds, Is.EquivalentTo(new[] { "e1" }));
        }

        [Test]
        public void Execute_GroundZeroRadiusHealAndShieldOnEnemyCell_IsIgnored()
        {
            var state = new BattleState(BattleMap.CreatePlain(2, 1));
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("e1", Team.Enemy, 1));
            state.GetUnit("e1").ApplyRawDamage(10);
            var actor = state.GetUnit("p1");
            var enemy = state.GetUnit("e1");
            var skill = Skill("blessing", SkillTargeting.Ground, 1, 0,
                new SkillEffectDefinition(SkillEffectKind.Heal, 0f, default, 5, 0),
                new SkillEffectDefinition(
                    SkillEffectKind.ApplyStatus, 0f, StatusType.Shielded, 3, 2));

            var result = SkillExecutor.Execute(
                state, actor, skill, new GridPosition(1, 0), RandomSourceFactory.FromSeed("blessing"));

            Assert.That(result.Success, Is.True);
            Assert.That(enemy.Health, Is.EqualTo(10));
            Assert.That(StatusSystem.GetShield(enemy), Is.Zero);
            Assert.That(result.HealingDone, Is.Zero);
            Assert.That(result.AffectedUnits, Is.Empty);
        }

        [Test]
        public void Execute_GroundZeroRadiusShieldOnAllyCell_AppliesShield()
        {
            var state = new BattleState(BattleMap.CreatePlain(2, 1));
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("p2", Team.Player, 1));
            var actor = state.GetUnit("p1");
            var ally = state.GetUnit("p2");
            var skill = Skill("blessing", SkillTargeting.Ground, 1, 0,
                new SkillEffectDefinition(
                    SkillEffectKind.ApplyStatus, 0f, StatusType.Shielded, 3, 2));

            var result = SkillExecutor.Execute(
                state, actor, skill, new GridPosition(1, 0), RandomSourceFactory.FromSeed("blessing"));

            Assert.That(result.Success, Is.True);
            Assert.That(StatusSystem.GetShield(ally), Is.EqualTo(3));
            Assert.That(result.AffectedUnitIds, Is.EquivalentTo(new[] { "p2" }));
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
            Unit(id, team, x, 8, 1, 0);

        private static BattleUnit Unit(
            string id,
            Team team,
            int x,
            int power,
            int armor,
            int resistance) =>
            new BattleUnit(id, "test.unit", team,
                new UnitStats(20, 10, power, armor, 5, 0f, resistance), new GridPosition(x, 0));

        private static BattleUnit UnitAt(string id, Team team, int x, int y) =>
            new BattleUnit(id, "test.unit", team,
                new UnitStats(20, 10, 8, 1, 5, 0f, 0), new GridPosition(x, y));

        private static SkillDefinition Skill(
            string id,
            SkillTargeting targeting,
            int range,
            int mana,
            params SkillEffectDefinition[] effects) =>
            new SkillDefinition(id, "skill." + id, targeting, range, 0, mana, 0, effects);

        private static SkillDefinition SkillWithRadius(
            string id,
            SkillTargeting targeting,
            int range,
            int radius,
            int mana,
            params SkillEffectDefinition[] effects) =>
            new SkillDefinition(id, "skill." + id, targeting, range, radius, mana, 0, effects);
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
