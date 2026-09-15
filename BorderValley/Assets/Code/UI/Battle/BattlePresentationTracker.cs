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
            BattleActionResult result) =>
            Observe(state, command, result, null);

        public IReadOnlyList<BattlePresentationEvent> Observe(
            BattleState state,
            BattleCommand command,
            BattleActionResult result,
            string currentActiveUnitId)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            var events = new List<BattlePresentationEvent>();
            if (!initialized)
            {
                activeUnitId = ResolveActiveUnitId(command, result, currentActiveUnitId);
                CaptureSnapshot(state);
                lastResult = result;
                initialized = true;
                events.Add(new BattlePresentationEvent(
                    activeUnitId,
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
            var nextActiveUnitId = ResolveActiveUnitId(
                command,
                result,
                currentActiveUnitId);
            if (!string.Equals(activeUnitId, nextActiveUnitId, StringComparison.Ordinal))
            {
                activeUnitId = nextActiveUnitId;
                events.Add(new BattlePresentationEvent(
                    activeUnitId,
                    BattlePresentationEventKind.TurnChanged));
            }

            CaptureSnapshot(state);
            return events.ToArray();
        }

        private string ResolveActiveUnitId(
            BattleCommand command,
            BattleActionResult result,
            string currentActiveUnitId)
        {
            if (!string.IsNullOrWhiteSpace(currentActiveUnitId))
                return currentActiveUnitId;

            if (result?.Success == true && command is EndTurnCommand)
                return string.Empty;

            return command?.UnitId ?? activeUnitId;
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
