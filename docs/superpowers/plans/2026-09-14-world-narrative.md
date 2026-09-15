# 世界探索、NPC、任务与交易 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development recommended or superpowers:executing-plans task-by-task.


**Goal:** 实现可持久化的半开放世界探索、NPC 对话与好感、任务接受推进提交、固定商店交易，以及与战斗和自动存档的完整闭环。

**Architecture:** 静态内容由 Data 的 ScriptableObject 定义；叙事规则位于 Narrative；地图移动和遇敌位于 World；UI 只调用服务接口；GameBootstrapper 安装并注册所有 ISaveParticipant。

**Tech Stack:** Unity 6000.6.0f1、C#、Unity Test Framework 1.8.0、Newtonsoft.Json、uGUI、InventoryService、EconomyService、LootGenerator、BattleFlowService、SaveService。

**Spec:** docs/superpowers/specs/2026-09-12-fantasy-tactics-rpg-vertical-slice-design.md

## Global Constraints

- Android 横屏、API 26、IL2CPP、ARM64、60 FPS、vSync 0。
- 玩家可见文字只使用本地化键，首版占位实现直接显示键名。
- 地图、交互、对话、任务、商店和奖励规则必须存在于纯 C# 可测试类型中，不能藏在 MonoBehaviour 内。
- 世界探索阶段必须包含 4 个区域、8 名有名字 NPC、1 条主线、2 条支线、固定商店、敌人遭遇和探索交互。
- NPC 好感只有陌生、友好、信任三档；只影响对白、商店折扣和少量确定任务结果，不做随机说服。
- 商店库存固定并按世界事件扩充，不做每日刷新；购买检查金币和 30 格背包容量，出售检查装备与任务物品限制。

- 任务状态固定为未接取、进行中、可提交、已完成和失败；奖励只能领取一次。
- 战斗上下文必须携带遭遇 ID、奖励掉落表、金币经验奖励和敌人定义 ID；战斗结束后由世界结算。
- 所有随机行为必须注入 IRandomSource；同一种子和同一输入必须得到同一结果。
- 自动存档至少发生在进入区域、完成任务、退出游戏、交易成功、宝箱或采集领取和战斗开始前。
- 不加入联网、玩家交易、仓库、每日刷新、随机说服、潜行、日程模拟或大型无缝地图。
- 不提交 APK、截图、测试 XML、日志和 .superpowers 内容；新增 Unity 文件必须提交对应 .meta。
- 每个 task 独立提交；真机测试前必须暂停并通知用户，不自行安装或启动 APK。

---

## 文件结构

~~~text
BorderValley/Assets/Code/Core/BattleFlow/
BattleContext.cs
BattleRequest.cs
BattleResult.cs

BorderValley/Assets/Code/Data/Narrative/
QuestState.cs QuestObjectiveKind.cs QuestRewardKind.cs
QuestObjectiveDefinition.cs QuestRewardDefinition.cs QuestDefinition.cs
DialogueConditionKind.cs DialogueActionKind.cs
DialogueConditionDefinition.cs DialogueActionDefinition.cs
DialogueChoiceDefinition.cs DialogueNodeDefinition.cs DialogueDefinition.cs
NpcDefinition.cs ShopOfferDefinition.cs ShopDefinition.cs

BorderValley/Assets/Code/Data/World/
WorldInteractableKind.cs WorldInteractableDefinition.cs
WorldEncounterDefinition.cs WorldAreaDefinition.cs
~~~

~~~text
BorderValley/Assets/Code/Narrative/
NarrativeTextKeys.cs NarrativeStateService.cs QuestService.cs
QuestJournalEntry.cs QuestObjectiveView.cs QuestRewardService.cs
DialogueConditionEvaluator.cs DialogueActionResolver.cs
DialogueSession.cs DialogueService.cs
ShopOfferFactory.cs ShopService.cs ShopOfferView.cs
NarrativeBootstrapInstaller.cs

BorderValley/Assets/Code/World/
WorldMovementSolver.cs WorldInteractionResolver.cs
WorldInteractionResult.cs WorldEncounterService.cs
WorldBattleSettlementService.cs WorldBattleSettlementResult.cs

BorderValley/Assets/Code/UI/World/
WorldTextKeys.cs VirtualJoystick.cs WorldMapView.cs
WorldExplorationController.cs
DialogueUiPresenter.cs DialoguePanelView.cs
ShopUiPresenter.cs ShopPanelView.cs
QuestLogPresenter.cs QuestLogPanelView.cs
~~~

~~~text
BorderValley/Assets/Editor/Tools/
WorldContentBuilder.cs FoundationSceneBuilder.cs

BorderValley/Assets/Tests/EditMode/Narrative/
NarrativeContentDefinitionTests.cs NarrativeStateServiceTests.cs
QuestServiceTests.cs DialogueServiceTests.cs ShopServiceTests.cs

BorderValley/Assets/Tests/EditMode/World/
BattleContextTests.cs WorldMovementSolverTests.cs
WorldInteractionResolverTests.cs WorldEncounterServiceTests.cs
WorldBattleSettlementServiceTests.cs

BorderValley/Assets/Tests/EditMode/UI/WorldUiPresenterTests.cs
BorderValley/Assets/Tests/PlayMode/WorldNarrativeFlowTests.cs
~~~

---

### Task 1: 扩展战斗上下文和结算契约

**Files:**
- Create: BorderValley/Assets/Code/Core/BattleFlow/BattleContext.cs
- Modify: BorderValley/Assets/Code/Core/BattleFlow/BattleRequest.cs
- Modify: BorderValley/Assets/Code/Core/BattleFlow/BattleResult.cs
- Modify: BorderValley/Assets/Code/Inventory/Progression/PartyBattleSnapshotBuilder.cs
- Modify: BorderValley/Assets/Code/UI/Battle/BattleSceneController.cs
- Create: BorderValley/Assets/Tests/EditMode/World/BattleContextTests.cs

**Interfaces:**
- Consumes: 现有 BattleRequest、BattleResult、BattleUnitResult、PartyBattleSnapshotBuilder。
- Produces: BattleContext、BattleRequest.Context、BattleResult.Context、BattleUnitResult.DefinitionId。


- [ ] **Step 1: 写失败测试**

~~~csharp
[Test]
public void RequestAndResult_PreserveEncounterContext()
{
var context = new BattleContext(
"encounter.forest.bandits", "loot.bandit.core", 30, 40,
new[] { "enemy.bandit", "enemy.ranger" }, false);
var request = new BattleRequest("core", "seed-1", "World", null, context);
var result = new BattleResult(
BattleFlowOutcome.PlayerVictory, 3,
new[] { new BattleUnitResult("unit.bandit", "enemy.bandit", 0, 0) }, context);

Assert.That(request.Context.EncounterId, Is.EqualTo("encounter.forest.bandits"));
Assert.That(result.Context.RewardTableId, Is.EqualTo("loot.bandit.core"));
Assert.That(result.UnitStates[0].DefinitionId, Is.EqualTo("enemy.bandit"));
}
~~~


- [ ] **Step 2: 运行测试确认失败**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: FAIL，编译错误指出 BattleContext 不存在或构造参数不匹配。

- [ ] **Step 3: 写最小实现**

~~~csharp
public sealed class BattleContext
{
public BattleContext(string encounterId, string rewardTableId, int gold, int experience, string[] enemies, bool repeatable)
{
EncounterId = encounterId;
RewardTableId = rewardTableId;
GoldReward = gold;
ExperienceReward = experience;
EnemyDefinitionIds = enemies ?? System.Array.Empty();
Repeatable = repeatable;
}
public string EncounterId { get; }
public string RewardTableId { get; }
public int GoldReward { get; }
public int ExperienceReward { get; }
public string[] EnemyDefinitionIds { get; }
public bool Repeatable { get; }
}
~~~

Request 增至 5 参数并保留 4 参数构造器。Result 增至 4 参数并保留旧构造器。UnitResult 增加 DefinitionId，旧构造器将 unitId 作为 DefinitionId。PartyBattleSnapshotBuilder 保留 request.Context。BattleSceneController 把 request.Context 和 unit.DefinitionId 写入结果。

- [ ] **Step 4: 运行测试确认通过**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: PASS，新增 2 个测试，既有战斗流程测试不变。

- [ ] **Step 5: 提交**

~~~bash
git add BorderValley/Assets/Code/Core/BattleFlow BorderValley/Assets/Code/Inventory/Progression/PartyBattleSnapshotBuilder.cs BorderValley/Assets/Code/UI/Battle/BattleSceneController.cs BorderValley/Assets/Tests
git commit -m feat-world-carry-encounter-context
~~~


---

### Task 2: 定义叙事和世界静态内容模型并扩展校验

**Files:**
- Create: BorderValley/Assets/Code/Data/Narrative 下本计划文件结构列出的 Quest、Dialogue、Npc、Shop 定义。
- Create: BorderValley/Assets/Code/Data/World 下 WorldInteractable、WorldEncounter、WorldArea 定义。
- Modify: BorderValley/Assets/Code/Data/ContentValidator.cs
- Create: BorderValley/Assets/Tests/EditMode/Narrative/NarrativeContentDefinitionTests.cs

**Interfaces:**
- Produces: NpcDefinition、QuestDefinition、DialogueDefinition、ShopDefinition、WorldAreaDefinition。
- Produces: QuestState、QuestObjectiveKind、QuestRewardKind、DialogueConditionKind、DialogueActionKind、WorldInteractableKind。


- [ ] **Step 1: 写失败测试**

测试构造最小内容目录，覆盖四个错误：WorldArea 内交互 ID 重复、Dialogue 的 nextNodeId 不存在、Quest 前置循环、Shop 引用不存在 ItemDefinition。每个断言检查 ContentValidator 返回的稳定错误码。

- [ ] **Step 2: 运行测试确认失败**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: FAIL，测试类型和定义不存在。

- [ ] **Step 3: 写最小实现**

所有定义继承 ContentDefinition。数组字段使用 SerializeField 和读属性。QuestObjectiveDefinition 包含 kind、targetId、requiredCount、consumeOnTurnIn、localizationKey。QuestRewardDefinition 包含 kind、targetId、amount。DialogueNodeDefinition 包含 nodeId、speakerNpcId、textKey、conditions、actions、choices、nextNodeId。WorldInteractableDefinition 包含 id、kind、labelKey、position、radius、targetId、arrivalPosition、requiredEventId。WorldEncounterDefinition 包含 encounterId、scenarioId、enemyDefinitionIds、rewardTableId、goldReward、experienceReward、position、triggerRadius、repeatable、requiredEventId、completionEventId。


ContentValidator 的 ValidateSpecialized 增加：所有嵌套 ID 非空且在同类范围内唯一；Npc 的 dialogueId 和 openShopId 存在；Dialogue 节点、choice 跳转、condition/action target 存在；Quest 前置存在且无环；Reward 的目标装备、材料、商店存在；Shop offer 的 ItemDefinition、AffixDefinition 和组合合法；WorldArea 的出口 Npc、Encounter、Event、RewardTable 引用存在。错误码固定为 duplicate_dialogue_node、missing_dialogue_node、cyclic_quest_prerequisite、missing_shop_item、missing_world_target。

- [ ] **Step 4: 运行测试确认通过**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: PASS，新增内容校验测试通过，现有 297 个 EditMode 测试不回归。

- [ ] **Step 5: 提交**

~~~bash
git add BorderValley/Assets/Code/Data BorderValley/Assets/Tests/EditMode/Narrative/NarrativeContentDefinitionTests.cs
git commit -m feat-narrative-define-content-and-validation
~~~


---

### Task 3: 实现可存档叙事状态和任务生命周期

**Files:**
- Create: BorderValley/Assets/Code/Narrative/NarrativeTextKeys.cs
- Create: BorderValley/Assets/Code/Narrative/NarrativeStateService.cs
- Create: BorderValley/Assets/Code/Narrative/QuestJournalEntry.cs
- Create: BorderValley/Assets/Code/Narrative/QuestObjectiveView.cs
- Create: BorderValley/Assets/Code/Narrative/QuestRewardService.cs
- Create: BorderValley/Assets/Code/Narrative/QuestService.cs
- Create: BorderValley/Assets/Code/Narrative/NarrativeBootstrapInstaller.cs
- Create: BorderValley/Assets/Tests/EditMode/Narrative/NarrativeStateServiceTests.cs
- Create: BorderValley/Assets/Tests/EditMode/Narrative/QuestServiceTests.cs


**Interfaces:**
- NarrativeStateService implements ISaveParticipant, key 为 narrative。
- GameObject 与任务状态：GetCurrentAreaId、SetCurrentLocation、HasEvent、SetEvent、IsInteractableResolved、MarkInteractableResolved、IsDialogueNodeRead、MarkDialogueNodeRead、GetFavorTier、TryChangeFavor、IsShopUnlocked、MarkShopUnlocked、IsOfferPurchased、MarkOfferPurchased。
- 任务状态：TryAcceptQuest、TryAdvanceQuestObjective、TryMarkQuestCompleted、GetQuestState、GetObjectiveProgress。
- QuestService 提供 GetJournal、TryAccept、RecordBattleDefeat、RecordTalk、RecordLocation、TryTurnIn。
- IQuestRewardService.TryApply(QuestDefinition quest, out string error)。


- [ ] **Step 1: 写失败测试**

NarrativeStateServiceTests 覆盖 Capture/Restore 后玩家位置、事件、好感、已解决交互、对话已读、商店购买与任务进度完全恢复。QuestServiceTests 覆盖前置任务、接受、击杀推进、提交物品、可提交、领取金币、材料、装备、商店解锁，以及第二次提交不重复发奖。

- [ ] **Step 2: 运行测试确认失败**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: FAIL，服务类型不存在。

- [ ] **Step 3: 写最小实现**

NarrativeStateService 使用 Dictionary 保存 QuestProgressRecord、Dictionary 保存 FavorTier、HashSet 保存事件、已解决交互、对话已读、已购买 offer 和已解锁商店。Capture 输出 JObject 数组并按 Ordinal 排序。Restore 先完整解析到临时集合，确认所有引用和取值范围有效后再替换当前状态，任一异常抛出并由 SaveService 回滚。

QuestService 所有推进先检查定义和状态；DefeatEnemy、SubmitItem、ReachLocation、TalkToNpc 分别由战斗结算、对话或提交、区域进入和 NPC 对话调用。TryTurnIn 先验证所有 SubmitItem 目标、背包空位和奖励，再执行消耗与奖励，最后标记 Completed。


- [ ] **Step 4: 运行测试确认通过**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: PASS。

- [ ] **Step 5: 提交**

~~~bash
git add BorderValley/Assets/Code/Narrative BorderValley/Assets/Tests/EditMode/Narrative
git commit -m feat-narrative-add-state-and-quests
~~~


---

### Task 4: 实现对话条件、行动、好感与已读节点

**Files:**
- Create: BorderValley/Assets/Code/Narrative/DialogueConditionEvaluator.cs
- Create: BorderValley/Assets/Code/Narrative/DialogueActionResolver.cs
- Create: BorderValley/Assets/Code/Narrative/DialogueSession.cs
- Create: BorderValley/Assets/Code/Narrative/DialogueService.cs
- Create: BorderValley/Assets/Tests/EditMode/Narrative/DialogueServiceTests.cs

**Interfaces:**
- DialogueService.TryStart(string npcId, out DialogueSession session, out string error)。
- DialogueService.TryChoose(DialogueSession session, int choiceIndex, out string error)。
- DialogueSession 提供 CurrentNode、VisibleChoices、OpenedShopId、IsComplete。
- Action Kind 包含 AcceptQuest、AdvanceQuest、TurnInQuest、OpenShop、ChangeFavor、SetEvent。


- [ ] **Step 1: 写失败测试**

覆盖条件对白选择：任务状态、持有物品、事件和好感等级只显示满足条件的节点；选择接受任务、改变好感、设置事件、打开商店后状态正确；已读节点被记录，重开对话仍可跳过已读文本。

- [ ] **Step 2: 运行测试确认失败**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: FAIL，对话服务不存在。

- [ ] **Step 3: 写最小实现**

DialogueConditionEvaluator 对四种条件做 AND 判断。DialogueActionResolver 顺序执行 action；任一 action 失败立即返回错误，不继续后续 action。DialogueService 从 NpcDefinition 的 dialogueId 开始，忽略条件失败节点，写入 NarrativeStateService 的已读集合；choice 只允许选择当前 VisibleChoices 范围。OpenShop 只返回 shopId，不直接修改 UI。

- [ ] **Step 4: 运行测试确认通过**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: PASS。

- [ ] **Step 5: 提交**

~~~bash
git add BorderValley/Assets/Code/Narrative BorderValley/Assets/Tests/EditMode/Narrative/DialogueServiceTests.cs
git commit -m feat-narrative-add-dialogue-actions
~~~


---

### Task 5: 实现固定商店、价格修正和买卖限制

**Files:**
- Modify: BorderValley/Assets/Code/Inventory/Economy/EconomyService.cs
- Create: BorderValley/Assets/Code/Narrative/ShopOfferFactory.cs
- Create: BorderValley/Assets/Code/Narrative/ShopOfferView.cs
- Create: BorderValley/Assets/Code/Narrative/ShopService.cs
- Create: BorderValley/Assets/Tests/EditMode/Narrative/ShopServiceTests.cs

**Interfaces:**
- EconomyService 增加 TryBuy(ItemInstance item, int modifierBps, out string error) 和 TrySell(string instanceId, int modifierBps, out string error)。
- ShopService.GetOffers(shopId)、TryBuy(shopId, offerId, out error)、TrySell(instanceId, out error)。
- ShopOfferView 包含 OfferId、Item、BuyPrice、IsPurchased。


- [ ] **Step 1: 写失败测试**

覆盖：锁定商店不可见；事件解锁后可见；同一 offer 购买后不再出现且重载存档仍不可购买；友好档位 5 百分比折扣、信任档位 10 百分比折扣；金币不足不改变库存；满包购买回滚金币；任务物品和已装备物品不能出售。

- [ ] **Step 2: 运行测试确认失败**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: FAIL，ShopService 和新 EconomyService 重载不存在。

- [ ] **Step 3: 写最小实现**

ShopOfferFactory 使用 shopId 和 offerIndex 生成稳定 instanceId，并通过 ItemRules 校验 definition、rarity、itemLevel、affix 组合。ShopService 的价格修正为 10000 减去 500 乘以好感档位。购买顺序是计算价格、TrySpendGold、TryAdd，TryAdd 失败时 AddGold 回滚。购买成功后再 MarkOfferPurchased。

- [ ] **Step 4: 运行测试确认通过**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: PASS。

- [ ] **Step 5: 提交**

~~~bash
git add BorderValley/Assets/Code/Inventory/Economy/EconomyService.cs BorderValley/Assets/Code/Narrative BorderValley/Assets/Tests/EditMode/Narrative/ShopServiceTests.cs
git commit -m feat-narrative-add-shops-and-trade
~~~


---

### Task 6: 实现世界移动、交互解析和遭遇请求

**Files:**
- Create: BorderValley/Assets/Code/World/WorldMovementSolver.cs
- Create: BorderValley/Assets/Code/World/WorldInteractionResult.cs
- Create: BorderValley/Assets/Code/World/WorldInteractionResolver.cs
- Create: BorderValley/Assets/Code/World/WorldEncounterService.cs
- Create: BorderValley/Assets/Tests/EditMode/World/WorldMovementSolverTests.cs
- Create: BorderValley/Assets/Tests/EditMode/World/WorldInteractionResolverTests.cs
- Create: BorderValley/Assets/Tests/EditMode/World/WorldEncounterServiceTests.cs

**Interfaces:**
- WorldMovementSolver.TryMove(Vector2 current, Vector2 delta, float radius, WorldAreaDefinition area, out Vector2 result)。
- WorldInteractionResolver.FindNearest(Vector2 position, WorldAreaDefinition area, NarrativeStateService state, out WorldInteractionResult result)。
- WorldInteractionResult 包含 Kind、DefinitionId、TargetId、ArrivalPosition、SourceId。
- WorldEncounterService.BuildRequest(WorldEncounterDefinition encounter, BattlePartySnapshot party, string seed) 返回带 BattleContext 的 BattleRequest。


- [ ] **Step 1: 写失败测试**

覆盖：移动被地图边界钳制；圆形半径不能穿过矩形障碍；最近交互在半径外返回 false；事件未满足的交互不可见；AreaExit 返回目标区域和到达点；Encounter 创建 BattleRequest 时保留敌人定义 ID、奖励表、金币、经验和 Repeatable。

- [ ] **Step 2: 运行测试确认失败**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: FAIL，世界服务不存在。

- [ ] **Step 3: 写最小实现**

WorldMovementSolver 先尝试 X 轴移动再尝试 Y 轴移动；每一步先把圆心限制在 area bounds 减去 radius 的范围，再用矩形最近点距离判断碰撞，若碰撞则只保留未碰撞轴。WorldInteractionResolver 只考虑 requiredEventId 为空或已设置的项目，并排除已处理的宝箱、采集和调查点；距离相同时按 definitionId 的 Ordinal 顺序选择。

WorldEncounterService 不接受 UI 或场景对象，只构造 BattleContext 和 BattleRequest。ScenarioId、Seed、ReturnScene 和 PartySnapshot 均来自参数，EncounterId 与 RewardTableId 来自 WorldEncounterDefinition。

- [ ] **Step 4: 运行测试确认通过**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: PASS。

- [ ] **Step 5: 提交**

~~~bash
git add BorderValley/Assets/Code/World BorderValley/Assets/Tests/EditMode/World
git commit -m feat-world-add-map-interactions-and-encounters
~~~


---

### Task 7: 实现战斗结果的世界结算与任务推进

**Files:**
- Create: BorderValley/Assets/Code/World/WorldBattleSettlementResult.cs
- Create: BorderValley/Assets/Code/World/WorldBattleSettlementService.cs
- Create: BorderValley/Assets/Tests/EditMode/World/WorldBattleSettlementServiceTests.cs
- Modify: BorderValley/Assets/Code/UI/Battle/WorldBattleEntryView.cs

**Interfaces:**
- WorldBattleSettlementService.Settle(BattleResult result, WorldEncounterDefinition encounter, out WorldBattleSettlementResult settlement)。
- WorldBattleSettlementResult 包含 Success、RequiresAutosave、LootAdded、GoldAwarded、ExperienceAwarded、DefeatedEnemyIds、ErrorKey。
- 结算成功后 WorldBattleEntryView 负责 SaveService.Save(0, World)。


- [ ] **Step 1: 写失败测试**

覆盖：胜利后掉落、金币、经验和敌人击杀目标只结算一次；背包满时不消耗奖励且 Encounter 未标记完成；失败时单位状态恢复、队伍回安全点、Encounter 保持未完成；Repeatable 遭遇不标记完成；不同 seed 产生不同掉落但相同 seed 产生相同掉落。

- [ ] **Step 2: 运行测试确认失败**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: FAIL，结算服务不存在。

- [ ] **Step 3: 写最小实现**

WorldBattleSettlementService 依赖 InventoryService、PartyProgressionService、LootGenerator、QuestService 和 NarrativeStateService。胜利时先用 result.Context 生成一次 loot，检查背包容量，加入掉落、金币和经验，应用 UnitStates，按 UnitStates 的 DefinitionId 调用 QuestService.RecordBattleDefeat，最后设置 completionEventId 并标记 encounter resolved。失败时只应用 UnitStates、调用 ReturnToSafePoint，不处理奖励和任务。

WorldBattleEntryView 保留 pending reward 语义：结算成功但 autosave 失败时保持 PendingBattleReward，下一次 ConsumePendingResult 只重试 Save，不重复调用 Settle。

- [ ] **Step 4: 运行测试确认通过**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: PASS，既有 EquipmentGrowthFlowTests 的结算/重试语义保持或按新结算服务更新。

- [ ] **Step 5: 提交**

~~~bash
git add BorderValley/Assets/Code/World BorderValley/Assets/Code/UI/Battle/WorldBattleEntryView.cs BorderValley/Assets/Tests/EditMode/World WorldBattleSettlementServiceTests.cs
git commit -m feat-world-settle-battles-and-quest-progress
~~~


---

### Task 8: 实现对话、商店和任务日志 UI presenter


**Files:**
- Create: BorderValley/Assets/Code/UI/World/WorldTextKeys.cs
- Create: BorderValley/Assets/Code/UI/World/DialogueUiPresenter.cs
- Create: BorderValley/Assets/Code/UI/World/DialoguePanelView.cs
- Create: BorderValley/Assets/Code/UI/World/ShopUiPresenter.cs
- Create: BorderValley/Assets/Code/UI/World/ShopPanelView.cs
- Create: BorderValley/Assets/Code/UI/World/QuestLogPresenter.cs
- Create: BorderValley/Assets/Code/UI/World/QuestLogPanelView.cs
- Create: BorderValley/Assets/Tests/EditMode/UI/WorldUiPresenterTests.cs


**Interfaces:**
- DialogueUiPresenter.Open(npcId)、SelectChoice(index)、Close。
- ShopUiPresenter.Open(shopId)、Buy(offerId)、Sell(instanceId)、Close。
- QuestLogPresenter.Open、Close、Refresh。
- 三个 view 只暴露按钮、文本和列表绑定，不读取 GameBootstrapper。

- [ ] **Step 1: 写失败测试**

使用内存服务和 stub view 覆盖：条件对白切换文本和选项；点击购买刷新金币和 offer 列表；满包显示本地化错误键；任务日志只显示已接取、进行中、可提交和已完成任务，并显示当前目标和奖励键。

- [ ] **Step 2: 运行测试确认失败**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: FAIL，presenter 和 view 不存在。

- [ ] **Step 3: 写最小实现**

所有 presenter 构造函数注入服务，不读取单例。View 使用 UnityEngine.UI 动态创建，中文暂以 key 文本显示。DialoguePanelView 只显示 speakerKey、textKey、choice labelKey 列表和关闭按钮；ShopPanelView 显示 buy/sell tab、价格、金币和错误键；QuestLogPanelView 显示状态、目标进度和奖励键。

- [ ] **Step 4: 运行测试确认通过**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: PASS。

- [ ] **Step 5: 提交**

~~~bash
git add BorderValley/Assets/Code/UI/World BorderValley/Assets/Tests/EditMode/UI/WorldUiPresenterTests.cs
git commit -m feat-ui-add-world-narrative-panels
~~~


---

### Task 9: 接入触控世界场景和首版内容资产


**Files:**
- Create: BorderValley/Assets/Code/UI/World/VirtualJoystick.cs
- Create: BorderValley/Assets/Code/UI/World/WorldMapView.cs
- Create: BorderValley/Assets/Code/UI/World/WorldExplorationController.cs
- Create: BorderValley/Assets/Editor/Tools/WorldContentBuilder.cs
- Modify: BorderValley/Assets/Editor/Tools/FoundationSceneBuilder.cs
- Modify: BorderValley/Assets/Scenes/World.unity
- Modify: BorderValley/Assets/Resources/ContentCatalog.asset
- Delete: BorderValley/Assets/Code/World/WorldPlaceholder.cs
- Modify: BorderValley/Assets/Tests/PlayMode/EquipmentGrowthFlowTests.cs
- Create: BorderValley/Assets/Tests/PlayMode/WorldNarrativeFlowTests.cs


**Interfaces:**
- WorldExplorationController 获取 NarrativeStateService、QuestService、DialogueService、ShopService、WorldAreaDefinition、WorldEncounterService、BattleFlowService、ISceneLoader 和 SaveService。
- VirtualJoystick.Value 返回归一化 Vector2，InteractButton 只在 WorldInteractionResolver 有结果时 interactable。
- WorldMapView.Render(WorldAreaDefinition area) 生成背景、障碍、NPC、采集点、宝箱、调查物和遭遇标记。

**首版内容 ID：**
- 区域：area.village、area.forest、area.watchtower、area.crypt。
- NPC：npc.elder、npc.blacksmith、npc.merchant、npc.innkeeper、npc.ranger_companion、npc.mage_companion、npc.hunter、npc.survivor。
- 任务：quest.main.crypt、quest.side.ranger、quest.side.survivor。
- 商店：shop.general、shop.blacksmith。
- 固定遭遇：encounter.forest.bandits、encounter.forest.wolves、encounter.watchtower.rangers、encounter.watchtower.elite、encounter.crypt.guardians、encounter.crypt.necromancers、encounter.crypt.boss、encounter.road.patrol。


- [ ] **Step 1: 写失败测试**

WorldNarrativeFlowTests 覆盖：从 Boot 进入 World 后出现玩家和村庄 NPC；移动到长者触发条件对白；接受主线后任务日志出现目标；打开杂货商商店，购买物品，金币下降且背包增加；区域出口切换后位置和区域存档恢复；触发遭遇进入 Battle，胜利返回 World 后任务击杀进度和掉落增加。

- [ ] **Step 2: 运行测试确认失败**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: FAIL，World 场景还没有 WorldExplorationController 和内容目录。

- [ ] **Step 3: 写最小实现**

WorldContentBuilder.Build 创建上述 NPC、Quest、Dialogue、Shop、WorldArea 和 Encounter 资产，并调用 ContentCatalog.EditorSetDefinitions。对话树首版为确定文本，主线长者接受 quest.main.crypt，杂货商和铁匠对话打开对应商店，游侠与幸存者分别接受支线。WorldArea 使用矩形障碍和标记位置，不依赖最终像素美术。

WorldExplorationController.Update 每帧读取 VirtualJoystick.Value，调用 WorldMovementSolver；相机跟随玩家；InteractButton 调用 resolver。Npc 打开 dialogue，OpenShop 打开 shop panel，Chest 或 Gather 发放内容并标记 resolved，Investigate 设置事件，AreaExit 切换 area 并自动存档，Encounter 先自动存档，再调用 WorldEncounterService.BuildRequest、flow.BeginBattle 并加载 Battle。OnApplicationPause、进入区域、交易成功和任务完成均调用 SaveService.Save(0, World)。

FoundationSceneBuilder 重建 World 场景时只创建 Main Camera、EventSystem 和 WorldExplorationController。EquipmentGrowthFlowTests 改为从 WorldExplorationController 调用测试入口，保持掉落、经验、失败回安全点和 autosave retry 语义。

- [ ] **Step 4: 运行测试确认通过**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/run-tests.ps1

Expected: PASS；PlayMode 覆盖世界到战斗到回城，EditMode 不回归。

- [ ] **Step 5: 提交**

~~~bash
git add BorderValley/Assets/Code/UI/World BorderValley/Assets/Code/World BorderValley/Assets/Editor BorderValley/Assets/Scenes/World.unity BorderValley/Assets/Resources BorderValley/Assets/Tests/PlayMode
git commit -m feat-world-add-playable-vertical-slice-loop
~~~


---

## 完成标准

- 4 个区域、8 名 NPC、1 条主线和 2 条支线可由 World 场景实际访问。
- 对话条件、任务推进、奖励只领取一次、NPC 好感折扣和商店购买在 EditMode 与 PlayMode 测试通过。
- 从 Boot 到 MainMenu、World、Dialogue、Shop、QuestLog、AreaExit、Battle、返回 World 再存档恢复的全流程有自动化证据。
- EditMode 和 PlayMode 全量测试通过。
- Android ARM64 IL2CPP 构建通过，产物不提交。
- 真机测试前暂停并通知用户；得到设备后验证触控移动、交互、对话、商店、任务奖励、区域切换、后台恢复和 10 分钟稳定性。
- 整分支最终 code review 无未处理 Critical/Important；Minor 写入 SDD ledger。
- push 分支并创建 PR；用户确认合并后更新交接文件和 ledger。
