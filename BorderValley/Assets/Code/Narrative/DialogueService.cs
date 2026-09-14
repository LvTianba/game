using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Data;
using BorderValley.Data.Narrative;
using BorderValley.Inventory;

namespace BorderValley.Narrative
{
    public sealed class DialogueService
    {
        private readonly NarrativeStateService state;
        private readonly InventoryService inventory;
        private readonly PartyProgressionService progression;
        private readonly DialogueConditionEvaluator conditionEvaluator;
        private readonly DialogueActionResolver actionResolver;
        private readonly IReadOnlyDictionary<string, NpcDefinition> npcs;
        private readonly IReadOnlyDictionary<string, DialogueDefinition> dialogues;

        public DialogueService(
            IEnumerable<ContentDefinition> definitions,
            NarrativeStateService state,
            QuestService quests,
            InventoryService inventory,
            PartyProgressionService progression = null)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.progression = progression;

            var all = definitions.ToArray();
            npcs = all.OfType<NpcDefinition>()
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.Id))
                .GroupBy(value => value.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            dialogues = all.OfType<DialogueDefinition>()
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.Id))
                .GroupBy(value => value.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            conditionEvaluator = new DialogueConditionEvaluator(all, state, inventory);
            actionResolver = new DialogueActionResolver(all, state, quests, inventory, progression);
        }

        public DialogueService(
            ContentCatalog catalog,
            NarrativeStateService state,
            QuestService quests,
            InventoryService inventory,
            PartyProgressionService progression = null)
            : this(
                catalog == null ? throw new ArgumentNullException(nameof(catalog)) : catalog.All,
                state,
                quests,
                inventory,
                progression)
        {
        }

        public bool TryStart(string npcId, out DialogueSession session, out string error)
        {
            session = null;
            if (string.IsNullOrWhiteSpace(npcId) || !npcs.TryGetValue(npcId, out var npc))
            {
                error = NarrativeTextKeys.UnknownNpc;
                return false;
            }

            if (string.IsNullOrWhiteSpace(npc.DialogueId) ||
                !dialogues.TryGetValue(npc.DialogueId, out var dialogue))
            {
                error = NarrativeTextKeys.UnknownDialogue;
                return false;
            }

            if (!TryFindAvailableNode(dialogue, dialogue.StartNodeId, out var node, out error))
                return false;

            if (!TryPrepareNode(node, out var wasRead, out var choices, out var openedShopId, out error))
                return false;

            session = new DialogueSession(dialogue, node, wasRead, conditionEvaluator.IsVisible);
            session.EnterNode(node, wasRead, choices, openedShopId);
            error = string.Empty;
            return true;
        }

        public bool TryChoose(DialogueSession session, int choiceIndex, out string error)
        {
            if (session == null)
            {
                error = NarrativeTextKeys.DialogueSessionRequired;
                return false;
            }
            if (session.IsComplete)
            {
                error = NarrativeTextKeys.DialogueComplete;
                return false;
            }
            if (choiceIndex < 0 || choiceIndex >= session.VisibleChoices.Count)
            {
                error = NarrativeTextKeys.InvalidDialogueChoice;
                return false;
            }

            var choice = session.VisibleChoices[choiceIndex];
            var stateSnapshot = state.Capture();
            var inventorySnapshot = inventory.Capture();
            var progressionSnapshot = progression?.Capture();
            if (!actionResolver.TryResolve(choice.Actions, out var openedShopId, out error))
                return false;

            if (string.IsNullOrWhiteSpace(choice.NextNodeId))
            {
                session.SetOpenedShopId(openedShopId);
                session.Complete();
                error = string.Empty;
                return true;
            }

            if (!TryFindAvailableNode(session.Dialogue, choice.NextNodeId, out var nextNode, out error))
            {
                Restore(progressionSnapshot, inventorySnapshot, stateSnapshot);
                return false;
            }

            if (!TryPrepareNode(nextNode, out var wasRead, out var choices, out var nodeShopId, out error))
            {
                Restore(progressionSnapshot, inventorySnapshot, stateSnapshot);
                return false;
            }

            session.EnterNode(
                nextNode,
                wasRead,
                choices,
                string.IsNullOrWhiteSpace(nodeShopId) ? openedShopId : nodeShopId);
            error = string.Empty;
            return true;
        }

        public bool TryContinue(DialogueSession session, out string error)
        {
            if (session == null)
            {
                error = NarrativeTextKeys.DialogueSessionRequired;
                return false;
            }
            if (session.VisibleChoices.Count > 0)
            {
                error = NarrativeTextKeys.DialogueChoiceRequired;
                return false;
            }
            if (session.IsComplete || string.IsNullOrWhiteSpace(session.CurrentNode.NextNodeId))
            {
                error = NarrativeTextKeys.DialogueComplete;
                return false;
            }

            var stateSnapshot = state.Capture();
            var inventorySnapshot = inventory.Capture();
            var progressionSnapshot = progression?.Capture();
            if (!TryFindAvailableNode(
                    session.Dialogue,
                    session.CurrentNode.NextNodeId,
                    out var nextNode,
                    out error))
            {
                return false;
            }

            if (!TryPrepareNode(nextNode, out var wasRead, out var choices, out var nodeShopId, out error))
            {
                Restore(progressionSnapshot, inventorySnapshot, stateSnapshot);
                return false;
            }

            session.EnterNode(nextNode, wasRead, choices, nodeShopId);
            error = string.Empty;
            return true;
        }
        private bool TryFindAvailableNode(
            DialogueDefinition dialogue,
            string startNodeId,
            out DialogueNodeDefinition node,
            out string error)
        {
            node = null;
            if (string.IsNullOrWhiteSpace(startNodeId))
            {
                error = NarrativeTextKeys.UnknownDialogueNode;
                return false;
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var nodeId = startNodeId;
            while (!string.IsNullOrWhiteSpace(nodeId))
            {
                if (!visited.Add(nodeId))
                {
                    error = NarrativeTextKeys.NoAvailableDialogueNode;
                    return false;
                }

                var candidate = dialogue.Nodes.FirstOrDefault(value =>
                    value != null && string.Equals(value.NodeId, nodeId, StringComparison.Ordinal));
                if (candidate == null)
                {
                    error = NarrativeTextKeys.UnknownDialogueNode;
                    return false;
                }

                if (conditionEvaluator.AreSatisfied(candidate.Conditions))
                {
                    node = candidate;
                    error = string.Empty;
                    return true;
                }

                nodeId = candidate.NextNodeId;
            }

            error = NarrativeTextKeys.NoAvailableDialogueNode;
            return false;
        }

        private bool TryPrepareNode(
            DialogueNodeDefinition node,
            out bool wasRead,
            out DialogueChoiceDefinition[] choices,
            out string openedShopId,
            out string error)
        {
            wasRead = state.IsDialogueNodeRead(node.NodeId);
            choices = Array.Empty<DialogueChoiceDefinition>();
            var stateSnapshot = state.Capture();
            var inventorySnapshot = inventory.Capture();
            var progressionSnapshot = progression?.Capture();
            if (!actionResolver.TryResolve(node.Actions, out openedShopId, out error))
                return false;

            if (!state.MarkDialogueNodeRead(node.NodeId))
            {
                Restore(progressionSnapshot, inventorySnapshot, stateSnapshot);
                choices = Array.Empty<DialogueChoiceDefinition>();
                error = NarrativeTextKeys.UnknownDialogueNode;
                return false;
            }

            choices = node.Choices.Where(choice => choice != null).ToArray();
            error = string.Empty;
            return true;
        }

        private void Restore(
            Newtonsoft.Json.Linq.JObject progressionSnapshot,
            Newtonsoft.Json.Linq.JObject inventorySnapshot,
            Newtonsoft.Json.Linq.JObject stateSnapshot)
        {
            if (progressionSnapshot != null) progression?.Restore(progressionSnapshot);
            inventory.Restore(inventorySnapshot);
            state.Restore(stateSnapshot);
        }
    }
}
