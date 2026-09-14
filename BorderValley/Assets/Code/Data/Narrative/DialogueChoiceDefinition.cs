using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace BorderValley.Data.Narrative
{
    [Serializable]
    public sealed class DialogueChoiceDefinition
    {
        [SerializeField] private string choiceId = string.Empty;
        [SerializeField] private string labelKey = string.Empty;
        [SerializeField] private DialogueConditionDefinition[] conditions = Array.Empty<DialogueConditionDefinition>();
        [SerializeField] private DialogueActionDefinition[] actions = Array.Empty<DialogueActionDefinition>();
        [SerializeField] private string nextNodeId = string.Empty;

        public DialogueChoiceDefinition()
        {
        }

        public DialogueChoiceDefinition(string choiceId, string labelKey, string nextNodeId)
            : this(choiceId, labelKey, nextNodeId, Array.Empty<DialogueConditionDefinition>(), Array.Empty<DialogueActionDefinition>())
        {
        }

        public DialogueChoiceDefinition(
            string choiceId,
            string labelKey,
            string nextNodeId,
            IEnumerable<DialogueConditionDefinition> conditions,
            IEnumerable<DialogueActionDefinition> actions)
        {
            EditorConfigure(choiceId, labelKey, conditions, actions, nextNodeId);
        }

        public string ChoiceId => choiceId;
        public string Id => choiceId;
        public string LabelKey => labelKey;
        public DialogueConditionDefinition[] Conditions => conditions;
        public DialogueActionDefinition[] Actions => actions;
        public string NextNodeId => nextNodeId;

        public void EditorConfigure(
            string choiceId,
            string labelKey,
            IEnumerable<DialogueConditionDefinition> conditions,
            IEnumerable<DialogueActionDefinition> actions,
            string nextNodeId)
        {
            this.choiceId = choiceId ?? string.Empty;
            this.labelKey = labelKey ?? string.Empty;
            this.conditions = (conditions ?? Array.Empty<DialogueConditionDefinition>()).ToArray();
            this.actions = (actions ?? Array.Empty<DialogueActionDefinition>()).ToArray();
            this.nextNodeId = nextNodeId ?? string.Empty;
        }
    }
}
