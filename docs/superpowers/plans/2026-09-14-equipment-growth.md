# 装备、掉落、随机词条、打造与成长 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在现有 Unity 垂直切片中加入可持久化的角色成长、装备、随机词条、种子掉落、背包经济、打造重铸和战斗生效闭环。

**Architecture:** 静态定义继续放在 `BorderValley.Data`，跨模块 DTO 放在 `BorderValley.Core.BattleFlow`，库存/成长/掉落/经济规则放在 `BorderValley.Inventory`，战斗只消费 `BattlePartySnapshot`，UI 只协调服务公开接口。Boot 通过 `IGameServiceInstaller` 安装 Inventory 服务并把库存、成长和钱包注册为 `ISaveParticipant`。

**Tech Stack:** Unity 6000.6.0f1、C#、Unity Test Framework 1.8.0、Newtonsoft.Json、uGUI、现有 `IRandomSource`、`SaveService`、`BattleScenario`。

**Spec:** `docs/superpowers/specs/2026-09-12-fantasy-tactics-rpg-vertical-slice-design.md`

## Global Constraints

- Android 横屏、API 26、IL2CPP、ARM64、60 FPS、vSync 0。
- 玩家可见文字只使用本地化键，首版占位实现直接显示键名。
- 所有随机行为必须注入 `IRandomSource`；同一种子、同一输入必须得到同一结果。
- 战斗规则不得读取场景、UI、Input 或帧时间。
- 装备、词条、掉落、成长和经济规则必须存在于纯 C# 可测试类型中，不能藏在 MonoBehaviour 内。
- 背包容量上限为 30；等级上限为 10；装备槽固定为武器、副手、头部、身体、饰品、靴子。
- 普通/精良/稀有/史诗随机词条数量分别为 0/1/2/3；史诗至少包含一条技能修正或触发词条。
- 同一装备不能出现重复词条或互斥词条；词条必须验证装备槽、最小品质和物品等级。
- 首版不加入耐久度、强化等级、仓库、每日商店刷新。
- 不提交 APK、截图、测试 XML、日志和 `.superpowers` 内容；新增 Unity 文件必须提交对应 `.meta`。
- 每个 task 独立提交；真机测试前必须暂停并通知用户，不自行安装或启动 APK。

---

## 文件结构

```text
BorderValley/Assets/Code/Core/Combat/
  CombatStat.cs
  SkillModifierKind.cs
  PassiveEffectKind.cs

BorderValley/Assets/Code/Core/BattleFlow/
  BattlePartySnapshot.cs
  BattleCombatantSnapshot.cs
  BattleSkillModifierSnapshot.cs
  BattlePassiveSnapshot.cs
  BattleRequest.cs
  BattleResult.cs

BorderValley/Assets/Code/Core/Boot/
  IGameServiceInstaller.cs
  GameBootstrapper.cs

BorderValley/Assets/Code/Data/Items/
  ItemSlot.cs
  ItemRarity.cs
  AffixEffectKind.cs
  StatValue.cs
  SkillUnlock.cs
  ItemDefinition.cs
  AffixDefinition.cs
  ItemDropTableDefinition.cs
  CharacterDefinition.cs

BorderValley/Assets/Code/Data/
  ContentCatalog.cs
  ContentValidator.cs

BorderValley/Assets/Code/Inventory/
  InventoryBootstrapInstaller.cs
  Items/AffixInstance.cs
  Items/ItemInstance.cs
  Items/InventoryFilter.cs
  Items/InventorySort.cs
  InventoryQuery.cs
  InventoryService.cs
  Progression/PartyMemberState.cs
  Progression/PartyProgressionService.cs
  Progression/EquipmentStatAggregator.cs
  Progression/PartyBattleSnapshotBuilder.cs
  Loot/LootGenerator.cs
  Economy/EconomyService.cs
  Economy/CraftingCosts.cs
  Economy/CraftingService.cs

BorderValley/Assets/Code/Battle/Domain/
  BattleUnit.cs
  SkillModifierApplier.cs
  BattlePassiveRules.cs
  DamageCalculator.cs
  SkillExecutor.cs
  BattleScenarioFactory.cs

BorderValley/Assets/Code/UI/Inventory/
  InventoryTextKeys.cs
  InventoryUiPresenter.cs
  InventoryPanelView.cs

BorderValley/Assets/Code/UI/Battle/
  BattleSceneController.cs
  WorldBattleEntryView.cs

BorderValley/Assets/Editor/Tools/
  EquipmentContentBuilder.cs
  FoundationSceneBuilder.cs

BorderValley/Assets/Tests/EditMode/Inventory/
  ContentDefinitionTests.cs
  InventoryServiceTests.cs
  PartyProgressionServiceTests.cs
  PartyBattleSnapshotBuilderTests.cs
  LootGeneratorTests.cs
  EconomyAndCraftingTests.cs
  BattleEquipmentIntegrationTests.cs
  InventoryUiPresenterTests.cs

BorderValley/Assets/Tests/PlayMode/
  EquipmentGrowthFlowTests.cs
```

---

### Task 1: 定义共享战斗快照、装备内容模型和校验

**Files:**
- Create: `BorderValley/Assets/Code/Core/Combat/CombatStat.cs`
- Create: `BorderValley/Assets/Code/Core/Combat/SkillModifierKind.cs`
- Create: `BorderValley/Assets/Code/Core/Combat/PassiveEffectKind.cs`
- Create: `BorderValley/Assets/Code/Core/BattleFlow/BattlePartySnapshot.cs`
- Create: `BorderValley/Assets/Code/Core/BattleFlow/BattleCombatantSnapshot.cs`
- Create: `BorderValley/Assets/Code/Core/BattleFlow/BattleSkillModifierSnapshot.cs`
- Create: `BorderValley/Assets/Code/Core/BattleFlow/BattlePassiveSnapshot.cs`
- Create: `BorderValley/Assets/Code/Data/Items/ItemSlot.cs`
- Create: `BorderValley/Assets/Code/Data/Items/ItemRarity.cs`
- Create: `BorderValley/Assets/Code/Data/Items/AffixEffectKind.cs`
- Create: `BorderValley/Assets/Code/Data/Items/StatValue.cs`
- Create: `BorderValley/Assets/Code/Data/Items/SkillUnlock.cs`
- Create: `BorderValley/Assets/Code/Data/Items/ItemDefinition.cs`
- Create: `BorderValley/Assets/Code/Data/Items/AffixDefinition.cs`
- Create: `BorderValley/Assets/Code/Data/Items/ItemDropTableDefinition.cs`
- Create: `BorderValley/Assets/Code/Data/Items/CharacterDefinition.cs`
- Modify: `BorderValley/Assets/Code/Data/ContentCatalog.cs`
- Modify: `BorderValley/Assets/Code/Data/ContentValidator.cs`
- Create: `BorderValley/Assets/Tests/EditMode/Inventory/ContentDefinitionTests.cs`

**Interfaces:**
- Consumes: `ContentDefinition`、`CombatStat`、`SkillModifierKind`、`PassiveEffectKind`。
- Produces: `BattlePartySnapshot`、`BattleCombatantSnapshot`、`ItemDefinition`、`AffixDefinition`、`CharacterDefinition`、`ItemDropTableDefinition`，后续所有 task 只使用这些公开属性。

- [ ] **Step 1: 写失败测试**

```csharp
using System.Linq;
using BorderValley.Core.Combat;
using BorderValley.Data;
using BorderValley.Data.Items;
using NUnit.Framework;
using UnityEngine;

namespace BorderValley.Inventory.Tests
{
    public sealed class ContentDefinitionTests
    {
        [Test]
        public void Validate_AffixWithImpossibleRange_ReturnsIssue()
        {
            var affix = ScriptableObject.CreateInstance<AffixDefinition>();
            affix.EditorConfigure(
                "affix.bad",
                "affix.bad.name",
                new[] { ItemSlot.Weapon },
                ItemRarity.Fine,
                AffixEffectKind.FlatStat,
                CombatStat.Power,
                default,
                default,
                5,
                2,
                1,
                10,
                "affix.bad.cost",
                System.Array.Empty<string>());
            var issues = ContentValidator.Validate(new[] { affix }).ToList();
            Assert.That(issues.Any(issue => issue.Code == "invalid_affix_range"), Is.True);
            Object.DestroyImmediate(affix);
        }

        [Test]
        public void Validate_ItemAndDropTableReferences_AreResolved()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.EditorConfigure(
                "item.sword",
                "item.sword.name",
                ItemSlot.Weapon,
                new[] { "class.warrior" },
                false,
                100,
                new[] { new StatValue(CombatStat.Power, 4) });
            var table = ScriptableObject.CreateInstance<ItemDropTableDefinition>();
            table.EditorConfigure(
                "loot.test",
                2,
                10,
                new[] { new LootEntry(item, 1) },
                new[]
                {
                    new RarityWeight(ItemRarity.Common, 60),
                    new RarityWeight(ItemRarity.Fine, 40)
                });
            var issues = ContentValidator.Validate(new ContentDefinition[] { item, table }).ToList();
            Assert.That(issues, Is.Empty);
            Object.DestroyImmediate(item);
            Object.DestroyImmediate(table);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: 编译失败，`CombatStat`、`AffixDefinition`、`ItemDropTableDefinition` 尚不存在。

- [ ] **Step 3: 实现共享枚举和快照**

```csharp
namespace BorderValley.Core.Combat
{
    public enum CombatStat
    {
        MaxHealth,
        MaxMana,
        Power,
        Armor,
        Speed,
        CritChanceBps,
        Resistance
    }

    public enum SkillModifierKind
    {
        Range,
        Radius,
        ManaCost,
        Cooldown,
        PowerMultiplierBps
    }

    public enum PassiveEffectKind
    {
        OnAttackApplySlow,
        OnHitGainShield,
        OnKillHeal,
        LowHealthArmor,
        HighGroundDamage
    }
}
```

`BattleCombatantSnapshot` 必须只保存值类型和字符串：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace BorderValley.Core.BattleFlow
{
    public sealed class BattleCombatantSnapshot
    {
        public BattleCombatantSnapshot(
            string unitId,
            string definitionId,
            string classId,
            int maxHealth,
            int maxMana,
            int power,
            int armor,
            int speed,
            int critChanceBps,
            int resistance,
            int currentHealth,
            int currentMana,
            IEnumerable<string> skillIds,
            IEnumerable<BattleSkillModifierSnapshot> skillModifiers,
            IEnumerable<BattlePassiveSnapshot> passives)
        {
            UnitId = Require(unitId, nameof(unitId));
            DefinitionId = Require(definitionId, nameof(definitionId));
            ClassId = Require(classId, nameof(classId));
            MaxHealth = Math.Max(1, maxHealth);
            MaxMana = Math.Max(0, maxMana);
            Power = Math.Max(0, power);
            Armor = Math.Max(0, armor);
            Speed = Math.Max(0, speed);
            CritChanceBps = Math.Clamp(critChanceBps, 0, 10000);
            Resistance = Math.Max(0, resistance);
            CurrentHealth = Math.Clamp(currentHealth, 1, MaxHealth);
            CurrentMana = Math.Clamp(currentMana, 0, MaxMana);
            SkillIds = Copy(skillIds);
            SkillModifiers = Copy(skillModifiers);
            Passives = Copy(passives);
        }

        public string UnitId { get; }
        public string DefinitionId { get; }
        public string ClassId { get; }
        public int MaxHealth { get; }
        public int MaxMana { get; }
        public int Power { get; }
        public int Armor { get; }
        public int Speed { get; }
        public int CritChanceBps { get; }
        public int Resistance { get; }
        public int CurrentHealth { get; }
        public int CurrentMana { get; }
        public IReadOnlyList<string> SkillIds { get; }
        public IReadOnlyList<BattleSkillModifierSnapshot> SkillModifiers { get; }
        public IReadOnlyList<BattlePassiveSnapshot> Passives { get; }

        private static string Require(string value, string name) =>
            string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(name) : value;

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values)
        {
            var copy = (values ?? Array.Empty<T>()).ToArray();
            if (copy.Any(value => value == null)) throw new ArgumentException("Snapshot lists cannot contain null.");
            return Array.AsReadOnly(copy);
        }
    }
}
```

`BattleSkillModifierSnapshot` 和 `BattlePassiveSnapshot` 分别保存 `(skillId, kind, value)` 与 `(kind, magnitude, duration)`；`BattlePartySnapshot` 复制成员并要求至少一个成员。

- [ ] **Step 4: 实现 ItemDefinition、AffixDefinition、DropTable、CharacterDefinition**

```csharp
namespace BorderValley.Data.Items
{
    public enum ItemSlot { Weapon, Offhand, Head, Body, Accessory, Boots }
    public enum ItemRarity { Common, Fine, Rare, Epic }
    public enum AffixEffectKind { FlatStat, PercentStat, SkillModifier, Trigger, Conditional }
}
```

`ItemDefinition` 公开 `Slot`、`AllowedClassIds`、`IsQuestItem`、`BaseValue`、`Stats` 和 `GetBaseStat(CombatStat stat, int itemLevel)`。`GetBaseStat` 返回 `value + Math.Max(0, itemLevel - 1)`，品质不直接修改基础值。

`AffixDefinition.EditorConfigure` 的固定参数顺序为 `(id, localizationKey, compatibleSlots, minimumRarity, effectKind, stat, skillModifier, passiveEffect, minValue, maxValue, minItemLevel, duration, weight, budgetCost, mutuallyExclusiveAffixIds)`。`AffixDefinition` 公开所有字段的只读属性，并提供 `Supports(ItemSlot slot, ItemRarity rarity, int itemLevel)`。`ItemDropTableDefinition` 公开 `MinItemLevel`、`MaxItemLevel`、`Entries`、`RarityWeights`。`CharacterDefinition` 公开基础属性、每级固定成长、起始技能和 `SkillUnlock[]`。

- [ ] **Step 5: 扩展 ContentValidator**

保留现有 ID 校验，然后增加：

```csharp
private static IEnumerable<ContentValidationIssue> ValidateSpecialized(IEnumerable<ContentDefinition> definitions)
{
    var all = definitions.Where(definition => definition != null).ToArray();
    var ids = all.Select(definition => definition.Id).Where(id => !string.IsNullOrWhiteSpace(id))
        .ToHashSet(System.StringComparer.Ordinal);

    foreach (var affix in all.OfType<AffixDefinition>())
    {
        if (affix.MinValue > affix.MaxValue)
            yield return Issue("invalid_affix_range", affix.Id, affix);
        if (affix.CompatibleSlots.Length == 0)
            yield return Issue("affix_without_slot", affix.Id, affix);
        foreach (var excluded in affix.MutuallyExclusiveAffixIds)
            if (!ids.Contains(excluded))
                yield return Issue("missing_affix_exclusion", $"{affix.Id} -> {excluded}", affix);
    }

    foreach (var table in all.OfType<ItemDropTableDefinition>())
    {
        if (table.MinItemLevel > table.MaxItemLevel)
            yield return Issue("invalid_drop_level_range", table.Id, table);
        foreach (var entry in table.Entries)
            if (entry.Item == null || !ids.Contains(entry.Item.Id))
                yield return Issue("invalid_drop_item", table.Id, table);
    }
}
```

`ContentCatalog` 增加 `#if UNITY_EDITOR public void EditorSetDefinitions(IEnumerable<ContentDefinition> values)`，仅编辑器 builder 使用。

- [ ] **Step 6: 运行测试并提交**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: EditMode 与 PlayMode 全部通过。

```powershell
git add BorderValley/Assets/Code/Core/Combat BorderValley/Assets/Code/Core/BattleFlow/Battle*Snapshot.cs `
  BorderValley/Assets/Code/Data/Items BorderValley/Assets/Code/Data/ContentCatalog.cs `
  BorderValley/Assets/Code/Data/ContentValidator.cs `
  BorderValley/Assets/Tests/EditMode/Inventory/ContentDefinitionTests.cs
git add BorderValley/Assets/Code/Core/Combat.meta BorderValley/Assets/Code/Data/Items.meta `
  BorderValley/Assets/Tests/EditMode/Inventory.meta
git commit -m "feat(inventory): define equipment and battle snapshot model"
```

---

### Task 2: 实现背包、装备、钱包、查询和原子存档参与

**Files:**
- Create: `BorderValley/Assets/Code/Core/Boot/IGameServiceInstaller.cs`
- Modify: `BorderValley/Assets/Code/Core/Boot/GameBootstrapper.cs`
- Create: `BorderValley/Assets/Code/Inventory/Items/AffixInstance.cs`
- Create: `BorderValley/Assets/Code/Inventory/Items/ItemInstance.cs`
- Create: `BorderValley/Assets/Code/Inventory/Items/InventoryFilter.cs`
- Create: `BorderValley/Assets/Code/Inventory/Items/InventorySort.cs`
- Create: `BorderValley/Assets/Code/Inventory/InventoryQuery.cs`
- Create: `BorderValley/Assets/Code/Inventory/InventoryService.cs`
- Create: `BorderValley/Assets/Code/Inventory/InventoryBootstrapInstaller.cs`
- Create: `BorderValley/Assets/Code/UI/Inventory/InventoryTextKeys.cs`
- Create: `BorderValley/Assets/Tests/EditMode/Inventory/InventoryServiceTests.cs`

**Interfaces:**
- Consumes: `ItemDefinition`、`ItemInstance`、`ISaveParticipant`、`GameContext`、`ContentCatalog`。
- Produces: `InventoryService`、`IGameServiceInstaller`、`InventoryFilter`、`InventorySort`。其他 task 只通过 `InventoryService` 修改库存和钱包。

- [ ] **Step 1: 写失败测试**

```csharp
using System.Linq;
using BorderValley.Core.Persistence;
using BorderValley.Data.Items;
using NUnit.Framework;
using UnityEngine;

namespace BorderValley.Inventory.Tests
{
    public sealed class InventoryServiceTests
    {
        [Test]
        public void TryEquip_ReplacesExistingItemAndReturnsItToBag()
        {
            var definitions = Definitions();
            var service = new InventoryService(2, definitions, 100);
            var sword = Item("i1", "item.sword", ItemRarity.Common);
            var axe = Item("i2", "item.axe", ItemRarity.Common);
            Assert.That(service.TryAdd(sword, out _), Is.True);
            Assert.That(service.TryAdd(axe, out _), Is.True);

            Assert.That(service.TryEquip("i1", "class.warrior", out _), Is.True);
            Assert.That(service.TryEquip("i2", "class.warrior", out _), Is.True);

            Assert.That(service.Equipped[ItemSlot.Weapon], Is.EqualTo("i2"));
            Assert.That(service.Items.Any(item => item.InstanceId == "i1"), Is.True);
        }

        [Test]
        public void SaveParticipant_RestoresItemsEquipmentGoldAndMaterials()
        {
            var root = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "BorderValleyTests", System.Guid.NewGuid().ToString("N"));
            var service = new InventoryService(3, Definitions(), 25);
            service.TryAdd(Item("i1", "item.sword", ItemRarity.Fine), out _);
            service.TryEquip("i1", "class.warrior", out _);
            service.TryAddMaterial("material.ore", 4);
            var save = new SaveService(root, new ISaveParticipant[] { service });
            save.Save(0, "World");

            var restored = new InventoryService(3, Definitions(), 0);
            new SaveService(root, new ISaveParticipant[] { restored }).Load(0);

            Assert.That(restored.Gold, Is.EqualTo(25));
            Assert.That(restored.Items.Single().InstanceId, Is.EqualTo("i1"));
            Assert.That(restored.Equipped[ItemSlot.Weapon], Is.EqualTo("i1"));
            Assert.That(restored.Materials["material.ore"], Is.EqualTo(4));
            System.IO.Directory.Delete(root, true);
        }

        private static System.Collections.Generic.Dictionary<string, ItemDefinition> Definitions()
        {
            var sword = ScriptableObject.CreateInstance<ItemDefinition>();
            sword.EditorConfigure("item.sword", "item.sword.name", ItemSlot.Weapon,
                new[] { "class.warrior" }, false, 20, System.Array.Empty<StatValue>());
            var axe = ScriptableObject.CreateInstance<ItemDefinition>();
            axe.EditorConfigure("item.axe", "item.axe.name", ItemSlot.Weapon,
                new[] { "class.warrior" }, false, 20, System.Array.Empty<StatValue>());
            return new System.Collections.Generic.Dictionary<string, ItemDefinition>
            {
                [sword.Id] = sword,
                [axe.Id] = axe
            };
        }

        private static ItemInstance Item(string id, string definitionId, ItemRarity rarity) =>
            new(id, definitionId, 1, rarity, System.Array.Empty<AffixInstance>());
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: 编译失败，`InventoryService` 尚不存在。

- [ ] **Step 3: 实现运行时实例和查询模式**

```csharp
namespace BorderValley.Inventory
{
    public sealed class AffixInstance
    {
        public AffixInstance(string affixId, int value)
        {
            AffixId = string.IsNullOrWhiteSpace(affixId)
                ? throw new System.ArgumentException(nameof(affixId))
                : affixId;
            Value = value;
        }

        public string AffixId { get; }
        public int Value { get; }
    }

    public sealed class ItemInstance
    {
        public ItemInstance(
            string instanceId,
            string itemDefinitionId,
            int itemLevel,
            Data.Items.ItemRarity rarity,
            System.Collections.Generic.IEnumerable<AffixInstance> affixes)
        {
            InstanceId = Require(instanceId, nameof(instanceId));
            ItemDefinitionId = Require(itemDefinitionId, nameof(itemDefinitionId));
            ItemLevel = System.Math.Clamp(itemLevel, 1, 10);
            Rarity = rarity;
            Affixes = Copy(affixes);
        }

        public string InstanceId { get; }
        public string ItemDefinitionId { get; }
        public int ItemLevel { get; }
        public Data.Items.ItemRarity Rarity { get; }
        public System.Collections.Generic.IReadOnlyList<AffixInstance> Affixes { get; private set; }

        public void ReplaceAffixes(System.Collections.Generic.IEnumerable<AffixInstance> values) =>
            Affixes = Copy(values);

        private static string Require(string value, string name) =>
            string.IsNullOrWhiteSpace(value) ? throw new System.ArgumentException(name) : value;

        private static System.Collections.Generic.IReadOnlyList<AffixInstance> Copy(
            System.Collections.Generic.IEnumerable<AffixInstance> values)
        {
            var result = new System.Collections.Generic.List<AffixInstance>(values ?? System.Array.Empty<AffixInstance>());
            if (result.Exists(value => value == null)) throw new System.ArgumentException("Affixes cannot contain null.");
            return result.AsReadOnly();
        }
    }

    public sealed class InventoryFilter
    {
        public Data.Items.ItemSlot? Slot { get; init; }
        public Data.Items.ItemRarity? Rarity { get; init; }
        public bool? Equipped { get; init; }
        public Data.Items.AffixEffectKind? AffixKind { get; init; }
    }

    public enum InventorySort
    {
        SlotThenRarity,
        RarityThenItemLevel,
        ItemLevelThenName,
        ValueThenName
    }
}
```

`InventoryQuery.Apply` 按 `ItemDefinition.Slot`、品质、装备状态和词条种类过滤，再按枚举排序；空结果返回 `Array.Empty<ItemInstance>()`，不修改源集合。

- [ ] **Step 4: 实现 InventoryService**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Core.Persistence;
using BorderValley.Data.Items;
using Newtonsoft.Json.Linq;

namespace BorderValley.Inventory
{
    public sealed class InventoryService : ISaveParticipant
    {
        private readonly Dictionary<string, ItemInstance> items = new(StringComparer.Ordinal);
        private readonly Dictionary<ItemSlot, string> equipped = new();
        private readonly Dictionary<string, int> materials = new(StringComparer.Ordinal);
        private readonly IReadOnlyDictionary<string, ItemDefinition> definitions;

        public InventoryService(
            int capacity,
            IReadOnlyDictionary<string, ItemDefinition> definitions,
            int startingGold)
        {
            Capacity = Math.Max(1, capacity);
            this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            Gold = Math.Max(0, startingGold);
        }

        public event Action Changed;
        public string Key => "inventory";
        public int Capacity { get; }
        public int Gold { get; private set; }
        public IReadOnlyDictionary<ItemSlot, string> Equipped => equipped;
        public IReadOnlyDictionary<string, int> Materials => materials;
        public IReadOnlyList<ItemInstance> Items => items.Values.OrderBy(item => item.InstanceId, StringComparer.Ordinal).ToArray();

        public ItemInstance GetItem(string instanceId) =>
            items.TryGetValue(instanceId, out var item)
                ? item
                : throw new KeyNotFoundException($"Unknown item instance: {instanceId}");

        public bool IsEquipped(string instanceId) => equipped.Values.Contains(instanceId, StringComparer.Ordinal);

        public bool TryAdd(ItemInstance item, out string error)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (items.Count >= Capacity) { error = InventoryTextKeys.BagFull; return false; }
            if (items.ContainsKey(item.InstanceId)) { error = InventoryTextKeys.DuplicateInstance; return false; }
            if (!definitions.ContainsKey(item.ItemDefinitionId)) { error = InventoryTextKeys.UnknownDefinition; return false; }
            items.Add(item.InstanceId, item);
            error = string.Empty;
            Changed?.Invoke();
            return true;
        }

        public bool TryEquip(string instanceId, string classId, out string error)
        {
            if (!items.TryGetValue(instanceId, out var item)) { error = InventoryTextKeys.ItemMissing; return false; }
            var definition = definitions[item.ItemDefinitionId];
            if (!definition.AllowsClass(classId)) { error = InventoryTextKeys.ClassRestricted; return false; }
            if (equipped.TryGetValue(definition.Slot, out var replaced))
                equipped.Remove(definition.Slot);
            equipped[definition.Slot] = instanceId;
            error = string.Empty;
            Changed?.Invoke();
            return true;
        }

        public bool TryUnequip(ItemSlot slot, out string error)
        {
            if (equipped.Remove(slot)) { error = string.Empty; Changed?.Invoke(); return true; }
            error = InventoryTextKeys.SlotEmpty;
            return false;
        }

        public bool TryRemove(string instanceId)
        {
            if (IsEquipped(instanceId) || !items.Remove(instanceId)) return false;
            Changed?.Invoke();
            return true;
        }

        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || Gold < amount) return false;
            Gold -= amount;
            Changed?.Invoke();
            return true;
        }

        public void AddGold(int amount)
        {
            Gold += Math.Max(0, amount);
            Changed?.Invoke();
        }

        public bool TrySpendMaterial(string materialId, int amount)
        {
            if (amount < 0 || !materials.TryGetValue(materialId, out var current) || current < amount) return false;
            materials[materialId] = current - amount;
            Changed?.Invoke();
            return true;
        }

        public void AddMaterial(string materialId, int amount)
        {
            if (amount <= 0) return;
            materials[materialId] = materials.GetValueOrDefault(materialId) + amount;
            Changed?.Invoke();
        }

        public JObject Capture() => new()
        {
            ["gold"] = Gold,
            ["equipped"] = JObject.FromObject(equipped),
            ["materials"] = JObject.FromObject(materials),
            ["items"] = new JArray(items.Values.OrderBy(value => value.InstanceId, StringComparer.Ordinal).Select(ToJson))
        };

        public void Restore(JObject state)
        {
            Reset();
            Gold = Math.Max(0, state.Value<int>("gold"));
            foreach (var pair in state["materials"]?.ToObject<Dictionary<string, int>>() ?? new())
                materials[pair.Key] = Math.Max(0, pair.Value);
            foreach (var token in state["items"] as JArray ?? new JArray())
            {
                var item = FromJson((JObject)token);
                if (definitions.ContainsKey(item.ItemDefinitionId)) items[item.InstanceId] = item;
            }
            foreach (var pair in state["equipped"]?.ToObject<Dictionary<ItemSlot, string>>() ?? new())
                if (items.ContainsKey(pair.Value)) equipped[pair.Key] = pair.Value;
            Changed?.Invoke();
        }

        public void RestoreContext(string sceneName) { }
        public void Reset() { items.Clear(); equipped.Clear(); materials.Clear(); Gold = 0; Changed?.Invoke(); }

        private static JObject ToJson(ItemInstance item) => new()
        {
            ["instanceId"] = item.InstanceId,
            ["definitionId"] = item.ItemDefinitionId,
            ["itemLevel"] = item.ItemLevel,
            ["rarity"] = item.Rarity.ToString(),
            ["affixes"] = new JArray(item.Affixes.Select(affix => new JObject
            {
                ["id"] = affix.AffixId,
                ["value"] = affix.Value
            }))
        };

        private static ItemInstance FromJson(JObject value) => new(
            value.Value<string>("instanceId"),
            value.Value<string>("definitionId"),
            value.Value<int>("itemLevel"),
            Enum.Parse<ItemRarity>(value.Value<string>("rarity")),
            (value["affixes"] as JArray ?? new JArray()).Select(affix => new AffixInstance(
                affix.Value<string>("id"), affix.Value<int>("value"))));
    }
}
```

`InventoryTextKeys` 由 Task 7 创建；本 task 可先创建只含错误键的静态类，Task 7 再扩展 UI 键。

- [ ] **Step 5: 安装服务并接入 SaveService**

```csharp
namespace BorderValley.Core.Boot
{
    public interface IGameServiceInstaller
    {
        void Install(GameContext context, System.Collections.Generic.ICollection<ISaveParticipant> participants);
    }
}
```

`GameBootstrapper.Awake` 先注册 `ISceneLoader`、`IBattleFlow`，再遍历 serialized `MonoBehaviour[] serviceInstallers` 并调用 `IGameServiceInstaller.Install`，最后注册 `SaveService(Application.persistentDataPath, participants)`。`InventoryBootstrapInstaller` 从 `Resources.Load<ContentCatalog>("ContentCatalog")` 建立 item/character 字典，注册 `InventoryService(30, items, 100)` 并加入 participants。

- [ ] **Step 6: 运行测试并提交**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: EditMode 与 PlayMode 全部通过，存档往返测试通过。

```powershell
git add BorderValley/Assets/Code/Core/Boot BorderValley/Assets/Code/Inventory `
  BorderValley/Assets/Tests/EditMode/Inventory/InventoryServiceTests.cs
git commit -m "feat(inventory): persist bag equipment wallet and materials"
```

---

### Task 3: 实现全队成长、技能点和战斗快照

**Files:**
- Create: `BorderValley/Assets/Code/Inventory/Progression/PartyMemberState.cs`
- Create: `BorderValley/Assets/Code/Inventory/Progression/PartyProgressionService.cs`
- Create: `BorderValley/Assets/Code/Inventory/Progression/EquipmentStatAggregator.cs`
- Create: `BorderValley/Assets/Code/Inventory/Progression/PartyBattleSnapshotBuilder.cs`
- Modify: `BorderValley/Assets/Code/Inventory/InventoryBootstrapInstaller.cs`
- Modify: `BorderValley/Assets/Code/Core/BattleFlow/BattleRequest.cs`
- Modify: `BorderValley/Assets/Code/Core/BattleFlow/BattleResult.cs`
- Create: `BorderValley/Assets/Tests/EditMode/Inventory/PartyProgressionServiceTests.cs`
- Create: `BorderValley/Assets/Tests/EditMode/Inventory/PartyBattleSnapshotBuilderTests.cs`

**Interfaces:**
- Consumes: `CharacterDefinition`、`InventoryService`、`ItemDefinition`、`AffixDefinition`、`BattlePartySnapshot`。
- Produces: `PartyProgressionService`、`PartyBattleSnapshotBuilder.Build(BattleRequest)`、带 `PartySnapshot` 的 `BattleRequest`、带 `UnitStates` 的 `BattleResult`。

- [ ] **Step 1: 写失败测试**

```csharp
[Test]
public void AwardExperience_LevelsEveryMemberAndGrantsSkillPoint()
{
    var progression = Progression();
    progression.AwardExperience(120);
    Assert.That(progression.Members.All(member => member.Level == 2), Is.True);
    Assert.That(progression.Members.All(member => member.SkillPoints == 1), Is.True);
}

[Test]
public void Build_AppliesEquipmentAndSkillModifier()
{
    var inventory = InventoryWithSwordAndBoots();
    var progression = Progression();
    var builder = new PartyBattleSnapshotBuilder(progression, inventory, Items, Affixes);
    var snapshot = builder.Build(new BattleRequest("core", "seed", "World")).PartySnapshot;
    var warrior = snapshot.Members.Single(member => member.ClassId == "class.warrior");
    Assert.That(warrior.Power, Is.GreaterThan(9));
    Assert.That(warrior.SkillModifiers.Single().Kind, Is.EqualTo(SkillModifierKind.Radius));
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: `PartyProgressionService` 和 `PartyBattleSnapshotBuilder` 尚未定义。

- [ ] **Step 3: 实现成长状态和服务**

```csharp
public sealed class PartyMemberState
{
    public PartyMemberState(string memberId, string characterId, int level, int experience,
        int skillPoints, int currentHealth, int currentMana)
    {
        MemberId = memberId;
        CharacterId = characterId;
        Level = Math.Clamp(level, 1, 10);
        Experience = Math.Max(0, experience);
        SkillPoints = Math.Max(0, skillPoints);
        CurrentHealth = Math.Max(1, currentHealth);
        CurrentMana = Math.Max(0, currentMana);
    }

    public string MemberId { get; }
    public string CharacterId { get; }
    public int Level { get; internal set; }
    public int Experience { get; internal set; }
    public int SkillPoints { get; internal set; }
    public int CurrentHealth { get; internal set; }
    public int CurrentMana { get; internal set; }
    public Dictionary<string, int> SkillRanks { get; } = new(StringComparer.Ordinal);
}
```

`PartyProgressionService` 构造参数为 `IReadOnlyDictionary<string, CharacterDefinition>` 和 `IEnumerable<PartyMemberState>`。升级经验需求为 `100 * currentLevel`；等级上限 10；升到 4、6、8、10 级各获得 1 个技能点，2 级获得 1 个技能点。`AwardExperience(int amount)` 把同一经验池加到所有未满级成员。`TrySpendSkillPoint(memberId, skillId, out error)` 只允许 `CharacterDefinition` 已声明且等级满足的 skill，每项最高 3 级。

`ApplyBattleUnitStates(IEnumerable<BattleUnitResult>)` 更新当前生命和法力；全队生命为 1 代表倒下，设置 `HasPendingWipeReturn=true`，由 World 返回 `SafePointId = "world.village"`。

- [ ] **Step 4: 扩展 BattleRequest 和 BattleResult**

```csharp
public BattleRequest(
    string scenarioId,
    string seed,
    string returnScene,
    BattlePartySnapshot partySnapshot = null)
{
    ScenarioId = Require(scenarioId, nameof(scenarioId));
    Seed = Require(seed, nameof(seed));
    ReturnScene = Require(returnScene, nameof(returnScene));
    PartySnapshot = partySnapshot;
}

public BattlePartySnapshot PartySnapshot { get; }
```

`BattleUnitResult` 保存 `UnitId`、`Health`、`Mana`。`BattleResult` 增加可选 `IReadOnlyList<BattleUnitResult> UnitStates`，旧三参构造仍可用并传入空数组。

- [ ] **Step 5: 实现装备聚合和快照组装**

`EquipmentStatAggregator` 遍历 `InventoryService.Equipped`，对每件装备叠加基础值和词条：

- `FlatStat` 直接累加。
- `PercentStat` 按 `value / 10000f` 应用到基础值，最终向下取整且最小为 0。
- `SkillModifier` 转为 `BattleSkillModifierSnapshot`。
- `Trigger` 和 `Conditional` 转为 `BattlePassiveSnapshot`，magnitude 使用词条值、duration 使用词条 duration。
- 同一 skill modifier 累加 value；`PowerMultiplierBps` 直接累加。

`PartyBattleSnapshotBuilder.Build` 依次执行：读取成长状态、读取角色基础值与每级成长、叠加装备、拼接已解锁技能、加入技能等级修正 `range + (rank - 1)`，最后输出不可变 `BattleRequest`。`PercentStat` 的 10000 点代表 100%。

- [ ] **Step 6: 写入 SaveParticipant 并注册**

`PartyProgressionService.Key => "party"`。Capture 保存每个成员的等级、经验、技能点、当前生命/法力、技能等级以及 `SafePointId`。Restore 遇到未知 character 或非法等级时跳过该成员，而不是覆盖已有有效状态。`InventoryBootstrapInstaller` 同时注册 `PartyProgressionService` 和 `PartyBattleSnapshotBuilder`，并把 progression 加入 participants。

- [ ] **Step 7: 运行测试并提交**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: 成长、技能点、装备数值聚合和快照测试全部通过。

```powershell
git add BorderValley/Assets/Code/Core/BattleFlow/BattleRequest.cs `
  BorderValley/Assets/Code/Core/BattleFlow/BattleResult.cs `
  BorderValley/Assets/Code/Inventory/Progression `
  BorderValley/Assets/Code/Inventory/InventoryBootstrapInstaller.cs `
  BorderValley/Assets/Tests/EditMode/Inventory/PartyProgressionServiceTests.cs `
  BorderValley/Assets/Tests/EditMode/Inventory/PartyBattleSnapshotBuilderTests.cs
git commit -m "feat(progression): add party growth and battle snapshots"
```

---

### Task 4: 实现可复现掉落、品质权重、词条去重和品质预算

**Files:**
- Create: `BorderValley/Assets/Code/Inventory/Loot/LootGenerator.cs`
- Create: `BorderValley/Assets/Tests/EditMode/Inventory/LootGeneratorTests.cs`

**Interfaces:**
- Consumes: `ItemDropTableDefinition`、`ItemDefinition`、`AffixDefinition`、`IRandomSource`。
- Produces: `LootGenerator.Generate(string instanceId, ItemDropTableDefinition table, int playerLevel, IRandomSource random)` 和 `GenerateForItem(...)`。

- [ ] **Step 1: 写失败测试**

```csharp
[Test]
public void Generate_WithSameSeedAndInstanceId_IsReproducible()
{
    var generator = Generator();
    var first = generator.Generate("drop.1", Table(), 5, RandomSourceFactory.FromSeed("seed"));
    var second = generator.Generate("drop.1", Table(), 5, RandomSourceFactory.FromSeed("seed"));

    Assert.That(second.ItemDefinitionId, Is.EqualTo(first.ItemDefinitionId));
    Assert.That(second.Rarity, Is.EqualTo(first.Rarity));
    Assert.That(second.ItemLevel, Is.EqualTo(first.ItemLevel));
    CollectionAssert.AreEqual(
        first.Affixes.Select(value => (value.AffixId, value.Value)),
        second.Affixes.Select(value => (value.AffixId, value.Value)));
}

[Test]
public void Generate_EpicAlwaysContainsSkillOrTriggerAffix()
{
    for (var seed = 0; seed < 100; seed++)
    {
        var item = Generator(new[] { Item(ItemRarity.Common), Item(ItemRarity.Epic) })
            .Generate($"drop.{seed}", EpicTable(), 10, RandomSourceFactory.FromSeed($"seed.{seed}"));
        if (item.Rarity != ItemRarity.Epic) continue;
        Assert.That(item.Affixes.Any(affix =>
            affix.AffixId == "affix.skill.radius" || affix.AffixId == "affix.trigger.slow"), Is.True);
    }
}

[Test]
public void Generate_NeverRepeatsOrSelectsMutuallyExclusiveAffixes()
{
    var item = Generator().Generate("drop", Table(), 10, RandomSourceFactory.FromSeed("seed"));
    Assert.That(item.Affixes.Select(affix => affix.AffixId).Distinct().Count(), Is.EqualTo(item.Affixes.Count));
    Assert.That(item.Affixes.Any(affix => affix.AffixId == "affix.speed") &&
                item.Affixes.Any(affix => affix.AffixId == "affix.armor")), Is.False);
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: `LootGenerator` 尚未定义。

- [ ] **Step 3: 实现生成流程**

```csharp
public sealed class LootGenerator
{
    private readonly IReadOnlyDictionary<string, ItemDefinition> items;
    private readonly IReadOnlyDictionary<string, AffixDefinition> affixes;

    public LootGenerator(
        IReadOnlyDictionary<string, ItemDefinition> items,
        IReadOnlyDictionary<string, AffixDefinition> affixes)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.affixes = affixes ?? throw new ArgumentNullException(nameof(affixes));
    }

    public ItemInstance Generate(
        string instanceId,
        ItemDropTableDefinition table,
        int playerLevel,
        IRandomSource random)
    {
        if (table == null) throw new ArgumentNullException(nameof(table));
        if (random == null) throw new ArgumentNullException(nameof(random));
        var local = random.Fork("loot:" + instanceId + ":" + table.Id);

        var item = Weighted(table.Entries, entry => entry.Weight, entry => entry.Item, local);
        var itemLevel = Math.Clamp(
            playerLevel + local.Range(table.MinLevelOffset, table.MaxLevelOffset + 1),
            table.MinItemLevel,
            table.MaxItemLevel);
        var rarity = RollRarity(table, local);
        return GenerateForItem(instanceId, item, itemLevel, rarity, local.Fork("affixes"));
    }

    public ItemInstance GenerateForItem(
        string instanceId,
        ItemDefinition item,
        int itemLevel,
        ItemRarity rarity,
        IRandomSource random)
    {
        var count = rarity switch
        {
            ItemRarity.Common => 0,
            ItemRarity.Fine => 1,
            ItemRarity.Rare => 2,
            ItemRarity.Epic => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity))
        };

        var chosen = new List<AffixDefinition>();
        var eligible = affixes.Values
            .Where(affix => affix.Supports(item.Slot, rarity, itemLevel))
            .OrderBy(affix => affix.Id, StringComparer.Ordinal)
            .ToArray();

        for (var index = 0; index < count; index++)
        {
            var candidates = eligible.Where(candidate =>
                chosen.All(value => value.Id != candidate.Id) &&
                chosen.All(value => !value.IsMutuallyExclusive(candidate.Id))).ToArray();
            if (candidates.Length == 0) break;
            chosen.Add(Weighted(candidates, value => value.Weight, value => value, random));
        }

        if (rarity == ItemRarity.Epic &&
            !chosen.Any(value => value.EffectKind is AffixEffectKind.SkillModifier or AffixEffectKind.Trigger))
        {
            var special = eligible.Where(value =>
                    value.EffectKind is AffixEffectKind.SkillModifier or AffixEffectKind.Trigger)
                .Where(value => chosen.All(existing => existing.Id != value.Id && !existing.IsMutuallyExclusive(value.Id)))
                .OrderBy(value => value.Id, StringComparer.Ordinal)
                .ToArray();
            if (special.Length > 0)
                chosen[chosen.Count - 1] = Weighted(special, value => value.Weight, value => value, random);
        }

        var affixInstances = ApplyBudget(chosen, rarity, random);
        return new ItemInstance(instanceId, item.Id, itemLevel, rarity, affixInstances);
    }
}
```

- [ ] **Step 4: 实现品质预算和数值范围**

权重抽取和预算收缩必须使用确定性的整数运算：

```csharp
private static TResult Weighted<TSource, TResult>(
    IEnumerable<TSource> values,
    Func<TSource, int> weight,
    Func<TSource, TResult> select,
    IRandomSource random)
{
    var rows = values.Select(value => new
        {
            Value = value,
            Key = select(value),
            Weight = weight(value)
        })
        .Where(row => row.Weight > 0)
        .ToArray();
    var total = rows.Sum(row => row.Weight);
    if (total <= 0) throw new InvalidOperationException("Weighted table has no positive entries.");
    var roll = random.Range(0, total);
    foreach (var row in rows)
    {
        if (roll < row.Weight) return row.Key;
        roll -= row.Weight;
    }
    return rows[rows.Length - 1].Key;
}

private static IReadOnlyList<AffixInstance> ApplyBudget(
    IReadOnlyList<AffixDefinition> chosen,
    ItemRarity rarity,
    IRandomSource random)
{
    var remaining = rarity switch
    {
        ItemRarity.Common => 0,
        ItemRarity.Fine => 12,
        ItemRarity.Rare => 24,
        ItemRarity.Epic => 40,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity))
    };
    var result = new List<AffixInstance>();
    foreach (var affix in chosen)
    {
        var value = random.Range(affix.MinValue, affix.MaxValue + 1);
        while (value > affix.MinValue && Math.Abs(value) * affix.BudgetCost > remaining)
            value += value > 0 ? -1 : 1;
        var cost = Math.Max(1, Math.Abs(value)) * affix.BudgetCost;
        if (cost > remaining)
            throw new InvalidOperationException($"Affix budget exceeded by {affix.Id}.");
        remaining -= cost;
        result.Add(new AffixInstance(affix.Id, value));
    }
    return result.AsReadOnly();
}
```

品质预算固定为 `Common=0`、`Fine=12`、`Rare=24`、`Epic=40`。每个词条的 `BudgetCost = Math.Max(1, Math.Abs(value))`；按选中顺序生成 `random.Range(minValue, maxValue + 1)`，若超预算则逐步向 `minValue` 收缩，仍超预算时按 item level 与 rarity 抛 `InvalidOperationException("Affix budget exceeded...")`。这样配置错误会在开发构建中暴露，而不是静默绕过预算。

技能修正和触发词条的合法值区间由 `AffixDefinition` 自身给出；掉落生成器不得硬编码 skill ID。品质权重总和为 0 时抛 `InvalidOperationException`。

- [ ] **Step 5: 运行测试并提交**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: 同种子复现、品质数量、史诗特殊词条、去重和互斥测试全部通过。

```powershell
git add BorderValley/Assets/Code/Inventory/Loot `
  BorderValley/Assets/Tests/EditMode/Inventory/LootGeneratorTests.cs
git commit -m "feat(loot): generate seeded items affixes and rarity budgets"
```

---

### Task 5: 实现统一经济、打造、分解、锁定重铸和价格限制

**Files:**
- Create: `BorderValley/Assets/Code/Inventory/Economy/EconomyService.cs`
- Create: `BorderValley/Assets/Code/Inventory/Economy/CraftingCosts.cs`
- Create: `BorderValley/Assets/Code/Inventory/Economy/CraftingService.cs`
- Modify: `BorderValley/Assets/Code/Inventory/InventoryBootstrapInstaller.cs`
- Create: `BorderValley/Assets/Tests/EditMode/Inventory/EconomyAndCraftingTests.cs`

**Interfaces:**
- Consumes: `InventoryService`、`LootGenerator`、`ItemDefinition`、`AffixDefinition`、`IRandomSource`。
- Produces: `EconomyService.GetBuyPrice/GetSellPrice/TryBuy/TrySell`、`CraftingService.Craft/Dismantle/Reforge`。

- [ ] **Step 1: 写失败测试**

```csharp
[Test]
public void Sell_RejectsEquippedAndQuestItems()
{
    var inventory = InventoryWithItem("i1", "item.sword");
    inventory.TryEquip("i1", "class.warrior", out _);
    Assert.That(new EconomyService(inventory, Items).TrySell("i1", out var error), Is.False);
    Assert.That(error, Is.EqualTo(InventoryTextKeys.EquippedCannotSell));
}

[Test]
public void Reforge_WithOneLockedAffix_PreservesLockedAffixAndRerollsOthers()
{
    var inventory = InventoryWithItem("i1", "item.sword", ItemRarity.Rare,
        new AffixInstance("affix.power", 4), new AffixInstance("affix.armor", 3));
    var crafting = CraftingService(inventory, gold: 1000);
    var result = crafting.Reforge("i1", "affix.power", RandomSourceFactory.FromSeed("reforge"));

    Assert.That(result.Success, Is.True);
    Assert.That(inventory.GetItem("i1").Affixes.Any(value => value.AffixId == "affix.power"), Is.True);
    Assert.That(result.GoldCost, Is.GreaterThan(0));
}

[Test]
public void Craft_WithInsufficientMaterials_DoesNotSpendGold()
{
    var inventory = new InventoryService(10, Items, 100);
    var crafting = new CraftingService(inventory, Items, Affixes, Generator(), new CraftingCosts());
    Assert.That(crafting.Craft("craft.1", Items["item.sword"], "class.warrior", 3,
        RandomSourceFactory.FromSeed("craft")).Success, Is.False);
    Assert.That(inventory.Gold, Is.EqualTo(100));
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: `EconomyService` 和 `CraftingService` 尚未定义。

- [ ] **Step 3: 实现 EconomyService**

```csharp
public sealed class EconomyService
{
    public int GetBuyPrice(ItemInstance item)
    {
        var definition = definitions[item.ItemDefinitionId];
        var rarityMultiplier = item.Rarity switch
        {
            ItemRarity.Common => 1,
            ItemRarity.Fine => 2,
            ItemRarity.Rare => 4,
            ItemRarity.Epic => 8,
            _ => 1
        };
        var affixValue = item.Affixes.Sum(affix => Math.Abs(affix.Value) * 2);
        return Math.Max(1, definition.BaseValue * item.ItemLevel * rarityMultiplier / 10 + affixValue);
    }

    public int GetSellPrice(ItemInstance item) => Math.Max(1, GetBuyPrice(item) * 2 / 5);

    public bool TrySell(string instanceId, out string error)
    {
        var item = inventory.GetItem(instanceId);
        var definition = definitions[item.ItemDefinitionId];
        if (inventory.IsEquipped(instanceId)) { error = InventoryTextKeys.EquippedCannotSell; return false; }
        if (definition.IsQuestItem) { error = InventoryTextKeys.QuestCannotSell; return false; }
        var price = GetSellPrice(item);
        inventory.TryRemove(instanceId);
        inventory.AddGold(price);
        error = string.Empty;
        return true;
    }

    public bool TryBuy(ItemInstance item, out string error)
    {
        var price = GetBuyPrice(item);
        if (!inventory.TrySpendGold(price)) { error = InventoryTextKeys.NotEnoughGold; return false; }
        if (!inventory.TryAdd(item, out error))
        {
            inventory.AddGold(price);
            return false;
        }
        error = string.Empty;
        return true;
    }
}
```

买入价必须高于卖出价；测试覆盖两者比值和 `baseValue + itemLevel + rarity + affix` 单调性。

- [ ] **Step 4: 实现 CraftingCosts 和 CraftingService**

```csharp
public sealed class CraftingCosts
{
    public int CraftGold(int itemLevel, ItemRarity rarity) => 40 + itemLevel * 5 + (int)rarity * 20;
    public int CraftMaterial(int itemLevel, ItemRarity rarity) => 2 + itemLevel / 3 + (int)rarity;
    public int DismantleMaterial(ItemInstance item) => 1 + item.ItemLevel / 3 + (int)item.Rarity;
    public int ReforgeGold(ItemInstance item, bool locksAffix) =>
        30 + item.ItemLevel * 4 + (int)item.Rarity * 15 + (locksAffix ? 60 : 0);
    public int ReforgeMaterial(ItemInstance item, bool locksAffix) =>
        1 + (int)item.Rarity + (locksAffix ? 3 : 0);
}
```

`Craft` 必须先检查金币和 `material.ore`，再调用 `LootGenerator.GenerateForItem`；生成后可重新校验预算，失败时回滚金币/材料。`Dismantle` 不能分解已装备或任务物品，成功后按 `DismantleMaterial` 产出，稀有以上额外产出 `material.essence`。

`Reforge` 只能处理非装备物品；锁定 ID 必须存在于当前词条中，锁定后重铸其余词条。锁定额外成本在 `CraftingCosts` 中体现，锁定词条的 ID 和 value 原样保留，其余词条按 `LootGenerator` 的合法池重新生成；结果不超过原品质词条数量，并再次验证互斥和预算。

- [ ] **Step 5: 注册服务并提交**

`InventoryBootstrapInstaller` 注册 `LootGenerator`、`EconomyService`、`CraftingService` 和 `CraftingCosts`；这些服务不单独实现 `ISaveParticipant`，状态全部由 `InventoryService` 持有。

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: 经济价格、买卖限制、打造回滚、分解、锁定重铸全部通过。

```powershell
git add BorderValley/Assets/Code/Inventory/Economy `
  BorderValley/Assets/Code/Inventory/InventoryBootstrapInstaller.cs `
  BorderValley/Assets/Tests/EditMode/Inventory/EconomyAndCraftingTests.cs
git commit -m "feat(crafting): add economy crafting dismantle and reforge"
```

---

### Task 6: 让装备属性、技能修正和被动词条在战斗中真实生效

**Files:**
- Create: `BorderValley/Assets/Code/Battle/Domain/SkillModifierApplier.cs`
- Create: `BorderValley/Assets/Code/Battle/Domain/BattlePassiveRules.cs`
- Modify: `BorderValley/Assets/Code/Battle/Domain/BattleUnit.cs`
- Modify: `BorderValley/Assets/Code/Battle/Domain/DamageCalculator.cs`
- Modify: `BorderValley/Assets/Code/Battle/Domain/SkillExecutor.cs`
- Modify: `BorderValley/Assets/Code/Battle/Domain/BattleScenarioFactory.cs`
- Modify: `BorderValley/Assets/Code/UI/Battle/BattleSceneController.cs`
- Create: `BorderValley/Assets/Tests/EditMode/Inventory/BattleEquipmentIntegrationTests.cs`

**Interfaces:**
- Consumes: `BattlePartySnapshot`、`BattleCombatantSnapshot`、`BattleSkillModifierSnapshot`、`BattlePassiveSnapshot`、现有 `SkillDefinition` 与 `BattleEngine`。
- Produces: `BattleScenarioFactory.CreateCoreScenario(BattlePartySnapshot partySnapshot)`、`BattleUnit.Passives`、完整 `BattleResult.UnitStates`。

- [ ] **Step 1: 写失败测试**

```csharp
[Test]
public void CreateCoreScenario_AppliesEquipmentStatsAndPerUnitSkillModifier()
{
    var snapshot = Snapshot(
        power: 14,
        skillModifiers: new[]
        {
            new BattleSkillModifierSnapshot("skill.whirlwind", SkillModifierKind.Radius, 1)
        });
    var scenario = BattleScenarioFactory.CreateCoreScenario(
        new BattlePartySnapshot(new[] { snapshot }));
    var warrior = scenario.State.GetUnit("player.warrior");
    var skillId = scenario.GetSkillsForUnit("player.warrior").Keys
        .Single(id => id.StartsWith("skill.whirlwind", StringComparison.Ordinal));

    Assert.That(warrior.Stats.Power, Is.EqualTo(14));
    Assert.That(scenario.GetSkillsForUnit("player.warrior")[skillId].Radius, Is.EqualTo(2));
}

[Test]
public void LowHealthArmorPassive_ReducesPhysicalDamage()
{
    var attacker = Unit("a", 10, 0, passives: Array.Empty<BattlePassiveSnapshot>());
    var defender = Unit("d", 0, 2, new[]
    {
        new BattlePassiveSnapshot(PassiveEffectKind.LowHealthArmor, 5, 0)
    });
    defender.ApplyRawDamage(defender.Stats.MaxHealth - 1);
    var result = DamageCalculator.Calculate(
        new DamageRequest(attacker, defender, 1f, DamageType.Physical, false), Map(), Random());
    Assert.That(result.Damage, Is.EqualTo(3));
}

[Test]
public void OnAttackSlow_AddsSlowedStatusToDamagedTarget()
{
    var attacker = Unit("a", 0, 0, new[]
    {
        new BattlePassiveSnapshot(PassiveEffectKind.OnAttackApplySlow, 1, 2)
    });
    var defender = Unit("d", 0, 1, Array.Empty<BattlePassiveSnapshot>());
    var skill = new SkillDefinition("skill.hit", "skill.hit.name", SkillTargeting.Enemy, 1, 0, 0, 0,
        new SkillEffectDefinition(SkillEffectKind.Damage, 1f, default, 0, 0));
    SkillExecutor.Execute(new BattleState(Map(attacker, defender)), attacker, skill, defender, Random());
    Assert.That(defender.Statuses.Any(status => status.Type == StatusType.Slowed), Is.True);
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: `BattleScenarioFactory.CreateCoreScenario(BattlePartySnapshot)` 和 `BattleUnit.Passives` 尚未定义。

- [ ] **Step 3: 扩展 BattleUnit 和技能修正器**

`BattleUnit` 构造器增加可选 `IEnumerable<BattlePassiveSnapshot> passives = null`，公开只读 `Passives`。保留现有所有构造调用兼容。

`SkillModifierApplier.Apply(SkillDefinition skill, SkillModifierKind kind, int value)` 返回新 `SkillDefinition`：Range/Radius/ManaCost/Cooldown 使用 `Math.Max(0, original + value)`；`PowerMultiplierBps` 对每个 Damage effect 使用 `original.PowerMultiplier + value / 10000f`。不得修改传入对象。

- [ ] **Step 4: 从 BattlePartySnapshot 构建玩家单位和独立技能 ID**

当 `partySnapshot == null` 时保留当前核心玩家构造。提供 snapshot 时：

```csharp
var mappedSkills = CreateSkills();
foreach (var member in partySnapshot.Members)
{
    var unit = new BattleUnit(
        member.UnitId,
        member.DefinitionId,
        Team.Player,
        new UnitStats(member.MaxHealth, member.MaxMana, member.Power, member.Armor,
            member.Speed, member.CritChanceBps / 10000f, member.Resistance),
        PlayerPosition(member.ClassId),
        member.Passives);
    unit.SetCurrentResources(member.CurrentHealth, member.CurrentMana);
    state.AddUnit(unit);

    var unitSkillIds = new List<string>();
    foreach (var baseSkillId in member.SkillIds.Distinct(StringComparer.Ordinal))
    {
        var baseSkill = mappedSkills[baseSkillId];
        var modified = member.SkillModifiers
            .Where(modifier => modifier.SkillId == baseSkillId)
            .Aggregate(baseSkill, (current, modifier) =>
                SkillModifierApplier.Apply(current, modifier.Kind, modifier.Value));
        if (baseSkillId == "skill.basic" && ReferenceEquals(baseSkill, modified))
        {
            unitSkillIds.Add(baseSkillId);
            continue;
        }

        var unitSkillId = $"{baseSkillId}@{member.UnitId}";
        mappedSkills[unitSkillId] = Rename(modified, unitSkillId);
        unitSkillIds.Add(unitSkillId);
    }
    unitSkills[member.UnitId] = unitSkillIds.ToArray();
}
```

玩家位置固定为战士 `(2,2)`、游侠 `(1,2)`、法师 `(1,3)`；未知职业使用 `(2,3)`，加入前检查不重叠。敌方继续使用当前 core 敌人。`BattleUiPresenter` 的 basic skill 判断改为查找 skill ID 的 `StartsWith("skill.basic@", Ordinal)` 或保留一个未改名的 basic per unit，推荐后者：`skill.basic` 不映射唯一 ID，避免移动/普攻 UI 分支。

- [ ] **Step 5: 实现被动规则**

`BattlePassiveRules` 提供：

- `GetArmorBonus(BattleUnit unit)`：生命不高于 30% 时返回全部 `LowHealthArmor` magnitude。
- `GetDamageMultiplier(BattleUnit attacker, BattleMap map)`：高地且拥有 `HighGroundDamage` 时返回 `1 + magnitude / 100f`。
- `ApplyOnHit(BattleUnit attacker, BattleUnit defender, int damage)`：damage 大于 0 时给 defender 叠加 `Shielded`，magnitude 来自 `OnHitGainShield`。
- `ApplyOnAttack(BattleUnit attacker, BattleUnit defender)`：有 `OnAttackApplySlow` 时调用 `StatusSystem.Apply(defender, Slowed, magnitude, duration, attacker.Id)`。
- `ApplyOnKill(BattleUnit attacker, BattleUnit defender)`：defender 刚倒下时给 attacker 加 `OnKillHeal`。

伤害计算在防御值中加 `GetArmorBonus(defender)`，在高地 1.25 倍后乘 `GetDamageMultiplier(attacker, map)`，最后调用 `ApplyOnHit`。技能执行在成功造成伤害后调用 `ApplyOnAttack`，并在每个伤害目标处理完后调用 `ApplyOnKill`。

- [ ] **Step 6: 从 BattleSceneController 传递快照和结果**

`Start` 中若请求包含 `PartySnapshot`，调用 `BattleScenarioFactory.CreateCoreScenario(request.PartySnapshot)`；否则保持当前 `CreateCoreScenario()`。

`OnContinue` 构造 `BattleUnitResult`：

```csharp
var unitStates = presenter.Engine.State.Units
    .Where(unit => unit.Team == Team.Player)
    .Select(unit => new BattleUnitResult(unit.Id, unit.Health, unit.Mana))
    .ToArray();
flow.CompleteBattle(new BattleResult(MapOutcome(presenter.Outcome),
    presenter.Engine.State.Round, unitStates));
```

- [ ] **Step 7: 运行测试并提交**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: 装备属性、技能半径、低生命护甲、高地增伤、攻击减速、受击护盾、击杀回复和战斗结果回传测试全部通过。

```powershell
git add BorderValley/Assets/Code/Battle/Domain `
  BorderValley/Assets/Code/UI/Battle/BattleSceneController.cs `
  BorderValley/Assets/Tests/EditMode/Inventory/BattleEquipmentIntegrationTests.cs
git commit -m "feat(battle): apply equipment growth and affix passives"
```

---

### Task 7: 生成首版内容资产、实现筛选排序和触控装备/打造面板

**Files:**
- Create: `BorderValley/Assets/Editor/Tools/EquipmentContentBuilder.cs`
- Modify: `BorderValley/Assets/Editor/Tools/FoundationSceneBuilder.cs`
- Modify: `BorderValley/Assets/Code/UI/Inventory/InventoryTextKeys.cs`
- Create: `BorderValley/Assets/Code/UI/Inventory/InventoryUiPresenter.cs`
- Create: `BorderValley/Assets/Code/UI/Inventory/InventoryPanelView.cs`
- Modify: `BorderValley/Assets/Code/UI/Battle/WorldBattleEntryView.cs`
- Create: `BorderValley/Assets/Tests/EditMode/Inventory/InventoryUiPresenterTests.cs`

**Interfaces:**
- Consumes: `InventoryService`、`PartyProgressionService`、`CraftingService`、`EconomyService`、`InventoryQuery`。
- Produces: `InventoryUiPresenter`、`InventoryPanelView.Open/Close`、World 中的 Inventory/Craft/Rest/Battle 四个按钮。

- [ ] **Step 1: 写失败测试**

```csharp
[Test]
public void SetFilter_AndSort_UsesServiceDataWithoutMutatingInventory()
{
    var presenter = Presenter();
    presenter.SetFilter(new InventoryFilter { Rarity = ItemRarity.Rare });
    presenter.SetSort(InventorySort.ItemLevelThenName);
    Assert.That(presenter.VisibleItems.All(item => item.Rarity == ItemRarity.Rare), Is.True);
    Assert.That(Service.Items.Count, Is.EqualTo(3));
}

[Test]
public void EquipSelected_WhenClassRestricted_ExposesLocalizedErrorKey()
{
    var presenter = Presenter();
    presenter.SetSelectedForTests("mage.item");
    Assert.That(presenter.EquipSelected("class.warrior"), Is.False);
    Assert.That(presenter.LastErrorKey, Is.EqualTo(InventoryTextKeys.ClassRestricted));
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: `InventoryUiPresenter` 尚未定义。

- [ ] **Step 3: 实现可测试 presenter**

`InventoryUiPresenter` 公开：

```csharp
public event Action Changed;
public string SelectedInstanceId { get; }
public IReadOnlyList<ItemInstance> VisibleItems { get; }
public InventoryFilter Filter { get; }
public InventorySort Sort { get; }
public string LastErrorKey { get; }

public void SetFilter(InventoryFilter value);
public void SetSort(InventorySort value);
public void Select(string instanceId);
public bool EquipSelected(string classId);
public bool Unequip(ItemSlot slot);
public bool DismantleSelected();
public bool ReforgeSelected(string lockedAffixId, IRandomSource random);
public bool Craft(string instanceId, string itemDefinitionId, string classId, int itemLevel, IRandomSource random);
public void RestParty(int amount);
```

所有修改操作捕获服务返回的错误键并把成功/失败结果写入 `LastErrorKey`；成功时清空。`VisibleItems` 每次读取时调用 `InventoryQuery.Apply`，不复制也不重排服务内部集合。

- [ ] **Step 4: 实现 InventoryPanelView**

使用 uGUI 创建 1920×1080 横屏面板：

- 左侧固定六个装备槽，显示本地化键和实例 ID。
- 中部每页最多 12 个物品按钮，显示本地化键、品质、物品等级、已装备标记。
- 顶部显示 `inventory.gold`、`inventory.material.ore`、筛选和排序按钮；筛选循环 `All -> Weapon -> Armor -> Accessory -> Equipped`，排序循环四种 `InventorySort`。
- 右侧详情显示基础属性、词条范围、装备/卸下、分解、锁定词条重铸按钮。
- 铁匠区域显示打造、分解、重铸成本与材料不足状态。
- 所有触控按钮增加 `Button` 和 `Image.raycastTarget=true`；文本使用 `InventoryTextKeys`，不硬编码玩家文案。

`WorldBattleEntryView` 增加 Inventory、Craft、Rest、Battle 按钮。Inventory/Craft 按钮调用 `InventoryPanelView.Open(mode)`；Rest 调用 `RecoverOutOfCombat(10)` 并刷新生命标签。保留现有战斗结果展示。

- [ ] **Step 5: 创建首版内容资产**

`EquipmentContentBuilder.Build` 创建或更新以下 `.asset` 并加入 `ContentCatalog`：

- 基础物品：`item.frost_longsword`、`item.hunter_bow`、`item.oak_staff`、`item.iron_helmet`、`item.leather_armor`、`item.swift_boots`、`item.ember_charm`。
- 词条：`affix.flat_health`、`affix.flat_power`、`affix.flat_armor`、`affix.crit_bps`、`affix.skill.whirlwind_radius`、`affix.skill.fireball_mana`、`affix.trigger.slow`、`affix.conditional.low_health_armor`、`affix.conditional.high_ground_damage`。
- 掉落池：`loot.bandit.core`，包含全部七件基础装备和四档品质权重。
- 角色：`class.warrior`、`class.ranger`、`class.mage`，各自有固定成长、起始技能和等级解锁技能。

Builder 必须幂等：重复执行只更新存在的资产，不重复加入 catalog。`FoundationSceneBuilder.Rebuild` 增加 `EquipmentContentBuilder.Build()`，并在 BootServices 上挂 `InventoryBootstrapInstaller`，序列化到 `GameBootstrapper.serviceInstallers`。

- [ ] **Step 6: 运行 Unity builder 和测试**

Run:

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Unity.exe' -batchmode -nographics `
  -projectPath "$PWD\BorderValley" `
  -executeMethod BorderValley.Editor.FoundationSceneBuilder.Rebuild `
  -quit -logFile "$PWD\rebuild-scenes.log"
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1
```

Expected: builder 退出码 0；内容资产和 `.meta` 生成；EditMode 与 PlayMode 全部通过。

- [ ] **Step 7: 提交**

```powershell
git add BorderValley/Assets/Code/UI/Inventory BorderValley/Assets/Code/UI/Battle/WorldBattleEntryView.cs `
  BorderValley/Assets/Editor/Tools BorderValley/Assets/Resources `
  BorderValley/Assets/Scenes BorderValley/Assets/Tests/EditMode/Inventory/InventoryUiPresenterTests.cs
git commit -m "feat(inventory): add content assets and touch equipment panel"
```

---

### Task 8: 打通 World 结算、自动存档、恢复和 Android 验证

**Files:**
- Modify: `BorderValley/Assets/Code/UI/Battle/WorldBattleEntryView.cs`
- Modify: `BorderValley/Assets/Code/Inventory/InventoryBootstrapInstaller.cs`
- Modify: `BorderValley/Assets/Code/Inventory/Progression/PartyProgressionService.cs`
- Modify: `BorderValley/Assets/Code/UI/Inventory/InventoryUiPresenter.cs`
- Create: `BorderValley/Assets/Tests/PlayMode/EquipmentGrowthFlowTests.cs`
- Modify: `D:\program\NEW_CHAT_HANDOFF.md`

**Interfaces:**
- Consumes: `IBattleFlow.TryTakeResult`、`PartyProgressionService.ApplyBattleUnitStates`、`InventoryService`、`CraftingService`、`SaveService`。
- Produces: `WorldBattleEntryView` 的战斗结算、自动存档、掉落入包、等级/技能点更新、战败安全点提示和完整 PlayMode 流程。

- [ ] **Step 1: 写失败的 PlayMode 测试**

```csharp
using System.Collections;
using System.Linq;
using BorderValley.Core;
using BorderValley.Core.BattleFlow;
using BorderValley.Inventory;
using BorderValley.UI.Battle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BorderValley.PlayMode.Tests
{
    public sealed class EquipmentGrowthFlowTests
    {
        [UnityTest]
        public IEnumerator World_ConsumesBattleResult_AddsLootAndAwardsExperience()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            yield return SceneManager.LoadSceneAsync("World");
            yield return null;

            var context = GameBootstrapper.Context;
            var flow = context.Get<IBattleFlow>();
            var inventory = context.Get<InventoryService>();
            var progression = context.Get<PartyProgressionService>();
            var beforeGold = inventory.Gold;

            flow.CompleteBattle(new BattleResult(
                BattleFlowOutcome.PlayerVictory,
                4,
                new[] { new BattleUnitResult("player.warrior", 10, 4) }));
            var entry = Object.FindFirstObjectByType<WorldBattleEntryView>();
            entry.ConsumePendingResultForTests();

            Assert.That(progression.Members.Any(member => member.Experience > 0), Is.True);
            Assert.That(inventory.Gold, Is.GreaterThan(beforeGold));
            Assert.That(inventory.Items.Count, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator World_CanOpenInventoryCraftAndReturnToBattle()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            yield return SceneManager.LoadSceneAsync("World");
            yield return null;

            var entry = Object.FindFirstObjectByType<WorldBattleEntryView>();
            entry.InventoryButton.onClick.Invoke();
            Assert.That(entry.InventoryPanel.IsOpen, Is.True);
            entry.InventoryPanel.Close();
            entry.BattleButton.onClick.Invoke();

            for (var index = 0; index < 180 && SceneManager.GetActiveScene().name != "Battle"; index++)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Battle"));
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1`

Expected: `ConsumePendingResultForTests` 和 `InventoryPanel` 尚未定义。

- [ ] **Step 3: 实现 World 战斗结算**

`WorldBattleEntryView` 从 `GameContext` 获取 Inventory、Progression、Crafting、LootGenerator、Economy 和 SaveService。`Start` 与每次重新启用时调用：

```csharp
public bool ConsumePendingResultForTests() => ConsumePendingResult();

private bool ConsumePendingResult()
{
    if (flow == null || !flow.TryTakeResult(out var result)) return false;

    progression.ApplyBattleUnitStates(result.UnitStates);
    if (result.Outcome == BattleFlowOutcome.PlayerVictory)
    {
        var rewardSeed = $"reward:{progression.SafePointId}:{result.Rounds}:{progression.TotalExperience}";
        var random = RandomSourceFactory.FromSeed(rewardSeed);
        var loot = lootGenerator.Generate(
            $"loot.{System.Guid.NewGuid():N}",
            banditDropTable,
            progression.HighestLevel,
            random);
        inventory.TryAdd(loot, out _);
        inventory.AddGold(25 + result.Rounds * 5);
        progression.AwardExperience(35 + result.Rounds * 5);
        saveService.Save(0, "World");
    }
    else if (result.Outcome == BattleFlowOutcome.EnemyVictory)
    {
        progression.ReturnToSafePoint();
        saveService.Save(0, "World");
    }

    RefreshResultAndPartyLabels();
    return true;
}
```

测试注入的确定性实例 ID在 production 使用 `Guid`，测试断言不依赖具体 ID。自动存档失败必须保留 World 流程并在结果标签显示 `save.error.autosave_failed`，不能丢失已结算的运行时奖励。

- [ ] **Step 4: 实现打造/退出自动存档**

`InventoryUiPresenter.Craft`、`DismantleSelected`、`ReforgeSelected` 成功后通过注入的 `Action<string> save` 回调保存 slot 0。`WorldBattleEntryView.OnApplicationPause(true)` 在 Context 存在时调用 `SaveService.Save(0, "World")`；重复保存不得覆盖有效 backup。Presenter 注入的 save 回调默认为空，EditMode 测试不写磁盘。

- [ ] **Step 5: 覆盖恢复流程**

Boot 在 MainMenu 点击 New Game 前不自动 Load。新增 `Continue` 路径不在本阶段重做 UI；本阶段只验证 `SaveService.Load(0)` 后 Inventory 和 Progression participant 恢复后再进入 World。PlayMode 或在 EditMode 用 `SaveService` 直接测试，确保装备实例、等级、当前生命/法力、金币、材料和技能等级完整恢复。

- [ ] **Step 6: 运行完整自动化和 Android 构建**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-android.ps1
```

Expected:

- EditMode 和 PlayMode 全部通过。
- Android ARM64 IL2CPP APK 构建成功。
- APK 位于 `Builds/Android/BorderValley.apk`，不提交。
- `git diff --check` 只允许既有 Unity `.meta` 行尾噪声；新增文件和本计划不得新增空白错误。

- [ ] **Step 7: 真机测试暂停**

在运行任何 ADB 安装、启动或 10 分钟稳定性测试之前，先执行：

```powershell
& 'D:\unity\editor\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe' devices -l
```

若发现设备，立即暂停并通知用户，等待用户明确同意后才安装 APK 或启动游戏。不得在未确认时继续真机步骤。

- [ ] **Step 8: 最终审查、提交和交接**

完成整分支最终 review，修复 Critical/Important 并做限定复审。PR 创建前 push `feature/equipment-growth`；网络失败时保留本地提交并在 handoff 标记未推送。PR 合并后更新 `D:\program\NEW_CHAT_HANDOFF.md`：

- 当前阶段状态和 PR 链接。
- 当前分支、HEAD、远端 main。
- 已完成 task 和测试计数。
- Android APK 是否构建。
- 真机是否验证；未验证时必须明确写“未验证”。
- 遗留 Minor。
- 下一阶段下一步骤。

```powershell
git add BorderValley/Assets/Code/UI/Battle/WorldBattleEntryView.cs `
  BorderValley/Assets/Code/UI/Inventory/InventoryUiPresenter.cs `
  BorderValley/Assets/Code/Inventory/InventoryBootstrapInstaller.cs `
  BorderValley/Assets/Code/Inventory/Progression/PartyProgressionService.cs `
  BorderValley/Assets/Tests/PlayMode/EquipmentGrowthFlowTests.cs
git commit -m "feat(world): complete equipment growth reward loop"
```

---

## 完成标准

- 等级上限 10，经验全队共享，升级固定提升属性，指定等级获得技能点。
- 技能点可解锁或强化技能，当前生命/法力在非战斗状态缓慢恢复，全队倒下返回最近安全点。
- 七个基础装备覆盖六个装备槽；品质与词条数量严格为 0/1/2/3。
- 史诗至少包含一个技能修正或触发词条；同装备不重复、不出现互斥词条。
- 掉落顺序、品质权重、词条选择和数值生成均可由同一 `IRandomSource` 种子复现。
- 铁匠支持指定基础装备打造、分解、锁定一条词条重铸，并显示词条范围。
- 背包容量 30，可按槽位、品质、装备状态和词条种类筛选，并支持四种排序。
- 统一经济服务处理价格、买卖限制、金币和材料；已装备物品和任务物品不能正常出售。
- 装备基础属性、百分比词条、技能修正、触发和条件效果在战斗中真实生效。
- 背包、装备实例、金币、材料、成长状态和技能等级可通过原子存档恢复。
- 完整 EditMode、PlayMode、Android ARM64 IL2CPP 构建通过。
- 真机测试若发现设备必须暂停并通知用户；未获得确认时不得声称真机验证完成。
- 最终整分支 review 无未处理 Critical/Important；PR 已创建；交接文件和 ledger 已更新。






