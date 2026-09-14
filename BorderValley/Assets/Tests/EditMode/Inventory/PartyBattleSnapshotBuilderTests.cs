using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Combat;
using BorderValley.Data.Items;
using NUnit.Framework;
using UnityEngine;

namespace BorderValley.Inventory.Tests
{
    public sealed class PartyBattleSnapshotBuilderTests
    {
        [Test]
        public void Build_AppliesEquipmentAndSkillModifier()
        {
            var inventory = InventoryWithSwordAndBoots();
            var progression = Progression();
            var builder = new PartyBattleSnapshotBuilder(progression, inventory, Items(), Affixes());
            var snapshot = builder.Build(new BattleRequest("core", "seed", "World")).PartySnapshot;
            var warrior = snapshot.Members.Single(member => member.ClassId == "class.warrior");
            Assert.That(warrior.Power, Is.GreaterThan(9));
            Assert.That(warrior.SkillModifiers.Single().Kind, Is.EqualTo(SkillModifierKind.Radius));
            Assert.That(warrior.SkillModifiers.Single().SkillId, Is.EqualTo("skill.whirlwind"));
        }

        [Test]
        public void Build_SkillModifierWithoutTarget_FailsLoudly()
        {
            var builder = new PartyBattleSnapshotBuilder(Progression(), InventoryWithSwordAndBoots(), Items(), Affixes(targetSkillId: null));
            Assert.Throws<InvalidOperationException>(() => builder.Build(new BattleRequest("core", "seed", "World")));
        }

        [Test]
        public void Build_AppliesFlatPercentStatsAndPassives()
        {
            var inventory = InventoryWithSwordAndBoots();
            var progression = Progression();
            var builder = new PartyBattleSnapshotBuilder(progression, inventory, Items(), Affixes());
            var snapshot = builder.Build(new BattleRequest("core", "seed", "World")).PartySnapshot;
            var warrior = snapshot.Members.Single(member => member.ClassId == "class.warrior");
            Assert.That(warrior.Power, Is.EqualTo(27));
            Assert.That(warrior.Passives.Single().Kind, Is.EqualTo(PassiveEffectKind.OnAttackApplySlow));
            Assert.That(warrior.Passives.Single().Magnitude, Is.EqualTo(1));
            Assert.That(warrior.Passives.Single().Duration, Is.EqualTo(2));
        }

        [Test]
        public void Build_AddsUnlockedSkillsAndSkillRankRangeModifier()
        {
            var definitions = Characters();
            var progression = new PartyProgressionService(
                definitions,
                new[] { new PartyMemberState("player.warrior", "class.warrior", 2, 0, 0, 20, 10) });
            progression.Members.Single().SkillRanks["skill.whirlwind"] = 2;
            var inventory = new InventoryService(1, Items(), 0);
            var builder = new PartyBattleSnapshotBuilder(progression, inventory, Items(), Affixes());
            var warrior = builder.Build(new BattleRequest("core", "seed", "World")).PartySnapshot.Members.Single();
            var modifier = warrior.SkillModifiers.Single(value => value.SkillId == "skill.whirlwind");
            Assert.That(warrior.SkillIds, Does.Contain("skill.whirlwind"));
            Assert.That(modifier.Kind, Is.EqualTo(SkillModifierKind.Range));
            Assert.That(modifier.Value, Is.EqualTo(1));
        }

        [Test]
        public void Build_AppliesEquipmentOnlyToTheOwningMember()
        {
            var inventory = new InventoryService(4, Items(), 0);
            inventory.TryAdd(
                new ItemInstance("i.sword", "item.sword", 1, ItemRarity.Fine, Array.Empty<AffixInstance>()),
                out _);
            inventory.TryAdd(
                new ItemInstance("i.boots", "item.boots", 1, ItemRarity.Fine, Array.Empty<AffixInstance>()),
                out _);
            inventory.TryEquip("i.sword", "player.warrior", "class.warrior", out _);
            inventory.TryEquip("i.boots", "player.ranger", "class.ranger", out _);

            var builder = new PartyBattleSnapshotBuilder(Progression(), inventory, Items(), Affixes());
            var snapshot = builder.Build(new BattleRequest("core", "seed", "World")).PartySnapshot;
            var warrior = snapshot.Members.Single(member => member.UnitId == "player.warrior");
            var ranger = snapshot.Members.Single(member => member.UnitId == "player.ranger");

            Assert.That(warrior.Power, Is.EqualTo(14));
            Assert.That(warrior.Speed, Is.Zero);
            Assert.That(ranger.Power, Is.EqualTo(7));
            Assert.That(ranger.Speed, Is.EqualTo(3));
        }

        [Test]
        public void BattleResult_CopiesUnitStatesAndKeepsLegacyConstructor()
        {
            var states = new List<BattleUnitResult> { new("player.warrior", 7, 2) };
            var defeated = new List<string> { "enemy.bandit", "enemy.bandit" };
            var result = new BattleResult(BattleFlowOutcome.PlayerVictory, 3, states);
            var contextful = new BattleResult(
                BattleFlowOutcome.PlayerVictory,
                3,
                states,
                null,
                defeated);
            var legacy = new BattleResult(BattleFlowOutcome.EnemyVictory, 1);
            states.Clear();
            defeated.Clear();
            Assert.That(result.UnitStates.Single().UnitId, Is.EqualTo("player.warrior"));
            Assert.That(result.UnitStates.Single().Health, Is.EqualTo(7));
            Assert.That(result.UnitStates.Single().Mana, Is.EqualTo(2));
            Assert.That(result.DefeatedEnemyIds, Is.Empty);
            Assert.That(contextful.DefeatedEnemyIds, Is.EqualTo(new[] { "enemy.bandit", "enemy.bandit" }));
            Assert.That(legacy.UnitStates, Is.Empty);
            Assert.That(legacy.DefeatedEnemyIds, Is.Empty);
        }

        private static InventoryService InventoryWithSwordAndBoots()
        {
            var inventory = new InventoryService(4, Items(), 0);
            inventory.TryAdd(
                new ItemInstance("i.sword", "item.sword", 1, ItemRarity.Fine, Array.Empty<AffixInstance>()),
                out _);
            inventory.TryAdd(
                new ItemInstance(
                    "i.boots",
                    "item.boots",
                    1,
                    ItemRarity.Fine,
                    new[]
                    {
                        new AffixInstance("affix.skill.radius", 1),
                        new AffixInstance("affix.flat.power", 4),
                        new AffixInstance("affix.percent.power", 5000),
                        new AffixInstance("affix.trigger.slow", 1)
                    }),
                out _);
            inventory.TryEquip("i.sword", "player.warrior", "class.warrior", out _);
            inventory.TryEquip("i.boots", "player.warrior", "class.warrior", out _);
            return inventory;
        }

        private static PartyProgressionService Progression() =>
            new(
                Characters(),
                new[]
                {
                    new PartyMemberState("player.warrior", "class.warrior", 1, 0, 0, 20, 10),
                    new PartyMemberState("player.ranger", "class.ranger", 1, 0, 0, 16, 10),
                    new PartyMemberState("player.mage", "class.mage", 1, 0, 0, 12, 20)
                });

        private static IReadOnlyDictionary<string, CharacterDefinition> Characters() =>
            new Dictionary<string, CharacterDefinition>(StringComparer.Ordinal)
            {
                ["class.warrior"] = Character(
                    "class.warrior",
                    new[]
                    {
                        new StatValue(CombatStat.MaxHealth, 20),
                        new StatValue(CombatStat.MaxMana, 10),
                        new StatValue(CombatStat.Power, 9)
                    },
                    new[] { new StatValue(CombatStat.Power, 2) },
                    new[] { "skill.basic" },
                    new[] { new SkillUnlock("skill.whirlwind", 2) }),
                ["class.ranger"] = Character(
                    "class.ranger",
                    new[]
                    {
                        new StatValue(CombatStat.MaxHealth, 16),
                        new StatValue(CombatStat.MaxMana, 10),
                        new StatValue(CombatStat.Power, 7)
                    },
                    new[] { new StatValue(CombatStat.Power, 1) },
                    new[] { "skill.shot" }),
                ["class.mage"] = Character(
                    "class.mage",
                    new[]
                    {
                        new StatValue(CombatStat.MaxHealth, 12),
                        new StatValue(CombatStat.MaxMana, 20),
                        new StatValue(CombatStat.Power, 5)
                    },
                    new[] { new StatValue(CombatStat.Power, 2) },
                    new[] { "skill.fireball" })
            };

        private static IReadOnlyDictionary<string, ItemDefinition> Items() =>
            new Dictionary<string, ItemDefinition>(StringComparer.Ordinal)
            {
                ["item.sword"] = Item(
                    "item.sword",
                    ItemSlot.Weapon,
                    new[] { "class.warrior" },
                    new[] { new StatValue(CombatStat.Power, 5) }),
                ["item.boots"] = Item(
                    "item.boots",
                    ItemSlot.Boots,
                    Array.Empty<string>(),
                    new[] { new StatValue(CombatStat.Speed, 3) })
            };

        private static IReadOnlyDictionary<string, AffixDefinition> Affixes(string targetSkillId = "skill.whirlwind") =>
            new Dictionary<string, AffixDefinition>(StringComparer.Ordinal)
            {
                ["affix.skill.radius"] = Affix(
                    "affix.skill.radius", AffixEffectKind.SkillModifier,
                    skillModifier: SkillModifierKind.Radius, minValue: 1, maxValue: 1,
                    targetSkillId: targetSkillId),
                ["affix.flat.power"] = Affix(
                    "affix.flat.power", AffixEffectKind.FlatStat,
                    stat: CombatStat.Power, minValue: 4, maxValue: 4),
                ["affix.percent.power"] = Affix(
                    "affix.percent.power", AffixEffectKind.PercentStat,
                    stat: CombatStat.Power, minValue: 5000, maxValue: 5000),
                ["affix.trigger.slow"] = Affix(
                    "affix.trigger.slow", AffixEffectKind.Trigger,
                    passive: PassiveEffectKind.OnAttackApplySlow,
                    minValue: 1, maxValue: 1, duration: 2)
            };

        private static CharacterDefinition Character(
            string id,
            IEnumerable<StatValue> baseStats,
            IEnumerable<StatValue> growthStats,
            IEnumerable<string> startingSkills,
            IEnumerable<SkillUnlock> unlocks = null)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            definition.EditorConfigure(
                id, id + ".name", baseStats, growthStats, startingSkills,
                unlocks ?? Array.Empty<SkillUnlock>());
            return definition;
        }

        private static ItemDefinition Item(
            string id, ItemSlot slot, IEnumerable<string> allowedClassIds, IEnumerable<StatValue> stats)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.EditorConfigure(id, id + ".name", slot, allowedClassIds, false, 100, stats);
            return definition;
        }

        private static AffixDefinition Affix(
            string id,
            AffixEffectKind effectKind,
            CombatStat stat = default,
            SkillModifierKind skillModifier = default,
            PassiveEffectKind passive = default,
            int minValue = 0,
            int maxValue = 0,
            int duration = 0,
            string targetSkillId = null)
        {
            var definition = ScriptableObject.CreateInstance<AffixDefinition>();
            definition.EditorConfigure(
                id, id + ".name", new[] { ItemSlot.Boots }, ItemRarity.Common,
                effectKind, stat, skillModifier, passive, minValue, maxValue, 1,
                duration, string.Empty, Array.Empty<string>());
            if (effectKind == AffixEffectKind.SkillModifier && !string.IsNullOrWhiteSpace(targetSkillId))
                definition.EditorSetTargetSkillId(targetSkillId);

            return definition;
        }
    }
}
