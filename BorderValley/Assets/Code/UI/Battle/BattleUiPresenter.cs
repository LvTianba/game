using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Battle.Domain;
using BorderValley.Core.Random;

namespace BorderValley.UI.Battle
{
    public sealed class BattleUiPresenter
    {
        private const string BasicSkillId = "skill.basic";

        private readonly BattleEngine engine;

        public BattleUiPresenter(BattleScenario scenario, IRandomSource random)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (random == null) throw new ArgumentNullException(nameof(random));

            engine = scenario.CreateEngine(random);
        }

        public event Action Changed;

        public BattleEngine Engine => engine;
        public BattleUnit ActiveUnit => engine.ActiveUnit;
        public bool IsPlayerTurn => ActiveUnit != null && ActiveUnit.Team == Team.Player;
        public bool IsFinished => engine.Outcome != BattleOutcome.InProgress;
        public string SelectedSkillId { get; private set; }
        public BattleOutcome Outcome => engine.Outcome;
        public BattleCommand LastCommand { get; private set; }
        public BattleActionResult LastResult { get; private set; }
        public long StateVersion { get; private set; }

        public void Start()
        {
            engine.Start();
            Notify();
        }

        public void SelectSkill(string skillId)
        {
            if (!IsPlayerTurn || ActiveUnit.HasActed || string.IsNullOrWhiteSpace(skillId))
                return;

            SelectedSkillId = SelectedSkillId == skillId ? null : skillId;
            Notify();
        }

        public BattleActionResult TapCell(GridPosition cell)
        {
            if (!IsPlayerTurn)
                return BattleActionResult.Failed(BattleTextKeys.NotPlayerTurn);

            BattleCommand command;
            var result = SelectedSkillId == null
                ? TryMoveOrBasicAttack(cell, out command)
                : TryUseSelectedSkill(cell, out command);
            LastCommand = command;
            LastResult = result;
            SelectedSkillId = null;
            Notify();
            return result;
        }

        public BattleActionResult EndTurn() =>
            IsPlayerTurn
                ? Execute(new EndTurnCommand(ActiveUnit.Id))
                : BattleActionResult.Failed(BattleTextKeys.NotPlayerTurn);

        public BattleActionResult Execute(BattleCommand command)
        {
            var activeUnitId = ActiveUnit?.Id;
            var result = engine.Execute(command);
            LastCommand = command;
            LastResult = result;
            var isSuccessfulEndTurn = result.Success && command is EndTurnCommand;
            if (IsFinished ||
                isSuccessfulEndTurn ||
                !string.Equals(activeUnitId, ActiveUnit?.Id, StringComparison.Ordinal))
            {
                SelectedSkillId = null;
            }

            Notify();
            return result;
        }

        public BattleHighlightKind GetHighlight(GridPosition cell)
        {
            if (!IsPlayerTurn) return BattleHighlightKind.None;
            if (SelectedSkillId == null)
            {
                if (IsFinished || ActiveUnit.HasMoved)
                    return BattleHighlightKind.None;

                return ReachableCells.ContainsKey(cell)
                    ? BattleHighlightKind.Move
                    : BattleHighlightKind.None;
            }

            var skills = engine.GetOwnedSkills(ActiveUnit.Id);
            if (!skills.TryGetValue(SelectedSkillId, out var skill))
                return BattleHighlightKind.None;

            if (skill.Targeting == SkillTargeting.Ground)
            {
                return SkillTargetValidator.GetValidGroundTargets(engine.State, ActiveUnit, skill)
                    .Contains(cell)
                    ? BattleHighlightKind.SkillRange
                    : BattleHighlightKind.None;
            }

            return SkillTargetValidator.GetValidTargets(engine.State, ActiveUnit, skill)
                .Any(unit => unit.Position == cell)
                ? BattleHighlightKind.Target
                : BattleHighlightKind.None;
        }

        private IReadOnlyDictionary<GridPosition, int> ReachableCells =>
            BattleMovement.FindReachableDestinations(engine.State, ActiveUnit);

        private BattleActionResult TryMoveOrBasicAttack(
            GridPosition cell,
            out BattleCommand command)
        {
            var skills = engine.GetOwnedSkills(ActiveUnit.Id);
            var basicSkill = skills.Values.FirstOrDefault(skill =>
                skill.Id == BasicSkillId ||
                skill.Id.StartsWith(BasicSkillId + "@", StringComparison.Ordinal));
            if (basicSkill != null)
            {
                var target = SkillTargetValidator.GetValidTargets(
                        engine.State,
                        ActiveUnit,
                        basicSkill)
                    .FirstOrDefault(unit => unit.Position == cell);
                if (target != null)
                {
                    command = new UseSkillCommand(ActiveUnit.Id, basicSkill.Id, target.Id);
                    return engine.Execute(command);
                }
            }

            command = new MoveCommand(ActiveUnit.Id, cell);
            return engine.Execute(command);
        }

        private BattleActionResult TryUseSelectedSkill(
            GridPosition cell,
            out BattleCommand command)
        {
            command = null;
            var skills = engine.GetOwnedSkills(ActiveUnit.Id);
            if (!skills.TryGetValue(SelectedSkillId, out var skill))
                return BattleActionResult.Failed(BattleTextKeys.InvalidTarget);

            if (skill.Targeting == SkillTargeting.Ground)
            {
                if (!SkillTargetValidator.GetValidGroundTargets(engine.State, ActiveUnit, skill)
                        .Contains(cell))
                {
                    return BattleActionResult.Failed(BattleTextKeys.InvalidTarget);
                }

                command = new UseSkillCommand(ActiveUnit.Id, skill.Id, cell);
                return engine.Execute(command);
            }

            var target = engine.State.LivingUnits.FirstOrDefault(unit => unit.Position == cell);
            if (target == null ||
                !SkillTargetValidator.GetValidTargets(engine.State, ActiveUnit, skill).Contains(target))
            {
                return BattleActionResult.Failed(BattleTextKeys.InvalidTarget);
            }

            command = new UseSkillCommand(ActiveUnit.Id, skill.Id, target.Id);
            return engine.Execute(command);
        }

        private void Notify()
        {
            StateVersion++;
            Changed?.Invoke();
        }
    }
}
