using System;
using System.Collections.Generic;
using UnityEngine;

namespace BorderValley.Data.Narrative
{
    [Serializable]
    public sealed class QuestRewardDefinition
    {
        [SerializeField] private QuestRewardKind kind;
        [SerializeField] private string targetId = string.Empty;
        [SerializeField] private int amount = 1;

        public QuestRewardDefinition()
        {
        }

        public QuestRewardDefinition(QuestRewardKind kind, string targetId, int amount)
        {
            EditorConfigure(kind, targetId, amount);
        }

        public QuestRewardKind Kind => kind;
        public string TargetId => targetId;
        public int Amount => amount;

        public void EditorConfigure(QuestRewardKind kind, string targetId, int amount)
        {
            this.kind = kind;
            this.targetId = targetId ?? string.Empty;
            this.amount = amount;
        }
    }
}
