using NUnit.Framework;
using UnityEngine;

namespace BorderValley.Presentation.Tests
{
    public sealed class PresentationServiceTests
    {
        [Test]
        public void GetAreaMusicCueId_MissingArea_ReturnsMenuFallback()
        {
            var catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            catalog.EditorSetDefaultMusicCueId("bgm.menu");
            var service = new PresentationService(catalog, null);

            Assert.That(service.GetAreaMusicCueId("area.missing"), Is.EqualTo("bgm.menu"));
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void GetItemIcon_MissingExactAndSlot_ReturnsMissingSprite()
        {
            var catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            var service = new PresentationService(catalog, null);

            Assert.That(service.GetItemIcon("item.missing", "weapon"), Is.SameAs(service.MissingSprite));
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void MissingSprite_ConfiguredCatalogSprite_IsUsed()
        {
            var texture = new Texture2D(3, 3);
            var configured = Sprite.Create(texture, new Rect(0, 0, 3, 3), Vector2.one * 0.5f);
            var catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            catalog.EditorSetVisualClips(new[]
            {
                VisualClipDefinition.CreateForTests("ui.missing.configured", configured)
            });
            catalog.EditorSetMissingSpriteId("ui.missing.configured");

            var service = new PresentationService(catalog, null);

            Assert.That(service.MissingSprite, Is.SameAs(configured));
            Assert.That(service.GetSprite("missing.sprite"), Is.SameAs(configured));
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(configured);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void MissingSprite_NoCatalogOverride_IsGeneratedTwoByTwoMagenta()
        {
            var catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            var service = new PresentationService(catalog, null);

            Assert.That(service.MissingSprite.texture.width, Is.EqualTo(2));
            Assert.That(service.MissingSprite.texture.height, Is.EqualTo(2));
            Assert.That(service.MissingSprite.texture.GetPixel(0, 0), Is.EqualTo(Color.magenta));
            Object.DestroyImmediate(catalog);
        }
    }
}
