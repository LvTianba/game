namespace BorderValley.Battle.Domain
{
    public readonly struct DamageResult
    {
        public DamageResult(int damage, bool critical, bool highGroundBonus, int shieldAbsorbed)
        {
            Damage = damage;
            Critical = critical;
            HighGroundBonus = highGroundBonus;
            ShieldAbsorbed = shieldAbsorbed;
        }

        public int Damage { get; }
        public bool Critical { get; }
        public bool HighGroundBonus { get; }
        public int ShieldAbsorbed { get; }
    }
}
