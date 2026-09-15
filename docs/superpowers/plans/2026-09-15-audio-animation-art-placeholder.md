# 音频、动画、美术占位替换 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** 将白块和文字占位替换为固定规格、可替换的像素占位素材，并加入事件驱动的动画和基础音频反馈。

**Architecture:** 新增 BorderValley.Presentation 程序集负责 Catalog、只读表现服务、音频路由和 Sprite 动画。UI 程序集只通过 IPresentationService 查询素材和播放声音。素材由 Editor 工具确定性生成，最终替换只修改 PNG、WAV 和 PresentationCatalog。

**Tech Stack:** Unity 6000.6.0f1、C#、Unity UI、Unity Test Framework、Git LFS、Android ARM64 IL2CPP。

**Spec:** docs/superpowers/specs/2026-09-15-audio-animation-art-placeholder-design.md

## Global Constraints

- 新子智能体必须使用 model deepseek-flash、reasoning_effort high。
- 当前分支是 codex/audio-animation-art-placeholder，禁止在 main 上实现。
- 每个 task 先写失败测试，再实现，再运行对应测试。
- 每个 task 独立提交，所有 Unity 新文件必须提交对应 .meta。
- Domain、Battle、Inventory、Narrative 不得引用 UnityEngine 表现类型。
- 玩法数值、战斗公式、任务、掉落和 SaveService schema 不得改变。
- 玩家可见文字继续使用本地化键，不得新增硬编码文案。
- PNG、WAV、TTF 使用现有 Git LFS 规则。
- 测试日志、XML、APK、截图和 .superpowers 不提交。
- 遇到真机安装、触控或稳定性测试时，暂停并通知用户。
- 最终必须运行完整 EditMode、PlayMode、Android 构建和整分支 review。

---

## 文件结构

新增 Runtime：

- BorderValley/Assets/Code/Presentation/BorderValley.Presentation.asmdef
- BorderValley/Assets/Code/Presentation/Catalog/PresentationCatalog.cs
- BorderValley/Assets/Code/Presentation/Catalog/PresentationCatalogValidator.cs
- BorderValley/Assets/Code/Presentation/Catalog/VisualClipDefinition.cs
- BorderValley/Assets/Code/Presentation/Catalog/AudioCueDefinition.cs
- BorderValley/Assets/Code/Presentation/Catalog/PresentationMappings.cs
- BorderValley/Assets/Code/Presentation/Runtime/IPresentationService.cs
- BorderValley/Assets/Code/Presentation/Runtime/VisualClip.cs
- BorderValley/Assets/Code/Presentation/Runtime/AudioCue.cs
- BorderValley/Assets/Code/Presentation/Runtime/PresentationService.cs
- BorderValley/Assets/Code/Presentation/Runtime/NullPresentationService.cs
- BorderValley/Assets/Code/Presentation/Audio/IAudioOutput.cs
- BorderValley/Assets/Code/Presentation/Audio/AudioDirector.cs
- BorderValley/Assets/Code/Presentation/Audio/UnityAudioOutput.cs
- BorderValley/Assets/Code/Presentation/Boot/PresentationBootstrapInstaller.cs
- BorderValley/Assets/Code/Presentation/Animation/SpriteAnimator.cs

新增 Editor：

- BorderValley/Assets/Editor/Tools/PlaceholderAssetGenerator.cs
- BorderValley/Assets/Editor/Tools/PresentationBootSceneBuilder.cs

新增 Assets：

- BorderValley/Assets/Art/Placeholder/World
- BorderValley/Assets/Art/Placeholder/Battle
- BorderValley/Assets/Art/Placeholder/Portraits
- BorderValley/Assets/Art/Placeholder/Ui
- BorderValley/Assets/Art/Placeholder/Items
- BorderValley/Assets/Audio/Placeholder/Music
- BorderValley/Assets/Audio/Placeholder/Sfx
- BorderValley/Assets/Resources/PresentationCatalog.asset

新增测试：

- BorderValley/Assets/Tests/EditMode/Presentation/PresentationCatalogTests.cs
- BorderValley/Assets/Tests/EditMode/Presentation/PresentationServiceTests.cs
- BorderValley/Assets/Tests/EditMode/Presentation/AudioDirectorTests.cs
- BorderValley/Assets/Tests/EditMode/Presentation/SpriteAnimatorTests.cs
- BorderValley/Assets/Tests/EditMode/Presentation/PlaceholderAssetTests.cs
- BorderValley/Assets/Tests/EditMode/UI/PresentationUiTests.cs
- BorderValley/Assets/Tests/EditMode/UI/BattlePresentationTrackerTests.cs
- BorderValley/Assets/Tests/PlayMode/PresentationFlowTests.cs

修改：

- BorderValley/Assets/Code/UI/BorderValley.UI.asmdef
- BorderValley/Assets/Editor/BorderValley.Editor.asmdef
- BorderValley/Assets/Tests/EditMode/BorderValley.EditModeTests.asmdef
- BorderValley/Assets/Tests/PlayMode/BorderValley.PlayModeTests.asmdef
- BorderValley/Assets/Code/UI/MainMenuView.cs
- BorderValley/Assets/Code/UI/World/WorldExplorationController.cs
- BorderValley/Assets/Code/UI/World/WorldMapView.cs
- BorderValley/Assets/Code/UI/World/VirtualJoystick.cs
- BorderValley/Assets/Code/UI/World/DialoguePanelView.cs
- BorderValley/Assets/Code/UI/World/DialogueUiPresenter.cs
- BorderValley/Assets/Code/UI/World/ShopPanelView.cs
- BorderValley/Assets/Code/UI/World/ShopUiPresenter.cs
- BorderValley/Assets/Code/UI/World/QuestLogPanelView.cs
- BorderValley/Assets/Code/UI/Inventory/InventoryPanelView.cs
- BorderValley/Assets/Code/UI/Inventory/InventoryUiPresenter.cs
- BorderValley/Assets/Code/UI/Battle/BattleGridView.cs
- BorderValley/Assets/Code/UI/Battle/BattleHudView.cs
- BorderValley/Assets/Code/UI/Battle/BattleSceneController.cs
- BorderValley/Assets/Editor/Tools/FoundationSceneBuilder.cs
- BorderValley/Assets/Scenes/Boot.unity

---

### Task 1: Presentation Catalog 和只读服务契约

**Files:**

- Create: BorderValley/Assets/Code/Presentation/BorderValley.Presentation.asmdef
- Create: BorderValley/Assets/Code/Presentation/Catalog/VisualClipDefinition.cs
- Create: BorderValley/Assets/Code/Presentation/Catalog/AudioCueDefinition.cs
- Create: BorderValley/Assets/Code/Presentation/Catalog/PresentationMappings.cs
- Create: BorderValley/Assets/Code/Presentation/Catalog/PresentationCatalog.cs
- Create: BorderValley/Assets/Code/Presentation/Catalog/PresentationCatalogValidator.cs
- Create: BorderValley/Assets/Code/Presentation/Runtime/VisualClip.cs
- Create: BorderValley/Assets/Code/Presentation/Runtime/AudioCue.cs
- Create: BorderValley/Assets/Code/Presentation/Runtime/IPresentationService.cs
- Create: BorderValley/Assets/Code/Presentation/Runtime/PresentationService.cs
- Create: BorderValley/Assets/Code/Presentation/Runtime/NullPresentationService.cs
- Test: BorderValley/Assets/Tests/EditMode/Presentation/PresentationCatalogTests.cs
- Test: BorderValley/Assets/Tests/EditMode/Presentation/PresentationServiceTests.cs
- Modify: BorderValley/Assets/Tests/EditMode/BorderValley.EditModeTests.asmdef

**Interfaces:**

- Consumes: BorderValley.Core.GameContext
- Produces: IPresentationService、PresentationService、PresentationCatalog、PresentationCatalogValidator、VisualClip、AudioCue。

**Step 1: 写失败测试**

在 PresentationCatalogTests.cs 写入：

~~~csharp
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
~~~

在 PresentationServiceTests.cs 写入：

~~~csharp
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
~~~

**Step 2: 运行测试确认 RED**

Run:

~~~powershell
& '.\scripts\run-tests.ps1'
~~~

Expected: FAIL，原因包含 BorderValley.Presentation 或 PresentationCatalog 尚不存在。

**Step 3: 实现程序集和 Catalog**

BorderValley.Presentation.asmdef：

~~~json
{
  "name": "BorderValley.Presentation",
  "rootNamespace": "BorderValley.Presentation",
  "references": ["BorderValley.Core"],
  "autoReferenced": true
}
~~~

VisualClipDefinition 使用以下公开契约：

~~~csharp
public sealed class VisualClipDefinition
{
    public string Id { get; }
    public Sprite[] Frames { get; }
    public float FramesPerSecond { get; }
    public bool Loop { get; }
    public Sprite Fallback { get; }
    public static VisualClipDefinition CreateForTests(string id, Sprite fallback);
}
~~~

AudioCueDefinition 使用以下公开契约：

~~~csharp
public enum AudioChannel { Music, Ui, World, Battle }

public sealed class AudioCueDefinition
{
    public string Id { get; }
    public AudioClip Clip { get; }
    public float Volume { get; }
    public bool Loop { get; }
    public AudioChannel Channel { get; }
    public static AudioCueDefinition CreateForTests(
        string id,
        AudioClip clip,
        float volume = 1f,
        bool loop = false,
        AudioChannel channel = AudioChannel.Ui);
}
~~~

PresentationCatalog 保存以下列表和映射：

- List<VisualClipDefinition> visualClips
- List<AudioCueDefinition> audioCues
- List<StringPair> areaMusic
- List<StringPair> npcVisuals
- List<StringPair> npcPortraits
- List<StringPair> characterVisuals
- List<StringPair> itemIcons
- List<StringPair> uiSprites
- string defaultMusicCueId
- string missingSpriteId

为 ScriptableObject 提供 UNITY_EDITOR 下的 EditorSetVisualClips、EditorSetAudioCues、EditorSetMapping、EditorSetDefaultMusicCueId、EditorSetMissingSpriteId 方法。

IPresentationService 精确使用设计文档 4.1 的签名。PresentationService 构造时把 Catalog 转换为 Ordinal 字典，并创建 2x2 洋红色 missing Sprite。所有查询只读且不分配新集合。

PresentationCatalogValidator 至少返回以下 Code：

- duplicate_visual_id
- duplicate_audio_id
- missing_visual_frames
- invalid_visual_fps
- invalid_audio_volume
- looping_music_not_loopable

**Step 4: 运行测试确认 GREEN**

先运行完整测试确认新程序集可编译，再运行目标测试类。目标 EditMode 过滤使用 NUnit 全名边框：

~~~powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' -batchmode -nographics -projectPath '.\BorderValley' -runTests -testPlatform EditMode -testFilter 'BorderValley.Presentation.Tests.PresentationCatalogTests;BorderValley.Presentation.Tests.PresentationServiceTests' -testResults '.\presentation-task1-results.xml' -logFile '.\presentation-task1.log'
~~~

Expected: PASS，且完整 EditMode 原有 449 个测试继续通过。

**Step 5: 提交**

~~~powershell
git add BorderValley/Assets/Code/Presentation BorderValley/Assets/Tests/EditMode/Presentation BorderValley/Assets/Tests/EditMode/BorderValley.EditModeTests.asmdef
git commit -m "feat: add presentation catalog contracts"
~~~

---

### Task 2: 确定性占位素材生成器和 Catalog 资源

**Files:**

- Create: BorderValley/Assets/Editor/Tools/PlaceholderAssetGenerator.cs
- Modify: BorderValley/Assets/Editor/BorderValley.Editor.asmdef
- Create: BorderValley/Assets/Art/Placeholder/World/*.png
- Create: BorderValley/Assets/Art/Placeholder/Battle/*.png
- Create: BorderValley/Assets/Art/Placeholder/Portraits/*.png
- Create: BorderValley/Assets/Art/Placeholder/Ui/*.png
- Create: BorderValley/Assets/Art/Placeholder/Items/*.png
- Create: BorderValley/Assets/Audio/Placeholder/Music/*.wav
- Create: BorderValley/Assets/Audio/Placeholder/Sfx/*.wav
- Create: BorderValley/Assets/Resources/PresentationCatalog.asset
- Test: BorderValley/Assets/Tests/EditMode/Presentation/PlaceholderAssetTests.cs

**Interfaces:**

- Consumes: VisualClipDefinition、AudioCueDefinition、PresentationCatalog。
- Produces: PlaceholderAssetGenerator.GenerateAll() 和 Resources/PresentationCatalog.asset。

**Step 1: 写失败素材测试**

PlaceholderAssetTests.cs 使用 AssetDatabase：

~~~csharp
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BorderValley.Presentation.Tests
{
    public sealed class PlaceholderAssetTests
    {
        [TestCase("Assets/Art/Placeholder/World/world_player.png", 512, 48)]
        [TestCase("Assets/Art/Placeholder/Battle/battle_unit_warrior.png", 576, 64)]
        [TestCase("Assets/Art/Placeholder/Portraits/portrait_npc_elder.png", 256, 256)]
        [TestCase("Assets/Art/Placeholder/Ui/ui_panel.png", 16, 16)]
        public void GeneratedTexture_HasExpectedSize(string path, int width, int height)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture, Is.Not.Null, path);
            Assert.That(texture.width, Is.EqualTo(width));
            Assert.That(texture.height, Is.EqualTo(height));
        }

        [Test]
        public void GeneratedTextures_UsePointFilterAndNoMipmaps()
        {
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Placeholder" });
            Assert.That(paths, Is.Not.Empty);
            foreach (var guid in paths)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
            }
        }
    }
}
~~~

**Step 2: 运行测试确认 RED**

Run:

~~~powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' -batchmode -nographics -projectPath '.\BorderValley' -runTests -testPlatform EditMode -testFilter 'BorderValley.Presentation.Tests.PlaceholderAssetTests' -testResults '.\presentation-task2-results.xml' -logFile '.\presentation-task2.log'
~~~

Expected: FAIL，素材路径和 PlaceholderAssetGenerator 不存在。

**Step 3: 实现生成器**

PlaceholderAssetGenerator 必须提供：

~~~csharp
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
    RebuildCatalog();
    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();
}
~~~

实现约束：

- 使用无随机数的整数像素图案和固定调色板。
- PNG 通过 Texture2D.EncodeToPNG 和 File.WriteAllBytes 生成。
- WAV 通过手写 44 字节 RIFF header 和 16-bit little-endian PCM 生成。
- 音频使用 44100 Hz、单声道；菜单和四个区域各 8 秒，战斗 8 秒，胜利 4 秒。
- 音效长度为 0.08 到 0.5 秒。
- 单帧生成后设置 SpriteImportMode.Single；动画 sprite sheet 设置 SpriteImportMode.Multiple、spriteMeshType FullRect，并按固定网格写入 SpriteMetaData。
- 所有贴图设置 Point、NoMipmaps、PPU 32。
- 多次生成必须先覆盖原文件，不得依赖已有导入状态。
- RebuildCatalog 对单帧资源使用 AssetDatabase.LoadAssetAtPath<Sprite>；对 Multiple sprite sheet 使用 AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()，按固定顺序组装 Catalog。音频使用 AssetDatabase.LoadAssetAtPath<AudioClip>。不得通过 Resources.Load。

必须生成的固定路径至少包括：

- world_player.png、world_ground_village.png、world_ground_forest.png、world_ground_watchtower.png、world_ground_crypt.png
- world_npc_elder.png、world_npc_merchant.png、world_npc_blacksmith.png、world_npc_innkeeper.png、world_npc_ranger.png、world_npc_mage.png、world_npc_hunter.png、world_npc_survivor.png
- world_marker_chest.png、world_marker_gather.png、world_marker_investigate.png、world_marker_area_exit.png、world_marker_encounter.png
- battle_unit_warrior.png、battle_unit_ranger.png、battle_unit_mage.png、battle_unit_bandit.png、battle_unit_wolf.png、battle_unit_skeleton.png、battle_unit_boss.png
- portrait_npc_elder.png、portrait_npc_merchant.png、portrait_npc_blacksmith.png、portrait_npc_innkeeper.png、portrait_npc_ranger.png、portrait_npc_mage.png、portrait_npc_hunter.png、portrait_npc_survivor.png
- ui_panel.png、ui_button.png、ui_button_pressed.png、ui_missing.png
- item_weapon.png、item_armor.png、item_accessory.png
- bgm_menu.wav、bgm_world_village.wav、bgm_world_forest.wav、bgm_world_watchtower.wav、bgm_world_crypt.wav、bgm_battle.wav、bgm_victory.wav
- sfx_ui_click.wav、sfx_ui_confirm.wav、sfx_ui_cancel.wav、sfx_ui_error.wav、sfx_dialogue_page.wav、sfx_shop_buy.wav、sfx_shop_sell.wav、sfx_inventory_equip.wav、sfx_inventory_craft.wav、sfx_world_reward.wav、sfx_world_encounter.wav、sfx_battle_attack.wav、sfx_battle_hit.wav、sfx_battle_down.wav

**Step 4: 生成并验证 GREEN**

Run:

~~~powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' -batchmode -nographics -projectPath '.\BorderValley' -executeMethod BorderValley.Editor.Tools.PlaceholderAssetGenerator.GenerateAll -quit -logFile '.\presentation-generate.log'
& '.\scripts\run-tests.ps1'
~~~

Expected: GenerateAll exit 0，PlaceholderAssetTests PASS，完整测试无回归。

**Step 5: 检查生成结果和提交**

Run:

~~~powershell
git status --short
git diff --check
git lfs status
~~~

确认所有 PNG、WAV、Catalog、脚本和 .meta 均被追踪。不要提交 presentation-generate.log、XML 或测试日志。

Commit:

~~~powershell
git add BorderValley/Assets/Art/Placeholder BorderValley/Assets/Audio/Placeholder BorderValley/Assets/Resources/PresentationCatalog.asset BorderValley/Assets/Editor/Tools/PlaceholderAssetGenerator.cs BorderValley/Assets/Editor/BorderValley.Editor.asmdef BorderValley/Assets/Tests/EditMode/Presentation/PlaceholderAssetTests.cs
git commit -m "feat: generate presentation placeholder assets"
~~~

---

### Task 3: AudioDirector、Bootstrap 和 Boot 场景接入

**Files:**

- Create: BorderValley/Assets/Code/Presentation/Audio/IAudioOutput.cs
- Create: BorderValley/Assets/Code/Presentation/Audio/AudioDirector.cs
- Create: BorderValley/Assets/Code/Presentation/Audio/UnityAudioOutput.cs
- Create: BorderValley/Assets/Code/Presentation/Boot/PresentationBootstrapInstaller.cs
- Create: BorderValley/Assets/Editor/Tools/PresentationBootSceneBuilder.cs
- Modify: BorderValley/Assets/Editor/Tools/FoundationSceneBuilder.cs
- Modify: BorderValley/Assets/Scenes/Boot.unity
- Test: BorderValley/Assets/Tests/EditMode/Presentation/AudioDirectorTests.cs

**Interfaces:**

- Consumes: PresentationCatalog、PresentationService、IPresentationService。
- Produces: IAudioOutput、AudioDirector、PresentationBootstrapInstaller、Boot 场景中的 IPresentationService 注册。

**Step 1: 写失败测试**

AudioDirectorTests.cs：

~~~csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BorderValley.Presentation.Tests
{
    public sealed class AudioDirectorTests
    {
        private sealed class FakeOutput : IAudioOutput
        {
            public readonly List<string> Calls = new();
            public string CurrentMusicCueId { get; private set; }

            public void Play(AudioCue cue)
            {
                Calls.Add("play:" + cue.Id);
                if (cue.Channel == AudioChannel.Music)
                    CurrentMusicCueId = cue.Id;
            }

            public void Stop(AudioChannel channel) => Calls.Add("stop:" + channel);
            public void SetPaused(bool paused) => Calls.Add("pause:" + paused);
        }

        [Test]
        public void PlayMusic_SameCue_DoesNotRestart()
        {
            var output = new FakeOutput();
            var director = new AudioDirector(output);
            var cue = new AudioCue("bgm.menu", null, 1f, true, AudioChannel.Music);

            director.PlayMusic(cue);
            director.PlayMusic(cue);

            Assert.That(output.Calls, Is.EqualTo(new[] { "play:bgm.menu" }));
        }

        [Test]
        public void Pause_True_PausesOutputAndRemembersDesiredMusic()
        {
            var output = new FakeOutput();
            var director = new AudioDirector(output);
            director.PlayMusic(new AudioCue("bgm.menu", null, 1f, true, AudioChannel.Music));

            director.SetPaused(true);

            Assert.That(output.Calls, Does.Contain("pause:True"));
            Assert.That(director.DesiredMusicCueId, Is.EqualTo("bgm.menu"));
        }
    }
}
~~~

**Step 2: 运行测试确认 RED**

Run:

~~~powershell
& '.\scripts\run-tests.ps1'
~~~

Expected: FAIL，IAudioOutput 或 AudioDirector 不存在。

**Step 3: 实现音频服务**

IAudioOutput：

~~~csharp
public interface IAudioOutput
{
    void Play(AudioCue cue);
    void Stop(AudioChannel channel);
    void SetPaused(bool paused);
    string CurrentMusicCueId { get; }
}
~~~

AudioDirector 是纯 C# 类，提供 PlayMusic、PlayUi、PlayWorld、PlayBattle、StopMusic、SetPaused 和 DesiredMusicCueId。规则：

- Music cue 相同不重复调用 output.Play。
- 新 Music 先 output.Stop(AudioChannel.Music)。
- 非 Music cue 只调用 output.Play。
- 没打开音频时仍保留 DesiredMusicCueId。
- 恢复暂停时若 DesiredMusicCueId 非空，重新调用 PlayMusic。

UnityAudioOutput 是 MonoBehaviour：

- Awake 创建 1 个 Music AudioSource、8 个 Battle AudioSource、6 个 UI AudioSource、1 个 World AudioSource、1 个 Victory AudioSource。
- 所有 AudioSource 设置 playOnAwake false。
- Play 根据 Channel 选择空闲非循环 AudioSource。
- Music 设置 clip、loop、volume 后 Play。
- 没有空闲 SFX source 时丢弃，不排队。
- SetPaused 调用 AudioListener.pause。
- 同 cue 在 0.05 秒内重复触发时忽略。

PresentationBootstrapInstaller.Install：

- 加载 Resources/PresentationCatalog。
- 在 installer 的子对象创建 PresentationRuntime。
- 添加 UnityAudioOutput 并初始化。
- 创建 AudioDirector 和 PresentationService。
- Catalog 缺失时注册 NullPresentationService。
- context.Register<IPresentationService>(service)。

PresentationBootSceneBuilder 提供 BorderValley/Presentation/Wire Boot Scene 菜单和 WireBootScene 静态方法。它打开 Boot.unity，在 BootServices 上确保存在 PresentationBootstrapInstaller，并在 GameBootstrapper 的 serviceInstallers 中追加且不重复，保存场景。

同时修改 FoundationSceneBuilder.Rebuild，使未来重建时创建同等 PresentationBootstrapInstaller 接线。

**Step 4: 运行测试和场景接线**

Run:

~~~powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' -batchmode -nographics -projectPath '.\BorderValley' -executeMethod BorderValley.Editor.Tools.PresentationBootSceneBuilder.WireBootScene -quit -logFile '.\presentation-boot-wire.log'
& '.\scripts\run-tests.ps1'
~~~

Expected: Boot.unity diff 只包含新组件和 serviceInstallers；AudioDirectorTests PASS；原 PlayMode 场景测试 PASS。

**Step 5: 提交**

~~~powershell
git add BorderValley/Assets/Code/Presentation BorderValley/Assets/Editor/Tools/PresentationBootSceneBuilder.cs BorderValley/Assets/Editor/Tools/FoundationSceneBuilder.cs BorderValley/Assets/Scenes/Boot.unity BorderValley/Assets/Tests/EditMode/Presentation/AudioDirectorTests.cs
git commit -m "feat: add presentation audio runtime"
~~~

---

### Task 4: SpriteAnimator、世界视觉和四方向移动动画

**Files:**

- Create: BorderValley/Assets/Code/Presentation/Animation/SpriteAnimator.cs
- Create: BorderValley/Assets/Code/UI/World/WorldAnimationSelector.cs
- Modify: BorderValley/Assets/Code/UI/World/WorldMapView.cs
- Modify: BorderValley/Assets/Code/UI/World/WorldExplorationController.cs
- Modify: BorderValley/Assets/Code/UI/BorderValley.UI.asmdef
- Modify: BorderValley/Assets/Tests/PlayMode/BorderValley.PlayModeTests.asmdef
- Test: BorderValley/Assets/Tests/EditMode/Presentation/SpriteAnimatorTests.cs
- Modify: BorderValley/Assets/Tests/PlayMode/WorldNarrativeFlowTests.cs
- Test: BorderValley/Assets/Tests/PlayMode/PresentationFlowTests.cs

**Interfaces:**

- Consumes: IPresentationService、VisualClip、SpriteRenderer。
- Produces: SpriteAnimator、WorldAnimationSelector、WorldMapView 的新表现渲染路径。

**Step 1: 写失败测试**

SpriteAnimatorTests.cs：

~~~csharp
using NUnit.Framework;
using UnityEngine;

namespace BorderValley.Presentation.Tests
{
    public sealed class SpriteAnimatorTests
    {
        [Test]
        public void PlayIfChanged_SameClip_DoesNotResetElapsedTime()
        {
            var root = new GameObject("animator", typeof(SpriteRenderer), typeof(SpriteAnimator));
            var animator = root.GetComponent<SpriteAnimator>();
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            var clip = new VisualClip("idle", new[] { sprite }, 4f, true, sprite);

            animator.PlayIfChanged(clip);
            animator.Tick(0.5f);
            var elapsed = animator.ElapsedForTests;
            animator.PlayIfChanged(clip);

            Assert.That(animator.ElapsedForTests, Is.EqualTo(elapsed));
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(texture);
        }
    }
}
~~~

在 WorldNarrativeFlowTests 的 Boot_ToWorld_CreatesTouchMapPlayerAndVillageContent 增加断言：

~~~csharp
Assert.That(controller.Player.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
Assert.That(controller.MapView.RenderedSpriteCount, Is.GreaterThan(0));
~~~

在 PresentationFlowTests.cs 增加：

~~~csharp
[UnityTest]
public IEnumerator PlayerMovingRight_UsesWalkEastClip()
{
    yield return SceneManager.LoadSceneAsync("Boot");
    yield return null;
    Object.FindAnyObjectByType<UnityEngine.UI.Button>().onClick.Invoke();
    yield return WaitForScene("World");

    var controller = Object.FindAnyObjectByType<WorldExplorationController>();
    controller.Joystick.SetValue(Vector2.right);
    yield return null;

    Assert.That(controller.PlayerAnimator.CurrentClipId, Is.EqualTo("world.player.east.walk"));
}
~~~

PresentationFlowTests 复制项目现有 WaitForScene helper，不能改变现有测试 helper 的语义。

**Step 2: 运行测试确认 RED**

Run:

~~~powershell
& '.\scripts\run-tests.ps1'
~~~

Expected: FAIL，SpriteAnimator、RenderedSpriteCount、PlayerAnimator 和真实 sprite 尚不存在。

**Step 3: 实现 SpriteAnimator**

SpriteAnimator API：

~~~csharp
public sealed class SpriteAnimator : MonoBehaviour
{
    public string CurrentClipId { get; }
    public bool IsPlaying { get; }
    public float ElapsedForTests { get; }
    public void Play(VisualClip clip);
    public void PlayIfChanged(VisualClip clip);
    public void Stop();
    public void Tick(float deltaTime);
}
~~~

Update 调用 Tick(unscaledDeltaTime)。非循环 clip 停在最后一帧。缺少 SpriteRenderer 时抛出 InvalidOperationException，避免静默配置错误。

**Step 4: 实现世界动画选择**

WorldAnimationSelector 使用纯函数：

~~~csharp
public enum WorldFacing { South, East, North, West }

public static WorldFacing Resolve(Vector2 input)
public static string BuildClipId(WorldFacing facing, bool moving)
public static bool IsMoving(Vector2 input)
~~~

BuildClipId 输出 world.player.south.walk、world.player.east.idle 等。

**Step 5: 改造 WorldMapView 和 Controller**

WorldMapView 增加 public int RenderedSpriteCount，并增加：

~~~csharp
public void Render(
    WorldAreaDefinition area,
    NarrativeStateService state,
    IPresentationService presentation)
~~~

渲染规则：

- Background 使用 world.ground.<area suffix> clip 的第一帧，SpriteRenderer.drawMode = Tiled，size = area bounds size。
- Obstacle 使用同区域地砖或 world.obstacle 的第一帧，Tiled。
- NPC 使用 world.npc.<npcId without prefix>.idle clip。
- Chest、Gather、Investigate、AreaExit 使用 world.marker.<kind>.idle clip。
- Encounter 使用 world.marker.encounter.idle clip。
- 不再创建 Texture2D.whiteTexture Sprite。
- 所有渲染对象继续加入 rendered 列表，Clear 行为保持不变。

WorldExplorationController：

- 初始化时从 GameContext 获取 IPresentationService，没有时使用 NullPresentationService。
- CreateMapAndPlayer 中给 Player 添加 SpriteAnimator，并暴露 PlayerAnimator 属性。
- Tick 在每帧移动处理后调用 UpdatePlayerAnimation(Joystick.Value)。
- 只有输入模长大于 0.1 且实际发生位置变化时视为 moving。
- 切换区域后继续使用相同 PlayerAnimator，clip 前缀不变。
- 渲染失败不能改变 SetPlayerPosition 或存档。

**Step 6: 运行测试确认 GREEN**

Run:

~~~powershell
& '.\scripts\run-tests.ps1'
~~~

Expected: 新增测试 PASS，WorldNarrativeFlowTests、BattleSceneFlowTests 和所有原测试无回归。

**Step 7: 提交**

~~~powershell
git add BorderValley/Assets/Code/Presentation/Animation BorderValley/Assets/Code/UI/World BorderValley/Assets/Code/UI/BorderValley.UI.asmdef BorderValley/Assets/Tests/EditMode/Presentation/SpriteAnimatorTests.cs BorderValley/Assets/Tests/PlayMode
git commit -m "feat: animate world with placeholder art"
~~~

---

### Task 5: UI 皮肤、对话头像和物品图标

**Files:**

- Create: BorderValley/Assets/Code/UI/Presentation/PresentationUiUtility.cs
- Modify: BorderValley/Assets/Code/UI/MainMenuView.cs
- Modify: BorderValley/Assets/Code/UI/World/DialoguePanelView.cs
- Modify: BorderValley/Assets/Code/UI/World/DialogueUiPresenter.cs
- Modify: BorderValley/Assets/Code/UI/World/ShopPanelView.cs
- Modify: BorderValley/Assets/Code/UI/World/QuestLogPanelView.cs
- Modify: BorderValley/Assets/Code/UI/Inventory/InventoryPanelView.cs
- Modify: BorderValley/Assets/Code/UI/Battle/BattleHudView.cs
- Modify: BorderValley/Assets/Scenes/MainMenu.unity
- Modify: BorderValley/Assets/Tests/EditMode/UI/WorldUiPresenterTests.cs
- Test: BorderValley/Assets/Tests/EditMode/UI/PresentationUiTests.cs

**Interfaces:**

- Consumes: IPresentationService、GetUiSprite、GetNpcPortrait、GetItemIcon。
- Produces: PresentationUiUtility、DialoguePanelViewData.PortraitSprite、统一 UI skin。

**Step 1: 写失败测试**

PresentationUiTests.cs：

~~~csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BorderValley.UI.Tests
{
    public sealed class PresentationUiTests
    {
        [Test]
        public void ApplyPanel_SetsSpriteAndSlicedType()
        {
            var root = new GameObject("panel", typeof(RectTransform), typeof(Image));
            var image = root.GetComponent<Image>();
            var texture = new Texture2D(16, 16);
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, 16, 16),
                Vector2.one * 0.5f,
                32f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(4, 4, 4, 4));

            PresentationUiUtility.ApplyPanel(image, sprite);

            Assert.That(image.sprite, Is.SameAs(sprite));
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(texture);
        }
    }
}
~~~

在 DialogueUiPresenter 测试增加：

~~~csharp
[Test]
public void Open_Render_IncludesSpeakerNpcId()
{
    var view = new SpyDialogueView();
    var presenter = new DialogueUiPresenter(service, view);
    presenter.Open("npc.elder");
    Assert.That(view.LastData.SpeakerNpcId, Is.EqualTo("npc.elder"));
}
~~~

**Step 2: 运行测试确认 RED**

Run:

~~~powershell
& '.\scripts\run-tests.ps1'
~~~

Expected: FAIL，PresentationUiUtility 和 SpeakerNpcId 不存在。

**Step 3: 实现 UI 工具**

PresentationUiUtility API：

~~~csharp
public static IPresentationService GetOrNull()
public static void ApplyPanel(Image image, Sprite sprite)
public static void ApplyButton(Button button, Sprite normal, Sprite pressed)
public static Sprite ResolvePanel(IPresentationService presentation)
public static Sprite ResolveButton(IPresentationService presentation)
public static Sprite ResolvePressedButton(IPresentationService presentation)
~~~

ApplyPanel：

- image.sprite = sprite
- image.type = Image.Type.Sliced
- image.pixelsPerUnitMultiplier = 1
- 保留 raycastTarget

ApplyButton：

- button.targetGraphic 使用 normal sprite
- button.spriteState 使用 pressed 和 highlighted sprite
- normal 为空时保留纯色 fallback

**Step 4: 改造面板和按钮**

WorldPanelViewFactory、BattleHudView、InventoryPanelView 的本地 CreatePanel/CreateButton 在创建 Image 后调用 PresentationUiUtility。保留错误红、选中蓝等语义色叠加。

MainMenuView 启动时获取 IPresentationService，对 newGameButton 应用 ui.button 和 ui.button.pressed。不要修改场景中按钮事件。

QuestLogPanelView 和 ShopPanelView 只改面板背景，不改变滚动、分页和按钮布局。

**Step 5: 增加对话头像**

DialoguePanelViewData 增加 SpeakerNpcId 和 PortraitSprite。为保持现有测试兼容，可增加带默认参数的重载，但现有生产路径必须显式提供 SpeakerNpcId。DialogueUiPresenter.Render 传入 session.CurrentNode.SpeakerNpcId，并通过 IPresentationService.GetNpcPortrait 解析 Sprite。

DialoguePanelView 在 speaker label 左侧增加 256×256 头像 Image，使用 PreserveAspect。没有头像时使用 ui.missing。头像不得覆盖 choice 和按钮的 raycast。

**Step 6: 增加物品图标**

InventoryPanelView 的每一页 item row 增加 32×32 Image。使用 VisibleItems 的 item.DefinitionId 和 item.Definition.Slot 调用 GetItemIcon。装备列表也显示图标；没有 exact icon 时使用 slot fallback。

ShopPanelView 在 offer/item 行文本左侧增加同尺寸图标。保留物品名、价格和按钮的可点击区域。

**Step 7: 运行测试确认 GREEN**

Run:

~~~powershell
& '.\scripts\run-tests.ps1'
~~~

Expected: 新增 UI 测试 PASS；Inventory、Shop、Dialogue、QuestLog、BattleHud 原测试无回归。

**Step 8: 提交**

~~~powershell
git add BorderValley/Assets/Code/UI BorderValley/Assets/Scenes/MainMenu.unity BorderValley/Assets/Tests/EditMode/UI
git commit -m "feat: apply placeholder ui skin and portraits"
~~~

---

### Task 6: 战斗单位视觉和事件驱动动画

**Files:**

- Create: BorderValley/Assets/Code/UI/Battle/BattlePresentationTracker.cs
- Modify: BorderValley/Assets/Code/UI/Battle/BattleGridView.cs
- Modify: BorderValley/Assets/Code/UI/Battle/BattleSceneController.cs
- Modify: BorderValley/Assets/Code/UI/Battle/BattleUiPresenter.cs
- Test: BorderValley/Assets/Tests/EditMode/UI/BattlePresentationTrackerTests.cs
- Modify: BorderValley/Assets/Tests/PlayMode/BattleSceneFlowTests.cs

**Interfaces:**

- Consumes: IPresentationService、SpriteAnimator、BattleState、BattleCommand、BattleActionResult。
- Produces: BattlePresentationTracker、BattlePresentationEvent、BattleGridView 的 64×64 单位图路径。

**Step 1: 写失败测试**

BattlePresentationTrackerTests.cs：

~~~csharp
using System.Linq;
using BorderValley.Battle.Domain;
using NUnit.Framework;

namespace BorderValley.UI.Tests
{
    public sealed class BattlePresentationTrackerTests
    {
        [Test]
        public void Observe_HealthDecreased_ReturnsHit()
        {
            var tracker = new BattlePresentationTracker();
            var map = BattleMap.CreatePlain(2, 2);
            BattleState State(int health)
            {
                var state = new BattleState(map);
                state.AddUnit(new BattleUnit(
                    "ally",
                    "class.warrior",
                    Team.Player,
                    new UnitStats(10, 0, 1, 0, 1, 0f, 0),
                    new GridPosition(0, 0)));
                state.GetUnit("ally").SetCurrentResources(health, 0);
                return state;
            }

            var before = State(10);
            var after = State(7);
            tracker.Observe(before, null, BattleActionResult.Succeeded(string.Empty));

            var events = tracker.Observe(after, null, BattleActionResult.Succeeded(string.Empty));

            Assert.That(events.Any(value => value.Kind == BattlePresentationEventKind.Hit), Is.True);
        }
    }
}
~~~

具体测试 fixture 应按现有 BattleScenario/BattleEngine 构造真实状态，不能新增仅用于测试的生产工厂。测试至少覆盖：

- 位置变化产生 Move。
- 生命下降产生 Hit。
- 生命归零产生 Down。
- 成功 UseSkillCommand 产生 Attack。
- 新回合不延续上一单位的 Attack 状态。

BattleSceneFlowTests 增加：

~~~csharp
var grid = Object.FindAnyObjectByType<BattleGridView>();
Assert.That(grid.RenderedUnitSpriteCount, Is.GreaterThan(0));
Assert.That(grid.UnitSpriteSize, Is.EqualTo(new Vector2Int(64, 64)));
~~~

**Step 2: 运行测试确认 RED**

Run:

~~~powershell
& '.\scripts\run-tests.ps1'
~~~

Expected: FAIL，Tracker、事件类型和 BattleGridView 新属性不存在。

**Step 3: 实现 Tracker**

公开类型：

~~~csharp
public enum BattlePresentationEventKind { Move, Attack, Hit, Down, TurnChanged }

public readonly struct BattlePresentationEvent
{
    public string UnitId { get; }
    public BattlePresentationEventKind Kind { get; }
}

public sealed class BattlePresentationTracker
{
    public IReadOnlyList<BattlePresentationEvent> Observe(
        BattleState state,
        BattleCommand command,
        BattleActionResult result);
}
~~~

规则：

- 第一次 Observe 只建立快照并产生 TurnChanged。
- 位置改变优先产生 Move。
- Health 降低产生 Hit。
- Health 从大于 0 到等于 0额外产生 Down。
- result.Success 且 command 是 UseSkillCommand 时给施放者产生 Attack。
- ActiveUnit 变化产生 TurnChanged。
- 返回数组后不持有 BattleState。

**Step 4: 让 Presenter 暴露命令结果**

BattleUiPresenter 增加只读：

- BattleCommand LastCommand
- BattleActionResult LastResult
- long StateVersion

Execute 成功后记录 command/result，再 Notify。SelectSkill、EndTurn 失败和纯 UI 选择不产生 Attack。StateVersion 每次 Notify 前递增，供 Controller 判断是否执行观察。

**Step 5: 改造 BattleGridView**

CellView 增加：

- Image UnitImage
- Text UnitLabel
- SpriteAnimator UnitAnimator

Render 接收 IPresentationService 并在 units 循环中：

- 使用 GetCharacterVisualPrefix(unit.DefinitionId) 获取前缀。
- 默认 idle clip 为 battle.unit.idle。
- UnitImage.sprite 为 idle clip 第一帧。
- UnitLabel 保留现有 DefinitionId 文本作为调试回退，但字号缩小并放在底部。
- RenderedUnitSpriteCount 计算有非空 sprite 的单位。
- UnitSpriteSize 返回第一个单位 sprite 的原始尺寸，无单位时返回 Vector2Int.zero。

新增方法：

~~~csharp
public void PlayEvent(BattlePresentationEvent value, IPresentationService presentation)
~~~

事件映射：

- Move 到 battle.unit.move
- Attack 到 battle.unit.attack
- Hit 到 battle.unit.hit
- Down 到 battle.unit.down
- TurnChanged 到 battle.unit.idle

clip 缺失时回退 idle，不抛出。

**Step 6: 接入 Controller**

BattleSceneController：

- Start 获取 IPresentationService 或 NullPresentationService。
- 创建 BattlePresentationTracker。
- gridView.Initialize 时传入 presentation。
- 每次 presenter.Changed 后只对新的 StateVersion 调用 Tracker.Observe，再逐事件调用 gridView.PlayEvent。
- 动画事件不得改变 continueHandled、EnemyCommandSelector 或 BattleResult。
- 保持 0.2 秒敌人回合节奏，不为动画额外改变战斗规则。

**Step 7: 运行测试确认 GREEN**

Run:

~~~powershell
& '.\scripts\run-tests.ps1'
~~~

Expected: Tracker 测试 PASS，BattleSceneFlowTests 中战斗结果、AI、目标选择全部无回归。

**Step 8: 提交**

~~~powershell
git add BorderValley/Assets/Code/UI/Battle BorderValley/Assets/Tests/EditMode/UI/BattlePresentationTrackerTests.cs BorderValley/Assets/Tests/PlayMode/BattleSceneFlowTests.cs
git commit -m "feat: animate battle units with placeholder art"
~~~

---

### Task 7: 音频事件接线和端到端表现测试

**Files:**

- Modify: BorderValley/Assets/Code/UI/MainMenuView.cs
- Modify: BorderValley/Assets/Code/UI/MainMenuPresenter.cs
- Modify: BorderValley/Assets/Code/UI/World/WorldExplorationController.cs
- Modify: BorderValley/Assets/Code/UI/World/DialogueUiPresenter.cs
- Modify: BorderValley/Assets/Code/UI/World/ShopUiPresenter.cs
- Modify: BorderValley/Assets/Code/UI/Inventory/InventoryUiPresenter.cs
- Modify: BorderValley/Assets/Code/UI/Battle/BattleSceneController.cs
- Test: BorderValley/Assets/Tests/EditMode/UI/PresentationUiTests.cs
- Test: BorderValley/Assets/Tests/PlayMode/PresentationFlowTests.cs

**Interfaces:**

- Consumes: IPresentationService.PlayMusic、PlaySfx、StopMusic。
- Produces: 菜单、区域、对话、商店、装备、奖励和战斗的音频反馈。

**Step 1: 写失败测试**

增加 RecordingPresentationService 测试替身，记录 MusicCalls、SfxCalls、StoppedMusic。测试优先构造 Presenter 或 Controller，不复制生产事件逻辑。

测试用例：

~~~csharp
[Test]
public void MainMenuStart_PlaysMenuMusic()
{
    var calls = new RecordingPresentationService();
    var presenter = new MainMenuPresenter(new FakeSceneLoader(), calls);
    presenter.StartNewGame();
    Assert.That(calls.MusicCalls, Does.Contain("bgm.menu"));
}

[Test]
public void ShopBuySucceeded_PlaysBuySound()
{
    var calls = new RecordingPresentationService();
    var presenter = CreateShopPresenter(calls);
    BuyFirstOffer(presenter);
    Assert.That(calls.SfxCalls, Does.Contain("sfx.shop.buy"));
}

[Test]
public void SuccessfulEquip_PlaysEquipSound()
{
    var calls = new RecordingPresentationService();
    var presenter = CreateInventoryPresenter(calls);
    EquipSelected(presenter);
    Assert.That(calls.SfxCalls, Does.Contain("sfx.inventory.equip"));
}
~~~

PresentationFlowTests 增加：

~~~csharp
[UnityTest]
public IEnumerator AreaTransition_ChangesMusicCue()
{
    yield return LoadWorld();
    var controller = Object.FindAnyObjectByType<WorldExplorationController>();
    var presentation = GameBootstrapper.Context.Get<IPresentationService>();
    var exit = FindAreaExit("area.village", "area.forest");

    yield return MoveNear(controller, exit.Position);
    Assert.That(controller.Interact(), Is.True);
    yield return null;

    Assert.That(presentation.LastMusicCueId, Is.EqualTo("bgm.world.forest"));
}
~~~

IPresentationService 不增加测试专用成员。测试通过 RecordingPresentationService 或 UnityAudioOutput 的可观察状态断言。

**Step 2: 运行测试确认 RED**

Run:

~~~powershell
& '.\scripts\run-tests.ps1'
~~~

Expected: FAIL，音频事件尚未接线。

**Step 3: 接入菜单和世界**

MainMenuView：

- Start 时 PlayMusic(bgm.menu)。
- 新游戏按钮点击先 PlaySfx(sfx.ui.confirm)，再进入 World。
- 点击失败或加载异常时 PlaySfx(sfx.ui.error)。

WorldExplorationController：

- Initialize 完成后播放当前区域音乐。
- SwitchArea 成功播放目标区域音乐和 sfx.ui.confirm。
- 打开或关闭页面、对话和商店时播放 sfx.ui.click 或 sfx.ui.cancel。
- Interact NPC 播放 sfx.dialogue.page。
- GrantInteractable 成功播放 sfx.world.reward。
- BeginEncounter 成功播放 sfx.world.encounter。
- 可恢复错误设置 LastErrorKey 时播放 sfx.ui.error。

不得在 SaveService、QuestService、ShopService 或 InventoryService 内直接调用音频。

**Step 4: 接入对话、商店和背包**

DialogueUiPresenter：

- Open、Continue、SelectChoice 成功播放 sfx.dialogue.page。
- Close 播放 sfx.ui.cancel。

ShopUiPresenter：

- Buy 成功播放 sfx.shop.buy。
- Sell 成功播放 sfx.shop.sell。
- Buy 或 Sell 失败播放 sfx.ui.error。

InventoryUiPresenter：

- EquipSelected 成功播放 sfx.inventory.equip。
- Unequip 成功播放 sfx.ui.cancel。
- Craft、Reforge、Dismantle 成功播放 sfx.inventory.craft。
- RestParty 成功播放 sfx.world.reward。
- 失败播放 sfx.ui.error。

为保持现有构造函数兼容，新增 IPresentationService 参数必须放在最后一个可选参数。旧调用默认使用 NullPresentationService。

**Step 5: 接入战斗**

BattleSceneController：

- Start 成功创建 presenter 后播放 bgm.battle。
- PlayEvent 时同时播放对应 sfx.battle.attack、hit、down。
- Finish 时 StopMusic，再播放 bgm.victory 或 sfx.ui.error。
- Continue 返回 World 前 StopMusic。

BattleState 或 BattleEngine 不得接触 AudioClip、AudioSource 或 IPresentationService。

**Step 6: 运行测试确认 GREEN**

Run:

~~~powershell
& '.\scripts\run-tests.ps1'
~~~

Expected: 全部 EditMode 和 PlayMode 测试 PASS。

**Step 7: 提交**

~~~powershell
git add BorderValley/Assets/Code/UI BorderValley/Assets/Tests/EditMode/UI/PresentationUiTests.cs BorderValley/Assets/Tests/PlayMode/PresentationFlowTests.cs
git commit -m "feat: wire presentation audio events"
~~~

---

### Task 8: 阶段验证、最终 review 和交接

**Files:**

- Create during verification only: .superpowers/sdd/2026-09-15-audio-animation-art-placeholder/progress.md，不提交
- Modify after user confirms PR merge: D:\program\NEW_CHAT_HANDOFF.md
- Modify after user confirms PR merge: D:\program\GAME_DEVELOPMENT_MASTER.md，仅在阶段状态需要时

**Interfaces:**

- Consumes: 全部前序 task 的提交和测试。
- Produces: 可构建、已审查、已创建 PR 的阶段分支，以及用户确认合并后的交接状态。

**Step 1: 运行完整测试**

Run:

~~~powershell
& '.\scripts\run-tests.ps1'
~~~

Expected: EditMode 和 PlayMode 全部 PASS，failed=0。解析 XML 保存具体总数。

**Step 2: 运行 Android ARM64 IL2CPP 构建**

Run:

~~~powershell
& '.\scripts\build-android.ps1'
~~~

Expected: Builds/Android/BorderValley.apk 生成，Unity exit code 0。计算 APK 大小和 SHA256，但不提交 APK。

**Step 3: 检查仓库边界**

Run:

~~~powershell
git status --short
git diff --check
git diff --stat
git lfs ls-files
~~~

Expected:

- 无未跟踪生成物。
- 无测试日志、XML、APK、截图或 .superpowers 进入 git status。
- 所有 PNG、WAV 是 LFS 对象。
- 所有新增 Unity 文件有 .meta。

**Step 4: 整分支 review**

使用 requesting-code-review skill，对合并基线 059b57ec 到当前 HEAD 进行 spec review 和 quality review。至少检查：

- 设计中的 Catalog、素材规格、音频、动画和替换边界是否全部实现。
- Domain 是否错误引用表现类型。
- 是否残留 Texture2D.whiteTexture 白块。
- 是否新增硬编码可见文本。
- 是否改变 save schema。
- 是否存在音频每帧分配、日志刷屏或动画阻塞战斗。

Critical 和 Important 必须修复并限定复审。Minor 写入 SDD ledger，留最终 review。

**Step 5: 提交最终修复**

每个修复作为独立 commit：

~~~powershell
git add BorderValley/Assets
git commit -m "fix: resolve final presentation review findings"
~~~

修复后重新运行完整测试和 Android 构建。

**Step 6: 推送和创建 PR**

Run:

~~~powershell
git push -u origin codex/audio-animation-art-placeholder
gh pr create --repo LvTianba/game --base main --head codex/audio-animation-art-placeholder --title "音频、动画、美术占位替换" --body-file .superpowers/pr-body.md
~~~

PR 描述必须包含：

- 阶段目标和设计/计划链接。
- EditMode 数量。
- PlayMode 数量。
- Android 构建结果、APK 大小和 SHA256。
- 真机验证状态。
- 未处理的 Minor。
- 真机测试暂停说明。

**Step 7: 真机门禁**

如果 PR 或阶段要求真机测试，立刻暂停并通知用户，给出 APK 路径、SHA256 和需要验证的清单：

- 主菜单音乐。
- 新游戏进入 World。
- 行走方向与动画。
- 区域音乐切换。
- 对话头像。
- 商店和背包图标。
- 战斗单位动画和音效。
- 后台恢复后音频恢复。
- 10 分钟稳定性。

在用户确认前不安装、不操作设备、不标记真机验证完成。

**Step 8: 用户确认合并后更新交接**

只有用户明确确认 PR 已合并后才执行：

- 获取 origin/main 合并 commit 和分支 HEAD。
- 把“音频、动画、美术占位替换”移入已合并阶段。
- 更新 D:\program\NEW_CHAT_HANDOFF.md 的当前阶段、Git、验证、PR 和下一阶段启动提示词。
- 更新本阶段 SDD ledger 最终状态。
- 不删除 worktree，不开始下一阶段。
- 停止当前对话。

---

## Self-Review

### Spec coverage

- Catalog、IPresentationService、缺失 fallback：Task 1。
- PNG、WAV、导入规格和 Catalog 资源：Task 2。
- AudioDirector、暂停恢复、Boot 注册：Task 3。
- SpriteAnimator、世界四方向、地图占位：Task 4。
- UI skin、头像、物品图标：Task 5。
- 战斗 64×64 和五类动画事件：Task 6。
- 菜单、区域、对话、商店、背包、奖励和战斗音频：Task 7。
- 完整测试、Android、review、PR、真机和交接：Task 8。

### Placeholder scan

计划不含 TBD、TODO、待补实现或“类似前一个 task”的代码替换指令。每个实现 task 都有 RED、GREEN、提交步骤。

### Type consistency

- 设计中的 VisualClipDefinition 对应运行时 VisualClip。
- AudioCueDefinition 对应运行时 AudioCue。
- IPresentationService 在 Task 1 固定，后续 tasks 只调用不改签名。
- SpriteAnimator.CurrentClipId 在 Task 4 和 Task 6 共用。
- BattlePresentationEventKind 在 Task 6 固定，Task 7 只消费。
- PresentationBootstrapInstaller 注册 IPresentationService，任务只通过 GameContext.Get<IPresentationService> 获取。
