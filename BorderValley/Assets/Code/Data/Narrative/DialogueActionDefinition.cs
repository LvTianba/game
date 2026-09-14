using System;
using UnityEngine;

namespace BorderValley.Data.Narrative
{
    [Serializable]
    public sealed class DialogueActionDefinition
    {
        [SerializeField] private DialogueActionKind kind;
        [SerializeField] private string targetId = string.Empty;
        [SerializeField] private string objectiveId = string.Empty;
        [SerializeField] private int amount = 1;

        public DialogueActionDefinition()
        {
        }

        public DialogueActionDefinition(DialogueActionKind kind, string targetId, int amount = 1)
            : this(kind, targetId, string.Empty, amount)
        {
        }

        public DialogueActionDefinition(
            DialogueActionKind kind,
            string targetId,
            string objectiveId,
            int amount = 1)
        {
            EditorConfigure(kind, targetId, objectiveId, amount);
        }

        public DialogueActionKind Kind => kind;
        public string TargetId => targetId;
        public string ObjectiveId => objectiveId;
        public int Amount => amount;

        public void EditorConfigure(DialogueActionKind kind, string targetId, int amount)
        {
            EditorConfigure(kind, targetId, string.Empty, amount);
        }

        public void EditorConfigure(
            DialogueActionKind kind,
            string targetId,
            string objectiveId,
            int amount)
        {
            this.kind = kind;
            this.targetId = targetId ?? string.Empty;
            this.objectiveId = objectiveId ?? string.Empty;
            this.amount = amount;
        }
    }
}
