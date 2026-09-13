using BorderValley.Battle.Domain;
using BorderValley.Core.Random;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class DamageCalculatorTests
    {
        [Test]
        public void PhysicalDamage_SubtractsArmorWithMinimumOne()
        {
            var attacker = Unit("a", Team.Player, power: 10, armor: 0, resistance: 0);
            var defender = Unit("b", Team.Enemy, power: 1, armor: 3, resistance: 0);
            var request = new DamageRequest(attacker, defender, 1f, DamageType.Physical, true);

            var result = DamageCalculator.Calculate(
                request, BattleMap.CreatePlain(2, 1), RandomSourceFactory.FromSeed("fixed"));

            Assert.That(result.Damage, Is.EqualTo(7));
        }

        [Test]
        public void MagicalDamage_SubtractsResistance()
        {
            var attacker = Unit("a", Team.Player, power: 10, armor: 0, resistance: 0);
            var defender = Unit("b", Team.Enemy, power: 1, armor: 9, resistance: 4);
            var request = new DamageRequest(attacker, defender, 1f, DamageType.Magical, false);

            var result = DamageCalculator.Calculate(
                request, BattleMap.CreatePlain(2, 1), RandomSourceFactory.FromSeed("fixed"));

            Assert.That(result.Damage, Is.EqualTo(6));
        }

        [Test]
        public void HighGround_AddsTwentyFivePercentAfterArmor()
        {
            var map = new BattleMap(2, 1, new[] { TerrainType.HighGround, TerrainType.Plain });
            var attacker = UnitAt("a", Team.Player, 10, 0, 0, 0);
            var defender = UnitAt("b", Team.Enemy, 1, 1, 3, 0);
            var request = new DamageRequest(attacker, defender, 1f, DamageType.Physical, false);

            var result = DamageCalculator.Calculate(request, map, RandomSourceFactory.FromSeed("fixed"));

            Assert.That(result.Damage, Is.EqualTo(9));
            Assert.That(result.HighGroundBonus, Is.True);
        }

        [Test]
        public void CriticalDamage_MultipliesAfterTerrain()
        {
            var map = new BattleMap(2, 1, new[] { TerrainType.HighGround, TerrainType.Plain });
            var attacker = UnitAt("a", Team.Player, 10, 0, 0, 0, critChance: 1f);
            var defender = UnitAt("b", Team.Enemy, 1, 1, 3, 0);
            var request = new DamageRequest(attacker, defender, 1f, DamageType.Physical, true);

            var result = DamageCalculator.Calculate(request, map, RandomSourceFactory.FromSeed("fixed"));

            Assert.That(result.Damage, Is.EqualTo(14));
            Assert.That(result.Critical, Is.True);
            Assert.That(result.HighGroundBonus, Is.True);
        }

        [Test]
        public void DamageBelowDefense_StillDealsOne()
        {
            var attacker = Unit("a", Team.Player, power: 1, armor: 0, resistance: 0);
            var defender = Unit("b", Team.Enemy, power: 1, armor: 99, resistance: 0);
            var request = new DamageRequest(attacker, defender, 1f, DamageType.Physical, false);

            var result = DamageCalculator.Calculate(
                request, BattleMap.CreatePlain(2, 1), RandomSourceFactory.FromSeed("fixed"));

            Assert.That(result.Damage, Is.EqualTo(1));
        }

        [Test]
        public void NegativePowerMultiplier_IsClampedToZero()
        {
            var request = new DamageRequest(
                Unit("a", Team.Player, power: 10, armor: 0, resistance: 0),
                Unit("b", Team.Enemy, power: 1, armor: 0, resistance: 0),
                -1f,
                DamageType.Physical,
                false);

            Assert.That(request.PowerMultiplier, Is.Zero);
        }

        [Test]
        public void Shield_AbsorbsDamageBeforeHealth()
        {
            var target = Unit("b", Team.Enemy, 1, 0, 0);
            StatusSystem.Apply(target, StatusType.Shielded, 4, 2, "a");

            var absorbed = StatusSystem.ConsumeShield(target, 3);

            Assert.That(absorbed, Is.EqualTo(3));
            Assert.That(target.Health, Is.EqualTo(target.Stats.MaxHealth));
            Assert.That(StatusSystem.GetShield(target), Is.EqualTo(1));
        }

        [Test]
        public void Calculation_ConsumesShieldAndReportsHealthDamage()
        {
            var attacker = Unit("a", Team.Player, power: 4, armor: 0, resistance: 0);
            var defender = Unit("b", Team.Enemy, power: 1, armor: 0, resistance: 0);
            StatusSystem.Apply(defender, StatusType.Shielded, 3, 2, "a");
            var request = new DamageRequest(attacker, defender, 1f, DamageType.Physical, false);

            var result = DamageCalculator.Calculate(
                request, BattleMap.CreatePlain(2, 1), RandomSourceFactory.FromSeed("fixed"));

            Assert.That(result.Damage, Is.EqualTo(1));
            Assert.That(result.ShieldAbsorbed, Is.EqualTo(3));
            Assert.That(defender.Health, Is.EqualTo(defender.Stats.MaxHealth - 1));
        }

        private static BattleUnit Unit(string id, Team team, int power, int armor, int resistance) =>
            UnitAt(id, team, power, 0, armor, resistance);

        private static BattleUnit UnitAt(
            string id,
            Team team,
            int power,
            int x,
            int armor,
            int resistance,
            float critChance = 0f) =>
            new BattleUnit(id, "test.unit", team,
                new UnitStats(20, 0, power, armor, 5, critChance, resistance), new GridPosition(x, 0));
    }
}
