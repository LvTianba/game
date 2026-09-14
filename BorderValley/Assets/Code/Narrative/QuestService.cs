using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Data.Narrative;
using BorderValley.Inventory;

namespace BorderValley.Narrative
{
    public sealed class QuestService
    {
        private readonly IReadOnlyDictionary<string, QuestDefinition> quests;
        private readonly NarrativeStateService state;
        private readonly InventoryService inventory;
        private readonly IQuestRewardService rewards;

        public QuestService(
            IReadOnlyDictionary<string, QuestDefinition> quests,
            NarrativeStateService state,
            InventoryService inventory,
            IQuestRewardService rewards)
        {
            this.quests = quests ?? throw new ArgumentNullException(nameof(quests));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
        }

        public IReadOnlyList<QuestJournalEntry> GetJournal() =>
            quests.Values
                .Select(BuildJournalEntry)
                .Where(entry => entry != null && entry.State != QuestState.NotStarted)
                .OrderBy(entry => entry.QuestId, StringComparer.Ordinal)
                .ToArray();

        public bool TryAccept(string questId, out string error)
        {
            if (string.IsNullOrWhiteSpace(questId) || !quests.TryGetValue(questId, out var quest))
            {
                error = NarrativeTextKeys.QuestNotFound;
                return false;
            }

            var currentState = state.GetQuestState(questId);
            if (currentState == QuestState.Completed)
            {
                error = NarrativeTextKeys.QuestAlreadyCompleted;
                return false;
            }
            if (currentState != QuestState.NotStarted)
            {
                error = NarrativeTextKeys.QuestAlreadyStarted;
                return false;
            }
            if (quest.PrerequisiteQuestIds.Any(prerequisite =>
                    state.GetQuestState(prerequisite) != QuestState.Completed))
            {
                error = NarrativeTextKeys.QuestPrerequisiteMissing;
                return false;
            }

            return state.TryAcceptQuest(questId, out error);
        }

        public bool RecordBattleDefeat(string enemyDefinitionId, out string error) =>
            Advance(QuestObjectiveKind.DefeatEnemy, enemyDefinitionId, 1, out error);

        public bool RecordBattleDefeat(string enemyDefinitionId, int amount, out string error) =>
            Advance(QuestObjectiveKind.DefeatEnemy, enemyDefinitionId, amount, out error);

        public bool RecordTalk(string npcId, out string error) =>
            Advance(QuestObjectiveKind.TalkToNpc, npcId, 1, out error);

        public bool RecordLocation(string areaId, out string error) =>
            Advance(QuestObjectiveKind.ReachLocation, areaId, 1, out error);

        public bool TryTurnIn(string questId, out string error)
        {
            if (string.IsNullOrWhiteSpace(questId) || !quests.TryGetValue(questId, out var quest))
            {
                error = NarrativeTextKeys.QuestNotFound;
                return false;
            }

            var currentState = state.GetQuestState(questId);
            if (currentState == QuestState.Completed)
            {
                error = NarrativeTextKeys.QuestAlreadyCompleted;
                return false;
            }
            if (currentState != QuestState.Active && currentState != QuestState.ReadyToTurnIn)
            {
                error = NarrativeTextKeys.QuestNotActive;
                return false;
            }

            var requirements = BuildItemRequirements(quest);
            if (!HasRequiredItems(requirements))
            {
                error = NarrativeTextKeys.QuestItemsMissing;
                return false;
            }
            if (!HasRequiredProgress(quest))
            {
                error = NarrativeTextKeys.QuestNotReady;
                return false;
            }

            var consumedItems = CollectItemsToConsume(quest, requirements);
            var freeSlots = inventory.Capacity - (inventory.Items.Count - consumedItems.Count);
            var equipmentRewards = quest.Rewards.Count(reward =>
                reward != null && reward.Kind == QuestRewardKind.Equipment);
            if (freeSlots < equipmentRewards)
            {
                error = NarrativeTextKeys.QuestInventoryFull;
                return false;
            }
            if (!rewards.TryValidate(quest, out error)) return false;

            var inventorySnapshot = inventory.Capture();
            var stateSnapshot = state.Capture();
            try
            {
                foreach (var item in consumedItems)
                {
                    if (!inventory.TryRemove(item.InstanceId))
                        throw new InvalidOperationException(NarrativeTextKeys.QuestItemsMissing);
                }

                if (!rewards.TryApply(quest, out error))
                {
                    Restore(inventorySnapshot, stateSnapshot);
                    return false;
                }

                if (!state.TryMarkQuestCompleted(questId, out error))
                {
                    Restore(inventorySnapshot, stateSnapshot);
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch
            {
                Restore(inventorySnapshot, stateSnapshot);
                error = NarrativeTextKeys.QuestRewardApplyFailed;
                return false;
            }
        }

        private QuestJournalEntry BuildJournalEntry(QuestDefinition quest)
        {
            var storedState = state.GetQuestState(quest.Id);
            if (storedState == QuestState.NotStarted) return null;

            var objectiveViews = quest.Objectives
                .Select((objective, index) => new QuestObjectiveView(
                    index,
                    objective,
                    objective.Kind == QuestObjectiveKind.SubmitItem
                        ? Math.Min(objective.RequiredCount, CountAvailableItems(objective.TargetId))
                        : state.GetObjectiveProgress(quest.Id, index)))
                .ToArray();
            var effectiveState = storedState == QuestState.Active &&
                                 objectiveViews.All(view => view.IsComplete)
                ? QuestState.ReadyToTurnIn
                : storedState;
            return new QuestJournalEntry(
                quest.Id,
                quest.TitleKey,
                quest.DescriptionKey,
                effectiveState,
                objectiveViews,
                quest.Rewards);
        }

        private bool Advance(
            QuestObjectiveKind kind,
            string targetId,
            int amount,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(targetId) || amount <= 0)
            {
                error = NarrativeTextKeys.QuestObjectiveInvalid;
                return false;
            }

            var snapshot = state.Capture();
            foreach (var quest in quests.Values.OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                var questState = state.GetQuestState(quest.Id);
                if (questState != QuestState.Active) continue;

                for (var index = 0; index < quest.Objectives.Length; index++)
                {
                    var objective = quest.Objectives[index];
                    if (objective == null ||
                        objective.Kind != kind ||
                        !string.Equals(objective.TargetId, targetId, StringComparison.Ordinal))
                        continue;
                    if (state.TryAdvanceQuestObjective(quest.Id, index, amount, out error))
                        continue;
                    state.Restore(snapshot);
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private Dictionary<string, int> BuildItemRequirements(QuestDefinition quest)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var objective in quest.Objectives)
            {
                if (objective == null || objective.Kind != QuestObjectiveKind.SubmitItem) continue;
                result.TryGetValue(objective.TargetId, out var current);
                result[objective.TargetId] = current + objective.RequiredCount;
            }
            return result;
        }

        private bool HasRequiredItems(IReadOnlyDictionary<string, int> requirements) =>
            requirements.All(pair => CountAvailableItems(pair.Key) >= pair.Value);

        private bool HasRequiredProgress(QuestDefinition quest)
        {
            for (var index = 0; index < quest.Objectives.Length; index++)
            {
                var objective = quest.Objectives[index];
                if (objective == null || objective.Kind == QuestObjectiveKind.SubmitItem) continue;
                if (state.GetObjectiveProgress(quest.Id, index) < objective.RequiredCount) return false;
            }
            return true;
        }

        private List<ItemInstance> CollectItemsToConsume(
            QuestDefinition quest,
            IReadOnlyDictionary<string, int> requirements)
        {
            var consumeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var objective in quest.Objectives)
            {
                if (objective == null ||
                    objective.Kind != QuestObjectiveKind.SubmitItem ||
                    !objective.ConsumeOnTurnIn)
                    continue;
                consumeCounts.TryGetValue(objective.TargetId, out var current);
                consumeCounts[objective.TargetId] = current + objective.RequiredCount;
            }

            var selected = new List<ItemInstance>();
            foreach (var pair in consumeCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var available = inventory.Items
                    .Where(item =>
                        string.Equals(item.ItemDefinitionId, pair.Key, StringComparison.Ordinal) &&
                        !inventory.IsEquipped(item.InstanceId))
                    .OrderBy(item => item.InstanceId, StringComparer.Ordinal);
                selected.AddRange(available.Take(pair.Value));
            }
            return selected;
        }

        private int CountAvailableItems(string definitionId) =>
            inventory.Items.Count(item =>
                string.Equals(item.ItemDefinitionId, definitionId, StringComparison.Ordinal) &&
                !inventory.IsEquipped(item.InstanceId));

        private void Restore(
            Newtonsoft.Json.Linq.JObject inventorySnapshot,
            Newtonsoft.Json.Linq.JObject stateSnapshot)
        {
            state.Restore(stateSnapshot);
            inventory.Restore(inventorySnapshot);
        }
    }
}
