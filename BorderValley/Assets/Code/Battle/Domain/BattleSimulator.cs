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

            ValidateScenarioMatchesEngine(engine, scenario);
            return RunUntilCompleteCore(engine, maxCommands);
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

            ValidateCatalogMatchesEngine(engine, playerSkills, Team.Player, nameof(playerSkills));
            ValidateCatalogMatchesEngine(engine, enemySkills, Team.Enemy, nameof(enemySkills));
            return RunUntilCompleteCore(engine, maxCommands);
        }

        private static SimulationRecord RunUntilCompleteCore(
            BattleEngine engine,
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

                var skills = engine.GetSkillsForUnitById(actor.Id);
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

        private static void ValidateScenarioMatchesEngine(
            BattleEngine engine,
            BattleScenario scenario)
        {
            foreach (var unit in engine.State.Units)
            {
                var engineSkills = engine.GetSkillsForUnitById(unit.Id);
                var scenarioSkills = scenario.GetSkillsForUnit(unit.Id);
                if (engineSkills.Count != scenarioSkills.Count)
                {
                    throw new InvalidOperationException(
                        $"BattleScenario skill ownership for unit '{unit.Id}' does not match the BattleEngine.");
                }

                foreach (var pair in engineSkills)
                {
                    if (!scenarioSkills.TryGetValue(pair.Key, out var supplied) ||
                        !ReferenceEquals(pair.Value, supplied))
                    {
                        throw new InvalidOperationException(
                            $"BattleScenario skill definition '{pair.Key}' for unit '{unit.Id}' does not match the BattleEngine-owned definition.");
                    }
                }
            }
        }

        private static void ValidateCatalogMatchesEngine(
            BattleEngine engine,
            IReadOnlyDictionary<string, SkillDefinition> skills,
            Team team,
            string parameterName)
        {
            foreach (var pair in skills)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    throw new ArgumentException("Skill IDs cannot be empty.", parameterName);
                if (pair.Value == null)
                    throw new ArgumentException("Skill definitions cannot contain null.", parameterName);
                if (!string.Equals(pair.Key, pair.Value.Id, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"Skill dictionary key '{pair.Key}' must match SkillDefinition.Id '{pair.Value.Id}'.",
                        parameterName);
                }
                if (!engine.TryGetSkillDefinition(pair.Key, out var authoritative))
                {
                    throw new InvalidOperationException(
                        $"Skill definition '{pair.Key}' is not configured in the BattleEngine.");
                }
                if (!ReferenceEquals(authoritative, pair.Value))
                {
                    throw new InvalidOperationException(
                        $"Skill definition '{pair.Key}' in {parameterName} does not match the BattleEngine-owned definition.");
                }
            }

            foreach (var unit in engine.State.Units.Where(unit => unit.Team == team))
            {
                foreach (var pair in engine.GetSkillsForUnitById(unit.Id))
                {
                    if (!skills.TryGetValue(pair.Key, out var supplied) ||
                        !ReferenceEquals(pair.Value, supplied))
                    {
                        throw new InvalidOperationException(
                            $"Skill definition '{pair.Key}' for unit '{unit.Id}' is missing or does not match the BattleEngine-owned definition.");
                    }
                }
            }
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
