# 奇幻像素战棋 RPG 工程基础 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立 Unity 6000.6.0f1 Android 工程、模块边界、确定性随机、原子存档、内容校验、启动流程和可重复 Android 构建。

**Architecture:** 使用 `BorderValley` Unity 工程和按职责拆分的程序集。`Core` 提供随机数、存档、场景加载和组合根；`Data` 提供静态内容定义与校验；`UI`、`World`、`Battle`、`Inventory`、`Narrative` 先建立空边界，由后续计划填充。所有持久化数据通过 `ISaveParticipant` 捕获为 JSON，不保存场景对象引用。

**Tech Stack:** Unity 6000.6.0f1、C#、Unity Test Framework 1.8.0（Unity 内置）、Input System 1.20.0、Newtonsoft.Json 3.2.2、UGUI 2.6.0、Android IL2CPP/ARM64

**Spec:** `docs/superpowers/specs/2026-09-12-fantasy-tactics-rpg-vertical-slice-design.md`

## Global Constraints

- 首发平台为 Android 横屏，最低 Android 8.0（API 26）。
- Unity 版本固定为 6000.6.0f1，changeset 为 f7f8ed4d1e24。
- Android 使用 IL2CPP 与 ARM64，帧率固定为 60 FPS；Boot 的 GameBootstrapper.Awake 必须在场景流转前执行一次 QualitySettings.vSyncCount = 0 与 Application.targetFrameRate = 60，确保 90/120 Hz 设备不会高于 60 FPS，冒烟测试必须包含 120 Hz 设备验证。
- 不加入联网、账号、云存档、内购、广告、iOS、手柄或正式 PC 发布。
- 代码文字使用本地化键，不在玩法代码中硬编码玩家文本。
- 存档采用临时文件写入、校验、替换和备份，不直接覆盖正式文件。
- 存档不保存场景对象引用，只保存定义 ID、实例 ID、数值、状态和随机种子。
- 所有随机行为必须接受可注入的 `IRandomSource`。
- 每个任务结束时必须通过对应测试并单独提交。
- 依赖版本固定；Unity Test Framework 使用 Unity 6000.6.0f1 内置版本 1.8.0；升级任何 Unity 包都必须单独提交并运行完整测试。
- Addressables 与 Localization 不在本计划安装，延后至内容管线计划；基础阶段仅使用 UGUI。

## 计划边界

本计划只交付工程基础和可构建的游戏外壳，结束状态是：

- Unity 工程可打开且无编译错误。
- 编辑模式测试与启动流程测试通过。
- 主菜单可以进入空白世界场景。
- 原生存档能够保存、加载、检测损坏并从备份恢复。
- 内容目录能够发现重复或非法 ID。
- 能通过一条命令生成可安装的 Android APK。

战斗、随机装备、NPC、任务、交易和地图内容由后续独立计划实现。后续计划必须复用本计划定义的 `IRandomSource`、`ISaveParticipant`、`ContentDefinition`、`GameContext` 和场景加载接口。

---

### Task 1: 安装固定版本 Unity 并创建工程

**Files:**
- Create: `scripts/verify-unity.ps1`
- Create: `scripts/install-unity.ps1`
- Create: `.gitignore`
- Create: `.gitattributes`
- Create: `BorderValley/Assets/.gitkeep`

**Interfaces:**
- Consumes: Windows `winget`，Unity Hub 3.21.2。
- Produces: `D:\unity\editor\6000.6.0f1\Editor\Unity.exe`，Unity 工程 `D:\program\phone game\BorderValley`。

- [ ] **Step 1: 写工具链检查脚本并确认当前失败**

创建 `scripts/verify-unity.ps1`：

```powershell
$ErrorActionPreference = 'Stop'
$unity = 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe'
if (-not (Test-Path -LiteralPath $unity)) {
    throw "Unity 6000.6.0f1 is not installed at $unity"
}
$version = & $unity -version
if ($version -notmatch '6000\.6\.0f1') {
    throw "Unexpected Unity version: $version"
}
& $unity -batchmode -quit -nographics -logFile - | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Unity batch mode failed with exit code $LASTEXITCODE"
}
Write-Output 'Unity 6000.6.0f1 toolchain is ready.'
```

运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-unity.ps1
```

预期：失败并显示 `Unity 6000.6.0f1 is not installed`。

- [ ] **Step 2: 安装 Unity Hub、编辑器和 Android 模块**

创建 `scripts/install-unity.ps1`：

```powershell
$ErrorActionPreference = 'Stop'
$unityVersion = '6000.6.0f1'
$unityChangeset = 'f7f8ed4d1e24'
$unityInstallRoot = 'D:\unity\editor'
$unityEditor = Join-Path $unityInstallRoot "$unityVersion\Editor\Unity.exe"
$androidPlayer = Join-Path $unityInstallRoot "$unityVersion\Editor\Data\PlaybackEngines\AndroidPlayer"

function Get-UnityHubPath {
    $candidates = @(
        'D:\unity\Unity Hub\Unity Hub.exe',
        "$env:LOCALAPPDATA\Programs\Unity Hub\Unity Hub.exe",
        "$env:LOCALAPPDATA\Unity Hub\Unity Hub.exe",
        'C:\Program Files\Unity Hub\Unity Hub.exe',
        "$env:LOCALAPPDATA\Microsoft\WindowsApps\Unity Hub.exe"
    )

    $appx = Get-AppxPackage -Name 'UnityTechnologies.UnityHub' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $appx) {
        $appx = Get-AppxPackage -ErrorAction SilentlyContinue | Where-Object { $_.Name -like '*UnityHub*' } | Select-Object -First 1
    }
    if ($appx) {
        $candidates += (Join-Path $appx.InstallLocation 'app\Unity Hub.exe')
    }

    return ($candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1)
}

function Test-AndroidModulesPresent {
    $ndk = Join-Path $androidPlayer 'NDK'
    $sdk = Join-Path $androidPlayer 'SDK'
    $openJdk = Join-Path $androidPlayer 'OpenJDK'

    $present = (Test-Path -LiteralPath $androidPlayer) -and
        (Test-Path -LiteralPath (Join-Path $ndk 'build')) -and
        (Test-Path -LiteralPath (Join-Path $ndk 'toolchains')) -and
        (Test-Path -LiteralPath (Join-Path $sdk 'platform-tools')) -and
        (Test-Path -LiteralPath (Join-Path $sdk 'cmdline-tools')) -and
        (Test-Path -LiteralPath (Join-Path $sdk 'platforms')) -and
        (Test-Path -LiteralPath (Join-Path $openJdk 'bin'))

    return $present
}

$hub = Get-UnityHubPath
if (-not $hub) {
    winget install --id Unity.UnityHub --exact --silent `
        --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) {
        throw "winget install Unity Hub failed with exit code $LASTEXITCODE"
    }
    $hub = Get-UnityHubPath
}
if (-not $hub) { throw 'Unity Hub executable was not found.' }

function Invoke-UnityHubHeadless {
    param([string[]]$Arguments)

    $out = Join-Path $env:TEMP ("unity-hub-{0}-out.txt" -f [guid]::NewGuid().ToString('N'))
    $err = Join-Path $env:TEMP ("unity-hub-{0}-err.txt" -f [guid]::NewGuid().ToString('N'))
    Remove-Item -LiteralPath $out,$err -ErrorAction SilentlyContinue

    $p = Start-Process -FilePath $hub -ArgumentList $Arguments -Wait -PassThru `
        -WindowStyle Hidden -RedirectStandardOutput $out -RedirectStandardError $err

    Get-Content -LiteralPath $out -ErrorAction SilentlyContinue | Out-Host
    Get-Content -LiteralPath $err -ErrorAction SilentlyContinue | Out-Host

    $global:LASTEXITCODE = $p.ExitCode
    if ($LASTEXITCODE -ne 0) {
        throw "Unity Hub command failed with exit code ${LASTEXITCODE}: $($Arguments -join ' ')"
    }
}

Invoke-UnityHubHeadless -Arguments @(
    '--','--headless','install-path','--set',$unityInstallRoot
)

if (-not (Test-Path -LiteralPath $unityEditor)) {
    Invoke-UnityHubHeadless -Arguments @(
        '--','--headless','install',
        '--version',$unityVersion,
        '--changeset',$unityChangeset,
        '--module','android','android-sdk-ndk-tools','android-open-jdk-17.0.18+8'
    )
}
elseif (-not (Test-AndroidModulesPresent)) {
    Invoke-UnityHubHeadless -Arguments @(
        '--','--headless','install-modules',
        '--version',$unityVersion,
        '--module','android','android-sdk-ndk-tools','android-open-jdk-17.0.18+8'
    )
}
else {
    Write-Output 'Android modules are already installed; skipping Unity Hub install-modules.'
}
```

运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install-unity.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-unity.ps1
```

预期：最后输出 `Unity 6000.6.0f1 toolchain is ready.`。安装完成后，在 Unity Hub 登录 Unity 账号并激活 Personal 或 Pro 许可证，否则第三步的批量模式无法运行。

- [ ] **Step 3: 创建 Unity 工程**

```powershell
$unity = 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe'
& $unity -batchmode -quit -createProject "$PWD\BorderValley" `
    -logFile "$PWD\BorderValley-create.log"
if ($LASTEXITCODE -ne 0) { throw "Project creation failed: $LASTEXITCODE" }
```

预期：存在 `BorderValley\Assets`、`BorderValley\Packages\manifest.json`、`BorderValley\ProjectSettings\ProjectVersion.txt`，且版本为 `6000.6.0f1`。

- [ ] **Step 4: 添加仓库规则并提交**

`.gitignore`：

```gitignore
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/
[Mm]emoryCaptures/
[Rr]ecordings/
*.csproj
*.sln
*.suo
*.user
*.userprefs
.vs/
.idea/
.vscode/
.worktrees/
.superpowers/
BorderValley-create.log
```

`.gitattributes`：

```gitattributes
* text=auto
*.cs text eol=lf
*.json text eol=lf
*.asmdef text eol=lf
*.unity text eol=lf
*.prefab text eol=lf
*.asset text eol=lf
*.png filter=lfs diff=lfs merge=lfs -text
*.jpg filter=lfs diff=lfs merge=lfs -text
*.psd filter=lfs diff=lfs merge=lfs -text
*.wav filter=lfs diff=lfs merge=lfs -text
*.mp3 filter=lfs diff=lfs merge=lfs -text
*.ogg filter=lfs diff=lfs merge=lfs -text
*.ttf filter=lfs diff=lfs merge=lfs -text
```

```powershell
git lfs install
git add .gitignore .gitattributes scripts BorderValley
git commit -m "build: bootstrap Unity 6000.6.0f1 project"
```

---

### Task 2: 建立程序集边界和组合根

**Files:**
- Create: `BorderValley/Assets/Code/Core/BorderValley.Core.asmdef`
- Create: `BorderValley/Assets/Code/Core/GameContext.cs`
- Create: `BorderValley/Assets/Code/Data/BorderValley.Data.asmdef`
- Create: `BorderValley/Assets/Code/Battle/BorderValley.Battle.asmdef`
- Create: `BorderValley/Assets/Code/Inventory/BorderValley.Inventory.asmdef`
- Create: `BorderValley/Assets/Code/Narrative/BorderValley.Narrative.asmdef`
- Create: `BorderValley/Assets/Code/World/BorderValley.World.asmdef`
- Create: `BorderValley/Assets/Code/UI/BorderValley.UI.asmdef`
- Create: `BorderValley/Assets/Editor/BorderValley.Editor.asmdef`
- Create: `BorderValley/Assets/Tests/EditMode/BorderValley.EditModeTests.asmdef`
- Create: `BorderValley/Assets/Tests/EditMode/GameContextTests.cs`
- Create: `BorderValley/Assets/Tests/PlayMode/BorderValley.PlayModeTests.asmdef`
- Create: `BorderValley/Assets/Tests/PlayMode/AssemblySmokeTests.cs`

**Interfaces:**
- Produces: `GameContext.Register<T>(T)`、`GameContext.Get<T>()`、`GameContext.TryGet<T>(out T)`。

- [ ] **Step 1: 写失败测试**

```csharp
using NUnit.Framework;

namespace BorderValley.Core.Tests
{
    public sealed class GameContextTests
    {
        [Test]
        public void Get_AfterRegister_ReturnsSameInstance()
        {
            var context = new GameContext();
            var service = new TestService();
            context.Register(service);
            Assert.That(context.Get<TestService>(), Is.SameAs(service));
        }

        [Test]
        public void Get_WhenNotRegistered_Throws()
        {
            var context = new GameContext();
            Assert.Throws<System.InvalidOperationException>(() => context.Get<TestService>());
        }

        private sealed class TestService { }
    }
}
```

编辑模式 asmdef：

```json
{
  "name": "BorderValley.EditModeTests",
  "references": ["BorderValley.Core", "BorderValley.Data", "BorderValley.Battle", "BorderValley.Inventory", "BorderValley.Narrative", "BorderValley.World", "BorderValley.UI"],
  "optionalUnityReferences": ["TestAssemblies"],
  "includePlatforms": ["Editor"],
  "autoReferenced": false
}
```

- [ ] **Step 2: 运行并确认失败**

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -runTests -testPlatform EditMode `
  -testResults "$PWD\editmode-results.xml" -logFile "$PWD\editmode-tests.log"
```

预期：编译失败并报告 `GameContext` 不存在。

- [ ] **Step 3: 更新固定依赖并实现程序集和组合根**

将 `BorderValley/Packages/manifest.json` 的 `dependencies` 固定为以下版本，并保留 Unity 生成的其余内置包：

```json
{
  "com.unity.inputsystem": "1.20.0",
  "com.unity.nuget.newtonsoft-json": "3.2.2",
  "com.unity.test-framework": "1.8.0"
}
```

> 注：Addressables 与 Localization 延后至内容管线计划，届时再添加包及其 asmdef 引用。

通过 Unity 菜单 `Window/Package Manager` 确认解析完成后再继续。然后创建程序集和组合根：

`BorderValley.Core.asmdef`：

```json
{
  "name": "BorderValley.Core",
  "rootNamespace": "BorderValley.Core",
  "autoReferenced": true
}
```

`BorderValley.Data.asmdef`：

```json
{
  "name": "BorderValley.Data",
  "rootNamespace": "BorderValley.Data",
  "references": ["BorderValley.Core"],
  "autoReferenced": true
}
```

`BorderValley.Battle.asmdef`：

```json
{
  "name": "BorderValley.Battle",
  "rootNamespace": "BorderValley.Battle",
  "references": ["BorderValley.Core", "BorderValley.Data"],
  "autoReferenced": true
}
```

`BorderValley.Inventory.asmdef`：

```json
{
  "name": "BorderValley.Inventory",
  "rootNamespace": "BorderValley.Inventory",
  "references": ["BorderValley.Core", "BorderValley.Data"],
  "autoReferenced": true
}
```

`BorderValley.Narrative.asmdef`：

```json
{
  "name": "BorderValley.Narrative",
  "rootNamespace": "BorderValley.Narrative",
  "references": ["BorderValley.Core", "BorderValley.Data", "BorderValley.Inventory"],
  "autoReferenced": true
}
```

`BorderValley.World.asmdef`：

```json
{
  "name": "BorderValley.World",
  "rootNamespace": "BorderValley.World",
  "references": ["BorderValley.Core", "BorderValley.Data", "BorderValley.Battle", "BorderValley.Inventory", "BorderValley.Narrative"],
  "autoReferenced": true
}
```

`BorderValley.UI.asmdef`：

```json
{
  "name": "BorderValley.UI",
  "rootNamespace": "BorderValley.UI",
  "references": ["BorderValley.Core", "BorderValley.Data", "BorderValley.Battle", "BorderValley.Inventory", "BorderValley.Narrative", "BorderValley.World", "Unity.InputSystem"],
  "autoReferenced": true
}
```

`BorderValley.Editor.asmdef`：

```json
{
  "name": "BorderValley.Editor",
  "rootNamespace": "BorderValley.Editor",
  "references": ["BorderValley.Core", "BorderValley.Data", "BorderValley.Battle", "BorderValley.Inventory", "BorderValley.Narrative", "BorderValley.World", "BorderValley.UI"],
  "includePlatforms": ["Editor"],
  "autoReferenced": true
}
```

`GameContext.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace BorderValley.Core
{
    public sealed class GameContext
    {
        private readonly Dictionary<Type, object> services = new();

        public void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            services[typeof(T)] = service;
        }

        public T Get<T>() where T : class
        {
            if (services.TryGetValue(typeof(T), out var service)) return (T)service;
            throw new InvalidOperationException($"Service {typeof(T).FullName} is not registered.");
        }

        public bool TryGet<T>(out T service) where T : class
        {
            if (services.TryGetValue(typeof(T), out var value))
            {
                service = (T)value;
                return true;
            }
            service = null;
            return false;
        }

        public void Clear() => services.Clear();
    }
}
```

`BorderValley.PlayModeTests.asmdef`：

```json
{
  "name": "BorderValley.PlayModeTests",
  "references": ["BorderValley.Core", "BorderValley.Data", "BorderValley.Battle", "BorderValley.Inventory", "BorderValley.Narrative", "BorderValley.World", "BorderValley.UI"],
  "optionalUnityReferences": ["TestAssemblies"],
  "autoReferenced": false
}
```

`AssemblySmokeTests.cs`：

```csharp
using NUnit.Framework;

namespace BorderValley.PlayModeTests
{
    public sealed class AssemblySmokeTests
    {
        [Test]
        public void CoreAssembly_IsLoadable()
        {
            Assert.That(typeof(BorderValley.Core.GameContext).Assembly.GetName().Name,
                Is.EqualTo("BorderValley.Core"));
        }
    }
}
```

- [ ] **Step 4: 运行编辑模式和启动模式测试**

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -runTests -testPlatform EditMode `
  -testResults "$PWD\editmode-results.xml" -logFile "$PWD\editmode-tests.log"

& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -runTests -testPlatform PlayMode `
  -testResults "$PWD\playmode-results.xml" -logFile "$PWD\playmode-tests.log"
```

预期：两套测试全部通过，结果 XML 中 `failed="0"`。

- [ ] **Step 5: 提交**

```powershell
git add BorderValley/Assets
git commit -m "refactor: establish module boundaries and game context"
```

### Task 3: 实现确定性随机源

**Files:**
- Create: `BorderValley/Assets/Code/Core/Random/IRandomSource.cs`
- Create: `BorderValley/Assets/Code/Core/Random/XorShiftRandom.cs`
- Create: `BorderValley/Assets/Code/Core/Random/RandomSourceFactory.cs`
- Create: `BorderValley/Assets/Tests/EditMode/RandomSourceTests.cs`

**Interfaces:**
- Produces: `IRandomSource.NextUInt()`、`IRandomSource.Range(int,int)`、`IRandomSource.Value01()`、`IRandomSource.Fork(string)`、`RandomSourceFactory.FromSeed(string)`。

- [ ] **Step 1: 写失败测试**

```csharp
using BorderValley.Core.Random;
using NUnit.Framework;

namespace BorderValley.Core.Tests
{
    public sealed class RandomSourceTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var first = RandomSourceFactory.FromSeed("loot-floor-1");
            var second = RandomSourceFactory.FromSeed("loot-floor-1");
            for (var i = 0; i < 20; i++)
                Assert.That(first.NextUInt(), Is.EqualTo(second.NextUInt()));
        }

        [Test]
        public void Range_IsHalfOpen()
        {
            var random = RandomSourceFactory.FromSeed("range-test");
            for (var i = 0; i < 1000; i++)
            {
                var value = random.Range(3, 8);
                Assert.That(value, Is.GreaterThanOrEqualTo(3));
                Assert.That(value, Is.LessThan(8));
            }
        }

        [Test]
        public void Fork_DifferentLabels_ProducesDifferentStreams()
        {
            var root = RandomSourceFactory.FromSeed("world-42");
            Assert.That(root.Fork("item").NextUInt(), Is.Not.EqualTo(root.Fork("ai").NextUInt()));
        }
    }
}
```

- [ ] **Step 2: 运行并确认失败**

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -runTests -testPlatform EditMode `
  -testResults "$PWD\editmode-results.xml" -logFile "$PWD\editmode-tests.log"
```

预期：失败并报告 `IRandomSource` 或 `RandomSourceFactory` 不存在。

- [ ] **Step 3: 实现接口和 xorshift32**

`IRandomSource.cs`：

```csharp
namespace BorderValley.Core.Random
{
    public interface IRandomSource
    {
        uint NextUInt();
        int Range(int minInclusive, int maxExclusive);
        float Value01();
        IRandomSource Fork(string label);
    }
}
```

`XorShiftRandom.cs`：

```csharp
using System;

namespace BorderValley.Core.Random
{
    public sealed class XorShiftRandom : IRandomSource
    {
        private uint state;

        public XorShiftRandom(uint seed)
        {
            state = seed == 0 ? 0x6D2B79F5u : seed;
        }

        public uint NextUInt()
        {
            var value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return value;
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            var span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % span);
        }

        public float Value01() => (NextUInt() & 0x00FFFFFFu) / 16777216f;

        public IRandomSource Fork(string label)
        {
            return new XorShiftRandom(RandomSourceFactory.StableHash(label + ":" + state));
        }
    }
}
```

`RandomSourceFactory.cs`：

```csharp
namespace BorderValley.Core.Random
{
    public static class RandomSourceFactory
    {
        public static IRandomSource FromSeed(string seed) => new XorShiftRandom(StableHash(seed));

        public static uint StableHash(string value)
        {
            unchecked
            {
                const uint offset = 2166136261u;
                const uint prime = 16777619u;
                var hash = offset;
                foreach (var character in value)
                {
                    hash ^= character;
                    hash *= prime;
                }
                return hash;
            }
        }
    }
}
```

- [ ] **Step 4: 运行测试并确认通过**

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -runTests -testPlatform EditMode `
  -testResults "$PWD\editmode-results.xml" -logFile "$PWD\editmode-tests.log"
```

预期：`RandomSourceTests` 三个测试通过，`failed="0"`。

- [ ] **Step 5: 提交**

```powershell
git add BorderValley/Assets/Code/Core/Random BorderValley/Assets/Tests/EditMode/RandomSourceTests.cs
git commit -m "feat: add deterministic random source"
```

---

### Task 4: 实现原子存档系统

**Files:**
- Create: `BorderValley/Assets/Code/Core/Persistence/ISaveParticipant.cs`
- Create: `BorderValley/Assets/Code/Core/Persistence/SaveGameData.cs`
- Create: `BorderValley/Assets/Code/Core/Persistence/SaveService.cs`
- Create: `BorderValley/Assets/Tests/EditMode/SaveServiceTests.cs`
- Modify: `BorderValley/Packages/manifest.json`
- Modify: `BorderValley/Assets/Tests/EditMode/BorderValley.EditModeTests.asmdef`

**Interfaces:**
- Consumes: Newtonsoft.Json 3.2.2。
- Produces: `ISaveParticipant.Key`、`Capture()`、`Restore(JObject)`、`RestoreContext(string)`、`Reset()`；`SaveService.Save(int,string)`、`Load(int)`、`Delete(int)`、`HasSave(int)`。
- Slot contract: slot 0 is the automatic save; slots 1 through 3 are manual saves.

- [ ] **Step 1: 写失败测试**

在 `manifest.json` 的 `dependencies` 中加入：

```json
"com.unity.nuget.newtonsoft-json": "3.2.2"
```

并修改 `BorderValley/Assets/Tests/EditMode/BorderValley.EditModeTests.asmdef` 为：

```json
{
  "name": "BorderValley.EditModeTests",
  "references": ["BorderValley.Core", "BorderValley.Data", "BorderValley.Battle", "BorderValley.Inventory", "BorderValley.Narrative", "BorderValley.World", "BorderValley.UI"],
  "optionalUnityReferences": ["TestAssemblies"],
  "overrideReferences": true,
  "precompiledReferences": ["Newtonsoft.Json.dll", "nunit.framework.dll"],
  "includePlatforms": ["Editor"],
  "autoReferenced": false
}
```

创建 `SaveServiceTests.cs`：

```csharp
using System;
using System.IO;
using BorderValley.Core.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BorderValley.Core.Tests
{
    public sealed class SaveServiceTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "BorderValleyTests", Guid.NewGuid().ToString("N"));

        [Test]
        public void SaveThenLoad_RestoresParticipant()
        {
            var participant = new FakeParticipant { Value = 7 };
            var service = new SaveService(root, new[] { participant });
            service.Save(0, "World");
            participant.Reset();
            service.Load(0);
            Assert.That(participant.Value, Is.EqualTo(7));
            Assert.That(participant.CurrentScene, Is.EqualTo("World"));
        }

        [Test]
        public void Load_WhenPrimaryIsCorrupt_UsesBackup()
        {
            var participant = new FakeParticipant { Value = 11 };
            var service = new SaveService(root, new[] { participant });
            service.Save(0, "World");
            participant.Value = 22;
            service.Save(0, "Forest");
            File.WriteAllText(service.GetPrimaryPathForTests(0), "{broken");
            participant.Reset();
            service.Load(0);
            Assert.That(participant.Value, Is.EqualTo(11));
            Assert.That(participant.CurrentScene, Is.EqualTo("World"));
        }

        [Test]
        public void Save_WhenPrimaryIsCorrupt_PreservesExistingBackup()
        {
            var participant = new FakeParticipant { Value = 11 };
            var service = new SaveService(root, new[] { participant });
            service.Save(0, "World");
            participant.Value = 22;
            service.Save(0, "Forest");
            File.WriteAllText(service.GetPrimaryPathForTests(0), "{broken");

            participant.Value = 33;
            service.Save(0, "Cave");

            participant.Reset();
            Assert.That(service.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(33));
            Assert.That(participant.CurrentScene, Is.EqualTo("Cave"));

            File.WriteAllText(service.GetPrimaryPathForTests(0), "{broken");
            participant.Reset();
            Assert.That(service.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(11));
            Assert.That(participant.CurrentScene, Is.EqualTo("World"));
        }

        [Test]
        public void Save_WritesSinglePrimaryFile_WithoutSidecar()
        {
            var participant = new FakeParticipant { Value = 5 };
            var service = new SaveService(root, new[] { participant });
            service.Save(0, "World");

            var primary = service.GetPrimaryPathForTests(0);
            Assert.That(File.Exists(primary), Is.True);
            Assert.That(File.Exists(primary + ".sha256"), Is.False);
            Assert.That(File.Exists(primary + ".tmp"), Is.False);
            Assert.That(Directory.GetFiles(root, "*.sha256"), Is.Empty);
        }

        [Test]
        public void Save_CleansStaleTempFile()
        {
            var participant = new FakeParticipant { Value = 6 };
            var service = new SaveService(root, new[] { participant });
            var primary = service.GetPrimaryPathForTests(0);
            File.WriteAllText(primary + ".tmp", "{stale");

            service.Save(0, "World");

            Assert.That(File.Exists(primary), Is.True);
            Assert.That(File.Exists(primary + ".tmp"), Is.False);

            participant.Reset();
            Assert.That(service.Load(0), Is.True);
            Assert.That(participant.Value, Is.EqualTo(6));
            Assert.That(participant.CurrentScene, Is.EqualTo("World"));
        }

        [Test]
        public void Save_WhenPrimaryIsLocked_ThrowsAndPreservesSave()
        {
            var participant = new FakeParticipant { Value = 11 };
            var service = new SaveService(root, new[] { participant });
            service.Save(1, "World");

            var primary = service.GetPrimaryPathForTests(1);
            using (var lockStream = new FileStream(primary, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                participant.Value = 22;
                Assert.Throws<IOException>(() => service.Save(1, "Forest"));
            }

            Assert.That(File.Exists(primary + ".tmp"), Is.False);
            Assert.That(File.Exists(primary + ".bak"), Is.False);
            Assert.That(service.Load(1), Is.True);
            Assert.That(participant.Value, Is.EqualTo(11));
            Assert.That(participant.CurrentScene, Is.EqualTo("World"));
        }

        public void Dispose()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private sealed class FakeParticipant : ISaveParticipant
        {
            public string Key => "fake";
            public int Value { get; set; }
            public string CurrentScene { get; private set; } = string.Empty;
            public JObject Capture() => new JObject { ["value"] = Value };
            public void Restore(JObject state) => Value = state.Value<int>("value");
            public void RestoreContext(string sceneName) => CurrentScene = sceneName;
            public void Reset() { Value = 0; CurrentScene = string.Empty; }
        }
    }
}
```

- [ ] **Step 2: 运行并确认失败**

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -runTests -testPlatform EditMode `
  -testResults "$PWD\editmode-results.xml" -logFile "$PWD\editmode-tests.log"
```

预期：失败并报告 `SaveService` 或 `ISaveParticipant` 不存在。

- [ ] **Step 3: 实现接口和数据结构**

`ISaveParticipant.cs`：

```csharp
using Newtonsoft.Json.Linq;

namespace BorderValley.Core.Persistence
{
    public interface ISaveParticipant
    {
        string Key { get; }
        JObject Capture();
        void Restore(JObject state);
        void RestoreContext(string sceneName);
        void Reset();
    }
}
```

`SaveGameData.cs`：

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BorderValley.Core.Persistence
{
    [Serializable]
    public sealed class SaveGameData
    {
        public const int CurrentSchemaVersion = 1;
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public string SceneName { get; set; } = string.Empty;
        public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
        public Dictionary<string, JObject> Participants { get; set; } = new();
    }
}
```

- [ ] **Step 4: 实现原子写入、校验和备份恢复**

`SaveService.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BorderValley.Core.Persistence
{
    public sealed class SaveService
    {
        private enum SaveFileState
        {
            Missing,
            Valid,
            Invalid,
            Unreadable
        }

        private readonly string root;
        private readonly IReadOnlyDictionary<string, ISaveParticipant> participants;

        public SaveService(string root, IEnumerable<ISaveParticipant> participants)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.participants = participants.ToDictionary(item => item.Key, StringComparer.Ordinal);
            Directory.CreateDirectory(root);
        }

        public bool HasSave(int slot) => File.Exists(GetPrimaryPath(slot));
        public string GetPrimaryPathForTests(int slot) => GetPrimaryPath(slot);

        public void Save(int slot, string sceneName)
        {
            var primary = GetPrimaryPath(slot);
            var temporary = primary + ".tmp";
            var backup = primary + ".bak";

            TryDeleteFile(temporary);

            try
            {
                var envelopeJson = BuildEnvelopeJson(sceneName);
                File.WriteAllText(temporary, envelopeJson, new UTF8Encoding(false));

                var temporaryState = InspectSaveFile(temporary, out var temporaryReadException);
                if (temporaryState != SaveFileState.Valid)
                {
                    if (temporaryState == SaveFileState.Unreadable && temporaryReadException != null)
                        throw temporaryReadException;
                    throw new IOException("Temporary save failed validation.");
                }
            }
            catch
            {
                TryDeleteFile(temporary);
                throw;
            }

            try
            {
                var primaryState = InspectSaveFile(primary, out var primaryReadException);
                switch (primaryState)
                {
                    case SaveFileState.Missing:
                        File.Move(temporary, primary);
                        break;
                    case SaveFileState.Valid:
                        File.Replace(temporary, primary, backup);
                        break;
                    case SaveFileState.Invalid:
                        File.Delete(primary);
                        File.Move(temporary, primary);
                        break;
                    case SaveFileState.Unreadable:
                        throw primaryReadException ?? new IOException(
                            "Primary save exists but cannot be read; save aborted to protect it.");
                    default:
                        throw new IOException("Unexpected primary save state.");
                }
            }
            catch
            {
                TryDeleteFile(temporary);
                throw;
            }
        }

        public bool Load(int slot)
        {
            var primary = GetPrimaryPath(slot);
            return TryRestoreFile(primary) || TryRestoreFile(primary + ".bak");
        }

        public void Delete(int slot)
        {
            var primary = GetPrimaryPath(slot);
            foreach (var path in new[]
            {
                primary,
                primary + ".bak",
                primary + ".tmp",
                primary + ".sha256",
                primary + ".bak.sha256"
            })
            {
                TryDeleteFile(path);
            }
        }

        private string BuildEnvelopeJson(string sceneName)
        {
            var data = new SaveGameData { SceneName = sceneName };
            foreach (var participant in participants.Values)
                data.Participants[participant.Key] = participant.Capture();

            var payload = JsonConvert.SerializeObject(data, Formatting.Indented);
            var checksum = ComputeChecksum(payload);
            var envelope = new JObject
            {
                ["payload"] = payload,
                ["checksum"] = checksum
            };
            return envelope.ToString(Formatting.Indented);
        }

        private bool TryRestoreFile(string path)
        {
            try
            {
                var data = ReadValidSaveData(path);
                if (data == null) return false;

                foreach (var participant in participants.Values)
                {
                    participant.Reset();
                    if (data.Participants.TryGetValue(participant.Key, out var state))
                        participant.Restore(state);
                    participant.RestoreContext(data.SceneName);
                }
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (JsonException) { return false; }
        }

        private SaveFileState InspectSaveFile(string path, out Exception readException)
        {
            readException = null;
            if (!File.Exists(path)) return SaveFileState.Missing;

            try
            {
                return ReadValidSaveData(path) != null ? SaveFileState.Valid : SaveFileState.Invalid;
            }
            catch (IOException exception)
            {
                readException = exception;
                return SaveFileState.Unreadable;
            }
            catch (UnauthorizedAccessException exception)
            {
                readException = exception;
                return SaveFileState.Unreadable;
            }
            catch (JsonException)
            {
                return SaveFileState.Invalid;
            }
        }

        private SaveGameData ReadValidSaveData(string path)
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var envelope = JObject.Parse(json);
            var payload = envelope.Value<string>("payload");
            var checksum = envelope.Value<string>("checksum");
            if (string.IsNullOrEmpty(payload) || string.IsNullOrEmpty(checksum)) return null;
            if (!string.Equals(ComputeChecksum(payload), checksum, StringComparison.Ordinal)) return null;

            var data = JsonConvert.DeserializeObject<SaveGameData>(payload);
            if (data == null || data.SchemaVersion != SaveGameData.CurrentSchemaVersion) return null;
            return data;
        }

        private string GetPrimaryPath(int slot)
        {
            if (slot < 0 || slot > 3) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(root, $"slot-{slot}.json");
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static string ComputeChecksum(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty);
        }
    }
}
```

- [ ] **Step 5: 运行测试并提交**

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -runTests -testPlatform EditMode `
  -testResults "$PWD\editmode-results.xml" -logFile "$PWD\editmode-tests.log"
git add BorderValley/Packages/manifest.json BorderValley/Assets/Code/Core/Persistence.meta BorderValley/Assets/Code/Core/Persistence BorderValley/Assets/Tests/EditMode/BorderValley.EditModeTests.asmdef BorderValley/Assets/Tests/EditMode/SaveServiceTests.cs BorderValley/Assets/Tests/EditMode/SaveServiceTests.cs.meta
git commit -m "feat: add atomic save service"
```

预期：`SaveServiceTests` 全部通过，`failed="0"`。

### Task 5: 添加静态内容定义与启动校验

**Files:**
- Create: `BorderValley/Assets/Code/Data/ContentDefinition.cs`
- Create: `BorderValley/Assets/Code/Data/ContentCatalog.cs`
- Create: `BorderValley/Assets/Code/Data/ContentValidationIssue.cs`
- Create: `BorderValley/Assets/Code/Data/ContentValidator.cs`
- Create: `BorderValley/Assets/Code/Data/ContentRuntimeValidator.cs`
- Create: `BorderValley/Assets/Tests/EditMode/ContentValidatorTests.cs`

**Interfaces:**
- Produces: `ContentDefinition.Id`、`ContentCatalog.All`、`ContentValidator.Validate(IEnumerable<ContentDefinition>)`、`ContentValidationIssue.Code`、`ContentValidationIssue.Message`。
- `ContentValidator.Validate(null)` 立即抛出 `ArgumentNullException`；序列中的 `null` 定义条目产生 `missing_id` 且 `Context` 为 `null`。

- [ ] **Step 1: 写失败测试**

```csharp
using System.Linq;
using BorderValley.Data;
using NUnit.Framework;
using UnityEngine;

namespace BorderValley.Data.Tests
{
    public sealed class ContentValidatorTests
    {
        [Test]
        public void Validate_DuplicateIds_ReturnsIssue()
        {
            var first = ScriptableObject.CreateInstance<ContentDefinition>();
            var second = ScriptableObject.CreateInstance<ContentDefinition>();
            first.EditorSetId("item.sword");
            second.EditorSetId("item.sword");
            var issues = ContentValidator.Validate(new[] { first, second }).ToList();
            Assert.That(issues.Any(issue => issue.Code == "duplicate_id"), Is.True);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        [Test]
        public void Validate_InvalidId_ReturnsIssue()
        {
            var definition = ScriptableObject.CreateInstance<ContentDefinition>();
            definition.EditorSetId("Item Sword!");
            var issues = ContentValidator.Validate(new[] { definition }).ToList();
            Assert.That(issues.Any(issue => issue.Code == "invalid_id"), Is.True);
            Object.DestroyImmediate(definition);
        }

        [TestCase("")]
        [TestCase("   ")]
        public void Validate_MissingOrWhitespaceId_ReturnsMissingId(string id)
        {
            var definition = ScriptableObject.CreateInstance<ContentDefinition>();
            definition.EditorSetId(id);
            var issues = ContentValidator.Validate(new[] { definition }).ToList();
            Assert.That(issues.Any(issue => issue.Code == "missing_id"), Is.True);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void Validate_NullDefinition_ReturnsMissingIdWithNullContext()
        {
            var issues = ContentValidator.Validate(new ContentDefinition[] { null }).ToList();
            var issue = issues.Single(candidate => candidate.Code == "missing_id");
            Assert.That(issue.Context, Is.Null);
        }

        [Test]
        public void Validate_ValidId_ReturnsNoIssues()
        {
            var definition = ScriptableObject.CreateInstance<ContentDefinition>();
            definition.EditorSetId("item.sword");
            var issues = ContentValidator.Validate(new[] { definition }).ToList();
            Assert.That(issues, Is.Empty);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void Validate_EmptySequence_ReturnsNoIssues()
        {
            var issues = ContentValidator.Validate(new ContentDefinition[0]).ToList();
            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void Validate_NullSequence_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() => ContentValidator.Validate(null));
        }
    }
}
```

- [ ] **Step 2: 运行并确认失败**

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -runTests -testPlatform EditMode `
  -testResults "$PWD\editmode-results.xml" -logFile "$PWD\editmode-tests.log"
```

预期：编译失败并报告 `ContentDefinition` 不存在。

- [ ] **Step 3: 实现内容定义和目录**

`ContentDefinition.cs`：

```csharp
using UnityEngine;

namespace BorderValley.Data
{
    public class ContentDefinition : ScriptableObject
    {
        [SerializeField] private string id = string.Empty;
        public string Id => id;

#if UNITY_EDITOR
        public void EditorSetId(string value) => id = value;
#endif
    }
}
```

`ContentCatalog.cs`：

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace BorderValley.Data
{
    [CreateAssetMenu(menuName = "BorderValley/Content Catalog")]
    public sealed class ContentCatalog : ScriptableObject
    {
        [SerializeField] private List<ContentDefinition> definitions = new();
        public IReadOnlyList<ContentDefinition> All => definitions;
    }
}
```

- [ ] **Step 4: 实现校验器并提交**

`ContentValidationIssue.cs`：

```csharp
using UnityEngine;

namespace BorderValley.Data
{
    public readonly struct ContentValidationIssue
    {
        public ContentValidationIssue(string code, string message, Object context)
        {
            Code = code;
            Message = message;
            Context = context;
        }

        public string Code { get; }
        public string Message { get; }
        public Object Context { get; }
    }
}
```

`ContentValidator.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BorderValley.Data
{
    public static class ContentValidator
    {
        private static readonly Regex ValidId = new("^[a-z0-9]+(?:[._-][a-z0-9]+)*$", RegexOptions.Compiled);

        public static IEnumerable<ContentValidationIssue> Validate(IEnumerable<ContentDefinition> definitions)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));

            return ValidateCore(definitions);
        }

        private static IEnumerable<ContentValidationIssue> ValidateCore(IEnumerable<ContentDefinition> definitions)
        {
            var seen = new HashSet<string>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    yield return new ContentValidationIssue("missing_id", "A content definition is null.", null);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.Id))
                {
                    yield return new ContentValidationIssue("missing_id", "A content definition has no ID.", definition);
                    continue;
                }

                if (!ValidId.IsMatch(definition.Id))
                {
                    yield return new ContentValidationIssue("invalid_id", $"Invalid ID: {definition.Id}", definition);
                    continue;
                }

                if (!seen.Add(definition.Id))
                    yield return new ContentValidationIssue("duplicate_id", $"Duplicate ID: {definition.Id}", definition);
            }
        }
    }
}
```

`ContentRuntimeValidator.cs`：

```csharp
using System.Linq;
using UnityEngine;

namespace BorderValley.Data
{
    [DefaultExecutionOrder(-1000)]
    public sealed class ContentRuntimeValidator : MonoBehaviour
    {
        private void Awake()
        {
            var catalog = Resources.Load<ContentCatalog>("ContentCatalog");
            if (catalog == null)
            {
                Debug.LogError("ContentCatalog was not found in Resources.");
                return;
            }

            var issues = ContentValidator.Validate(catalog.All).ToArray();
            foreach (var issue in issues)
            {
                Debug.LogError($"{issue.Code}: {issue.Message}", issue.Context);
            }

            if (issues.Length > 0 && Debug.isDebugBuild)
            {
                throw new System.InvalidOperationException(
                    $"Content validation failed with {issues.Length} issue(s).");
            }
        }
    }
}
```

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -runTests -testPlatform EditMode `
  -testResults "$PWD\editmode-results.xml" -logFile "$PWD\editmode-tests.log"
git add BorderValley/Assets/Code/Data BorderValley/Assets/Tests/EditMode/ContentValidatorTests.cs
git commit -m "feat: add content catalog validation"
```

预期：`ContentValidatorTests` 全部通过，包括空/空白 ID、null 定义条目、合法 ID、空序列和 null 输入序列的边界用例。

### Task 6: 建立启动流程、主菜单和占位世界

**Files:**
- Create: `BorderValley/Assets/Code/Core/SceneManagement/ISceneLoader.cs`
- Create: `BorderValley/Assets/Code/Core/SceneManagement/UnitySceneLoader.cs`
- Create: `BorderValley/Assets/Code/Core/Boot/GameBootstrapper.cs`
- Create: `BorderValley/Assets/Code/Core/Boot/IPreflightCheck.cs`
- Create: `BorderValley/Assets/Code/Core/Boot/StartupPreflight.cs`
- Create: `BorderValley/Assets/Tests/EditMode/StartupPreflightTests.cs`
- Modify: `BorderValley/Assets/Code/Data/ContentRuntimeValidator.cs`
- Create: `BorderValley/Assets/Resources/ContentCatalog.asset`
- Create: `BorderValley/Assets/Code/UI/MainMenuPresenter.cs`
- Create: `BorderValley/Assets/Code/UI/MainMenuView.cs`
- Create: `BorderValley/Assets/Code/World/WorldPlaceholder.cs`
- Create: `BorderValley/Assets/Editor/Tools/FoundationSceneBuilder.cs`
- Create: `BorderValley/Assets/Tests/PlayMode/FoundationFlowTests.cs`
- Modify: `BorderValley/ProjectSettings/EditorBuildSettings.asset`

**Interfaces:**
- Consumes: `GameContext`、`SaveService`、`ContentRuntimeValidator`。
- Produces: `ISceneLoader.LoadAsync(string)`、`IPreflightCheck.Validate(out string)`、`StartupPreflight.ShouldContinue(...)`、`GameBootstrapper.Context`、`GameBootstrapper.StartupBlocked`，场景 `Boot`、`MainMenu`、`World`。

- [ ] **Step 1: 写失败测试**

`FoundationFlowTests.cs`：

```csharp
using System.Collections;
using BorderValley.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BorderValley.PlayModeTests
{
    public sealed class FoundationFlowTests
    {
        [UnityTest]
        public IEnumerator Boot_EntersMainMenu()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            Assert.That(GameBootstrapper.Context, Is.Not.Null);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0));
            Assert.That(Application.targetFrameRate, Is.EqualTo(60));
        }
    }
}
```

`StartupPreflightTests.cs`：

```csharp
using BorderValley.Core.Boot;
using NUnit.Framework;

namespace BorderValley.Core.Tests
{
    public sealed class StartupPreflightTests
    {
        [Test]
        public void ShouldContinue_FailedCheckInDebug_Blocks()
        {
            var checks = new IPreflightCheck[] { new FakeCheck(false, "bad content") };
            Assert.That(StartupPreflight.ShouldContinue(checks, true, out var errors), Is.False);
            Assert.That(errors, Does.Contain("bad content"));
        }

        [Test]
        public void ShouldContinue_FailedCheckInProduction_ContinuesAndReports()
        {
            var checks = new IPreflightCheck[] { new FakeCheck(false, "bad content") };
            Assert.That(StartupPreflight.ShouldContinue(checks, false, out var errors), Is.True);
            Assert.That(errors, Does.Contain("bad content"));
        }

        [Test]
        public void ShouldContinue_AllChecksPass_Continues()
        {
            var checks = new IPreflightCheck[] { new FakeCheck(true, string.Empty) };
            Assert.That(StartupPreflight.ShouldContinue(checks, true, out var errors), Is.True);
            Assert.That(errors, Is.Empty);
        }

        private sealed class FakeCheck : IPreflightCheck
        {
            private readonly bool result;
            private readonly string error;
            public FakeCheck(bool result, string error) { this.result = result; this.error = error; }
            public bool Validate(out string error) { error = this.error; return result; }
        }
    }
}
```
- [ ] **Step 2: 运行并确认失败**

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -runTests -testPlatform PlayMode `
  -testResults "$PWD\playmode-results.xml" -logFile "$PWD\playmode-tests.log"
```

预期：失败并报告场景 `Boot` 不存在。

- [ ] **Step 3: 实现场景加载和启动流程**

`ISceneLoader.cs`：

```csharp
using System.Threading.Tasks;

namespace BorderValley.Core.SceneManagement
{
    public interface ISceneLoader
    {
        Task LoadAsync(string sceneName);
        string ActiveSceneName { get; }
    }
}
```

`UnitySceneLoader.cs`：

```csharp
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace BorderValley.Core.SceneManagement
{
    public sealed class UnitySceneLoader : ISceneLoader
    {
        public string ActiveSceneName => SceneManager.GetActiveScene().name;

        public Task LoadAsync(string sceneName)
        {
            var completion = new TaskCompletionSource<bool>();
            var operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
                throw new System.InvalidOperationException($"Scene is not in Build Settings: {sceneName}");
            operation.completed += _ => completion.SetResult(true);
            return completion.Task;
        }
    }
}
```

`IPreflightCheck.cs`：

```csharp
namespace BorderValley.Core.Boot
{
    public interface IPreflightCheck
    {
        bool Validate(out string error);
    }
}
```

`StartupPreflight.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Core.Boot
{
    public static class StartupPreflight
    {
        public static bool ShouldContinue(
            IEnumerable<IPreflightCheck> checks,
            bool blockOnFailure,
            out string errors)
        {
            var messages = new List<string>();
            foreach (var check in checks ?? Array.Empty<IPreflightCheck>())
            {
                try
                {
                    if (!check.Validate(out var error))
                    {
                        messages.Add(string.IsNullOrWhiteSpace(error)
                            ? check.GetType().FullName
                            : error);
                    }
                }
                catch (Exception exception)
                {
                    messages.Add($"{check.GetType().FullName}: {exception.Message}");
                }
            }

            errors = string.Join(Environment.NewLine, messages.Distinct());
            return !blockOnFailure || messages.Count == 0;
        }
    }
}
```

`GameBootstrapper.cs`：

```csharp
using System.Collections.Generic;
using BorderValley.Core.Boot;
using BorderValley.Core.Persistence;
using BorderValley.Core.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorderValley.Core
{
    public sealed class GameBootstrapper : MonoBehaviour
    {
        public static GameContext Context { get; private set; }
        public static bool StartupBlocked { get; private set; }

        [SerializeField] private MonoBehaviour[] preflightChecks = System.Array.Empty<MonoBehaviour>();

        private void Awake()
        {
            StartupBlocked = false;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            DontDestroyOnLoad(gameObject);
            Context = new GameContext();
            Context.Register<ISceneLoader>(new UnitySceneLoader());
            Context.Register(new SaveService(Application.persistentDataPath, System.Array.Empty<ISaveParticipant>()));
        }

        private void Start()
        {
            var checks = new List<IPreflightCheck>();
            foreach (var candidate in preflightChecks)
            {
                if (candidate is IPreflightCheck check)
                {
                    checks.Add(check);
                }
            }

            if (!StartupPreflight.ShouldContinue(checks, Debug.isDebugBuild, out var errors))
            {
                StartupBlocked = true;
                Debug.LogError($"Startup blocked by preflight validation:\n{errors}");
                return;
            }

            if (!string.IsNullOrWhiteSpace(errors))
            {
                Debug.LogError($"Startup validation warnings:\n{errors}");
            }

            SceneManager.LoadScene("MainMenu");
        }
    }
}
```
- [ ] **Step 4: 创建主菜单、占位世界和场景构建器**

`ContentRuntimeValidator.cs` 更新为可被 Boot 门控调用的实现：

```csharp
using System.Linq;
using BorderValley.Core.Boot;
using UnityEngine;

namespace BorderValley.Data
{
    public sealed class ContentRuntimeValidator : MonoBehaviour, IPreflightCheck
    {
        public bool Validate(out string error)
        {
            var catalog = Resources.Load<ContentCatalog>("ContentCatalog");
            if (catalog == null)
            {
                error = "ContentCatalog was not found in Resources.";
                Debug.LogError(error);
                return false;
            }

            var issues = ContentValidator.Validate(catalog.All).ToArray();
            foreach (var issue in issues)
            {
                Debug.LogError($"{issue.Code}: {issue.Message}", issue.Context);
            }

            error = string.Join(System.Environment.NewLine,
                issues.Select(issue => $"{issue.Code}: {issue.Message}"));
            return issues.Length == 0;
        }
    }
}
```

`MainMenuPresenter.cs`：

```csharp
using BorderValley.Core.SceneManagement;

namespace BorderValley.UI
{
    public sealed class MainMenuPresenter
    {
        private readonly ISceneLoader sceneLoader;
        public MainMenuPresenter(ISceneLoader sceneLoader) => this.sceneLoader = sceneLoader;
        public void StartNewGame() => _ = sceneLoader.LoadAsync("World");
    }
}
```

`MainMenuView.cs`：

```csharp
using BorderValley.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BorderValley.UI
{
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] private Button newGameButton;
        private MainMenuPresenter presenter;

        private void Start()
        {
            var loader = GameBootstrapper.Context.Get<Core.SceneManagement.ISceneLoader>();
            presenter = new MainMenuPresenter(loader);
            newGameButton.onClick.AddListener(presenter.StartNewGame);
        }

        private void OnDestroy() => newGameButton.onClick.RemoveAllListeners();
    }
}
```

`WorldPlaceholder.cs`：

```csharp
using UnityEngine;

namespace BorderValley.World
{
    public sealed class WorldPlaceholder : MonoBehaviour
    {
        private void Start() => Debug.Log("BorderValley foundation world loaded.");
    }
}
```

`FoundationSceneBuilder.cs`：

```csharp
using BorderValley.Core;
using BorderValley.Data;
using BorderValley.UI;
using BorderValley.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BorderValley.Editor
{
    public static class FoundationSceneBuilder
    {
        [MenuItem("Tools/BorderValley/Rebuild Foundation Scenes")]
        public static void Rebuild()
        {
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            System.IO.Directory.CreateDirectory("Assets/Resources");

            const string catalogPath = "Assets/Resources/ContentCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<ContentCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ContentCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }

            var boot = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var services = new GameObject("BootServices");
            var validator = services.AddComponent<ContentRuntimeValidator>();
            var bootstrapper = services.AddComponent<GameBootstrapper>();
            var bootSerialized = new SerializedObject(bootstrapper);
            var preflightProperty = bootSerialized.FindProperty("preflightChecks");
            preflightProperty.arraySize = 1;
            preflightProperty.GetArrayElementAtIndex(0).objectReferenceValue = validator;
            bootSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(boot, "Assets/Scenes/Boot.unity");

            var menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvas = CreateCanvas(menu);
            var button = CreateButton(canvas.transform);
            var eventSystem = CreateEventSystem(menu);
            var component = canvas.AddComponent<MainMenuView>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("newGameButton").objectReferenceValue = button;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(menu, "Assets/Scenes/MainMenu.unity");

            var world = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateMainCamera(world);
            new GameObject("WorldPlaceholder").AddComponent<WorldPlaceholder>();
            EditorSceneManager.SaveScene(world, "Assets/Scenes/World.unity");

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Boot.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/World.unity", true)
            };
            AssetDatabase.SaveAssets();
        }

        private static GameObject CreateMainCamera(Scene scene)
        {
            var cameraGameObject = new GameObject("Main Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraGameObject, scene);
            cameraGameObject.tag = "MainCamera";

            var camera = cameraGameObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 1f);
            return cameraGameObject;
        }

        private static GameObject CreateCanvas(Scene scene)
        {
            var canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvas, scene);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static GameObject CreateEventSystem(Scene scene)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
            return eventSystem;
        }

        private static Button CreateButton(Transform parent)
        {
            var root = new GameObject("New Game", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 80f);
            root.GetComponent<Image>().color = new Color(0.2f, 0.35f, 0.55f, 1f);
            return root.GetComponent<Button>();
        }
    }
}
```

生成场景：

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -executeMethod BorderValley.Editor.FoundationSceneBuilder.Rebuild `
  -quit -logFile "$PWD\scene-builder.log"
```

- [ ] **Step 5: 运行测试并提交**

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -runTests -testPlatform EditMode `
  -testResults "$PWD\editmode-results.xml" -logFile "$PWD\editmode-tests.log"

& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -runTests -testPlatform PlayMode `
  -testResults "$PWD\playmode-results.xml" -logFile "$PWD\playmode-tests.log"
git add BorderValley/Assets BorderValley/ProjectSettings/EditorBuildSettings.asset
git commit -m "feat: add boot flow and foundation scenes"
```

预期：`StartupPreflightTests` 与 `FoundationFlowTests` 通过；Boot 场景包含 ContentRuntimeValidator；Build Settings 包含 Boot、MainMenu、World；无效内容在开发构建中不会进入 MainMenu。

### Task 7: 建立 Android 构建与性能基线

**Files:**
- Create: `BorderValley/Assets/Editor/Build/BuildAndroid.cs`
- Create: `scripts/run-tests.ps1`
- Create: `scripts/build-android.ps1`
- Create: `docs/development/android-smoke-test.md`
- Modify: `BorderValley/ProjectSettings/ProjectSettings.asset`

**Interfaces:**
- Consumes: 场景 `Assets/Scenes/Boot.unity`、`MainMenu.unity`、`World.unity`。
- Produces: `BorderValley.Editor.Build.BuildAndroid.PerformBuild()`，APK `Builds/Android/BorderValley.apk`。

- [ ] **Step 1: 写构建脚本并确认缺少入口失败**

`scripts/build-android.ps1`：

```powershell
$ErrorActionPreference = 'Stop'
$unity = 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe'
$project = Join-Path $PSScriptRoot '..\BorderValley'
$output = Join-Path $PSScriptRoot '..\Builds\Android\BorderValley.apk'
New-Item -ItemType Directory -Force -Path (Split-Path $output) | Out-Null

& $unity -batchmode -quit -nographics `
    -projectPath $project `
    -executeMethod BorderValley.Editor.Build.BuildAndroid.PerformBuild `
    -logFile (Join-Path $PSScriptRoot '..\android-build.log')
if ($LASTEXITCODE -ne 0) { throw "Android build failed: $LASTEXITCODE" }
if (-not (Test-Path -LiteralPath $output)) { throw "APK not found: $output" }
Write-Output "Built $output"
```

运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-android.ps1
```

预期：失败并报告 `BuildAndroid` 类不存在。

- [ ] **Step 2: 实现 Android 构建入口和测试脚本**

`BuildAndroid.cs`：

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BorderValley.Editor.Build
{
    public static class BuildAndroid
    {
        private const string OutputPath = "../../Builds/Android/BorderValley.apk";

        [MenuItem("Tools/BorderValley/Build Android")]
        public static void PerformBuild()
        {
            PlayerSettings.companyName = "BorderValley";
            PlayerSettings.productName = "Border Valley";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.bordervalley.game");
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Medium);

            var output = Path.GetFullPath(Path.Combine(Application.dataPath, OutputPath));
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "Builds/Android");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[]
                {
                    "Assets/Scenes/Boot.unity",
                    "Assets/Scenes/MainMenu.unity",
                    "Assets/Scenes/World.unity"
                },
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.StrictMode
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Android build failed: {report.summary.result}");
        }
    }
}
```

`scripts/run-tests.ps1`：

```powershell
$ErrorActionPreference = 'Stop'
$unity = 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe'
$project = Join-Path $PSScriptRoot '..\BorderValley'

foreach ($platform in @('EditMode', 'PlayMode')) {
    $results = Join-Path $PSScriptRoot "..\$($platform.ToLower())-results.xml"
    & $unity -batchmode -nographics -projectPath $project `
        -runTests -testPlatform $platform `
        -testResults $results `
        -logFile (Join-Path $PSScriptRoot "..\$($platform.ToLower())-tests.log")
    if ($LASTEXITCODE -ne 0) { throw "$platform tests failed: $LASTEXITCODE" }
}
Write-Output 'All Unity tests passed.'
```

- [ ] **Step 3: 创建移动端冒烟记录**

`docs/development/android-smoke-test.md`：

```markdown
# Android Smoke Test

## Frame pacing policy

The game targets a fixed 60 FPS. `GameBootstrapper.Awake` runs once before scene flow and sets `QualitySettings.vSyncCount = 0` and `Application.targetFrameRate = 60`, so 90/120 Hz devices must not run above 60 FPS. Every smoke-test build must be checked against this policy.

1. Install `Builds/Android/BorderValley.apk` on an Android 8.0 or newer device.
2. Launch the game and confirm it enters landscape mode.
3. Confirm Boot transitions to Main Menu within 5 seconds.
4. Tap New Game and confirm WorldPlaceholder logs the foundation message.
5. Background the app for 30 seconds and resume it without a crash.
6. Run for 10 minutes and record average FPS, peak memory, and visible corruption. Average FPS must stay at or near 60 and must not exceed 60.
7. On a 120 Hz device, repeat steps 2-6 and confirm the frame rate stays at the 60 FPS cap instead of reaching 90/120 FPS.
```

- [ ] **Step 4: 运行全部测试并构建 APK**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-android.ps1
```

预期：

- 编辑模式测试全部通过。
- 启动模式测试全部通过。
- 输出 `Built D:\program\phone game\Builds\Android\BorderValley.apk`。
- APK 存在且大小大于 1 MB。
- 120 Hz 设备上确认帧率保持在 60 FPS 上限。

- [ ] **Step 5: 提交并完成安卓设备冒烟测试**

```powershell
git add BorderValley/Assets/Editor/Build BorderValley/ProjectSettings scripts docs/development
git commit -m "build: add Android build and smoke test pipeline"
```

在真实设备上按 `docs/development/android-smoke-test.md` 执行。若任一项失败，任务保持未完成并修复后重新验证。

---

## 完成标准

- `scripts/verify-unity.ps1` 输出工具链已就绪。
- `scripts/run-tests.ps1` 全部通过。
- `scripts/build-android.ps1` 生成 `Builds/Android/BorderValley.apk`。
- Unity Console 无编译错误。
- 主菜单可以进入占位世界。
- 存档测试覆盖正常保存、正常读取、主文件损坏和备份恢复。
- 内容校验测试覆盖缺失 ID、非法 ID 和重复 ID。
- 每个任务分别提交，工作树干净。

## 后续计划顺序

1. 战棋核心与战斗场景。
2. 装备、掉落、背包、打造、经济和角色成长。
3. 世界探索、NPC、对话、任务和战斗奖励整合。
4. 关卡内容、占位美术替换、性能优化和完整垂直切片验收。

以上计划各自生成独立文档和测试周期，并在开始前读取本计划产出的接口。
