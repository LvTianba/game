using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Data;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;
using BorderValley.Inventory;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorderValley.Narrative.Tests
{
    public sealed class DialogueServiceTests
    {
        private const string ElderId = "npc.elder";
        private const string InvalidNpcId = "npc.invalid";
        private const string HerbId = "item.herb";
        private const string ShopId = "shop.general";
        private const string ReadyEventId = "event.ready";
        private const string FirstEventId = "event.first";
        private const string ThirdEventId = "event.third";

        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                Object.DestroyImmediate(value);
            created.Clear();
        }

        [Test]
        public void TryStart_ConditionedChoices_ShowsOnlySatisfiedAndCombinedChoices()
        {
            var quest = CreateQuest("quest.accept", Objective("objective.talk", QuestObjectiveKind.TalkToNpc, ElderId, 1));
            var dialogue = CreateDialogue(
                "dialogue.conditions",
                "node.hub",
                Node(
                    "node.hub",
                    "dialogue.conditions.hub",
                    choices: new[]
                    {
                        Choice(
                            "choice.quest",
                            conditions: new[] { Condition(DialogueConditionKind.QuestState, quest.Id, (int)QuestState.Active) }),
                        Choice(
                            "choice.item",
                            conditions: new[] { Condition(DialogueConditionKind.HasItem, HerbId, 2) }),
                        Choice(
                            "choice.event",
                            conditions: new[] { Condition(DialogueConditionKind.Event, ReadyEventId) }),
                        Choice(
                            "choice.favor",
                            conditions: new[] { Condition(DialogueConditionKind.FavorTier, ElderId, 1) }),
                        Choice(
                            "choice.all",
                            conditions: new[]
                            {
                                Condition(DialogueConditionKind.QuestState, quest.Id, (int)QuestState.Active),
                                Condition(DialogueConditionKind.HasItem, HerbId, 2),
                                Condition(DialogueConditionKind.Event, ReadyEventId),
                                Condition(DialogueConditionKind.FavorTier, ElderId, 1)
                            })
                    }));
            var runtime = CreateRuntime(dialogue, new[] { quest });

            Assert.That(runtime.Service.TryStart(ElderId, out var session, out var startError), Is.True, startError);
            AssertChoiceIds(session);

            Assert.That(runtime.State.TryAcceptQuest(quest.Id, out var acceptError), Is.True, acceptError);
            AssertChoiceIds(session, "choice.quest");

            Assert.That(runtime.Inventory.TryAdd(Item("herb.1", HerbId), out var firstAddError), Is.True, firstAddError);
            AssertChoiceIds(session, "choice.quest");

            Assert.That(runtime.Inventory.TryAdd(Item("herb.2", HerbId), out var secondAddError), Is.True, secondAddError);
            AssertChoiceIds(session, "choice.quest", "choice.item");

            Assert.That(runtime.State.SetEvent(ReadyEventId), Is.True);
            AssertChoiceIds(session, "choice.quest", "choice.item", "choice.event");

            Assert.That(runtime.State.TryChangeFavor(ElderId, 1, out var favorError), Is.True, favorError);
            AssertChoiceIds(session, "choice.quest", "choice.item", "choice.event", "choice.favor", "choice.all");
        }

        [Test]
        public void TryStart_WhenStartNodeConditionFails_SkipsToSatisfiedNode()
        {
            var dialogue = CreateDialogue(
                "dialogue.node_conditions",
                "node.conditional",
                Node(
                    "node.conditional",
                    "dialogue.conditional",
                    conditions: new[] { Condition(DialogueConditionKind.Event, ReadyEventId) },
                    nextNodeId: "node.fallback"),
                Node("node.fallback", "dialogue.fallback"));
            var runtime = CreateRuntime(dialogue);

            Assert.That(runtime.Service.TryStart(ElderId, out var session, out var error), Is.True, error);
            Assert.That(session.CurrentNode.NodeId, Is.EqualTo("node.fallback"));

            Assert.That(runtime.State.SetEvent(ReadyEventId), Is.True);
            Assert.That(runtime.Service.TryStart(ElderId, out session, out error), Is.True, error);
            Assert.That(session.CurrentNode.NodeId, Is.EqualTo("node.conditional"));
        }

        [Test]
        public void TryStart_WhenEveryCandidateNodeFailsCondition_ReturnsStableError()
        {
            var dialogue = CreateDialogue(
                "dialogue.no_start",
                "node.start",
                Node(
                    "node.start",
                    "dialogue.no_start",
                    conditions: new[] { Condition(DialogueConditionKind.Event, ReadyEventId) }));
            var runtime = CreateRuntime(dialogue);

            Assert.That(runtime.Service.TryStart(ElderId, out var session, out var error), Is.False);
            Assert.That(session, Is.Null);
            Assert.That(error, Is.EqualTo(NarrativeTextKeys.NoAvailableDialogueNode));
        }

        [Test]
        public void TryStart_WithMissingNpcDialogueOrNode_ReturnsStableErrors()
        {
            var dialogue = CreateDialogue("dialogue.present", "node.missing");
            var runtime = CreateRuntime(dialogue);

            Assert.That(runtime.Service.TryStart("npc.unknown", out _, out var npcError), Is.False);
            Assert.That(npcError, Is.EqualTo(NarrativeTextKeys.UnknownNpc));

            Assert.That(runtime.Service.TryStart(InvalidNpcId, out _, out var dialogueError), Is.False);
            Assert.That(dialogueError, Is.EqualTo(NarrativeTextKeys.UnknownDialogue));

            Assert.That(runtime.Service.TryStart(ElderId, out _, out var nodeError), Is.False);
            Assert.That(nodeError, Is.EqualTo(NarrativeTextKeys.UnknownDialogueNode));
        }

        [Test]
        public void TryChoose_WithAcceptQuestAction_StartsQuest()
        {
            var quest = CreateQuest("quest.accept", Objective("objective.talk", QuestObjectiveKind.TalkToNpc, ElderId, 1));
            var dialogue = CreateChoiceDialogue(
                "dialogue.accept",
                new[] { Action(DialogueActionKind.AcceptQuest, quest.Id) });
            var runtime = CreateRuntime(dialogue, new[] { quest });

            var session = StartAndChoose(runtime, out var error);

            Assert.That(error, Is.Empty);
            Assert.That(session.IsComplete, Is.True);
            Assert.That(runtime.State.GetQuestState(quest.Id), Is.EqualTo(QuestState.Active));
        }

        [Test]
        public void TryChoose_WithAdvanceQuestAction_AdvancesFirstIncompleteObjective()
        {
            var quest = CreateQuest(
                "quest.advance",
                Objective("objective.reach", QuestObjectiveKind.ReachLocation, "area.village", 2));
            var dialogue = CreateChoiceDialogue(
                "dialogue.advance",
                new[] { Action(DialogueActionKind.AdvanceQuest, quest.Id, 1) });
            var runtime = CreateRuntime(dialogue, new[] { quest });
            Assert.That(runtime.State.TryAcceptQuest(quest.Id, out var acceptError), Is.True, acceptError);

            var session = StartAndChoose(runtime, out var error);

            Assert.That(error, Is.Empty);
            Assert.That(session.IsComplete, Is.True);
            Assert.That(runtime.State.GetObjectiveProgress(quest.Id, "objective.reach"), Is.EqualTo(1));
        }

        [Test]
        public void TryChoose_WithTurnInQuestAction_CompletesQuest()
        {
            var quest = CreateQuest("quest.turnin", Objective("objective.talk", QuestObjectiveKind.TalkToNpc, ElderId, 1));
            var dialogue = CreateChoiceDialogue(
                "dialogue.turnin",
                new[] { Action(DialogueActionKind.TurnInQuest, quest.Id) });
            var runtime = CreateRuntime(dialogue, new[] { quest });
            Assert.That(runtime.State.TryAcceptQuest(quest.Id, out var acceptError), Is.True, acceptError);
            Assert.That(runtime.State.TryAdvanceQuestObjective(quest.Id, "objective.talk", 1, out var advanceError), Is.True, advanceError);
            Assert.That(runtime.State.GetQuestState(quest.Id), Is.EqualTo(QuestState.ReadyToTurnIn));

            var session = StartAndChoose(runtime, out var error);

            Assert.That(error, Is.Empty);
            Assert.That(session.IsComplete, Is.True);
            Assert.That(runtime.State.GetQuestState(quest.Id), Is.EqualTo(QuestState.Completed));
        }

        [Test]
        public void TryChoose_WithFavorEventAndShopActions_UpdatesStateAndSession()
        {
            var dialogue = CreateChoiceDialogue(
                "dialogue.world_actions",
                new[]
                {
                    Action(DialogueActionKind.ChangeFavor, ElderId, 1),
                    Action(DialogueActionKind.SetEvent, ReadyEventId),
                    Action(DialogueActionKind.OpenShop, ShopId)
                });
            var runtime = CreateRuntime(dialogue);

            var session = StartAndChoose(runtime, out var error);

            Assert.That(error, Is.Empty);
            Assert.That(session.IsComplete, Is.True);
            Assert.That(session.OpenedShopId, Is.EqualTo(ShopId));
            Assert.That(runtime.State.GetFavorTier(ElderId), Is.EqualTo(1));
            Assert.That(runtime.State.HasEvent(ReadyEventId), Is.True);
        }

        [Test]
        public void TryChoose_WhenActionFails_StopsAndRollsBackEarlierActions()
        {
            var quest = CreateQuest("quest.active", Objective("objective.talk", QuestObjectiveKind.TalkToNpc, ElderId, 1));
            var dialogue = CreateChoiceDialogue(
                "dialogue.failure",
                new[]
                {
                    Action(DialogueActionKind.SetEvent, FirstEventId),
                    Action(DialogueActionKind.AcceptQuest, quest.Id),
                    Action(DialogueActionKind.SetEvent, ThirdEventId)
                });
            var runtime = CreateRuntime(dialogue, new[] { quest });
            Assert.That(runtime.State.TryAcceptQuest(quest.Id, out var acceptError), Is.True, acceptError);
            Assert.That(runtime.Service.TryStart(ElderId, out var session, out var startError), Is.True, startError);

            Assert.That(runtime.Service.TryChoose(session, 0, out var error), Is.False);

            Assert.That(error, Is.EqualTo(NarrativeTextKeys.QuestAlreadyStarted));
            Assert.That(runtime.State.HasEvent(FirstEventId), Is.False);
            Assert.That(runtime.State.HasEvent(ThirdEventId), Is.False);
            Assert.That(session.IsComplete, Is.False);
        }

        [Test]
        public void TryChoose_WithIndexOutsideVisibleChoices_ReturnsErrorAndDoesNotExecute()
        {
            var dialogue = CreateChoiceDialogue(
                "dialogue.choice_range",
                new[] { Action(DialogueActionKind.SetEvent, FirstEventId) });
            var runtime = CreateRuntime(dialogue);
            Assert.That(runtime.Service.TryStart(ElderId, out var session, out var startError), Is.True, startError);

            Assert.That(runtime.Service.TryChoose(session, -1, out var negativeError), Is.False);
            Assert.That(negativeError, Is.EqualTo(NarrativeTextKeys.InvalidDialogueChoice));
            Assert.That(runtime.Service.TryChoose(session, session.VisibleChoices.Count, out var highError), Is.False);
            Assert.That(highError, Is.EqualTo(NarrativeTextKeys.InvalidDialogueChoice));
            Assert.That(runtime.State.HasEvent(FirstEventId), Is.False);
        }

        [Test]
        public void TryStart_AfterReadNode_ExposesSkipTextSemantics()
        {
            var dialogue = CreateChoiceDialogue("dialogue.read", Array.Empty<DialogueActionDefinition>(), "node.end");
            var runtime = CreateRuntime(dialogue);

            Assert.That(runtime.Service.TryStart(ElderId, out var firstSession, out var firstError), Is.True, firstError);
            Assert.That(firstSession.IsCurrentNodeRead, Is.False);
            Assert.That(firstSession.ShouldSkipCurrentNodeText, Is.False);
            Assert.That(runtime.State.IsDialogueNodeRead("node.start"), Is.True);
            Assert.That(runtime.Service.TryChoose(firstSession, 0, out var chooseError), Is.True, chooseError);

            Assert.That(runtime.Service.TryStart(ElderId, out var reopened, out var reopenError), Is.True, reopenError);
            Assert.That(reopened.IsCurrentNodeRead, Is.True);
            Assert.That(reopened.ShouldSkipCurrentNodeText, Is.True);
        }

        [Test]
        public void TryChoose_WhenDestinationNodeIsMissing_ReturnsErrorAndRollsBackActions()
        {
            var dialogue = CreateChoiceDialogue(
                "dialogue.missing_destination",
                new[] { Action(DialogueActionKind.SetEvent, FirstEventId) },
                "node.missing");
            var runtime = CreateRuntime(dialogue);
            Assert.That(runtime.Service.TryStart(ElderId, out var session, out var startError), Is.True, startError);

            Assert.That(runtime.Service.TryChoose(session, 0, out var error), Is.False);

            Assert.That(error, Is.EqualTo(NarrativeTextKeys.UnknownDialogueNode));
            Assert.That(runtime.State.HasEvent(FirstEventId), Is.False);
            Assert.That(session.IsComplete, Is.False);
        }

        [Test]
        public void TryChoose_WhenSessionIsMissing_ReturnsStableError()
        {
            var dialogue = CreateChoiceDialogue("dialogue.required_session", Array.Empty<DialogueActionDefinition>());
            var runtime = CreateRuntime(dialogue);

            Assert.That(runtime.Service.TryChoose(null, 0, out var error), Is.False);
            Assert.That(error, Is.EqualTo(NarrativeTextKeys.DialogueSessionRequired));
        }

        private static DialogueSession StartAndChoose(Runtime runtime, out string error)
        {
            Assert.That(runtime.Service.TryStart(ElderId, out var session, out var startError), Is.True, startError);
            Assert.That(runtime.Service.TryChoose(session, 0, out error), Is.True, error);
            return session;
        }

        private static void AssertChoiceIds(DialogueSession session, params string[] expected)
        {
            var actual = session.VisibleChoices.Select(choice => choice.ChoiceId).ToArray();
            Assert.That(actual, Is.EquivalentTo(expected));
        }

        private Runtime CreateRuntime(
            DialogueDefinition dialogue,
            IEnumerable<QuestDefinition> quests = null,
            IEnumerable<string> events = null)
        {
            var elder = Track(ScriptableObject.CreateInstance<NpcDefinition>());
            elder.EditorConfigure(ElderId, "npc.elder.name", dialogue.Id, string.Empty, 0);
            var invalidNpc = Track(ScriptableObject.CreateInstance<NpcDefinition>());
            invalidNpc.EditorConfigure(InvalidNpcId, "npc.invalid.name", "dialogue.missing", string.Empty, 0);
            var herb = CreateItem(HerbId);
            var shop = Track(ScriptableObject.CreateInstance<ShopDefinition>());
            shop.EditorConfigure(ShopId, "shop.general.name", string.Empty, Array.Empty<ShopOfferDefinition>());
            var area = Track(ScriptableObject.CreateInstance<WorldAreaDefinition>());
            area.EditorConfigure(
                "area.village",
                new Rect(0f, 0f, 20f, 20f),
                Array.Empty<Rect>(),
                new[] { elder, invalidNpc },
                Array.Empty<WorldEncounterDefinition>(),
                Array.Empty<WorldInteractableDefinition>(),
                Array.Empty<ItemDropTableDefinition>(),
                events ?? new[] { ReadyEventId, FirstEventId, ThirdEventId },
                Array.Empty<string>());

            var questList = (quests ?? Array.Empty<QuestDefinition>()).ToArray();
            var definitions = new ContentDefinition[] { elder, invalidNpc, herb, shop, area, dialogue }
                .Concat(questList)
                .ToArray();
            var state = new NarrativeStateService(definitions);
            var inventory = new InventoryService(8, new Dictionary<string, ItemDefinition>(StringComparer.Ordinal)
            {
                [herb.Id] = herb
            }, 10);
            var progression = new PartyProgressionService(
                new Dictionary<string, CharacterDefinition>(StringComparer.Ordinal),
                Array.Empty<PartyMemberState>());
            var rewardService = new QuestRewardService(inventory, progression, inventory.Definitions, state);
            var questService = new QuestService(
                questList.ToDictionary(quest => quest.Id, StringComparer.Ordinal),
                state,
                inventory,
                rewardService);
            var service = new DialogueService(definitions, state, questService, inventory, progression);
            return new Runtime(state, inventory, service);
        }

        private DialogueDefinition CreateDialogue(
            string id,
            string startNodeId,
            params DialogueNodeDefinition[] nodes)
        {
            var dialogue = Track(ScriptableObject.CreateInstance<DialogueDefinition>());
            dialogue.EditorConfigure(id, startNodeId, nodes);
            return dialogue;
        }

        private DialogueDefinition CreateChoiceDialogue(
            string id,
            IEnumerable<DialogueActionDefinition> actions,
            string nextNodeId = "node.end")
        {
            return CreateDialogue(
                id,
                "node.start",
                Node(
                    "node.start",
                    id + ".start",
                    choices: new[] { Choice(id + ".choice", nextNodeId, actions: actions) }),
                Node("node.end", id + ".end"));
        }

        private DialogueNodeDefinition Node(
            string nodeId,
            string textKey,
            IEnumerable<DialogueConditionDefinition> conditions = null,
            IEnumerable<DialogueActionDefinition> actions = null,
            IEnumerable<DialogueChoiceDefinition> choices = null,
            string nextNodeId = "")
        {
            return new DialogueNodeDefinition(
                nodeId,
                ElderId,
                textKey,
                conditions ?? Array.Empty<DialogueConditionDefinition>(),
                actions ?? Array.Empty<DialogueActionDefinition>(),
                choices ?? Array.Empty<DialogueChoiceDefinition>(),
                nextNodeId);
        }

        private DialogueChoiceDefinition Choice(
            string choiceId,
            string nextNodeId = "",
            IEnumerable<DialogueConditionDefinition> conditions = null,
            IEnumerable<DialogueActionDefinition> actions = null)
        {
            return new DialogueChoiceDefinition(
                choiceId,
                choiceId + ".label",
                nextNodeId,
                conditions ?? Array.Empty<DialogueConditionDefinition>(),
                actions ?? Array.Empty<DialogueActionDefinition>());
        }

        private static DialogueConditionDefinition Condition(
            DialogueConditionKind kind,
            string targetId,
            int value = 0) =>
            new(kind, targetId, value);

        private static DialogueActionDefinition Action(
            DialogueActionKind kind,
            string targetId,
            int amount = 1) =>
            new(kind, targetId, amount);

        private QuestDefinition CreateQuest(string id, params QuestObjectiveDefinition[] objectives)
        {
            var quest = Track(ScriptableObject.CreateInstance<QuestDefinition>());
            quest.EditorConfigure(
                id,
                id + ".title",
                id + ".description",
                Array.Empty<string>(),
                objectives,
                Array.Empty<QuestRewardDefinition>());
            return quest;
        }

        private static QuestObjectiveDefinition Objective(
            string objectiveId,
            QuestObjectiveKind kind,
            string targetId,
            int requiredCount) =>
            new(objectiveId, kind, targetId, requiredCount, false, "quest.objective");

        private ItemDefinition CreateItem(string id)
        {
            var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
            item.EditorConfigure(
                id,
                id + ".name",
                ItemSlot.Accessory,
                Array.Empty<string>(),
                false,
                10,
                Array.Empty<StatValue>());
            return item;
        }

        private static ItemInstance Item(string instanceId, string definitionId) =>
            new(instanceId, definitionId, 1, ItemRarity.Common, Array.Empty<AffixInstance>());

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }

        private sealed class Runtime
        {
            public Runtime(
                NarrativeStateService state,
                InventoryService inventory,
                DialogueService service)
            {
                State = state;
                Inventory = inventory;
                Service = service;
            }

            public NarrativeStateService State { get; }
            public InventoryService Inventory { get; }
            public DialogueService Service { get; }
        }
    }
}
