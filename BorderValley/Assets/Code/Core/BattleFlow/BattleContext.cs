namespace BorderValley.Core.BattleFlow
{
    public sealed class BattleContext
    {
        public BattleContext(
            string encounterId,
            string rewardTableId,
            int gold,
            int experience,
            string[] enemies,
            bool repeatable)
        {
            EncounterId = encounterId;
            RewardTableId = rewardTableId;
            GoldReward = gold;
            ExperienceReward = experience;
            EnemyDefinitionIds = enemies ?? System.Array.Empty<string>();
            Repeatable = repeatable;
        }

        public string EncounterId { get; }
        public string RewardTableId { get; }
        public int GoldReward { get; }
        public int ExperienceReward { get; }
        public string[] EnemyDefinitionIds { get; }
        public bool Repeatable { get; }
    }
}
