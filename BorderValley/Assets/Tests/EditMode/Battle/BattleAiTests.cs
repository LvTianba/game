using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Battle.Domain;
using BorderValley.Core.Random;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class BattleAiTests
    {
        [Test]
        public void ChooseCommand_PrefersLethalAttack()
        {
            var skills = Skills(BasicAttack());
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 1, speed: 6),
                    Unit("p1", Team.Player, 0, speed: 5)
                },
                skills);
            var player = engine.State.GetUnit("p1");
            player.ApplyRawDamage(player.Health - 1);

            var command = BattleAi.ChooseCommand(engine, "e1", skills);

            Assert.That(command, Is.TypeOf<UseSkillCommand>());
            Assert.That(((UseSkillCommand)command).SkillId, Is.EqualTo("skill.basic"));
            Assert.That(((UseSkillCommand)command).TargetUnitId, Is.EqualTo("p1"));
        }

        [Test]
        public void ChooseCommand_HigherPowerRaisesLethalThreatScore()
        {
            var skills = Skills(BasicAttack());
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 1, speed: 6),
                    Unit("p1", Team.Player, 0, speed: 5, power: 2),
                    Unit("p2", Team.Player, 2, speed: 4, power: 10)
                },
                skills);
            engine.State.GetUnit("p1").ApplyRawDamage(19);
            engine.State.GetUnit("p2").ApplyRawDamage(19);

            var command = (UseSkillCommand)BattleAi.ChooseCommand(engine, "e1", skills);

            Assert.That(command.TargetUnitId, Is.EqualTo("p2"));
        }

        [Test]
        public void ChooseCommand_PrefersDamageOverHealing()
        {
            var skills = new Dictionary<string, SkillDefinition>
            {
                ["damage"] = Skill(
                    "skill.damage",
                    SkillTargeting.Enemy,
                    1,
                    Damage(1f)),
                ["heal"] = Skill(
                    "skill.heal",
                    SkillTargeting.Self,
                    0,
                    Heal(10))
            };
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 0, speed: 1, power: 12),
                    Unit("p1", Team.Player, 1, speed: 0, maxHealth: 30)
                },
                skills);
            engine.State.GetUnit("e1").ApplyRawDamage(10);

            var command = (UseSkillCommand)BattleAi.ChooseCommand(engine, "e1", skills);

            Assert.That(command.SkillId, Is.EqualTo("damage"));
            Assert.That(command.TargetUnitId, Is.EqualTo("p1"));
        }

        [Test]
        public void ChooseCommand_PrefersHealingDamagedAlly()
        {
            var skills = Skills(Skill("skill.heal", SkillTargeting.Ally, 2, Heal(5)));
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 0, speed: 1),
                    Unit("a1", Team.Enemy, 1, speed: 0),
                    Unit("p1", Team.Player, 5, speed: 0)
                },
                skills);
            engine.State.GetUnit("a1").ApplyRawDamage(5);

            var command = (UseSkillCommand)BattleAi.ChooseCommand(engine, "e1", skills);

            Assert.That(command.SkillId, Is.EqualTo("skill.heal"));
            Assert.That(command.TargetUnitId, Is.EqualTo("a1"));
        }

        [Test]
        public void ChooseCommand_PrefersStunOverTauntAndSlow()
        {
            var skills = new Dictionary<string, SkillDefinition>
            {
                ["stun"] = StatusSkill("skill.stun", StatusType.Stunned),
                ["taunt"] = StatusSkill("skill.taunt", StatusType.Taunted),
                ["slow"] = StatusSkill("skill.slow", StatusType.Slowed)
            };
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 0, speed: 1),
                    Unit("p1", Team.Player, 1, speed: 0)
                },
                skills);

            var command = (UseSkillCommand)BattleAi.ChooseCommand(engine, "e1", skills);

            Assert.That(command.SkillId, Is.EqualTo("stun"));
        }

        [Test]
        public void ChooseCommand_PrefersTauntOverSlow()
        {
            var skills = new Dictionary<string, SkillDefinition>
            {
                ["taunt"] = StatusSkill("skill.taunt", StatusType.Taunted),
                ["slow"] = StatusSkill("skill.slow", StatusType.Slowed)
            };
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 0, speed: 1),
                    Unit("p1", Team.Player, 1, speed: 0)
                },
                skills);

            var command = (UseSkillCommand)BattleAi.ChooseCommand(engine, "e1", skills);

            Assert.That(command.SkillId, Is.EqualTo("taunt"));
        }

        [Test]
        public void ChooseCommand_ScoresShieldByAmount()
        {
            var skills = new Dictionary<string, SkillDefinition>
            {
                ["shield"] = StatusSkill(
                    "skill.shield",
                    StatusType.Shielded,
                    SkillTargeting.Self,
                    80),
                ["heal"] = Skill("skill.heal", SkillTargeting.Self, 0, Heal(100))
            };
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 0, speed: 1),
                    Unit("p1", Team.Player, 5, speed: 0)
                },
                skills);

            var command = (UseSkillCommand)BattleAi.ChooseCommand(engine, "e1", skills);

            Assert.That(command.SkillId, Is.EqualTo("shield"));
            Assert.That(command.TargetUnitId, Is.EqualTo("e1"));
        }

        [Test]
        public void ChooseCommand_WithNoTargetInRange_MovesTowardEnemy()
        {
            var skills = Skills(BasicAttack());
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 5, speed: 2),
                    Unit("p1", Team.Player, 0, speed: 0)
                },
                skills,
                width: 7);

            var command = BattleAi.ChooseCommand(engine, "e1", skills);

            Assert.That(command, Is.TypeOf<MoveCommand>());
            Assert.That(((MoveCommand)command).Destination, Is.EqualTo(new GridPosition(3, 0)));
        }

        [Test]
        public void ChooseCommand_PrefersMoveThatEntersSkillRange()
        {
            var skills = Skills(Skill(
                "skill.basic",
                SkillTargeting.Enemy,
                3,
                Damage(1f)));
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 0, speed: 3),
                    Unit("p1", Team.Player, 5, speed: 0)
                },
                skills,
                width: 7);

            var command = BattleAi.ChooseCommand(engine, "e1", skills);

            Assert.That(command, Is.TypeOf<MoveCommand>());
            Assert.That(((MoveCommand)command).Destination, Is.EqualTo(new GridPosition(3, 0)));
        }

        [Test]
        public void ChooseCommand_IsDeterministicAndDoesNotMutateState()
        {
            var skills = Skills(BasicAttack());
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 1, speed: 6),
                    Unit("p1", Team.Player, 0, speed: 5)
                },
                skills);
            var actor = engine.State.GetUnit("e1");
            var target = engine.State.GetUnit("p1");
            target.ApplyRawDamage(target.Health - 1);
            var healthBefore = target.Health;
            var manaBefore = actor.Mana;
            var movedBefore = actor.HasMoved;
            var actedBefore = actor.HasActed;

            var first = BattleAi.ChooseCommand(engine, "e1", skills);
            var second = BattleAi.ChooseCommand(engine, "e1", skills);

            AssertCommandEqual(second, first);
            Assert.That(actor.Mana, Is.EqualTo(manaBefore));
            Assert.That(actor.HasMoved, Is.EqualTo(movedBefore));
            Assert.That(actor.HasActed, Is.EqualTo(actedBefore));
            Assert.That(target.Health, Is.EqualTo(healthBefore));
        }

        [Test]
        public void ChooseCommand_TieBreaksBySkillIdOrdinal()
        {
            var skills = new Dictionary<string, SkillDefinition>
            {
                ["key.b"] = Skill("skill.b", SkillTargeting.Enemy, 1, Damage(1f)),
                ["key.z"] = Skill("skill.a", SkillTargeting.Enemy, 1, Damage(1f))
            };
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 0, speed: 1),
                    Unit("p1", Team.Player, 1, speed: 0)
                },
                skills);

            var command = (UseSkillCommand)BattleAi.ChooseCommand(engine, "e1", skills);

            Assert.That(command.SkillId, Is.EqualTo("key.z"));
        }

        [Test]
        public void ChooseCommand_TieBreaksByTargetUnitIdOrdinal()
        {
            var skills = Skills(BasicAttack());
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 1, speed: 1),
                    Unit("p2", Team.Player, 2, speed: 0),
                    Unit("p1", Team.Player, 0, speed: 0)
                },
                skills);

            var command = (UseSkillCommand)BattleAi.ChooseCommand(engine, "e1", skills);

            Assert.That(command.TargetUnitId, Is.EqualTo("p1"));
        }

        [Test]
        public void ChooseCommand_TieBreaksMoveByTargetCellCoordinates()
        {
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 3, 3, speed: 1),
                    Unit("p1", Team.Player, 3, 0, speed: 0),
                    Unit("p2", Team.Player, 3, 6, speed: 0)
                },
                skills: null,
                width: 7,
                height: 7);

            var command = BattleAi.ChooseCommand(engine, "e1",
                new Dictionary<string, SkillDefinition>());

            Assert.That(command, Is.TypeOf<MoveCommand>());
            Assert.That(((MoveCommand)command).Destination, Is.EqualTo(new GridPosition(3, 2)));
        }

        [Test]
        public void ChooseCommand_WithNoLegalAction_EndsTurn()
        {
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 0, speed: 0),
                    Unit("p1", Team.Player, 5, speed: 0)
                },
                skills: null);

            var command = BattleAi.ChooseCommand(engine, "e1",
                new Dictionary<string, SkillDefinition>());

            Assert.That(command, Is.TypeOf<EndTurnCommand>());
            Assert.That(command.UnitId, Is.EqualTo("e1"));
        }

        [Test]
        public void ChooseCommand_WhenAlreadyMoved_DoesNotMoveAgain()
        {
            var actor = Unit("e1", Team.Enemy, 0, speed: 1);
            var engine = Engine(
                new[]
                {
                    actor,
                    Unit("p1", Team.Player, 5, speed: 0)
                },
                skills: null);
            actor.MoveTo(new GridPosition(1, 0));

            var command = BattleAi.ChooseCommand(engine, "e1",
                new Dictionary<string, SkillDefinition>());

            Assert.That(command, Is.TypeOf<EndTurnCommand>());
        }

        [Test]
        public void ChooseCommand_WithInsufficientManaOrCooldown_SkipsSkill()
        {
            var skill = Skill("skill.expensive", SkillTargeting.Enemy, 1, new[] { Damage(1f) }, 1);
            var skills = Skills(skill);
            var actor = Unit("e1", Team.Enemy, 0, speed: 0, maxMana: 0);
            var engine = Engine(
                new[]
                {
                    actor,
                    Unit("p1", Team.Player, 1, speed: 0)
                },
                skills);

            var command = BattleAi.ChooseCommand(engine, "e1", skills);

            Assert.That(command, Is.TypeOf<EndTurnCommand>());
        }

        [Test]
        public void ChooseCommand_GroundSkillUsesOccupiedAnchorUnit()
        {
            var skills = Skills(Skill(
                "skill.quake",
                SkillTargeting.Ground,
                2,
                Damage(1f)));
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 0, speed: 1),
                    Unit("p1", Team.Player, 1, speed: 0)
                },
                skills);

            var command = (UseSkillCommand)BattleAi.ChooseCommand(engine, "e1", skills);

            Assert.That(command.SkillId, Is.EqualTo("skill.quake"));
            Assert.That(command.TargetUnitId, Is.EqualTo("p1"));
        }

        [Test]
        public void ChooseCommand_WhenNotActiveUnit_Throws()
        {
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 0, speed: 1),
                    Unit("p1", Team.Player, 5, speed: 0)
                },
                skills: null);

            Assert.Throws<InvalidOperationException>(() =>
                BattleAi.ChooseCommand(engine, "p1",
                    new Dictionary<string, SkillDefinition>()));
        }

        [Test]
        public void ChooseCommand_WhenBattleNotStarted_Throws()
        {
            var state = new BattleState(BattleMap.CreatePlain(2, 1));
            state.AddUnit(Unit("e1", Team.Enemy, 0));
            state.AddUnit(Unit("p1", Team.Player, 1));
            var engine = new BattleEngine(state, RandomSourceFactory.FromSeed("ai"));

            Assert.Throws<InvalidOperationException>(() =>
                BattleAi.ChooseCommand(engine, "e1",
                    new Dictionary<string, SkillDefinition>()));
        }

        [Test]
        public void ChooseCommand_WithInvalidArguments_Throws()
        {
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 0, speed: 1),
                    Unit("p1", Team.Player, 1, speed: 0)
                },
                skills: null);
            var skills = new Dictionary<string, SkillDefinition>();

            Assert.Throws<ArgumentNullException>(() =>
                BattleAi.ChooseCommand(null, "e1", skills));
            Assert.Throws<ArgumentException>(() =>
                BattleAi.ChooseCommand(engine, " ", skills));
            Assert.Throws<ArgumentNullException>(() =>
                BattleAi.ChooseCommand(engine, "e1", null));
            Assert.Throws<ArgumentException>(() =>
                BattleAi.ChooseCommand(
                    engine,
                    "e1",
                    new Dictionary<string, SkillDefinition> { ["bad"] = null }));
        }

        [Test]
        public void ChooseCommand_NoTargetInHealRange_MovesIntoHealingRange()
        {
            var skills = Skills(Skill("skill.heal", SkillTargeting.Ally, 1, Heal(5)));
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 2, speed: 1),
                    Unit("a1", Team.Enemy, 4, speed: 0),
                    Unit("p1", Team.Player, 0, speed: 0)
                },
                skills);
            engine.State.GetUnit("a1").ApplyRawDamage(5);

            var command = BattleAi.ChooseCommand(engine, "e1", skills);

            Assert.That(command, Is.TypeOf<MoveCommand>());
            Assert.That(((MoveCommand)command).Destination, Is.EqualTo(new GridPosition(3, 0)));
        }

        [Test]
        public void ChooseCommand_NoTargetInShieldRange_MovesIntoShieldRange()
        {
            var skills = Skills(StatusSkill(
                "skill.shield",
                StatusType.Shielded,
                SkillTargeting.Ally,
                5));
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 2, speed: 1),
                    Unit("a1", Team.Enemy, 4, speed: 0),
                    Unit("p1", Team.Player, 0, speed: 0)
                },
                skills);

            var command = BattleAi.ChooseCommand(engine, "e1", skills);

            Assert.That(command, Is.TypeOf<MoveCommand>());
            Assert.That(((MoveCommand)command).Destination, Is.EqualTo(new GridPosition(3, 0)));
        }

        [Test]
        public void ChooseCommand_GroundAreaSkill_UsesLegalAnchorRadiusAndEffectTeam()
        {
            var skills = new Dictionary<string, SkillDefinition>
            {
                ["quake"] = GroundSkill(
                    "skill.quake",
                    2,
                    2,
                    Damage(1f))
            };
            var map = new BattleMap(5, 2, new[]
            {
                TerrainType.Plain, TerrainType.Plain, TerrainType.Bush,
                TerrainType.Plain, TerrainType.Plain,
                TerrainType.Plain, TerrainType.Plain, TerrainType.Plain,
                TerrainType.Plain, TerrainType.Plain
            });
            var state = new BattleState(map);
            state.AddUnit(Unit("e1", Team.Enemy, 0, 1, speed: 1));
            state.AddUnit(Unit("a1", Team.Enemy, 2, 0, speed: 0));
            state.AddUnit(Unit("p1", Team.Player, 4, 0, speed: 0));
            var engine = new BattleEngine(state, RandomSourceFactory.FromSeed("ai"), skills);
            engine.Start();

            var command = BattleAi.ChooseCommand(engine, "e1", skills);

            Assert.That(command, Is.TypeOf<MoveCommand>());
            Assert.That(((MoveCommand)command).Destination, Is.EqualTo(new GridPosition(0, 0)));
        }

        [Test]
        public void ChooseCommand_DictionaryKeyDifferentFromDefinitionId_ExecutesSuccessfully()
        {
            var skills = new Dictionary<string, SkillDefinition>
            {
                ["basic"] = Skill("skill.basic", SkillTargeting.Enemy, 1, Damage(1f))
            };
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 1, speed: 6),
                    Unit("p1", Team.Player, 0, speed: 5)
                },
                skills);

            var command = BattleAi.ChooseCommand(engine, "e1", skills);

            Assert.That(command, Is.TypeOf<UseSkillCommand>());
            Assert.That(((UseSkillCommand)command).SkillId, Is.EqualTo("basic"));
            Assert.That(engine.Execute(command).Success, Is.True);
        }

        [Test]
        public void ChooseCommand_DuplicateSkillDefinitionId_Throws()
        {
            var skill = BasicAttack();
            var skills = new Dictionary<string, SkillDefinition>
            {
                ["basic.a"] = skill,
                ["basic.b"] = skill
            };
            var engine = Engine(
                new[]
                {
                    Unit("e1", Team.Enemy, 1, speed: 6),
                    Unit("p1", Team.Player, 0, speed: 5)
                },
                skills);

            Assert.Throws<ArgumentException>(() =>
                BattleAi.ChooseCommand(engine, "e1", skills));
        }

        private static SkillDefinition GroundSkill(
            string id,
            int range,
            int radius,
            params SkillEffectDefinition[] effects) =>
            new(id, id + ".name", SkillTargeting.Ground, range, radius, 0, 0, effects);
        private static Dictionary<string, SkillDefinition> Skills(params SkillDefinition[] skills) =>
            skills.ToDictionary(skill => skill.Id, skill => skill, StringComparer.Ordinal);

        private static SkillDefinition BasicAttack() =>
            Skill("skill.basic", SkillTargeting.Enemy, 1, Damage(1f));

        private static SkillDefinition StatusSkill(
            string id,
            StatusType status,
            SkillTargeting targeting = SkillTargeting.Enemy,
            int magnitude = 1) =>
            Skill(
                id,
                targeting,
                1,
                new SkillEffectDefinition(
                    SkillEffectKind.ApplyStatus,
                    0f,
                    status,
                    magnitude,
                    2));

        private static SkillEffectDefinition Damage(float multiplier) =>
            new(SkillEffectKind.Damage, multiplier, default, 0, 0);

        private static SkillEffectDefinition Heal(int amount) =>
            new(SkillEffectKind.Heal, 0f, default, amount, 0);

        private static SkillDefinition Skill(
            string id,
            SkillTargeting targeting,
            int range,
            params SkillEffectDefinition[] effects) =>
            Skill(id, targeting, range, effects, 0);

        private static SkillDefinition Skill(
            string id,
            SkillTargeting targeting,
            int range,
            SkillEffectDefinition[] effects,
            int mana) =>
            new(
                id,
                id + ".name",
                targeting,
                range,
                0,
                mana,
                0,
                effects);

        private static BattleEngine Engine(
            IEnumerable<BattleUnit> units,
            IReadOnlyDictionary<string, SkillDefinition> skills,
            int width = 7,
            int height = 1)
        {
            var state = new BattleState(BattleMap.CreatePlain(width, height));
            foreach (var unit in units) state.AddUnit(unit);
            var engine = new BattleEngine(state, RandomSourceFactory.FromSeed("ai"), skills);
            engine.Start();
            return engine;
        }

        private static BattleUnit Unit(
            string id,
            Team team,
            int x,
            int y = 0,
            int maxHealth = 20,
            int maxMana = 10,
            int power = 8,
            int armor = 0,
            int speed = 5,
            float critChance = 0f,
            int resistance = 0) =>
            new(
                id,
                "test.unit",
                team,
                new UnitStats(maxHealth, maxMana, power, armor, speed, critChance, resistance),
                new GridPosition(x, y));

        private static void AssertCommandEqual(BattleCommand actual, BattleCommand expected)
        {
            Assert.That(actual.GetType(), Is.EqualTo(expected.GetType()));
            Assert.That(actual.UnitId, Is.EqualTo(expected.UnitId));
            if (expected is MoveCommand expectedMove)
            {
                Assert.That(((MoveCommand)actual).Destination, Is.EqualTo(expectedMove.Destination));
            }
            else if (expected is UseSkillCommand expectedSkill)
            {
                var actualSkill = (UseSkillCommand)actual;
                Assert.That(actualSkill.SkillId, Is.EqualTo(expectedSkill.SkillId));
                Assert.That(actualSkill.TargetUnitId, Is.EqualTo(expectedSkill.TargetUnitId));
            }
        }
    }
}
