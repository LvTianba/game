using System.Collections.Generic;
using BorderValley.Battle.Domain;
using BorderValley.Core.Random;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class BattleGroundTargetCommandTests
    {
        [Test]
        public void UseSkillCommand_CanTargetEmptyGroundCell()
        {
            var engine = GroundSkillEngine(out var actor, out var enemy);
            actor.MoveForced(new GridPosition(0, 0));
            enemy.MoveForced(new GridPosition(2, 0));

            var result = engine.Execute(new UseSkillCommand(
                actor.Id,
                "skill.quake",
                new GridPosition(1, 0)));

            Assert.That(result.Success, Is.True);
            Assert.That(actor.HasActed, Is.True);
            Assert.That(actor.Mana, Is.EqualTo(8));
            Assert.That(enemy.Health, Is.EqualTo(12));
        }

        [Test]
        public void UseSkillCommand_GroundSkillWithoutPosition_FailsWithoutMutation()
        {
            var engine = GroundSkillEngine(out var actor, out _);

            var result = engine.Execute(new UseSkillCommand(actor.Id, "skill.quake", actor.Id));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.command.error.target_position_required"));
            Assert.That(actor.HasActed, Is.False);
            Assert.That(actor.Mana, Is.EqualTo(10));
        }

        private static BattleEngine GroundSkillEngine(
            out BattleUnit actor,
            out BattleUnit enemy)
        {
            var state = new BattleState(BattleMap.CreatePlain(4, 1));
            actor = new BattleUnit(
                "p1",
                "unit.test",
                Team.Player,
                new UnitStats(20, 10, 8, 0, 5, 0f, 0),
                new GridPosition(0, 0));
            enemy = new BattleUnit(
                "e1",
                "unit.test",
                Team.Enemy,
                new UnitStats(20, 0, 8, 0, 1, 0f, 0),
                new GridPosition(2, 0));
            state.AddUnit(actor);
            state.AddUnit(enemy);
            var skill = new SkillDefinition(
                "skill.quake",
                "skill.quake.name",
                SkillTargeting.Ground,
                2,
                1,
                2,
                0,
                new SkillEffectDefinition(SkillEffectKind.Damage, 1f, StatusType.Burning, 0, 0));
            var skills = new Dictionary<string, SkillDefinition> { [skill.Id] = skill };
            var ownership = new Dictionary<string, string[]> { [actor.Id] = new[] { skill.Id } };
            var engine = new BattleEngine(
                state,
                RandomSourceFactory.FromSeed("ground-target"),
                skills,
                ownership);
            engine.Start();
            return engine;
        }
    }
}
