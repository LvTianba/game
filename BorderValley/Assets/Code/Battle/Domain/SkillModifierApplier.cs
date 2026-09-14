using BorderValley.Core.Combat;
using System;
using System.Linq;

namespace BorderValley.Battle.Domain
{
    public static class SkillModifierApplier
    {
        public static SkillDefinition Apply(SkillDefinition skill, SkillModifierKind kind, int value)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));

            var range = skill.Range;
            var radius = skill.Radius;
            var mana = skill.Mana;
            var cooldown = skill.Cooldown;
            SkillEffectDefinition[] effects = null;

            switch (kind)
            {
                case SkillModifierKind.Range:
                    range = Math.Max(0, range + value);
                    break;
                case SkillModifierKind.Radius:
                    radius = Math.Max(0, radius + value);
                    break;
                case SkillModifierKind.ManaCost:
                    mana = Math.Max(0, mana + value);
                    break;
                case SkillModifierKind.Cooldown:
                    cooldown = Math.Max(0, cooldown + value);
                    break;
                case SkillModifierKind.PowerMultiplierBps:
                    effects = skill.Effects
                        .Select(effect => effect.Kind == SkillEffectKind.Damage
                            ? new SkillEffectDefinition(
                                effect.Kind,
                                effect.PowerMultiplier + value / 10000f,
                                effect.StatusType,
                                effect.Magnitude,
                                effect.Duration,
                                effect.DamageType,
                                effect.ArmorPenetration,
                                effect.CanCrit)
                            : effect)
                        .ToArray();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }

            return new SkillDefinition(
                skill.Id,
                skill.LocalizationKey,
                skill.Targeting,
                range,
                radius,
                mana,
                cooldown,
                effects ?? skill.Effects.ToArray());
        }
    }
}
