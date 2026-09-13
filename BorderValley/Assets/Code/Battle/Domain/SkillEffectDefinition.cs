using System;

namespace BorderValley.Battle.Domain
{
    public sealed class SkillEffectDefinition
    {
        public SkillEffectDefinition(
            SkillEffectKind kind,
            float powerMultiplier,
            StatusType statusType,
            int magnitude,
            int duration,
            DamageType damageType = DamageType.Physical)
        {
            if (float.IsNaN(powerMultiplier) || float.IsInfinity(powerMultiplier))
                throw new ArgumentOutOfRangeException(nameof(powerMultiplier));

            Kind = kind;
            PowerMultiplier = Math.Max(0f, powerMultiplier);
            StatusType = statusType;
            Magnitude = Math.Max(0, magnitude);
            Duration = Math.Max(0, duration);
            DamageType = damageType;
        }

        public SkillEffectKind Kind { get; }
        public float PowerMultiplier { get; }
        public StatusType StatusType { get; }
        public int Magnitude { get; }
        public int Duration { get; }
        public DamageType DamageType { get; }
    }
}
