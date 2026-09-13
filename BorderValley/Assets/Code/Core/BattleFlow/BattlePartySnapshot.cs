using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Core.BattleFlow
{
    public sealed class BattlePartySnapshot
    {
        public BattlePartySnapshot(IEnumerable<BattleCombatantSnapshot> members)
        {
            if (members == null) throw new ArgumentNullException(nameof(members));
            var copy = members.ToArray();
            if (copy.Length == 0) throw new ArgumentException("A party snapshot must contain at least one member.", nameof(members));
            if (copy.Any(member => member == null)) throw new ArgumentException("Party members cannot contain null.", nameof(members));
            Members = Array.AsReadOnly(copy);
        }

        public IReadOnlyList<BattleCombatantSnapshot> Members { get; }
    }
}
