using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Core.BattleFlow
{
    public sealed class BattleUnitResult
    {
        public BattleUnitResult(string unitId, int health, int mana)
        {
            UnitId = string.IsNullOrWhiteSpace(unitId)
                ? throw new ArgumentException(nameof(unitId))
                : unitId;
            Health = Math.Max(0, health);
            Mana = Math.Max(0, mana);
        }

        public string UnitId { get; }
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
        {
            Outcome = outcome;
            Rounds = rounds < 0 ? 0 : rounds;
            UnitStates = Copy(unitStates);
        }

        public BattleFlowOutcome Outcome { get; }
        public int Rounds { get; }
        public IReadOnlyList<BattleUnitResult> UnitStates { get; }

        private static IReadOnlyList<BattleUnitResult> Copy(IEnumerable<BattleUnitResult> values)
        {
            var copy = (values ?? Array.Empty<BattleUnitResult>()).ToArray();
            if (copy.Any(value => value == null))
                throw new ArgumentException("Unit states cannot contain null.", nameof(values));
            return Array.AsReadOnly(copy);
        }
    }
}
