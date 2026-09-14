using System;
using System.Linq;
using BorderValley.Core.BattleFlow;

using System.Collections.Generic;

namespace BorderValley.Battle.Domain
{
    public static class BattleScenarioFactory
    {
        public static BattleScenario CreateCoreScenario()
        {
            var skills = CreateSkills();
            var playerSkills = new Dictionary<string, SkillDefinition>(skills, StringComparer.Ordinal);
            var enemySkills = new Dictionary<string, SkillDefinition>(skills, StringComparer.Ordinal);
            var state = CreateState();
            var unitSkills = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["player.warrior"] = new[]
                {
                    "skill.shield_bash",
                    "skill.whirlwind",
                    "skill.iron_guard",
                    "skill.taunt",
                    "skill.basic"
                },
                ["player.ranger"] = new[]
                {
                    "skill.piercing_shot",
                    "skill.snare",
                    "skill.twin_shot",
                    "skill.basic"
                },
                ["player.mage"] = new[]
                {
                    "skill.fireball",
                    "skill.frost_nova",
                    "skill.arcane_ward",
                    "skill.basic"
                },
                ["enemy.bandit"] = new[] { "skill.basic" },
                ["enemy.ranger"] = new[]
                {
                    "skill.piercing_shot",
                    "skill.snare",
                    "skill.twin_shot",
                    "skill.basic"
                },
                ["enemy.mage"] = new[]
                {
                    "skill.fireball",
                    "skill.frost_nova",
                    "skill.arcane_ward",
                    "skill.basic"
                }
            };

            return new BattleScenario(state, playerSkills, enemySkills, unitSkills);
        }

        public static BattleScenario CreateCoreScenario(BattlePartySnapshot partySnapshot)
        {
            if (partySnapshot == null) return CreateCoreScenario();

            var skills = CreateSkills();
            var playerSkills = new Dictionary<string, SkillDefinition>(skills, StringComparer.Ordinal);
            var enemySkills = new Dictionary<string, SkillDefinition>(skills, StringComparer.Ordinal);
            var state = CreateSnapshotState(partySnapshot);
            var unitSkills = CreateEnemyUnitSkills();

            foreach (var member in partySnapshot.Members)
            {
                var unitSkillIds = new List<string>();
                foreach (var baseSkillId in member.SkillIds.Distinct(StringComparer.Ordinal))
                {
                    if (!playerSkills.TryGetValue(baseSkillId, out var baseSkill))
                        throw new ArgumentException("Snapshot skill is not present in the core catalog: " + baseSkillId);

                    var modified = member.SkillModifiers
                        .Where(modifier => modifier.SkillId == baseSkillId)
                        .Aggregate(baseSkill, (current, modifier) =>
                            SkillModifierApplier.Apply(current, modifier.Kind, modifier.Value));

                    if (baseSkillId == "skill.basic" && ReferenceEquals(baseSkill, modified))
                    {
                        unitSkillIds.Add(baseSkillId);
                        continue;
                    }

                    var unitSkillId = baseSkillId + "@" + member.UnitId;
                    playerSkills[unitSkillId] = Rename(modified, unitSkillId);
                    unitSkillIds.Add(unitSkillId);
                }

                unitSkills[member.UnitId] = unitSkillIds.ToArray();
            }

            return new BattleScenario(state, playerSkills, enemySkills, unitSkills);
        }

        private static BattleState CreateSnapshotState(BattlePartySnapshot partySnapshot)
        {
            const int width = 8;
            const int height = 6;
            var cells = new TerrainType[width * height];
            Array.Fill(cells, TerrainType.Plain);
            cells[2 * width + 3] = TerrainType.HighGround;
            cells[3 * width + 4] = TerrainType.HighGround;
            cells[3 * width + 3] = TerrainType.Mud;
            cells[2 * width + 4] = TerrainType.Mud;
            var state = new BattleState(new BattleMap(width, height, cells));

            foreach (var member in partySnapshot.Members)
                AddSnapshotPlayer(state, member);

            state.AddUnit(CreateUnit(
                "enemy.bandit",
                "enemy.bandit",
                Team.Enemy,
                20,
                0,
                8,
                3,
                5,
                0.05f,
                1,
                new GridPosition(5, 2)));
            state.AddUnit(CreateUnit(
                "enemy.ranger",
                "enemy.ranger",
                Team.Enemy,
                18,
                10,
                10,
                3,
                7,
                0.15f,
                2,
                new GridPosition(6, 2)));
            state.AddUnit(CreateUnit(
                "enemy.mage",
                "enemy.mage",
                Team.Enemy,
                15,
                16,
                11,
                1,
                5,
                0.05f,
                6,
                new GridPosition(6, 3)));

            return state;
        }

        private static void AddSnapshotPlayer(BattleState state, BattleCombatantSnapshot member)
        {
            var position = PlayerPosition(member.ClassId);
            if (state.OccupiedPositions.Contains(position))
                throw new ArgumentException("Party snapshot contains overlapping player positions.", nameof(member));

            var unit = new BattleUnit(
                member.UnitId,
                member.DefinitionId,
                Team.Player,
                new UnitStats(
                    member.MaxHealth,
                    member.MaxMana,
                    member.Power,
                    member.Armor,
                    member.Speed,
                    member.CritChanceBps / 10000f,
                    member.Resistance),
                position,
                member.Passives);
            unit.SetCurrentResources(member.CurrentHealth, member.CurrentMana);
            state.AddUnit(unit);
        }

        private static GridPosition PlayerPosition(string classId)
        {
            return classId switch
            {
                "class.warrior" => new GridPosition(2, 2),
                "class.ranger" => new GridPosition(1, 2),
                "class.mage" => new GridPosition(1, 3),
                _ => new GridPosition(2, 3)
            };
        }

        private static SkillDefinition Rename(SkillDefinition skill, string id) =>
            new(
                id,
                skill.LocalizationKey,
                skill.Targeting,
                skill.Range,
                skill.Radius,
                skill.Mana,
                skill.Cooldown,
                skill.Effects.ToArray());

        private static Dictionary<string, string[]> CreateEnemyUnitSkills() =>
            new(StringComparer.Ordinal)
            {
                ["enemy.bandit"] = new[] { "skill.basic" },
                ["enemy.ranger"] = new[]
                {
                    "skill.piercing_shot",
                    "skill.snare",
                    "skill.twin_shot",
                    "skill.basic"
                },
                ["enemy.mage"] = new[]
                {
                    "skill.fireball",
                    "skill.frost_nova",
                    "skill.arcane_ward",
                    "skill.basic"
                }
            };
        private static BattleState CreateState()
        {
            const int width = 8;
            const int height = 6;
            var cells = new TerrainType[width * height];
            Array.Fill(cells, TerrainType.Plain);
            cells[2 * width + 3] = TerrainType.HighGround;
            cells[3 * width + 4] = TerrainType.HighGround;
            cells[3 * width + 3] = TerrainType.Mud;
            cells[2 * width + 4] = TerrainType.Mud;
            var state = new BattleState(new BattleMap(width, height, cells));

            state.AddUnit(CreateUnit(
                "player.warrior",
                "unit.warrior",
                Team.Player,
                26,
                8,
                9,
                6,
                4,
                0.05f,
                2,
                new GridPosition(2, 2)));
            state.AddUnit(CreateUnit(
                "player.ranger",
                "unit.ranger",
                Team.Player,
                18,
                10,
                10,
                3,
                7,
                0.15f,
                2,
                new GridPosition(1, 2)));
            state.AddUnit(CreateUnit(
                "player.mage",
                "unit.mage",
                Team.Player,
                15,
                16,
                11,
                1,
                5,
                0.05f,
                6,
                new GridPosition(1, 3)));

            state.AddUnit(CreateUnit(
                "enemy.bandit",
                "enemy.bandit",
                Team.Enemy,
                20,
                0,
                8,
                3,
                5,
                0.05f,
                1,
                new GridPosition(5, 2)));
            state.AddUnit(CreateUnit(
                "enemy.ranger",
                "enemy.ranger",
                Team.Enemy,
                18,
                10,
                10,
                3,
                7,
                0.15f,
                2,
                new GridPosition(6, 2)));
            state.AddUnit(CreateUnit(
                "enemy.mage",
                "enemy.mage",
                Team.Enemy,
                15,
                16,
                11,
                1,
                5,
                0.05f,
                6,
                new GridPosition(6, 3)));

            return state;
        }

        private static BattleUnit CreateUnit(
            string id,
            string definitionId,
            Team team,
            int health,
            int mana,
            int power,
            int armor,
            int speed,
            float critChance,
            int resistance,
            GridPosition position)
        {
            return new BattleUnit(
                id,
                definitionId,
                team,
                new UnitStats(
                    health,
                    mana,
                    power,
                    armor,
                    speed,
                    critChance,
                    resistance),
                position);
        }

        private static Dictionary<string, SkillDefinition> CreateSkills()
        {
            return new Dictionary<string, SkillDefinition>(StringComparer.Ordinal)
            {
                ["skill.shield_bash"] = Skill(
                    "skill.shield_bash",
                    SkillTargeting.Enemy,
                    1,
                    0,
                    2,
                    Damage(1f),
                    Status(StatusType.Stunned, 1, 1)),
                ["skill.whirlwind"] = Skill(
                    "skill.whirlwind",
                    SkillTargeting.Ground,
                    0,
                    1,
                    4,
                    Damage(0.8f),
                    MagnitudeEffect(SkillEffectKind.Push, 1)),
                ["skill.iron_guard"] = Skill(
                    "skill.iron_guard",
                    SkillTargeting.Self,
                    0,
                    0,
                    3,
                    Status(StatusType.Shielded, 6, 2)),
                ["skill.taunt"] = SkillWithCooldown(
                    "skill.taunt",
                    SkillTargeting.Enemy,
                    1,
                    0,
                    2,
                    2,
                    Status(StatusType.Taunted, 1, 2)),
                ["skill.piercing_shot"] = Skill(
                    "skill.piercing_shot",
                    SkillTargeting.Enemy,
                    4,
                    0,
                    2,
                    Damage(1.2f, armorPenetration: 2)),
                ["skill.snare"] = Skill(
                    "skill.snare",
                    SkillTargeting.Enemy,
                    3,
                    0,
                    2,
                    Damage(0.6f),
                    Status(StatusType.Slowed, 1, 2)),
                ["skill.twin_shot"] = Skill(
                    "skill.twin_shot",
                    SkillTargeting.Enemy,
                    3,
                    0,
                    3,
                    Damage(1f),
                    Status(StatusType.Poisoned, 2, 2)),
                ["skill.fireball"] = Skill(
                    "skill.fireball",
                    SkillTargeting.Enemy,
                    4,
                    1,
                    4,
                    Damage(1.3f, DamageType.Magical),
                    Status(StatusType.Burning, 2, 2)),
                ["skill.frost_nova"] = Skill(
                    "skill.frost_nova",
                    SkillTargeting.Self,
                    0,
                    2,
                    4,
                    Damage(0.7f, DamageType.Magical),
                    Status(StatusType.Slowed, 2, 2)),
                ["skill.arcane_ward"] = Skill(
                    "skill.arcane_ward",
                    SkillTargeting.Ally,
                    3,
                    0,
                    3,
                    MagnitudeEffect(SkillEffectKind.Heal, 6),
                    Status(StatusType.Shielded, 4, 2)),
                ["skill.basic"] = Skill(
                    "skill.basic",
                    SkillTargeting.Enemy,
                    1,
                    0,
                    0,
                    Damage(1f))
            };
        }

        private static SkillDefinition Skill(
            string id,
            SkillTargeting targeting,
            int range,
            int radius,
            int mana,
            params SkillEffectDefinition[] effects)
        {
            return new SkillDefinition(
                id,
                id + ".name",
                targeting,
                range,
                radius,
                mana,
                0,
                effects);
        }

        private static SkillDefinition SkillWithCooldown(
            string id,
            SkillTargeting targeting,
            int range,
            int radius,
            int mana,
            int cooldown,
            params SkillEffectDefinition[] effects)
        {
            return new SkillDefinition(
                id,
                id + ".name",
                targeting,
                range,
                radius,
                mana,
                cooldown,
                effects);
        }

        private static SkillEffectDefinition Damage(
            float powerMultiplier,
            DamageType damageType = DamageType.Physical,
            int armorPenetration = 0)
        {
            return new SkillEffectDefinition(
                SkillEffectKind.Damage,
                powerMultiplier,
                default,
                0,
                0,
                damageType,
                armorPenetration);
        }

        private static SkillEffectDefinition Status(
            StatusType status,
            int magnitude,
            int duration)
        {
            return new SkillEffectDefinition(
                SkillEffectKind.ApplyStatus,
                0f,
                status,
                magnitude,
                duration);
        }

        private static SkillEffectDefinition MagnitudeEffect(
            SkillEffectKind kind,
            int magnitude)
        {
            return new SkillEffectDefinition(kind, 0f, default, magnitude, 0);
        }
    }
}
