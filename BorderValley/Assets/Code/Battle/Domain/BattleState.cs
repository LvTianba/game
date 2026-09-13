using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Battle.Domain
{
    public sealed class BattleState
    {
        private readonly List<BattleUnit> units = new();
        private readonly Dictionary<string, BattleUnit> byId = new(StringComparer.Ordinal);

        public BattleState(BattleMap map)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
        }

        public BattleMap Map { get; }
        public int Round { get; internal set; } = 1;
        public IReadOnlyList<BattleUnit> Units => units;
        public IEnumerable<BattleUnit> LivingUnits => units.Where(unit => unit.IsAlive);
        public ISet<GridPosition> OccupiedPositions =>
            LivingUnits.Select(unit => unit.Position).ToHashSet();

        public void AddUnit(BattleUnit unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (!Map.InBounds(unit.Position)) throw new ArgumentOutOfRangeException(nameof(unit.Position));
            if (byId.ContainsKey(unit.Id)) throw new ArgumentException($"Duplicate unit ID: {unit.Id}");
            units.Add(unit);
            byId.Add(unit.Id, unit);
        }

        public BattleUnit GetUnit(string id)
        {
            if (byId.TryGetValue(id, out var unit)) return unit;
            throw new KeyNotFoundException($"Unknown unit: {id}");
        }

        public bool TryGetUnit(string id, out BattleUnit unit) => byId.TryGetValue(id, out unit);
        public IEnumerable<BattleUnit> UnitsOf(Team team) => LivingUnits.Where(unit => unit.Team == team);
    }
}

