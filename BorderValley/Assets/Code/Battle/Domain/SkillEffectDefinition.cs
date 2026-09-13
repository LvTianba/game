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
            int duration)
        {
            if (float.IsNaN(powerMultiplier) || float.IsInfinity(powerMultiplier))
                throw new ArgumentOutOfRangeException(nameof(powerMultiplier));

            Kind = kind;
            PowerMultiplier = Math.Max(0f, powerMultiplier);
            StatusType = statusType;
            Magnitude = Math.Max(0, magnitude);
            Duration = Math.Max(0, duration);
        }

        public SkillEffectKind Kind { get; }
        public float PowerMultiplier { get; }
        public StatusType StatusType { get; }
        public int Magnitude { get; }
        public int Duration { get; }
    }
}
