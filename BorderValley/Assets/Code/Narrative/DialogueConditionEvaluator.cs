using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Data;
using BorderValley.Data.Items;
using BorderValley.Data.Narrative;
using BorderValley.Data.World;
using BorderValley.Inventory;

namespace BorderValley.Narrative
{
    public sealed class DialogueConditionEvaluator
    {
        private readonly NarrativeStateService state;
        private readonly InventoryService inventory;
        private readonly HashSet<string> questIds;
        private readonly HashSet<string> itemIds;
        private readonly HashSet<string> npcIds;
        private readonly HashSet<string> eventIds;

        public DialogueConditionEvaluator(
            IEnumerable<ContentDefinition> definitions,
            NarrativeStateService state,
            InventoryService inventory)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));

            var all = definitions.ToArray();
            questIds = Ids<QuestDefinition>(all);
            itemIds = Ids<ItemDefinition>(all);
            npcIds = Ids<NpcDefinition>(all);
            eventIds = CollectKnownEventIds(all);
        }

        public bool AreSatisfied(IEnumerable<DialogueConditionDefinition> conditions)
        {
            if (conditions == null) return true;
            foreach (var condition in conditions)
                if (!IsSatisfied(condition))
                    return false;
            return true;
        }

        public bool IsVisible(DialogueChoiceDefinition choice) =>
            choice != null && AreSatisfied(choice.Conditions);

        public bool IsSatisfied(DialogueConditionDefinition condition)
        {
            if (condition == null || string.IsNullOrWhiteSpace(condition.TargetId)) return false;

            return condition.Kind switch
            {
                DialogueConditionKind.QuestState => IsQuestStateSatisfied(condition),
                DialogueConditionKind.HasItem => IsItemConditionSatisfied(condition),
                DialogueConditionKind.Event => eventIds.Contains(condition.TargetId) &&
                                               state.HasEvent(condition.TargetId),
                DialogueConditionKind.Favor => npcIds.Contains(condition.TargetId) &&
                                               condition.Value >= 0 &&
                                               condition.Value <= 2 &&
                                               state.GetFavorTier(condition.TargetId) >= condition.Value,
                _ => false
            };
        }

        private bool IsQuestStateSatisfied(DialogueConditionDefinition condition)
        {
            if (!questIds.Contains(condition.TargetId) || !IsDefinedQuestState(condition.Value))
                return false;
            return state.GetQuestState(condition.TargetId) == (QuestState)condition.Value;
        }

        private bool IsItemConditionSatisfied(DialogueConditionDefinition condition)
        {
            if (!itemIds.Contains(condition.TargetId) || condition.Value < 0) return false;
            var required = Math.Max(1, condition.Value);
            return inventory.Items.Count(item =>
                string.Equals(item.ItemDefinitionId, condition.TargetId, StringComparison.Ordinal)) >= required;
        }

        private static bool IsDefinedQuestState(int value) =>
            value >= (int)QuestState.NotStarted && value <= (int)QuestState.Failed;

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
