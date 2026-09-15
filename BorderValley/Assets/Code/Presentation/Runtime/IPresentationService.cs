using UnityEngine;

namespace BorderValley.Presentation
{
    public interface IPresentationService
    {
        bool IsAvailable { get; }
        VisualClip GetVisualClip(string clipId);
        Sprite GetSprite(string spriteId);
        AudioCue GetAudioCue(string cueId);
        string GetAreaMusicCueId(string areaId);
        string GetCharacterVisualPrefix(string definitionId);
        Sprite GetNpcPortrait(string npcId);
        Sprite GetItemIcon(string itemDefinitionId, string slotId);
        Sprite GetUiSprite(string partId);
        void PlayMusic(string cueId);
        void PlaySfx(string cueId);
        void StopMusic();
    }

    public interface IPresentationAudio
    {
        void PlayMusic(AudioCue cue);
        void PlaySfx(AudioCue cue);
        void StopMusic();
    }
}
