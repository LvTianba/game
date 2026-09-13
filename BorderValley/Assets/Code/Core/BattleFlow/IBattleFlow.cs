namespace BorderValley.Core.BattleFlow
{
    public interface IBattleFlow
    {
        void BeginBattle(BattleRequest request);
        bool TryTakeRequest(out BattleRequest request);
        void CompleteBattle(BattleResult result);
        bool TryTakeResult(out BattleResult result);
    }
}
