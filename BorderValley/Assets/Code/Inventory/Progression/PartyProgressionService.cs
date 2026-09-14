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
            if (!(state["members"] is JArray savedMembers))
                throw new InvalidOperationException("Save contains malformed party members.");

            var safePointToken = state["safePointId"];
            if (safePointToken != null &&
                safePointToken.Type != JTokenType.Null &&
                safePointToken.Type != JTokenType.String)
                throw new InvalidOperationException("Save contains a malformed safe point.");
            var safePoint = safePointToken?.Value<string>();
            if (safePointToken != null &&
                safePointToken.Type != JTokenType.Null &&
                string.IsNullOrWhiteSpace(safePoint))
                throw new InvalidOperationException("Save contains a malformed safe point.");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var parsed = new List<(PartyMemberState Member, int Level, int Experience, int SkillPoints, int CurrentHealth, int CurrentMana, Dictionary<string, int> SkillRanks)>();
            foreach (var token in savedMembers)
            {
                if (!(token is JObject saved))
                    throw new InvalidOperationException("Save contains a malformed party member.");

                var memberIdToken = saved["memberId"];
                var characterIdToken = saved["characterId"];
                var levelToken = saved["level"];
                if (memberIdToken == null ||
                    memberIdToken.Type != JTokenType.String ||
                    characterIdToken == null ||
                    characterIdToken.Type != JTokenType.String ||
                    string.IsNullOrWhiteSpace(memberIdToken.Value<string>()) ||
                    string.IsNullOrWhiteSpace(characterIdToken.Value<string>()) ||
                    levelToken == null ||
                    levelToken.Type != JTokenType.Integer)
                    throw new InvalidOperationException("Save contains a malformed party member identity.");

                var memberId = memberIdToken.Value<string>();
                var characterId = characterIdToken.Value<string>();
                var level = levelToken.Value<int>();
                if (level < 1 || level > MaxLevel || !characters.TryGetValue(characterId, out var character))
                    throw new InvalidOperationException("Save contains an invalid party member level or class.");
                var member = members.FirstOrDefault(value => value.MemberId == memberId);
                if (member == null || !string.Equals(member.CharacterId, characterId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Save contains an unknown or mismatched party member.");
                if (!seen.Add(memberId))
                    throw new InvalidOperationException("Save contains duplicate party members.");

                var experience = RequireNonNegativeInt(saved, "experience");
                var skillPoints = RequireNonNegativeInt(saved, "skillPoints");
                var currentHealth = RequireNonNegativeInt(saved, "currentHealth");
                var currentMana = RequireNonNegativeInt(saved, "currentMana");
                if (currentHealth < 1)
                    throw new InvalidOperationException("Save contains invalid party member health.");

                var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
                if (saved["skillRanks"] != null && saved["skillRanks"].Type != JTokenType.Null)
                {
                    if (!(saved["skillRanks"] is JObject skillRanks))
                        throw new InvalidOperationException("Save contains malformed skill ranks.");
                    foreach (var pair in skillRanks)
                    {
                        if (pair.Value.Type != JTokenType.Integer)
                            throw new InvalidOperationException("Save contains a malformed skill rank.");
                        var rank = pair.Value.Value<int>();
                        if (rank < 0 || rank > MaxSkillRank ||
                            !IsSkillUnlocked(character, level, pair.Key))
                            throw new InvalidOperationException("Save contains an invalid skill rank.");
                        ranks[pair.Key] = rank;
                    }
                }

                parsed.Add((member, level, experience, skillPoints, currentHealth, currentMana, ranks));
            }

            if (parsed.Count != members.Count || members.Any(member => !seen.Contains(member.MemberId)))
                throw new InvalidOperationException("Save does not contain every party member exactly once.");

            var savedPendingWipe = state["hasPendingWipeReturn"];
            if (savedPendingWipe != null &&
                savedPendingWipe.Type != JTokenType.Null &&
                savedPendingWipe.Type != JTokenType.Boolean)
                throw new InvalidOperationException("Save contains a malformed wipe state.");

            if (!string.IsNullOrWhiteSpace(safePoint))
                SafePointId = safePoint;
            foreach (var value in parsed)
            {
                value.Member.Level = value.Level;
                value.Member.Experience = value.Experience;
                value.Member.SkillPoints = value.SkillPoints;
                value.Member.CurrentHealth = value.CurrentHealth;
                value.Member.CurrentMana = value.CurrentMana;
                value.Member.SkillRanks.Clear();
                foreach (var rank in value.SkillRanks)
                    value.Member.SkillRanks[rank.Key] = rank.Value;
            }

            HasPendingWipeReturn = savedPendingWipe == null || savedPendingWipe.Type == JTokenType.Null
                ? members.Count > 0 && members.All(member => member.CurrentHealth <= 1)
                : savedPendingWipe.Value<bool>();
            Changed?.Invoke();
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

        private static int RequireNonNegativeInt(JObject state, string key)
        {
            var token = state[key];
            if (token == null || token.Type != JTokenType.Integer || token.Value<int>() < 0)
                throw new InvalidOperationException($"Save contains malformed {key}.");
            return token.Value<int>();
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
