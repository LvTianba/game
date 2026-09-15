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
            var audio = AudioCueDefinition.CreateForTests(
                "bgm.menu",
                null,
                0f,
                true,
                AudioChannel.Music);
            var catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            catalog.EditorSetAudioCues(new[] { audio });
            Assert.That(PresentationCatalogValidator.Validate(catalog), Is.Empty);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Validate_NonLoopingMusic_ReturnsLoopingMusicIssue()
        {
            var audio = AudioCueDefinition.CreateForTests(
                "bgm.menu",
                null,
                1f,
                false,
                AudioChannel.Music);
            var catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            catalog.EditorSetAudioCues(new[] { audio });

            var issues = PresentationCatalogValidator.Validate(catalog).ToArray();

            Assert.That(issues.Any(value => value.Code == "looping_music_not_loopable"), Is.True);
            Object.DestroyImmediate(catalog);
        }

        [TestCase("")]
        [TestCase("   ")]
        public void Validate_BlankVisualId_ReturnsInvalidVisualId(string id)
        {
            var catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            catalog.EditorSetVisualClips(new[] { VisualClipDefinition.CreateForTests(id, null) });

            var issues = PresentationCatalogValidator.Validate(catalog).ToArray();

            Assert.That(issues.Any(value => value.Code == "invalid_visual_id"), Is.True);
            Object.DestroyImmediate(catalog);
        }

        [TestCase("")]
        [TestCase("   ")]
        public void Validate_BlankAudioId_ReturnsInvalidAudioId(string id)
        {
            var catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            catalog.EditorSetAudioCues(new[]
            {
                AudioCueDefinition.CreateForTests(id, null, 1f, true, AudioChannel.Music)
            });

            var issues = PresentationCatalogValidator.Validate(catalog).ToArray();

            Assert.That(issues.Any(value => value.Code == "invalid_audio_id"), Is.True);
            Object.DestroyImmediate(catalog);
        }
    }
}
