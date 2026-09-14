using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Data;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;
using BorderValley.Inventory;
using BorderValley.Narrative;
using BorderValley.UI.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BorderValley.UI.World.Tests
{
    public sealed class WorldUiPresenterTests
    {
        private const string ElderId = "npc.elder";
        private const string EventId = "event.ready";
        private const string ShopId = "shop.general";
        private const string OfferId = "offer.sword";
        private const string SwordId = "item.sword";
        private const string FillerId = "item.filler";

        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
            {
                if (value != null)
                    Object.DestroyImmediate(value);
            }
            created.Clear();
        }

        [Test]
        public void Open_DialogueConditionChangesTextAndVisibleChoices()
        {
            var runtime = CreateDialogueRuntime(CreateConditionalDialogue(), ElderId);
            var view = new StubDialoguePanelView();
            var presenter = new DialogueUiPresenter(runtime.Service, view);

            Assert.That(presenter.Open(ElderId), Is.True);
            Assert.That(view.Data.TextKey, Is.EqualTo("dialogue.fallback.text"));
            Assert.That(view.Data.Choices.Select(choice => choice.ChoiceId),
                Is.EqualTo(new[] { "choice.fallback" }));

            Assert.That(runtime.State.SetEvent(EventId), Is.True);

            Assert.That(presenter.Open(ElderId), Is.True);
            Assert.That(view.Data.TextKey, Is.EqualTo("dialogue.ready.text"));
            Assert.That(view.Data.Choices.Select(choice => choice.ChoiceId),
                Is.EqualTo(new[] { "choice.ready" }));
        }

        [Test]
        public void Open_ReadDialogueNode_SkipsTextButKeepsVisibleChoices()
        {
            var runtime = CreateDialogueRuntime(CreateConditionalDialogue(), ElderId);
            var view = new StubDialoguePanelView();
            var presenter = new DialogueUiPresenter(runtime.Service, view);

            Assert.That(presenter.Open(ElderId), Is.True);
            view.RaiseChoice(0);
            presenter.Close();

            Assert.That(presenter.Open(ElderId), Is.True);
            Assert.That(view.Data.TextKey, Is.Empty);
            Assert.That(view.Data.Choices.Select(choice => choice.ChoiceId),
                Is.EqualTo(new[] { "choice.fallback" }));
            Assert.That(view.Data.ShowContinue, Is.False);
        }

        [Test]
        public void Continue_LinearDialogue_AdvancesUntilComplete()
        {
            var dialogue = CreateDialogue(
                "dialogue.linear",
                ElderId,
                "node.start",
                Node("node.start", ElderId, "dialogue.linear.start", nextNodeId: "node.middle"),
                Node("node.middle", ElderId, "dialogue.linear.middle", nextNodeId: "node.end"),
                Node("node.end", ElderId, "dialogue.linear.end"));
            var runtime = CreateDialogueRuntime(dialogue, ElderId);
            var view = new StubDialoguePanelView();
            var presenter = new DialogueUiPresenter(runtime.Service, view);

            Assert.That(presenter.Open(ElderId), Is.True);
            Assert.That(view.Data.TextKey, Is.EqualTo("dialogue.linear.start"));
            Assert.That(view.Data.ShowContinue, Is.True);

            view.RaiseContinue();
            Assert.That(view.Data.TextKey, Is.EqualTo("dialogue.linear.middle"));

            view.RaiseContinue();
            Assert.That(view.Data.TextKey, Is.EqualTo("dialogue.linear.end"));
            Assert.That(view.Data.ShowContinue, Is.False);

            view.RaiseContinue();
            Assert.That(view.Data.ErrorKey, Is.EqualTo(NarrativeTextKeys.DialogueComplete));
        }

        [Test]
        public void SelectChoice_OpenedShopId_IsConsumedOnceAndNotRepeated()
        {
            var shop = Track(ScriptableObject.CreateInstance<ShopDefinition>());
            shop.EditorConfigure(ShopId, "shop.general.name", string.Empty, Array.Empty<ShopOfferDefinition>());
            var dialogue = CreateDialogue(
                "dialogue.open_shop",
                ElderId,
                "node.start",
                Node(
                    "node.start",
                    ElderId,
                    "dialogue.open_shop.start",
                    choices: new[]
                    {
                        Choice(
                            "choice.open_shop",
                            actions: new[]
                            {
                                new DialogueActionDefinition(DialogueActionKind.OpenShop, ShopId)
                            })
                    }));
            var runtime = CreateDialogueRuntime(dialogue, ElderId, shop);
            var view = new StubDialoguePanelView();
            var presenter = new DialogueUiPresenter(runtime.Service, view);
            Assert.That(presenter.Open(ElderId), Is.True);

            view.RaiseChoice(0);

            Assert.That(presenter.PendingOpenedShopId, Is.EqualTo(ShopId));
            Assert.That(presenter.ConsumeOpenedShopId(), Is.EqualTo(ShopId));
            Assert.That(presenter.PendingOpenedShopId, Is.Empty);
            Assert.That(presenter.ConsumeOpenedShopId(), Is.Empty);
        }

        [Test]
        public void Open_NodeActionOpenShop_QueuesShopIdOnce()
        {
            var shop = Track(ScriptableObject.CreateInstance<ShopDefinition>());
            shop.EditorConfigure(ShopId, "shop.general.name", string.Empty, Array.Empty<ShopOfferDefinition>());
            var dialogue = CreateDialogue(
                "dialogue.node_open_shop",
                ElderId,
                "node.start",
                Node(
                    "node.start",
                    ElderId,
                    "dialogue.node_open_shop.start",
                    actions: new[]
                    {
                        new DialogueActionDefinition(DialogueActionKind.OpenShop, ShopId)
                    }));
            var runtime = CreateDialogueRuntime(dialogue, ElderId, shop);
            var view = new StubDialoguePanelView();
            var presenter = new DialogueUiPresenter(runtime.Service, view);

            Assert.That(presenter.Open(ElderId), Is.True);
            Assert.That(presenter.PendingOpenedShopId, Is.EqualTo(ShopId));
            Assert.That(presenter.ConsumeOpenedShopId(), Is.EqualTo(ShopId));
            Assert.That(presenter.ConsumeOpenedShopId(), Is.Empty);
        }

        [Test]
        public void Buy_Offer_RefreshesGoldAndOfferList()
        {
            var runtime = CreateShopRuntime(startingGold: 100);
            var view = new StubShopPanelView();
            var presenter = new ShopUiPresenter(runtime.Shop, runtime.Inventory, runtime.Economy, runtime.State, view);
            presenter.Open(ShopId);
            Assert.That(view.Data.Gold, Is.EqualTo(100));
            Assert.That(view.Data.BuyOffers.Select(offer => offer.OfferId),
                Is.EqualTo(new[] { OfferId }));

            view.RaiseBuy(OfferId);

            Assert.That(runtime.Inventory.Gold, Is.EqualTo(80));
            Assert.That(runtime.Shop.GetOffers(ShopId), Is.Empty);
            Assert.That(view.Data.Gold, Is.EqualTo(80));
            Assert.That(view.Data.BuyOffers, Is.Empty);
            Assert.That(view.Data.ErrorKey, Is.Empty);
        }

        [Test]
        public void Buy_WhenBagIsFull_ShowsLocalizedErrorAndKeepsState()
        {
            var runtime = CreateShopRuntime(capacity: 1, startingGold: 100, addFiller: true);
            var view = new StubShopPanelView();
            var presenter = new ShopUiPresenter(runtime.Shop, runtime.Inventory, runtime.Economy, runtime.State, view);
            presenter.Open(ShopId);

            view.RaiseBuy(OfferId);

            Assert.That(presenter.LastErrorKey, Is.EqualTo(InventoryTextKeys.BagFull));
            Assert.That(view.Data.ErrorKey, Is.EqualTo(InventoryTextKeys.BagFull));
            Assert.That(runtime.Inventory.Gold, Is.EqualTo(100));
            Assert.That(runtime.Inventory.Items.Count, Is.EqualTo(1));
            Assert.That(runtime.Shop.GetOffers(ShopId).Select(offer => offer.OfferId),
                Is.EqualTo(new[] { OfferId }));
        }

        [Test]
        public void Sell_Instance_RefreshesGoldAndSellList()
        {
            var runtime = CreateShopRuntime(startingGold: 0, addSword: true);
            var view = new StubShopPanelView();
            var presenter = new ShopUiPresenter(runtime.Shop, runtime.Inventory, runtime.Economy, runtime.State, view);
            presenter.Open(ShopId);
            Assert.That(view.Data.SellItems.Select(item => item.InstanceId),
                Is.EqualTo(new[] { "sword.0" }));

            view.RaiseSell("sword.0");

            Assert.That(runtime.Inventory.Gold, Is.EqualTo(8));
            Assert.That(runtime.Inventory.Items, Is.Empty);
            Assert.That(view.Data.Gold, Is.EqualTo(8));
            Assert.That(view.Data.SellItems, Is.Empty);
            Assert.That(view.Data.ErrorKey, Is.Empty);
        }

        [Test]
        public void QuestLog_FiltersStatesAndShowsObjectiveAndRewardKeys()
        {
            var runtime = CreateQuestRuntime();
            var view = new StubQuestLogPanelView();
            var presenter = new QuestLogPresenter(runtime.Quests, view);

            presenter.Open();

            Assert.That(view.Data.Entries.Select(entry => entry.QuestId),
                Is.EqualTo(new[] { "quest.active", "quest.completed", "quest.ready" }));

            var active = view.Data.Entries.Single(entry => entry.QuestId == "quest.active");
            Assert.That(active.StateKey, Is.EqualTo(WorldTextKeys.QuestStateActive));
            Assert.That(active.Objectives.Single().LocalizationKey,
                Is.EqualTo("quest.active.objective"));
            Assert.That(active.Objectives.Single().CurrentCount, Is.EqualTo(1));
            Assert.That(active.Objectives.Single().RequiredCount, Is.EqualTo(2));
            Assert.That(active.Rewards.Select(reward => reward.RewardKey),
                Is.EqualTo(new[]
                {
                    WorldTextKeys.QuestRewardKey(QuestRewardKind.Gold),
                    WorldTextKeys.QuestRewardKey(QuestRewardKind.Experience)
                }));
        }

        [Test]
        public void Open_InvalidNpc_ShowsStableLocalizedError()
        {
            var runtime = CreateDialogueRuntime(CreateConditionalDialogue(), ElderId);
            var view = new StubDialoguePanelView();
            var presenter = new DialogueUiPresenter(runtime.Service, view);

            Assert.That(presenter.Open("npc.missing"), Is.False);

            Assert.That(presenter.LastErrorKey, Is.EqualTo(NarrativeTextKeys.UnknownNpc));
            Assert.That(view.Data.ErrorKey, Is.EqualTo(NarrativeTextKeys.UnknownNpc));
        }

        [Test]
        public void Shop_InvalidShopAndOffer_ShowStableLocalizedErrors()
        {
            var runtime = CreateShopRuntime();
            var view = new StubShopPanelView();
            var presenter = new ShopUiPresenter(runtime.Shop, runtime.Inventory, runtime.Economy, runtime.State, view);

            presenter.Open("shop.missing");
            Assert.That(presenter.LastErrorKey, Is.EqualTo(NarrativeTextKeys.UnknownShop));
            Assert.That(view.Data.ErrorKey, Is.EqualTo(NarrativeTextKeys.UnknownShop));

            presenter.Open(ShopId);
            view.RaiseBuy("offer.missing");
            Assert.That(presenter.LastErrorKey, Is.EqualTo(NarrativeTextKeys.UnknownOffer));
            Assert.That(view.Data.ErrorKey, Is.EqualTo(NarrativeTextKeys.UnknownOffer));
        }

        [Test]
        public void ShopUiPresenter_ExposesOnlyExplicitDependencyConstructor()
        {
            var constructors = typeof(ShopUiPresenter).GetConstructors();

            Assert.That(constructors, Has.Length.EqualTo(1));
            Assert.That(
                constructors[0].GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[]
                {
                    typeof(ShopService),
                    typeof(InventoryService),
                    typeof(EconomyService),
                    typeof(NarrativeStateService),
                    typeof(IShopPanelView)
                }));
        }

        [Test]
        public void Open_LockedShop_ShowsUnknownShopAndDoesNotRenderAsOpen()
        {
            var runtime = CreateShopRuntime(unlockShop: false);
            var view = new StubShopPanelView();
            var presenter = new ShopUiPresenter(runtime.Shop, runtime.Inventory, runtime.Economy, runtime.State, view);

            Assert.That(presenter.Open(ShopId), Is.False);

            Assert.That(presenter.LastErrorKey, Is.EqualTo(NarrativeTextKeys.UnknownShop));
            Assert.That(view.Data.ErrorKey, Is.EqualTo(NarrativeTextKeys.UnknownShop));
            Assert.That(view.Data.BuyOffers, Is.Empty);
            Assert.That(presenter.CurrentShopId, Is.Empty);
        }

        [Test]
        public void Sell_InvalidInstance_ShowsStableLocalizedError()
        {
            var runtime = CreateShopRuntime();
            var view = new StubShopPanelView();
            var presenter = new ShopUiPresenter(runtime.Shop, runtime.Inventory, runtime.Economy, runtime.State, view);
            presenter.Open(ShopId);

            view.RaiseSell("instance.missing");

            Assert.That(presenter.LastErrorKey, Is.EqualTo(InventoryTextKeys.ItemMissing));
            Assert.That(view.Data.ErrorKey, Is.EqualTo(InventoryTextKeys.ItemMissing));
        }

        [Test]
        public void DialoguePanelView_RepeatedRenderAndClose_DestroyChildrenImmediately()
        {
            var root = Track(new GameObject("DialoguePanelViewTest"));
            var view = root.AddComponent<DialoguePanelView>();
            view.Render(DialogueData(2));
            var panel = view.transform.Find("DialoguePanel");
            var choices = panel.Find("Choices");
            var staleButton = choices.GetChild(0).GetComponent<Button>();
            Assert.That(choices.childCount, Is.EqualTo(2));

            view.Render(DialogueData(1));

            Assert.That(choices.childCount, Is.EqualTo(1));
            Assert.That(staleButton == null, Is.True);

            view.SetVisible(false);
            view.Render(DialogueData(0));

            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(choices.childCount, Is.EqualTo(0));
            var panelObject = panel.gameObject;
            Object.DestroyImmediate(root);
            Assert.That(panelObject == null, Is.True);
        }

        [Test]
        public void ShopPanelView_RepeatedRenderAndClose_DestroyChildrenImmediately()
        {
            var root = Track(new GameObject("ShopPanelViewTest"));
            var view = root.AddComponent<ShopPanelView>();
            view.Render(ShopData(2, 1));
            var panel = view.transform.Find("ShopPanel");
            var list = panel.Find("List");
            var staleButton = list.GetChild(0).GetComponent<Button>();
            Assert.That(list.childCount, Is.EqualTo(2));

            view.Render(ShopData(1, 1));

            Assert.That(list.childCount, Is.EqualTo(1));
            Assert.That(staleButton == null, Is.True);

            view.SetVisible(false);
            view.Render(ShopData(0, 0));

            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(list.childCount, Is.EqualTo(1));
            Assert.That(list.GetChild(0).name, Is.EqualTo("Empty"));
            var panelObject = panel.gameObject;
            Object.DestroyImmediate(root);
            Assert.That(panelObject == null, Is.True);
        }

        [Test]
        public void QuestLogPanelView_RepeatedRenderAndClose_DestroyChildrenImmediately()
        {
            var root = Track(new GameObject("QuestLogPanelViewTest"));
            var view = root.AddComponent<QuestLogPanelView>();
            view.Render(QuestData(2));
            var panel = view.transform.Find("QuestLogPanel");
            var entries = panel.Find("Entries");
            var staleEntry = entries.GetChild(0).gameObject;
            Assert.That(entries.childCount, Is.EqualTo(2));

            view.Render(QuestData(1));

            Assert.That(entries.childCount, Is.EqualTo(1));
            Assert.That(staleEntry == null, Is.True);

            view.SetVisible(false);
            view.Render(QuestData(0));

            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(entries.childCount, Is.EqualTo(1));
            Assert.That(entries.GetChild(0).name, Is.EqualTo("Empty"));
            var panelObject = panel.gameObject;
            Object.DestroyImmediate(root);
            Assert.That(panelObject == null, Is.True);
        }

        private DialogueRuntime CreateDialogueRuntime(
            DialogueDefinition dialogue,
            string npcId,
            params ContentDefinition[] extraDefinitions)
        {
            var npc = CreateNpc(npcId, dialogue.Id);
            var area = CreateEventArea();
            var definitions = new List<ContentDefinition> { npc, dialogue, area };
            definitions.AddRange(extraDefinitions.Where(value => value != null));
            var inventory = new InventoryService(
                8,
                new Dictionary<string, ItemDefinition>(StringComparer.Ordinal),
                0);
            var state = new NarrativeStateService(definitions);
            var rewards = new QuestRewardService(inventory, null, inventory.Definitions, state);
            var quests = new QuestService(
                new Dictionary<string, QuestDefinition>(StringComparer.Ordinal),
                state,
                inventory,
                rewards);
            var service = new DialogueService(definitions, state, quests, inventory);
            return new DialogueRuntime(state, service);
        }

        private ShopRuntime CreateShopRuntime(
            int capacity = 8,
            int startingGold = 100,
            bool addFiller = false,
            bool addSword = false,
            bool unlockShop = true)
        {
            var sword = CreateItem(SwordId, 200, false);
            var filler = CreateItem(FillerId, 10, false);
            var owner = CreateNpc("npc.merchant", string.Empty, ShopId);
            var shop = Track(ScriptableObject.CreateInstance<ShopDefinition>());
            shop.EditorConfigure(
                ShopId,
                "shop.general.name",
                string.Empty,
                new[]
                {
                    new ShopOfferDefinition(
                        OfferId,
                        sword,
                        ItemRarity.Common,
                        1,
                        Array.Empty<AffixDefinition>())
                });

            var definitions = new ContentDefinition[] { sword, filler, owner, shop };
            var items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal)
            {
                [SwordId] = sword,
                [FillerId] = filler
            };
            var state = new NarrativeStateService(definitions);
            if (unlockShop)
                Assert.That(state.MarkShopUnlocked(ShopId), Is.True);
            var inventory = new InventoryService(capacity, items, startingGold);
            var economy = new EconomyService(inventory, items, new Dictionary<string, AffixDefinition>());
            if (addFiller)
            {
                Assert.That(
                    inventory.TryAdd(Item("filler.0", FillerId), out var fillerError),
                    Is.True,
                    fillerError);
            }
            if (addSword)
            {
                Assert.That(
                    inventory.TryAdd(Item("sword.0", SwordId), out var swordError),
                    Is.True,
                    swordError);
            }
            var shopService = new ShopService(definitions, state, economy);
            return new ShopRuntime(state, inventory, economy, shopService);
        }

        private QuestRuntime CreateQuestRuntime()
        {
            var active = CreateQuest(
                "quest.active",
                "quest.active.objective",
                2,
                new[]
                {
                    new QuestRewardDefinition(QuestRewardKind.Gold, string.Empty, 10),
                    new QuestRewardDefinition(QuestRewardKind.Experience, string.Empty, 5)
                });
            var ready = CreateQuest("quest.ready", "quest.ready.objective", 2, Array.Empty<QuestRewardDefinition>());
            var completed = CreateQuest(
                "quest.completed",
                "quest.completed.objective",
                2,
                Array.Empty<QuestRewardDefinition>());
            var failed = CreateQuest("quest.failed", "quest.failed.objective", 2, Array.Empty<QuestRewardDefinition>());
            var notStarted = CreateQuest(
                "quest.not_started",
                "quest.not_started.objective",
                1,
                Array.Empty<QuestRewardDefinition>());
            var definitions = new ContentDefinition[] { active, ready, completed, failed, notStarted };
            var state = new NarrativeStateService(definitions);
            var inventory = new InventoryService(
                8,
                new Dictionary<string, ItemDefinition>(StringComparer.Ordinal),
                0);
            var rewards = new QuestRewardService(inventory, null, inventory.Definitions, state);
            var quests = new QuestService(
                definitions.OfType<QuestDefinition>().ToDictionary(quest => quest.Id, StringComparer.Ordinal),
                state,
                inventory,
                rewards);

            Assert.That(state.TryAcceptQuest(active.Id, out var activeError), Is.True, activeError);
            Assert.That(
                state.TryAdvanceQuestObjective(active.Id, active.Objectives[0].ObjectiveId, 1, out var activeProgressError),
                Is.True,
                activeProgressError);

            Assert.That(state.TryAcceptQuest(ready.Id, out var readyError), Is.True, readyError);
            Assert.That(
                state.TryAdvanceQuestObjective(ready.Id, ready.Objectives[0].ObjectiveId, 2, out var readyProgressError),
                Is.True,
                readyProgressError);

            Assert.That(state.TryAcceptQuest(completed.Id, out var completedError), Is.True, completedError);
            Assert.That(
                state.TryAdvanceQuestObjective(
                    completed.Id,
                    completed.Objectives[0].ObjectiveId,
                    2,
                    out var completedProgressError),
                Is.True,
                completedProgressError);
            Assert.That(
                state.TryMarkQuestCompleted(completed.Id, out var completionError),
                Is.True,
                completionError);

            Assert.That(state.TryAcceptQuest(failed.Id, out var failedError), Is.True, failedError);
            var snapshot = state.Capture();
            var failedRecord = snapshot["questProgress"]
                .OfType<Newtonsoft.Json.Linq.JObject>()
                .Single(value => value.Value<string>("questId") == failed.Id);
            failedRecord["state"] = QuestState.Failed.ToString();
            state.Restore(snapshot);

            return new QuestRuntime(quests);
        }

        private DialogueDefinition CreateConditionalDialogue() =>
            CreateDialogue(
                "dialogue.conditions",
                ElderId,
                "node.conditional",
                Node(
                    "node.conditional",
                    ElderId,
                    "dialogue.ready.text",
                    conditions: new[]
                    {
                        new DialogueConditionDefinition(DialogueConditionKind.Event, EventId)
                    },
                    choices: new[] { Choice("choice.ready") },
                    nextNodeId: "node.fallback"),
                Node(
                    "node.fallback",
                    ElderId,
                    "dialogue.fallback.text",
                    choices: new[] { Choice("choice.fallback") }));

        private WorldAreaDefinition CreateEventArea()
        {
            var area = Track(ScriptableObject.CreateInstance<WorldAreaDefinition>());
            area.EditorConfigure(
                "area.test",
                new Rect(0f, 0f, 10f, 10f),
                Array.Empty<Rect>(),
                Array.Empty<NpcDefinition>(),
                Array.Empty<WorldEncounterDefinition>(),
                Array.Empty<WorldInteractableDefinition>(),
                Array.Empty<ItemDropTableDefinition>(),
                new[] { EventId },
                Array.Empty<string>());
            return area;
        }

        private NpcDefinition CreateNpc(string id, string dialogueId, string openShopId = "")
        {
            var npc = Track(ScriptableObject.CreateInstance<NpcDefinition>());
            npc.EditorConfigure(id, id + ".name", dialogueId, openShopId);
            return npc;
        }

        private DialogueDefinition CreateDialogue(
            string id,
            string npcId,
            string startNodeId,
            params DialogueNodeDefinition[] nodes)
        {
            var dialogue = Track(ScriptableObject.CreateInstance<DialogueDefinition>());
            dialogue.EditorConfigure(id, startNodeId, nodes);
            return dialogue;
        }

        private static DialogueNodeDefinition Node(
            string nodeId,
            string npcId,
            string textKey,
            IEnumerable<DialogueConditionDefinition> conditions = null,
            IEnumerable<DialogueActionDefinition> actions = null,
            IEnumerable<DialogueChoiceDefinition> choices = null,
            string nextNodeId = "") =>
            new(
                nodeId,
                npcId,
                textKey,
                conditions ?? Array.Empty<DialogueConditionDefinition>(),
                actions ?? Array.Empty<DialogueActionDefinition>(),
                choices ?? Array.Empty<DialogueChoiceDefinition>(),
                nextNodeId);

        private static DialogueChoiceDefinition Choice(
            string choiceId,
            IEnumerable<DialogueConditionDefinition> conditions = null,
            IEnumerable<DialogueActionDefinition> actions = null,
            string nextNodeId = "") =>
            new(
                choiceId,
                choiceId + ".label",
                nextNodeId,
                conditions ?? Array.Empty<DialogueConditionDefinition>(),
                actions ?? Array.Empty<DialogueActionDefinition>());

        private ItemDefinition CreateItem(string id, int baseValue, bool isQuestItem)
        {
            var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
            item.EditorConfigure(
                id,
                id + ".name",
                ItemSlot.Weapon,
                Array.Empty<string>(),
                isQuestItem,
                baseValue,
                Array.Empty<StatValue>());
            return item;
        }

        private QuestDefinition CreateQuest(
            string id,
            string objectiveKey,
            int requiredCount,
            IEnumerable<QuestRewardDefinition> rewards)
        {
            var quest = Track(ScriptableObject.CreateInstance<QuestDefinition>());
            quest.EditorConfigure(
                id,
                id + ".title",
                id + ".description",
                Array.Empty<string>(),
                new[]
                {
                    new QuestObjectiveDefinition(
                        id + ".objective",
                        QuestObjectiveKind.DefeatEnemy,
                        "enemy.bandit",
                        requiredCount,
                        false,
                        objectiveKey)
                },
                rewards);
            return quest;
        }

        private static ItemInstance Item(string instanceId, string definitionId) =>
            new(instanceId, definitionId, 1, ItemRarity.Common, Array.Empty<AffixInstance>());

        private static DialoguePanelViewData DialogueData(int choiceCount) =>
            new(
                "npc.elder.name",
                "dialogue.text",
                Enumerable.Range(0, choiceCount)
                    .Select(index => new DialogueChoiceBinding(
                        index,
                        "choice." + index,
                        "choice." + index + ".label")),
                false,
                true,
                string.Empty);

        private static ShopPanelViewData ShopData(int buyCount, int sellCount) =>
            new(
                WorldTextKeys.ShopTitle,
                100,
                Enumerable.Range(0, buyCount)
                    .Select(index => new ShopOfferBinding("offer." + index, "item." + index, 20)),
                Enumerable.Range(0, sellCount)
                    .Select(index => new ShopSellBinding("instance." + index, "item." + index, 8)),
                string.Empty);

        private static QuestLogPanelViewData QuestData(int entryCount) =>
            new(
                Enumerable.Range(0, entryCount)
                    .Select(index => new QuestLogEntryBinding(
                        "quest." + index,
                        "quest." + index + ".title",
                        "quest." + index + ".description",
                        WorldTextKeys.QuestStateActive,
                        new[]
                        {
                            new QuestObjectiveBinding(
                                "objective." + index,
                                "quest." + index + ".objective",
                                1,
                                2)
                        },
                        Array.Empty<QuestRewardBinding>())),
                string.Empty);

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }

        private sealed class DialogueRuntime
        {
            public DialogueRuntime(NarrativeStateService state, DialogueService service)
            {
                State = state;
                Service = service;
            }

            public NarrativeStateService State { get; }
            public DialogueService Service { get; }
        }

        private sealed class ShopRuntime
        {
            public ShopRuntime(
                NarrativeStateService state,
                InventoryService inventory,
                EconomyService economy,
                ShopService shop)
            {
                State = state;
                Inventory = inventory;
                Economy = economy;
                Shop = shop;
            }

            public NarrativeStateService State { get; }
            public InventoryService Inventory { get; }
            public EconomyService Economy { get; }
            public ShopService Shop { get; }
        }

        private sealed class QuestRuntime
        {
            public QuestRuntime(QuestService quests) => Quests = quests;

            public QuestService Quests { get; }
        }

        private sealed class StubDialoguePanelView : IDialoguePanelView
        {
            public event Action<int> ChoiceSelected;
            public event Action ContinueRequested;
            public event Action CloseRequested;

            public bool IsVisible { get; private set; }
            public DialoguePanelViewData Data { get; private set; }

            public void SetVisible(bool visible) => IsVisible = visible;
            public void Render(DialoguePanelViewData data) => Data = data;
            public void RaiseChoice(int index) => ChoiceSelected?.Invoke(index);
            public void RaiseContinue() => ContinueRequested?.Invoke();
            public void RaiseClose() => CloseRequested?.Invoke();
        }

        private sealed class StubShopPanelView : IShopPanelView
        {
            public event Action<string> BuyRequested;
            public event Action<string> SellRequested;
            public event Action CloseRequested;

            public bool IsVisible { get; private set; }
            public ShopPanelViewData Data { get; private set; }

            public void SetVisible(bool visible) => IsVisible = visible;
            public void Render(ShopPanelViewData data) => Data = data;
            public void RaiseBuy(string offerId) => BuyRequested?.Invoke(offerId);
            public void RaiseSell(string instanceId) => SellRequested?.Invoke(instanceId);
            public void RaiseClose() => CloseRequested?.Invoke();
        }

        private sealed class StubQuestLogPanelView : IQuestLogPanelView
        {
            public event Action CloseRequested;

            public bool IsVisible { get; private set; }
            public QuestLogPanelViewData Data { get; private set; }

            public void SetVisible(bool visible) => IsVisible = visible;
            public void Render(QuestLogPanelViewData data) => Data = data;
            public void RaiseClose() => CloseRequested?.Invoke();
        }
    }
}
