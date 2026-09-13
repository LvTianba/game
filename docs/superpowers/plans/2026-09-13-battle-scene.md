# 战斗场景与 UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 Unity 中增加可触控完成的独立战斗场景，并提供 World → Battle → World 的请求和结果交接。

**Architecture:** `BorderValley.Battle` 继续保存纯规则并补齐地格技能命令；`BorderValley.Core` 保存跨场景战斗流服务；`BorderValley.UI` 保存可单测的表现协调器、网格视图、HUD 和场景控制器。场景由 Editor builder 生成，运行时不调用规则内部实现。

**Tech Stack:** Unity 6000.6.0f1、C#、Unity Test Framework 1.8.0、uGUI、Input System、现有 `BattleEngine` 和 `BattleScenarioFactory`。

**Spec:** `docs/superpowers/specs/2026-09-12-fantasy-tactics-rpg-vertical-slice-design.md`

## Global Constraints

- Android 横屏、API 26、IL2CPP、ARM64、60 FPS、vSync 0。
- 战棋规则不直接读取 `UnityEngine.Time`、Input 或场景对象。
- 所有随机判定通过 `IRandomSource` 注入，同一请求种子必须复现同一战斗。
- 战斗 UI 只调用 `BattleEngine` 的公开命令和查询接口，不复制伤害、目标或地形规则。
- 玩家可见文字使用 `battle.*` 本地化键；首版占位实现直接显示键名，不写最终文案。
- 输入使用 uGUI Button，确保 Android 触控和 EventSystem 工作。
- 不引入 Addressables、正式角色动画、音频或最终像素素材。
- 不把 APK、截图、测试 XML、日志、`.superpowers` 内容提交到仓库。
- 每个 task 单独提交，新增 Unity 文件必须提交对应 `.meta`。

---

## 文件结构

```text
BorderValley/Assets/Code/Core/BattleFlow/
  BattleFlowOutcome.cs
  BattleRequest.cs
  BattleResult.cs
  IBattleFlow.cs
  BattleFlowService.cs

BorderValley/Assets/Code/UI/Battle/
  BattleTextKeys.cs
  BattleHighlightKind.cs
  BattleUiPresenter.cs
  BattleGridView.cs
  BattleHudView.cs
  BattleSceneController.cs
  WorldBattleEntryView.cs

BorderValley/Assets/Editor/Tools/
  BattleSceneBuilder.cs

BorderValley/Assets/Tests/EditMode/Battle/
  BattleGroundTargetCommandTests.cs
  BattleUiPresenterTests.cs

BorderValley/Assets/Tests/EditMode/
  BattleFlowServiceTests.cs

BorderValley/Assets/Tests/PlayMode/
  BattleSceneFlowTests.cs
```

---

### Task 1: 补齐地格技能命令

**Files:**
- Modify: `BorderValley/Assets/Code/Battle/Domain/BattleCommand.cs`
- Modify: `BorderValley/Assets/Code/Battle/Domain/BattleEngine.cs`
- Create: `BorderValley/Assets/Tests/EditMode/Battle/BattleGroundTargetCommandTests.cs`

**Interfaces:**
- Consumes: `SkillExecutor.Execute(BattleState, BattleUnit, SkillDefinition, GridPosition, IRandomSource)`。
- Produces: `UseSkillCommand(unitId, skillId, GridPosition targetPosition)`；`BattleEngine.UseSkill(string, string, GridPosition)`。

- [ ] **Step 1: 写失败测试**

```csharp
using System.Collections.Generic;
using BorderValley.Battle.Domain;
using BorderValley.Core.Random;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class BattleGroundTargetCommandTests
    {
        [Test]
        public void UseSkillCommand_CanTargetEmptyGroundCell()
        {
            var engine = GroundSkillEngine(out var actor, out var enemy);
            actor.MoveForced(new GridPosition(0, 0));
            enemy.MoveForced(new GridPosition(2, 0));

            var result = engine.Execute(new UseSkillCommand(
                actor.Id,
                "skill.quake",
                new GridPosition(1, 0)));

            Assert.That(result.Success, Is.True);
            Assert.That(actor.HasActed, Is.True);
            Assert.That(actor.Mana, Is.EqualTo(8));
            Assert.That(enemy.Health, Is.EqualTo(16));
        }

        [Test]
        public void UseSkillCommand_GroundSkillWithoutPosition_FailsWithoutMutation()
        {
            var engine = GroundSkillEngine(out var actor, out _);

            var result = engine.Execute(new UseSkillCommand(actor.Id, "skill.quake", actor.Id));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("battle.command.error.target_position_required"));
            Assert.That(actor.HasActed, Is.False);
            Assert.That(actor.Mana, Is.EqualTo(10));
        }

        private static BattleEngine GroundSkillEngine(
            out BattleUnit actor,
            out BattleUnit enemy)
        {
            var state = new BattleState(BattleMap.CreatePlain(4, 1));
            actor = new BattleUnit(
                "p1",
                "unit.test",
                Team.Player,
                new UnitStats(20, 10, 8, 0, 5, 0f, 0),
                new GridPosition(0, 0));
            enemy = new BattleUnit(
                "e1",
                "unit.test",
                Team.Enemy,
                new UnitStats(20, 0, 8, 0, 1, 0f, 0),
                new GridPosition(2, 0));
            state.AddUnit(actor);
            state.AddUnit(enemy);
            var skill = new SkillDefinition(
                "skill.quake",
                "skill.quake.name",
                SkillTargeting.Ground,
                2,
                1,
                2,
                0,
                new SkillEffectDefinition(SkillEffectKind.Damage, 1f));
            var skills = new Dictionary<string, SkillDefinition> { [skill.Id] = skill };
            var ownership = new Dictionary<string, string[]> { [actor.Id] = new[] { skill.Id } };
            var engine = new BattleEngine(
                state,
                RandomSourceFactory.FromSeed("ground-target"),
                skills,
                ownership);
            engine.Start();
            return engine;
        }
    }
}
```

- [ ] **Step 2: 运行测试并确认失败**

Run:

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -runTests -testPlatform EditMode `
  -testResults "$PWD\ground-target-fail.xml" -logFile "$PWD\ground-target-fail.log"
```

Expected: 编译失败，因为 `UseSkillCommand` 没有 `GridPosition` 构造函数。

- [ ] **Step 3: 实现命令和引擎分支**

`BattleCommand.cs` 中 `UseSkillCommand`：

```csharp
public UseSkillCommand(string unitId, string skillId, string targetUnitId)
    : this(unitId, skillId, targetUnitId, null)
{
}

public UseSkillCommand(string unitId, string skillId, GridPosition targetPosition)
    : this(unitId, skillId, string.Empty, targetPosition)
{
}

private UseSkillCommand(
    string unitId,
    string skillId,
    string targetUnitId,
    GridPosition? targetPosition)
    : base(unitId)
{
    SkillId = string.IsNullOrWhiteSpace(skillId)
        ? throw new System.ArgumentException(nameof(skillId))
        : skillId;
    TargetUnitId = targetUnitId ?? string.Empty;
    TargetPosition = targetPosition;
}

public string SkillId { get; }
public string TargetUnitId { get; }
public GridPosition? TargetPosition { get; }
```

`BattleEngine.cs` 增加：

```csharp
private const string TargetPositionRequiredError =
    "battle.command.error.target_position_required";

public BattleActionResult UseSkill(
    string unitId,
    string skillId,
    GridPosition targetPosition)
{
    var result = UseSkillCore(unitId, skillId, targetPosition);
    EvaluateOutcome();
    return result;
}

private BattleActionResult UseSkillCore(
    string unitId,
    string skillId,
    GridPosition targetPosition)
{
    if (!TryGetActiveActor(unitId, out var actor, out var failure))
        return failure;
    if (actor.HasActed)
        return Failed(AlreadyActedError);
    if (!skills.TryGetValue(skillId, out var skill))
        return Failed(SkillNotFoundError);
    if (!OwnsSkill(actor.Id, skill.Id))
        return Failed(SkillNotOwnedError);
    if (skill.Targeting != SkillTargeting.Ground)
        return Failed(TargetPositionRequiredError);

    var execution = SkillExecutor.Execute(state, actor, skill, targetPosition, random);
    return execution.Success
        ? Succeeded(SuccessSkillMessage, execution.AffectedUnitIds.ToArray())
        : BattleActionResult.Failed(
            execution.FailureReason,
            execution.FailureReason,
            execution.AffectedUnitIds.ToArray());
}
```

`Execute(BattleCommand)` 的 `UseSkillCommand` 分支改为按目标类型调用对应 `UseSkillCore` 重载；地格技能缺少 `TargetPosition` 时返回 `TargetPositionRequiredError`，非地格技能仍按 `TargetUnitId` 查找单位。

- [ ] **Step 4: 运行 EditMode**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: 全部现有与新增测试通过。

- [ ] **Step 5: 提交**

```powershell
git add BorderValley/Assets/Code/Battle/Domain/BattleCommand.cs `
  BorderValley/Assets/Code/Battle/Domain/BattleEngine.cs `
  BorderValley/Assets/Tests/EditMode/Battle/BattleGroundTargetCommandTests.cs `
  BorderValley/Assets/Tests/EditMode/Battle/BattleGroundTargetCommandTests.cs.meta
git commit -m "feat(battle): support ground target commands"
```

---

### Task 2: 跨场景战斗流服务

**Files:**
- Create: `BorderValley/Assets/Code/Core/BattleFlow/BattleFlowOutcome.cs`
- Create: `BorderValley/Assets/Code/Core/BattleFlow/BattleRequest.cs`
- Create: `BorderValley/Assets/Code/Core/BattleFlow/BattleResult.cs`
- Create: `BorderValley/Assets/Code/Core/BattleFlow/IBattleFlow.cs`
- Create: `BorderValley/Assets/Code/Core/BattleFlow/BattleFlowService.cs`
- Modify: `BorderValley/Assets/Code/Core/Boot/GameBootstrapper.cs`
- Create: `BorderValley/Assets/Tests/EditMode/BattleFlowServiceTests.cs`

**Interfaces:**
- Produces: `IBattleFlow.BeginBattle(BattleRequest)`、`TryTakeRequest(out BattleRequest)`、`CompleteBattle(BattleResult)`、`TryTakeResult(out BattleResult)`。
- Consumes: `GameContext.Register<IBattleFlow>(new BattleFlowService())`。

- [ ] **Step 1: 写失败测试**

```csharp
using BorderValley.Core.BattleFlow;
using NUnit.Framework;

namespace BorderValley.EditModeTests
{
    public sealed class BattleFlowServiceTests
    {
        [Test]
        public void RequestAndResult_AreConsumedOnce()
        {
            var flow = new BattleFlowService();
            var request = new BattleRequest("core", "seed-1", "World");
            var result = new BattleResult(BattleFlowOutcome.PlayerVictory, 7);

            flow.BeginBattle(request);
            Assert.That(flow.TryTakeRequest(out var takenRequest), Is.True);
            Assert.That(takenRequest.ScenarioId, Is.EqualTo("core"));
            Assert.That(flow.TryTakeRequest(out _), Is.False);

            flow.CompleteBattle(result);
            Assert.That(flow.TryTakeResult(out var takenResult), Is.True);
            Assert.That(takenResult.Outcome, Is.EqualTo(BattleFlowOutcome.PlayerVictory));
            Assert.That(takenResult.Rounds, Is.EqualTo(7));
            Assert.That(flow.TryTakeResult(out _), Is.False);
        }

        [Test]
        public void BeginBattle_RejectsSecondActiveRequest()
        {
            var flow = new BattleFlowService();
            flow.BeginBattle(new BattleRequest("core", "a", "World"));

            Assert.That(
                () => flow.BeginBattle(new BattleRequest("core", "b", "World")),
                Throws.InvalidOperationException);
        }
    }
}
```

- [ ] **Step 2: 运行测试并确认失败**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: 编译失败，`BattleFlowService` 尚不存在。

- [ ] **Step 3: 实现最小流服务**

```csharp
namespace BorderValley.Core.BattleFlow
{
    public enum BattleFlowOutcome { InProgress, PlayerVictory, EnemyVictory }

    public sealed class BattleRequest
    {
        public BattleRequest(string scenarioId, string seed, string returnScene)
        {
            ScenarioId = Require(scenarioId, nameof(scenarioId));
            Seed = Require(seed, nameof(seed));
            ReturnScene = Require(returnScene, nameof(returnScene));
        }
        public string ScenarioId { get; }
        public string Seed { get; }
        public string ReturnScene { get; }
        private static string Require(string value, string name) =>
            string.IsNullOrWhiteSpace(value)
                ? throw new System.ArgumentException(name)
                : value;
    }

    public sealed class BattleResult
    {
        public BattleResult(BattleFlowOutcome outcome, int rounds)
        {
            Outcome = outcome;
            Rounds = rounds < 0 ? 0 : rounds;
        }
        public BattleFlowOutcome Outcome { get; }
        public int Rounds { get; }
    }

    public interface IBattleFlow
    {
        void BeginBattle(BattleRequest request);
        bool TryTakeRequest(out BattleRequest request);
        void CompleteBattle(BattleResult result);
        bool TryTakeResult(out BattleResult result);
    }

    public sealed class BattleFlowService : IBattleFlow
    {
        private BattleRequest request;
        private BattleResult result;
        public void BeginBattle(BattleRequest value)
        {
            if (value == null) throw new System.ArgumentNullException(nameof(value));
            if (request != null) throw new System.InvalidOperationException("A battle request is already active.");
            request = value;
        }
        public bool TryTakeRequest(out BattleRequest value)
        {
            value = request;
            request = null;
            return value != null;
        }
        public void CompleteBattle(BattleResult value) =>
            result = value ?? throw new System.ArgumentNullException(nameof(value));
        public bool TryTakeResult(out BattleResult value)
        {
            value = result;
            result = null;
            return value != null;
        }
    }
}
```

`GameBootstrapper.Awake()` 在注册 `ISceneLoader` 后注册：

```csharp
Context.Register<IBattleFlow>(new BattleFlowService());
```

- [ ] **Step 4: 运行测试**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: 全部通过。

- [ ] **Step 5: 提交**

```powershell
git add BorderValley/Assets/Code/Core/BattleFlow `
  BorderValley/Assets/Code/Core/Boot/GameBootstrapper.cs `
  BorderValley/Assets/Tests/EditMode/BattleFlowServiceTests.cs `
  BorderValley/Assets/Tests/EditMode/BattleFlowServiceTests.cs.meta
git commit -m "feat(core): add battle flow handoff"
```

---

### Task 3: 可测试的战斗表现协调器

**Files:**
- Create: `BorderValley/Assets/Code/UI/Battle/BattleTextKeys.cs`
- Create: `BorderValley/Assets/Code/UI/Battle/BattleHighlightKind.cs`
- Create: `BorderValley/Assets/Code/UI/Battle/BattleUiPresenter.cs`
- Create: `BorderValley/Assets/Tests/EditMode/Battle/BattleUiPresenterTests.cs`

**Interfaces:**
- Consumes: `BattleScenarioFactory.CreateCoreScenario()`、`BattleEngine`、`GridPathfinder`、`SkillTargetValidator`。
- Produces: `Start()`、`SelectSkill(string)`、`TapCell(GridPosition)`、`EndTurn()`、`Execute(BattleCommand)`、`GetHighlight(GridPosition)`、`Changed`。

- [ ] **Step 1: 写失败测试**

```csharp
using BorderValley.Battle.Domain;
using BorderValley.Core.Random;
using BorderValley.UI.Battle;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class BattleUiPresenterTests
    {
        [Test]
        public void TapCell_OnReachableCell_MovesActiveUnit()
        {
            var presenter = Create();
            var actor = presenter.ActiveUnit;

            var result = presenter.TapCell(new GridPosition(0, 2));

            Assert.That(result.Success, Is.True);
            Assert.That(actor.Position, Is.EqualTo(new GridPosition(0, 2)));
        }

        [Test]
        public void SelectBasicSkill_ThenTapEnemy_UsesSkill()
        {
            var presenter = Create();
            presenter.TapCell(new GridPosition(4, 2));
            var enemy = presenter.Engine.State.GetUnit("enemy.bandit");
            var health = enemy.Health;

            presenter.SelectSkill("skill.basic");
            var result = presenter.TapCell(enemy.Position);

            Assert.That(result.Success, Is.True);
            Assert.That(enemy.Health, Is.LessThan(health));
        }

        [Test]
        public void GetHighlight_SelectedSkill_MarksTargetCells()
        {
            var presenter = Create();
            presenter.SelectSkill("skill.piercing_shot");

            Assert.That(
                presenter.GetHighlight(new GridPosition(5, 2)),
                Is.EqualTo(BattleHighlightKind.Target));
        }

        private static BattleUiPresenter Create()
        {
            var presenter = new BattleUiPresenter(
                BattleScenarioFactory.CreateCoreScenario(),
                RandomSourceFactory.FromSeed("presenter"));
            presenter.Start();
            return presenter;
        }
    }
}
```

- [ ] **Step 2: 运行测试并确认失败**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: 编译失败，表现协调器尚不存在。

- [ ] **Step 3: 实现协调器**

`BattleUiPresenter` 的核心规则：

```csharp
public sealed class BattleUiPresenter
{
    private readonly BattleScenario scenario;
    private readonly BattleEngine engine;
    public event Action Changed;

    public BattleEngine Engine => engine;
    public BattleUnit ActiveUnit => engine.ActiveUnit;
    public bool IsPlayerTurn => ActiveUnit != null && ActiveUnit.Team == Team.Player;
    public bool IsFinished => engine.Outcome != BattleOutcome.InProgress;
    public string SelectedSkillId { get; private set; }
    public BattleOutcome Outcome => engine.Outcome;

    public void Start()
    {
        engine.Start();
        Notify();
    }

    public void SelectSkill(string skillId)
    {
        if (!IsPlayerTurn || ActiveUnit.HasActed || string.IsNullOrWhiteSpace(skillId))
            return;
        SelectedSkillId = SelectedSkillId == skillId ? null : skillId;
        Notify();
    }

    public BattleActionResult TapCell(GridPosition cell)
    {
        if (!IsPlayerTurn)
            return BattleActionResult.Failed(BattleTextKeys.NotPlayerTurn);

        var result = SelectedSkillId == null
            ? TryMoveOrBasicAttack(cell)
            : TryUseSelectedSkill(cell);
        SelectedSkillId = null;
        Notify();
        return result;
    }

    public BattleActionResult EndTurn() =>
        IsPlayerTurn
            ? Execute(new EndTurnCommand(ActiveUnit.Id))
            : BattleActionResult.Failed(BattleTextKeys.NotPlayerTurn);

    public BattleActionResult Execute(BattleCommand command)
    {
        var result = engine.Execute(command);
        Notify();
        return result;
    }
}
```

`TryMoveOrBasicAttack` 的顺序固定为：先尝试射程内敌方单位的 `skill.basic`，否则执行 `MoveCommand`。`TryUseSelectedSkill` 对 `Ground` 使用 `SkillTargetValidator.GetValidGroundTargets` 和 `UseSkillCommand(actor, skill, cell)`；其他类型按格子上的存活单位调用 `SkillTargetValidator.GetValidTargets`，无效时返回 `BattleTextKeys.InvalidTarget`。

`GetHighlight` 规则固定为：

```csharp
public BattleHighlightKind GetHighlight(GridPosition cell)
{
    if (!IsPlayerTurn) return BattleHighlightKind.None;
    if (SelectedSkillId == null)
        return ReachableCells.Contains(cell)
            ? BattleHighlightKind.Move
            : BattleHighlightKind.None;

    var skill = engine.GetOwnedSkills(ActiveUnit.Id)[SelectedSkillId];
    if (skill.Targeting == SkillTargeting.Ground)
        return SkillTargetValidator.GetValidGroundTargets(engine.State, ActiveUnit, skill).Contains(cell)
            ? BattleHighlightKind.SkillRange
            : BattleHighlightKind.None;

    return SkillTargetValidator.GetValidTargets(engine.State, ActiveUnit, skill)
        .Any(unit => unit.Position == cell)
        ? BattleHighlightKind.Target
        : BattleHighlightKind.None;
}
```

`BattleTextKeys` 只包含键字符串，例如：

```csharp
public const string EndTurn = "battle.ui.end_turn";
public const string Continue = "battle.ui.continue";
public const string InvalidTarget = "battle.ui.error.invalid_target";
public const string NotPlayerTurn = "battle.ui.error.not_player_turn";
```

- [ ] **Step 4: 运行测试**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: 全部通过。

- [ ] **Step 5: 提交**

```powershell
git add BorderValley/Assets/Code/UI/Battle `
  BorderValley/Assets/Tests/EditMode/Battle/BattleUiPresenterTests.cs `
  BorderValley/Assets/Tests/EditMode/Battle/BattleUiPresenterTests.cs.meta
git commit -m "feat(ui): add battle presentation coordinator"
```

---

### Task 4: 战斗场景、网格和 HUD

**Files:**
- Create: `BorderValley/Assets/Code/UI/Battle/BattleGridView.cs`
- Create: `BorderValley/Assets/Code/UI/Battle/BattleHudView.cs`
- Create: `BorderValley/Assets/Code/UI/Battle/BattleSceneController.cs`
- Create: `BorderValley/Assets/Editor/Tools/BattleSceneBuilder.cs`
- Modify: `BorderValley/Assets/Editor/Tools/FoundationSceneBuilder.cs`
- Modify: `BorderValley/Assets/Editor/Build/BuildAndroid.cs`
- Create: `BorderValley/Assets/Tests/PlayMode/BattleSceneFlowTests.cs`

**Interfaces:**
- Consumes: `IBattleFlow.TryTakeRequest`、`BattleUiPresenter`、`BattleAi.ChooseCommand`。
- Produces: `BattleSceneController.RenderedCellCount`、`RenderedUnitCount`、`EndTurnButton`。

- [ ] **Step 1: 写 PlayMode 失败测试**

```csharp
using System.Collections;
using BorderValley.UI.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BorderValley.PlayModeTests
{
    public sealed class BattleSceneFlowTests
    {
        [UnityTest]
        public IEnumerator BattleScene_RendersCoreScenario()
        {
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;

            var controller = Object.FindFirstObjectByType<BattleSceneController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.RenderedCellCount, Is.EqualTo(48));
            Assert.That(controller.RenderedUnitCount, Is.EqualTo(6));
            Assert.That(controller.EndTurnButton, Is.Not.Null);
        }
    }
}
```

- [ ] **Step 2: 运行测试并确认失败**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: `BattleSceneFlowTests` 失败，因为 Build Settings 和场景尚不存在。

- [ ] **Step 3: 实现视图组件**

`BattleSceneController.Start()`：

```csharp
flow = GameBootstrapper.Context == null
    ? new BattleFlowService()
    : GameBootstrapper.Context.Get<IBattleFlow>();
if (!flow.TryTakeRequest(out request))
    request = new BattleRequest("core", "battle-scene-debug", string.Empty);

presenter = new BattleUiPresenter(
    BattleScenarioFactory.CreateCoreScenario(),
    RandomSourceFactory.FromSeed(request.Seed));
presenter.Changed += Refresh;

var root = new GameObject(
    "BattleCanvas",
    typeof(RectTransform),
    typeof(Canvas),
    typeof(CanvasScaler),
    typeof(GraphicRaycaster));
root.transform.SetParent(transform, false);
ConfigureCanvas(root.GetComponent<Canvas>(), root.GetComponent<CanvasScaler>());

gridView = CreateGridView(root.transform);
hudView = CreateHudView(root.transform);
gridView.Initialize(OnCellSelected);
hudView.Initialize(OnSkillSelected, OnEndTurn, OnContinue);
presenter.Start();
Refresh();
```

`BattleGridView`：

- 使用 `GridLayoutGroup` 创建 `map.Width * map.Height` 个 `Button`。
- 每格保存 `GridPosition` 和背景 `Image`。
- `Render(BattleUiPresenter)` 按 `GetHighlight` 设置颜色：移动蓝、技能范围黄、目标红。
- 在对应格子内绘制单位标记；玩家和敌人使用不同颜色，并把 `DefinitionId` 放入 `Text` 占位。
- 暴露 `CellCount`、`UnitCount`、`Tap(GridPosition)` 供测试。

`BattleHudView`：

- 顶部显示回合数、当前单位和行动顺序。
- 左侧或底部显示当前单位生命、法力、`HasMoved/HasActed` 和状态。
- 技能按钮由 `engine.GetOwnedSkills(activeUnit.Id)` 生成，按钮文字直接使用 `SkillDefinition.LocalizationKey`。
- 显示“结束回合”按钮；文字使用 `BattleTextKeys.EndTurn`。
- 结果面板显示 `battle.result.player_victory` 或 `battle.result.enemy_victory`，继续按钮使用 `BattleTextKeys.Continue`。
- 暴露 `EndTurnButton`、`SkillButtons`、`ContinueButton`，测试不依赖层级名称。

所有玩家可见字符串经过以下占位方法，禁止在视图里写最终文案：

```csharp
private static string Key(string localizationKey) => localizationKey;
```

- [ ] **Step 4: 让敌方 AI 自动行动**

`BattleSceneController` 在玩家命令成功后启动：

```csharp
private IEnumerator RunEnemyTurns()
{
    while (!presenter.IsFinished && presenter.ActiveUnit.Team == Team.Enemy)
    {
        var actor = presenter.ActiveUnit;
        var command = BattleAi.ChooseCommand(
            presenter.Engine,
            actor.Id,
            presenter.Engine.GetSkillsForUnitById(actor.Id));
        presenter.Execute(command);
        Refresh();
        yield return new WaitForSeconds(0.35f);
    }
}
```

战斗结束时停止接收地格触控，显示结果面板。继续按钮将 `BattleOutcome` 映射为 `BattleFlowOutcome`，调用 `flow.CompleteBattle`，再通过 `ISceneLoader` 返回 `request.ReturnScene`；调试请求的 `ReturnScene` 为空时留在当前场景。

- [ ] **Step 5: 生成 Battle 场景**

`BattleSceneBuilder.BuildScene()` 使用 `EditorSceneManager.NewScene` 创建：

- Main Camera，正交、SolidColor、非黑背景。
- EventSystem + StandaloneInputModule。
- `BattleSceneRoot`，挂载 `BattleSceneController`。
- 保存到 `Assets/Scenes/Battle.unity`。

`FoundationSceneBuilder.Rebuild()` 在 World 场景之后调用 `BattleSceneBuilder.BuildScene()`，并把 Build Settings 设为：

```csharp
EditorBuildSettings.scenes = new[]
{
    new EditorBuildSettingsScene("Assets/Scenes/Boot.unity", true),
    new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
    new EditorBuildSettingsScene("Assets/Scenes/World.unity", true),
    new EditorBuildSettingsScene("Assets/Scenes/Battle.unity", true)
};
```

`BuildAndroid.PerformBuild()` 的 `scenes` 数组同样加入 `Assets/Scenes/Battle.unity`。

Run:

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -executeMethod BorderValley.Editor.FoundationSceneBuilder.Rebuild `
  -quit -logFile "$PWD\rebuild-scenes.log"
```

Expected: 退出码 0；`Assets/Scenes/Battle.unity` 和 `.meta` 生成；Build Settings 包含四个场景。

- [ ] **Step 6: 运行完整测试**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: EditMode 与 PlayMode 全部通过。

- [ ] **Step 7: 提交**

```powershell
git add BorderValley/Assets/Code/UI/Battle `
  BorderValley/Assets/Editor/Tools/BattleSceneBuilder.cs `
  BorderValley/Assets/Editor/Tools/BattleSceneBuilder.cs.meta `
  BorderValley/Assets/Editor/Tools/FoundationSceneBuilder.cs `
  BorderValley/Assets/Editor/Build/BuildAndroid.cs `
  BorderValley/Assets/Scenes/Battle.unity `
  BorderValley/Assets/Scenes/Battle.unity.meta `
  BorderValley/Assets/Tests/PlayMode/BattleSceneFlowTests.cs `
  BorderValley/Assets/Tests/PlayMode/BattleSceneFlowTests.cs.meta
git commit -m "feat(battle): add playable battle scene and hud"
```

---

### Task 5: World 战斗入口和端到端交接

**Files:**
- Create: `BorderValley/Assets/Code/UI/Battle/WorldBattleEntryView.cs`
- Modify: `BorderValley/Assets/Editor/Tools/FoundationSceneBuilder.cs`
- Modify: `BorderValley/Assets/Tests/PlayMode/BattleSceneFlowTests.cs`

**Interfaces:**
- Consumes: `IBattleFlow.BeginBattle`、`IBattleFlow.TryTakeResult`、`ISceneLoader.LoadAsync`。
- Produces: `WorldBattleEntryView.BattleButton`、`ResultLabel`。

- [ ] **Step 1: 写失败流程测试**

在 `BattleSceneFlowTests` 增加：

```csharp
[UnityTest]
public IEnumerator MainMenu_ToWorld_ToBattle()
{
    yield return SceneManager.LoadSceneAsync("Boot");
    yield return null;

    var menuButton = Object.FindFirstObjectByType<UnityEngine.UI.Button>();
    menuButton.onClick.Invoke();
    yield return WaitForScene("World");

    var entry = Object.FindFirstObjectByType<WorldBattleEntryView>();
    Assert.That(entry, Is.Not.Null);
    entry.BattleButton.onClick.Invoke();
    yield return WaitForScene("Battle");

    Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Battle"));
    Assert.That(Object.FindFirstObjectByType<BattleSceneController>(), Is.Not.Null);
}

private static IEnumerator WaitForScene(string sceneName)
{
    for (var i = 0; i < 180 && SceneManager.GetActiveScene().name != sceneName; i++)
        yield return null;
}
```

- [ ] **Step 2: 运行测试并确认失败**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: `WorldBattleEntryView` 尚不存在。

- [ ] **Step 3: 实现 World 入口**

`WorldBattleEntryView.Start()`：

```csharp
flow = GameBootstrapper.Context.Get<IBattleFlow>();
loader = GameBootstrapper.Context.Get<ISceneLoader>();
CreateCanvasAndButton();

if (flow.TryTakeResult(out var result))
    resultLabel.text = result.Outcome switch
    {
        BattleFlowOutcome.PlayerVictory => "battle.result.player_victory",
        BattleFlowOutcome.EnemyVictory => "battle.result.enemy_victory",
        _ => "battle.result.in_progress"
    };
```

按钮点击：

```csharp
flow.BeginBattle(new BattleRequest(
    "core",
    "vertical-slice",
    "World"));
_ = loader.LoadAsync("Battle");
```

`FoundationSceneBuilder` 的 World 场景增加 `WorldBattleEntryView` 组件；该组件自行创建 Canvas、EventSystem（若缺失）、战斗按钮和结果 `Text`。

- [ ] **Step 4: 运行完整测试**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: 所有 EditMode 和 PlayMode 测试通过，主菜单可以进入 World 再进入 Battle。

- [ ] **Step 5: 提交**

```powershell
git add BorderValley/Assets/Code/UI/Battle/WorldBattleEntryView.cs `
  BorderValley/Assets/Code/UI/Battle/WorldBattleEntryView.cs.meta `
  BorderValley/Assets/Editor/Tools/FoundationSceneBuilder.cs `
  BorderValley/Assets/Tests/PlayMode/BattleSceneFlowTests.cs
git commit -m "feat(battle): wire world entry to battle flow"
```

---

## 完成标准

- `Boot → MainMenu → World → Battle` 可由触控按钮进入。
- Battle 场景渲染 8×6 网格、6 个单位、行动顺序、当前单位、生命、法力、状态和结束回合按钮。
- 玩家可以触控移动、选择技能、选择单位目标或地格目标，并看到范围和目标高亮。
- 敌方单位按 `BattleAi` 自动行动；玩家和敌方行动都由同一个 `BattleEngine` 校验。
- 战斗结束后能返回 World，World 可消费一次 `BattleResult`。
- 同一种子重复运行产生相同结果。
- 完整 EditMode 和 PlayMode 通过。
- Android ARM64 APK 构建通过。
- 真机测试阶段先运行 `adb devices -l`；检测到设备后暂停并通知用户，不自行安装或启动。
- 最终整分支审查无未处理 Critical/Important。

