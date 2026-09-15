using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BorderValley.Presentation;
using UnityEditor;
using UnityEngine;

namespace BorderValley.Editor.Tools
{
    public static class PlaceholderAssetGenerator
    {
        private const string WorldRoot = "Assets/Art/Placeholder/World";
        private const string BattleRoot = "Assets/Art/Placeholder/Battle";
        private const string PortraitRoot = "Assets/Art/Placeholder/Portraits";
        private const string UiRoot = "Assets/Art/Placeholder/Ui";
        private const string ItemRoot = "Assets/Art/Placeholder/Items";
        private const string MusicRoot = "Assets/Audio/Placeholder/Music";
        private const string SfxRoot = "Assets/Audio/Placeholder/Sfx";
        private const string CatalogPath = "Assets/Resources/PresentationCatalog.asset";
        private const int PixelsPerUnit = 32;
        private const int SampleRate = 44100;

        private static readonly string[] Directions = { "south", "east", "north", "west" };
        private static readonly string[] NpcNames = { "elder", "merchant", "blacksmith", "innkeeper", "ranger", "mage", "hunter", "survivor" };
        private static readonly string[] GroundNames = { "village", "forest", "watchtower", "crypt" };
        private static readonly string[] MarkerNames = { "chest", "gather", "investigate", "area_exit", "encounter" };
        private static readonly string[] UnitNames = { "warrior", "ranger", "mage", "bandit", "wolf", "skeleton", "boss" };
        private static readonly string[] UnitStates = { "idle", "move", "attack", "hit", "down" };

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 Ink = new Color32(24, 22, 35, 255);
        private static readonly Color32 Light = new Color32(239, 226, 190, 255);
        private static readonly Color32 Magenta = new Color32(255, 0, 255, 255);
        private static readonly Color32[] NpcPalette =
        {
            Hex("516f8d"), Hex("8b5a3c"), Hex("8b3f32"), Hex("b08b50"),
            Hex("397c4b"), Hex("634b91"), Hex("76523d"), Hex("5c6670")
        };
        private static readonly Color32[] UnitPalette =
        {
            Hex("3f73b8"), Hex("4e9b62"), Hex("8155b4"), Hex("9b4a3f"),
            Hex("6e665d"), Hex("b8b39d"), Hex("b43f5e")
        };

        private sealed class MusicSpec
        {
            public MusicSpec(string fileName, float seconds, params double[] notes)
            {
                FileName = fileName;
                Seconds = seconds;
                Notes = notes;
            }

            public string FileName { get; }
            public float Seconds { get; }
            public double[] Notes { get; }
        }

        private sealed class SfxSpec
        {
            public SfxSpec(string fileName, float seconds, double frequency, bool noiseLike)
            {
                FileName = fileName;
                Seconds = seconds;
                Frequency = frequency;
                NoiseLike = noiseLike;
            }

            public string FileName { get; }
            public float Seconds { get; }
            public double Frequency { get; }
            public bool NoiseLike { get; }
        }

        private static readonly MusicSpec[] Music =
        {
            new MusicSpec("bgm_menu.wav", 8f, 220.00, 277.18, 329.63),
            new MusicSpec("bgm_world_village.wav", 8f, 261.63, 329.63, 392.00),
            new MusicSpec("bgm_world_forest.wav", 8f, 196.00, 246.94, 293.66),
            new MusicSpec("bgm_world_watchtower.wav", 8f, 174.61, 220.00, 261.63),
            new MusicSpec("bgm_world_crypt.wav", 8f, 146.83, 174.61, 220.00),
            new MusicSpec("bgm_battle.wav", 8f, 146.83, 196.00, 246.94),
            new MusicSpec("bgm_victory.wav", 4f, 261.63, 329.63, 392.00, 523.25)
        };

        private static readonly SfxSpec[] Sfx =
        {
            new SfxSpec("sfx_ui_click.wav", 0.08f, 880.00, false),
            new SfxSpec("sfx_ui_confirm.wav", 0.16f, 659.25, false),
            new SfxSpec("sfx_ui_cancel.wav", 0.14f, 392.00, false),
            new SfxSpec("sfx_ui_error.wav", 0.28f, 174.61, true),
            new SfxSpec("sfx_dialogue_page.wav", 0.12f, 523.25, false),
            new SfxSpec("sfx_shop_buy.wav", 0.22f, 783.99, false),
            new SfxSpec("sfx_shop_sell.wav", 0.22f, 587.33, false),
            new SfxSpec("sfx_inventory_equip.wav", 0.18f, 698.46, false),
            new SfxSpec("sfx_inventory_craft.wav", 0.30f, 440.00, true),
            new SfxSpec("sfx_world_reward.wav", 0.36f, 880.00, false),
            new SfxSpec("sfx_world_encounter.wav", 0.42f, 130.81, true),
            new SfxSpec("sfx_battle_attack.wav", 0.12f, 329.63, true),
            new SfxSpec("sfx_battle_hit.wav", 0.16f, 196.00, true),
            new SfxSpec("sfx_battle_down.wav", 0.50f, 110.00, true)
        };

        [MenuItem("BorderValley/Placeholder/Generate All")]
        public static void GenerateAll()
        {
            EnsureFolders();
            GenerateWorldArt();
            GenerateBattleArt();
            GeneratePortraits();
            GenerateUiArt();
            GenerateItemIcons();
            GenerateMusic();
            GenerateSfx();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            RebuildCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Art");
            EnsureFolder("Assets/Art/Placeholder");
            EnsureFolder(WorldRoot);
            EnsureFolder(BattleRoot);
            EnsureFolder(PortraitRoot);
            EnsureFolder(UiRoot);
            EnsureFolder(ItemRoot);
            EnsureFolder("Assets/Audio");
            EnsureFolder("Assets/Audio/Placeholder");
            EnsureFolder(MusicRoot);
            EnsureFolder(SfxRoot);
            EnsureFolder("Assets/Resources");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = path.Substring(0, path.LastIndexOf('/'));
            var name = path.Substring(path.LastIndexOf('/') + 1);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void GenerateWorldArt()
        {
            WriteSheet(WorldRoot + "/world_player.png", 512, 48, 16, 32, 48, (texture, frame) =>
            {
                var direction = frame / 4;
                var state = (frame % 4) / 2;
                var phase = frame % 2;
                DrawCharacter(texture, frame * 32, 0, NpcPalette[direction], state == 1, phase, true, direction);
            });

            for (var i = 0; i < GroundNames.Length; i++)
            {
                var seed = i;
                WriteSingle(WorldRoot + "/world_ground_" + GroundNames[i] + ".png", 32, 32,
                    (texture, x, y) => GroundPixel(texture, x, y, seed), false);
            }

            for (var i = 0; i < NpcNames.Length; i++)
            {
                var npc = i;
                WriteSheet(WorldRoot + "/world_npc_" + NpcNames[i] + ".png", 64, 48, 2, 32, 48,
                    (texture, frame) => DrawCharacter(texture, frame * 32, 0, NpcPalette[npc], false, frame, false, -1));
            }

            WriteSingle(WorldRoot + "/world_marker_chest.png", 32, 32, DrawChest, false);
            WriteSingle(WorldRoot + "/world_marker_gather.png", 32, 32, DrawGather, false);
            WriteSingle(WorldRoot + "/world_marker_investigate.png", 32, 32, DrawInvestigate, false);
            WriteSingle(WorldRoot + "/world_marker_area_exit.png", 32, 32, DrawAreaExit, false);
            WriteSingle(WorldRoot + "/world_marker_encounter.png", 32, 32, DrawEncounter, false);
        }

        private static void GenerateBattleArt()
        {
            for (var unit = 0; unit < UnitNames.Length; unit++)
            {
                var unitIndex = unit;
                WriteSheet(BattleRoot + "/battle_unit_" + UnitNames[unit] + ".png", 576, 64, 9, 64, 64,
                    (texture, frame) => DrawBattleUnit(texture, frame * 64, 0, unitIndex, frame));
            }
        }

        private delegate void TexturePainter(Texture2D texture, int offsetX, int offsetY);

        private static void WriteSingle(string path, int width, int height, TexturePainter painter, bool repeat)
        {
            WriteSingle(path, width, height, painter, repeat, Vector4.zero);
        }

        private static void WriteSingle(
            string path,
            int width,
            int height,
            TexturePainter painter,
            bool repeat,
            Vector4 border)
        {
            var texture = NewTexture(width, height);
            painter(texture, 0, 0);
            WriteTexture(path, texture, SpriteImportMode.Single, null, border, repeat);
        }

        private static void WriteSheet(
            string path,
            int width,
            int height,
            int frameCount,
            int frameWidth,
            int frameHeight,
            Action<Texture2D, int> painter)
        {
            var texture = NewTexture(width, height);
            for (var frame = 0; frame < frameCount; frame++)
                painter(texture, frame);

            var metadata = new SpriteMetaData[frameCount];
            var baseName = Path.GetFileNameWithoutExtension(path);
            for (var frame = 0; frame < frameCount; frame++)
            {
                metadata[frame] = new SpriteMetaData
                {
                    name = baseName + "_" + frame.ToString("00"),
                    rect = new Rect(frame * frameWidth, 0, frameWidth, frameHeight),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero
                };
            }

            WriteTexture(path, texture, SpriteImportMode.Multiple, metadata, Vector4.zero, false);
        }

        private static Texture2D NewTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = Clear;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static void WriteTexture(
            string path,
            Texture2D texture,
            SpriteImportMode mode,
            SpriteMetaData[] metadata,
            Vector4 border,
            bool repeat)
        {
            texture.Apply(false, false);
            var absolutePath = ToAbsolutePath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? string.Empty);
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporter(path, mode, metadata, border, repeat);
        }

        private static void ConfigureTextureImporter(
            string path,
            SpriteImportMode mode,
            SpriteMetaData[] metadata,
            Vector4 border,
            bool repeat)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Texture importer missing for " + path);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = mode;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            if (metadata != null)
                importer.spritesheet = metadata;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spritePixelsPerUnit = PixelsPerUnit;
            settings.spriteBorder = border;
            importer.SetTextureSettings(settings);
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        private static void DrawCharacter(
            Texture2D texture,
            int x,
            int y,
            Color32 color,
            bool moving,
            int phase,
            bool player,
            int direction)
        {
            var shift = moving && phase == 1 ? 1 : 0;
            var accent = Lighten(color, 38);
            DrawRect(texture, x + 7, y + 2, 17, 4, new Color32(0, 0, 0, 70));
            DrawRect(texture, x + 11, y + 9 + shift, 10, 16, color);
            DrawRect(texture, x + 9, y + 23 + shift, 14, 5, accent);
            DrawRect(texture, x + 12, y + 27 + shift, 8, 9, Lighten(color, 8));
            DrawRect(texture, x + 13, y + 30 + shift, 6, 5, new Color32(224, 174, 128, 255));
            DrawRect(texture, x + 12, y + 36 + shift, 8, 2, player ? Ink : Lighten(color, 55));
            DrawCharacterFace(texture, x, y + shift, direction);

            var leftFoot = phase == 0 ? 0 : 1;
            var rightFoot = phase == 0 ? 1 : 0;
            DrawRect(texture, x + 11 + leftFoot, y + 5, 4, 5, Ink);
            DrawRect(texture, x + 17 + rightFoot, y + 5, 4, 5, Ink);
            DrawRect(texture, x + 8, y + 17 + shift, 3, 10, accent);
            DrawRect(texture, x + 21, y + 17 + shift, 3, 10, accent);

            if (player)
            {
                DrawRect(texture, x + 23, y + 12 + shift, 2, 17, Hex("d9d2b8"));
                DrawRect(texture, x + 22, y + 27 + shift, 4, 2, Hex("8f5b31"));
            }
        }

        private static void DrawCharacterFace(Texture2D texture, int x, int y, int direction)
        {
            if (direction == 2)
            {
                DrawRect(texture, x + 13, y + 31, 6, 3, Lighten(NpcPalette[2], 55));
                return;
            }

            if (direction == 1)
            {
                DrawRect(texture, x + 17, y + 32, 2, 2, Ink);
                DrawRect(texture, x + 20, y + 33, 3, 2, Hex("d9a179"));
                return;
            }

            if (direction == 3)
            {
                DrawRect(texture, x + 13, y + 32, 2, 2, Ink);
                DrawRect(texture, x + 10, y + 33, 3, 2, Hex("d9a179"));
                return;
            }

            DrawRect(texture, x + 14, y + 32, 2, 2, Ink);
            DrawRect(texture, x + 18, y + 32, 2, 2, Ink);
        }
        private static void GroundPixel(Texture2D texture, int offsetX, int offsetY, int seed)
        {
            var bases = new[]
            {
                Hex("6f9958"), Hex("315f43"), Hex("77736c"), Hex("40354d")
            };
            var accents = new[]
            {
                Hex("d7b56d"), Hex("6f9958"), Hex("a58f70"), Hex("716080")
            };
            var baseColor = bases[seed];
            for (var y = 0; y < 32; y++)
            {
                for (var x = 0; x < 32; x++)
                {
                    var value = (x * 3 + y * 5 + seed * 11) % 17;
                    var color = value < 3 ? Lighten(baseColor, 16) : value > 14 ? Darken(baseColor, 12) : baseColor;
                    SetPixel(texture, offsetX + x, offsetY + y, color);
                }
            }

            for (var i = 0; i < 5; i++)
            {
                var x = (seed * 7 + i * 9) % 28;
                var y = (seed * 5 + i * 11) % 28;
                DrawRect(texture, offsetX + x, offsetY + y, 3 + i % 2, 2, accents[seed]);
            }

            DrawOutline(texture, offsetX, offsetY, 32, 32, Darken(baseColor, 24), 1);
        }

        private static void DrawChest(Texture2D texture, int x, int y)
        {
            DrawRect(texture, x + 5, y + 7, 22, 17, Hex("704526"));
            DrawRect(texture, x + 7, y + 9, 18, 13, Hex("a66b32"));
            DrawRect(texture, x + 5, y + 15, 22, 3, Ink);
            DrawRect(texture, x + 14, y + 13, 4, 7, Hex("e0bc59"));
            DrawOutline(texture, x + 5, y + 7, 22, 17, Ink, 2);
        }

        private static void DrawGather(Texture2D texture, int x, int y)
        {
            DrawRect(texture, x + 14, y + 7, 4, 18, Hex("4f7d3a"));
            DrawRect(texture, x + 7, y + 15, 10, 6, Hex("72a84e"));
            DrawRect(texture, x + 16, y + 18, 10, 6, Hex("8bc562"));
            DrawRect(texture, x + 11, y + 10, 9, 5, Hex("5d913f"));
            DrawRect(texture, x + 7, y + 5, 6, 6, Hex("d8b84f"));
            DrawOutline(texture, x + 14, y + 7, 4, 18, Ink, 1);
        }

        private static void DrawInvestigate(Texture2D texture, int x, int y)
        {
            DrawCircle(texture, x + 12, y + 18, 8, Hex("e7e2cf"), 2, Ink);
            DrawLine(texture, x + 18, y + 12, x + 26, y + 4, Ink, 4);
            DrawLine(texture, x + 19, y + 12, x + 26, y + 5, Hex("8b6844"), 2);
            DrawRect(texture, x + 11, y + 17, 3, 5, Ink);
        }

        private static void DrawAreaExit(Texture2D texture, int x, int y)
        {
            DrawRect(texture, x + 5, y + 5, 5, 24, Hex("8a867a"));
            DrawRect(texture, x + 22, y + 5, 5, 24, Hex("8a867a"));
            DrawRect(texture, x + 10, y + 25, 12, 4, Hex("8a867a"));
            DrawRect(texture, x + 12, y + 5, 8, 20, new Color32(16, 20, 34, 210));
            DrawRect(texture, x + 13, y + 2, 6, 4, Hex("d7c58c"));
        }

        private static void DrawEncounter(Texture2D texture, int x, int y)
        {
            DrawLine(texture, x + 7, y + 6, x + 25, y + 26, Ink, 4);
            DrawLine(texture, x + 25, y + 6, x + 7, y + 26, Ink, 4);
            DrawLine(texture, x + 8, y + 7, x + 24, y + 25, Hex("d8d4c4"), 2);
            DrawLine(texture, x + 24, y + 7, x + 8, y + 25, Hex("d8d4c4"), 2);
            DrawRect(texture, x + 13, y + 13, 6, 6, Hex("c94a52"));
        }

        private static void DrawBattleUnit(Texture2D texture, int x, int y, int paletteIndex, int frame)
        {
            var color = UnitPalette[paletteIndex];
            var state = frame / 2;
            if (frame == 8)
            {
                DrawRect(texture, x + 10, y + 9, 44, 16, new Color32(0, 0, 0, 70));
                DrawRect(texture, x + 14, y + 14, 36, 15, color);
                DrawRect(texture, x + 18, y + 27, 22, 10, Lighten(color, 18));
                DrawRect(texture, x + 15, y + 10, 8, 7, Lighten(color, 42));
                DrawRect(texture, x + 40, y + 10, 8, 7, Lighten(color, 42));
                return;
            }

            var phase = frame % 2;
            var offsetX = state == 2 && phase == 1 ? 3 : 0;
            var offsetY = state == 3 ? -3 - phase : 0;
            var body = state == 3 && phase == 0 ? Lighten(color, 70) : color;
            DrawRect(texture, x + 18, y + 7, 28, 4, new Color32(0, 0, 0, 65));
            DrawRect(texture, x + 24 + offsetX, y + 17 + offsetY, 16, 23, body);
            DrawRect(texture, x + 21 + offsetX, y + 34 + offsetY, 22, 7, Lighten(color, 30));
            DrawRect(texture, x + 26 + offsetX, y + 41 + offsetY, 12, 11, Hex("d7ad83"));
            DrawRect(texture, x + 24 + offsetX, y + 50 + offsetY, 16, 4, Lighten(color, 70));
            DrawRect(texture, x + 24 - (phase == 0 ? 2 : 0), y + 10, 6, 8, Ink);
            DrawRect(texture, x + 36 + (phase == 0 ? 2 : 0), y + 10, 6, 8, Ink);

            if (state == 2)
            {
                DrawLine(texture, x + 40, y + 31, x + 53 + phase * 3, y + 24 - phase * 2, Hex("d9d2b8"), 4);
                DrawRect(texture, x + 49 + phase * 3, y + 21 - phase * 2, 4, 8, Hex("8b5a34"));
            }

            if (state == 4)
                DrawCircle(texture, x + 31, y + 36, 11, new Color32(255, 255, 255, 0), 0, Ink);
        }

        private static void GeneratePortraits()
        {
            for (var i = 0; i < NpcNames.Length; i++)
            {
                var npcIndex = i;
                var path = PortraitRoot + "/portrait_npc_" + NpcNames[i] + ".png";
                WriteSingle(path, 256, 256, (texture, x, y) => DrawPortrait(texture, npcIndex), false);
            }
        }

        private static void DrawPortrait(Texture2D texture, int npcIndex)
        {
            var color = NpcPalette[npcIndex];
            var background = Darken(color, 42);
            Fill(texture, background);
            for (var y = 0; y < 256; y += 16)
            {
                for (var x = 0; x < 256; x += 16)
                {
                    if (((x / 16) + (y / 16)) % 2 == 0)
                        DrawRect(texture, x, y, 16, 16, Lighten(background, 6));
                }
            }

            DrawRect(texture, 54, 29, 148, 198, Ink);
            DrawRect(texture, 61, 36, 134, 184, Lighten(color, 15));
            DrawRect(texture, 83, 48, 90, 78, Hex("d4a47b"));
            DrawRect(texture, 74, 54, 108, 18, color);
            DrawRect(texture, 73, 72, 16, 45, Darken(color, 20));
            DrawRect(texture, 167, 72, 16, 45, Darken(color, 20));
            DrawRect(texture, 73, 126, 110, 94, color);
            DrawRect(texture, 93, 63, 12, 10, Ink);
            DrawRect(texture, 151, 63, 12, 10, Ink);
            DrawRect(texture, 112, 96, 32, 6, Darken(color, 38));
            DrawRect(texture, 89, 111, 78, 8, Lighten(color, 40));

            switch (npcIndex)
            {
                case 0:
                    DrawRect(texture, 72, 39, 112, 12, Hex("e7e2d5"));
                    break;
                case 1:
                    DrawRect(texture, 66, 45, 124, 12, Hex("a63428"));
                    break;
                case 2:
                    DrawRect(texture, 66, 37, 124, 20, Hex("4a4643"));
                    DrawRect(texture, 92, 18, 72, 24, Hex("4a4643"));
                    break;
                case 3:
                    DrawRect(texture, 69, 38, 118, 17, Hex("f0ead8"));
                    break;
                case 4:
                    DrawRect(texture, 72, 42, 112, 12, Hex("315c38"));
                    break;
                case 5:
                    DrawRect(texture, 64, 35, 128, 20, Hex("3f346b"));
                    DrawRect(texture, 100, 14, 56, 24, Hex("3f346b"));
                    break;
                case 6:
                    DrawRect(texture, 67, 42, 122, 15, Hex("4b3424"));
                    break;
                default:
                    DrawRect(texture, 68, 40, 120, 15, Hex("4e5558"));
                    DrawRect(texture, 82, 55, 14, 34, Hex("6d7477"));
                    break;
            }

            DrawOutline(texture, 54, 29, 148, 198, Ink, 4);
        }

        private static void GenerateUiArt()
        {
            WriteSingle(UiRoot + "/ui_panel.png", 16, 16, (texture, x, y) => DrawUiNineSlice(texture, 0), false, new Vector4(4f, 4f, 4f, 4f));
            WriteSingle(UiRoot + "/ui_button.png", 16, 16, (texture, x, y) => DrawUiNineSlice(texture, 1), false, new Vector4(4f, 4f, 4f, 4f));
            WriteSingle(UiRoot + "/ui_button_hover.png", 16, 16, (texture, x, y) => DrawUiNineSlice(texture, 2), false, new Vector4(4f, 4f, 4f, 4f));
            WriteSingle(UiRoot + "/ui_button_pressed.png", 16, 16, (texture, x, y) => DrawUiNineSlice(texture, 3), false, new Vector4(4f, 4f, 4f, 4f));
            WriteSingle(UiRoot + "/ui_missing.png", 32, 32, DrawMissing, false);
        }

        private static void DrawUiNineSlice(Texture2D texture, int style)
        {
            var fill = style == 0 ? Hex("20243a") : Hex("53647d");
            var edge = style == 0 ? Hex("7e8ba3") : Hex("d8c78f");
            var highlight = style == 0 ? Hex("3d4966") : Hex("778ca6");
            if (style == 2)
                fill = Lighten(fill, 16);
            if (style == 3)
            {
                fill = Darken(fill, 25);
                edge = Darken(edge, 20);
                highlight = Darken(highlight, 15);
            }

            Fill(texture, fill);
            DrawRect(texture, 1, 1, 14, 2, highlight);
            DrawRect(texture, 1, 1, 2, 14, highlight);
            DrawRect(texture, 1, 13, 14, 2, edge);
            DrawRect(texture, 13, 1, 2, 14, edge);
            SetPixel(texture, 0, 0, Clear);
            SetPixel(texture, 15, 0, Clear);
            SetPixel(texture, 0, 15, Clear);
            SetPixel(texture, 15, 15, Clear);
        }

        private static void DrawMissing(Texture2D texture, int x, int y)
        {
            for (var py = 0; py < 32; py++)
            {
                for (var px = 0; px < 32; px++)
                    SetPixel(texture, px, py, ((px / 8) + (py / 8)) % 2 == 0 ? Magenta : Ink);
            }
            DrawLine(texture, 7, 7, 24, 24, Light, 4);
            DrawLine(texture, 24, 7, 7, 24, Light, 4);
        }

        private static void GenerateItemIcons()
        {
            WriteSingle(ItemRoot + "/item_weapon.png", 32, 32, DrawWeaponIcon, false);
            WriteSingle(ItemRoot + "/item_armor.png", 32, 32, DrawArmorIcon, false);
            WriteSingle(ItemRoot + "/item_accessory.png", 32, 32, DrawAccessoryIcon, false);
        }

        private static void DrawWeaponIcon(Texture2D texture, int x, int y)
        {
            DrawLine(texture, x + 6, y + 25, x + 24, y + 5, Ink, 7);
            DrawLine(texture, x + 7, y + 24, x + 23, y + 6, Hex("d9e1e6"), 5);
            DrawLine(texture, x + 9, y + 24, x + 22, y + 10, Hex("f6f2df"), 2);
            DrawLine(texture, x + 5, y + 22, x + 15, y + 27, Hex("d6a445"), 3);
            DrawRect(texture, x + 4, y + 23, 4, 5, Hex("8f5d2e"));
        }

        private static void DrawArmorIcon(Texture2D texture, int x, int y)
        {
            DrawRect(texture, x + 8, y + 7, 16, 18, Hex("8b98aa"));
            DrawRect(texture, x + 4, y + 10, 6, 12, Hex("69798d"));
            DrawRect(texture, x + 22, y + 10, 6, 12, Hex("69798d"));
            DrawRect(texture, x + 10, y + 5, 5, 5, Hex("b9c4cf"));
            DrawRect(texture, x + 17, y + 5, 5, 5, Hex("b9c4cf"));
            DrawLine(texture, x + 16, y + 9, x + 16, y + 24, Hex("c6ced5"), 2);
            DrawOutline(texture, x + 8, y + 7, 16, 18, Ink, 2);
        }

        private static void DrawAccessoryIcon(Texture2D texture, int x, int y)
        {
            DrawCircle(texture, x + 16, y + 17, 8, new Color32(0, 0, 0, 0), 0, Hex("e1b84e"));
            DrawCircle(texture, x + 16, y + 17, 5, new Color32(0, 0, 0, 0), 3, Hex("e1b84e"));
            DrawRect(texture, x + 12, y + 3, 8, 8, Hex("c94a72"));
            DrawRect(texture, x + 14, y + 5, 4, 4, Hex("f08fb0"));
            DrawLine(texture, x + 16, y + 11, x + 16, y + 14, Ink, 2);
        }

        private static void GenerateMusic()
        {
            foreach (var spec in Music)
            {
                WriteWav(MusicRoot + "/" + spec.FileName, spec.Seconds, (time, duration) =>
                {
                    var noteIndex = Math.Min(spec.Notes.Length - 1, (int)(time * 2d) % spec.Notes.Length);
                    var beat = ((int)(time * 2d) % 2 == 0) ? 1d : 0.72d;
                    var value = Triangle(time, spec.Notes[noteIndex]) * 0.20d * beat;
                    value += Triangle(time, spec.Notes[(noteIndex + 1) % spec.Notes.Length] * 0.5d) * 0.08d;
                    var fade = Math.Min(1d, time / 0.08d) * Math.Min(1d, (duration - time) / 0.08d);
                    return value * Math.Max(0d, fade);
                });
            }
        }

        private static void GenerateSfx()
        {
            foreach (var spec in Sfx)
            {
                WriteWav(SfxRoot + "/" + spec.FileName, spec.Seconds, (time, duration) =>
                {
                    var decay = Math.Exp(-8d * time / duration);
                    var frequency = spec.Frequency * (1d + time * 1.6d);
                    var wave = spec.NoiseLike
                        ? Square(time, frequency) * 0.45d + Triangle(time, frequency * 0.51d) * 0.35d
                        : Triangle(time, frequency);
                    return wave * decay * 0.42d;
                });
            }
        }

        private static void WriteWav(string path, float seconds, Func<double, double, double> sampler)
        {
            var sampleCount = (int)Math.Round(seconds * SampleRate);
            var dataSize = sampleCount * sizeof(short);
            var absolutePath = ToAbsolutePath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? string.Empty);
            using (var stream = File.Create(absolutePath))
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, false))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(SampleRate);
                writer.Write(SampleRate * sizeof(short));
                writer.Write((short)sizeof(short));
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);

                for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    var time = sampleIndex / (double)SampleRate;
                    var value = Math.Max(-1d, Math.Min(1d, sampler(time, seconds)));
                    writer.Write((short)Math.Round(value * short.MaxValue));
                }
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        private static double Triangle(double time, double frequency)
        {
            var phase = time * frequency - Math.Floor(time * frequency);
            return 1d - 4d * Math.Abs(phase - 0.5d);
        }

        private static double Square(double time, double frequency)
        {
            return time * frequency - Math.Floor(time * frequency) < 0.5d ? 1d : -1d;
        }

        private static void RebuildCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PresentationCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var missingSprite = LoadSingleSprite(UiRoot + "/ui_missing.png");
            var visuals = new List<VisualClipDefinition>();
            var playerFrames = LoadSheetSprites(WorldRoot + "/world_player.png");
            for (var direction = 0; direction < Directions.Length; direction++)
            {
                for (var state = 0; state < 2; state++)
                {
                    var stateName = state == 0 ? "idle" : "walk";
                    var frames = playerFrames.Skip(direction * 4 + state * 2).Take(2).ToArray();
                    visuals.Add(new VisualClipDefinition(
                        "world.player." + Directions[direction] + "." + stateName,
                        frames,
                        state == 0 ? 3f : 8f,
                        true,
                        missingSprite));
                }
            }

            foreach (var ground in GroundNames)
            {
                visuals.Add(StaticClip(
                    "world.ground." + ground,
                    LoadSingleSprite(WorldRoot + "/world_ground_" + ground + ".png"),
                    missingSprite));
            }

            foreach (var npc in NpcNames)
            {
                visuals.Add(new VisualClipDefinition(
                    "world.npc." + npc + ".idle",
                    LoadSheetSprites(WorldRoot + "/world_npc_" + npc + ".png"),
                    2f,
                    true,
                    missingSprite));
                visuals.Add(StaticClip(
                    "portrait.npc." + npc,
                    LoadSingleSprite(PortraitRoot + "/portrait_npc_" + npc + ".png"),
                    missingSprite));
            }

            foreach (var marker in MarkerNames)
            {
                visuals.Add(StaticClip(
                    "world.marker." + marker + ".idle",
                    LoadSingleSprite(WorldRoot + "/world_marker_" + marker + ".png"),
                    missingSprite));
            }

            foreach (var unit in UnitNames)
            {
                var frames = LoadSheetSprites(BattleRoot + "/battle_unit_" + unit + ".png");
                visuals.Add(new VisualClipDefinition("battle.unit." + unit + ".idle", frames.Skip(0).Take(2).ToArray(), 4f, true, missingSprite));
                visuals.Add(new VisualClipDefinition("battle.unit." + unit + ".move", frames.Skip(2).Take(2).ToArray(), 8f, true, missingSprite));
                visuals.Add(new VisualClipDefinition("battle.unit." + unit + ".attack", frames.Skip(4).Take(2).ToArray(), 10f, true, missingSprite));
                visuals.Add(new VisualClipDefinition("battle.unit." + unit + ".hit", frames.Skip(6).Take(2).ToArray(), 12f, true, missingSprite));
                visuals.Add(new VisualClipDefinition("battle.unit." + unit + ".down", frames.Skip(8).Take(1).ToArray(), 1f, false, missingSprite));
            }

            visuals.Add(StaticClip("ui.panel", LoadSingleSprite(UiRoot + "/ui_panel.png"), missingSprite));
            visuals.Add(StaticClip("ui.button", LoadSingleSprite(UiRoot + "/ui_button.png"), missingSprite));
            visuals.Add(StaticClip("ui.button.hover", LoadSingleSprite(UiRoot + "/ui_button_hover.png"), missingSprite));
            visuals.Add(StaticClip("ui.button.pressed", LoadSingleSprite(UiRoot + "/ui_button_pressed.png"), missingSprite));
            visuals.Add(StaticClip("ui.missing", missingSprite, missingSprite));
            visuals.Add(StaticClip("item.weapon", LoadSingleSprite(ItemRoot + "/item_weapon.png"), missingSprite));
            visuals.Add(StaticClip("item.armor", LoadSingleSprite(ItemRoot + "/item_armor.png"), missingSprite));
            visuals.Add(StaticClip("item.accessory", LoadSingleSprite(ItemRoot + "/item_accessory.png"), missingSprite));

            catalog.EditorSetVisualClips(visuals);
            catalog.EditorSetAudioCues(CreateAudioCues());
            catalog.EditorSetMapping("areaMusic", new[]
            {
                new StringPair("area.village", "bgm.world.village"),
                new StringPair("area.forest", "bgm.world.forest"),
                new StringPair("area.watchtower", "bgm.world.watchtower"),
                new StringPair("area.crypt", "bgm.world.crypt")
            });
            catalog.EditorSetMapping("npcVisuals", CreateNpcVisualMappings());
            catalog.EditorSetMapping("npcPortraits", CreateNpcPortraitMappings());
            catalog.EditorSetMapping("characterVisuals", CreateCharacterMappings());
            catalog.EditorSetMapping("itemIcons", CreateItemIconMappings());
            catalog.EditorSetMapping("uiSprites", new[]
            {
                new StringPair("ui.panel", "ui.panel"),
                new StringPair("ui.button", "ui.button"),
                new StringPair("ui.button.hover", "ui.button.hover"),
                new StringPair("ui.button.pressed", "ui.button.pressed"),
                new StringPair("ui.missing", "ui.missing")
            });
            catalog.EditorSetDefaultMusicCueId("bgm.menu");
            catalog.EditorSetMissingSpriteId("ui.missing");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static List<AudioCueDefinition> CreateAudioCues()
        {
            var cues = new List<AudioCueDefinition>();
            AddCue(cues, "bgm.menu", "bgm_menu.wav", true, AudioChannel.Music, 0.38f);
            AddCue(cues, "bgm.world.village", "bgm_world_village.wav", true, AudioChannel.Music, 0.38f);
            AddCue(cues, "bgm.world.forest", "bgm_world_forest.wav", true, AudioChannel.Music, 0.38f);
            AddCue(cues, "bgm.world.watchtower", "bgm_world_watchtower.wav", true, AudioChannel.Music, 0.38f);
            AddCue(cues, "bgm.world.crypt", "bgm_world_crypt.wav", true, AudioChannel.Music, 0.38f);
            AddCue(cues, "bgm.battle", "bgm_battle.wav", true, AudioChannel.Music, 0.38f);
            AddCue(cues, "bgm.victory", "bgm_victory.wav", false, AudioChannel.Music, 0.42f);
            AddCue(cues, "sfx.ui.click", "sfx_ui_click.wav", false, AudioChannel.Ui, 0.65f);
            AddCue(cues, "sfx.ui.confirm", "sfx_ui_confirm.wav", false, AudioChannel.Ui, 0.65f);
            AddCue(cues, "sfx.ui.cancel", "sfx_ui_cancel.wav", false, AudioChannel.Ui, 0.65f);
            AddCue(cues, "sfx.ui.error", "sfx_ui_error.wav", false, AudioChannel.Ui, 0.65f);
            AddCue(cues, "sfx.dialogue.page", "sfx_dialogue_page.wav", false, AudioChannel.Ui, 0.60f);
            AddCue(cues, "sfx.shop.buy", "sfx_shop_buy.wav", false, AudioChannel.Ui, 0.65f);
            AddCue(cues, "sfx.shop.sell", "sfx_shop_sell.wav", false, AudioChannel.Ui, 0.65f);
            AddCue(cues, "sfx.inventory.equip", "sfx_inventory_equip.wav", false, AudioChannel.Ui, 0.65f);
            AddCue(cues, "sfx.inventory.craft", "sfx_inventory_craft.wav", false, AudioChannel.Ui, 0.65f);
            AddCue(cues, "sfx.world.reward", "sfx_world_reward.wav", false, AudioChannel.World, 0.65f);
            AddCue(cues, "sfx.world.encounter", "sfx_world_encounter.wav", false, AudioChannel.World, 0.70f);
            AddCue(cues, "sfx.battle.attack", "sfx_battle_attack.wav", false, AudioChannel.Battle, 0.70f);
            AddCue(cues, "sfx.battle.hit", "sfx_battle_hit.wav", false, AudioChannel.Battle, 0.70f);
            AddCue(cues, "sfx.battle.down", "sfx_battle_down.wav", false, AudioChannel.Battle, 0.70f);
            return cues;
        }

        private static void AddCue(
            ICollection<AudioCueDefinition> cues,
            string id,
            string fileName,
            bool loop,
            AudioChannel channel,
            float volume)
        {
            var root = channel == AudioChannel.Music ? MusicRoot : SfxRoot;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(root + "/" + fileName);
            if (clip == null)
                throw new InvalidOperationException("Audio clip was not imported: " + root + "/" + fileName);
            cues.Add(new AudioCueDefinition(id, clip, volume, loop, channel));
        }

        private static IEnumerable<StringPair> CreateNpcVisualMappings()
        {
            var mappings = new List<StringPair>();
            foreach (var npc in NpcNames)
            {
                mappings.Add(new StringPair("npc." + npc, "world.npc." + npc + ".idle"));
            }
            mappings.Add(new StringPair("npc.ranger_companion", "world.npc.ranger.idle"));
            mappings.Add(new StringPair("npc.mage_companion", "world.npc.mage.idle"));
            return mappings;
        }

        private static IEnumerable<StringPair> CreateNpcPortraitMappings()
        {
            var mappings = new List<StringPair>();
            foreach (var npc in NpcNames)
                mappings.Add(new StringPair("npc." + npc, "portrait.npc." + npc));
            mappings.Add(new StringPair("npc.ranger_companion", "portrait.npc.ranger"));
            mappings.Add(new StringPair("npc.mage_companion", "portrait.npc.mage"));
            return mappings;
        }

        private static IEnumerable<StringPair> CreateCharacterMappings()
        {
            return new[]
            {
                new StringPair("class.warrior", "battle.unit.warrior"),
                new StringPair("class.ranger", "battle.unit.ranger"),
                new StringPair("class.mage", "battle.unit.mage"),
                new StringPair("enemy.bandit", "battle.unit.bandit"),
                new StringPair("enemy.ranger", "battle.unit.ranger"),
                new StringPair("enemy.mage", "battle.unit.mage"),
                new StringPair("enemy.wolf", "battle.unit.wolf"),
                new StringPair("enemy.skeleton", "battle.unit.skeleton"),
                new StringPair("enemy.crypt_boss", "battle.unit.boss"),
                new StringPair("enemy.boss", "battle.unit.boss")
            };
        }

        private static IEnumerable<StringPair> CreateItemIconMappings()
        {
            return new[]
            {
                new StringPair("item.wooden_buckler", "item.armor"),
                new StringPair("item.iron_helmet", "item.armor"),
                new StringPair("item.leather_armor", "item.armor"),
                new StringPair("item.swift_boots", "item.armor"),
                new StringPair("item.frost_longsword", "item.weapon"),
                new StringPair("item.oak_staff", "item.weapon"),
                new StringPair("item.hunter_bow", "item.weapon"),
                new StringPair("item.ember_charm", "item.accessory"),
                new StringPair("weapon", "item.weapon"),
                new StringPair("shield", "item.armor"),
                new StringPair("helmet", "item.armor"),
                new StringPair("armor", "item.armor"),
                new StringPair("accessory", "item.accessory"),
                new StringPair("boots", "item.armor")
            };
        }

        private static VisualClipDefinition StaticClip(string id, Sprite sprite, Sprite fallback)
        {
            if (sprite == null)
                throw new InvalidOperationException("Sprite was not imported for clip " + id);
            return new VisualClipDefinition(id, new[] { sprite }, 1f, false, fallback ?? sprite);
        }

        private static Sprite LoadSingleSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new InvalidOperationException("Sprite was not imported: " + path);
            return sprite;
        }

        private static Sprite[] LoadSheetSprites(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
                .ToArray();
        }

        private static void Fill(Texture2D texture, Color32 color)
        {
            DrawRect(texture, 0, 0, texture.width, texture.height, color);
        }

        private static void DrawRect(Texture2D texture, int x, int y, int width, int height, Color32 color)
        {
            for (var py = y; py < y + height; py++)
            {
                for (var px = x; px < x + width; px++)
                    SetPixel(texture, px, py, color);
            }
        }

        private static void DrawOutline(
            Texture2D texture,
            int x,
            int y,
            int width,
            int height,
            Color32 color,
            int thickness)
        {
            DrawRect(texture, x, y, width, thickness, color);
            DrawRect(texture, x, y + height - thickness, width, thickness, color);
            DrawRect(texture, x, y, thickness, height, color);
            DrawRect(texture, x + width - thickness, y, thickness, height, color);
        }

        private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color32 color, int thickness)
        {
            var dx = Math.Abs(x1 - x0);
            var sx = x0 < x1 ? 1 : -1;
            var dy = -Math.Abs(y1 - y0);
            var sy = y0 < y1 ? 1 : -1;
            var error = dx + dy;
            while (true)
            {
                DrawRect(texture, x0 - thickness / 2, y0 - thickness / 2, thickness, thickness, color);
                if (x0 == x1 && y0 == y1)
                    break;
                var twice = 2 * error;
                if (twice >= dy)
                {
                    error += dy;
                    x0 += sx;
                }
                if (twice <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private static void DrawCircle(
            Texture2D texture,
            int centerX,
            int centerY,
            int radius,
            Color32 fill,
            int thickness,
            Color32 stroke)
        {
            for (var y = -radius; y <= radius; y++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    var distance = x * x + y * y;
                    if (distance > radius * radius)
                        continue;
                    var color = thickness > 0 && distance >= (radius - thickness) * (radius - thickness)
                        ? stroke
                        : fill;
                    SetPixel(texture, centerX + x, centerY + y, color);
                }
            }
        }

        private static void SetPixel(Texture2D texture, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
                return;
            texture.SetPixel(x, y, color);
        }

        private static Color32 Hex(string value)
        {
            if (value.Length != 6)
                throw new ArgumentException("Color must contain six hex digits.", nameof(value));
            return new Color32(
                Convert.ToByte(value.Substring(0, 2), 16),
                Convert.ToByte(value.Substring(2, 2), 16),
                Convert.ToByte(value.Substring(4, 2), 16),
                255);
        }

        private static Color32 Lighten(Color32 color, int amount)
        {
            return new Color32(
                (byte)Math.Min(255, color.r + amount),
                (byte)Math.Min(255, color.g + amount),
                (byte)Math.Min(255, color.b + amount),
                color.a);
        }

        private static Color32 Darken(Color32 color, int amount)
        {
            return new Color32(
                (byte)Math.Max(0, color.r - amount),
                (byte)Math.Max(0, color.g - amount),
                (byte)Math.Max(0, color.b - amount),
                color.a);
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
