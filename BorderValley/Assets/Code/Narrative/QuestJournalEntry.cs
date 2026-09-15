using System.Collections.Generic;
using BorderValley.Data.Narrative;

namespace BorderValley.Narrative
{
    public sealed class QuestJournalEntry
    {
        public QuestJournalEntry(
            string questId,
            string titleKey,
            string descriptionKey,
            QuestState state,
            IReadOnlyList<QuestObjectiveView> objectives,
            IReadOnlyList<QuestRewardDefinition> rewards)
        {
            QuestId = questId;
            TitleKey = titleKey;
            DescriptionKey = descriptionKey;
            State = state;
            Objectives = objectives;
            Rewards = rewards;
        }

        public string QuestId { get; }
        public string TitleKey { get; }
        public string DescriptionKey { get; }
        public QuestState State { get; }
        public IReadOnlyList<QuestObjectiveView> Objectives { get; }
        public IReadOnlyList<QuestRewardDefinition> Rewards { get; }
    }
}
