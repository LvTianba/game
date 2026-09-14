using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Data;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;
using BorderValley.Inventory;

namespace BorderValley.Narrative
{
    public sealed class DialogueActionResolver
    {
        private readonly NarrativeStateService state;
        private readonly QuestService quests;
        private readonly InventoryService inventory;
        private readonly PartyProgressionService progression;
        private readonly IReadOnlyDictionary<string, QuestDefinition> questDefinitions;
        private readonly HashSet<string> npcIds;
        private readonly HashSet<string> shopIds;
        private readonly HashSet<string> eventIds;

        public DialogueActionResolver(
            IEnumerable<ContentDefinition> definitions,
            NarrativeStateService state,
            QuestService quests,
            InventoryService inventory,
            PartyProgressionService progression = null)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.quests = quests ?? throw new ArgumentNullException(nameof(quests));
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.progression = progression;

            var all = definitions.ToArray();
            questDefinitions = all.OfType<QuestDefinition>()
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.Id))
                .GroupBy(value => value.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            npcIds = Ids<NpcDefinition>(all);
            shopIds = Ids<ShopDefinition>(all);
            eventIds = CollectKnownEventIds(all);
        }

        public bool TryResolve(
            IEnumerable<DialogueActionDefinition> actions,
            out string openedShopId,
            out string error)
        {
            var normalized = (actions ?? Array.Empty<DialogueActionDefinition>()).ToArray();
            foreach (var action in normalized)
            {
                if (TryValidate(action, out error)) continue;
                openedShopId = string.Empty;
                return false;
            }

            var stateSnapshot = state.Capture();
            var inventorySnapshot = inventory.Capture();
            var progressionSnapshot = progression?.Capture();
            openedShopId = string.Empty;
            foreach (var action in normalized)
            {
                if (TryExecute(action, ref openedShopId, out error)) continue;
                Restore(progressionSnapshot, inventorySnapshot, stateSnapshot);
                openedShopId = string.Empty;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryExecute(
            DialogueActionDefinition action,
            ref string openedShopId,
            out string error)
        {
            switch (action.Kind)
            {
                case DialogueActionKind.AcceptQuest:
                    return quests.TryAccept(action.TargetId, out error);
                case DialogueActionKind.AdvanceQuest:
                    return TryAdvanceQuest(action, out error);
                case DialogueActionKind.TurnInQuest:
                    return quests.TryTurnIn(action.TargetId, out error);
                case DialogueActionKind.OpenShop:
                    openedShopId = action.TargetId;
                    error = string.Empty;
                    return true;
                case DialogueActionKind.ChangeFavor:
                    return state.TryChangeFavor(action.TargetId, action.Amount, out error);
                case DialogueActionKind.SetEvent:
                    if (state.SetEvent(action.TargetId))
                    {
                        error = string.Empty;
                        return true;
                    }
                    error = NarrativeTextKeys.UnknownEvent;
                    return false;
                default:
                    error = NarrativeTextKeys.InvalidDialogueAction;
                    return false;
            }
        }

        private bool TryAdvanceQuest(DialogueActionDefinition action, out string error)
        {
            if (!questDefinitions.TryGetValue(action.TargetId, out var quest))
            {
                error = NarrativeTextKeys.QuestNotFound;
                return false;
            }

            var questState = state.GetQuestState(quest.Id);
            if (questState == QuestState.Completed)
            {
                error = NarrativeTextKeys.QuestAlreadyCompleted;
                return false;
            }
            if (questState != QuestState.Active)
            {
                error = NarrativeTextKeys.QuestNotActive;
                return false;
            }
            if (action.Amount <= 0)
            {
                error = NarrativeTextKeys.QuestObjectiveAmountInvalid;
                return false;
            }

            var objective = quest.Objectives.FirstOrDefault(candidate =>
                candidate != null &&
                state.GetObjectiveProgress(quest.Id, candidate.ObjectiveId) < candidate.RequiredCount);
            if (objective == null || string.IsNullOrWhiteSpace(objective.ObjectiveId))
            {
                error = NarrativeTextKeys.QuestNotReady;
                return false;
            }

            return state.TryAdvanceQuestObjective(
                quest.Id,
                objective.ObjectiveId,
                action.Amount,
                out error);
        }

        private bool TryValidate(DialogueActionDefinition action, out string error)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.TargetId))
            {
                error = NarrativeTextKeys.InvalidDialogueAction;
                return false;
            }

            var valid = action.Kind switch
            {
                DialogueActionKind.AcceptQuest => questDefinitions.ContainsKey(action.TargetId),
                DialogueActionKind.AdvanceQuest => questDefinitions.ContainsKey(action.TargetId),
                DialogueActionKind.TurnInQuest => questDefinitions.ContainsKey(action.TargetId),
                DialogueActionKind.OpenShop => shopIds.Contains(action.TargetId),
                DialogueActionKind.ChangeFavor => npcIds.Contains(action.TargetId),
                DialogueActionKind.SetEvent => eventIds.Contains(action.TargetId),
                _ => false
            };
            if (valid)
            {
                error = string.Empty;
                return true;
            }

            error = action.Kind switch
            {
                DialogueActionKind.AcceptQuest => NarrativeTextKeys.QuestNotFound,
                DialogueActionKind.AdvanceQuest => NarrativeTextKeys.QuestNotFound,
                DialogueActionKind.TurnInQuest => NarrativeTextKeys.QuestNotFound,
                DialogueActionKind.OpenShop => NarrativeTextKeys.UnknownShop,
                DialogueActionKind.ChangeFavor => NarrativeTextKeys.UnknownNpc,
                DialogueActionKind.SetEvent => NarrativeTextKeys.UnknownEvent,
                _ => NarrativeTextKeys.InvalidDialogueAction
            };
            return false;
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

        private static HashSet<string> Ids<T>(IEnumerable<ContentDefinition> definitions)
            where T : ContentDefinition =>
            definitions.OfType<T>()
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.Id))
                .Select(value => value.Id)
                .ToHashSet(StringComparer.Ordinal);

        private static HashSet<string> CollectKnownEventIds(IEnumerable<ContentDefinition> definitions)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var area in definitions.OfType<WorldAreaDefinition>())
            {
                foreach (var eventId in area.EventIds)
                    if (!string.IsNullOrWhiteSpace(eventId))
                        result.Add(eventId);

                foreach (var interactable in area.Interactables)
                {
                    if (interactable != null &&
                        interactable.Kind == WorldInteractableKind.Investigate &&
                        !string.IsNullOrWhiteSpace(interactable.TargetId))
                    {
                        result.Add(interactable.TargetId);
                    }
                }
            }

            foreach (var encounter in definitions.OfType<WorldEncounterDefinition>())
                if (!string.IsNullOrWhiteSpace(encounter.CompletionEventId))
                    result.Add(encounter.CompletionEventId);

            return result;
        }
    }
}
