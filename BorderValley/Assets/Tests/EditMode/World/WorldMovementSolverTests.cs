using System;
using System.Collections.Generic;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorderValley.World.Tests
{
    public sealed class WorldMovementSolverTests
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
        public void TryMove_ClampsCenterInsideBoundsWithRadius()
        {
            var area = CreateArea(new Rect(0f, 0f, 10f, 10f));

            var moved = WorldMovementSolver.TryMove(
                new Vector2(1f, 1f),
                new Vector2(20f, 20f),
                0.5f,
                area,
                out var result);

            Assert.That(moved, Is.True);
            Assert.That(result.x, Is.EqualTo(9.5f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(9.5f).Within(0.0001f));
        }

        [Test]
        public void TryMove_CircleRadiusCannotEnterRectangleObstacle()
        {
            var area = CreateArea(new Rect(0f, 0f, 10f, 10f), new Rect(4f, 4f, 2f, 2f));

            var moved = WorldMovementSolver.TryMove(
                new Vector2(2.5f, 5f),
                new Vector2(3.9f, 0f),
                0.5f,
                area,
                out var result);

            Assert.That(moved, Is.True);
            Assert.That(result, Is.EqualTo(new Vector2(2.5f, 5f)));
        }

        [Test]
        public void TryMove_WhenVerticalAxisIsBlocked_StillMovesOnHorizontalAxis()
        {
            var area = CreateArea(new Rect(0f, 0f, 10f, 10f), new Rect(2f, 2f, 2f, 1f));

            var moved = WorldMovementSolver.TryMove(
                new Vector2(1f, 1f),
                new Vector2(3f, 1f),
                0.5f,
                area,
                out var result);

            Assert.That(moved, Is.True);
            Assert.That(result, Is.EqualTo(new Vector2(4f, 1f)));
        }

        [Test]
        public void TryMove_InvalidRadius_ReturnsFalseAndOriginalPosition()
        {
            var area = CreateArea(new Rect(0f, 0f, 10f, 10f));

            var moved = WorldMovementSolver.TryMove(
                new Vector2(2f, 3f),
                Vector2.one,
                -0.1f,
                area,
                out var result);

            Assert.That(moved, Is.False);
            Assert.That(result, Is.EqualTo(new Vector2(2f, 3f)));
        }

        [Test]
        public void TryMove_InvalidArea_ReturnsFalseAndOriginalPosition()
        {
            var moved = WorldMovementSolver.TryMove(
                new Vector2(2f, 3f),
                Vector2.one,
                0.5f,
                null,
                out var result);

            Assert.That(moved, Is.False);
            Assert.That(result, Is.EqualTo(new Vector2(2f, 3f)));
        }

        private WorldAreaDefinition CreateArea(Rect bounds, params Rect[] obstacles)
        {
            var area = ScriptableObject.CreateInstance<WorldAreaDefinition>();
            created.Add(area);
            area.EditorConfigure(
                "area.test",
                bounds,
                obstacles,
                Array.Empty<NpcDefinition>(),
                Array.Empty<WorldEncounterDefinition>(),
                Array.Empty<WorldInteractableDefinition>(),
                Array.Empty<ItemDropTableDefinition>(),
                Array.Empty<string>(),
                Array.Empty<string>());
            return area;
        }
    }
}
