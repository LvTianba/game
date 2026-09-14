using BorderValley.Data.Narrative;

namespace BorderValley.Narrative
{
    public sealed class QuestObjectiveView
    {
        public QuestObjectiveView(
            QuestObjectiveDefinition definition,
            int currentCount)
        {
            Definition = definition;
            CurrentCount = currentCount;
        }

        public QuestObjectiveDefinition Definition { get; }
        public string ObjectiveId => Definition.ObjectiveId;
        public QuestObjectiveKind Kind => Definition.Kind;
        public string TargetId => Definition.TargetId;
        public string LocalizationKey => Definition.LocalizationKey;
        public int RequiredCount => Definition.RequiredCount;
        public int CurrentCount { get; }
        public bool IsComplete => CurrentCount >= RequiredCount;
    }
}
