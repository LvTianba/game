using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Battle.Domain
{
    public sealed class SkillExecutionResult
    {
        public SkillExecutionResult(
            bool success,
            string failureReason,
            int damageDealt,
            int healingDone,
            IEnumerable<BattleUnit> affectedUnits)
        {
            Success = success;
            FailureReason = failureReason ?? string.Empty;
            DamageDealt = Math.Max(0, damageDealt);
            HealingDone = Math.Max(0, healingDone);

            var units = affectedUnits == null ? new List<BattleUnit>() : affectedUnits.ToList();
            AffectedUnits = units.AsReadOnly();
            AffectedUnitIds = units.Select(unit => unit.Id).ToList().AsReadOnly();
        }

        public bool Success { get; }
        public string FailureReason { get; }
        public int DamageDealt { get; }
        public int HealingDone { get; }
        public IReadOnlyList<BattleUnit> AffectedUnits { get; }
        public IReadOnlyList<string> AffectedUnitIds { get; }
        public int Damage => DamageDealt;
        public int Healing => HealingDone;

        public static SkillExecutionResult Failed(string failureReason) =>
            new(false, failureReason, 0, 0, Array.Empty<BattleUnit>());

        public static SkillExecutionResult Succeeded(
            int damageDealt,
            int healingDone,
            IEnumerable<BattleUnit> affectedUnits) =>
            new(true, string.Empty, damageDealt, healingDone, affectedUnits);
    }
}
