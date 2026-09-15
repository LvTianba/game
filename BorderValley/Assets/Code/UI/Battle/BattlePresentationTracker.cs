using System;
using System.Collections.Generic;
using BorderValley.Battle.Domain;

namespace BorderValley.UI.Battle
{
    public enum BattlePresentationEventKind
    {
        Move,
        Attack,
        Hit,
        Down,
        TurnChanged
    }

    public readonly struct BattlePresentationEvent
    {
        public BattlePresentationEvent(string unitId, BattlePresentationEventKind kind)
        {
            UnitId = unitId ?? string.Empty;
            Kind = kind;
        }

        public string UnitId { get; }
        public BattlePresentationEventKind Kind { get; }
    }

    public sealed class BattlePresentationTracker
    {
        private readonly Dictionary<string, UnitSnapshot> snapshots =
            new Dictionary<string, UnitSnapshot>(StringComparer.Ordinal);

        private bool initialized;
        private string activeUnitId = string.Empty;
        private BattleActionResult lastResult;

        public IReadOnlyList<BattlePresentationEvent> Observe(
            BattleState state,
            BattleCommand command,
            BattleActionResult result,
            string activeUnitId)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (activeUnitId == null)
                throw new ArgumentNullException(nameof(activeUnitId));

            var events = new List<BattlePresentationEvent>();
            if (!initialized)
            {
                this.activeUnitId = activeUnitId;
                CaptureSnapshot(state);
                lastResult = result;
                initialized = true;
                events.Add(new BattlePresentationEvent(
                    this.activeUnitId,
                    BattlePresentationEventKind.TurnChanged));
                return events.ToArray();
            }

            foreach (var unit in state.Units)
            {
                if (!snapshots.TryGetValue(unit.Id, out var previous))
                    continue;

                if (previous.Position != unit.Position)
                {
                    events.Add(new BattlePresentationEvent(
                        unit.Id,
                        BattlePresentationEventKind.Move));
                }

                if (unit.Health < previous.Health)
                {
                    events.Add(new BattlePresentationEvent(
                        unit.Id,
                        BattlePresentationEventKind.Hit));

                    if (previous.Health > 0 && unit.Health == 0)
                    {
                        events.Add(new BattlePresentationEvent(
                            unit.Id,
                            BattlePresentationEventKind.Down));
                    }
                }
            }

            if (!ReferenceEquals(result, lastResult) &&
                result != null &&
                result.Success &&
                command is UseSkillCommand)
            {
                events.Add(new BattlePresentationEvent(
                    command.UnitId,
                    BattlePresentationEventKind.Attack));
            }

            lastResult = result;
            if (!string.Equals(this.activeUnitId, activeUnitId, StringComparison.Ordinal))
            {
                this.activeUnitId = activeUnitId;
                events.Add(new BattlePresentationEvent(
                    this.activeUnitId,
                    BattlePresentationEventKind.TurnChanged));
            }

            CaptureSnapshot(state);
            return events.ToArray();
        }

        private void CaptureSnapshot(BattleState state)
        {
            snapshots.Clear();
            foreach (var unit in state.Units)
            {
                snapshots[unit.Id] = new UnitSnapshot(unit.Position, unit.Health);
            }
        }

        private readonly struct UnitSnapshot
        {
            public UnitSnapshot(GridPosition position, int health)
            {
                Position = position;
                Health = health;
            }

            public GridPosition Position { get; }
            public int Health { get; }
        }
    }
}
