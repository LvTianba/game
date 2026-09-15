using System;
using System.Collections.Generic;
using UnityEngine;

namespace BorderValley.Presentation
{
    [CreateAssetMenu(fileName = "PresentationCatalog", menuName = "BorderValley/Presentation Catalog")]
    public sealed class PresentationCatalog : ScriptableObject
    {
        [SerializeField] private List<VisualClipDefinition> visualClips = new List<VisualClipDefinition>();
        [SerializeField] private List<AudioCueDefinition> audioCues = new List<AudioCueDefinition>();
        [SerializeField] private List<StringPair> areaMusic = new List<StringPair>();
        [SerializeField] private List<StringPair> npcVisuals = new List<StringPair>();
        [SerializeField] private List<StringPair> npcPortraits = new List<StringPair>();
        [SerializeField] private List<StringPair> characterVisuals = new List<StringPair>();
        [SerializeField] private List<StringPair> itemIcons = new List<StringPair>();
        [SerializeField] private List<StringPair> uiSprites = new List<StringPair>();
        [SerializeField] private string defaultMusicCueId;
        [SerializeField] private string missingSpriteId;

        public IReadOnlyList<VisualClipDefinition> VisualClips => visualClips;
        public IReadOnlyList<AudioCueDefinition> AudioCues => audioCues;
        public IReadOnlyList<StringPair> AreaMusic => areaMusic;
        public IReadOnlyList<StringPair> NpcVisuals => npcVisuals;
        public IReadOnlyList<StringPair> NpcPortraits => npcPortraits;
        public IReadOnlyList<StringPair> CharacterVisuals => characterVisuals;
        public IReadOnlyList<StringPair> ItemIcons => itemIcons;
        public IReadOnlyList<StringPair> UiSprites => uiSprites;
        public string DefaultMusicCueId => defaultMusicCueId;
        public string MissingSpriteId => missingSpriteId;

#if UNITY_EDITOR
        public void EditorSetVisualClips(IEnumerable<VisualClipDefinition> values)
        {
            visualClips = Copy(values);
        }

        public void EditorSetAudioCues(IEnumerable<AudioCueDefinition> values)
        {
            audioCues = Copy(values);
        }

        public void EditorSetMapping(string mappingName, IEnumerable<StringPair> values)
        {
            var copy = Copy(values);
            switch (mappingName)
            {
                case "areaMusic":
                case "AreaMusic":
                    areaMusic = copy;
                    break;
                case "npcVisuals":
                case "NpcVisuals":
                    npcVisuals = copy;
                    break;
                case "npcPortraits":
                case "NpcPortraits":
                    npcPortraits = copy;
                    break;
                case "characterVisuals":
                case "CharacterVisuals":
                    characterVisuals = copy;
                    break;
                case "itemIcons":
                case "ItemIcons":
                    itemIcons = copy;
                    break;
                case "uiSprites":
                case "UiSprites":
                    uiSprites = copy;
                    break;
                default:
                    throw new ArgumentException("Unknown presentation mapping: " + mappingName, nameof(mappingName));
            }
        }

        public void EditorSetDefaultMusicCueId(string value)
        {
            defaultMusicCueId = value;
        }

        public void EditorSetMissingSpriteId(string value)
        {
            missingSpriteId = value;
        }

        private static List<T> Copy<T>(IEnumerable<T> values)
        {
            return values == null ? new List<T>() : new List<T>(values);
        }
#endif
    }
}
