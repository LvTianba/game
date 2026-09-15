using System;
using System.Collections.Generic;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;
using BorderValley.Narrative;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorderValley.World.Tests
{
    public sealed class WorldInteractionResolverTests
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
        public void FindNearest_WhenNearestInteractionIsOutsideRadius_ReturnsFalse()
        {
            var area = CreateArea(
                "area.village",
                Interactable("npc.elder", WorldInteractableKind.Npc, new Vector2(3f, 0f), 1f));
            var state = CreateState(area);

            var found = WorldInteractionResolver.FindNearest(
                Vector2.zero,
                area,
                state,
                out var result);

            Assert.That(found, Is.False);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void FindNearest_WhenRequiredEventIsNotSet_IsInvisible()
        {
            var area = CreateArea(
                "area.village",
                Interactable(
                    "investigation.secret",
                    WorldInteractableKind.Investigate,
                    Vector2.zero,
                    2f,
                    "event.secret"));
            var state = CreateState(area, "event.secret");

            var found = WorldInteractionResolver.FindNearest(
                Vector2.zero,
                area,
                state,
                out var result);

            Assert.That(found, Is.False);
            Assert.That(result, Is.Null);
        }

        [TestCase(WorldInteractableKind.Chest)]
        [TestCase(WorldInteractableKind.Gather)]
        [TestCase(WorldInteractableKind.Investigate)]
        public void FindNearest_WhenResolvableInteractionIsResolved_IsInvisible(
            WorldInteractableKind kind)
        {
            var definition = Interactable("interaction.resolved", kind, Vector2.zero, 2f);
            var area = CreateArea("area.village", definition);
            var state = CreateState(area);
            Assert.That(state.MarkInteractableResolved(definition.Id), Is.True);

            var found = WorldInteractionResolver.FindNearest(
                Vector2.zero,
                area,
                state,
                out var result);

            Assert.That(found, Is.False);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void FindNearest_WhenDistancesAreEqual_UsesDefinitionIdOrdinalOrder()
        {
            var area = CreateArea(
                "area.village",
                Interactable("interaction.b", WorldInteractableKind.Npc, new Vector2(-1f, 0f), 2f),
                Interactable("interaction.a", WorldInteractableKind.Npc, new Vector2(1f, 0f), 2f));
            var state = CreateState(area);

            var found = WorldInteractionResolver.FindNearest(
                Vector2.zero,
                area,
                state,
                out var result);

            Assert.That(found, Is.True);
            Assert.That(result.DefinitionId, Is.EqualTo("interaction.a"));
        }

        [Test]
        public void FindNearest_AreaExit_ReturnsTargetArrivalAndSource()
        {
            var area = CreateArea(
                "area.village",
                Interactable(
                    "exit.forest",
                    WorldInteractableKind.AreaExit,
                    new Vector2(2f, 0f),
                    2f,
                    string.Empty,
                    "area.forest",
                    new Vector2(1f, 3f)));
            var state = CreateState(area);

            var found = WorldInteractionResolver.FindNearest(
                Vector2.zero,
                area,
                state,
                out var result);

            Assert.That(found, Is.True);
            Assert.That(result.Kind, Is.EqualTo(WorldInteractableKind.AreaExit));
            Assert.That(result.DefinitionId, Is.EqualTo("exit.forest"));
            Assert.That(result.TargetId, Is.EqualTo("area.forest"));
            Assert.That(result.ArrivalPosition, Is.EqualTo(new Vector2(1f, 3f)));
            Assert.That(result.SourceId, Is.EqualTo("area.village"));
        }

        [Test]
        public void FindNearest_WhenNonRepeatableEncounterCompleted_IsInvisible()
        {
            var encounter = ScriptableObject.CreateInstance<WorldEncounterDefinition>();
            created.Add(encounter);
            encounter.EditorConfigure(
                "encounter.boss",
                "core",
                new[] { "enemy.mage" },
                string.Empty,
                10,
                10,
                Vector2.zero,
                1.5f,
                false,
                string.Empty,
                "event.boss.defeated");
            var interactable = Interactable(
                "interaction.boss",
                WorldInteractableKind.Encounter,
                Vector2.zero,
                1.5f,
                targetId: encounter.EncounterId);
            var area = CreateArea("area.crypt", interactable);
            area.EditorConfigure(
                area.Id,
                area.Bounds,
                area.Obstacles,
                area.Npcs,
                new[] { encounter },
                area.Interactables,
                area.RewardTables,
                new[] { encounter.CompletionEventId },
                area.RewardTableIds);
            var state = CreateState(area, encounter.CompletionEventId);
            Assert.That(state.SetEvent(encounter.CompletionEventId), Is.True);

            var found = WorldInteractionResolver.FindNearest(
                Vector2.zero,
                area,
                state,
                out var result);

            Assert.That(found, Is.False);
            Assert.That(result, Is.Null);
        }
        private WorldInteractableDefinition Interactable(
            string id,
            WorldInteractableKind kind,
            Vector2 position,
            float radius,
            string requiredEventId = "",
            string targetId = "",
            Vector2 arrivalPosition = default)
        {
            return new WorldInteractableDefinition(
                id,
                kind,
                id + ".label",
                position,
                radius,
                targetId,
                arrivalPosition,
                requiredEventId);
        }

        private WorldAreaDefinition CreateArea(
            string id,
            params WorldInteractableDefinition[] interactables)
        {
            var area = ScriptableObject.CreateInstance<WorldAreaDefinition>();
            created.Add(area);
            area.EditorConfigure(
                id,
                new Rect(0f, 0f, 10f, 10f),
                Array.Empty<Rect>(),
                Array.Empty<NpcDefinition>(),
                Array.Empty<WorldEncounterDefinition>(),
                interactables,
                Array.Empty<ItemDropTableDefinition>(),
                Array.Empty<string>(),
                Array.Empty<string>());
            return area;
        }

        private NarrativeStateService CreateState(
            WorldAreaDefinition area,
            params string[] eventIds)
        {
            area.EditorConfigure(
                area.Id,
                area.Bounds,
                area.Obstacles,
                area.Npcs,
                area.Encounters,
                area.Interactables,
                area.RewardTables,
                eventIds,
                area.RewardTableIds);
            return new NarrativeStateService(new[] { area });
        }
    }
}
