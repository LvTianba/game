using System.Collections.Generic;
using System.Linq;
using BorderValley.Battle.Domain;
using BorderValley.Core.Random;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class BattleEngineTests
    {
        [Test]
        public void Move_ToReachableCell_ConsumesMovement()
        {
            var engine = EngineWithUnits();
            var actor = engine.State.GetUnit("p1");

            var result = engine.Execute(new MoveCommand("p1", new GridPosition(1, 0)));

            Assert.That(result.Success, Is.True);
            Assert.That(actor.Position, Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(actor.HasMoved, Is.True);
            Assert.That(result.AffectedUnitIds, Does.Contain("p1"));
        }

        [Test]
        public void Move_ToOccupiedCell_FailsWithoutMutation()
        {
            var engine = EngineWithUnits();
            var actor = engine.State.GetUnit("p1");

            var result = engine.Execute(new MoveCommand("p1", new GridPosition(2, 0)));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.command.error.destination_occupied"));
            Assert.That(actor.Position, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(actor.HasMoved, Is.False);
        }

        [Test]
        public void Move_ToUnreachableCell_FailsWithoutMutation()
        {
            var engine = EngineWithUnits(
                enemyPosition: new GridPosition(4, 0),
                playerSpeed: 1,
                enemySpeed: 0);
            var actor = engine.State.GetUnit("p1");

            var result = engine.Execute(new MoveCommand("p1", new GridPosition(2, 0)));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.command.error.destination_unreachable"));
            Assert.That(actor.Position, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(actor.HasMoved, Is.False);
        }

        [Test]
        public void Move_ToCurrentCell_FailsWithoutMutation()
        {
            var engine = EngineWithUnits();
            var actor = engine.State.GetUnit("p1");

            var result = engine.Execute(new MoveCommand("p1", new GridPosition(0, 0)));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.command.error.destination_occupied"));
            Assert.That(actor.Position, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(actor.HasMoved, Is.False);
        }
        [Test]
        public void Move_WhenAlreadyMoved_FailsWithoutAdditionalMutation()
        {
            var engine = EngineWithUnits();
            var actor = engine.State.GetUnit("p1");
            engine.Execute(new MoveCommand("p1", new GridPosition(1, 0)));

            var result = engine.Execute(new MoveCommand("p1", new GridPosition(1, 0)));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.command.error.already_moved"));
            Assert.That(actor.Position, Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(actor.HasMoved, Is.True);
        }

        [Test]
        public void Move_WhenNotCurrentUnit_FailsWithoutMutation()
        {
            var engine = EngineWithUnits();
            var enemy = engine.State.GetUnit("e1");

            var result = engine.Execute(new MoveCommand("e1", new GridPosition(3, 0)));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.command.error.not_active_unit"));
            Assert.That(enemy.Position, Is.EqualTo(new GridPosition(2, 0)));
            Assert.That(enemy.HasMoved, Is.False);
        }

        [Test]
        public void Move_AppliesMovementPenaltyFromStatuses()
        {
            var engine = EngineWithUnits(playerSpeed: 2);
            var actor = engine.State.GetUnit("p1");
            StatusSystem.Apply(actor, StatusType.Slowed, 2, 2, "test");

            var result = engine.Execute(new MoveCommand("p1", new GridPosition(1, 0)));

            Assert.That(result.Success, Is.False);
            Assert.That(actor.Position, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(actor.HasMoved, Is.False);
        }

        [Test]
        public void UseSkill_OnValidTarget_ConsumesManaCooldownAndAction()
        {
            var skills = Skills();
            var engine = EngineWithUnits(
                enemyPosition: new GridPosition(1, 0),
                skills: skills);
            var actor = engine.State.GetUnit("p1");
            var target = engine.State.GetUnit("e1");

            var result = engine.Execute(new UseSkillCommand("p1", "skill.damage", "e1"));

            Assert.That(result.Success, Is.True);
            Assert.That(actor.Mana, Is.EqualTo(8));
            Assert.That(actor.Cooldowns["skill.damage"], Is.EqualTo(2));
            Assert.That(actor.HasActed, Is.True);
            Assert.That(target.Health, Is.EqualTo(12));
            Assert.That(result.AffectedUnitIds, Does.Contain("e1"));
        }

        [Test]
        public void UseSkill_WhenUnknownSkill_FailsWithoutMutation()
        {
            var engine = EngineWithUnits(enemyPosition: new GridPosition(1, 0));
            var actor = engine.State.GetUnit("p1");
            var target = engine.State.GetUnit("e1");

            var result = engine.Execute(new UseSkillCommand("p1", "skill.missing", "e1"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.command.error.skill_not_found"));
            Assert.That(actor.Mana, Is.EqualTo(10));
            Assert.That(actor.HasActed, Is.False);
            Assert.That(target.Health, Is.EqualTo(20));
        }

        [Test]
        public void UseSkill_WithInvalidTarget_FailsWithoutMutation()
        {
            var engine = EngineWithUnits(skills: Skills());
            var actor = engine.State.GetUnit("p1");

            var result = engine.Execute(new UseSkillCommand("p1", "skill.damage", "p1"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.skill.error.invalid_target"));
            Assert.That(actor.Mana, Is.EqualTo(10));
            Assert.That(actor.HasActed, Is.False);
        }

        [Test]
        public void UseSkill_WithUnknownTarget_FailsWithoutMutation()
        {
            var engine = EngineWithUnits(enemyPosition: new GridPosition(1, 0), skills: Skills());
            var actor = engine.State.GetUnit("p1");

            var result = engine.Execute(new UseSkillCommand("p1", "skill.damage", "missing"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.command.error.target_not_found"));
            Assert.That(actor.Mana, Is.EqualTo(10));
            Assert.That(actor.HasActed, Is.False);
        }

        [Test]
        public void UseSkill_WhenNotCurrentUnit_FailsWithoutMutation()
        {
            var engine = EngineWithUnits(
                enemyPosition: new GridPosition(1, 0),
                skills: Skills());
            var actor = engine.State.GetUnit("e1");

            var result = engine.Execute(new UseSkillCommand("e1", "skill.damage", "p1"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.command.error.not_active_unit"));
            Assert.That(actor.HasActed, Is.False);
        }

        [Test]
        public void UseSkill_WhenAlreadyActed_FailsWithoutMutation()
        {
            var engine = EngineWithUnits(enemyPosition: new GridPosition(1, 0), skills: Skills());
            var actor = engine.State.GetUnit("p1");
            actor.MarkActionUsed();

            var result = engine.Execute(new UseSkillCommand("p1", "skill.damage", "e1"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.command.error.already_acted"));
            Assert.That(actor.Mana, Is.EqualTo(10));
            Assert.That(engine.State.GetUnit("e1").Health, Is.EqualTo(20));
        }

        [Test]
        public void UseSkill_WithInsufficientMana_FailsWithoutActionOrCooldown()
        {
            var engine = EngineWithUnits(
                enemyPosition: new GridPosition(1, 0),
                playerMana: 1,
                skills: Skills(mana: 2));
            var actor = engine.State.GetUnit("p1");

            var result = engine.Execute(new UseSkillCommand("p1", "skill.damage", "e1"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.skill.error.insufficient_mana"));
            Assert.That(actor.Mana, Is.EqualTo(1));
            Assert.That(actor.HasActed, Is.False);
            Assert.That(actor.Cooldowns.ContainsKey("skill.damage"), Is.False);
        }

        [Test]
        public void UseSkill_WhileOnCooldown_FailsWithoutActionOrManaCost()
        {
            var engine = EngineWithUnits(enemyPosition: new GridPosition(1, 0), skills: Skills());
            var actor = engine.State.GetUnit("p1");
            actor.Cooldowns["skill.damage"] = 1;

            var result = engine.Execute(new UseSkillCommand("p1", "skill.damage", "e1"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.skill.error.cooldown"));
            Assert.That(actor.Mana, Is.EqualTo(10));
            Assert.That(actor.HasActed, Is.False);
        }

        [Test]
        public void MoveAndThenUseSkill_ConsumesIndependentActionLimits()
        {
            var engine = EngineWithUnits(
                enemyPosition: new GridPosition(2, 0),
                skills: Skills(range: 2));
            var actor = engine.State.GetUnit("p1");

            var move = engine.Execute(new MoveCommand("p1", new GridPosition(1, 0)));
            var skill = engine.Execute(new UseSkillCommand("p1", "skill.damage", "e1"));

            Assert.That(move.Success, Is.True);
            Assert.That(skill.Success, Is.True);
            Assert.That(actor.Position, Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(actor.HasMoved, Is.True);
            Assert.That(actor.HasActed, Is.True);
        }

        [Test]
        public void EndTurn_ResolvesCurrentEndThenNextStartAndAdvances()
        {
            var engine = EngineWithThreeUnits(withSecondEnemy: true);
            var actor = engine.State.GetUnit("p1");
            var next = engine.State.GetUnit("e1");
            StatusSystem.Apply(actor, StatusType.Burning, 1, 1, "test");
            StatusSystem.Apply(next, StatusType.Poisoned, 3, 2, "test");

            var result = engine.Execute(new EndTurnCommand("p1"));

            Assert.That(result.Success, Is.True);
            Assert.That(engine.ActiveUnit, Is.SameAs(next));
            Assert.That(actor.Statuses, Is.Empty);
            Assert.That(next.Health, Is.EqualTo(17));
        }

        [Test]
        public void EndTurn_ContinuesPastUnitKilledByTurnStartDamage()
        {
            var engine = EngineWithThreeUnits(withSecondEnemy: true);
            var doomed = engine.State.GetUnit("e1");
            var following = engine.State.GetUnit("p2");
            doomed.ApplyRawDamage(doomed.Health - 1);
            StatusSystem.Apply(doomed, StatusType.Poisoned, 99, 2, "test");

            var result = engine.Execute(new EndTurnCommand("p1"));

            Assert.That(result.Success, Is.True);
            Assert.That(doomed.IsAlive, Is.False);
            Assert.That(engine.ActiveUnit, Is.SameAs(following));
            Assert.That(engine.Outcome, Is.EqualTo(BattleOutcome.InProgress));
        }

        [Test]
        public void EndTurn_SkipsStunnedUnitAndResolvesItsTurnEnd()
        {
            var engine = EngineWithThreeUnits(withSecondEnemy: true);
            var stunned = engine.State.GetUnit("e1");
            var following = engine.State.GetUnit("p2");
            StatusSystem.Apply(stunned, StatusType.Stunned, 1, 1, "test");

            var result = engine.Execute(new EndTurnCommand("p1"));

            Assert.That(result.Success, Is.True);
            Assert.That(engine.ActiveUnit, Is.SameAs(following));
            Assert.That(stunned.Statuses, Is.Empty);
            Assert.That(StatusSystem.IsStunned(stunned), Is.False);
        }

        [Test]
        public void EndTurn_WhenNotCurrentUnit_FailsWithoutAdvancing()
        {
            var engine = EngineWithThreeUnits(withSecondEnemy: true);
            var active = engine.ActiveUnit;

            var result = engine.Execute(new EndTurnCommand("e1"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.command.error.not_active_unit"));
            Assert.That(engine.ActiveUnit, Is.SameAs(active));
        }

        [Test]
        public void EndTurn_DecrementsCooldownAtNextOwnTurn()
        {
            var engine = EngineWithUnits(enemyPosition: new GridPosition(1, 0), skills: Skills());
            var actor = engine.State.GetUnit("p1");
            engine.Execute(new UseSkillCommand("p1", "skill.damage", "e1"));

            engine.Execute(new EndTurnCommand("p1"));
            engine.Execute(new EndTurnCommand("e1"));

            Assert.That(engine.ActiveUnit, Is.SameAs(actor));
            Assert.That(actor.Cooldowns["skill.damage"], Is.EqualTo(1));
        }

        [Test]
        public void Outcome_IsPlayerVictory_WhenAllEnemiesDie()
        {
            var engine = EngineWithUnits();
            engine.State.GetUnit("e1").ApplyRawDamage(999);

            engine.Execute(new EndTurnCommand("p1"));

            Assert.That(engine.Outcome, Is.EqualTo(BattleOutcome.PlayerVictory));
        }

        [Test]
        public void Outcome_IsEnemyVictory_WhenAllPlayersDie()
        {
            var engine = EngineWithUnits(
                enemyPosition: new GridPosition(1, 0),
                playerSpeed: 1,
                playerHealth: 1,
                enemySpeed: 2,
                enemyMana: 10,
                skills: Skills());

            var result = engine.Execute(new UseSkillCommand("e1", "skill.damage", "p1"));

            Assert.That(result.Success, Is.True);
            Assert.That(engine.State.GetUnit("p1").IsAlive, Is.False);
            Assert.That(engine.Outcome, Is.EqualTo(BattleOutcome.EnemyVictory));
        }

        [Test]
        public void Outcome_IsPlayerVictory_WhenSkillKillsAllEnemies()
        {
            var engine = EngineWithUnits(
                enemyPosition: new GridPosition(1, 0),
                enemyHealth: 1,
                skills: Skills());

            var result = engine.Execute(new UseSkillCommand("p1", "skill.damage", "e1"));

            Assert.That(result.Success, Is.True);
            Assert.That(engine.Outcome, Is.EqualTo(BattleOutcome.PlayerVictory));
        }

        [Test]
        public void BattleEnded_RejectsEveryStateChangingCommand()
        {
            var engine = EngineWithUnits(skills: Skills());
            var actor = engine.State.GetUnit("p1");
            engine.State.GetUnit("e1").ApplyRawDamage(999);
            engine.Execute(new EndTurnCommand("p1"));
            var active = engine.ActiveUnit;

            var move = engine.Execute(new MoveCommand("p1", new GridPosition(1, 0)));
            var skill = engine.Execute(new UseSkillCommand("p1", "skill.damage", "e1"));
            var endTurn = engine.Execute(new EndTurnCommand("p1"));

            Assert.That(move.Success, Is.False);
            Assert.That(skill.Success, Is.False);
            Assert.That(endTurn.Success, Is.False);
            Assert.That(move.ErrorCode, Is.EqualTo("battle.command.error.battle_finished"));
            Assert.That(skill.ErrorCode, Is.EqualTo("battle.command.error.battle_finished"));
            Assert.That(endTurn.ErrorCode, Is.EqualTo("battle.command.error.battle_finished"));
            Assert.That(actor.Position, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(actor.HasMoved, Is.False);
            Assert.That(actor.HasActed, Is.False);
            Assert.That(actor.Mana, Is.EqualTo(10));
            Assert.That(engine.ActiveUnit, Is.SameAs(active));
        }

        [Test]
        public void Execute_UnknownCommand_StillEvaluatesOutcome()
        {
            var engine = EngineWithUnits();
            engine.State.GetUnit("e1").ApplyRawDamage(999);

            var result = engine.Execute(new UnknownCommand("p1"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.command.error.invalid_command"));
            Assert.That(engine.Outcome, Is.EqualTo(BattleOutcome.PlayerVictory));
        }
        [Test]
        public void Execute_ReturnsFreshSuccessAndFailureResults()
        {
            var engine = EngineWithUnits();
            BattleActionResult success = null;
            BattleActionResult failure = null;

            Assert.DoesNotThrow(() => success = engine.Execute(
                new MoveCommand("p1", new GridPosition(1, 0))));
            Assert.DoesNotThrow(() => failure = engine.Execute(
                new MoveCommand("e1", new GridPosition(3, 0))));

            Assert.That(success, Is.Not.Null);
            Assert.That(failure, Is.Not.Null);
            Assert.That(success, Is.Not.SameAs(failure));
            Assert.That(success.Success, Is.True);
            Assert.That(failure.Success, Is.False);
            Assert.That(success.Message, Does.StartWith("battle."));
            Assert.That(failure.Message, Does.StartWith("battle."));
        }

        [Test]
        public void UseSkill_WhenTauntedTargetsNonSource_FailsBeforeAnyMutation()
        {
            var engine = EngineWithUnits(
                enemyPosition: new GridPosition(1, 0),
                secondEnemyPosition: new GridPosition(3, 0),
                skills: Skills(range: 4));
            var actor = engine.State.GetUnit("p1");
            var source = engine.State.GetUnit("e1");
            var other = engine.State.GetUnit("e2");
            StatusSystem.Apply(actor, StatusType.Taunted, 1, 2, source.Id);

            var result = engine.Execute(new UseSkillCommand("p1", "skill.damage", "e2"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.command.error.taunted"));
            Assert.That(actor.Mana, Is.EqualTo(10));
            Assert.That(actor.HasActed, Is.False);
            Assert.That(actor.Cooldowns, Is.Empty);
            Assert.That(source.Health, Is.EqualTo(20));
            Assert.That(other.Health, Is.EqualTo(20));
        }

        [Test]
        public void UseSkill_WithCanCrit_UsesRealEnginePathAndCrits()
        {
            var engine = EngineWithUnits(
                enemyPosition: new GridPosition(1, 0),
                playerCritChance: 1f,
                skills: Skills(canCrit: true));
            var actor = engine.State.GetUnit("p1");
            var target = engine.State.GetUnit("e1");

            var result = engine.Execute(new UseSkillCommand("p1", "skill.damage", "e1"));

            Assert.That(result.Success, Is.True);
            Assert.That(target.Health, Is.EqualTo(8));
            Assert.That(actor.Mana, Is.EqualTo(8));
        }

        [Test]
        public void UseSkill_WithSameSeedAndCommand_IsDeterministic()
        {
            var skills = Skills(canCrit: true);
            var firstEngine = EngineWithUnits(
                enemyPosition: new GridPosition(1, 0),
                playerCritChance: 0.5f,
                skills: skills);
            var secondEngine = EngineWithUnits(
                enemyPosition: new GridPosition(1, 0),
                playerCritChance: 0.5f,
                skills: skills);

            var first = firstEngine.Execute(new UseSkillCommand("p1", "skill.damage", "e1"));
            var second = secondEngine.Execute(new UseSkillCommand("p1", "skill.damage", "e1"));

            Assert.That(first.Success, Is.True);
            Assert.That(second.Success, Is.True);
            Assert.That(
                secondEngine.State.GetUnit("e1").Health,
                Is.EqualTo(firstEngine.State.GetUnit("e1").Health));
            Assert.That(
                secondEngine.State.GetUnit("p1").Mana,
                Is.EqualTo(firstEngine.State.GetUnit("p1").Mana));
            Assert.That(
                secondEngine.State.GetUnit("p1").Cooldowns["skill.damage"],
                Is.EqualTo(firstEngine.State.GetUnit("p1").Cooldowns["skill.damage"]));
        }
        private sealed class UnknownCommand : BattleCommand
        {
            public UnknownCommand(string unitId)
                : base(unitId)
            {
            }
        }
        private static BattleEngine EngineWithUnits(
            GridPosition? playerPosition = null,
            GridPosition? enemyPosition = null,
            int playerSpeed = 6,
            int playerMana = 10,
            int playerHealth = 20,
            int enemyHealth = 20,
            int enemySpeed = 2,
            int enemyMana = 0,
            int width = 5,
            float playerCritChance = 0f,
            IReadOnlyDictionary<string, SkillDefinition> skills = null,
            GridPosition? secondEnemyPosition = null)
        {
            var state = new BattleState(BattleMap.CreatePlain(width, 1));
            state.AddUnit(Unit(
                "p1",
                Team.Player,
                playerHealth,
                playerMana,
                8,
                playerSpeed,
                playerPosition ?? new GridPosition(0, 0),
                playerCritChance));
            state.AddUnit(Unit(
                "e1",
                Team.Enemy,
                enemyHealth,
                enemyMana,
                8,
                enemySpeed,
                enemyPosition ?? new GridPosition(2, 0)));
            if (secondEnemyPosition.HasValue)
            {
                state.AddUnit(Unit(
                    "e2",
                    Team.Enemy,
                    enemyHealth,
                    enemyMana,
                    8,
                    enemySpeed,
                    secondEnemyPosition.Value));
            }

            IReadOnlyDictionary<string, string[]> unitSkills = null;
            if (skills != null)
            {
                var mappings = new Dictionary<string, string[]>
                {
                    ["p1"] = skills.Keys.ToArray(),
                    ["e1"] = skills.Keys.ToArray()
                };
                if (secondEnemyPosition.HasValue)
                    mappings["e2"] = skills.Keys.ToArray();
                unitSkills = mappings;
            }

            var engine = new BattleEngine(
                state,
                RandomSourceFactory.FromSeed("battle"),
                skills,
                unitSkills);
            engine.Start();
            return engine;
        }

        private static BattleEngine EngineWithThreeUnits(bool withSecondEnemy = false)
        {
            var state = new BattleState(BattleMap.CreatePlain(5, 1));
            state.AddUnit(Unit("p1", Team.Player, 20, 10, 8, 9, new GridPosition(0, 0)));
            state.AddUnit(Unit("e1", Team.Enemy, 20, 0, 8, 8, new GridPosition(1, 0)));
            state.AddUnit(Unit("p2", Team.Player, 20, 10, 8, 7, new GridPosition(2, 0)));
            if (withSecondEnemy)
                state.AddUnit(Unit("e2", Team.Enemy, 20, 0, 8, 6, new GridPosition(4, 0)));
            var engine = new BattleEngine(state, RandomSourceFactory.FromSeed("battle"));
            engine.Start();
            return engine;
        }

        private static BattleUnit Unit(
            string id,
            Team team,
            int maxHealth,
            int maxMana,
            int power,
            int speed,
            GridPosition position,
            float critChance = 0f) =>
            new BattleUnit(
                id,
                "test.unit",
                team,
                new UnitStats(maxHealth, maxMana, power, 0, speed, critChance, 0),
                position);

        private static IReadOnlyDictionary<string, SkillDefinition> Skills(
            int mana = 2,
            int cooldown = 2,
            int range = 1,
            bool canCrit = true) =>
            new Dictionary<string, SkillDefinition>
            {
                ["skill.damage"] = new SkillDefinition(
                    "skill.damage",
                    "skill.damage.name",
                    SkillTargeting.Enemy,
                    range,
                    0,
                    mana,
                    cooldown,
                    new SkillEffectDefinition(
                        SkillEffectKind.Damage,
                        1f,
                        StatusType.Burning,
                        0,
                        0,
                        canCrit: canCrit))
            };
    }
}
