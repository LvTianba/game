using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Battle.Domain
{
    public static class TurnOrder
    {
        public static IReadOnlyList<BattleUnit> Build(IEnumerable<BattleUnit> units)
        {
            if (units == null) throw new ArgumentNullException(nameof(units));

            return units
                .Select((unit, index) =>
                {
                    if (unit == null) throw new ArgumentException("Unit collection cannot contain null.", nameof(units));
                    return new { unit, index };
                })
                .OrderByDescending(item => item.unit.Stats.Speed)
                .ThenBy(item => item.index)
                .Select(item => item.unit)
                .ToArray();
        }
    }
}
