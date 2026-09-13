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
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (random == null) throw new ArgumentNullException(nameof(random));

            if (!actor.IsAlive || !target.IsAlive)
                return SkillExecutionResult.Failed(DeadUnitReason);

            if (!SkillTargetValidator.GetValidTargets(state, actor, skill).Contains(target))
                return SkillExecutionResult.Failed(InvalidTargetReason);

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
                if (!target.IsAlive) break;

                AddAffected(affected, affectedSet, target);
                ExecuteEffect(effect, state, actor, target, random, ref damaged, ref healed);
            }

            return SkillExecutionResult.Succeeded(damaged, healed, affected);
        }

        private static void ExecuteEffect(
            SkillEffectDefinition effect,
            BattleState state,
            BattleUnit actor,
            BattleUnit target,
            IRandomSource random,
            ref int damaged,
            ref int healed)
        {
            switch (effect.Kind)
            {
                case SkillEffectKind.Damage:
                    var request = new DamageRequest(
                        actor,
                        target,
                        effect.PowerMultiplier,
                        DamageType.Physical,
                        false);
                    damaged += DamageCalculator.Calculate(request, state.Map, random).Damage;
                    break;
                case SkillEffectKind.Heal:
                    var previousHealth = target.Health;
                    target.Heal(effect.Magnitude);
                    healed += target.Health - previousHealth;
                    break;
                case SkillEffectKind.ApplyStatus:
                    StatusSystem.Apply(
                        target,
                        effect.StatusType,
                        effect.Magnitude,
                        effect.Duration,
                        actor.Id);
                    break;
                case SkillEffectKind.Push:
                    MoveAwayFromActor(state, actor, target, effect.Magnitude);
                    break;
                case SkillEffectKind.Pull:
                    MoveTowardActor(state, actor, target, effect.Magnitude);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(effect.Kind));
            }

            if (effect.Kind != SkillEffectKind.ApplyStatus &&
                effect.Magnitude > 0 &&
                effect.Duration > 0)
            {
                StatusSystem.Apply(
                    target,
                    effect.StatusType,
                    effect.Magnitude,
                    effect.Duration,
                    actor.Id);
            }
        }

        private static void MoveAwayFromActor(
            BattleState state,
            BattleUnit actor,
            BattleUnit target,
            int distance)
        {
            var step = Direction(actor, target);
            MoveInDirection(state, target, step, distance);
        }

        private static void MoveTowardActor(
            BattleState state,
            BattleUnit actor,
            BattleUnit target,
            int distance)
        {
            var step = Direction(actor, target);
            MoveInDirection(
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

        private static void MoveInDirection(
            BattleState state,
            BattleUnit target,
            GridPosition step,
            int distance)
        {
            if (step == new GridPosition(0, 0)) return;

            for (var moved = 0; moved < distance; moved++)
            {
                var destination = new GridPosition(
                    target.Position.X + step.X,
                    target.Position.Y + step.Y);

                if (!CanOccupy(state, target, destination)) break;
                target.MoveForced(destination);
            }
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
    }
}
