namespace BorderValley.Core.BattleFlow
{
    public sealed class BattleFlowService : IBattleFlow
    {
        private BattleRequest request;
        private BattleResult result;

        public void BeginBattle(BattleRequest value)
        {
            if (value == null) throw new System.ArgumentNullException(nameof(value));
            if (request != null) throw new System.InvalidOperationException("A battle request is already active.");
            request = value;
        }

        public bool TryTakeRequest(out BattleRequest value)
        {
            value = request;
            request = null;
            return value != null;
        }

        public void CompleteBattle(BattleResult value) =>
            result = value ?? throw new System.ArgumentNullException(nameof(value));

        public bool TryTakeResult(out BattleResult value)
        {
            value = result;
            result = null;
            return value != null;
        }
    }
}
