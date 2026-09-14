using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BorderValley.UI.World
{
    public interface IQuestLogPanelView
    {
        event Action CloseRequested;
        void SetVisible(bool visible);
        void Render(QuestLogPanelViewData data);
    }

    public sealed class QuestObjectiveBinding
    {
        public QuestObjectiveBinding(
            string objectiveId,
            string localizationKey,
            int currentCount,
            int requiredCount)
        {
            ObjectiveId = objectiveId ?? string.Empty;
            LocalizationKey = localizationKey ?? string.Empty;
            CurrentCount = currentCount;
            RequiredCount = requiredCount;
        }

        public string ObjectiveId { get; }
        public string LocalizationKey { get; }
        public int CurrentCount { get; }
        public int RequiredCount { get; }
    }

    public sealed class QuestRewardBinding
    {
        public QuestRewardBinding(string rewardKey, string targetId, int amount)
        {
            RewardKey = rewardKey ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            Amount = amount;
        }

        public string RewardKey { get; }
        public string TargetId { get; }
        public int Amount { get; }
    }

    public sealed class QuestLogEntryBinding
    {
        public QuestLogEntryBinding(
            string questId,
            string titleKey,
            string descriptionKey,
            string stateKey,
            IEnumerable<QuestObjectiveBinding> objectives,
            IEnumerable<QuestRewardBinding> rewards)
        {
            QuestId = questId ?? string.Empty;
            TitleKey = titleKey ?? string.Empty;
            DescriptionKey = descriptionKey ?? string.Empty;
            StateKey = stateKey ?? string.Empty;
            Objectives = (objectives ?? Array.Empty<QuestObjectiveBinding>()).ToArray();
            Rewards = (rewards ?? Array.Empty<QuestRewardBinding>()).ToArray();
        }

        public string QuestId { get; }
        public string TitleKey { get; }
        public string DescriptionKey { get; }
        public string StateKey { get; }
        public IReadOnlyList<QuestObjectiveBinding> Objectives { get; }
        public IReadOnlyList<QuestRewardBinding> Rewards { get; }
    }

    public sealed class QuestLogPanelViewData
    {
        public QuestLogPanelViewData(
            IEnumerable<QuestLogEntryBinding> entries,
            string errorKey)
        {
            Entries = (entries ?? Array.Empty<QuestLogEntryBinding>()).ToArray();
            ErrorKey = errorKey ?? string.Empty;
        }

        public IReadOnlyList<QuestLogEntryBinding> Entries { get; }
        public string ErrorKey { get; }
    }

    public sealed class QuestLogPanelView : MonoBehaviour, IQuestLogPanelView
    {
        private GameObject panelRoot;
        private Text titleLabel;
        private Text errorLabel;
        private RectTransform entriesRoot;
        private Button closeButton;
        private QuestLogPanelViewData data;

        public event Action CloseRequested;

        private void Awake()
        {
            EnsureBuilt();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveAllListeners();
        }

        public void SetVisible(bool visible)
        {
            EnsureBuilt();
            panelRoot.SetActive(visible);
        }

        public void Render(QuestLogPanelViewData value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            EnsureBuilt();
            data = value;
            errorLabel.text = value.ErrorKey;
            RebuildEntries();
        }

        private void RebuildEntries()
        {
            if (entriesRoot == null) return;
            for (var index = entriesRoot.childCount - 1; index >= 0; index--)
            {
                var child = entriesRoot.GetChild(index).gameObject;
                child.SetActive(false);
                WorldPanelViewFactory.DestroyForMode(child);
            }

            if (data == null || data.Entries.Count == 0)
            {
                WorldPanelViewFactory.CreateText(
                    entriesRoot,
                    "Empty",
                    WorldTextKeys.QuestLogEmpty,
                    18,
                    Vector2.zero,
                    Vector2.one,
                    TextAnchor.MiddleCenter);
                return;
            }

            for (var index = 0; index < data.Entries.Count; index++)
            {
                var entry = data.Entries[index];
                var label = WorldPanelViewFactory.CreateText(
                    entriesRoot,
                    "Quest_" + index,
                    FormatEntry(entry),
                    17,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    TextAnchor.UpperLeft);
                var rect = label.GetComponent<RectTransform>();
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(0f, -((index + 1) * 112f));
                rect.offsetMax = new Vector2(0f, -(index * 112f));
            }
        }

        private static string FormatEntry(QuestLogEntryBinding entry)
        {
            var text = new StringBuilder();
            text.Append(entry.TitleKey);
            text.Append(" [");
            text.Append(entry.StateKey);
            text.Append(']');
            if (!string.IsNullOrWhiteSpace(entry.DescriptionKey))
            {
                text.Append('\n');
                text.Append(entry.DescriptionKey);
            }

            foreach (var objective in entry.Objectives)
            {
                text.Append('\n');
                text.Append(objective.LocalizationKey);
                text.Append(' ');
                text.Append(objective.CurrentCount);
                text.Append('/');
                text.Append(objective.RequiredCount);
            }

            foreach (var reward in entry.Rewards)
            {
                text.Append('\n');
                text.Append(reward.RewardKey);
                text.Append(" x");
                text.Append(reward.Amount);
            }
            return text.ToString();
        }

        private void EnsureBuilt()
        {
            if (panelRoot != null) return;

            panelRoot = WorldPanelViewFactory.CreatePanel(transform, "QuestLogPanel");
            titleLabel = WorldPanelViewFactory.CreateText(
                panelRoot.transform,
                "Title",
                WorldTextKeys.QuestLogTitle,
                22,
                new Vector2(0.08f, 0.88f),
                new Vector2(0.72f, 0.96f),
                TextAnchor.MiddleLeft);
            errorLabel = WorldPanelViewFactory.CreateText(
                panelRoot.transform,
                "Error",
                string.Empty,
                16,
                new Vector2(0.08f, 0.8f),
                new Vector2(0.72f, 0.87f),
                TextAnchor.MiddleLeft);
            errorLabel.color = new Color(1f, 0.45f, 0.45f, 1f);
            closeButton = WorldPanelViewFactory.CreateButton(
                panelRoot.transform,
                "Close",
                WorldTextKeys.QuestLogClose,
                new Vector2(0.78f, 0.86f),
                new Vector2(0.92f, 0.94f),
                () => CloseRequested?.Invoke());

            var entries = new GameObject("Entries", typeof(RectTransform));
            entries.transform.SetParent(panelRoot.transform, false);
            entriesRoot = entries.GetComponent<RectTransform>();
            entriesRoot.anchorMin = new Vector2(0.08f, 0.06f);
            entriesRoot.anchorMax = new Vector2(0.92f, 0.78f);
            entriesRoot.offsetMin = Vector2.zero;
            entriesRoot.offsetMax = Vector2.zero;
            panelRoot.SetActive(false);
        }
    }
}
