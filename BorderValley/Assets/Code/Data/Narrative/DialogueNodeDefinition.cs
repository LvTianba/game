using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace BorderValley.Data.Narrative
{
    [Serializable]
    public sealed class DialogueNodeDefinition
    {
        [SerializeField] private string nodeId = string.Empty;
        [SerializeField] private string speakerNpcId = string.Empty;
        [SerializeField] private string textKey = string.Empty;
        [SerializeField] private DialogueConditionDefinition[] conditions = Array.Empty<DialogueConditionDefinition>();
        [SerializeField] private DialogueActionDefinition[] actions = Array.Empty<DialogueActionDefinition>();
        [SerializeField] private DialogueChoiceDefinition[] choices = Array.Empty<DialogueChoiceDefinition>();
        [SerializeField] private string nextNodeId = string.Empty;

        public DialogueNodeDefinition()
        {
        }

        public DialogueNodeDefinition(
            string nodeId,
            string speakerNpcId,
            string textKey,
            IEnumerable<DialogueConditionDefinition> conditions,
            IEnumerable<DialogueActionDefinition> actions,
            IEnumerable<DialogueChoiceDefinition> choices,
            string nextNodeId)
        {
            EditorConfigure(nodeId, speakerNpcId, textKey, conditions, actions, choices, nextNodeId);
        }

        public string NodeId => nodeId;
        public string SpeakerNpcId => speakerNpcId;
        public string TextKey => textKey;
        public DialogueConditionDefinition[] Conditions => conditions;
        public DialogueActionDefinition[] Actions => actions;
        public DialogueChoiceDefinition[] Choices => choices;
        public string NextNodeId => nextNodeId;

        public void EditorConfigure(
            string nodeId,
            string speakerNpcId,
            string textKey,
            IEnumerable<DialogueConditionDefinition> conditions,
            IEnumerable<DialogueActionDefinition> actions,
            IEnumerable<DialogueChoiceDefinition> choices,
            string nextNodeId)
        {
            this.nodeId = nodeId ?? string.Empty;
            this.speakerNpcId = speakerNpcId ?? string.Empty;
            this.textKey = textKey ?? string.Empty;
            this.conditions = (conditions ?? Array.Empty<DialogueConditionDefinition>()).ToArray();
            this.actions = (actions ?? Array.Empty<DialogueActionDefinition>()).ToArray();
            this.choices = (choices ?? Array.Empty<DialogueChoiceDefinition>()).ToArray();
            this.nextNodeId = nextNodeId ?? string.Empty;
        }
    }
}
