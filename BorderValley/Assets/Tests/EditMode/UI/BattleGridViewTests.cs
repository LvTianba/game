using System.Collections.Generic;
using System.Linq;
using BorderValley.Battle.Domain;
using BorderValley.Core.Random;
using BorderValley.Presentation;
using BorderValley.UI.Battle;
using NUnit.Framework;
using UnityEngine;

namespace BorderValley.UI.Tests
{
    public sealed class BattleGridViewTests
    {
        private readonly List<UnityEngine.Object> owned = new();

        [TearDown]
        public void TearDown()
        {
            for (var index = owned.Count - 1; index >= 0; index--)
            {
                if (owned[index] != null)
                    UnityEngine.Object.DestroyImmediate(owned[index]);
            }

            owned.Clear();
        }

        [Test]
        public void Render_AfterUnitDeath_KeepsDownClip()
        {
            var presentation = CreateCatalogService();
            var presenter = CreateStartedPresenter();
            var grid = CreateGrid(presentation);
            grid.Render(presenter, presentation);
            var target = presenter.Engine.State.GetUnit("enemy.bandit");
            target.ApplyRawDamage(target.Health);

            grid.Render(presenter, presentation);
            grid.PlayEvent(
                new BattlePresentationEvent(target.Id, BattlePresentationEventKind.Down),
                presentation);
            grid.Render(presenter, presentation);

            var animator = UnitAnimator(grid, target.Position);
            Assert.That(animator.CurrentClipId, Is.EqualTo("battle.unit.bandit.down"));
        }

        [Test]
        public void PlayEvent_MissingClip_FallsBackToIdle()
        {
            var presentation = CreateIdleAndDownOnlyService();
            var presenter = CreateStartedPresenter();
            var grid = CreateGrid(presentation);
            grid.Render(presenter, presentation);
            var target = presenter.Engine.State.GetUnit("enemy.bandit");

            grid.PlayEvent(
                new BattlePresentationEvent(target.Id, BattlePresentationEventKind.Down),
                presentation);
            Assert.That(
                UnitAnimator(grid, target.Position).CurrentClipId,
                Is.EqualTo("battle.unit.bandit.down"));

            grid.PlayEvent(
                new BattlePresentationEvent(target.Id, BattlePresentationEventKind.Hit),
                presentation);
            Assert.That(
                UnitAnimator(grid, target.Position).CurrentClipId,
                Is.EqualTo("battle.unit.bandit.idle"));
        }

        private BattleGridView CreateGrid(IPresentationService presentation)
        {
            var root = new GameObject(
                "BattleGrid",
                typeof(RectTransform),
                typeof(BattleGridView));
            owned.Add(root);
            var grid = root.GetComponent<BattleGridView>();
            grid.Initialize(_ => { }, presentation);
            return grid;
        }

        private static SpriteAnimator UnitAnimator(BattleGridView grid, GridPosition position) =>
            grid.transform
                .Find($"Cell_{position.X}_{position.Y}")
                .GetComponentInChildren<SpriteAnimator>(true);

        private static BattleUiPresenter CreateStartedPresenter()
        {
            var presenter = new BattleUiPresenter(
                BattleScenarioFactory.CreateCoreScenario(),
                RandomSourceFactory.FromSeed("battle-grid"));
            presenter.Start();
            return presenter;
        }

        private static PresentationService CreateCatalogService()
        {
            var catalog = Resources.Load<PresentationCatalog>("PresentationCatalog");
            Assert.That(catalog, Is.Not.Null);
            return new PresentationService(catalog, null);
        }

        private PresentationService CreateIdleAndDownOnlyService()
        {
            var texture = new Texture2D(8, 2, TextureFormat.RGBA32, false);
            owned.Add(texture);
            var idle = Sprite.Create(
                texture,
                new Rect(0f, 0f, 4f, 2f),
                Vector2.one * 0.5f,
                2f);
            owned.Add(idle);
            var down = Sprite.Create(
                texture,
                new Rect(4f, 0f, 4f, 2f),
                Vector2.one * 0.5f,
                2f);
            owned.Add(down);
            var catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            owned.Add(catalog);
            catalog.EditorSetVisualClips(new[]
            {
                VisualClipDefinition.CreateForTests("battle.unit.bandit.idle", idle),
                VisualClipDefinition.CreateForTests("battle.unit.bandit.down", down)
            });
            catalog.EditorSetMapping(
                "CharacterVisuals",
                new[] { new StringPair("enemy.bandit", "battle.unit.bandit") });
            return new PresentationService(catalog, null);
        }
    }
}
