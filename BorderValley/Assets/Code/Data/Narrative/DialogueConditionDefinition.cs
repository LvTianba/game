using System;
using UnityEngine;

namespace BorderValley.Data.Narrative
{
    [Serializable]
    public sealed class DialogueConditionDefinition
    {
        [SerializeField] private DialogueConditionKind kind;
        [SerializeField] private string targetId = string.Empty;
        [SerializeField] private int value;

        public DialogueConditionDefinition()
        {
        }

        public DialogueConditionDefinition(DialogueConditionKind kind, string targetId, int value = 0)
        {
            EditorConfigure(kind, targetId, value);
        }

        public DialogueConditionKind Kind => kind;
        public string TargetId => targetId;
        public int Value => value;
        public int Amount => value;

        public void EditorConfigure(DialogueConditionKind kind, string targetId, int value)
        {
            this.kind = kind;
            this.targetId = targetId ?? string.Empty;
            this.value = value;
        }
    }
}
