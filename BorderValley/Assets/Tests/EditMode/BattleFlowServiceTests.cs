using BorderValley.Core.BattleFlow;
using NUnit.Framework;

namespace BorderValley.EditModeTests
{
    public sealed class BattleFlowServiceTests
    {
        [Test]
        public void RequestAndResult_AreConsumedOnce()
        {
            var flow = new BattleFlowService();
            var request = new BattleRequest("core", "seed-1", "World");
            var result = new BattleResult(BattleFlowOutcome.PlayerVictory, 7);

            flow.BeginBattle(request);
            Assert.That(flow.TryTakeRequest(out var takenRequest), Is.True);
            Assert.That(takenRequest.ScenarioId, Is.EqualTo("core"));
            Assert.That(flow.TryTakeRequest(out _), Is.False);

            flow.CompleteBattle(result);
            Assert.That(flow.TryTakeResult(out var takenResult), Is.True);
            Assert.That(takenResult.Outcome, Is.EqualTo(BattleFlowOutcome.PlayerVictory));
            Assert.That(takenResult.Rounds, Is.EqualTo(7));
            Assert.That(flow.TryTakeResult(out _), Is.False);
        }

        [Test]
        public void BeginBattle_RejectsSecondActiveRequest()
        {
            var flow = new BattleFlowService();
            flow.BeginBattle(new BattleRequest("core", "a", "World"));

            Assert.That(
                () => flow.BeginBattle(new BattleRequest("core", "b", "World")),
                Throws.InvalidOperationException);
        }
    }
}
