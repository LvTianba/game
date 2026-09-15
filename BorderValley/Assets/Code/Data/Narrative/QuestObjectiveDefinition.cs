using System;
using System.Collections.Generic;
using UnityEngine;

namespace BorderValley.Data.Narrative
{
    [Serializable]
    public sealed class QuestObjectiveDefinition
    {
        [SerializeField] private string objectiveId = string.Empty;
        [SerializeField] private QuestObjectiveKind kind;
        [SerializeField] private string targetId = string.Empty;
        [SerializeField] private int requiredCount = 1;
        [SerializeField] private bool consumeOnTurnIn;
        [SerializeField] private string localizationKey = string.Empty;

        public QuestObjectiveDefinition()
        {
        }

        public QuestObjectiveDefinition(
            string objectiveId,
            QuestObjectiveKind kind,
            string targetId,
            int requiredCount,
            bool consumeOnTurnIn,
            string localizationKey)
        {
            EditorConfigure(objectiveId, kind, targetId, requiredCount, consumeOnTurnIn, localizationKey);
        }

        public string ObjectiveId => objectiveId;
        public QuestObjectiveKind Kind => kind;
        public string TargetId => targetId;
        public int RequiredCount => requiredCount;
        public bool ConsumeOnTurnIn => consumeOnTurnIn;
        public string LocalizationKey => localizationKey;

        public void EditorConfigure(
            string objectiveId,
            QuestObjectiveKind kind,
            string targetId,
            int requiredCount,
            bool consumeOnTurnIn,
            string localizationKey)
        {
            this.objectiveId = objectiveId ?? string.Empty;
            this.kind = kind;
            this.targetId = targetId ?? string.Empty;
            this.requiredCount = requiredCount;
            this.consumeOnTurnIn = consumeOnTurnIn;
            this.localizationKey = localizationKey ?? string.Empty;
        }
    }
}
