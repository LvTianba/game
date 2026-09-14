using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Persistence;
using BorderValley.Data.Items;
using Newtonsoft.Json.Linq;

namespace BorderValley.Inventory
{
    public sealed class PartyProgressionService : ISaveParticipant
    {
        public const string DefaultSafePointId = "world.village";
        public const int MaxLevel = 10;
        private const int MaxSkillRank = 3;
        private readonly IReadOnlyDictionary<string, CharacterDefinition> characters;
        private readonly List<PartyMemberState> initialMembers;
        private List<PartyMemberState> members;

        public PartyProgressionService(
            IReadOnlyDictionary<string, CharacterDefinition> characters,
            IEnumerable<PartyMemberState> members)
        {
            this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
            if (members == null) throw new ArgumentNullException(nameof(members));

            initialMembers = new List<PartyMemberState>();
            var memberIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in members)
            {
                if (member == null) throw new ArgumentException("Members cannot contain null.", nameof(members));
                if (!this.characters.ContainsKey(member.CharacterId)) continue;
                if (!memberIds.Add(member.MemberId))
                    throw new ArgumentException($"Duplicate member id: {member.MemberId}", nameof(members));
                initialMembers.Add(member.Clone());
            }

            this.members = Clone(initialMembers);
        }

        public event Action Changed;
        public string Key => "party";
        public string SafePointId { get; private set; } = DefaultSafePointId;
        public bool HasPendingWipeReturn { get; private set; }
        public IReadOnlyList<PartyMemberState> Members => members;
        public int TotalExperience => members.Sum(member => member.Experience);
        public int HighestLevel => members.Count == 0 ? 1 : members.Max(member => member.Level);

        public void AwardExperience(int amount)
        {
            if (amount <= 0) return;
            var changed = false;
            foreach (var member in members)
            {
                if (member.Level >= MaxLevel) continue;
                var experience = member.Experience + amount;
                while (member.Level < MaxLevel)
                {
                    var requirement = RequiredExperience(member.Level);
                    if (experience < requirement) break;
                    experience -= requirement;
                    member.Level++;
                    if (member.Level % 2 == 0) member.SkillPoints++;
                    changed = true;
                }
                member.Experience = experience;
                changed = true;
            }
            if (changed) Changed?.Invoke();
        }

        public bool TrySpendSkillPoint(string memberId, string skillId, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(memberId) || string.IsNullOrWhiteSpace(skillId))
            {
                error = "party.invalid_skill";
                return false;
            }
            var member = members.FirstOrDefault(value => value.MemberId == memberId);
            if (member == null)
            {
                error = "party.member_missing";
                return false;
            }
            if (!characters.TryGetValue(member.CharacterId, out var character))
            {
                error = "party.character_missing";
                return false;
            }
            if (!IsSkillUnlocked(character, member.Level, skillId))
            {
                error = "party.skill_locked";
                return false;
            }
            if (member.SkillPoints <= 0)
            {
                error = "party.no_skill_point";
                return false;
            }
            member.SkillRanks.TryGetValue(skillId, out var rank);
            if (rank >= MaxSkillRank)
            {
                error = "party.skill_maxed";
                return false;
            }
            member.SkillRanks[skillId] = rank + 1;
            member.SkillPoints--;
            Changed?.Invoke();
            return true;
        }

        public void ApplyBattleUnitStates(IEnumerable<BattleUnitResult> unitStates)
        {
            if (unitStates == null) throw new ArgumentNullException(nameof(unitStates));
            var changed = false;
            foreach (var state in unitStates)
            {
                if (state == null) continue;
                var member = members.FirstOrDefault(value => value.MemberId == state.UnitId);
                if (member == null) continue;
                var health = Math.Max(1, state.Health);
                var mana = Math.Max(0, state.Mana);
                if (member.CurrentHealth == health && member.CurrentMana == mana) continue;
                member.CurrentHealth = health;
                member.CurrentMana = mana;
                changed = true;
            }

            var pendingWipe = members.Count > 0 && members.All(member => member.CurrentHealth <= 1);
            if (HasPendingWipeReturn != pendingWipe)
            {
                HasPendingWipeReturn = pendingWipe;
                changed = true;
            }
            if (changed) Changed?.Invoke();
        }

        public void RecoverOutOfCombat(int amount)
        {
            if (amount <= 0) return;
            var changed = false;
            foreach (var member in members)
            {
                if (!characters.TryGetValue(member.CharacterId, out var character))
                    continue;
                var maxHealth = Math.Max(1, character.GetBaseStat(BorderValley.Core.Combat.CombatStat.MaxHealth, member.Level));
                var maxMana = Math.Max(0, character.GetBaseStat(BorderValley.Core.Combat.CombatStat.MaxMana, member.Level));
                var health = Math.Min(maxHealth, member.CurrentHealth + amount);
                var mana = Math.Min(maxMana, member.CurrentMana + amount);
                if (member.CurrentHealth == health && member.CurrentMana == mana)
                    continue;
                member.CurrentHealth = health;
                member.CurrentMana = mana;
                changed = true;
            }
            if (changed) Changed?.Invoke();
        }

        public void ReturnToSafePoint()
        {
            SafePointId = DefaultSafePointId;
            HasPendingWipeReturn = false;
            Changed?.Invoke();
        }

        public JObject Capture() => new()
        {
            ["safePointId"] = SafePointId,
            ["hasPendingWipeReturn"] = HasPendingWipeReturn,
            ["members"] = new JArray(members.Select(member => new JObject
            {
                ["memberId"] = member.MemberId,
                ["characterId"] = member.CharacterId,
                ["level"] = member.Level,
                ["experience"] = member.Experience,
                ["skillPoints"] = member.SkillPoints,
                ["currentHealth"] = member.CurrentHealth,
                ["currentMana"] = member.CurrentMana,
                ["skillRanks"] = new JObject(member.SkillRanks.Select(pair => new JProperty(pair.Key, pair.Value)))
            }))
        };

        public void Restore(JObject state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var changed = false;
            var safePoint = state.Value<string>("safePointId");
            if (!string.IsNullOrWhiteSpace(safePoint) && SafePointId != safePoint)
            {
                SafePointId = safePoint;
                changed = true;
            }

            foreach (var token in state["members"] as JArray ?? new JArray())
            {
                if (!(token is JObject saved)) continue;
                var memberId = saved.Value<string>("memberId");
                var characterId = saved.Value<string>("characterId");
                var level = saved.Value<int>("level");
                if (string.IsNullOrWhiteSpace(memberId) ||
                    string.IsNullOrWhiteSpace(characterId) ||
                    level < 1 || level > MaxLevel ||
                    !characters.ContainsKey(characterId))
                    continue;

                var member = members.FirstOrDefault(value => value.MemberId == memberId);
                if (member == null || !string.Equals(member.CharacterId, characterId, StringComparison.Ordinal))
                    continue;

                member.Level = level;
                member.Experience = Math.Max(0, saved.Value<int>("experience"));
                member.SkillPoints = Math.Max(0, saved.Value<int>("skillPoints"));
                member.CurrentHealth = Math.Max(1, saved.Value<int>("currentHealth"));
                member.CurrentMana = Math.Max(0, saved.Value<int>("currentMana"));
                member.SkillRanks.Clear();
                var character = characters[characterId];
                if (saved["skillRanks"] is JObject ranks)
                {
                    foreach (var pair in ranks)
                    {
                        if (!IsSkillUnlocked(character, level, pair.Key)) continue;
                        member.SkillRanks[pair.Key] = Math.Clamp(pair.Value.Value<int>(), 0, MaxSkillRank);
                    }
                }
                changed = true;
            }

            var savedPendingWipe = state["hasPendingWipeReturn"];
            var pendingWipe = savedPendingWipe == null || savedPendingWipe.Type == JTokenType.Null
                ? members.Count > 0 && members.All(member => member.CurrentHealth <= 1)
                : savedPendingWipe.Value<bool>();
            if (HasPendingWipeReturn != pendingWipe)
            {
                HasPendingWipeReturn = pendingWipe;
                changed = true;
            }
            if (changed) Changed?.Invoke();
        }

        public void RestoreContext(string sceneName) { }

        public void Reset()
        {
            members = Clone(initialMembers);
            SafePointId = DefaultSafePointId;
            HasPendingWipeReturn = false;
            Changed?.Invoke();
        }

        internal CharacterDefinition GetCharacter(string characterId) =>
            characters.TryGetValue(characterId, out var character) ? character : null;

        internal IReadOnlyList<string> GetUnlockedSkillIds(PartyMemberState member)
        {
            if (member == null || !characters.TryGetValue(member.CharacterId, out var character))
                return Array.Empty<string>();
            return UnlockedSkillIds(character, member.Level);
        }

        private static List<PartyMemberState> Clone(IEnumerable<PartyMemberState> source) =>
            source.Select(member => member.Clone()).ToList();

        private static int RequiredExperience(int level) => 100 * level;

        private static bool IsSkillUnlocked(CharacterDefinition character, int level, string skillId) =>
            !string.IsNullOrWhiteSpace(skillId) &&
            ((character.StartingSkillIds ?? Array.Empty<string>())
                 .Any(value => string.Equals(value, skillId, StringComparison.Ordinal)) ||
             (character.SkillUnlocks ?? Array.Empty<SkillUnlock>())
                 .Any(value => value.Level <= level &&
                               string.Equals(value.SkillId, skillId, StringComparison.Ordinal)));

        private static IReadOnlyList<string> UnlockedSkillIds(CharacterDefinition character, int level)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var skillId in character.StartingSkillIds ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(skillId) && seen.Add(skillId)) result.Add(skillId);
            foreach (var unlock in character.SkillUnlocks ?? Array.Empty<SkillUnlock>())
                if (unlock.Level <= level && !string.IsNullOrWhiteSpace(unlock.SkillId) && seen.Add(unlock.SkillId))
                    result.Add(unlock.SkillId);
            return result;
        }
    }
}
