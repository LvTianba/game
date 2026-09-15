using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BorderValley.Presentation.Tests
{
    public sealed class PlaceholderAssetTests
    {
        private const string CatalogPath = "Assets/Resources/PresentationCatalog.asset";
        private const string WorldRoot = "Assets/Art/Placeholder/World/";
        private const string BattleRoot = "Assets/Art/Placeholder/Battle/";
        private const string PortraitRoot = "Assets/Art/Placeholder/Portraits/";
        private const string UiRoot = "Assets/Art/Placeholder/Ui/";
        private const string ItemRoot = "Assets/Art/Placeholder/Items/";
        private const string MusicRoot = "Assets/Audio/Placeholder/Music/";
        private const string SfxRoot = "Assets/Audio/Placeholder/Sfx/";

        private static readonly string[] NpcNames = { "elder", "merchant", "blacksmith", "innkeeper", "ranger", "mage", "hunter", "survivor" };
        private static readonly string[] UnitNames = { "warrior", "ranger", "mage", "bandit", "wolf", "skeleton", "boss" };
        private static readonly string[] GroundNames = { "village", "forest", "watchtower", "crypt" };
        private static readonly string[] MarkerNames = { "chest", "gather", "investigate", "area_exit", "encounter" };
        private static readonly string[] Directions = { "south", "east", "north", "west" };

        private static readonly string[] WorldTextures =
        {
            WorldRoot + "world_player.png",
            WorldRoot + "world_ground_village.png",
            WorldRoot + "world_ground_forest.png",
            WorldRoot + "world_ground_watchtower.png",
            WorldRoot + "world_ground_crypt.png",
            WorldRoot + "world_npc_elder.png",
            WorldRoot + "world_npc_merchant.png",
            WorldRoot + "world_npc_blacksmith.png",
            WorldRoot + "world_npc_innkeeper.png",
            WorldRoot + "world_npc_ranger.png",
            WorldRoot + "world_npc_mage.png",
            WorldRoot + "world_npc_hunter.png",
            WorldRoot + "world_npc_survivor.png",
            WorldRoot + "world_marker_chest.png",
            WorldRoot + "world_marker_gather.png",
            WorldRoot + "world_marker_investigate.png",
            WorldRoot + "world_marker_area_exit.png",
            WorldRoot + "world_marker_encounter.png"
        };

        private static readonly string[] BattleTextures = UnitNames.Select(value => BattleRoot + "battle_unit_" + value + ".png").ToArray();
        private static readonly string[] PortraitTextures = NpcNames.Select(value => PortraitRoot + "portrait_npc_" + value + ".png").ToArray();
        private static readonly string[] UiTextures =
        {
            UiRoot + "ui_panel.png",
            UiRoot + "ui_button.png",
            UiRoot + "ui_button_hover.png",
            UiRoot + "ui_button_pressed.png",
            UiRoot + "ui_missing.png"
        };
        private static readonly string[] ItemTextures =
        {
            ItemRoot + "item_weapon.png",
            ItemRoot + "item_armor.png",
            ItemRoot + "item_accessory.png"
        };
        private static readonly string[] MusicPaths =
        {
            MusicRoot + "bgm_menu.wav",
            MusicRoot + "bgm_world_village.wav",
            MusicRoot + "bgm_world_forest.wav",
            MusicRoot + "bgm_world_watchtower.wav",
            MusicRoot + "bgm_world_crypt.wav",
            MusicRoot + "bgm_battle.wav",
            MusicRoot + "bgm_victory.wav"
        };
        private static readonly string[] SfxPaths =
        {
            SfxRoot + "sfx_ui_click.wav", SfxRoot + "sfx_ui_confirm.wav", SfxRoot + "sfx_ui_cancel.wav",
            SfxRoot + "sfx_ui_error.wav", SfxRoot + "sfx_dialogue_page.wav", SfxRoot + "sfx_shop_buy.wav",
            SfxRoot + "sfx_shop_sell.wav", SfxRoot + "sfx_inventory_equip.wav", SfxRoot + "sfx_inventory_craft.wav",
            SfxRoot + "sfx_world_reward.wav", SfxRoot + "sfx_world_encounter.wav", SfxRoot + "sfx_battle_attack.wav",
            SfxRoot + "sfx_battle_hit.wav", SfxRoot + "sfx_battle_down.wav"
        };

        private static IEnumerable<string> AllTexturePaths => WorldTextures.Concat(BattleTextures)
            .Concat(PortraitTextures).Concat(UiTextures).Concat(ItemTextures);

        [Test]
        public void GeneratedAssets_AllRequiredPathsAreImported()
        {
            foreach (var path in AllTexturePaths)
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(path), Is.Not.Null, path);
                Assert.That(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Any(), Is.True, path);
            }

            foreach (var path in MusicPaths.Concat(SfxPaths))
                Assert.That(AssetDatabase.LoadAssetAtPath<AudioClip>(path), Is.Not.Null, path);
        }

        [TestCase(WorldRoot + "world_player.png", 512, 48)]
        [TestCase(WorldRoot + "world_ground_village.png", 32, 32)]
        [TestCase(WorldRoot + "world_npc_elder.png", 64, 48)]
        [TestCase(WorldRoot + "world_marker_chest.png", 32, 32)]
        [TestCase(BattleRoot + "battle_unit_warrior.png", 576, 64)]
        [TestCase(PortraitRoot + "portrait_npc_elder.png", 256, 256)]
        [TestCase(UiRoot + "ui_panel.png", 16, 16)]
        [TestCase(UiRoot + "ui_missing.png", 32, 32)]
        [TestCase(ItemRoot + "item_weapon.png", 32, 32)]
        public void GeneratedTexture_HasExpectedSize(string path, int width, int height)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture, Is.Not.Null, path);
            Assert.That(texture.width, Is.EqualTo(width), path);
            Assert.That(texture.height, Is.EqualTo(height), path);
        }

        [Test]
        public void GeneratedTextures_UseRequiredImportSettings()
        {
            foreach (var path in AllTexturePaths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(32f), path);
                Assert.That(importer.alphaIsTransparency, Is.True, path);
                Assert.That(importer.wrapMode == TextureWrapMode.Clamp ||
                            importer.wrapMode == TextureWrapMode.Repeat, Is.True, path);
            }
        }

        [Test]
        public void GeneratedTextures_UsePointFilterAndNoMipmaps()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Placeholder" });
            Assert.That(guids, Is.Not.Empty);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(32f), path);
            }
        }

        [Test]
        public void AnimatedSheets_HaveFixedMultipleSpriteSlices()
        {
            AssertMultipleSheet(WorldRoot + "world_player.png", 16, 32, 48);
            foreach (var unit in UnitNames)
                AssertMultipleSheet(BattleRoot + "battle_unit_" + unit + ".png", 9, 64, 64);
            foreach (var npc in NpcNames)
                AssertMultipleSheet(WorldRoot + "world_npc_" + npc + ".png", 2, 32, 48);
        }

        [Test]
        public void SingleFrameTextures_UseSingleSpriteModeAndFullRect()
        {
            var singlePaths = WorldTextures
                .Where(path => !path.Contains("world_player.png", StringComparison.Ordinal) &&
                               !path.Contains("world_npc_", StringComparison.Ordinal))
                .Concat(PortraitTextures).Concat(UiTextures).Concat(ItemTextures);

            foreach (var path in singlePaths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
                Assert.That(ReadSpriteMeshType(importer), Is.EqualTo(SpriteMeshType.FullRect), path);

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.That(sprite, Is.Not.Null, path);
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32f), path);
            }
        }

        [Test]
        public void UiNineSlices_HaveFourPixelBorders()
        {
            foreach (var path in new[]
                     {
                         UiRoot + "ui_panel.png",
                         UiRoot + "ui_button.png",
                         UiRoot + "ui_button_hover.png",
                         UiRoot + "ui_button_pressed.png"
                     })
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.That(sprite, Is.Not.Null, path);
                Assert.That(sprite.border, Is.EqualTo(new Vector4(4f, 4f, 4f, 4f)), path);
            }
        }

        [Test]
        public void GeneratedWavFiles_UseMono44100Hz16BitPcm()
        {
            foreach (var path in MusicPaths.Concat(SfxPaths))
            {
                var bytes = File.ReadAllBytes(ToAbsolutePath(path));
                Assert.That(bytes.Length, Is.GreaterThan(44), path);
                Assert.That(System.Text.Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("RIFF"), path);
                Assert.That(System.Text.Encoding.ASCII.GetString(bytes, 8, 4), Is.EqualTo("WAVE"), path);
                Assert.That(BitConverter.ToInt16(bytes, 22), Is.EqualTo(1), path);
                Assert.That(BitConverter.ToInt32(bytes, 24), Is.EqualTo(44100), path);
                Assert.That(BitConverter.ToInt16(bytes, 34), Is.EqualTo(16), path);
                Assert.That(System.Text.Encoding.ASCII.GetString(bytes, 36, 4), Is.EqualTo("data"), path);

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.That(clip, Is.Not.Null, path);
                Assert.That(clip.frequency, Is.EqualTo(44100), path);
                Assert.That(clip.channels, Is.EqualTo(1), path);
            }
        }

        [Test]
        public void GeneratedAudio_HasRequiredCueLengths()
        {
            foreach (var path in MusicPaths.Where(path => !path.EndsWith("bgm_victory.wav", StringComparison.Ordinal)))
                AssertAudioLength(path, 8f);
            AssertAudioLength(MusicRoot + "bgm_victory.wav", 4f);

            foreach (var path in SfxPaths)
                Assert.That(GetWavLength(path), Is.InRange(0.08f, 0.5f), path);
        }

        private static void AssertMultipleSheet(string path, int expectedCount, int width, int height)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple), path);
            Assert.That(ReadSpriteMeshType(importer), Is.EqualTo(SpriteMeshType.FullRect), path);
            Assert.That(importer.spritesheet, Has.Length.EqualTo(expectedCount), path);

            foreach (var metadata in importer.spritesheet)
            {
                Assert.That(metadata.rect.width, Is.EqualTo(width), path);
                Assert.That(metadata.rect.height, Is.EqualTo(height), path);
            }

            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            Assert.That(sprites, Has.Length.EqualTo(expectedCount), path);
            Assert.That(sprites.Select(sprite => sprite.name).Distinct().Count(), Is.EqualTo(expectedCount), path);
        }

        private static SpriteMeshType ReadSpriteMeshType(TextureImporter importer)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            return settings.spriteMeshType;
        }

        private static void AssertAudioLength(string path, float expectedSeconds)
        {
            Assert.That(GetWavLength(path), Is.EqualTo(expectedSeconds).Within(0.001f), path);
        }

        private static float GetWavLength(string path)
        {
            var bytes = File.ReadAllBytes(ToAbsolutePath(path));
            return (bytes.Length - 44) / 2f / 44100f;
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        [Test]
        public void PresentationCatalog_ContainsRequiredVisuals()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PresentationCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            var clips = catalog.VisualClips.ToDictionary(value => value.Id, StringComparer.Ordinal);

            var required = new List<string>();
            foreach (var direction in Directions)
            {
                required.Add("world.player." + direction + ".idle");
                required.Add("world.player." + direction + ".walk");
            }
            foreach (var area in GroundNames)
                required.Add("world.ground." + area);
            foreach (var npc in NpcNames)
                required.Add("world.npc." + npc + ".idle");
            foreach (var marker in MarkerNames)
                required.Add("world.marker." + marker + ".idle");
            foreach (var unit in UnitNames)
                foreach (var state in new[] { "idle", "move", "attack", "hit", "down" })
                    required.Add("battle.unit." + unit + "." + state);
            required.AddRange(new[]
            {
                "ui.panel", "ui.button", "ui.button.hover", "ui.button.pressed", "ui.missing",
                "item.weapon", "item.armor", "item.accessory"
            });

            foreach (var id in required)
            {
                Assert.That(clips.ContainsKey(id), Is.True, id);
                Assert.That(clips[id].Frames, Is.Not.Empty, id);
                Assert.That(clips[id].Frames.All(frame => frame != null), Is.True, id);
            }

            foreach (var direction in Directions)
            {
                Assert.That(clips["world.player." + direction + ".idle"].Frames, Has.Length.EqualTo(2));
                Assert.That(clips["world.player." + direction + ".walk"].Frames, Has.Length.EqualTo(2));
            }
            foreach (var state in new[] { "idle", "move", "attack", "hit" })
                Assert.That(clips["battle.unit.warrior." + state].Frames, Has.Length.EqualTo(2));
            Assert.That(clips["battle.unit.warrior.down"].Frames, Has.Length.EqualTo(1));
        }

        [Test]
        public void PresentationCatalog_ContainsRequiredAudioCues()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PresentationCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            var cues = catalog.AudioCues.ToDictionary(value => value.Id, StringComparer.Ordinal);
            var expected = new[]
            {
                "bgm.menu|Assets/Audio/Placeholder/Music/bgm_menu.wav",
                "bgm.world.village|Assets/Audio/Placeholder/Music/bgm_world_village.wav",
                "bgm.world.forest|Assets/Audio/Placeholder/Music/bgm_world_forest.wav",
                "bgm.world.watchtower|Assets/Audio/Placeholder/Music/bgm_world_watchtower.wav",
                "bgm.world.crypt|Assets/Audio/Placeholder/Music/bgm_world_crypt.wav",
                "bgm.battle|Assets/Audio/Placeholder/Music/bgm_battle.wav",
                "bgm.victory|Assets/Audio/Placeholder/Music/bgm_victory.wav",
                "sfx.ui.click|Assets/Audio/Placeholder/Sfx/sfx_ui_click.wav",
                "sfx.ui.confirm|Assets/Audio/Placeholder/Sfx/sfx_ui_confirm.wav",
                "sfx.ui.cancel|Assets/Audio/Placeholder/Sfx/sfx_ui_cancel.wav",
                "sfx.ui.error|Assets/Audio/Placeholder/Sfx/sfx_ui_error.wav",
                "sfx.dialogue.page|Assets/Audio/Placeholder/Sfx/sfx_dialogue_page.wav",
                "sfx.shop.buy|Assets/Audio/Placeholder/Sfx/sfx_shop_buy.wav",
                "sfx.shop.sell|Assets/Audio/Placeholder/Sfx/sfx_shop_sell.wav",
                "sfx.inventory.equip|Assets/Audio/Placeholder/Sfx/sfx_inventory_equip.wav",
                "sfx.inventory.craft|Assets/Audio/Placeholder/Sfx/sfx_inventory_craft.wav",
                "sfx.world.reward|Assets/Audio/Placeholder/Sfx/sfx_world_reward.wav",
                "sfx.world.encounter|Assets/Audio/Placeholder/Sfx/sfx_world_encounter.wav",
                "sfx.battle.attack|Assets/Audio/Placeholder/Sfx/sfx_battle_attack.wav",
                "sfx.battle.hit|Assets/Audio/Placeholder/Sfx/sfx_battle_hit.wav",
                "sfx.battle.down|Assets/Audio/Placeholder/Sfx/sfx_battle_down.wav"
            };

            foreach (var entry in expected)
            {
                var pair = entry.Split('|');
                Assert.That(cues.ContainsKey(pair[0]), Is.True, pair[0]);
                Assert.That(AssetDatabase.GetAssetPath(cues[pair[0]].Clip), Is.EqualTo(pair[1]), pair[0]);
            }
            Assert.That(cues["bgm.victory"].Loop, Is.False);
            foreach (var id in cues.Keys.Where(id => id.StartsWith("bgm.", StringComparison.Ordinal) && id != "bgm.victory"))
            {
                Assert.That(cues[id].Loop, Is.True, id);
                Assert.That(cues[id].Channel, Is.EqualTo(AudioChannel.Music), id);
            }
        }

        [Test]
        public void PresentationCatalog_MapsContentIdsToGeneratedAssets()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PresentationCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            var service = new PresentationService(catalog, null);

            foreach (var area in GroundNames)
                Assert.That(service.GetAreaMusicCueId("area." + area), Is.EqualTo("bgm.world." + area), area);
            Assert.That(service.GetAreaMusicCueId("area.missing"), Is.EqualTo("bgm.menu"));

            foreach (var npc in new[] { "elder", "merchant", "blacksmith", "innkeeper", "ranger_companion", "mage_companion", "hunter", "survivor" })
            {
                Assert.That(service.GetNpcPortrait("npc." + npc), Is.Not.SameAs(service.MissingSprite), npc);
                Assert.That(service.GetVisualClip("world.npc." + NormalizeNpcName(npc) + ".idle").Frames, Has.Length.EqualTo(2), npc);
            }

            Assert.That(service.GetCharacterVisualPrefix("class.warrior"), Is.EqualTo("battle.unit.warrior"));
            Assert.That(service.GetCharacterVisualPrefix("class.ranger"), Is.EqualTo("battle.unit.ranger"));
            Assert.That(service.GetCharacterVisualPrefix("class.mage"), Is.EqualTo("battle.unit.mage"));
            Assert.That(service.GetCharacterVisualPrefix("enemy.bandit"), Is.EqualTo("battle.unit.bandit"));
            Assert.That(service.GetCharacterVisualPrefix("enemy.wolf"), Is.EqualTo("battle.unit.wolf"));
            Assert.That(service.GetCharacterVisualPrefix("enemy.crypt_boss"), Is.EqualTo("battle.unit.boss"));

            Assert.That(service.GetItemIcon("item.frost_longsword", "weapon"), Is.Not.SameAs(service.MissingSprite));
            Assert.That(service.GetItemIcon("item.leather_armor", "armor"), Is.Not.SameAs(service.MissingSprite));
            Assert.That(service.GetItemIcon("item.ember_charm", "accessory"), Is.Not.SameAs(service.MissingSprite));

            foreach (var part in new[] { "ui.panel", "ui.button", "ui.button.hover", "ui.button.pressed" })
                Assert.That(service.GetUiSprite(part), Is.Not.SameAs(service.MissingSprite), part);

            Assert.That(service.GetUiSprite("ui.missing"), Is.SameAs(service.MissingSprite));
            Assert.That(catalog.DefaultMusicCueId, Is.EqualTo("bgm.menu"));
            Assert.That(catalog.MissingSpriteId, Is.EqualTo("ui.missing"));
        }

        private static string NormalizeNpcName(string npc)
        {
            return npc.Replace("_companion", string.Empty);
        }
    }
}
