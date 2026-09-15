using System;
using UnityEngine;

namespace BorderValley.Data.Narrative
{
    [CreateAssetMenu(menuName = "BorderValley/Narrative/Npc Definition", fileName = "NpcDefinition")]
    public sealed class NpcDefinition : ContentDefinition
    {
        [SerializeField] private string localizationKey = string.Empty;
        [SerializeField] private string portraitKey = string.Empty;
        [SerializeField] private string dialogueId = string.Empty;
        [SerializeField] private string openShopId = string.Empty;
        [SerializeField] private int initialFavorTier;

        public string LocalizationKey => localizationKey;
        public string DisplayNameKey => localizationKey;
        public string PortraitKey => portraitKey;
        public string DialogueId => dialogueId;
        public string OpenShopId => openShopId;
        public int InitialFavorTier => initialFavorTier;
        public int DefaultFavorTier => initialFavorTier;

#if UNITY_EDITOR
        public void EditorConfigure(string id, string localizationKey, string dialogueId, string openShopId)
        {
            EditorConfigure(id, localizationKey, dialogueId, openShopId, 0);
        }

        public void EditorConfigure(
            string id,
            string localizationKey,
            string dialogueId,
            string openShopId,
            int initialFavorTier)
        {
            EditorSetId(id);
            this.localizationKey = localizationKey ?? string.Empty;
            this.portraitKey = localizationKey ?? string.Empty;
            this.dialogueId = dialogueId ?? string.Empty;
            this.openShopId = openShopId ?? string.Empty;
            this.initialFavorTier = initialFavorTier;
        }
#endif
    }
}
