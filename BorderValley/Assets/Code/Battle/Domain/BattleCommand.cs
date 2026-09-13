using System;

namespace BorderValley.Battle.Domain
{
    public abstract class BattleCommand
    {
        protected BattleCommand(string unitId)
        {
            UnitId = string.IsNullOrWhiteSpace(unitId)
                ? throw new ArgumentException(nameof(unitId))
                : unitId;
        }

        public string UnitId { get; }
    }

    public sealed class MoveCommand : BattleCommand
    {
        public MoveCommand(string unitId, GridPosition destination)
            : base(unitId)
        {
            Destination = destination;
        }

        public GridPosition Destination { get; }
    }

    public sealed class UseSkillCommand : BattleCommand
    {
        public UseSkillCommand(string unitId, string skillId, string targetUnitId)
            : this(
                unitId,
                skillId,
                string.IsNullOrWhiteSpace(targetUnitId)
                    ? throw new ArgumentException(nameof(targetUnitId))
                    : targetUnitId,
                null)
        {
        }

        public UseSkillCommand(string unitId, string skillId, GridPosition targetPosition)
            : this(unitId, skillId, string.Empty, targetPosition)
        {
        }

        private UseSkillCommand(
            string unitId,
            string skillId,
            string targetUnitId,
            GridPosition? targetPosition)
            : base(unitId)
        {
            SkillId = string.IsNullOrWhiteSpace(skillId)
                ? throw new ArgumentException(nameof(skillId))
                : skillId;
            TargetUnitId = targetUnitId ?? string.Empty;
            TargetPosition = targetPosition;
        }

        public string SkillId { get; }
        public string TargetUnitId { get; }
        public GridPosition? TargetPosition { get; }
    }

    public sealed class EndTurnCommand : BattleCommand
    {
        public EndTurnCommand(string unitId)
            : base(unitId)
        {
        }
    }
}