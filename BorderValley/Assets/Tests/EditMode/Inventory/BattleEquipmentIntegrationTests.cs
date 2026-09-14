using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Battle.Domain;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Combat;
using BorderValley.Core.Random;
using NUnit.Framework;
using BorderValley.UI.Battle;

namespace BorderValley.Inventory.Tests
{
    public sealed class BattleEquipmentIntegrationTests
    {
        [Test]
        public void CreateCoreScenario_AppliesEquipmentStatsAndPerUnitSkillModifier()
        {
            var snapshot = Snapshot(
                power: 14,
                skillModifiers: new[]
                {
                    new BattleSkillModifierSnapshot("skill.whirlwind", SkillModifierKind.Radius, 1)
                });
            var scenario = BattleScenarioFactory.CreateCoreScenario(
                new BattlePartySnapshot(new[] { snapshot }));
            var warrior = scenario.State.GetUnit("player.warrior");
            var skillId = scenario.GetSkillsForUnit("player.warrior").Keys
                .Single(id => id.StartsWith("skill.whirlwind", StringComparison.Ordinal));

            Assert.That(warrior.Stats.Power, Is.EqualTo(14));
            Assert.That(scenario.GetSkillsForUnit("player.warrior")[skillId].Radius, Is.EqualTo(2));
        }

        [Test]
        public void CreateCoreScenario_PreservesSnapshotResourcesAndPassives()
        {
            var snapshot = Snapshot(
                power: 14,
                currentHealth: 7,
                currentMana: 3,
                passives: new[]
                {
                    new BattlePassiveSnapshot(PassiveEffectKind.OnKillHeal, 2, 0)
                });
            var scenario = BattleScenarioFactory.CreateCoreScenario(
                new BattlePartySnapshot(new[] { snapshot }));
            var warrior = scenario.State.GetUnit("player.warrior");

            Assert.That(warrior.Health, Is.EqualTo(7));
            Assert.That(warrior.Mana, Is.EqualTo(3));
            Assert.That(warrior.Passives.Single().Kind, Is.EqualTo(PassiveEffectKind.OnKillHeal));
        }

        [Test]
        public void LowHealthArmorPassive_ReducesPhysicalDamage()
        {
            var attacker = Unit("a", 10, 0, passives: Array.Empty<BattlePassiveSnapshot>());
            var defender = Unit("d", 0, 2, new[]
            {
                new BattlePassiveSnapshot(PassiveEffectKind.LowHealthArmor, 5, 0)
            });
            defender.ApplyRawDamage(defender.Stats.MaxHealth - 1);
            var result = DamageCalculator.Calculate(
                new DamageRequest(attacker, defender, 1f, DamageType.Physical, false), Map(), Random());
            Assert.That(result.Damage, Is.EqualTo(3));
        }

        [Test]
        public void HighGroundDamagePassive_MultipliesAfterHighGroundBonus()
        {
            var map = new BattleMap(2, 1, new[] { TerrainType.HighGround, TerrainType.Plain });
            var attacker = Unit(
                "a",
                10,
                0,
                new[] { new BattlePassiveSnapshot(PassiveEffectKind.HighGroundDamage, 20, 0) },
                maxHealth: 10,
                position: new GridPosition(0, 0));
            var defender = Unit(
                "d",
                0,
                0,
                Array.Empty<BattlePassiveSnapshot>(),
                maxHealth: 20,
                position: new GridPosition(1, 0));
            var result = DamageCalculator.Calculate(
                new DamageRequest(attacker, defender, 1f, DamageType.Physical, false), map, Random());

            Assert.That(result.Damage, Is.EqualTo(16));
            Assert.That(result.HighGroundBonus, Is.True);
        }

        [Test]
        public void OnHitGainShield_AddsShieldedStatusToDamagedTarget()
        {
            var attacker = Unit("a", 4, 0);
            var defender = Unit(
                "d",
                0,
                0,
                new[] { new BattlePassiveSnapshot(PassiveEffectKind.OnHitGainShield, 3, 2) },
                maxHealth: 10,
                position: new GridPosition(1, 0));
            var result = DamageCalculator.Calculate(
                new DamageRequest(attacker, defender, 1f, DamageType.Physical, false), Map(), Random());

            Assert.That(result.Damage, Is.EqualTo(4));
            Assert.That(StatusSystem.GetShield(defender), Is.EqualTo(3));
            Assert.That(defender.Statuses.Single(status => status.Type == StatusType.Shielded).RemainingTurns, Is.EqualTo(2));
        }

        [Test]
        public void OnAttackSlow_AddsSlowedStatusToDamagedTarget()
        {
            var attacker = Unit("a", 0, 0, new[]
            {
                new BattlePassiveSnapshot(PassiveEffectKind.OnAttackApplySlow, 1, 2)
            });
            var defender = Unit(
                "d",
                0,
                1,
                Array.Empty<BattlePassiveSnapshot>(),
                maxHealth: 4,
                team: Team.Enemy,
                position: new GridPosition(1, 0));
            var skill = new SkillDefinition("skill.hit", "skill.hit.name", SkillTargeting.Enemy, 1, 0, 0, 0,
                new SkillEffectDefinition(SkillEffectKind.Damage, 1f, default, 0, 0));
            SkillExecutor.Execute(Map(attacker, defender), attacker, skill, defender, Random());
            Assert.That(defender.Statuses.Any(status => status.Type == StatusType.Slowed), Is.True);
        }

        [Test]
        public void OnKillHeal_HealsAttackerWhenDamageKillsTarget()
        {
            var attacker = Unit(
                "a",
                5,
                0,
                new[] { new BattlePassiveSnapshot(PassiveEffectKind.OnKillHeal, 3, 0) },
                maxHealth: 10,
                position: new GridPosition(0, 0));
            attacker.ApplyRawDamage(5);
            var defender = Unit(
                "d",
                0,
                0,
                Array.Empty<BattlePassiveSnapshot>(),
                maxHealth: 1,
                team: Team.Enemy,
                position: new GridPosition(1, 0));
            var skill = new SkillDefinition("skill.hit", "skill.hit.name", SkillTargeting.Enemy, 1, 0, 0, 0,
                new SkillEffectDefinition(SkillEffectKind.Damage, 1f, default, 0, 0));

            SkillExecutor.Execute(Map(attacker, defender), attacker, skill, defender, Random());

            Assert.That(defender.IsAlive, Is.False);
            Assert.That(attacker.Health, Is.EqualTo(8));
        }

        [Test]
        public void SkillModifierApplier_ModifiesAllSupportedKindsWithoutMutation()
        {
            var skill = new SkillDefinition(
                "skill.hit",
                "skill.hit.name",
                SkillTargeting.Enemy,
                1,
                1,
                4,
                3,
                new SkillEffectDefinition(SkillEffectKind.Damage, 1f, default, 0, 0),
                new SkillEffectDefinition(SkillEffectKind.ApplyStatus, 0f, StatusType.Slowed, 1, 2));

            var range = SkillModifierApplier.Apply(skill, SkillModifierKind.Range, 2);
            var radius = SkillModifierApplier.Apply(skill, SkillModifierKind.Radius, 2);
            var mana = SkillModifierApplier.Apply(skill, SkillModifierKind.ManaCost, 2);
            var cooldown = SkillModifierApplier.Apply(skill, SkillModifierKind.Cooldown, 2);
            var power = SkillModifierApplier.Apply(skill, SkillModifierKind.PowerMultiplierBps, 2500);

            Assert.That(range.Range, Is.EqualTo(3));
            Assert.That(radius.Radius, Is.EqualTo(3));
            Assert.That(mana.Mana, Is.EqualTo(6));
            Assert.That(cooldown.Cooldown, Is.EqualTo(5));
            Assert.That(power.Effects[0].PowerMultiplier, Is.EqualTo(1.25f));
            Assert.That(power.Effects[1].PowerMultiplier, Is.EqualTo(0f));
            Assert.That(skill.Range, Is.EqualTo(1));
            Assert.That(skill.Radius, Is.EqualTo(1));
            Assert.That(skill.Mana, Is.EqualTo(4));
            Assert.That(skill.Cooldown, Is.EqualTo(3));
            Assert.That(skill.Effects[0].PowerMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void BattleUiPresenter_TreatsPerUnitBasicSkillIdAsBasicAttack()
        {
            var attacker = new BattleUnit(
                "player",
                "unit.test",
                Team.Player,
                new UnitStats(10, 10, 5, 0, 10, 0f, 0),
                new GridPosition(0, 0));
            var defender = Unit(
                "enemy",
                0,
                0,
                Array.Empty<BattlePassiveSnapshot>(),
                maxHealth: 10,
                team: Team.Enemy,
                position: new GridPosition(1, 0));
            var skill = new SkillDefinition(
                "skill.basic@player",
                "skill.basic.name",
                SkillTargeting.Enemy,
                1,
                0,
                0,
                0,
                new SkillEffectDefinition(SkillEffectKind.Damage, 1f, default, 0, 0));
            var scenario = new BattleScenario(
                Map(attacker, defender),
                new Dictionary<string, SkillDefinition> { [skill.Id] = skill },
                new Dictionary<string, SkillDefinition>(),
                new Dictionary<string, string[]>
                {
                    ["player"] = new[] { skill.Id },
                    ["enemy"] = Array.Empty<string>()
                });
            var presenter = new BattleUiPresenter(scenario, Random());
            presenter.Start();

            var result = presenter.TapCell(defender.Position);

            Assert.That(result.Success, Is.True);
            Assert.That(defender.Health, Is.EqualTo(5));
            Assert.That(attacker.HasActed, Is.True);
        }
        private static BattleCombatantSnapshot Snapshot(
            int power,
            int maxHealth = 20,
            int maxMana = 10,
            int armor = 0,
            int speed = 5,
            int critChanceBps = 0,
            int resistance = 0,
            int currentHealth = 20,
            int currentMana = 10,
            IEnumerable<string> skillIds = null,
            IEnumerable<BattleSkillModifierSnapshot> skillModifiers = null,
            IEnumerable<BattlePassiveSnapshot> passives = null)
        {
            return new BattleCombatantSnapshot(
                "player.warrior",
                "unit.warrior",
                "class.warrior",
                maxHealth,
                maxMana,
                power,
                armor,
                speed,
                critChanceBps,
                resistance,
                currentHealth,
                currentMana,
                skillIds ?? new[] { "skill.whirlwind", "skill.basic" },
                skillModifiers ?? Array.Empty<BattleSkillModifierSnapshot>(),
                passives ?? Array.Empty<BattlePassiveSnapshot>());
        }

        private static BattleUnit Unit(
            string id,
            int power,
            int armor,
            IEnumerable<BattlePassiveSnapshot> passives = null,
            int maxHealth = 4,
            Team team = Team.Player,
            GridPosition position = default)
        {
            return new BattleUnit(
                id,
                "unit.test",
                team,
                new UnitStats(maxHealth, 10, power, armor, 5, 0f, 0),
                position,
                passives);
        }

        private static BattleMap Map() => BattleMap.CreatePlain(2, 1);

        private static BattleState Map(BattleUnit attacker, BattleUnit defender)
        {
            var state = new BattleState(Map());
            state.AddUnit(attacker);
            state.AddUnit(defender);
            return state;
        }

        private static IRandomSource Random() => RandomSourceFactory.FromSeed("battle-equipment-integration");
    }
}
