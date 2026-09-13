using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Battle.Domain
{
    public static class BattleAi
    {
        private const long LethalScore = 10000;
        private const int DamageWeight = 20;
        private const int HealingWeight = 15;
        private const int StunScore = 900;
        private const int TauntScore = 500;
        private const int SlowScore = 250;
        private const int ShieldWeight = 8;
        private const int EnterRangeScore = 400;
        private const int ApproachScore = 100;
        private const int ApproachDistanceWeight = 10;
        private const int EndTurnScore = -1000;
        private const int ThreatValuePerPower = 1;

        private const int SkillCommandRank = 0;
        private const int MoveCommandRank = 1;
        private const int EndTurnCommandRank = 2;

        public static BattleCommand ChooseCommand(
            BattleEngine engine,
            string unitId,
            IReadOnlyDictionary<string, SkillDefinition> skills)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (string.IsNullOrWhiteSpace(unitId))
                throw new ArgumentException("Unit ID cannot be empty.", nameof(unitId));
            if (skills == null) throw new ArgumentNullException(nameof(skills));

            ValidateSkills(skills);

            if (engine.Outcome != BattleOutcome.InProgress)
                throw new InvalidOperationException("Cannot choose a command after the battle has finished.");
            if (engine.ActiveUnit == null)
                throw new InvalidOperationException("Cannot choose a command before the battle starts.");

            var actor = engine.ActiveUnit;
            if (!engine.State.TryGetUnit(unitId, out var requestedUnit))
                throw new ArgumentException($"Unknown unit ID: {unitId}", nameof(unitId));
            if (!ReferenceEquals(requestedUnit, actor))
                throw new InvalidOperationException($"Unit '{unitId}' is not the active unit.");
            if (!actor.IsAlive)
                throw new InvalidOperationException($"Unit '{unitId}' is dead.");

            var candidates = new List<AiCandidate>();
            AddMoveCandidates(candidates, engine.State, actor, skills);
            AddSkillCandidates(candidates, engine.State, actor, skills);
            candidates.Add(new AiCandidate(
                new EndTurnCommand(actor.Id),
                EndTurnScore,
                EndTurnCommandRank,
                string.Empty,
                string.Empty,
                actor.Position));

            return candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.CommandRank)
                .ThenBy(candidate => candidate.SkillId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.TargetUnitId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.TargetPosition.X)
                .ThenBy(candidate => candidate.TargetPosition.Y)
                .First()
                .Command;
        }

        private static void AddMoveCandidates(
            ICollection<AiCandidate> candidates,
            BattleState state,
            BattleUnit actor,
            IReadOnlyDictionary<string, SkillDefinition> skills)
        {
            if (actor.HasMoved) return;

            var movement = Math.Max(0, actor.Stats.Speed - StatusSystem.MovementPenalty(actor));
            var occupied = state.LivingUnits
                .Where(unit => !ReferenceEquals(unit, actor))
                .Select(unit => unit.Position)
                .ToHashSet();
            var reachable = GridPathfinder.FindReachable(
                state.Map,
                actor.Position,
                movement,
                occupied);

            foreach (var destination in reachable.Keys
                         .Where(position => position != actor.Position)
                         .OrderBy(position => position.X)
                         .ThenBy(position => position.Y))
            {
                candidates.Add(new AiCandidate(
                    new MoveCommand(actor.Id, destination),
                    ScoreMove(state, actor, skills, destination),
                    MoveCommandRank,
                    string.Empty,
                    string.Empty,
                    destination));
            }
        }

        private static void AddSkillCandidates(
            ICollection<AiCandidate> candidates,
            BattleState state,
            BattleUnit actor,
            IReadOnlyDictionary<string, SkillDefinition> skills)
        {
            if (actor.HasActed) return;

            foreach (var pair in skills)
            {
                var skill = pair.Value;
                if (!CanUseSkillNow(actor, skill)) continue;

                var validTargets = SkillTargetingRules
                    .GetValidTargets(state, actor, actor.Position, skill)
                    .OrderBy(target => target.Id, StringComparer.Ordinal);

                foreach (var target in validTargets)
                {
                    var anchorPosition = skill.Targeting == SkillTargeting.Self
                        ? actor.Position
                        : target.Position;
                    var evaluation = EvaluateSkill(
                        state,
                        actor,
                        skill,
                        actor.Position,
                        anchorPosition,
                        target);

                    candidates.Add(new AiCandidate(
                        new UseSkillCommand(actor.Id, skill.Id, target.Id),
                        evaluation.Score,
                        SkillCommandRank,
                        skill.Id,
                        target.Id,
                        target.Position));
                }
            }
        }

        private static long ScoreMove(
            BattleState state,
            BattleUnit actor,
            IReadOnlyDictionary<string, SkillDefinition> skills,
            GridPosition destination)
        {
            var enemies = state.LivingUnits
                .Where(unit => unit.Team != actor.Team)
                .ToArray();
            var nearestEnemyDistance = enemies.Length == 0
                ? int.MaxValue
                : enemies.Min(enemy =>
                    SkillTargetingRules.ManhattanDistance(destination, enemy.Position));

            var enterRangeDistance = int.MaxValue;
            foreach (var pair in skills)
            {
                var skill = pair.Value;
                if (!CanUseSkillNow(actor, skill)) continue;

                if (skill.Targeting == SkillTargeting.Ground)
                {
                    foreach (var anchor in SkillTargetingRules.GetValidGroundTargets(
                                 state,
                                 destination,
                                 skill))
                    {
                        var evaluation = EvaluateSkill(
                            state,
                            actor,
                            skill,
                            destination,
                            anchor,
                            null);
                        if (!evaluation.HasEffect) continue;

                        enterRangeDistance = Math.Min(
                            enterRangeDistance,
                            evaluation.NearestAffectedDistance);
                    }

                    continue;
                }

                foreach (var target in SkillTargetingRules.GetValidTargets(
                             state,
                             actor,
                             destination,
                             skill))
                {
                    var anchorPosition = skill.Targeting == SkillTargeting.Self
                        ? destination
                        : target.Position;
                    var evaluation = EvaluateSkill(
                        state,
                        actor,
                        skill,
                        destination,
                        anchorPosition,
                        target);
                    if (!evaluation.HasEffect) continue;

                    enterRangeDistance = Math.Min(
                        enterRangeDistance,
                        evaluation.NearestAffectedDistance);
                }
            }

            if (enterRangeDistance != int.MaxValue)
                return EnterRangeScore - enterRangeDistance;
            if (nearestEnemyDistance == int.MaxValue)
                return 0;

            return ApproachScore - nearestEnemyDistance * ApproachDistanceWeight;
        }

        private static SkillEvaluation EvaluateSkill(
            BattleState state,
            BattleUnit actor,
            SkillDefinition skill,
            GridPosition actorPosition,
            GridPosition anchorPosition,
            BattleUnit unitTarget)
        {
            var score = 0L;
            var hasEffect = false;
            var nearestAffectedDistance = int.MaxValue;
            var projectedHealth = new Dictionary<BattleUnit, int>();
            var remainingShields = new Dictionary<BattleUnit, int>();

            void MarkEffect(BattleUnit target)
            {
                hasEffect = true;
                nearestAffectedDistance = Math.Min(
                    nearestAffectedDistance,
                    DistanceFromActor(actor, target, actorPosition));
            }

            foreach (var effect in skill.Effects)
            {
                foreach (var target in SkillTargetingRules.GetEffectTargets(
                             state,
                             actor,
                             skill,
                             anchorPosition,
                             unitTarget,
                             effect,
                             actorPosition))
                {
                    var health = GetProjectedHealth(projectedHealth, target);
                    if (health <= 0) continue;

                    switch (effect.Kind)
                    {
                        case SkillEffectKind.Damage:
                        {
                            var shield = GetRemainingShield(remainingShields, target);
                            var damage = PredictDamage(
                                state.Map,
                                actor,
                                actorPosition,
                                target,
                                effect);
                            var absorbed = Math.Min(shield, damage);
                            remainingShields[target] = shield - absorbed;

                            var healthDamage = damage - absorbed;
                            score += (long)healthDamage * DamageWeight;
                            projectedHealth[target] = Math.Max(0, health - healthDamage);
                            if (healthDamage > 0 || absorbed > 0)
                                MarkEffect(target);
                            break;
                        }
                        case SkillEffectKind.Heal:
                        {
                            var healed = Math.Min(
                                Math.Max(0, effect.Magnitude),
                                target.Stats.MaxHealth - health);
                            score += (long)healed * HealingWeight;
                            projectedHealth[target] = health + healed;
                            if (healed > 0) MarkEffect(target);
                            break;
                        }
                        case SkillEffectKind.ApplyStatus:
                        {
                            if (effect.Magnitude <= 0 || effect.Duration <= 0) break;

                            score += StatusScore(effect.StatusType, effect.Magnitude);
                            MarkEffect(target);
                            break;
                        }
                        case SkillEffectKind.Push:
                        {
                            if (CanDisplace(
                                    state,
                                    actor,
                                    actorPosition,
                                    target,
                                    effect.Kind,
                                    effect.Magnitude))
                            {
                                MarkEffect(target);
                            }

                            break;
                        }
                        case SkillEffectKind.Pull:
                        {
                            if (CanDisplace(
                                    state,
                                    actor,
                                    actorPosition,
                                    target,
                                    effect.Kind,
                                    effect.Magnitude))
                            {
                                MarkEffect(target);
                            }

                            break;
                        }
                    }
                }
            }

            foreach (var target in projectedHealth)
            {
                if (target.Value <= 0)
                    score += LethalScore + ThreatValue(target.Key);
            }

            return new SkillEvaluation(score, hasEffect, nearestAffectedDistance);
        }

        private static bool CanDisplace(
            BattleState state,
            BattleUnit actor,
            GridPosition actorPosition,
            BattleUnit target,
            SkillEffectKind kind,
            int distance)
        {
            if (distance <= 0) return false;

            var step = Direction(actorPosition, target.Position);
            if (kind == SkillEffectKind.Pull)
                step = new GridPosition(-step.X, -step.Y);
            if (step == new GridPosition(0, 0)) return false;

            var position = target.Position;
            for (var moved = 0; moved < distance; moved++)
            {
                var destination = new GridPosition(
                    position.X + step.X,
                    position.Y + step.Y);

                if (!CanOccupy(state, actor, actorPosition, target, destination))
                    return false;

                position = destination;
            }

            return position != target.Position;
        }

        private static GridPosition Direction(
            GridPosition actorPosition,
            GridPosition targetPosition)
        {
            var deltaX = targetPosition.X - actorPosition.X;
            var deltaY = targetPosition.Y - actorPosition.Y;

            if (Math.Abs(deltaX) >= Math.Abs(deltaY))
                return new GridPosition(Math.Sign(deltaX), 0);

            return new GridPosition(0, Math.Sign(deltaY));
        }

        private static bool CanOccupy(
            BattleState state,
            BattleUnit actor,
            GridPosition actorPosition,
            BattleUnit target,
            GridPosition destination)
        {
            if (!state.Map.InBounds(destination) ||
                state.Map.GetTerrain(destination) == TerrainType.Obstacle)
            {
                return false;
            }

            foreach (var unit in state.LivingUnits)
            {
                if (ReferenceEquals(unit, target)) continue;
                if (ReferenceEquals(unit, actor))
                {
                    if (actorPosition == destination) return false;
                    continue;
                }

                if (unit.Position == destination) return false;
            }

            return true;
        }

        private static int PredictDamage(
            BattleMap map,
            BattleUnit actor,
            GridPosition actorPosition,
            BattleUnit target,
            SkillEffectDefinition effect)
        {
            var raw = Math.Max(
                1,
                (int)MathF.Round(actor.Stats.Power * effect.PowerMultiplier));
            var defense = effect.DamageType == DamageType.Physical
                ? Math.Max(0, target.Stats.Armor - effect.ArmorPenetration)
                : target.Stats.Resistance;
            var damage = Math.Max(1, raw - defense);

            if (map.GetTerrain(actorPosition) == TerrainType.HighGround)
                damage = (int)MathF.Ceiling(damage * 1.25f);

            return damage;
        }

        private static int GetProjectedHealth(
            IDictionary<BattleUnit, int> projectedHealth,
            BattleUnit target)
        {
            if (!projectedHealth.TryGetValue(target, out var health))
            {
                health = target.Health;
                projectedHealth[target] = health;
            }

            return health;
        }

        private static int GetRemainingShield(
            IDictionary<BattleUnit, int> remainingShields,
            BattleUnit target)
        {
            if (!remainingShields.TryGetValue(target, out var shield))
            {
                shield = StatusSystem.GetShield(target);
                remainingShields[target] = shield;
            }

            return shield;
        }

        private static int DistanceFromActor(
            BattleUnit actor,
            BattleUnit target,
            GridPosition actorPosition) =>
            ReferenceEquals(actor, target)
                ? 0
                : SkillTargetingRules.ManhattanDistance(actorPosition, target.Position);

        private static long StatusScore(StatusType status, int magnitude) =>
            status switch
            {
                StatusType.Stunned => StunScore,
                StatusType.Taunted => TauntScore,
                StatusType.Slowed => SlowScore,
                StatusType.Shielded => (long)magnitude * ShieldWeight,
                _ => 0
            };

        // Controller ruling for the vertical slice: threat is exactly the target's Power stat.
        private static int ThreatValue(BattleUnit target) =>
            target.Stats.Power * ThreatValuePerPower;

        private static bool CanUseSkillNow(BattleUnit actor, SkillDefinition skill) =>
            actor.Mana >= skill.Mana &&
            (!actor.Cooldowns.TryGetValue(skill.Id, out var cooldown) || cooldown <= 0);

        private static void ValidateSkills(
            IReadOnlyDictionary<string, SkillDefinition> skills)
        {
            var definitionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in skills)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    throw new ArgumentException("Skill IDs cannot be empty.", nameof(skills));
                if (pair.Value == null)
                    throw new ArgumentException("Skill definitions cannot contain null.", nameof(skills));
                if (!string.Equals(pair.Key, pair.Value.Id, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"Skill dictionary key '{pair.Key}' must match SkillDefinition.Id '{pair.Value.Id}'.",
                        nameof(skills));
                }
                if (!definitionIds.Add(pair.Value.Id))
                {
                    throw new ArgumentException(
                        $"Duplicate skill definition ID: {pair.Value.Id}",
                        nameof(skills));
                }
            }
        }

        private readonly struct SkillEvaluation
        {
            public SkillEvaluation(
                long score,
                bool hasEffect,
                int nearestAffectedDistance)
            {
                Score = score;
                HasEffect = hasEffect;
                NearestAffectedDistance = nearestAffectedDistance;
            }

            public long Score { get; }
            public bool HasEffect { get; }
            public int NearestAffectedDistance { get; }
        }

        private sealed class AiCandidate
        {
            public AiCandidate(
                BattleCommand command,
                long score,
                int commandRank,
                string skillId,
                string targetUnitId,
                GridPosition targetPosition)
            {
                Command = command;
                Score = score;
                CommandRank = commandRank;
                SkillId = skillId;
                TargetUnitId = targetUnitId;
                TargetPosition = targetPosition;
            }

            public BattleCommand Command { get; }
            public long Score { get; }
            public int CommandRank { get; }
            public string SkillId { get; }
            public string TargetUnitId { get; }
            public GridPosition TargetPosition { get; }
        }
    }
}
