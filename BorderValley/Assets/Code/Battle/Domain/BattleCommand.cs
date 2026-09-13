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
            : base(unitId)
        {
            SkillId = string.IsNullOrWhiteSpace(skillId)
                ? throw new ArgumentException(nameof(skillId))
                : skillId;
            TargetUnitId = string.IsNullOrWhiteSpace(targetUnitId)
                ? throw new ArgumentException(nameof(targetUnitId))
                : targetUnitId;
        }

        public string SkillId { get; }
        public string TargetUnitId { get; }
    }

    public sealed class EndTurnCommand : BattleCommand
    {
        public EndTurnCommand(string unitId)
            : base(unitId)
        {
        }
    }
}