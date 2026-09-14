using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.BattleFlow;
using BorderValley.Core.Combat;
using BorderValley.Core.Persistence;
using BorderValley.Data.Items;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BorderValley.Inventory.Tests
{
    public sealed class PartyProgressionServiceTests
    {
        [Test]
        public void AwardExperience_LevelsEveryMemberAndGrantsSkillPoint()
        {
            var progression = Progression();
            progression.AwardExperience(120);
            Assert.That(progression.Members.All(member => member.Level == 2), Is.True);
            Assert.That(progression.Members.All(member => member.Experience == 20), Is.True);
            Assert.That(progression.Members.All(member => member.SkillPoints == 1), Is.True);
        }

        [Test]
        public void AwardExperience_StopsAtMaximumLevel()
        {
            var progression = Progression(new[]
            {
                new PartyMemberState("player.warrior", "class.warrior", 10, 0, 0, 20, 10),
                new PartyMemberState("player.ranger", "class.ranger", 9, 0, 0, 16, 10)
            });
            progression.AwardExperience(1000);
            Assert.That(progression.Members[0].Level, Is.EqualTo(10));
            Assert.That(progression.Members[0].Experience, Is.Zero);
            Assert.That(progression.Members[1].Level, Is.EqualTo(10));
            Assert.That(progression.Members[1].Experience, Is.EqualTo(100));
        }

        [Test]
        public void TrySpendSkillPoint_RequiresUnlockedSkillAndCapsRankAtThree()
        {
            var progression = Progression(new[]
            {
                new PartyMemberState("player.warrior", "class.warrior", 2, 0, 3, 20, 10)
            });
            Assert.That(progression.TrySpendSkillPoint("player.warrior", "skill.unknown", out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(progression.TrySpendSkillPoint("player.warrior", "skill.locked", out _), Is.True);
            Assert.That(progression.TrySpendSkillPoint("player.warrior", "skill.locked", out _), Is.True);
            Assert.That(progression.TrySpendSkillPoint("player.warrior", "skill.locked", out _), Is.True);
            Assert.That(progression.TrySpendSkillPoint("player.warrior", "skill.locked", out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(progression.Members.Single().SkillRanks["skill.locked"], Is.EqualTo(3));
            Assert.That(progression.Members.Single().SkillPoints, Is.Zero);
        }

        [Test]
        public void ApplyBattleUnitStates_ClampsValuesAndFlagsWipeReturn()
        {
            var progression = Progression();
            progression.ApplyBattleUnitStates(new[]
            {
                new BattleUnitResult("player.warrior", 0, -1),
                new BattleUnitResult("player.ranger", 1, 3),
                new BattleUnitResult("player.mage", 1, 0)
            });
            Assert.That(progression.Members.All(member => member.CurrentHealth == 1), Is.True);
            Assert.That(progression.Members.Single(member => member.MemberId == "player.warrior").CurrentMana, Is.Zero);
            Assert.That(progression.HasPendingWipeReturn, Is.True);
            progression.ReturnToSafePoint();
            Assert.That(progression.SafePointId, Is.EqualTo("world.village"));
            Assert.That(progression.HasPendingWipeReturn, Is.False);
        }

        [Test]
        public void SaveParticipant_AfterSafePointReturn_RestoresWithoutPendingWipe()
        {
            var root = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "BorderValleyTests", Guid.NewGuid().ToString("N"));
            var progression = Progression();
            progression.ApplyBattleUnitStates(new[]
            {
                new BattleUnitResult("player.warrior", 0, 0),
                new BattleUnitResult("player.ranger", 1, 0),
                new BattleUnitResult("player.mage", 1, 0)
            });
            Assert.That(progression.HasPendingWipeReturn, Is.True);
            progression.ReturnToSafePoint();
            Assert.That(progression.HasPendingWipeReturn, Is.False);

            var save = new SaveService(root, new ISaveParticipant[] { progression });
            save.Save(0, "World");
            var restored = Progression();
            Assert.That(new SaveService(root, new ISaveParticipant[] { restored }).Load(0), Is.True);
            Assert.That(restored.HasPendingWipeReturn, Is.False);
            System.IO.Directory.Delete(root, true);
        }

        [Test]
        public void Restore_WhenPendingWipeFlagIsMissing_RecomputesFromHealth()
        {
            var progression = Progression(new[]
            {
                new PartyMemberState("player.warrior", "class.warrior", 1, 0, 0, 1, 0)
            });
            progression.Restore(new JObject
            {
                ["safePointId"] = "world.camp",
                ["members"] = new JArray
                {
                    new JObject
                    {
                        ["memberId"] = "player.warrior",
                        ["characterId"] = "class.warrior",
                        ["level"] = 1,
                        ["experience"] = 0,
                        ["skillPoints"] = 0,
                        ["currentHealth"] = 1,
                        ["currentMana"] = 0,
                        ["skillRanks"] = new JObject()
                    }
                }
            });

            Assert.That(progression.HasPendingWipeReturn, Is.True);
            Assert.That(progression.SafePointId, Is.EqualTo("world.camp"));
        }

        [Test]
        public void SaveParticipant_RoundTripsGrowthSkillRanksAndResources()
        {
            var root = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "BorderValleyTests", Guid.NewGuid().ToString("N"));
            var progression = Progression();
            progression.AwardExperience(1000);
            Assert.That(progression.TrySpendSkillPoint("player.warrior", "skill.basic", out _), Is.True);
            progression.ApplyBattleUnitStates(new[] { new BattleUnitResult("player.warrior", 3, 4) });
            var save = new SaveService(root, new ISaveParticipant[] { progression });
            save.Save(0, "World");
            var restored = Progression();
            new SaveService(root, new ISaveParticipant[] { restored }).Load(0);
            var warrior = restored.Members.Single(member => member.MemberId == "player.warrior");
            Assert.That(warrior.Level, Is.EqualTo(5));
            Assert.That(warrior.Experience, Is.Zero);
            Assert.That(warrior.SkillPoints, Is.EqualTo(1));
            Assert.That(warrior.SkillRanks["skill.basic"], Is.EqualTo(1));
            Assert.That(warrior.CurrentHealth, Is.EqualTo(3));
            Assert.That(warrior.CurrentMana, Is.EqualTo(4));
            Assert.That(restored.SafePointId, Is.EqualTo("world.village"));
            System.IO.Directory.Delete(root, true);
        }

        [Test]
        public void Restore_WithUnknownCharacterOrInvalidLevel_Throws()
        {
            var progression = Progression();
            var originalHealth = progression.Members[0].CurrentHealth;
            var state = new JObject
            {
                ["safePointId"] = "world.camp",
                ["members"] = new JArray
                {
                    new JObject
                    {
                        ["memberId"] = "player.warrior",
                        ["characterId"] = "class.unknown",
                        ["level"] = 8,
                        ["experience"] = 500,
                        ["skillPoints"] = 4,
                        ["currentHealth"] = 1,
                        ["currentMana"] = 0,
                        ["skillRanks"] = new JObject { ["skill.basic"] = 3 }
                    },
                    new JObject
                    {
                        ["memberId"] = "player.ranger",
                        ["characterId"] = "class.ranger",
                        ["level"] = 11,
                        ["experience"] = 500,
                        ["skillPoints"] = 4,
                        ["currentHealth"] = 1,
                        ["currentMana"] = 0
                    }
                }
            };
            Assert.Throws<System.InvalidOperationException>(() => progression.Restore(state));
            Assert.That(progression.Members.All(member => member.Level == 1), Is.True);
            Assert.That(progression.Members[0].CurrentHealth, Is.EqualTo(originalHealth));
            Assert.That(progression.Members.All(member => member.SkillPoints == 0), Is.True);
            Assert.That(progression.SafePointId, Is.EqualTo(PartyProgressionService.DefaultSafePointId));
        }

        private static PartyProgressionService Progression(IEnumerable<PartyMemberState> members = null)
        {
            var characters = Characters();
            var states = members ?? new[]
            {
                new PartyMemberState("player.warrior", "class.warrior", 1, 0, 0, 20, 10),
                new PartyMemberState("player.ranger", "class.ranger", 1, 0, 0, 16, 10),
                new PartyMemberState("player.mage", "class.mage", 1, 0, 0, 12, 20)
            };
            return new PartyProgressionService(characters, states);
        }

        private static IReadOnlyDictionary<string, CharacterDefinition> Characters()
        {
            var characters = new Dictionary<string, CharacterDefinition>(StringComparer.Ordinal)
            {
                ["class.warrior"] = Character(
                    "class.warrior",
                    new[] { new StatValue(CombatStat.MaxHealth, 20), new StatValue(CombatStat.Power, 9) },
                    new[] { "skill.basic" },
                    new[] { new SkillUnlock("skill.locked", 2) }),
                ["class.ranger"] = Character(
                    "class.ranger",
                    new[] { new StatValue(CombatStat.MaxHealth, 16), new StatValue(CombatStat.Power, 7) },
                    new[] { "skill.shot" }),
                ["class.mage"] = Character(
                    "class.mage",
                    new[]
                    {
                        new StatValue(CombatStat.MaxHealth, 12),
                        new StatValue(CombatStat.MaxMana, 20),
                        new StatValue(CombatStat.Power, 5)
                    },
                    new[] { "skill.fireball" })
            };
            return characters;
        }

        private static CharacterDefinition Character(
            string id,
            IEnumerable<StatValue> baseStats,
            IEnumerable<string> startingSkills,
            IEnumerable<SkillUnlock> unlocks = null)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            definition.EditorConfigure(
                id,
                id + ".name",
                baseStats,
                Array.Empty<StatValue>(),
                startingSkills,
                unlocks ?? Array.Empty<SkillUnlock>());
            return definition;
        }
    }
}
