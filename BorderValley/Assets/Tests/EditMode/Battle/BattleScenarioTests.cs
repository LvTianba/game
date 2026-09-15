using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Battle.Domain;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Random;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class BattleScenarioTests
    {
        private static readonly string[] RequiredSkillIds =
        {
            "skill.shield_bash",
            "skill.whirlwind",
            "skill.iron_guard",
            "skill.taunt",
            "skill.piercing_shot",
            "skill.snare",
            "skill.twin_shot",
            "skill.fireball",
            "skill.frost_nova",
            "skill.arcane_ward"
        };

        [Test]
        public void CoreScenario_DefinesMapUnitsSkillsAndOwnership()
        {
            var scenario = BattleScenarioFactory.CreateCoreScenario();

            Assert.That(scenario.State.Map.Width, Is.EqualTo(8));
            Assert.That(scenario.State.Map.Height, Is.EqualTo(6));
            AssertTerrainCount(scenario.State.Map, TerrainType.HighGround, 2);
            AssertTerrainCount(scenario.State.Map, TerrainType.Mud, 2);

            Assert.That(scenario.State.Units, Has.Count.EqualTo(6));
            Assert.That(scenario.State.Units.Count(unit => unit.Team == Team.Player), Is.EqualTo(3));
            Assert.That(scenario.State.Units.Count(unit => unit.Team == Team.Enemy), Is.EqualTo(3));

            AssertUnitStats(scenario, "player.warrior", 26, 8, 9, 6, 4, 0.05f, 2);
            AssertUnitStats(scenario, "player.ranger", 18, 10, 10, 3, 7, 0.15f, 2);
            AssertUnitStats(scenario, "player.mage", 15, 16, 11, 1, 5, 0.05f, 6);
            AssertUnitStats(scenario, "enemy.bandit", 20, 0, 8, 3, 5, 0.05f, 1);
            AssertUnitStats(scenario, "enemy.ranger", 18, 10, 10, 3, 7, 0.15f, 2);
            AssertUnitStats(scenario, "enemy.mage", 15, 16, 11, 1, 5, 0.05f, 6);

            Assert.That(
                scenario.State.GetUnit("player.warrior").Position,
                Is.EqualTo(new GridPosition(2, 2)));
            Assert.That(
                scenario.State.GetUnit("player.ranger").Position,
                Is.EqualTo(new GridPosition(1, 2)));
            Assert.That(
                scenario.State.GetUnit("player.mage").Position,
                Is.EqualTo(new GridPosition(1, 3)));
            Assert.That(
                scenario.State.GetUnit("enemy.bandit").Position,
                Is.EqualTo(new GridPosition(5, 2)));
            Assert.That(
                scenario.State.GetUnit("enemy.ranger").Position,
                Is.EqualTo(new GridPosition(6, 2)));
            Assert.That(
                scenario.State.GetUnit("enemy.mage").Position,
                Is.EqualTo(new GridPosition(6, 3)));

            var expectedSkillIds = RequiredSkillIds.Concat(new[] { "skill.basic" });
            Assert.That(scenario.AllSkills.Keys, Is.EquivalentTo(expectedSkillIds));
            Assert.That(scenario.AllSkills.Keys, Is.Unique);
            Assert.That(scenario.AllSkills, Has.All.Matches<KeyValuePair<string, SkillDefinition>>(
                pair => pair.Key == pair.Value.Id));

            Assert.That(scenario.UnitSkills.Keys, Is.EquivalentTo(scenario.State.Units.Select(unit => unit.Id)));
            foreach (var pair in scenario.UnitSkills)
            {
                Assert.That(pair.Value, Is.Unique);
                Assert.That(pair.Value, Is.Not.Empty);
                foreach (var skillId in pair.Value)
                    Assert.That(scenario.AllSkills.ContainsKey(skillId), Is.True, skillId);
            }

            Assert.That(
                scenario.UnitSkills["player.warrior"],
                Is.EquivalentTo(new[] { "skill.shield_bash", "skill.whirlwind", "skill.iron_guard", "skill.taunt", "skill.basic" }));
            Assert.That(
                scenario.UnitSkills["player.ranger"],
                Is.EquivalentTo(new[] { "skill.piercing_shot", "skill.snare", "skill.twin_shot", "skill.basic" }));
            Assert.That(
                scenario.UnitSkills["player.mage"],
                Is.EquivalentTo(new[] { "skill.fireball", "skill.frost_nova", "skill.arcane_ward", "skill.basic" }));
            Assert.That(scenario.UnitSkills["enemy.bandit"], Is.EquivalentTo(new[] { "skill.basic" }));
        }

        [Test]
        public void CoreScenario_SkillDefinitionsUseRequiredTargetingAndEffects()
        {
            var scenario = BattleScenarioFactory.CreateCoreScenario();

            var shieldBash = scenario.AllSkills["skill.shield_bash"];
            Assert.That(shieldBash.Targeting, Is.EqualTo(SkillTargeting.Enemy));
            Assert.That(shieldBash.Range, Is.EqualTo(1));
            Assert.That(shieldBash.Mana, Is.EqualTo(2));
            AssertEffect(shieldBash, SkillEffectKind.Damage, 1f);
            AssertStatusEffect(shieldBash, StatusType.Stunned, 1, 1);

            var whirlwind = scenario.AllSkills["skill.whirlwind"];
            Assert.That(whirlwind.Targeting, Is.EqualTo(SkillTargeting.Ground));
            Assert.That(whirlwind.Range, Is.Zero);
            Assert.That(whirlwind.Radius, Is.EqualTo(1));
            Assert.That(whirlwind.Mana, Is.EqualTo(4));
            AssertEffect(whirlwind, SkillEffectKind.Damage, 0.8f);
            AssertMagnitudeEffect(whirlwind, SkillEffectKind.Push, 1);

            var ironGuard = scenario.AllSkills["skill.iron_guard"];
            Assert.That(ironGuard.Targeting, Is.EqualTo(SkillTargeting.Self));
            Assert.That(ironGuard.Mana, Is.EqualTo(3));
            AssertStatusEffect(ironGuard, StatusType.Shielded, 6, 2);

            var taunt = scenario.AllSkills["skill.taunt"];
            Assert.That(taunt.Targeting, Is.EqualTo(SkillTargeting.Enemy));
            Assert.That(taunt.Range, Is.EqualTo(1));
            Assert.That(taunt.Radius, Is.Zero);
            Assert.That(taunt.Mana, Is.EqualTo(2));
            Assert.That(taunt.Cooldown, Is.EqualTo(2));
            AssertStatusEffect(taunt, StatusType.Taunted, 1, 2);

            var piercingShot = scenario.AllSkills["skill.piercing_shot"];
            Assert.That(piercingShot.Targeting, Is.EqualTo(SkillTargeting.Enemy));
            Assert.That(piercingShot.Range, Is.EqualTo(4));
            Assert.That(piercingShot.Mana, Is.EqualTo(2));
            var piercingDamage = AssertEffect(piercingShot, SkillEffectKind.Damage, 1.2f);
            Assert.That(piercingDamage.ArmorPenetration, Is.EqualTo(2));

            var snare = scenario.AllSkills["skill.snare"];
            AssertEffect(snare, SkillEffectKind.Damage, 0.6f);
            AssertStatusEffect(snare, StatusType.Slowed, 1, 2);

            var twinShot = scenario.AllSkills["skill.twin_shot"];
            AssertEffect(twinShot, SkillEffectKind.Damage, 1f);
            AssertStatusEffect(twinShot, StatusType.Poisoned, 2, 2);

            var fireball = scenario.AllSkills["skill.fireball"];
            Assert.That(fireball.Targeting, Is.EqualTo(SkillTargeting.Enemy));
            Assert.That(fireball.Range, Is.EqualTo(4));
            Assert.That(fireball.Radius, Is.EqualTo(1));
            AssertEffect(fireball, SkillEffectKind.Damage, 1.3f, DamageType.Magical);
            AssertStatusEffect(fireball, StatusType.Burning, 2, 2);

            var frostNova = scenario.AllSkills["skill.frost_nova"];
            Assert.That(frostNova.Targeting, Is.EqualTo(SkillTargeting.Self));
            Assert.That(frostNova.Radius, Is.EqualTo(2));
            AssertEffect(frostNova, SkillEffectKind.Damage, 0.7f, DamageType.Magical);
            AssertStatusEffect(frostNova, StatusType.Slowed, 2, 2);

            var arcaneWard = scenario.AllSkills["skill.arcane_ward"];
            Assert.That(arcaneWard.Targeting, Is.EqualTo(SkillTargeting.Ally));
            Assert.That(arcaneWard.Range, Is.EqualTo(3));
            AssertMagnitudeEffect(arcaneWard, SkillEffectKind.Heal, 6);
            AssertStatusEffect(arcaneWard, StatusType.Shielded, 4, 2);

            Assert.That(scenario.AllSkills["skill.basic"].Targeting, Is.EqualTo(SkillTargeting.Enemy));
            Assert.That(scenario.AllSkills["skill.basic"].Range, Is.EqualTo(1));
            Assert.That(scenario.AllSkills["skill.basic"].Mana, Is.Zero);
        }

        [Test]
        public void CoreScenario_DamageEffectsAllowCritByDefault()
        {
            var scenario = BattleScenarioFactory.CreateCoreScenario();

            var damageEffects = scenario.AllSkills.Values
                .SelectMany(skill => skill.Effects)
                .Where(effect => effect.Kind == SkillEffectKind.Damage)
                .ToArray();

            Assert.That(damageEffects, Is.Not.Empty);
            Assert.That(damageEffects, Has.All.Matches<SkillEffectDefinition>(
                effect => effect.CanCrit));
        }

        [Test]
        public void BattleEngine_GetSkillsForUnitById_ReturnsConfiguredInstances()
        {
            var scenario = BattleScenarioFactory.CreateCoreScenario();
            var engine = scenario.CreateEngine(RandomSourceFactory.FromSeed("authoritative-skills"));

            var owned = engine.GetSkillsForUnitById("player.mage");

            Assert.That(owned.Keys, Is.EquivalentTo(new[]
            {
                "skill.fireball",
                "skill.frost_nova",
                "skill.arcane_ward",
                "skill.basic"
            }));
            Assert.That(ReferenceEquals(owned["skill.fireball"], scenario.AllSkills["skill.fireball"]), Is.True);
            Assert.That(ReferenceEquals(
                engine.GetOwnedSkills("player.mage")["skill.fireball"],
                scenario.AllSkills["skill.fireball"]), Is.True);
        }

        [Test]
        public void Simulator_CompatibilityEntry_RejectsDefinitionCopiesWithoutMutation()
        {
            var scenario = BattleScenarioFactory.CreateCoreScenario();
            var engine = scenario.CreateEngine(RandomSourceFactory.FromSeed("definition-copy"));
            var playerSkills = scenario.PlayerSkills.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal);
            playerSkills["skill.fireball"] = CopyWithDifferentConfiguration(
                scenario.AllSkills["skill.fireball"]);
            var before = Snapshot(engine.State);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                BattleSimulator.RunUntilComplete(
                    engine,
                    playerSkills,
                    scenario.EnemySkills,
                    maxCommands: 20));

            Assert.That(exception.Message, Does.Contain("BattleEngine"));
            Assert.That(engine.ActiveUnit, Is.Null);
            Assert.That(engine.Outcome, Is.EqualTo(BattleOutcome.InProgress));
            Assert.That(Snapshot(engine.State), Is.EqualTo(before));
        }

        [Test]
        public void Simulator_CompatibilityEntry_UsesEngineAuthorityWithMatchingDefinitions()
        {
            var scenario = BattleScenarioFactory.CreateCoreScenario();
            var engine = scenario.CreateEngine(RandomSourceFactory.FromSeed("compatible-authority"));

            var result = BattleSimulator.RunUntilComplete(
                engine,
                scenario.PlayerSkills,
                scenario.EnemySkills,
                maxCommands: 200);

            Assert.That(result.Outcome, Is.Not.EqualTo(BattleOutcome.InProgress));
            Assert.That(result.Rounds, Is.LessThanOrEqualTo(30));
            Assert.That(result.Commands, Is.Not.Empty);
        }

        [Test]
        public void Simulator_ScenarioEntry_RejectsMismatchedDefinitionInstancesWithoutMutation()
        {
            var scenario = BattleScenarioFactory.CreateCoreScenario();
            var engine = scenario.CreateEngine(RandomSourceFactory.FromSeed("scenario-copy"));
            var playerSkills = scenario.PlayerSkills.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal);
            var enemySkills = scenario.EnemySkills.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal);
            var copiedFireball = CopyWithDifferentConfiguration(
                scenario.AllSkills["skill.fireball"]);
            playerSkills["skill.fireball"] = copiedFireball;
            enemySkills["skill.fireball"] = copiedFireball;
            var mismatchedScenario = new BattleScenario(
                scenario.State,
                playerSkills,
                enemySkills,
                scenario.UnitSkills);
            var before = Snapshot(engine.State);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                BattleSimulator.RunUntilComplete(engine, mismatchedScenario, maxCommands: 20));

            Assert.That(exception.Message, Does.Contain("BattleEngine"));
            Assert.That(engine.ActiveUnit, Is.Null);
            Assert.That(engine.Outcome, Is.EqualTo(BattleOutcome.InProgress));
            Assert.That(Snapshot(engine.State), Is.EqualTo(before));
        }

        [Test]
        public void CoreScenario_AreaSkillsUseRadiusAnchorsAndFactionFiltering()
        {
            var scenario = BattleScenarioFactory.CreateCoreScenario();

            var fireballState = new BattleState(BattleMap.CreatePlain(5, 3));
            fireballState.AddUnit(TestUnit("mage", Team.Player, 0, 1, 11));
            fireballState.AddUnit(TestUnit("enemy.a", Team.Enemy, 1, 1));
            fireballState.AddUnit(TestUnit("enemy.b", Team.Enemy, 2, 1));
            fireballState.AddUnit(TestUnit("ally", Team.Player, 1, 0));
            var fireballResult = SkillExecutor.Execute(
                fireballState,
                fireballState.GetUnit("mage"),
                scenario.AllSkills["skill.fireball"],
                fireballState.GetUnit("enemy.a"),
                RandomSourceFactory.FromSeed("fireball"));
            Assert.That(fireballResult.Success, Is.True);
            Assert.That(fireballState.GetUnit("enemy.a").Health, Is.LessThan(30));
            Assert.That(fireballState.GetUnit("enemy.b").Health, Is.LessThan(30));
            Assert.That(fireballState.GetUnit("ally").Health, Is.EqualTo(30));
            Assert.That(fireballState.GetUnit("enemy.a").Statuses.Any(status => status.Type == StatusType.Burning), Is.True);
            Assert.That(fireballState.GetUnit("enemy.b").Statuses.Any(status => status.Type == StatusType.Burning), Is.True);
            Assert.That(fireballState.GetUnit("ally").Statuses, Is.Empty);

            var frostState = new BattleState(BattleMap.CreatePlain(5, 3));
            frostState.AddUnit(TestUnit("mage", Team.Player, 2, 1, 11));
            frostState.AddUnit(TestUnit("enemy.a", Team.Enemy, 1, 1));
            frostState.AddUnit(TestUnit("enemy.b", Team.Enemy, 3, 1));
            frostState.AddUnit(TestUnit("ally", Team.Player, 2, 0));
            var frostResult = SkillExecutor.Execute(
                frostState,
                frostState.GetUnit("mage"),
                scenario.AllSkills["skill.frost_nova"],
                frostState.GetUnit("mage"),
                RandomSourceFactory.FromSeed("frost"));
            Assert.That(frostResult.Success, Is.True);
            Assert.That(frostState.GetUnit("enemy.a").Health, Is.LessThan(30));
            Assert.That(frostState.GetUnit("enemy.b").Health, Is.LessThan(30));
            Assert.That(frostState.GetUnit("ally").Health, Is.EqualTo(30));
            Assert.That(frostState.GetUnit("enemy.a").Statuses.Any(status => status.Type == StatusType.Slowed), Is.True);
            Assert.That(frostState.GetUnit("ally").Statuses, Is.Empty);

            var whirlwindState = new BattleState(BattleMap.CreatePlain(5, 5));
            whirlwindState.AddUnit(TestUnit("warrior", Team.Player, 2, 2, 9));
            whirlwindState.AddUnit(TestUnit("enemy.a", Team.Enemy, 3, 2));
            whirlwindState.AddUnit(TestUnit("enemy.b", Team.Enemy, 2, 3));
            whirlwindState.AddUnit(TestUnit("ally", Team.Player, 1, 2));
            var whirlwindResult = SkillExecutor.Execute(
                whirlwindState,
                whirlwindState.GetUnit("warrior"),
                scenario.AllSkills["skill.whirlwind"],
                new GridPosition(2, 2),
                RandomSourceFactory.FromSeed("whirlwind"));
            Assert.That(whirlwindResult.Success, Is.True);
            Assert.That(whirlwindState.GetUnit("enemy.a").Health, Is.LessThan(30));
            Assert.That(whirlwindState.GetUnit("enemy.b").Health, Is.LessThan(30));
            Assert.That(whirlwindState.GetUnit("enemy.a").Position, Is.EqualTo(new GridPosition(4, 2)));
            Assert.That(whirlwindState.GetUnit("enemy.b").Position, Is.EqualTo(new GridPosition(2, 4)));
            Assert.That(whirlwindState.GetUnit("ally").Health, Is.EqualTo(30));
            Assert.That(whirlwindState.GetUnit("ally").Position, Is.EqualTo(new GridPosition(1, 2)));
        }

        [Test]
        public void CoreScenario_CompletesWithinThirtyRounds()
        {
            var scenario = BattleScenarioFactory.CreateCoreScenario();

            var result = BattleSimulator.RunUntilComplete(
                scenario,
                RandomSourceFactory.FromSeed("vertical-slice"),
                maxCommands: 200);

            Assert.That(result.Outcome, Is.Not.EqualTo(BattleOutcome.InProgress));
            Assert.That(result.Rounds, Is.LessThanOrEqualTo(30));
            Assert.That(result.Commands, Is.Not.Empty);
        }

        [Test]
        public void CoreScenario_IsDeterministicForSameSeed()
        {
            var first = Run("same-seed");
            RandomSourceFactory.FromSeed("unrelated-static-state").NextUInt();
            var second = Run("same-seed");

            Assert.That(second.Outcome, Is.EqualTo(first.Outcome));
            Assert.That(second.Rounds, Is.EqualTo(first.Rounds));
            Assert.That(second.Commands, Is.EqualTo(first.Commands));
        }

        [Test]
        public void CoreScenario_EveryAiCommandExecutesSuccessfully()
        {
            var scenario = BattleScenarioFactory.CreateCoreScenario();
            var engine = scenario.CreateEngine(RandomSourceFactory.FromSeed("ai-command-check"));
            engine.Start();
            var commandCount = 0;

            while (engine.Outcome == BattleOutcome.InProgress && commandCount < 200)
            {
                var actor = engine.ActiveUnit;
                var skills = engine.GetSkillsForUnitById(actor.Id);
                var command = BattleAi.ChooseCommand(engine, actor.Id, skills);
                var result = engine.Execute(command);

                Assert.That(result.Success, Is.True, command.ToString());
                if (command is UseSkillCommand skillCommand)
                    Assert.That(skills.ContainsKey(skillCommand.SkillId), Is.True);

                commandCount++;
            }

            Assert.That(engine.Outcome, Is.Not.EqualTo(BattleOutcome.InProgress));
            Assert.That(commandCount, Is.LessThanOrEqualTo(200));
        }

        [Test]
        public void BattleEngine_RejectsSkillNotOwnedByUnit_WithoutMutation()
        {
            var scenario = BattleScenarioFactory.CreateCoreScenario();
            var engine = scenario.CreateEngine(RandomSourceFactory.FromSeed("ownership"));
            engine.Start();
            var actor = engine.ActiveUnit;
            var target = engine.State.GetUnit("enemy.bandit");
            var before = Snapshot(engine.State);
            var manaBefore = actor.Mana;
            var cooldownCountBefore = actor.Cooldowns.Count;
            var targetHealthBefore = target.Health;

            Assert.That(scenario.GetSkillsForUnit(actor.Id).ContainsKey("skill.fireball"), Is.False);

            var result = engine.Execute(new UseSkillCommand(actor.Id, "skill.fireball", target.Id));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.command.error.skill_not_owned"));
            Assert.That(actor.Mana, Is.EqualTo(manaBefore));
            Assert.That(actor.Cooldowns, Has.Count.EqualTo(cooldownCountBefore));
            Assert.That(actor.HasActed, Is.False);
            Assert.That(target.Health, Is.EqualTo(targetHealthBefore));
            Assert.That(engine.Outcome, Is.EqualTo(BattleOutcome.InProgress));
            Assert.That(Snapshot(engine.State), Is.EqualTo(before));
        }

        [Test]
        public void Simulator_MaxCommandsExceeded_Throws()
        {
            var scenario = BattleScenarioFactory.CreateCoreScenario();

            Assert.Throws<InvalidOperationException>(() =>
                BattleSimulator.RunUntilComplete(
                    scenario,
                    RandomSourceFactory.FromSeed("max-commands"),
                    maxCommands: 1));
        }

        [Test]
        public void ScenarioAndEngine_RejectNonCanonicalOrDuplicateSkills()
        {
            var scenario = BattleScenarioFactory.CreateCoreScenario();
            var mismatchedPlayerSkills = scenario.PlayerSkills.ToDictionary(
                pair => pair.Key == "skill.basic" ? "skill.basic.alias" : pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal);

            Assert.Throws<ArgumentException>(() => new BattleScenario(
                scenario.State,
                mismatchedPlayerSkills,
                scenario.EnemySkills,
                scenario.UnitSkills));

            var unknownSkillMappings = scenario.UnitSkills.ToDictionary(
                pair => pair.Key,
                pair => (string[])pair.Value.Clone(),
                StringComparer.Ordinal);
            unknownSkillMappings["player.warrior"] = new[] { "skill.missing" };
            Assert.Throws<ArgumentException>(() => new BattleScenario(
                scenario.State,
                scenario.PlayerSkills,
                scenario.EnemySkills,
                unknownSkillMappings));

            var state = new BattleState(BattleMap.CreatePlain(2, 1));
            state.AddUnit(new BattleUnit(
                "p1",
                "unit.test",
                Team.Player,
                new UnitStats(10, 10, 5, 0, 5, 0f, 0),
                new GridPosition(0, 0)));
            state.AddUnit(new BattleUnit(
                "e1",
                "unit.test",
                Team.Enemy,
                new UnitStats(10, 10, 5, 0, 1, 0f, 0),
                new GridPosition(1, 0)));
            var basic = scenario.AllSkills["skill.basic"];
            var duplicateSkills = new Dictionary<string, SkillDefinition>(StringComparer.Ordinal)
            {
                ["skill.basic"] = basic,
                ["skill.basic.duplicate"] = basic
            };
            var mappings = new Dictionary<string, string[]>
            {
                ["p1"] = new[] { "skill.basic" },
                ["e1"] = new[] { "skill.basic" }
            };

            Assert.Throws<ArgumentException>(() => new BattleEngine(
                state,
                RandomSourceFactory.FromSeed("duplicate"),
                duplicateSkills,
                mappings));
        }

        private static SimulationRecord Run(string seed)
        {
            return BattleSimulator.RunUntilComplete(
                BattleScenarioFactory.CreateCoreScenario(),
                RandomSourceFactory.FromSeed(seed),
                maxCommands: 200);
        }

        [Test]
        public void CreateScenario_WithBattleContext_BuildsRequestedEnemyFormation()
        {
            var party = PartySnapshot();
            var formations = new[]
            {
                new[] { "enemy.bandit" },
                new[] { "enemy.bandit", "enemy.bandit" },
                new[] { "enemy.ranger", "enemy.mage" },
                new[] { "enemy.wolf", "enemy.wolf", "enemy.wolf" },
                new[] { "enemy.crypt_boss" }
            };

            foreach (var enemyDefinitionIds in formations)
            {
                var context = new BattleContext(
                    "encounter.test",
                    "loot.test",
                    10,
                    10,
                    enemyDefinitionIds,
                    true);
                var scenario = BattleScenarioFactory.CreateScenario(party, context);
                var enemies = scenario.State.Units
                    .Where(unit => unit.Team == Team.Enemy)
                    .ToArray();
                var label = string.Join(",", enemyDefinitionIds);

                Assert.That(
                    enemies.Select(unit => unit.DefinitionId),
                    Is.EqualTo(enemyDefinitionIds),
                    label);
                Assert.That(enemies.Select(unit => unit.Id), Is.Unique, label);
                Assert.That(enemies.Select(unit => unit.Position), Is.Unique, label);
                Assert.That(
                    scenario.State.Units.Count(unit => unit.Team == Team.Player),
                    Is.EqualTo(party.Members.Count));
                foreach (var enemy in enemies)
                {
                    Assert.That(scenario.UnitSkills.ContainsKey(enemy.Id), Is.True, enemy.Id);
                    Assert.That(scenario.UnitSkills[enemy.Id], Is.Not.Empty, enemy.Id);
                    foreach (var skillId in scenario.UnitSkills[enemy.Id])
                        Assert.That(scenario.AllSkills.ContainsKey(skillId), Is.True, skillId);
                }
            }
        }

        [Test]
        public void CreateScenario_EnemyDefinitionsHaveDeterministicStatsAndSkills()
        {
            var party = PartySnapshot();
            var context = new BattleContext(
                "encounter.test",
                "loot.test",
                0,
                0,
                new[] { "enemy.bandit", "enemy.ranger", "enemy.mage", "enemy.wolf", "enemy.crypt_boss" },
                true);

            var scenario = BattleScenarioFactory.CreateScenario(party, context);

            AssertUnitStats(scenario, "enemy.bandit", 20, 0, 8, 3, 5, 0.05f, 1);
            AssertUnitStats(scenario, "enemy.ranger", 18, 10, 10, 3, 7, 0.15f, 2);
            AssertUnitStats(scenario, "enemy.mage", 15, 16, 11, 1, 5, 0.05f, 6);
            AssertUnitStats(scenario, "enemy.wolf", 16, 0, 9, 2, 8, 0.1f, 1);
            AssertUnitStats(scenario, "enemy.crypt_boss", 60, 24, 14, 6, 4, 0.1f, 5);

            Assert.That(
                scenario.State.GetUnit("enemy.crypt_boss").Stats.MaxHealth,
                Is.GreaterThan(scenario.State.GetUnit("enemy.bandit").Stats.MaxHealth));
            Assert.That(
                scenario.UnitSkills["enemy.crypt_boss"],
                Is.EquivalentTo(new[] { "skill.fireball", "skill.frost_nova", "skill.basic" }));
            Assert.That(
                scenario.UnitSkills["enemy.wolf"],
                Is.EquivalentTo(new[] { "skill.basic" }));
        }

        [Test]
        public void CreateScenario_WithoutContextEnemyIds_FallsBackToCoreFormation()
        {
            var party = PartySnapshot();
            var scenario = BattleScenarioFactory.CreateScenario(
                party,
                new BattleContext("encounter.test", "loot.test", 0, 0, System.Array.Empty<string>(), true));

            Assert.That(
                scenario.State.Units
                    .Where(unit => unit.Team == Team.Enemy)
                    .Select(unit => unit.DefinitionId),
                Is.EqualTo(new[] { "enemy.bandit", "enemy.ranger", "enemy.mage" }));
        }

        private static BattlePartySnapshot PartySnapshot()
        {
            var members = new[]
            {
                ("player.warrior", "class.warrior"),
                ("player.ranger", "class.ranger"),
                ("player.mage", "class.mage")
            };
            return new BattlePartySnapshot(members.Select(member => new BattleCombatantSnapshot(
                member.Item1,
                "unit." + member.Item1.Substring("player.".Length),
                member.Item2,
                20,
                8,
                8,
                2,
                4,
                500,
                1,
                20,
                8,
                new[] { "skill.basic" },
                System.Array.Empty<BattleSkillModifierSnapshot>(),
                System.Array.Empty<BattlePassiveSnapshot>())));
        }

        private static void AssertTerrainCount(BattleMap map, TerrainType terrain, int expected)
        {
            var count = 0;
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    if (map.GetTerrain(new GridPosition(x, y)) == terrain)
                        count++;
                }
            }

            Assert.That(count, Is.EqualTo(expected));
        }

        private static void AssertUnitStats(
            BattleScenario scenario,
            string unitId,
            int health,
            int mana,
            int power,
            int armor,
            int speed,
            float crit,
            int resistance)
        {
            var stats = scenario.State.GetUnit(unitId).Stats;
            Assert.That(stats.MaxHealth, Is.EqualTo(health));
            Assert.That(stats.MaxMana, Is.EqualTo(mana));
            Assert.That(stats.Power, Is.EqualTo(power));
            Assert.That(stats.Armor, Is.EqualTo(armor));
            Assert.That(stats.Speed, Is.EqualTo(speed));
            Assert.That(stats.CritChance, Is.EqualTo(crit));
            Assert.That(stats.Resistance, Is.EqualTo(resistance));
        }

        private static BattleUnit TestUnit(
            string id,
            Team team,
            int x,
            int y,
            int power = 8)
        {
            return new BattleUnit(
                id,
                "unit.test",
                team,
                new UnitStats(30, 10, power, 0, 5, 0f, 0),
                new GridPosition(x, y));
        }

        private static SkillDefinition CopyWithDifferentConfiguration(SkillDefinition source)
        {
            var effects = source.Effects.Select(effect => new SkillEffectDefinition(
                effect.Kind,
                effect.Kind == SkillEffectKind.Damage
                    ? effect.PowerMultiplier + 0.1f
                    : effect.PowerMultiplier,
                effect.StatusType,
                effect.Magnitude,
                effect.Duration,
                effect.DamageType,
                effect.ArmorPenetration)).ToArray();

            return new SkillDefinition(
                source.Id,
                source.LocalizationKey + ".copy",
                source.Targeting,
                source.Range + 1,
                source.Radius + 1,
                source.Mana,
                source.Cooldown,
                effects);
        }

        private static SkillEffectDefinition AssertEffect(
            SkillDefinition skill,
            SkillEffectKind kind,
            float powerMultiplier,
            DamageType damageType = DamageType.Physical)
        {
            var effect = skill.Effects.Single(candidate =>
                candidate.Kind == kind &&
                candidate.DamageType == damageType &&
                Math.Abs(candidate.PowerMultiplier - powerMultiplier) < 0.0001f);
            return effect;
        }

        private static void AssertMagnitudeEffect(
            SkillDefinition skill,
            SkillEffectKind kind,
            int magnitude)
        {
            Assert.That(skill.Effects.Any(effect =>
                effect.Kind == kind && effect.Magnitude == magnitude), Is.True, skill.Id);
        }

        private static void AssertStatusEffect(
            SkillDefinition skill,
            StatusType status,
            int magnitude,
            int duration)
        {
            Assert.That(skill.Effects.Any(effect =>
                effect.Kind == SkillEffectKind.ApplyStatus &&
                effect.StatusType == status &&
                effect.Magnitude == magnitude &&
                effect.Duration == duration), Is.True, skill.Id);
        }

        private static string Snapshot(BattleState state)
        {
            return $"{state.Round}|" + string.Join("|", state.Units.Select(unit =>
                $"{unit.Id}:{unit.Health}:{unit.Mana}:{unit.Position}:{unit.HasMoved}:{unit.HasActed}:" +
                string.Join(",", unit.Statuses.Select(status =>
                    $"{status.Type}:{status.Magnitude}:{status.RemainingTurns}:{status.SourceUnitId}")) + ":" +
                string.Join(",", unit.Cooldowns.OrderBy(pair => pair.Key).Select(pair =>
                    $"{pair.Key}:{pair.Value}"))));
        }
    }
}
