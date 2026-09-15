using UnityEngine;

namespace BorderValley.Presentation
{
    public sealed class NullPresentationService : IPresentationService
    {
        private readonly PresentationService fallback = new PresentationService(null, null);

        public bool IsAvailable => false;
        public Sprite MissingSprite => fallback.MissingSprite;

        public VisualClip GetVisualClip(string clipId) => fallback.GetVisualClip(clipId);
        public Sprite GetSprite(string spriteId) => MissingSprite;
        public AudioCue GetAudioCue(string cueId) => fallback.GetAudioCue(cueId);
        public string GetAreaMusicCueId(string areaId) => string.Empty;
        public string GetCharacterVisualPrefix(string definitionId) => "battle.unit";
        public Sprite GetNpcPortrait(string npcId) => MissingSprite;
        public Sprite GetItemIcon(string itemDefinitionId, string slotId) => MissingSprite;
        public Sprite GetUiSprite(string partId) => MissingSprite;
        public void PlayMusic(string cueId)
        {
        }

        public void PlaySfx(string cueId)
        {
        }

        public void StopMusic()
        {
        }
    }
}
