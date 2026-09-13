using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Random;

namespace BorderValley.Battle.Domain
{
    public static class SkillExecutor
    {
        private const string DeadUnitReason = "battle.skill.error.unit_dead";
        private const string InvalidTargetReason = "battle.skill.error.invalid_target";
        private const string InsufficientManaReason = "battle.skill.error.insufficient_mana";
        private const string CooldownReason = "battle.skill.error.cooldown";

        public static SkillExecutionResult Execute(
            BattleState state,
            BattleUnit actor,
            SkillDefinition skill,
            BattleUnit target,
            IRandomSource random)
        {
            ValidateArguments(state, actor, skill, random);
            if (target == null) throw new ArgumentNullException(nameof(target));

            if (!actor.IsAlive || !target.IsAlive)
                return SkillExecutionResult.Failed(DeadUnitReason);

            if (!SkillTargetValidator.GetValidTargets(state, actor, skill).Contains(target))
                return SkillExecutionResult.Failed(InvalidTargetReason);

            return ExecuteValidated(state, actor, skill, target.Position, target, random);
        }

        public static SkillExecutionResult Execute(
            BattleState state,
            BattleUnit actor,
            SkillDefinition skill,
            GridPosition target,
            IRandomSource random)
        {
            ValidateArguments(state, actor, skill, random);

            if (!actor.IsAlive)
                return SkillExecutionResult.Failed(DeadUnitReason);

            if (skill.Targeting != SkillTargeting.Ground ||
                !SkillTargetValidator.GetValidGroundTargets(state, actor, skill).Contains(target))
            {
                return SkillExecutionResult.Failed(InvalidTargetReason);
            }

            return ExecuteValidated(state, actor, skill, target, null, random);
        }

        private static SkillExecutionResult ExecuteValidated(
            BattleState state,
            BattleUnit actor,
            SkillDefinition skill,
            GridPosition anchor,
            BattleUnit unitTarget,
            IRandomSource random)
        {
            if (actor.Mana < skill.Mana)
                return SkillExecutionResult.Failed(InsufficientManaReason);

            if (actor.Cooldowns.TryGetValue(skill.Id, out var remainingCooldown) && remainingCooldown > 0)
                return SkillExecutionResult.Failed(CooldownReason);

            if (!actor.TrySpendMana(skill.Mana))
                return SkillExecutionResult.Failed(InsufficientManaReason);

            if (skill.Cooldown > 0)
                actor.Cooldowns[skill.Id] = skill.Cooldown;
            else
                actor.Cooldowns.Remove(skill.Id);

            actor.MarkActionUsed();

            var damaged = 0;
            var healed = 0;
            var affected = new List<BattleUnit>();
            var affectedSet = new HashSet<BattleUnit>();

            foreach (var effect in skill.Effects)
            {
                var targets = GetEffectTargets(state, actor, skill, anchor, unitTarget, effect);
                foreach (var target in targets)
                {
                    if (!target.IsAlive) continue;

                    if (ExecuteEffect(effect, state, actor, target, random, ref damaged, ref healed))
                        AddAffected(affected, affectedSet, target);
                }
            }

            return SkillExecutionResult.Succeeded(damaged, healed, affected);
        }

        private static IReadOnlyList<BattleUnit> GetEffectTargets(
            BattleState state,
            BattleUnit actor,
            SkillDefinition skill,
            GridPosition anchor,
            BattleUnit unitTarget,
            SkillEffectDefinition effect)
        {
            if (skill.Radius == 0)
            {
                if (unitTarget != null)
                {
                    return MatchesEffectTarget(effect, actor.Team, unitTarget)
                        ? new[] { unitTarget }
                        : Array.Empty<BattleUnit>();
                }

                return state.LivingUnits
                    .Where(unit =>
                        unit.Position == anchor &&
                        MatchesEffectTarget(effect, actor.Team, unit))
                    .ToArray();
            }

            return state.LivingUnits
                .Where(unit =>
                    ManhattanDistance(unit.Position, anchor) <= skill.Radius &&
                    MatchesEffectTarget(effect, actor.Team, unit))
                .ToArray();
        }

        private static bool MatchesEffectTarget(
            SkillEffectDefinition effect,
            Team actorTeam,
            BattleUnit unit)
        {
            return effect.Kind switch
            {
                SkillEffectKind.Damage or SkillEffectKind.Push or SkillEffectKind.Pull =>
                    unit.Team != actorTeam,
                SkillEffectKind.Heal =>
                    unit.Team == actorTeam,
                SkillEffectKind.ApplyStatus when effect.StatusType == StatusType.Shielded =>
                    unit.Team == actorTeam,
                SkillEffectKind.ApplyStatus =>
                    unit.Team != actorTeam,
                _ => throw new ArgumentOutOfRangeException(nameof(effect.Kind))
            };
        }

        private static bool ExecuteEffect(
            SkillEffectDefinition effect,
            BattleState state,
            BattleUnit actor,
            BattleUnit target,
            IRandomSource random,
            ref int damaged,
            ref int healed)
        {
            var changed = effect.Kind switch
            {
                SkillEffectKind.Damage => ApplyDamage(
                    effect, state, actor, target, random, ref damaged),
                SkillEffectKind.Heal => ApplyHeal(effect, target, ref healed),
                SkillEffectKind.ApplyStatus => ApplyStatus(effect, actor, target),
                SkillEffectKind.Push => MoveAwayFromActor(
                    state, actor, target, effect.Magnitude),
                SkillEffectKind.Pull => MoveTowardActor(
                    state, actor, target, effect.Magnitude),
                _ => throw new ArgumentOutOfRangeException(nameof(effect.Kind))
            };

            if (effect.Kind != SkillEffectKind.ApplyStatus &&
                target.IsAlive &&
                effect.Magnitude > 0 &&
                effect.Duration > 0)
            {
                StatusSystem.Apply(
                    target,
                    effect.StatusType,
                    effect.Magnitude,
                    effect.Duration,
                    actor.Id);
                changed = true;
            }

            return changed;
        }

        private static bool ApplyDamage(
            SkillEffectDefinition effect,
            BattleState state,
            BattleUnit actor,
            BattleUnit target,
            IRandomSource random,
            ref int damaged)
        {
            var request = new DamageRequest(
                actor,
                target,
                effect.PowerMultiplier,
                effect.DamageType,
                false);
            var result = DamageCalculator.Calculate(request, state.Map, random);
            damaged += result.Damage;
            return result.Damage > 0 || result.ShieldAbsorbed > 0;
        }

        private static bool ApplyHeal(
            SkillEffectDefinition effect,
            BattleUnit target,
            ref int healed)
        {
            var previousHealth = target.Health;
            target.Heal(effect.Magnitude);
            var actualHealing = target.Health - previousHealth;
            healed += actualHealing;
            return actualHealing > 0;
        }

        private static bool ApplyStatus(
            SkillEffectDefinition effect,
            BattleUnit actor,
            BattleUnit target)
        {
            if (effect.Magnitude <= 0 || effect.Duration <= 0) return false;

            StatusSystem.Apply(
                target,
                effect.StatusType,
                effect.Magnitude,
                effect.Duration,
                actor.Id);
            return true;
        }

        private static bool MoveAwayFromActor(
            BattleState state,
            BattleUnit actor,
            BattleUnit target,
            int distance)
        {
            var step = Direction(actor, target);
            return MoveInDirection(state, target, step, distance);
        }

        private static bool MoveTowardActor(
            BattleState state,
            BattleUnit actor,
            BattleUnit target,
            int distance)
        {
            var step = Direction(actor, target);
            return MoveInDirection(
                state,
                target,
                new GridPosition(-step.X, -step.Y),
                distance);
        }

        private static GridPosition Direction(BattleUnit actor, BattleUnit target)
        {
            var deltaX = target.Position.X - actor.Position.X;
            var deltaY = target.Position.Y - actor.Position.Y;

            if (Math.Abs(deltaX) >= Math.Abs(deltaY))
                return new GridPosition(Math.Sign(deltaX), 0);

            return new GridPosition(0, Math.Sign(deltaY));
        }

        private static bool MoveInDirection(
            BattleState state,
            BattleUnit target,
            GridPosition step,
            int distance)
        {
            if (step == new GridPosition(0, 0)) return false;

            var origin = target.Position;
            for (var moved = 0; moved < distance; moved++)
            {
                var destination = new GridPosition(
                    target.Position.X + step.X,
                    target.Position.Y + step.Y);

                if (!CanOccupy(state, target, destination)) break;
                target.MoveForced(destination);
            }

            return target.Position != origin;
        }

        private static bool CanOccupy(BattleState state, BattleUnit target, GridPosition destination)
        {
            if (!state.Map.InBounds(destination) ||
                state.Map.GetTerrain(destination) == TerrainType.Obstacle)
                return false;

            return !state.LivingUnits.Any(unit =>
                !ReferenceEquals(unit, target) && unit.Position == destination);
        }

        private static void AddAffected(
            List<BattleUnit> affected,
            ISet<BattleUnit> affectedSet,
            BattleUnit unit)
        {
            if (affectedSet.Add(unit)) affected.Add(unit);
        }

        private static int ManhattanDistance(GridPosition left, GridPosition right) =>
            Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);

        private static void ValidateArguments(
            BattleState state,
            BattleUnit actor,
            SkillDefinition skill,
            IRandomSource random)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            if (random == null) throw new ArgumentNullException(nameof(random));
        }
    }
}
