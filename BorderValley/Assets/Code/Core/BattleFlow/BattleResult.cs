namespace BorderValley.Core.BattleFlow
{
    public sealed class BattleResult
    {
        public BattleResult(BattleFlowOutcome outcome, int rounds)
        {
            Outcome = outcome;
            Rounds = rounds < 0 ? 0 : rounds;
        }

        public BattleFlowOutcome Outcome { get; }
        public int Rounds { get; }
    }
}
