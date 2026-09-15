using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BorderValley.Presentation.Tests
{
    public sealed class PresentationCatalogTests
    {
        [Test]
        public void Validate_DuplicateClipId_ReturnsIssue()
        {
            var catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            catalog.EditorSetVisualClips(new[]
            {
                VisualClipDefinition.CreateForTests("world.player.idle", null),
                VisualClipDefinition.CreateForTests("world.player.idle", null)
            });

            var issues = PresentationCatalogValidator.Validate(catalog).ToArray();

            Assert.That(issues.Any(value => value.Code == "duplicate_visual_id"), Is.True);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Validate_LoopingMusicWithZeroVolume_IsAllowed()
        {
            var audio = AudioCueDefinition.CreateForTests("bgm.menu", null, 0f, true);
            var catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            catalog.EditorSetAudioCues(new[] { audio });
            Assert.That(PresentationCatalogValidator.Validate(catalog), Is.Empty);
            Object.DestroyImmediate(catalog);
        }
    }
}
