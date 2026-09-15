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
    }
}
