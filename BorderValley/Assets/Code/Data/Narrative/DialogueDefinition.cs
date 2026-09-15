using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace BorderValley.Data.Narrative
{
    [CreateAssetMenu(menuName = "BorderValley/Narrative/Dialogue Definition", fileName = "DialogueDefinition")]
    public sealed class DialogueDefinition : ContentDefinition
    {
        [SerializeField] private string startNodeId = string.Empty;
        [SerializeField] private DialogueNodeDefinition[] nodes = Array.Empty<DialogueNodeDefinition>();

        public string StartNodeId => startNodeId;
        public DialogueNodeDefinition[] Nodes => nodes;

#if UNITY_EDITOR
        public void EditorConfigure(string id, string startNodeId, IEnumerable<DialogueNodeDefinition> nodes)
        {
            EditorSetId(id);
            this.startNodeId = startNodeId ?? string.Empty;
            this.nodes = (nodes ?? Array.Empty<DialogueNodeDefinition>()).ToArray();
        }
#endif
    }
}
