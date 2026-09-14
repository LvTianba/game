using System.Collections.Generic;
using UnityEngine;

namespace BorderValley.Data
{
    [CreateAssetMenu(menuName = "BorderValley/Content Catalog")]
    public sealed class ContentCatalog : ScriptableObject
    {
        [SerializeField] private List<ContentDefinition> definitions = new();
        public IReadOnlyList<ContentDefinition> All => definitions;

#if UNITY_EDITOR
        public void EditorSetDefinitions(IEnumerable<ContentDefinition> values)
        {
            definitions.Clear();
            if (values != null) definitions.AddRange(values);
        }
#endif
    }
}
