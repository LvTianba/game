using BorderValley.Data.Narrative;

namespace BorderValley.Narrative
{
    public sealed class QuestObjectiveView
    {
        public QuestObjectiveView(
            int objectiveIndex,
            QuestObjectiveDefinition definition,
            int currentCount)
        {
            ObjectiveIndex = objectiveIndex;
            Definition = definition;
            CurrentCount = currentCount;
        }

        public int ObjectiveIndex { get; }
        public QuestObjectiveDefinition Definition { get; }
        public QuestObjectiveKind Kind => Definition.Kind;
        public string TargetId => Definition.TargetId;
        public string LocalizationKey => Definition.LocalizationKey;
        public int RequiredCount => Definition.RequiredCount;
        public int CurrentCount { get; }
        public bool IsComplete => CurrentCount >= RequiredCount;
    }
}
