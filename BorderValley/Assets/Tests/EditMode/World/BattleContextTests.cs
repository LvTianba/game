using BorderValley.Core.BattleFlow;
using NUnit.Framework;

namespace BorderValley.World.Tests
{
    public sealed class BattleContextTests
    {
        [Test]
        public void RequestAndResult_PreserveEncounterContext()
        {
            var context = new BattleContext(
                "encounter.forest.bandits",
                "loot.bandit.core",
                30,
                40,
                new[] { "enemy.bandit", "enemy.ranger" },
                false);
            var request = new BattleRequest("core", "seed-1", "World", null, context);
            var result = new BattleResult(
                BattleFlowOutcome.PlayerVictory,
                3,
                new[] { new BattleUnitResult("unit.bandit", "enemy.bandit", 0, 0) },
                context);

            Assert.That(request.Context, Is.SameAs(context));
            Assert.That(request.Context.EncounterId, Is.EqualTo("encounter.forest.bandits"));
            Assert.That(request.Context.GoldReward, Is.EqualTo(30));
            Assert.That(request.Context.EnemyDefinitionIds, Is.EqualTo(new[] { "enemy.bandit", "enemy.ranger" }));
            Assert.That(result.Context, Is.SameAs(context));
            Assert.That(result.Context.RewardTableId, Is.EqualTo("loot.bandit.core"));
            Assert.That(result.UnitStates[0].DefinitionId, Is.EqualTo("enemy.bandit"));
        }

        [Test]
        public void LegacyRequestAndUnitResult_PreserveExistingDefaults()
        {
            var request = new BattleRequest("core", "seed-1", "World");
            var unit = new BattleUnitResult("unit.bandit", 0, 0);

            Assert.That(request.Context, Is.Null);
            Assert.That(unit.DefinitionId, Is.EqualTo("unit.bandit"));
        }
    }
}
