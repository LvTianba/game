using System.Linq;

namespace BorderValley.Battle.Domain
{
    public static class StatusSystem
    {
        public static void Apply(BattleUnit target, StatusType type, int magnitude, int duration, string sourceId)
        {
            var existing = target.MutableStatuses.FirstOrDefault(status => status.Type == type);
            if (existing == null)
            {
                target.MutableStatuses.Add(new StatusInstance(type, magnitude, duration, sourceId));
                return;
            }

            if (type is StatusType.Burning or StatusType.Poisoned or StatusType.Shielded)
                existing.Magnitude += magnitude;
            else
                existing.Magnitude = System.Math.Max(existing.Magnitude, magnitude);
            if (type == StatusType.Taunted)
                existing.SourceUnitId = sourceId;
            existing.RemainingTurns = System.Math.Max(existing.RemainingTurns, duration);
        }

        public static void ResolveTurnStart(BattleUnit unit)
        {
            var damage = unit.MutableStatuses
                .Where(status => status.Type is StatusType.Burning or StatusType.Poisoned)
                .Sum(status => status.Magnitude);
            if (damage > 0) unit.ApplyRawDamage(damage);
        }

        public static void ResolveTurnEnd(BattleUnit unit)
        {
            foreach (var status in unit.MutableStatuses)
                status.RemainingTurns--;
            unit.MutableStatuses.RemoveAll(status => status.RemainingTurns <= 0);
        }

        public static bool IsStunned(BattleUnit unit) =>
            unit.MutableStatuses.Any(status => status.Type == StatusType.Stunned);

        public static bool IsTaunted(BattleUnit unit) =>
            unit.MutableStatuses.Any(status => status.Type == StatusType.Taunted);

        public static bool TryGetActiveTauntSource(
            BattleState state,
            BattleUnit unit,
            out BattleUnit source)
        {
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            if (unit == null) throw new System.ArgumentNullException(nameof(unit));

            source = null;
            var taunt = unit.MutableStatuses.FirstOrDefault(status => status.Type == StatusType.Taunted);
            if (taunt == null || string.IsNullOrWhiteSpace(taunt.SourceUnitId))
                return false;
            if (!state.TryGetUnit(taunt.SourceUnitId, out source) || !source.IsAlive)
            {
                source = null;
                return false;
            }

            return true;
        }

        public static int MovementPenalty(BattleUnit unit) =>
            unit.MutableStatuses.Where(status => status.Type == StatusType.Slowed)
                .Sum(status => status.Magnitude);

        public static int GetShield(BattleUnit unit) =>
            unit.MutableStatuses.Where(status => status.Type == StatusType.Shielded)
                .Sum(status => status.Magnitude);

        public static int ConsumeShield(BattleUnit unit, int amount)
        {
            var remaining = amount;
            var absorbed = 0;
            foreach (var status in unit.MutableStatuses.Where(status => status.Type == StatusType.Shielded).ToArray())
            {
                var used = System.Math.Min(status.Magnitude, remaining);
                status.Magnitude -= used;
                remaining -= used;
                absorbed += used;
                if (remaining == 0) break;
            }
            unit.MutableStatuses.RemoveAll(status => status.Type == StatusType.Shielded && status.Magnitude <= 0);
            return absorbed;
        }
    }
}
