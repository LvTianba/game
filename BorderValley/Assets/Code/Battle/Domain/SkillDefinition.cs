using System;
using System.Collections.Generic;

namespace BorderValley.Battle.Domain
{
    public sealed class SkillDefinition
    {
        public SkillDefinition(
            string id,
            string localizationKey,
            SkillTargeting targeting,
            int range,
            int radius,
            int mana,
            int cooldown,
            params SkillEffectDefinition[] effects)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException(nameof(id));
            if (string.IsNullOrWhiteSpace(localizationKey)) throw new ArgumentException(nameof(localizationKey));
            if (effects == null) throw new ArgumentNullException(nameof(effects));
            if (Array.Exists(effects, effect => effect == null))
                throw new ArgumentException("Skill effects cannot contain null.", nameof(effects));

            Id = id;
            LocalizationKey = localizationKey;
            Targeting = targeting;
            Range = Math.Max(0, range);
            Radius = Math.Max(0, radius);
            Mana = Math.Max(0, mana);
            Cooldown = Math.Max(0, cooldown);
            Effects = Array.AsReadOnly((SkillEffectDefinition[])effects.Clone());
        }

        public string Id { get; }
        public string LocalizationKey { get; }
        public SkillTargeting Targeting { get; }
        public int Range { get; }
        public int Radius { get; }
        public int Mana { get; }
        public int Cooldown { get; }
        public IReadOnlyList<SkillEffectDefinition> Effects { get; }
    }
}
