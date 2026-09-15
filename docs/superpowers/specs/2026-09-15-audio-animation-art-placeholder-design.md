# 音频、动画、美术占位替换阶段设计

- 日期：2026-09-15
- 状态：待用户复核
- 上游规格：docs/superpowers/specs/2026-09-12-fantasy-tactics-rpg-vertical-slice-design.md
- 基线：main 合并提交 059b57ecb7eaaf34d6701316d8cc0c025a7b40c0
- 实现分支：codex/audio-animation-art-placeholder

## 1. 阶段目标

把当前以纯色块和文字为主的程序占位表现，替换为固定规格、可重复生成、可通过内容 ID 替换的像素占位素材，并加入最小可用的音频播放和事件驱动动画层。

本阶段交付的是“可替换占位层”，不是最终美术或完整配音。替换 PNG、WAV 或 Catalog 映射后，不应修改玩法代码或存档格式。

完成后应达到：

- 主菜单、世界、战斗和主要面板使用统一的像素占位视觉主题。
- 世界玩家具备四方向移动动画，NPC 和交互物使用稳定图标。
- 战斗单位使用 64×64 单位图，并响应待机、移动、攻击、受击和倒下事件。
- 菜单、区域、战斗、对话、商店、装备和奖励具有基础音乐或音效反馈。
- 所有素材通过 ID 和 PresentationCatalog 定位。
- 游戏逻辑、战斗规则和存档 schema 不依赖 UnityEngine 表现类型。

## 2. 非目标

本阶段不包含：

- 最终角色、场景、装备或 UI 美术。
- 完整配音、动态混音、音频压缩配方或多语言语音。
- 音量设置、静音菜单、辅助功能菜单或音频持久化设置。
- Timeline、Spine、DOTween 或第三方动画框架。
- 对玩法数值、战斗公式、掉落、任务或存档结构的重构。
- 制作每个任务、技能和词条的独立最终图标。
- 为素材加载引入 Addressables。首版占位素材继续放在 Unity 工程内，Catalog 保留未来替换为 Addressables 的接口边界。

## 3. 设计原则

### 3.1 表现与玩法隔离

Domain 和 Service 层不能引用 Sprite、AudioClip、Animator、AudioSource 或 PresentationService。表现层只消费稳定 ID 和只读状态。

允许在以下边界调用表现服务：

- MonoBehaviour View 和 Controller。
- UI Presenter 的视图绑定层。
- BattleSceneController 对战斗状态的只读观察。
- MainMenuView、WorldExplorationController 的场景入口。

### 3.2 ID 优先

所有视觉和音频调用使用字符串 ID。玩法数据 ID 与表现 ID 分离，避免内容定义直接持有 Unity 资源引用。

ID 规则：

- 区域音乐：bgm.world.<area-id-without-prefix>
- 菜单音乐：bgm.menu
- 战斗音乐：bgm.battle
- 胜利音乐：bgm.victory
- UI 音效：sfx.ui.<action>
- 世界音效：sfx.world.<action>
- 战斗音效：sfx.battle.<action>
- 世界视觉：world.<kind>.<state>
- 战斗视觉：battle.unit.<state>
- NPC 头像：portrait.<npc-id>
- UI 九宫格：ui.panel、ui.button、ui.button.pressed

### 3.3 Catalog 是唯一替换点

运行时通过 Resources 加载 PresentationCatalog。替换最终素材时只修改：

- Assets/Art 下的 PNG 及导入设置。
- Assets/Audio 下的 WAV 及导入设置。
- Resources/PresentationCatalog.asset 中的资源引用和映射。

不得为了替换素材修改存档 schema、玩法 Definition 或 Domain 规则。

### 3.4 失败不阻塞主流程

Release 构建缺少可选音频或装饰素材时记录警告并继续。缺少战斗单位和世界玩家基础视觉时使用 Catalog 的 fallback 视觉并记录错误。Catalog 缺失时表现服务进入 no-op 模式，不能让世界或战斗输入失效。

## 4. 模块结构

新增独立程序集：

- BorderValley.Presentation
- 引用：BorderValley.Core
- 不引用：Battle、Inventory、Narrative、World 或 UI

BorderValley.Presentation 包含：

- PresentationCatalog：ScriptableObject，保存视觉、动画、音频和映射。
- PresentationService：实现 IPresentationService，提供只读查询。
- AudioDirector：管理背景音乐、音效、AudioSource 池和暂停恢复。
- SpriteAnimator：按 clip 播放 Sprite 帧。
- PresentationBootstrapInstaller：在 Boot 场景注册服务。
- 纯数据对象：VisualClipDefinition、AudioCueDefinition、PresentationMapping。

BorderValley.UI 新增对 BorderValley.Presentation 的引用。世界、战斗和 UI 代码只通过 IPresentationService 获取素材或播放声音。

BorderValley.Editor 新增 PlaceholderAssetGenerator，用于生成 PNG、WAV 和 PresentationCatalog。

### 4.1 公共接口

BorderValley.Presentation 对外提供以下稳定接口：

    public interface IPresentationService
    {
        bool IsAvailable { get; }
        VisualClip GetVisualClip(string clipId);
        Sprite GetSprite(string spriteId);
        AudioCue GetAudioCue(string cueId);
        string GetAreaMusicCueId(string areaId);
        string GetCharacterVisualPrefix(string definitionId);
        Sprite GetNpcPortrait(string npcId);
        Sprite GetItemIcon(string itemDefinitionId, string slotId);
        Sprite GetUiSprite(string partId);
        void PlayMusic(string cueId);
        void PlaySfx(string cueId);
        void StopMusic();
    }

VisualClip、AudioCue 是 Presentation 程序集内的只读运行时对象。Catalog 的 ScriptableObject 序列化定义在启动时转换成不可变字典。

调用规则：

- Get 系列方法在缺失时返回 fallback，不返回 null。调用方无需自行兜底。
- Play 系列方法在没有 AudioDirector、缺少 cue 或场景未初始化时只记录一次警告。
- StopMusic 只停止当前 Music 通道，不停止 UI、World 或 Battle 音效。
- IsAvailable 表示 Catalog 已加载。false 时所有视觉 Get 返回内置 missing，Play 为 no-op。
- PresentationService 不持有玩法对象，不缓存 Unit、Area 或 Quest 引用。

Boot 场景的 GameBootstrapper.serviceInstallers 增加 PresentationBootstrapInstaller。启动顺序为：

1. GameBootstrapper 创建 GameContext。
2. PresentationBootstrapInstaller 加载 Catalog 并注册 IPresentationService。
3. Inventory 和 Narrative installer 继续注册玩法服务。
4. MainMenu、World、Battle 从 GameContext 获取表现服务。

## 5. PresentationCatalog 数据模型

PresentationCatalog 使用以下逻辑结构。实际字段可使用 Unity 可序列化类型实现。

### 5.1 视觉剪辑

VisualClipDefinition：

- string Id
- Sprite[] Frames
- float FramesPerSecond
- bool Loop
- Sprite Fallback

约束：

- Id 全局唯一且非空。
- Frames 至少一帧。
- FramesPerSecond 大于 0。
- 单帧素材也允许作为静态 clip。
- Fallback 缺失时使用 ui.missing。

### 5.2 音频线索

AudioCueDefinition：

- string Id
- AudioClip Clip
- float Volume
- bool Loop
- AudioChannel Channel

AudioChannel：

- Music
- Ui
- World
- Battle

约束：

- Id 全局唯一且非空。
- Volume 限制在 0 到 1。
- Music clip 必须可循环。
- 缺少 Clip 时该线索为静默并记录警告。

### 5.3 内容映射

Catalog 保存以下映射：

- AreaMusic：areaId 到音乐 cueId。
- EncounterMusic：encounterId 到音乐或战斗前音效 cueId，可选。
- NpcVisual：npcId 到 world NPC clipId。
- NpcPortrait：npcId 到 portrait spriteId。
- CharacterVisual：characterId 到 battle unit clip prefix。
- EnemyVisual：enemyDefinitionId 到 battle unit clip prefix。
- ItemIcon：itemDefinitionId 到 item icon spriteId。
- ItemSlotFallback：ItemSlot 到通用 icon spriteId。
- InteractableVisual：WorldInteractableKind 到世界 clipId。
- UISkin：命名 UI 部件到 Sprite。
- Fallbacks：通用缺失素材。

Catalog 映射缺失时按以下顺序回退：

1. 精确 ID。
2. 分类 fallback。
3. ui.missing 或静默音频。
4. 记录一次聚合警告，避免逐帧刷日志。

## 6. 占位素材生成

### 6.1 生成方式

PlaceholderAssetGenerator 是 Editor-only 工具。它通过菜单和批处理方法同时支持：

- BorderValley/Placeholder/Generate All
- BorderValley.Editor.Tools.PlaceholderAssetGenerator.GenerateAll

生成规则必须确定性，不依赖随机数、系统时间或本机字体。相同版本重复生成应得到相同像素和 PCM 数据。

生成后必须执行 AssetDatabase.Refresh，并确保对应 .meta 文件进入提交。

### 6.2 美术素材

世界：

- 地块：32×32。
- 世界玩家：32×48，四方向，每个方向 idle 2 帧和 walk 2 帧。
- NPC：32×48，每个 NPC 一个 idle 2 帧 clip。
- 障碍：32×32 Tiled sprite。
- 宝箱、采集、调查、区域出口和遭遇图标：32×32。
- NPC 头像：256×256，以简单像素剪影和职业配色区分。

战斗：

- 单位帧：64×64。
- 每类单位生成 idle、move、attack、hit、down 状态帧。
- idle 和 move 至少 2 帧，attack 和 hit 至少 2 帧，down 至少 1 帧。
- 使用职业或敌人类别配色，不在位图内写文字。

UI：

- 面板九宫格：16×16，border 4。
- 按钮普通、悬停、按下三种 16×16 九宫格。
- 通用物品图标：32×32。
- missing 图标：32×32。

导入设置：

- Texture Type：Sprite。
- Sprite Mode：Single，Godot 之外的 Atlas 仅在确实减少文件数时使用。
- Pixels Per Unit：32，战斗 UI 仍按原像素显示。
- Filter Mode：Point。
- Compression：None 或高质量无损。
- Generate Mip Maps：关闭。
- Alpha Is Transparency：开启。
- Wrap Mode：Clamp；Tiled 地块可使用 Repeat。

### 6.3 音频素材

使用 44.1 kHz、16-bit、单声道 PCM WAV。

音乐：

- bgm.menu：8 到 12 秒菜单循环。
- bgm.world.village：8 到 12 秒安全区循环。
- bgm.world.forest：8 到 12 秒森林循环。
- bgm.world.watchtower：8 到 12 秒遗迹循环。
- bgm.world.crypt：8 到 12 秒墓穴循环。
- bgm.battle：8 到 12 秒战斗循环。
- bgm.victory：3 到 5 秒非循环胜利短曲。

音效：

- sfx.ui.click
- sfx.ui.confirm
- sfx.ui.cancel
- sfx.ui.error
- sfx.dialogue.page
- sfx.shop.buy
- sfx.shop.sell
- sfx.inventory.equip
- sfx.inventory.craft
- sfx.world.reward
- sfx.world.encounter
- sfx.battle.attack
- sfx.battle.hit
- sfx.battle.down

素材音量应以对话可听但不盖住后续对白为目标。占位音频允许使用简单方波、三角波和包络，不追求音乐质量。

## 7. 运行时表现设计

### 7.1 SpriteAnimator

SpriteAnimator 是 MonoBehaviour 组件：

- Play(string clipId)
- PlayIfChanged(string clipId)
- Stop()
- CurrentClipId
- IsPlaying

同一个 GameObject 只允许一个 SpriteAnimator。切换 clip 时保留当前帧如果 clip 相同。帧速使用 unscaledDeltaTime，避免暂停或慢动作影响 UI 动画。

世界玩家在移动状态改变或方向改变时调用 PlayIfChanged。实体销毁时必须停止协程和事件订阅。

### 7.2 世界表现

WorldMapView 保留现有碰撞和布局数据，只替换渲染实现：

- 背景使用区域主题 Tiled sprite。
- 障碍使用 area、forest、ruin、crypt 主题 sprite。
- NPC 使用 NpcVisual 映射的 idle clip。
- 宝箱、采集、调查、区域出口使用 InteractableVisual。
- 遭遇使用独立 encounter sprite，保持现有可见性规则。
- 玩家使用 world.player.<direction>.<idle-or-walk> clip。

移动方向由 Joystick.Value 的主轴决定：

- abs(x) 大于 abs(y)：east 或 west。
- 否则：north 或 south。

移动中播放 walk clip，停止后播放该方向 idle clip。角色仍使用现有运动解算和存档写入，动画不影响位置。

DialoguePanelView 增加 256×256 头像区域。DialogueUiPresenter 把当前节点的 SpeakerNpcId 传入 ViewData，View 使用 NpcPortrait 映射。没有头像时显示 ui.missing，不改变对白流程。

### 7.3 战斗表现

BattleGridView 的 CellView 从单 Text 改为：

- Image CellBackground
- Image UnitImage
- Text UnitLabel
- SpriteAnimator UnitAnimator

单位图按 CharacterVisual 或 EnemyVisual 获取 clip 前缀。阵营只影响边框或轻微颜色叠加，不复制素材。

BattlePresentationTracker 为纯 C# 类，接收 BattleState 的只读快照并产生动画事件：

- PositionChanged：Move
- HealthDecreased：Hit
- UnitDied：Down
- ActiveUnit 使用技能或基础攻击后：Attack
- 回合变化：清空攻击/受击状态并回到 Idle

Tracker 不依赖 MonoBehaviour，可通过 EditMode 测试。BattleSceneController 在每次 presenter.Changed 后调用 Tracker.Observe，再把事件交给 BattleGridView。动画播放失败不能改变 BattleEngine 结果。

战斗完成时播放 bgm.victory 或 sfx.error。继续按钮返回场景前停止战斗循环音。

### 7.4 UI 皮肤

WorldPanelViewFactory、MainMenuView 的按钮、BattleHudView、InventoryPanelView、ShopPanelView 和 QuestLogPanelView 使用 UISkin 映射：

- 面板使用 ui.panel 九宫格。
- 按钮使用 ui.button 和 ui.button.pressed。
- 错误文本颜色保持现有语义。
- 物品列表若已有 definitionId，则显示 ItemIcon 或 ItemSlotFallback。
- 不为每个面板复制背景贴图。

现有布局、触摸区域、本地化键和可交互状态保持不变。

## 8. 音频行为

AudioDirector 启动时创建：

- 1 个循环 Music AudioSource。
- 1 个循环 World Ambience AudioSource。首阶段默认静默，保留接口。
- 8 个 Battle SFX AudioSource。
- 6 个 UI SFX AudioSource，并支持全局 polyphony cap 12。
- 1 个保留 AudioSource 用于胜利短曲。

播放规则：

- PlayMusic 相同 cueId 时不重启。
- PlayMusic 新 cueId 时使用 0.15 秒交叉淡出和淡入。
- PlaySfx 在可用通道播放；所有通道忙时丢弃低优先级音效，不排队。
- OnApplicationPause(true) 暂停所有声音并保存 desiredMusicId。
- OnApplicationPause(false) 恢复 AudioListener 和 desiredMusicId。
- 场景切换不销毁 AudioDirector。

事件接入：

- MainMenuView.Start：bgm.menu。
- MainMenuPresenter 成功发起新游戏：sfx.ui.confirm。
- WorldExplorationController.ChangeArea 成功：目标区域 bgm。
- NPC 交互和对话翻页：sfx.dialogue.page。
- 宝箱、采集和任务奖励：sfx.world.reward。
- 遭遇开始：sfx.world.encounter，随后 BattleSceneController 切 bgm.battle。
- 战斗攻击、受击、倒下：对应 battle sfx。
- 商店买入和卖出成功：shop sfx。
- 背包 Equip、Unequip、Craft、Reforge 成功：inventory sfx。
- 可恢复错误：sfx.ui.error。

表现调用不得改变事务结果。音效播放失败只记录警告。

## 9. 测试设计

### 9.1 EditMode

新增覆盖：

- Catalog 重复 ID、空 ID、非正帧率和非法音量校验。
- 缺失映射按精确、分类、missing 顺序回退。
- AudioDirector 的相同音乐不重启、切换音乐、暂停恢复和通道上限。
- SpriteAnimator 的循环、非循环、切帧和 PlayIfChanged。
- 世界方向计算和 idle、walk 状态选择。
- BattlePresentationTracker 对移动、受击、死亡、攻击和回合变化的输出。
- UI 工厂应用 UISkin 后仍满足现有按钮事件和 raycast 行为。
- 素材导入器尺寸、Point filter、无 mipmap 和 PPU 断言。

### 9.2 PlayMode

新增覆盖：

- 主菜单进入 World 后音乐 ID 为 bgm.world.village。
- 区域切换后音乐 ID 随区域变化，位置存档仍正确。
- WorldMapView 生成玩家、NPC、交互物和遭遇 sprite，数量与旧测试一致。
- 玩家移动时 CurrentClipId 进入 walk，停止后回到 idle。
- BattleSceneController 渲染 64×64 单位图，并且原有战斗流程测试继续通过。
- 攻击、受击、倒下事件触发预期动画和音效，不改变 BattleEngine 结果。
- 对话头像、商店、背包和任务日志面板使用皮肤素材，现有 UI 流程仍通过。

### 9.3 自动化门禁

每个 task：

- 对应 EditMode 或 PlayMode 测试 RED 后 GREEN。
- git diff --check。
- 提交包含所有新增 .meta。

阶段收尾：

- 完整 EditMode。
- 完整 PlayMode。
- Android ARM64 IL2CPP 构建。
- 运行整分支 spec review 和 quality review。
- 如进入真机验证，暂停并通知用户，由用户确认设备操作。

## 10. 验收标准

- 不再使用 Texture2D.whiteTexture 作为世界玩家、世界标记、战斗单位或主要面板的最终渲染素材。
- 世界玩家四方向移动动画在真实输入下正确切换。
- 战斗单位的待机、移动、攻击、受击和倒下可由事件触发。
- 菜单、四个区域、战斗和胜利具有独立音乐 cue。
- 对话、商店、背包、打造、奖励和战斗具备基础音效。
- 所有素材可通过 PresentationCatalog 替换，玩法 Definition 和 SaveService schema 不变。
- 缺少可选素材不导致崩溃、卡死或无法操作。
- EditMode、PlayMode 和 Android 构建通过。
- 真机验证若执行，只在用户明确确认后开始；未执行时交接文件标记为未验证。
- 创建 PR，用户确认合并后才更新交接文件并结束阶段。

## 11. 风险与处理

- 生成素材体积失控：只生成固定最小集合，WAV 使用短循环，不提交录音或未压缩长音乐源文件。
- Sprite 数量增加导致加载变慢：首版直接安装进 APK；Catalog 查询按字典构建一次，不在 Update 中分配。
- 动画影响战斗状态：Tracker 只读 BattleState，任何表现异常不得修改 Engine。
- 音效抖动或过密：同一音效设置最小重触发间隔，通道忙时丢弃而非无限排队。
- 最终替换破坏代码：所有运行时查询只通过 Catalog ID，测试断言替换映射后玩法测试不变。
- UI 布局因头像或图标溢出：新增区域使用固定锚点和 Preserve Aspect，保留现有触控区域断言。
