namespace BorderValley.Battle.Domain
{
    public readonly struct DamageRequest
    {
        public DamageRequest(BattleUnit attacker, BattleUnit defender, float powerMultiplier,
            DamageType damageType, bool canCrit)
        {
            Attacker = attacker;
            Defender = defender;
            PowerMultiplier = powerMultiplier < 0f ? 0f : powerMultiplier;
            DamageType = damageType;
            CanCrit = canCrit;
        }

        public BattleUnit Attacker { get; }
        public BattleUnit Defender { get; }
        public float PowerMultiplier { get; }
        public DamageType DamageType { get; }
        public bool CanCrit { get; }
    }
}
