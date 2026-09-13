# 战棋规则核心 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 Unity 工程中实现可测试、确定性、与表现层解耦的战棋规则核心，包括网格、移动、回合、伤害、状态、技能、胜负和敌方 AI。

**Architecture:** 所有规则放在 `BorderValley.Battle` 程序集，不依赖场景、UI 或动画。战斗通过命令修改 `BattleState`，随机行为通过现有 `IRandomSource` 注入。后续战斗场景只负责把命令转换为输入和把状态渲染为对象，不复制规则。

**Tech Stack:** Unity 6000.6.0f1、C#、Unity Test Framework 1.8.0、现有 `BorderValley.Core`、现有 `IRandomSource` 和 `ContentDefinition`。

**Spec:** `docs/superpowers/specs/2026-09-12-fantasy-tactics-rpg-vertical-slice-design.md`

## Global Constraints

- Android 横屏、API 26、IL2CPP、ARM64、60 FPS、vSync 0。
- 战棋规则不直接读取 UnityEngine.Time、Input 或场景对象。
- 所有随机判定必须通过 `IRandomSource` 注入。
- 同一局面和随机种子必须得到相同结果。
- 首版每回合为“移动一次 + 主要行动一次”。
- 地形包括平地、障碍、灌木、高地、泥地。
- 状态包括燃烧、中毒、眩晕、减速、护盾、嘲讽。
- 不实现联网、账号、云存档、内购、广告、iOS、手柄或 PC 发布。
- 玩家可见文字只保存本地化键，不在规则代码中硬编码最终文案。
- 核心规则测试运行在 EditMode，战斗场景和 UI 由后续独立计划实现。

## 本计划边界

完成后的产物：

- `BorderValley.Battle` 程序集包含完整的纯规则战棋模型。
- 可以创建一张战场、3 对 3 单位并完整模拟战斗直到胜利或失败。
- 移动、地形、伤害、状态、技能、行动顺序和 AI 都有自动化测试。
- 同一种子和同一组命令产生相同战斗结果。
- 本计划不创建战斗场景、不制作角色动画、不接背包和掉落，也不接世界场景。

这些表现与整合工作放到下一份 `battle-scene` 计划，避免把规则引擎和 Unity 场景耦合。

## 文件结构

```text
BorderValley/Assets/Code/Battle/
  Domain/
    GridPosition.cs
    TerrainType.cs
    BattleMap.cs
    GridPathfinder.cs
    Team.cs
    UnitStats.cs
    BattleUnit.cs
    BattleState.cs
    TurnOrder.cs
    BattleTurnEngine.cs
    DamageType.cs
    DamageRequest.cs
    DamageResult.cs
    DamageCalculator.cs
    StatusType.cs
    StatusInstance.cs
    StatusSystem.cs
    SkillTargeting.cs
    SkillEffectDefinition.cs
    SkillDefinition.cs
    SkillTargetValidator.cs
    SkillExecutor.cs
    BattleCommand.cs
    BattleActionResult.cs
    BattleOutcome.cs
    BattleEngine.cs
    BattleAi.cs
    BattleScenario.cs
    BattleScenarioFactory.cs

BorderValley/Assets/Tests/EditMode/Battle/
  GridPathfinderTests.cs
  BattleUnitTests.cs
  BattleTurnEngineTests.cs
  DamageCalculatorTests.cs
  StatusSystemTests.cs
  SkillExecutorTests.cs
  BattleEngineTests.cs
  BattleAiTests.cs
  BattleScenarioTests.cs
```

每个文件只承担一个清晰职责。`BattleEngine` 负责命令校验和状态推进，`SkillExecutor` 负责单个技能结算，`BattleAi` 只选择和返回命令。

---

### Task 1: 网格、地形和寻路

**Files:**
- Create: `BorderValley/Assets/Code/Battle/Domain/GridPosition.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/TerrainType.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/BattleMap.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/GridPathfinder.cs`
- Test: `BorderValley/Assets/Tests/EditMode/Battle/GridPathfinderTests.cs`

**Interfaces:**
- Consumes: 无。
- Produces: `GridPosition(int X,int Y)`、`TerrainType`、`BattleMap(int,int,TerrainType[])`、`GridPathfinder.FindReachable(...)`。

- [ ] **Step 1: 写失败测试**

```csharp
using System.Collections.Generic;
using BorderValley.Battle.Domain;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class GridPathfinderTests
    {
        [Test]
        public void FindReachable_StopsAtObstacles()
        {
            var map = new BattleMap(3, 3, new[]
            {
                TerrainType.Plain, TerrainType.Plain, TerrainType.Plain,
                TerrainType.Plain, TerrainType.Obstacle, TerrainType.Plain,
                TerrainType.Plain, TerrainType.Plain, TerrainType.Plain
            });

            var reachable = GridPathfinder.FindReachable(
                map, new GridPosition(0, 0), 2, new HashSet<GridPosition>());

            Assert.That(reachable.ContainsKey(new GridPosition(2, 0)), Is.False);
            Assert.That(reachable[new GridPosition(0, 2)], Is.EqualTo(2));
        }

        [Test]
        public void FindReachable_MudCostsTwo()
        {
            var map = new BattleMap(3, 1, new[]
            {
                TerrainType.Plain, TerrainType.Mud, TerrainType.Plain
            });

            var reachable = GridPathfinder.FindReachable(
                map, new GridPosition(0, 0), 5, new HashSet<GridPosition>());

            Assert.That(reachable[new GridPosition(1, 0)], Is.EqualTo(2));
            Assert.That(reachable[new GridPosition(2, 0)], Is.EqualTo(3));
        }

        [Test]
        public void FindReachable_ExcludesOccupiedCells()
        {
            var map = BattleMap.CreatePlain(3, 1);
            var occupied = new HashSet<GridPosition> { new GridPosition(1, 0) };

            var reachable = GridPathfinder.FindReachable(
                map, new GridPosition(0, 0), 2, occupied);

            Assert.That(reachable.ContainsKey(new GridPosition(1, 0)), Is.False);
            Assert.That(reachable.ContainsKey(new GridPosition(2, 0)), Is.False);
        }
    }
}
```

- [ ] **Step 2: 运行测试并确认失败**

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -runTests -testPlatform EditMode `
  -testResults "$PWD\editmode-battle-results.xml" -logFile "$PWD\editmode-battle.log"
```

预期：编译失败，提示 `BattleMap`、`GridPosition` 或 `GridPathfinder` 不存在。

- [ ] **Step 3: 实现最小规则**

`GridPosition.cs`：

```csharp
using System;

namespace BorderValley.Battle.Domain
{
    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }
        public bool Equals(GridPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPosition other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X},{Y})";
        public static bool operator ==(GridPosition left, GridPosition right) => left.Equals(right);
        public static bool operator !=(GridPosition left, GridPosition right) => !left.Equals(right);
    }
}
```

`TerrainType.cs`：

```csharp
namespace BorderValley.Battle.Domain
{
    public enum TerrainType
    {
        Plain,
        Obstacle,
        Bush,
        HighGround,
        Mud
    }
}
```

`BattleMap.cs`：

```csharp
using System;

namespace BorderValley.Battle.Domain
{
    public sealed class BattleMap
    {
        private readonly TerrainType[] cells;

        public BattleMap(int width, int height, TerrainType[] cells)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (cells == null || cells.Length != width * height)
                throw new ArgumentException("Cell count does not match map dimensions.", nameof(cells));
            Width = width;
            Height = height;
            this.cells = (TerrainType[])cells.Clone();
        }

        public int Width { get; }
        public int Height { get; }

        public static BattleMap CreatePlain(int width, int height)
        {
            var cells = new TerrainType[width * height];
            Array.Fill(cells, TerrainType.Plain);
            return new BattleMap(width, height, cells);
        }

        public bool InBounds(GridPosition position) =>
            position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height;

        public TerrainType GetTerrain(GridPosition position)
        {
            if (!InBounds(position)) throw new ArgumentOutOfRangeException(nameof(position));
            return cells[position.Y * Width + position.X];
        }

        public int GetMovementCost(GridPosition position)
        {
            return GetTerrain(position) switch
            {
                TerrainType.Obstacle => int.MaxValue,
                TerrainType.Mud => 2,
                _ => 1
            };
        }
    }
}
```

`GridPathfinder.cs`：

```csharp
using System.Collections.Generic;

namespace BorderValley.Battle.Domain
{
    public static class GridPathfinder
    {
        public static IReadOnlyDictionary<GridPosition, int> FindReachable(
            BattleMap map,
            GridPosition start,
            int movement,
            ISet<GridPosition> occupied)
        {
            var costs = new Dictionary<GridPosition, int> { [start] = 0 };
            var queue = new Queue<GridPosition>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentCost = costs[current];
                foreach (var next in Neighbors(current))
                {
                    if (!map.InBounds(next) || map.GetTerrain(next) == TerrainType.Obstacle)
                        continue;

                    var nextCost = currentCost + map.GetMovementCost(next);
                    if (nextCost > movement || occupied.Contains(next))
                        continue;

                    if (costs.TryGetValue(next, out var known) && known <= nextCost)
                        continue;

                    costs[next] = nextCost;
                    queue.Enqueue(next);
                }
            }

            return costs;
        }

        private static IEnumerable<GridPosition> Neighbors(GridPosition position)
        {
            yield return new GridPosition(position.X + 1, position.Y);
            yield return new GridPosition(position.X - 1, position.Y);
            yield return new GridPosition(position.X, position.Y + 1);
            yield return new GridPosition(position.X, position.Y - 1);
        }
    }
}
```

- [ ] **Step 4: 运行测试并确认通过**

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -runTests -testPlatform EditMode `
  -testResults "$PWD\editmode-battle-results.xml" -logFile "$PWD\editmode-battle.log"
```

预期：`GridPathfinderTests` 全部通过。

- [ ] **Step 5: 提交**

```powershell
git add BorderValley/Assets/Code/Battle/Domain BorderValley/Assets/Tests/EditMode/Battle
git commit -m "feat(battle): add grid map and pathfinding"
```

---

### Task 2: 单位、属性与战斗状态

**Files:**
- Create: `BorderValley/Assets/Code/Battle/Domain/Team.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/UnitStats.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/BattleUnit.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/BattleState.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/StatusType.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/StatusInstance.cs`
- Test: `BorderValley/Assets/Tests/EditMode/Battle/BattleUnitTests.cs`

**Interfaces:**
- Consumes: `GridPosition`、`BattleMap`。
- Produces: `UnitStats`、`BattleUnit`、`BattleState.AddUnit`、`BattleState.GetUnit`、`BattleState.OccupiedPositions`。

- [ ] **Step 1: 写失败测试**

```csharp
using BorderValley.Battle.Domain;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class BattleUnitTests
    {
        [Test]
        public void NewUnit_StartsAtFullResources()
        {
            var stats = new UnitStats(20, 10, 6, 3, 5, 0.1f, 2);
            var unit = new BattleUnit("p1", "class.warrior", Team.Player, stats, new GridPosition(1, 1));

            Assert.That(unit.Health, Is.EqualTo(20));
            Assert.That(unit.Mana, Is.EqualTo(10));
            Assert.That(unit.IsAlive, Is.True);
            Assert.That(unit.HasMoved, Is.False);
            Assert.That(unit.HasActed, Is.False);
        }

        [Test]
        public void ApplyRawDamage_DoesNotGoBelowZero()
        {
            var unit = new BattleUnit("p1", "class.warrior",
                Team.Player, new UnitStats(5, 0, 2, 0, 1, 0f, 0), new GridPosition(0, 0));

            unit.ApplyRawDamage(99);

            Assert.That(unit.Health, Is.Zero);
            Assert.That(unit.IsAlive, Is.False);
        }

        [Test]
        public void BattleState_OccupiedPositions_ContainsOnlyLivingUnits()
        {
            var state = new BattleState(BattleMap.CreatePlain(2, 1));
            var living = new BattleUnit("p1", "class.warrior", Team.Player,
                new UnitStats(5, 0, 2, 0, 1, 0f, 0), new GridPosition(0, 0));
            var dead = new BattleUnit("e1", "enemy.slime", Team.Enemy,
                new UnitStats(1, 0, 1, 0, 1, 0f, 0), new GridPosition(1, 0));
            state.AddUnit(living);
            state.AddUnit(dead);
            dead.ApplyRawDamage(1);

            Assert.That(state.OccupiedPositions.Count, Is.EqualTo(1));
            Assert.That(state.OccupiedPositions.Contains(new GridPosition(0, 0)), Is.True);
        }
    }
}
```

- [ ] **Step 2: 运行测试并确认失败**

预期：编译失败，提示 `UnitStats`、`BattleUnit` 或 `BattleState` 不存在。

- [ ] **Step 3: 实现单位与状态容器**

`Team.cs`：

```csharp
namespace BorderValley.Battle.Domain
{
    public enum Team
    {
        Player,
        Enemy
    }
}
```

`UnitStats.cs`：

```csharp
using System;

namespace BorderValley.Battle.Domain
{
    public readonly struct UnitStats
    {
        public UnitStats(
            int maxHealth,
            int maxMana,
            int power,
            int armor,
            int speed,
            float critChance,
            int resistance)
        {
            MaxHealth = Math.Max(1, maxHealth);
            MaxMana = Math.Max(0, maxMana);
            Power = Math.Max(0, power);
            Armor = Math.Max(0, armor);
            Speed = Math.Max(0, speed);
            CritChance = Math.Clamp(critChance, 0f, 1f);
            Resistance = Math.Max(0, resistance);
        }

        public int MaxHealth { get; }
        public int MaxMana { get; }
        public int Power { get; }
        public int Armor { get; }
        public int Speed { get; }
        public float CritChance { get; }
        public int Resistance { get; }
    }
}
```

`StatusType.cs`：

```csharp
namespace BorderValley.Battle.Domain
{
    public enum StatusType
    {
        Burning,
        Poisoned,
        Stunned,
        Slowed,
        Shielded,
        Taunted
    }
}
```

`StatusInstance.cs`：

```csharp
namespace BorderValley.Battle.Domain
{
    public sealed class StatusInstance
    {
        public StatusInstance(StatusType type, int magnitude, int remainingTurns, string sourceUnitId)
        {
            Type = type;
            Magnitude = magnitude;
            RemainingTurns = remainingTurns;
            SourceUnitId = sourceUnitId;
        }

        public StatusType Type { get; }
        public int Magnitude { get; internal set; }
        public int RemainingTurns { get; internal set; }
        public string SourceUnitId { get; }
    }
}
```

`BattleUnit.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace BorderValley.Battle.Domain
{
    public sealed class BattleUnit
    {
        private readonly List<StatusInstance> statuses = new();

        public BattleUnit(
            string id,
            string definitionId,
            Team team,
            UnitStats stats,
            GridPosition position)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException(nameof(id)) : id;
            DefinitionId = string.IsNullOrWhiteSpace(definitionId) ? throw new ArgumentException(nameof(definitionId)) : definitionId;
            Team = team;
            Stats = stats;
            Position = position;
            Health = stats.MaxHealth;
            Mana = stats.MaxMana;
        }

        public string Id { get; }
        public string DefinitionId { get; }
        public Team Team { get; }
        public UnitStats Stats { get; }
        public GridPosition Position { get; private set; }
        public int Health { get; private set; }
        public int Mana { get; private set; }
        public bool IsAlive => Health > 0;
        public bool HasMoved { get; private set; }
        public bool HasActed { get; private set; }
        public IReadOnlyList<StatusInstance> Statuses => statuses;

        public void MoveTo(GridPosition position)
        {
            if (!IsAlive) throw new InvalidOperationException("Dead units cannot move.");
            Position = position;
            HasMoved = true;
        }

        public void MarkActionUsed()
        {
            if (!IsAlive) throw new InvalidOperationException("Dead units cannot act.");
            HasActed = true;
        }

        public void RefreshForTurn()
        {
            HasMoved = false;
            HasActed = false;
        }

        public void ApplyRawDamage(int amount)
        {
            Health = Math.Max(0, Health - Math.Max(0, amount));
        }

        public void Heal(int amount)
        {
            if (!IsAlive) return;
            Health = Math.Min(Stats.MaxHealth, Health + Math.Max(0, amount));
        }

        public bool TrySpendMana(int amount)
        {
            if (amount < 0 || Mana < amount) return false;
            Mana -= amount;
            return true;
        }

        public void RestoreMana(int amount)
        {
            Mana = Math.Min(Stats.MaxMana, Mana + Math.Max(0, amount));
        }

        public List<StatusInstance> MutableStatuses => statuses;
    }
}
```

`BattleState.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Battle.Domain
{
    public sealed class BattleState
    {
        private readonly List<BattleUnit> units = new();
        private readonly Dictionary<string, BattleUnit> byId = new(StringComparer.Ordinal);

        public BattleState(BattleMap map)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
        }

        public BattleMap Map { get; }
        public int Round { get; internal set; } = 1;
        public IReadOnlyList<BattleUnit> Units => units;
        public IEnumerable<BattleUnit> LivingUnits => units.Where(unit => unit.IsAlive);
        public ISet<GridPosition> OccupiedPositions =>
            LivingUnits.Select(unit => unit.Position).ToHashSet();

        public void AddUnit(BattleUnit unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (!Map.InBounds(unit.Position)) throw new ArgumentOutOfRangeException(nameof(unit.Position));
            if (byId.ContainsKey(unit.Id)) throw new ArgumentException($"Duplicate unit ID: {unit.Id}");
            units.Add(unit);
            byId.Add(unit.Id, unit);
        }

        public BattleUnit GetUnit(string id)
        {
            if (byId.TryGetValue(id, out var unit)) return unit;
            throw new KeyNotFoundException($"Unknown unit: {id}");
        }

        public bool TryGetUnit(string id, out BattleUnit unit) => byId.TryGetValue(id, out unit);
        public IEnumerable<BattleUnit> UnitsOf(Team team) => LivingUnits.Where(unit => unit.Team == team);
    }
}
```

本任务先建立状态数据容器；Task 4 负责状态的施加、叠加、回合结算和护盾吸收。

- [ ] **Step 4: 运行测试并确认通过**

预期：`BattleUnitTests` 全部通过。

- [ ] **Step 5: 提交**

```powershell
git add BorderValley/Assets/Code/Battle/Domain BorderValley/Assets/Tests/EditMode/Battle/BattleUnitTests.cs
git commit -m "feat(battle): add unit and battle state model"
```

---

### Task 3: 行动顺序与回合引擎

**Files:**
- Create: `BorderValley/Assets/Code/Battle/Domain/TurnOrder.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/BattleTurnEngine.cs`
- Test: `BorderValley/Assets/Tests/EditMode/Battle/BattleTurnEngineTests.cs`

**Interfaces:**
- Consumes: `BattleState`、`BattleUnit`。
- Produces: `TurnOrder.Build(IEnumerable<BattleUnit>)`、`BattleTurnEngine.Start`、`ActiveUnit`、`EndTurn`。

- [ ] **Step 1: 写失败测试**

```csharp
using System.Linq;
using BorderValley.Battle.Domain;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class BattleTurnEngineTests
    {
        [Test]
        public void Build_OrdersBySpeedThenOriginalOrder()
        {
            var fast = Unit("a", Team.Player, 9, 0);
            var slow = Unit("b", Team.Enemy, 3, 0);
            var tie = Unit("c", Team.Player, 3, 1);

            var order = TurnOrder.Build(new[] { slow, fast, tie }).ToArray();

            Assert.That(order.Select(unit => unit.Id), Is.EqualTo(new[] { "a", "b", "c" }));
        }

        [Test]
        public void EndTurn_AdvancesRoundAndSkipsDeadUnits()
        {
            var state = new BattleState(BattleMap.CreatePlain(3, 1));
            var first = Unit("a", Team.Player, 10, 0);
            var second = Unit("b", Team.Enemy, 5, 0);
            state.AddUnit(first);
            state.AddUnit(second);
            var engine = new BattleTurnEngine(state);
            engine.Start();
            second.ApplyRawDamage(999);

            engine.EndTurn();

            Assert.That(engine.ActiveUnit.Id, Is.EqualTo("a"));
            Assert.That(state.Round, Is.EqualTo(2));
        }

        private static BattleUnit Unit(string id, Team team, int speed, int x) =>
            new BattleUnit(id, "test.unit", team,
                new UnitStats(10, 0, 1, 0, speed, 0f, 0), new GridPosition(x, 0));
    }
}
```

- [ ] **Step 2: 实现顺序和回合**

`TurnOrder.cs`：

```csharp
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Battle.Domain
{
    public static class TurnOrder
    {
        public static IReadOnlyList<BattleUnit> Build(IEnumerable<BattleUnit> units)
        {
            return units
                .Select((unit, index) => new { unit, index })
                .OrderByDescending(item => item.unit.Stats.Speed)
                .ThenBy(item => item.index)
                .Select(item => item.unit)
                .ToArray();
        }
    }
}
```

`BattleTurnEngine.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Battle.Domain
{
    public sealed class BattleTurnEngine
    {
        private readonly BattleState state;
        private readonly List<BattleUnit> order = new();
        private int activeIndex;

        public BattleTurnEngine(BattleState state) => this.state = state;
        public BattleUnit ActiveUnit { get; private set; }

        public void Start()
        {
            order.Clear();
            order.AddRange(TurnOrder.Build(state.LivingUnits));
            activeIndex = 0;
            ActivateCurrent();
        }

        public void EndTurn()
        {
            if (ActiveUnit == null) throw new InvalidOperationException("Battle has not started.");
            var previousIndex = activeIndex;
            do
            {
                activeIndex++;
                if (activeIndex >= order.Count)
                {
                    state.Round++;
                    order.Clear();
                    order.AddRange(TurnOrder.Build(state.LivingUnits));
                    activeIndex = 0;
                }
            } while (order.Count > 0 && !order[activeIndex].IsAlive);

            if (order.Count == 0) throw new InvalidOperationException("No living units remain.");
            ActivateCurrent();
        }

        private void ActivateCurrent()
        {
            ActiveUnit = order[activeIndex];
            ActiveUnit.RefreshForTurn();
        }
    }
}
```

- [ ] **Step 3: 运行测试并提交**

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -runTests -testPlatform EditMode `
  -testResults "$PWD\editmode-battle-results.xml" -logFile "$PWD\editmode-battle.log"
git add BorderValley/Assets/Code/Battle/Domain BorderValley/Assets/Tests/EditMode/Battle/BattleTurnEngineTests.cs
git commit -m "feat(battle): add turn order and round engine"
```

预期：`BattleTurnEngineTests` 全部通过。

---

### Task 4: 伤害、地形与状态结算

**Files:**
- Create: `BorderValley/Assets/Code/Battle/Domain/DamageType.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/DamageRequest.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/DamageResult.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/DamageCalculator.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/StatusSystem.cs`
- Test: `BorderValley/Assets/Tests/EditMode/Battle/DamageCalculatorTests.cs`
- Test: `BorderValley/Assets/Tests/EditMode/Battle/StatusSystemTests.cs`

**Interfaces:**
- Consumes: `BattleUnit`、`BattleMap`、`IRandomSource`。
- Produces: `DamageCalculator.Calculate(DamageRequest, BattleMap, IRandomSource)`、`StatusSystem.Apply`、`ResolveTurnStart`、`ResolveTurnEnd`、`IsStunned`、`MovementPenalty`、`ConsumeShield`。

- [ ] **Step 1: 写测试**

```csharp
using System.Collections.Generic;
using BorderValley.Battle.Domain;
using BorderValley.Core.Random;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class DamageCalculatorTests
    {
        [Test]
        public void PhysicalDamage_SubtractsArmorWithMinimumOne()
        {
            var attacker = Unit("a", Team.Player, power: 10, armor: 0, resistance: 0);
            var defender = Unit("b", Team.Enemy, power: 1, armor: 3, resistance: 0);
            var request = new DamageRequest(attacker, defender, 1f, DamageType.Physical, true);

            var result = DamageCalculator.Calculate(
                request, BattleMap.CreatePlain(2, 1), RandomSourceFactory.FromSeed("fixed"));

            Assert.That(result.Damage, Is.EqualTo(7));
        }

        [Test]
        public void HighGround_AddsTwentyFivePercentAfterArmor()
        {
            var map = new BattleMap(2, 1, new[] { TerrainType.HighGround, TerrainType.Plain });
            var attacker = UnitAt("a", Team.Player, 10, 0, 0, 0);
            var defender = UnitAt("b", Team.Enemy, 1, 1, 3, 0);
            var request = new DamageRequest(attacker, defender, 1f, DamageType.Physical, false);

            var result = DamageCalculator.Calculate(request, map, RandomSourceFactory.FromSeed("fixed"));

            Assert.That(result.Damage, Is.EqualTo(9));
            Assert.That(result.HighGroundBonus, Is.True);
        }

        [Test]
        public void Shield_AbsorbsDamageBeforeHealth()
        {
            var target = Unit("b", Team.Enemy, 1, 0, 0);
            StatusSystem.Apply(target, StatusType.Shielded, 4, 2, "a");

            var absorbed = StatusSystem.ConsumeShield(target, 3);

            Assert.That(absorbed, Is.EqualTo(3));
            Assert.That(target.Health, Is.EqualTo(target.Stats.MaxHealth));
            Assert.That(StatusSystem.GetShield(target), Is.EqualTo(1));
        }

        private static BattleUnit Unit(string id, Team team, int power, int armor, int resistance) =>
            UnitAt(id, team, power, 0, armor, resistance);

        private static BattleUnit UnitAt(string id, Team team, int power, int x, int armor, int resistance) =>
            new BattleUnit(id, "test.unit", team,
                new UnitStats(20, 0, power, armor, 5, 0f, resistance), new GridPosition(x, 0));
    }
}
```

- [ ] **Step 2: 实现伤害与状态**

`DamageType.cs`：

```csharp
namespace BorderValley.Battle.Domain
{
    public enum DamageType { Physical, Magical }
}
```

`DamageRequest.cs`：

```csharp
namespace BorderValley.Battle.Domain
{
    public readonly struct DamageRequest
    {
        public DamageRequest(BattleUnit attacker, BattleUnit defender, float powerMultiplier,
            DamageType damageType, bool canCrit)
        {
            Attacker = attacker;
            Defender = defender;
            PowerMultiplier = powerMultiplier < 0f ? 0f : powerMultiplier;
            DamageType = damageType;
            CanCrit = canCrit;
        }

        public BattleUnit Attacker { get; }
        public BattleUnit Defender { get; }
        public float PowerMultiplier { get; }
        public DamageType DamageType { get; }
        public bool CanCrit { get; }
    }
}
```

`DamageResult.cs`：

```csharp
namespace BorderValley.Battle.Domain
{
    public readonly struct DamageResult
    {
        public DamageResult(int damage, bool critical, bool highGroundBonus, int shieldAbsorbed)
        {
            Damage = damage;
            Critical = critical;
            HighGroundBonus = highGroundBonus;
            ShieldAbsorbed = shieldAbsorbed;
        }

        public int Damage { get; }
        public bool Critical { get; }
        public bool HighGroundBonus { get; }
        public int ShieldAbsorbed { get; }
    }
}
```

`DamageCalculator.cs`：

```csharp
using System;
using BorderValley.Core.Random;

namespace BorderValley.Battle.Domain
{
    public static class DamageCalculator
    {
        public static DamageResult Calculate(DamageRequest request, BattleMap map, IRandomSource random)
        {
            var raw = Math.Max(1, (int)MathF.Round(request.Attacker.Stats.Power * request.PowerMultiplier));
            var defense = request.DamageType == DamageType.Physical
                ? request.Defender.Stats.Armor
                : request.Defender.Stats.Resistance;
            var damage = Math.Max(1, raw - defense);

            var highGround = map.GetTerrain(request.Attacker.Position) == TerrainType.HighGround;
            if (highGround) damage = (int)MathF.Ceiling(damage * 1.25f);

            var critical = request.CanCrit && random.Value01() < request.Attacker.Stats.CritChance;
            if (critical) damage = (int)MathF.Ceiling(damage * 1.5f);

            var absorbed = StatusSystem.ConsumeShield(request.Defender, damage);
            var healthDamage = Math.Max(0, damage - absorbed);
            request.Defender.ApplyRawDamage(healthDamage);
            return new DamageResult(healthDamage, critical, highGround, absorbed);
        }
    }
}
```

`StatusSystem.cs` 使用 Task 2 已有的 `StatusType` 和 `StatusInstance`：

```csharp
using System.Linq;

namespace BorderValley.Battle.Domain
{
    public static class StatusSystem
    {
        public static void Apply(BattleUnit target, StatusType type, int magnitude, int duration, string sourceId)
        {
            var existing = target.MutableStatuses.FirstOrDefault(status => status.Type == type);
            if (existing == null)
            {
                target.MutableStatuses.Add(new StatusInstance(type, magnitude, duration, sourceId));
                return;
            }

            if (type is StatusType.Burning or StatusType.Poisoned or StatusType.Shielded)
                existing.Magnitude += magnitude;
            else
                existing.Magnitude = System.Math.Max(existing.Magnitude, magnitude);
            existing.RemainingTurns = System.Math.Max(existing.RemainingTurns, duration);
        }

        public static void ResolveTurnStart(BattleUnit unit)
        {
            var damage = unit.MutableStatuses
                .Where(status => status.Type is StatusType.Burning or StatusType.Poisoned)
                .Sum(status => status.Magnitude);
            if (damage > 0) unit.ApplyRawDamage(damage);
        }

        public static void ResolveTurnEnd(BattleUnit unit)
        {
            foreach (var status in unit.MutableStatuses)
                status.RemainingTurns--;
            unit.MutableStatuses.RemoveAll(status => status.RemainingTurns <= 0);
        }

        public static bool IsStunned(BattleUnit unit) =>
            unit.MutableStatuses.Any(status => status.Type == StatusType.Stunned);

        public static int MovementPenalty(BattleUnit unit) =>
            unit.MutableStatuses.Where(status => status.Type == StatusType.Slowed)
                .Sum(status => status.Magnitude);

        public static int GetShield(BattleUnit unit) =>
            unit.MutableStatuses.Where(status => status.Type == StatusType.Shielded)
                .Sum(status => status.Magnitude);

        public static int ConsumeShield(BattleUnit unit, int amount)
        {
            var remaining = amount;
            var absorbed = 0;
            foreach (var status in unit.MutableStatuses.Where(status => status.Type == StatusType.Shielded).ToArray())
            {
                var used = System.Math.Min(status.Magnitude, remaining);
                status.Magnitude -= used;
                remaining -= used;
                absorbed += used;
                if (remaining == 0) break;
            }
            unit.MutableStatuses.RemoveAll(status => status.Type == StatusType.Shielded && status.Magnitude <= 0);
            return absorbed;
        }
    }
}
```

- [ ] **Step 3: 运行测试并提交**

预期：`DamageCalculatorTests` 和 `StatusSystemTests` 全部通过。

```powershell
git add BorderValley/Assets/Code/Battle/Domain BorderValley/Assets/Tests/EditMode/Battle
git commit -m "feat(battle): add damage terrain and status rules"
```

---

### Task 5: 技能定义、目标选择与执行

**Files:**
- Create: `BorderValley/Assets/Code/Battle/Domain/SkillTargeting.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/SkillEffectKind.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/SkillEffectDefinition.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/SkillDefinition.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/SkillTargetValidator.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/SkillExecutor.cs`
- Test: `BorderValley/Assets/Tests/EditMode/Battle/SkillExecutorTests.cs`

**Interfaces:**
- Consumes: `BattleState`、`BattleUnit`、`DamageCalculator`、`StatusSystem`。
- Produces: `SkillDefinition`、`SkillTargetValidator.GetValidTargets`、`SkillExecutor.Execute`。

- [ ] **Step 1: 写失败测试**

```csharp
using System.Linq;
using BorderValley.Battle.Domain;
using BorderValley.Core.Random;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class SkillExecutorTests
    {
        [Test]
        public void GetValidTargets_EnemySkill_RespectsRange()
        {
            var state = StateWithThreeUnits();
            var actor = state.GetUnit("p1");
            var skill = Skill("slash", SkillTargeting.Enemy, range: 1, mana: 0);

            var targets = SkillTargetValidator.GetValidTargets(state, actor, skill).ToArray();

            Assert.That(targets.Select(unit => unit.Id), Is.EquivalentTo(new[] { "e1" }));
        }

        [Test]
        public void Execute_DamageSkill_SpendsManaAndMarksAction()
        {
            var state = StateWithThreeUnits();
            var actor = state.GetUnit("p1");
            var target = state.GetUnit("e1");
            var skill = Skill("fireball", SkillTargeting.Enemy, range: 2, mana: 2,
                new SkillEffectDefinition(SkillEffectKind.Damage, 1.5f, StatusType.Burning, 1, 2));

            var result = SkillExecutor.Execute(
                state, actor, skill, target, RandomSourceFactory.FromSeed("fireball"));

            Assert.That(result.Success, Is.True);
            Assert.That(actor.Mana, Is.EqualTo(8));
            Assert.That(actor.HasActed, Is.True);
            Assert.That(target.Health, Is.LessThan(target.Stats.MaxHealth));
            Assert.That(target.Statuses.Any(status => status.Type == StatusType.Burning), Is.True);
        }

        private static BattleState StateWithThreeUnits()
        {
            var state = new BattleState(BattleMap.CreatePlain(4, 1));
            state.AddUnit(Unit("p1", Team.Player, 0));
            state.AddUnit(Unit("e1", Team.Enemy, 1));
            state.AddUnit(Unit("e2", Team.Enemy, 3));
            return state;
        }

        private static BattleUnit Unit(string id, Team team, int x) =>
            new BattleUnit(id, "test.unit", team,
                new UnitStats(20, 10, 8, 1, 5, 0f, 0), new GridPosition(x, 0));

        private static SkillDefinition Skill(string id, SkillTargeting targeting, int range, int mana,
            params SkillEffectDefinition[] effects) =>
            new SkillDefinition(id, "skill." + id, targeting, range, 0, mana, 0, effects);
    }
}
```

- [ ] **Step 2: 实现技能系统**

`SkillTargeting.cs`：`Enemy`、`Ally`、`Self`、`Ground`。
`SkillEffectKind.cs`：`Damage`、`Heal`、`ApplyStatus`、`Push`、`Pull`。
`SkillEffectDefinition.cs` 和 `SkillDefinition.cs` 使用只读属性和构造参数，不依赖场景对象。
`SkillTargetValidator` 使用曼哈顿距离，检查 `BattleMap.InBounds`，敌人/友军按 `Team` 过滤，`Self` 返回施放者，`Ground` 返回范围内非障碍格。
`SkillExecutor.Execute` 必须按顺序校验：
1. 战斗单位存活。
2. 技能目标在 `GetValidTargets`。
3. 法力足够且技能不在冷却。
4. 扣法力、记录冷却并标记主要行动。
5. 顺序执行效果；任意效果抛错时不回滚。
6. 返回 `SkillExecutionResult`，其中包含成功、失败原因、造成伤害、治疗量和受影响单位。

冷却记录在 `BattleUnit.Cooldowns`：`Dictionary<string,int>`；每次单位回合开始时全部减一，最低为零。

- [ ] **Step 3: 运行测试并提交**

```powershell
git add BorderValley/Assets/Code/Battle/Domain BorderValley/Assets/Tests/EditMode/Battle/SkillExecutorTests.cs
git commit -m "feat(battle): add skill targeting and execution"
```

预期：`SkillExecutorTests` 全部通过。

---

### Task 6: 战斗命令、行动限制与胜负结算

**Files:**
- Create: `BorderValley/Assets/Code/Battle/Domain/BattleCommand.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/BattleActionResult.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/BattleOutcome.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/BattleEngine.cs`
- Test: `BorderValley/Assets/Tests/EditMode/Battle/BattleEngineTests.cs`

**Interfaces:**
- Consumes: `BattleState`、`BattleTurnEngine`、`GridPathfinder`、`SkillExecutor`。
- Produces: `BattleEngine.Start`、`Execute(BattleCommand)`、`Outcome`、`TryMove`、`UseSkill`、`EndTurn`。

- [ ] **Step 1: 写失败测试**

```csharp
using BorderValley.Battle.Domain;
using BorderValley.Core.Random;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class BattleEngineTests
    {
        [Test]
        public void Move_ToReachableCell_ConsumesMovement()
        {
            var engine = EngineWithUnits();
            var actor = engine.State.GetUnit("p1");

            var result = engine.Execute(new MoveCommand("p1", new GridPosition(1, 0)));

            Assert.That(result.Success, Is.True);
            Assert.That(actor.Position, Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(actor.HasMoved, Is.True);
        }

        [Test]
        public void Move_ToOccupiedCell_FailsWithoutMutation()
        {
            var engine = EngineWithUnits();
            var actor = engine.State.GetUnit("p1");

            var result = engine.Execute(new MoveCommand("p1", new GridPosition(2, 0)));

            Assert.That(result.Success, Is.False);
            Assert.That(actor.Position, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(actor.HasMoved, Is.False);
        }

        [Test]
        public void Outcome_IsPlayerVictory_WhenAllEnemiesDie()
        {
            var engine = EngineWithUnits();
            engine.State.GetUnit("e1").ApplyRawDamage(999);

            engine.Execute(new EndTurnCommand("p1"));

            Assert.That(engine.Outcome, Is.EqualTo(BattleOutcome.PlayerVictory));
        }

        private static BattleEngine EngineWithUnits()
        {
            var state = new BattleState(BattleMap.CreatePlain(3, 1));
            state.AddUnit(Unit("p1", Team.Player, 6, 0));
            state.AddUnit(Unit("e1", Team.Enemy, 2, 0));
            var engine = new BattleEngine(state, RandomSourceFactory.FromSeed("battle"));
            engine.Start();
            return engine;
        }

        private static BattleUnit Unit(string id, Team team, int speed, int x) =>
            new BattleUnit(id, "test.unit", team,
                new UnitStats(20, 10, 8, 1, speed, 0f, 0), new GridPosition(x, 0));
    }
}
```

- [ ] **Step 2: 实现命令和引擎**

定义以下命令类型：

```csharp
public abstract class BattleCommand { public string UnitId { get; } }
public sealed class MoveCommand : BattleCommand { public GridPosition Destination { get; } }
public sealed class UseSkillCommand : BattleCommand { public string SkillId { get; } public string TargetUnitId { get; } }
public sealed class EndTurnCommand : BattleCommand { }
```

`BattleActionResult` 包含 `Success`、`ErrorCode`、`Message` 和受影响单位 ID。成功与失败都必须返回新对象，不使用异常处理普通操作失败。

`BattleOutcome`：

```csharp
public enum BattleOutcome { InProgress, PlayerVictory, EnemyVictory }
```

`BattleEngine` 规则：

- `Start` 调用 `BattleTurnEngine.Start`。
- `MoveCommand`：只能操作当前回合单位；单位未移动；目标在 `GridPathfinder.FindReachable` 中；目标未被占用；成功后设置位置并消耗移动。
- `UseSkillCommand`：只能操作当前回合单位；单位未行动；技能存在；目标有效；技能执行成功后消耗行动。
- `EndTurnCommand`：只能由当前单位发出；使用 `StatusSystem.ResolveTurnEnd`；由回合引擎推进到下一单位；下一单位开始前调用 `StatusSystem.ResolveTurnStart`。如果该步杀死了单位，则继续推进；如果新单位带有眩晕，则立即对其执行 `ResolveTurnEnd`，不生成任何行动机会，再继续推进。
- 每次命令后调用 `EvaluateOutcome`：玩家全灭为 `EnemyVictory`，敌人全灭为 `PlayerVictory`。
- 战斗结束后拒绝所有带状态变更的命令。

- [ ] **Step 3: 运行测试并提交**

```powershell
git add BorderValley/Assets/Code/Battle/Domain BorderValley/Assets/Tests/EditMode/Battle/BattleEngineTests.cs
git commit -m "feat(battle): add command engine and battle outcome"
```

预期：`BattleEngineTests` 全部通过。

---

### Task 7: 可解释的敌方 AI

**Files:**
- Create: `BorderValley/Assets/Code/Battle/Domain/BattleAi.cs`
- Test: `BorderValley/Assets/Tests/EditMode/Battle/BattleAiTests.cs`

**Interfaces:**
- Consumes: `BattleEngine`、`SkillDefinition` 集合。
- Produces: `BattleAi.ChooseCommand(BattleEngine, string unitId, IReadOnlyDictionary<string, SkillDefinition>)`。

- [ ] **Step 1: 写失败测试**

```csharp
using System.Collections.Generic;
using BorderValley.Battle.Domain;
using BorderValley.Core.Random;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class BattleAiTests
    {
        [Test]
        public void ChooseCommand_PrefersLethalAttack()
        {
            var engine = BattleEngineWithUnits();
            var enemy = engine.State.GetUnit("e1");
            var player = engine.State.GetUnit("p1");
            player.ApplyRawDamage(player.Health - 1);
            var skills = new Dictionary<string, SkillDefinition>
            {
                ["basic"] = BasicAttack()
            };

            var command = BattleAi.ChooseCommand(engine, enemy.Id, skills);

            Assert.That(command, Is.TypeOf<UseSkillCommand>());
            Assert.That(((UseSkillCommand)command).SkillId, Is.EqualTo("skill.basic"));
            Assert.That(((UseSkillCommand)command).TargetUnitId, Is.EqualTo("p1"));
        }

        [Test]
        public void ChooseCommand_WithNoTargetInRange_MovesTowardEnemy()
        {
            var engine = BattleEngineWithUnits(distance: 5);
            var enemy = engine.State.GetUnit("e1");

            var command = BattleAi.ChooseCommand(
                engine, enemy.Id, new Dictionary<string, SkillDefinition> { ["basic"] = BasicAttack() });

            Assert.That(command, Is.TypeOf<MoveCommand>());
        }

        private static BattleEngine BattleEngineWithUnits(int distance = 1)
        {
            var width = distance + 2;
            var state = new BattleState(BattleMap.CreatePlain(width, 1));
            state.AddUnit(Unit("p1", Team.Player, 5, 0, 3));
            state.AddUnit(Unit("e1", Team.Enemy, 6, distance, 3));
            var engine = new BattleEngine(state, RandomSourceFactory.FromSeed("ai"));
            engine.Start();
            return engine;
        }

        private static BattleUnit Unit(string id, Team team, int speed, int x, int power) =>
            new BattleUnit(id, "test.unit", team,
                new UnitStats(20, 10, power, 0, speed, 0f, 0), new GridPosition(x, 0));

        private static SkillDefinition BasicAttack() =>
            new SkillDefinition("skill.basic", "skill.basic.name", SkillTargeting.Enemy,
                1, 0, 0, 1f,
                new SkillEffectDefinition(SkillEffectKind.Damage, 1f, StatusType.Burning, 0, 0));
    }
}
```

- [ ] **Step 2: 实现评分和确定性平分规则**

AI 对每个合法命令评分：

1. 可以击杀目标：`10000 + 目标威胁值`。
2. 造成伤害：`damage * 20`。
3. 治疗：`healAmount * 15`。
4. 施加眩晕：`900`；嘲讽：`500`；减速：`250`。
5. 护盾：`shieldAmount * 8`。
6. 移动后能进入技能射程：`400 - 剩余距离`。
7. 移动靠近最近敌人：`100 - 移动后距离 * 10`。
8. 结束回合：`-1000`。

平分规则按以下顺序固定：

1. 更高分数。
2. 技能 ID 字典序更小。
3. 目标单位 ID 字典序更小。
4. 目标格 X、Y 依次更小。
5. 最后选择 `EndTurnCommand`。

AI 不访问动画、UI 或隐藏数据；所有评估都读取 `BattleEngine.State`。

- [ ] **Step 3: 运行测试并提交**

```powershell
git add BorderValley/Assets/Code/Battle/Domain/BattleAi.cs BorderValley/Assets/Tests/EditMode/Battle/BattleAiTests.cs
git commit -m "feat(battle): add deterministic enemy AI"
```

预期：`BattleAiTests` 全部通过，运行两次返回同一命令。

---

### Task 8: 首版职业、技能与 3 对 3 场景

**Files:**
- Create: `BorderValley/Assets/Code/Battle/Domain/BattleScenario.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/BattleScenarioFactory.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/BattleSimulator.cs`
- Test: `BorderValley/Assets/Tests/EditMode/Battle/BattleScenarioTests.cs`

**Interfaces:**
- Consumes: 前七个任务的所有规则接口。
- Produces: `BattleScenarioFactory.CreateCoreScenario()`、`BattleSimulator.RunUntilComplete(...)`。

- [ ] **Step 1: 写完整战斗测试**

```csharp
using System.Collections.Generic;
using BorderValley.Battle.Domain;
using BorderValley.Core.Random;
using NUnit.Framework;

namespace BorderValley.Battle.Tests
{
    public sealed class BattleScenarioTests
    {
        [Test]
        public void CoreScenario_CompletesWithinThirtyRounds()
        {
            var scenario = BattleScenarioFactory.CreateCoreScenario();
            var engine = new BattleEngine(scenario.State, RandomSourceFactory.FromSeed("vertical-slice"));
            engine.Start();

            var rounds = BattleSimulator.RunUntilComplete(
                engine, scenario.PlayerSkills, scenario.EnemySkills, maxCommands: 200);

            Assert.That(engine.Outcome, Is.Not.EqualTo(BattleOutcome.InProgress));
            Assert.That(rounds, Is.LessThanOrEqualTo(30));
        }

        [Test]
        public void CoreScenario_IsDeterministicForSameSeed()
        {
            var first = Run("same-seed");
            var second = Run("same-seed");

            Assert.That(second.Outcome, Is.EqualTo(first.Outcome));
            Assert.That(second.Commands, Is.EqualTo(first.Commands));
        }

        private static SimulationRecord Run(string seed)
        {
            var scenario = BattleScenarioFactory.CreateCoreScenario();
            var engine = new BattleEngine(scenario.State, RandomSourceFactory.FromSeed(seed));
            engine.Start();
            return BattleSimulator.RunUntilComplete(
                engine, scenario.PlayerSkills, scenario.EnemySkills, maxCommands: 200);
        }
    }
}
```

- [ ] **Step 2: 定义首版数值和技能**

`BattleScenarioFactory.CreateCoreScenario` 使用 8×6 平地地图，中央放置两块高地和两格泥地，双方各三名单位：

| 单位 | 生命 | 法力 | 攻击 | 护甲 | 速度 | 暴击 | 抗性 |
|---|---:|---:|---:|---:|---:|---:|---:|
| 战士 | 26 | 8 | 9 | 6 | 4 | 5% | 2 |
| 游侠 | 18 | 10 | 10 | 3 | 7 | 15% | 2 |
| 法师 | 15 | 16 | 11 | 1 | 5 | 5% | 6 |
| 山贼 | 20 | 0 | 8 | 3 | 5 | 5% | 1 |

技能定义：

- `skill.shield_bash`：敌方，射程 1，法力 2，伤害 1.0，附加眩晕 1 回合。
- `skill.whirlwind`：地面，射程 0，半径 1，法力 4，对范围内敌人造成 0.8 伤害并击退 1 格。
- `skill.iron_guard`：自身，法力 3，获得护盾 6，持续 2 回合。
- `skill.piercing_shot`：敌方，射程 4，法力 2，伤害 1.2，忽略 2 点护甲。
- `skill.snare`：敌方，射程 3，法力 2，伤害 0.6，减速 1，持续 2 回合。
- `skill.twin_shot`：敌方，射程 3，法力 3，伤害 1.0，并施加中毒 2，持续 2 回合。
- `skill.fireball`：敌方，射程 4，半径 1，法力 4，伤害 1.3，燃烧 2，持续 2 回合。
- `skill.frost_nova`：自身，半径 2，法力 4，造成 0.7 伤害并减速 2，持续 2 回合。
- `skill.arcane_ward`：友方，射程 3，法力 3，治疗 6 并施加护盾 4，持续 2 回合。

如果某个技能需要新增效果类型，必须先在 Task 5 的技能执行器中加入对应测试，再在本任务使用。

- [ ] **Step 3: 实现场景构造和模拟器**

`BattleScenario` 包含：

- `BattleState State`
- `Dictionary<string, SkillDefinition> PlayerSkills`
- `Dictionary<string, SkillDefinition> EnemySkills`
- `Dictionary<string, string[]> UnitSkills`
- `BattleEngine` 初始化后由 `BattleSimulator` 按单位选择 AI 命令。

`BattleSimulator.RunUntilComplete` 每次只调用 `BattleAi.ChooseCommand`，然后执行该命令，记录命令字符串，直到战斗结束或达到 `maxCommands`。达到上限仍未结束时抛出 `InvalidOperationException`，避免无限循环。

- [ ] **Step 4: 运行全部战棋测试并提交**

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath "$PWD\BorderValley" `
  -runTests -testPlatform EditMode `
  -testResults "$PWD\editmode-battle-results.xml" -logFile "$PWD\editmode-battle.log"
git add BorderValley/Assets/Code/Battle BorderValley/Assets/Tests/EditMode/Battle
git commit -m "feat(battle): add core 3v3 scenario and simulation"
```

预期：全部战棋测试通过，核心场景可在 30 回合内结束，同一种子得到相同命令序列。

---

## 完成标准

- `BorderValley.Battle` 程序集中没有场景、UI、动画或 Input 依赖。
- 网格移动、泥地、障碍、占位和可达范围都有测试。
- 行动顺序、回合重置、死亡跳过和胜负结算都有测试。
- 物理/魔法伤害、暴击、高地、护盾和持续伤害都有测试。
- 至少 9 个技能效果覆盖单体、范围、治疗、护盾、击退和状态。
- AI 至少覆盖击杀优先、治疗、接近目标和确定性平分。
- 3 对 3 核心场景能完整结束，并可重复复现。
- 每个任务单独提交，测试命令和结果记录在报告中。

## 后续计划

本计划完成后创建 `2026-09-13-battle-scene.md`，实现：

- 战斗场景和网格渲染。
- 触控选人、移动范围、技能范围和目标预览。
- 行动顺序、血条、法力、状态和结束回合 HUD。
- 战斗与世界场景的 `BattleRequest` / `BattleResult` 交接。
- 小米 10S 真机触控和 60 FPS 验证。