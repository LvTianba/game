using System;
using System.Collections.Generic;

namespace BorderValley.Inventory
{
    public sealed class PartyMemberState
    {
        public PartyMemberState(
            string memberId,
            string characterId,
            int level,
            int experience,
            int skillPoints,
            int currentHealth,
            int currentMana)
        {
            MemberId = Require(memberId, nameof(memberId));
            CharacterId = Require(characterId, nameof(characterId));
            Level = Math.Clamp(level, 1, 10);
            Experience = Math.Max(0, experience);
            SkillPoints = Math.Max(0, skillPoints);
            CurrentHealth = Math.Max(1, currentHealth);
            CurrentMana = Math.Max(0, currentMana);
        }

        public string MemberId { get; }
        public string CharacterId { get; }
        public int Level { get; internal set; }
        public int Experience { get; internal set; }
        public int SkillPoints { get; internal set; }
        public int CurrentHealth { get; internal set; }
        public int CurrentMana { get; internal set; }
        public Dictionary<string, int> SkillRanks { get; } = new(StringComparer.Ordinal);

        internal PartyMemberState Clone()
        {
            var copy = new PartyMemberState(
                MemberId,
                CharacterId,
                Level,
                Experience,
                SkillPoints,
                CurrentHealth,
                CurrentMana);
            foreach (var pair in SkillRanks)
                copy.SkillRanks[pair.Key] = pair.Value;
            return copy;
        }

        private static string Require(string value, string name) =>
            string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(name) : value;
    }
}
