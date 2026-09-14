using BorderValley.Core.Combat;
using System;
using System.Linq;

namespace BorderValley.Battle.Domain
{
    public static class BattlePassiveRules
    {
        public static int GetArmorBonus(BattleUnit unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if ((long)unit.Health * 100 > (long)unit.Stats.MaxHealth * 30) return 0;

            return unit.Passives
                .Where(passive => passive.Kind == PassiveEffectKind.LowHealthArmor)
                .Sum(passive => passive.Magnitude);
        }

        public static float GetDamageMultiplier(BattleUnit attacker, BattleMap map)
        {
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (map.GetTerrain(attacker.Position) != TerrainType.HighGround) return 1f;

            var magnitude = attacker.Passives
                .Where(passive => passive.Kind == PassiveEffectKind.HighGroundDamage)
                .Sum(passive => passive.Magnitude);
            return 1f + magnitude / 100f;
        }

        public static void ApplyOnHit(BattleUnit attacker, BattleUnit defender, int damage)
        {
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (defender == null) throw new ArgumentNullException(nameof(defender));
            if (damage <= 0) return;

            foreach (var passive in defender.Passives.Where(passive => passive.Kind == PassiveEffectKind.OnHitGainShield))
            {
                StatusSystem.Apply(
                    defender,
                    StatusType.Shielded,
                    passive.Magnitude,
                    passive.Duration,
                    attacker.Id);
            }
        }

        public static void ApplyOnAttack(BattleUnit attacker, BattleUnit defender)
        {
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (defender == null) throw new ArgumentNullException(nameof(defender));

            foreach (var passive in attacker.Passives.Where(passive => passive.Kind == PassiveEffectKind.OnAttackApplySlow))
            {
                StatusSystem.Apply(
                    defender,
                    StatusType.Slowed,
                    passive.Magnitude,
                    passive.Duration,
                    attacker.Id);
            }
        }

        public static void ApplyOnKill(BattleUnit attacker, BattleUnit defender)
        {
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (defender == null) throw new ArgumentNullException(nameof(defender));
            if (defender.IsAlive) return;

            var healing = attacker.Passives
                .Where(passive => passive.Kind == PassiveEffectKind.OnKillHeal)
                .Sum(passive => passive.Magnitude);
            attacker.Heal(healing);
        }
    }
}
