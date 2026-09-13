using BorderValley.Core.Combat;

namespace BorderValley.Core.BattleFlow
{
    public sealed class BattlePassiveSnapshot
    {
        public BattlePassiveSnapshot(PassiveEffectKind kind, int magnitude, int duration)
        {
            Kind = kind;
            Magnitude = magnitude;
            Duration = duration;
        }

        public PassiveEffectKind Kind { get; }
        public int Magnitude { get; }
        public int Duration { get; }
    }
}
