using System;
using BorderValley.Core.Combat;

namespace BorderValley.Core.BattleFlow
{
    public sealed class BattleSkillModifierSnapshot
    {
        public BattleSkillModifierSnapshot(string skillId, SkillModifierKind kind, int value)
        {
            SkillId = string.IsNullOrWhiteSpace(skillId)
                ? throw new ArgumentException(nameof(skillId))
                : skillId;
            Kind = kind;
            Value = value;
        }

        public string SkillId { get; }
        public SkillModifierKind Kind { get; }
        public int Value { get; }
    }
}
