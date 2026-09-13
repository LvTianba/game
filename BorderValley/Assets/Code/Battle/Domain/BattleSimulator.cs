using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Random;

namespace BorderValley.Battle.Domain
{
    public sealed class SimulationRecord
    {
        public SimulationRecord(
            BattleOutcome outcome,
            int rounds,
            IEnumerable<string> commands)
        {
            Outcome = outcome;
            Rounds = Math.Max(1, rounds);
            Commands = (commands ?? Array.Empty<string>()).ToList().AsReadOnly();
        }

        public BattleOutcome Outcome { get; }
        public int Rounds { get; }
        public int Round => Rounds;
        public IReadOnlyList<string> Commands { get; }
        public int CommandCount => Commands.Count;
    }

    public static class BattleSimulator
    {
        public static SimulationRecord RunUntilComplete(
            BattleScenario scenario,
            IRandomSource random,
            int maxCommands = 200)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var engine = scenario.CreateEngine(random);
            engine.Start();
            return RunUntilComplete(engine, scenario, maxCommands);
        }

        public static SimulationRecord RunUntilComplete(
            BattleEngine engine,
            BattleScenario scenario,
            int maxCommands = 200)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (!ReferenceEquals(engine.State, scenario.State))
                throw new InvalidOperationException("BattleEngine and BattleScenario must share the same BattleState.");

            return RunUntilCompleteCore(engine, scenario.GetSkillsForUnit, maxCommands);
        }

        public static SimulationRecord RunUntilComplete(
            BattleEngine engine,
            IReadOnlyDictionary<string, SkillDefinition> playerSkills,
            IReadOnlyDictionary<string, SkillDefinition> enemySkills,
            int maxCommands = 200)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (playerSkills == null) throw new ArgumentNullException(nameof(playerSkills));
            if (enemySkills == null) throw new ArgumentNullException(nameof(enemySkills));
            if (!engine.HasUnitSkillConfiguration)
            {
                throw new InvalidOperationException(
                    "BattleEngine must be configured with UnitSkills before simulation.");
            }

            return RunUntilCompleteCore(
                engine,
                unitId =>
                {
                    if (!engine.State.TryGetUnit(unitId, out var unit))
                        throw new ArgumentException($"Unknown unit ID: {unitId}", nameof(unitId));

                    var sideSkills = unit.Team == Team.Player ? playerSkills : enemySkills;
                    return FilterOwnedSkills(engine, unitId, sideSkills);
                },
                maxCommands);
        }

        private static SimulationRecord RunUntilCompleteCore(
            BattleEngine engine,
            Func<string, IReadOnlyDictionary<string, SkillDefinition>> skillsForUnit,
            int maxCommands)
        {
            if (maxCommands <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCommands));
            if (engine.ActiveUnit == null)
                engine.Start();

            var commands = new List<string>();
            while (engine.Outcome == BattleOutcome.InProgress && commands.Count < maxCommands)
            {
                var actor = engine.ActiveUnit;
                if (actor == null)
                    throw new InvalidOperationException("Battle has no active unit.");

                var skills = skillsForUnit(actor.Id);
                var command = BattleAi.ChooseCommand(engine, actor.Id, skills);
                var result = engine.Execute(command);
                if (!result.Success)
                {
                    throw new InvalidOperationException(
                        $"AI command failed: {Format(command)} ({result.ErrorCode}).");
                }

                commands.Add(Format(command));
            }

            if (engine.Outcome == BattleOutcome.InProgress)
            {
                throw new InvalidOperationException(
                    $"Battle did not complete within {maxCommands} commands.");
            }

            return new SimulationRecord(engine.Outcome, engine.State.Round, commands);
        }

        private static IReadOnlyDictionary<string, SkillDefinition> FilterOwnedSkills(
            BattleEngine engine,
            string unitId,
            IReadOnlyDictionary<string, SkillDefinition> skills)
        {
            var result = new Dictionary<string, SkillDefinition>(StringComparer.Ordinal);
            foreach (var pair in skills)
            {
                if (pair.Value == null)
                    throw new ArgumentException("Skill definitions cannot contain null.", nameof(skills));
                if (!string.Equals(pair.Key, pair.Value.Id, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"Skill dictionary key '{pair.Key}' must match SkillDefinition.Id '{pair.Value.Id}'.",
                        nameof(skills));
                }

                if (engine.OwnsSkill(unitId, pair.Key))
                    result.Add(pair.Key, pair.Value);
            }

            return result;
        }

        private static string Format(BattleCommand command)
        {
            return command switch
            {
                MoveCommand move => $"move:{move.UnitId}:{move.Destination}",
                UseSkillCommand skill =>
                    $"skill:{skill.UnitId}:{skill.SkillId}:{skill.TargetUnitId}",
                EndTurnCommand endTurn => $"end_turn:{endTurn.UnitId}",
                _ => throw new ArgumentOutOfRangeException(nameof(command))
            };
        }
    }
}
