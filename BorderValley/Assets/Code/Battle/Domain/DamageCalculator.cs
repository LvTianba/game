using System;
using BorderValley.Core.Random;

namespace BorderValley.Battle.Domain
{
    public static class DamageCalculator
    {
        public static DamageResult Calculate(DamageRequest request, BattleMap map, IRandomSource random)
        {
            var raw = Math.Max(1, (int)MathF.Round(request.Attacker.Stats.Power * request.PowerMultiplier));
            var defense = request.DamageType == DamageType.Physical
                ? Math.Max(0, request.Defender.Stats.Armor - request.ArmorPenetration)
                : request.Defender.Stats.Resistance;
            var damage = Math.Max(1, raw - defense);

            var highGround = map.GetTerrain(request.Attacker.Position) == TerrainType.HighGround;
            if (highGround) damage = (int)MathF.Ceiling(damage * 1.25f);

            var critical = request.CanCrit && random.Value01() < request.Attacker.Stats.CritChance;
            if (critical) damage = (int)MathF.Ceiling(damage * 1.5f);

            var absorbed = StatusSystem.ConsumeShield(request.Defender, damage);
            var healthDamage = Math.Max(0, damage - absorbed);
            request.Defender.ApplyRawDamage(healthDamage);
            return new DamageResult(healthDamage, critical, highGround, absorbed);
        }
    }
}
