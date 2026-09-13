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

            foreach (var skill in skills.Values)
            {
                if (actor.Mana < skill.Mana) continue;
                if (actor.Cooldowns.TryGetValue(skill.Id, out var cooldown) && cooldown > 0)
                    continue;

                var validTargets = SkillTargetValidator
                    .GetValidTargets(state, actor, skill)
                    .OrderBy(target => target.Id, StringComparer.Ordinal);

                foreach (var target in validTargets)
                {
                    candidates.Add(new AiCandidate(
                        new UseSkillCommand(actor.Id, skill.Id, target.Id),
                        ScoreSkill(state, actor, skill, target),
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
            if (enemies.Length == 0) return 0;

            var nearestDistance = enemies.Min(enemy =>
                ManhattanDistance(destination, enemy.Position));

            var enterRangeDistance = int.MaxValue;
            foreach (var skill in skills.Values)
            {
                if (!HasHostileEffect(skill)) continue;

                foreach (var enemy in enemies)
                {
                    if (!CanAffectEnemyFrom(state, actor, skill, destination, enemy))
                        continue;

                    enterRangeDistance = Math.Min(
                        enterRangeDistance,
                        ManhattanDistance(destination, enemy.Position));
                }
            }

            if (enterRangeDistance != int.MaxValue)
                return EnterRangeScore - enterRangeDistance;

            return ApproachScore - nearestDistance * ApproachDistanceWeight;
        }

        private static long ScoreSkill(
            BattleState state,
            BattleUnit actor,
            SkillDefinition skill,
            BattleUnit anchor)
        {
            var score = 0L;
            var projectedHealth = new Dictionary<BattleUnit, int>();
            var remainingShields = new Dictionary<BattleUnit, int>();

            foreach (var effect in skill.Effects)
            {
                foreach (var target in GetEffectTargets(state, actor, skill, anchor, effect))
                {
                    var health = GetProjectedHealth(projectedHealth, target);
                    if (health <= 0) continue;

                    switch (effect.Kind)
                    {
                        case SkillEffectKind.Damage:
                        {
                            var shield = GetRemainingShield(remainingShields, target);
                            var damage = PredictDamage(state.Map, actor, target, effect);
                            var absorbed = Math.Min(shield, damage);
                            remainingShields[target] = shield - absorbed;

                            var healthDamage = damage - absorbed;
                            score += (long)healthDamage * DamageWeight;
                            projectedHealth[target] = Math.Max(0, health - healthDamage);
                            break;
                        }
                        case SkillEffectKind.Heal:
                        {
                            var healed = Math.Min(
                                Math.Max(0, effect.Magnitude),
                                target.Stats.MaxHealth - health);
                            score += (long)healed * HealingWeight;
                            projectedHealth[target] = health + healed;
                            break;
                        }
                        case SkillEffectKind.ApplyStatus:
                        {
                            if (effect.Magnitude <= 0 || effect.Duration <= 0) break;

                            score += StatusScore(effect.StatusType, effect.Magnitude);
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

            return score;
        }

        private static IEnumerable<BattleUnit> GetEffectTargets(
            BattleState state,
            BattleUnit actor,
            SkillDefinition skill,
            BattleUnit anchor,
            SkillEffectDefinition effect)
        {
            if (skill.Radius == 0)
            {
                if (MatchesEffectTarget(effect, actor.Team, anchor))
                    yield return anchor;
                yield break;
            }

            foreach (var unit in state.LivingUnits)
            {
                if (ManhattanDistance(unit.Position, anchor.Position) <= skill.Radius &&
                    MatchesEffectTarget(effect, actor.Team, unit))
                {
                    yield return unit;
                }
            }
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

        private static int PredictDamage(
            BattleMap map,
            BattleUnit actor,
            BattleUnit target,
            SkillEffectDefinition effect)
        {
            var raw = Math.Max(
                1,
                (int)MathF.Round(actor.Stats.Power * effect.PowerMultiplier));
            var defense = effect.DamageType == DamageType.Physical
                ? target.Stats.Armor
                : target.Stats.Resistance;
            var damage = Math.Max(1, raw - defense);

            if (map.GetTerrain(actor.Position) == TerrainType.HighGround)
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

        private static long StatusScore(StatusType status, int magnitude) =>
            status switch
            {
                StatusType.Stunned => StunScore,
                StatusType.Taunted => TauntScore,
                StatusType.Slowed => SlowScore,
                StatusType.Shielded => (long)magnitude * ShieldWeight,
                _ => 0
            };

        private static int ThreatValue(BattleUnit target) => target.Stats.Power;

        private static bool HasHostileEffect(SkillDefinition skill) =>
            skill.Effects.Any(effect =>
                effect.Kind is SkillEffectKind.Damage or SkillEffectKind.Push or SkillEffectKind.Pull ||
                effect.Kind == SkillEffectKind.ApplyStatus &&
                effect.StatusType != StatusType.Shielded);

        private static bool CanAffectEnemyFrom(
            BattleState state,
            BattleUnit actor,
            SkillDefinition skill,
            GridPosition origin,
            BattleUnit enemy)
        {
            var distance = ManhattanDistance(origin, enemy.Position);
            return skill.Targeting switch
            {
                SkillTargeting.Enemy =>
                    distance <= skill.Range &&
                    (skill.Range <= 1 || HasLineOfSight(state.Map, origin, enemy.Position)),
                SkillTargeting.Ground =>
                    distance <= skill.Range + skill.Radius &&
                    (skill.Range <= 1 || HasLineOfSight(state.Map, origin, enemy.Position)),
                SkillTargeting.Self =>
                    skill.Radius > 0 && distance <= skill.Radius,
                _ => false
            };
        }

        private static bool HasLineOfSight(
            BattleMap map,
            GridPosition start,
            GridPosition end)
        {
            if (ManhattanDistance(start, end) <= 1) return true;

            var x = start.X;
            var y = start.Y;
            var deltaX = Math.Abs(end.X - start.X);
            var deltaY = -Math.Abs(end.Y - start.Y);
            var stepX = start.X < end.X ? 1 : -1;
            var stepY = start.Y < end.Y ? 1 : -1;
            var error = deltaX + deltaY;

            while (true)
            {
                var isEndpoint = (x == start.X && y == start.Y) || (x == end.X && y == end.Y);
                if (!isEndpoint)
                {
                    var terrain = map.GetTerrain(new GridPosition(x, y));
                    if (terrain is TerrainType.Obstacle or TerrainType.Bush)
                        return false;
                }

                if (x == end.X && y == end.Y) break;

                var doubledError = 2 * error;
                if (doubledError >= deltaY)
                {
                    error += deltaY;
                    x += stepX;
                }

                if (doubledError <= deltaX)
                {
                    error += deltaX;
                    y += stepY;
                }
            }

            return true;
        }

        private static int ManhattanDistance(GridPosition left, GridPosition right) =>
            Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);

        private static void ValidateSkills(
            IReadOnlyDictionary<string, SkillDefinition> skills)
        {
            foreach (var pair in skills)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    throw new ArgumentException("Skill IDs cannot be empty.", nameof(skills));
                if (pair.Value == null)
                    throw new ArgumentException("Skill definitions cannot contain null.", nameof(skills));
            }
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
