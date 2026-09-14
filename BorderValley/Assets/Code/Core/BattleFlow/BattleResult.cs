using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Core.BattleFlow
{
    public sealed class BattleUnitResult
    {
        public BattleUnitResult(string unitId, int health, int mana)
            : this(unitId, unitId, health, mana)
        {
        }

        public BattleUnitResult(string unitId, string definitionId, int health, int mana)
        {
            UnitId = string.IsNullOrWhiteSpace(unitId)
                ? throw new ArgumentException(nameof(unitId))
                : unitId;
            DefinitionId = string.IsNullOrWhiteSpace(definitionId)
                ? throw new ArgumentException(nameof(definitionId))
                : definitionId;
            Health = Math.Max(0, health);
            Mana = Math.Max(0, mana);
        }

        public string UnitId { get; }
        public string DefinitionId { get; }
        public int Health { get; }
        public int Mana { get; }
    }

    public sealed class BattleResult
    {
        public BattleResult(BattleFlowOutcome outcome, int rounds)
            : this(outcome, rounds, Array.Empty<BattleUnitResult>())
        {
        }

        public BattleResult(
            BattleFlowOutcome outcome,
            int rounds,
            IEnumerable<BattleUnitResult> unitStates)
            : this(outcome, rounds, unitStates, null)
        {
        }

        public BattleResult(
            BattleFlowOutcome outcome,
            int rounds,
            IEnumerable<BattleUnitResult> unitStates,
            BattleContext context)
            : this(outcome, rounds, unitStates, context, Array.Empty<string>())
        {
        }

        public BattleResult(
            BattleFlowOutcome outcome,
            int rounds,
            IEnumerable<BattleUnitResult> unitStates,
            BattleContext context,
            IEnumerable<string> defeatedEnemyIds)
        {
            Outcome = outcome;
            Rounds = rounds < 0 ? 0 : rounds;
            UnitStates = Copy(unitStates);
            Context = context;
            DefeatedEnemyIds = CopyDefeatedEnemyIds(defeatedEnemyIds);
        }

        public BattleFlowOutcome Outcome { get; }
        public int Rounds { get; }
        public IReadOnlyList<BattleUnitResult> UnitStates { get; }
        public BattleContext Context { get; }
        public IReadOnlyList<string> DefeatedEnemyIds { get; }

        private static IReadOnlyList<string> CopyDefeatedEnemyIds(IEnumerable<string> values)
        {
            var copy = (values ?? Array.Empty<string>()).ToArray();
            if (copy.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Defeated enemy IDs cannot contain blank values.", nameof(values));
            return Array.AsReadOnly(copy);
        }

        private static IReadOnlyList<BattleUnitResult> Copy(IEnumerable<BattleUnitResult> values)
        {
            var copy = (values ?? Array.Empty<BattleUnitResult>()).ToArray();
            if (copy.Any(value => value == null))
                throw new ArgumentException("Unit states cannot contain null.", nameof(values));
            return Array.AsReadOnly(copy);
        }
    }
}
