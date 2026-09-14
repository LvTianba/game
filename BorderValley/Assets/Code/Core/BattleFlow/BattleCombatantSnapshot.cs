using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Core.BattleFlow
{
    public sealed class BattleCombatantSnapshot
    {
        public BattleCombatantSnapshot(
            string unitId,
            string definitionId,
            string classId,
            int maxHealth,
            int maxMana,
            int power,
            int armor,
            int speed,
            int critChanceBps,
            int resistance,
            int currentHealth,
            int currentMana,
            IEnumerable<string> skillIds,
            IEnumerable<BattleSkillModifierSnapshot> skillModifiers,
            IEnumerable<BattlePassiveSnapshot> passives)
        {
            UnitId = Require(unitId, nameof(unitId));
            DefinitionId = Require(definitionId, nameof(definitionId));
            ClassId = Require(classId, nameof(classId));
            MaxHealth = Math.Max(1, maxHealth);
            MaxMana = Math.Max(0, maxMana);
            Power = Math.Max(0, power);
            Armor = Math.Max(0, armor);
            Speed = Math.Max(0, speed);
            CritChanceBps = Math.Clamp(critChanceBps, 0, 10000);
            Resistance = Math.Max(0, resistance);
            CurrentHealth = Math.Clamp(currentHealth, 1, MaxHealth);
            CurrentMana = Math.Clamp(currentMana, 0, MaxMana);
            SkillIds = Copy(skillIds);
            SkillModifiers = Copy(skillModifiers);
            Passives = Copy(passives);
        }

        public string UnitId { get; }
        public string DefinitionId { get; }
        public string ClassId { get; }
        public int MaxHealth { get; }
        public int MaxMana { get; }
        public int Power { get; }
        public int Armor { get; }
        public int Speed { get; }
        public int CritChanceBps { get; }
        public int Resistance { get; }
        public int CurrentHealth { get; }
        public int CurrentMana { get; }
        public IReadOnlyList<string> SkillIds { get; }
        public IReadOnlyList<BattleSkillModifierSnapshot> SkillModifiers { get; }
        public IReadOnlyList<BattlePassiveSnapshot> Passives { get; }

        private static string Require(string value, string name) =>
            string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(name) : value;

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values)
        {
            var copy = (values ?? Array.Empty<T>()).ToArray();
            if (copy.Any(value => value == null)) throw new ArgumentException("Snapshot lists cannot contain null.");
            return Array.AsReadOnly(copy);
        }
    }
}
