using System.Linq;
using NUnit.Framework;
using UnityEditor;
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
        public void Validate_NonLoopingVictoryMusic_IsAllowed()
        {
            var audio = AudioCueDefinition.CreateForTests(
                "bgm.victory",
                null,
                1f,
                false,
                AudioChannel.Music);
            var catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            catalog.EditorSetAudioCues(new[] { audio });

            Assert.That(PresentationCatalogValidator.Validate(catalog), Is.Empty);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Validate_ShippedCatalog_HasNoValidationIssues()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PresentationCatalog>(
                "Assets/Resources/PresentationCatalog.asset");

            Assert.That(catalog, Is.Not.Null);
            Assert.That(PresentationCatalogValidator.Validate(catalog), Is.Empty);
        }

        [Test]
        public void ShippedCatalog_VictoryIsOneShotAndBackgroundMusicLoops()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PresentationCatalog>(
                "Assets/Resources/PresentationCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            var audio = catalog.AudioCues.ToDictionary(value => value.Id, System.StringComparer.Ordinal);

            Assert.That(audio["bgm.victory"].Loop, Is.False);
            foreach (var id in new[]
                     {
                         "bgm.menu",
                         "bgm.world.village",
                         "bgm.world.forest",
                         "bgm.world.watchtower",
                         "bgm.world.crypt",
                         "bgm.battle"
                     })
            {
                Assert.That(audio[id].Loop, Is.True, id);
            }
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
