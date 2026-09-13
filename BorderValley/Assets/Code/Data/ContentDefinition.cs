using UnityEngine;

namespace BorderValley.Data
{
    public class ContentDefinition : ScriptableObject
    {
        [SerializeField] private string id = string.Empty;
        public string Id => id;

#if UNITY_EDITOR
        public void EditorSetId(string value) => id = value;
#endif
    }
}
