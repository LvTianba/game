using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Random;

namespace BorderValley.Battle.Domain
{
    public sealed class BattleScenario
    {
        private readonly Dictionary<string, SkillDefinition> playerSkills;
        private readonly Dictionary<string, SkillDefinition> enemySkills;
        private readonly Dictionary<string, string[]> unitSkills;
        private readonly Dictionary<string, SkillDefinition> allSkills;
        private readonly Dictionary<string, IReadOnlyDictionary<string, SkillDefinition>> skillsByUnit;

        public BattleScenario(
            BattleState state,
            IReadOnlyDictionary<string, SkillDefinition> playerSkills,
            IReadOnlyDictionary<string, SkillDefinition> enemySkills,
            IReadOnlyDictionary<string, string[]> unitSkills)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            this.playerSkills = CopySkills(playerSkills, nameof(playerSkills));
            this.enemySkills = CopySkills(enemySkills, nameof(enemySkills));
            this.unitSkills = CopyUnitSkills(unitSkills);
            allSkills = BuildAllSkills(this.playerSkills, this.enemySkills);
            ValidateUnitSkills();
            skillsByUnit = BuildSkillsByUnit();
        }

        public BattleState State { get; }
        public IReadOnlyDictionary<string, SkillDefinition> PlayerSkills => playerSkills;
        public IReadOnlyDictionary<string, SkillDefinition> EnemySkills => enemySkills;
        public IReadOnlyDictionary<string, string[]> UnitSkills => unitSkills;
        public IReadOnlyDictionary<string, SkillDefinition> AllSkills => allSkills;

        public BattleEngine CreateEngine(IRandomSource random)
        {
            return new BattleEngine(State, random, allSkills, unitSkills);
        }

        public IReadOnlyDictionary<string, SkillDefinition> GetSkillsForUnit(string unitId)
        {
            if (!State.TryGetUnit(unitId, out _))
                throw new ArgumentException($"Unknown unit ID: {unitId}", nameof(unitId));

            return skillsByUnit[unitId];
        }

        private Dictionary<string, IReadOnlyDictionary<string, SkillDefinition>> BuildSkillsByUnit()
        {
            var result = new Dictionary<string, IReadOnlyDictionary<string, SkillDefinition>>(
                StringComparer.Ordinal);
            foreach (var pair in unitSkills)
            {
                result.Add(
                    pair.Key,
                    pair.Value.ToDictionary(
                        skillId => skillId,
                        skillId => allSkills[skillId],
                        StringComparer.Ordinal));
            }

            return result;
        }

        private void ValidateUnitSkills()
        {
            foreach (var unit in State.Units)
            {
                if (!unitSkills.TryGetValue(unit.Id, out var ownedSkillIds))
                    throw new ArgumentException($"Missing skill ownership for unit: {unit.Id}");

                var catalog = unit.Team == Team.Player ? playerSkills : enemySkills;
                foreach (var skillId in ownedSkillIds)
                {
                    if (!catalog.ContainsKey(skillId))
                    {
                        throw new ArgumentException(
                            $"Skill '{skillId}' is not present in the {unit.Team} skill catalog.");
                    }
                }
            }

            foreach (var unitId in unitSkills.Keys)
            {
                if (!State.TryGetUnit(unitId, out _))
                    throw new ArgumentException($"Unknown unit ID in skill ownership: {unitId}");
            }
        }

        private static Dictionary<string, SkillDefinition> CopySkills(
            IReadOnlyDictionary<string, SkillDefinition> source,
            string parameterName)
        {
            if (source == null) throw new ArgumentNullException(parameterName);

            var copy = new Dictionary<string, SkillDefinition>(StringComparer.Ordinal);
            foreach (var pair in source)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    throw new ArgumentException("Skill IDs cannot be empty.", parameterName);
                if (pair.Value == null)
                    throw new ArgumentException("Skill definitions cannot contain null.", parameterName);
                if (!string.Equals(pair.Key, pair.Value.Id, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"Skill dictionary key '{pair.Key}' must match SkillDefinition.Id '{pair.Value.Id}'.",
                        parameterName);
                }

                copy.Add(pair.Key, pair.Value);
            }

            return copy;
        }

        private static Dictionary<string, string[]> CopyUnitSkills(
            IReadOnlyDictionary<string, string[]> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var copy = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var pair in source)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    throw new ArgumentException("Unit IDs cannot be empty.", nameof(source));
                if (pair.Value == null)
                    throw new ArgumentException("Unit skill arrays cannot be null.", nameof(source));

                var owned = new HashSet<string>(StringComparer.Ordinal);
                foreach (var skillId in pair.Value)
                {
                    if (string.IsNullOrWhiteSpace(skillId))
                        throw new ArgumentException("Owned skill IDs cannot be empty.", nameof(source));
                    if (!owned.Add(skillId))
                        throw new ArgumentException($"Duplicate owned skill ID: {skillId}", nameof(source));
                }

                copy.Add(pair.Key, (string[])pair.Value.Clone());
            }

            return copy;
        }

        private static Dictionary<string, SkillDefinition> BuildAllSkills(
            IReadOnlyDictionary<string, SkillDefinition> playerSkills,
            IReadOnlyDictionary<string, SkillDefinition> enemySkills)
        {
            var result = new Dictionary<string, SkillDefinition>(StringComparer.Ordinal);
            foreach (var pair in playerSkills)
                AddSharedDefinition(result, pair);
            foreach (var pair in enemySkills)
                AddSharedDefinition(result, pair);

            return result;
        }

        private static void AddSharedDefinition(
            IDictionary<string, SkillDefinition> result,
            KeyValuePair<string, SkillDefinition> pair)
        {
            if (!result.TryGetValue(pair.Key, out var existing))
            {
                result.Add(pair.Key, pair.Value);
                return;
            }

            if (!ReferenceEquals(existing, pair.Value))
            {
                throw new ArgumentException(
                    $"Skill definition ID '{pair.Key}' is defined by more than one instance.");
            }
        }
    }
}
