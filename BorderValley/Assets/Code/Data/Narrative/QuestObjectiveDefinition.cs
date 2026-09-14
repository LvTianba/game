using System;
using System.Collections.Generic;
using UnityEngine;

namespace BorderValley.Data.Narrative
{
    [Serializable]
    public sealed class QuestObjectiveDefinition
    {
        [SerializeField] private QuestObjectiveKind kind;
        [SerializeField] private string targetId = string.Empty;
        [SerializeField] private int requiredCount = 1;
        [SerializeField] private bool consumeOnTurnIn;
        [SerializeField] private string localizationKey = string.Empty;

        public QuestObjectiveDefinition()
        {
        }

        public QuestObjectiveDefinition(
            QuestObjectiveKind kind,
            string targetId,
            int requiredCount,
            bool consumeOnTurnIn,
            string localizationKey)
        {
            EditorConfigure(kind, targetId, requiredCount, consumeOnTurnIn, localizationKey);
        }

        public QuestObjectiveKind Kind => kind;
        public string TargetId => targetId;
        public int RequiredCount => requiredCount;
        public bool ConsumeOnTurnIn => consumeOnTurnIn;
        public string LocalizationKey => localizationKey;

        public void EditorConfigure(
            QuestObjectiveKind kind,
            string targetId,
            int requiredCount,
            bool consumeOnTurnIn,
            string localizationKey)
        {
            this.kind = kind;
            this.targetId = targetId ?? string.Empty;
            this.requiredCount = requiredCount;
            this.consumeOnTurnIn = consumeOnTurnIn;
            this.localizationKey = localizationKey ?? string.Empty;
        }
    }
}
