using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Random;

namespace BorderValley.Battle.Domain
{
    public sealed class BattleEngine
    {
        private const string SuccessMoveMessage = "battle.command.success.move";
        private const string SuccessSkillMessage = "battle.command.success.skill";
        private const string SuccessEndTurnMessage = "battle.command.success.end_turn";
        private const string InvalidCommandError = "battle.command.error.invalid_command";
        private const string BattleNotStartedError = "battle.command.error.battle_not_started";
        private const string BattleFinishedError = "battle.command.error.battle_finished";
        private const string UnitNotFoundError = "battle.command.error.unit_not_found";
        private const string NotActiveUnitError = "battle.command.error.not_active_unit";
        private const string UnitDeadError = "battle.command.error.unit_dead";
        private const string AlreadyMovedError = "battle.command.error.already_moved";
        private const string DestinationOccupiedError = "battle.command.error.destination_occupied";
        private const string DestinationUnreachableError = "battle.command.error.destination_unreachable";
        private const string AlreadyActedError = "battle.command.error.already_acted";
        private const string SkillNotFoundError = "battle.command.error.skill_not_found";
        private const string TargetNotFoundError = "battle.command.error.target_not_found";

        private readonly BattleState state;
        private readonly IRandomSource random;
        private readonly BattleTurnEngine turnEngine;
        private readonly Dictionary<string, SkillDefinition> skills =
            new(StringComparer.Ordinal);

        public BattleEngine(
            BattleState state,
            IRandomSource random,
            IReadOnlyDictionary<string, SkillDefinition> skills = null)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            turnEngine = new BattleTurnEngine(state);

            if (skills == null) return;
            foreach (var pair in skills)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    throw new ArgumentException("Skill IDs cannot be empty.", nameof(skills));
                if (pair.Value == null)
                    throw new ArgumentException("Skill definitions cannot contain null.", nameof(skills));
                this.skills.Add(pair.Key, pair.Value);
            }
        }

        public BattleState State => state;
        public BattleUnit ActiveUnit => turnEngine.ActiveUnit;
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.InProgress;

        public void Start()
        {
            if (turnEngine.ActiveUnit != null)
                throw new InvalidOperationException("Battle has already started.");

            Outcome = BattleOutcome.InProgress;
            turnEngine.Start();
            ResolveCurrentTurnStart();
        }

        public BattleActionResult Execute(BattleCommand command)
        {
            if (command == null)
            {
                EvaluateOutcome();
                return Failed(InvalidCommandError);
            }

            var result = command switch
            {
                MoveCommand move => TryMove(move.UnitId, move.Destination),
                UseSkillCommand skill => UseSkill(
                    skill.UnitId,
                    skill.SkillId,
                    skill.TargetUnitId),
                EndTurnCommand endTurn => EndTurn(endTurn.UnitId),
                _ => Failed(InvalidCommandError)
            };
            EvaluateOutcome();
            return result;
        }

        public BattleActionResult TryMove(string unitId, GridPosition destination)
        {
            var result = TryMoveCore(unitId, destination);
            EvaluateOutcome();
            return result;
        }

        public BattleActionResult UseSkill(
            string unitId,
            string skillId,
            string targetUnitId)
        {
            var result = UseSkillCore(unitId, skillId, targetUnitId);
            EvaluateOutcome();
            return result;
        }

        public BattleActionResult EndTurn(string unitId)
        {
            var result = EndTurnCore(unitId);
            EvaluateOutcome();
            return result;
        }

        private BattleActionResult TryMoveCore(string unitId, GridPosition destination)
        {
            if (!TryGetActiveActor(unitId, out var actor, out var failure))
                return failure;

            if (actor.HasMoved)
                return Failed(AlreadyMovedError);

            if (destination == actor.Position)
                return Failed(DestinationOccupiedError);

            var occupied = state.LivingUnits
                .Where(unit => !ReferenceEquals(unit, actor))
                .Select(unit => unit.Position)
                .ToHashSet();

            if (occupied.Contains(destination))
                return Failed(DestinationOccupiedError);

            var movement = Math.Max(
                0,
                actor.Stats.Speed - StatusSystem.MovementPenalty(actor));
            var reachable = GridPathfinder.FindReachable(
                state.Map,
                actor.Position,
                movement,
                occupied);

            if (!reachable.ContainsKey(destination))
                return Failed(DestinationUnreachableError);

            actor.MoveTo(destination);
            return Succeeded(SuccessMoveMessage, actor.Id);
        }

        private BattleActionResult UseSkillCore(
            string unitId,
            string skillId,
            string targetUnitId)
        {
            if (!TryGetActiveActor(unitId, out var actor, out var failure))
                return failure;

            if (actor.HasActed)
                return Failed(AlreadyActedError);

            if (!skills.TryGetValue(skillId, out var skill))
                return Failed(SkillNotFoundError);

            if (!state.TryGetUnit(targetUnitId, out var target))
                return Failed(TargetNotFoundError);

            var execution = SkillExecutor.Execute(state, actor, skill, target, random);
            if (!execution.Success)
                return BattleActionResult.Failed(
                    execution.FailureReason,
                    execution.FailureReason,
                    execution.AffectedUnitIds.ToArray());

            return Succeeded(
                SuccessSkillMessage,
                execution.AffectedUnitIds.ToArray());
        }

        private BattleActionResult EndTurnCore(string unitId)
        {
            if (!TryGetActiveActor(unitId, out var actor, out var failure))
                return failure;

            StatusSystem.ResolveTurnEnd(actor);
            EvaluateOutcome();
            if (Outcome == BattleOutcome.InProgress)
            {
                turnEngine.EndTurn();
                ResolveCurrentTurnStart();
            }

            return Succeeded(SuccessEndTurnMessage, actor.Id);
        }

        private void ResolveCurrentTurnStart()
        {
            while (Outcome == BattleOutcome.InProgress)
            {
                var active = turnEngine.ActiveUnit;
                StatusSystem.ResolveTurnStart(active);
                EvaluateOutcome();
                if (Outcome != BattleOutcome.InProgress) return;

                if (!active.IsAlive)
                {
                    turnEngine.EndTurn();
                    continue;
                }

                if (!StatusSystem.IsStunned(active)) return;

                StatusSystem.ResolveTurnEnd(active);
                EvaluateOutcome();
                if (Outcome != BattleOutcome.InProgress) return;

                turnEngine.EndTurn();
            }
        }

        private bool TryGetActiveActor(
            string unitId,
            out BattleUnit actor,
            out BattleActionResult failure)
        {
            actor = null;
            failure = null;

            if (Outcome != BattleOutcome.InProgress)
            {
                failure = Failed(BattleFinishedError);
                return false;
            }

            if (turnEngine.ActiveUnit == null)
            {
                failure = Failed(BattleNotStartedError);
                return false;
            }

            if (!state.TryGetUnit(unitId, out actor))
            {
                failure = Failed(UnitNotFoundError);
                return false;
            }

            if (!ReferenceEquals(actor, turnEngine.ActiveUnit))
            {
                failure = Failed(NotActiveUnitError);
                return false;
            }

            if (!actor.IsAlive)
            {
                failure = Failed(UnitDeadError);
                return false;
            }

            return true;
        }

        private void EvaluateOutcome()
        {
            if (!state.LivingUnits.Any(unit => unit.Team == Team.Enemy))
            {
                Outcome = BattleOutcome.PlayerVictory;
                return;
            }

            if (!state.LivingUnits.Any(unit => unit.Team == Team.Player))
                Outcome = BattleOutcome.EnemyVictory;
        }

        private static BattleActionResult Succeeded(
            string message,
            params string[] affectedUnitIds) =>
            BattleActionResult.Succeeded(message, affectedUnitIds);

        private static BattleActionResult Failed(string errorCode) =>
            BattleActionResult.Failed(errorCode);
    }
}