using System;

namespace BorderValley.Battle.Domain
{
    public readonly struct UnitStats
    {
        public UnitStats(
            int maxHealth,
            int maxMana,
            int power,
            int armor,
            int speed,
            float critChance,
            int resistance)
        {
            MaxHealth = Math.Max(1, maxHealth);
            MaxMana = Math.Max(0, maxMana);
            Power = Math.Max(0, power);
            Armor = Math.Max(0, armor);
            Speed = Math.Max(0, speed);
            CritChance = Math.Clamp(critChance, 0f, 1f);
            Resistance = Math.Max(0, resistance);
        }

        public int MaxHealth { get; }
        public int MaxMana { get; }
        public int Power { get; }
        public int Armor { get; }
        public int Speed { get; }
        public float CritChance { get; }
        public int Resistance { get; }
    }
}

