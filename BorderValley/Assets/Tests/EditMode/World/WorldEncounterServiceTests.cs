using System;
using System.Collections.Generic;
using BorderValley.Core.BattleFlow;
using BorderValley.Data.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorderValley.World.Tests
{
    public sealed class WorldEncounterServiceTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                Object.DestroyImmediate(value);
            created.Clear();
        }

        [Test]
        public void BuildRequest_PreservesScenarioPartySeedAndEncounterContext()
        {
            var encounter = CreateEncounter();
            var party = PartySnapshot();

            var request = WorldEncounterService.BuildRequest(encounter, party, "seed.forest.7");

            Assert.That(request.ScenarioId, Is.EqualTo("scenario.forest.bandits"));
            Assert.That(request.Seed, Is.EqualTo("seed.forest.7"));
            Assert.That(request.ReturnScene, Is.EqualTo("World"));
            Assert.That(request.PartySnapshot, Is.SameAs(party));
            Assert.That(request.Context, Is.Not.Null);
            Assert.That(request.Context.EncounterId, Is.EqualTo("encounter.forest.bandits"));
            Assert.That(request.Context.RewardTableId, Is.EqualTo("loot.bandit.core"));
            Assert.That(request.Context.GoldReward, Is.EqualTo(30));
            Assert.That(request.Context.ExperienceReward, Is.EqualTo(45));
            Assert.That(
                request.Context.EnemyDefinitionIds,
                Is.EqualTo(new[] { "enemy.bandit", "enemy.wolf" }));
            Assert.That(request.Context.Repeatable, Is.True);
        }

        [Test]
        public void BuildRequest_WithExplicitReturnScene_PreservesIt()
        {
            var request = WorldEncounterService.BuildRequest(
                CreateEncounter(),
                PartySnapshot(),
                "seed.explicit",
                "World.Return");

            Assert.That(request.Seed, Is.EqualTo("seed.explicit"));
            Assert.That(request.ReturnScene, Is.EqualTo("World.Return"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void BuildRequest_WhenExplicitReturnSceneIsBlank_Throws(string returnScene)
        {
            Assert.Throws<ArgumentException>(() => WorldEncounterService.BuildRequest(
                CreateEncounter(),
                PartySnapshot(),
                "seed.invalid-return",
                returnScene));
        }

        private WorldEncounterDefinition CreateEncounter()
        {
            var encounter = Track(ScriptableObject.CreateInstance<WorldEncounterDefinition>());
            encounter.EditorConfigure(
                "encounter.forest.bandits",
                "scenario.forest.bandits",
                new[] { "enemy.bandit", "enemy.wolf" },
                "loot.bandit.core",
                30,
                45,
                new Vector2(3f, 4f),
                2f,
                true,
                "event.forest.enter",
                "event.bandits.defeated");
            return encounter;
        }

        private static BattlePartySnapshot PartySnapshot()
        {
            return new BattlePartySnapshot(new[]
            {
                new BattleCombatantSnapshot(
                    "player.warrior",
                    "class.warrior",
                    "class.warrior",
                    20,
                    10,
                    5,
                    2,
                    3,
                    100,
                    0,
                    20,
                    10,
                    new[] { "skill.basic" },
                    Array.Empty<BattleSkillModifierSnapshot>(),
                    Array.Empty<BattlePassiveSnapshot>())
            });
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }
    }
}
