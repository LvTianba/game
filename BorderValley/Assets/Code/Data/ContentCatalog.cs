using System.Collections.Generic;
using UnityEngine;

namespace BorderValley.Data
{
    [CreateAssetMenu(menuName = "BorderValley/Content Catalog")]
    public sealed class ContentCatalog : ScriptableObject
    {
        [SerializeField] private List<ContentDefinition> definitions = new();
        public IReadOnlyList<ContentDefinition> All => definitions;
    }
}
