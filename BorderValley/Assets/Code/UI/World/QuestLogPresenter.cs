using System;
using System.Linq;
using BorderValley.Data.Narrative;
using BorderValley.Narrative;

namespace BorderValley.UI.World
{
    public sealed class QuestLogPresenter
    {
        private readonly QuestService quests;
        private readonly IQuestLogPanelView view;

        public QuestLogPresenter(QuestService quests, IQuestLogPanelView view)
        {
            this.quests = quests ?? throw new ArgumentNullException(nameof(quests));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.view.CloseRequested += Close;
        }

        public bool IsOpen { get; private set; }
        public string LastErrorKey { get; private set; } = string.Empty;

        public void Open()
        {
            IsOpen = true;
            view.SetVisible(true);
            Refresh();
        }

        public void Close()
        {
            IsOpen = false;
            LastErrorKey = string.Empty;
            view.SetVisible(false);
            view.Render(new QuestLogPanelViewData(
                Array.Empty<QuestLogEntryBinding>(),
                string.Empty));
        }

        public void Refresh()
        {
            var entries = quests.GetJournal()
                .Where(IsVisible)
                .Select(BuildEntry)
                .ToArray();
            LastErrorKey = string.Empty;
            view.Render(new QuestLogPanelViewData(entries, LastErrorKey));
        }

        private static bool IsVisible(QuestJournalEntry entry) =>
            entry != null &&
            entry.State != QuestState.NotStarted &&
            entry.State != QuestState.Failed;

        private static QuestLogEntryBinding BuildEntry(QuestJournalEntry entry) =>
            new(
                entry.QuestId,
                entry.TitleKey,
                entry.DescriptionKey,
                WorldTextKeys.QuestStateKey(entry.State),
                entry.Objectives
                    .Where(objective => objective != null)
                    .Select(objective => new QuestObjectiveBinding(
                        objective.ObjectiveId,
                        objective.LocalizationKey,
                        objective.CurrentCount,
                        objective.RequiredCount))
                    .ToArray(),
                entry.Rewards
                    .Where(reward => reward != null)
                    .Select(reward => new QuestRewardBinding(
                        WorldTextKeys.QuestRewardKey(reward),
                        reward.TargetId,
                        reward.Amount))
                    .ToArray());
    }
}
