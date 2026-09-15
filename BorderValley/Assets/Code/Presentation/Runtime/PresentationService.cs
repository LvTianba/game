using System;
using System.Collections.Generic;
using UnityEngine;

namespace BorderValley.Presentation
{
    public sealed class PresentationService : IPresentationService
    {
        private readonly Dictionary<string, VisualClip> clips = new Dictionary<string, VisualClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioCue> cues = new Dictionary<string, AudioCue>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> areaMusic = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> npcVisuals = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> npcPortraits = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> characterVisuals = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> itemIcons = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> uiSprites = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly IPresentationAudio audio;
        private readonly string defaultMusicCueId;
        private readonly VisualClip missingClip;
        private readonly AudioCue silentCue;
        private bool playbackWarningLogged;

        public PresentationService(PresentationCatalog catalog, IPresentationAudio audio)
        {
            this.audio = audio;
            IsAvailable = catalog != null;
            MissingSprite = CreateMissingSprite();

            if (catalog != null)
            {
                defaultMusicCueId = catalog.DefaultMusicCueId;
                AddVisualClips(catalog.VisualClips);
                AddAudioCues(catalog.AudioCues);
                Fill(areaMusic, catalog.AreaMusic);
                Fill(npcVisuals, catalog.NpcVisuals);
                Fill(npcPortraits, catalog.NpcPortraits);
                Fill(characterVisuals, catalog.CharacterVisuals);
                Fill(itemIcons, catalog.ItemIcons);
                Fill(uiSprites, catalog.UiSprites);
            }

            missingClip = new VisualClip("ui.missing", new[] { MissingSprite }, 1f, false, MissingSprite);
            silentCue = new AudioCue(string.Empty, null, 0f, false, AudioChannel.Ui);
        }

        public bool IsAvailable { get; }
        public Sprite MissingSprite { get; }

        public VisualClip GetVisualClip(string clipId)
        {
            if (!string.IsNullOrWhiteSpace(clipId) && clips.TryGetValue(clipId, out var clip))
                return clip;

            return missingClip;
        }

        public Sprite GetSprite(string spriteId)
        {
            if (string.IsNullOrWhiteSpace(spriteId) || !clips.TryGetValue(spriteId, out var clip))
                return MissingSprite;

            foreach (var frame in clip.Frames)
            {
                if (frame != null)
                    return frame;
            }

            return clip.Fallback != null ? clip.Fallback : MissingSprite;
        }

        public AudioCue GetAudioCue(string cueId)
        {
            if (!string.IsNullOrWhiteSpace(cueId) && cues.TryGetValue(cueId, out var cue))
                return cue;

            return silentCue;
        }

        public string GetAreaMusicCueId(string areaId)
        {
            if (!string.IsNullOrWhiteSpace(areaId) && areaMusic.TryGetValue(areaId, out var cueId) &&
                !string.IsNullOrWhiteSpace(cueId))
            {
                return cueId;
            }

            return defaultMusicCueId ?? string.Empty;
        }

        public string GetCharacterVisualPrefix(string definitionId)
        {
            if (!string.IsNullOrWhiteSpace(definitionId) &&
                characterVisuals.TryGetValue(definitionId, out var prefix) &&
                !string.IsNullOrWhiteSpace(prefix))
            {
                return prefix;
            }

            return "battle.unit";
        }

        public Sprite GetNpcPortrait(string npcId)
        {
            return ResolveSprite(npcPortraits, npcId);
        }

        public Sprite GetItemIcon(string itemDefinitionId, string slotId)
        {
            var exact = ResolveSprite(itemIcons, itemDefinitionId);
            if (exact != MissingSprite)
                return exact;

            return ResolveSprite(itemIcons, slotId);
        }

        public Sprite GetUiSprite(string partId)
        {
            return ResolveSprite(uiSprites, partId);
        }

        public void PlayMusic(string cueId)
        {
            if (!TryResolveCue(cueId, out var cue))
                return;

            if (audio == null)
            {
                LogPlaybackWarning("PresentationService has no audio director.");
                return;
            }

            audio.PlayMusic(cue);
        }

        public void PlaySfx(string cueId)
        {
            if (!TryResolveCue(cueId, out var cue))
                return;

            if (audio == null)
            {
                LogPlaybackWarning("PresentationService has no audio director.");
                return;
            }

            audio.PlaySfx(cue);
        }

        public void StopMusic()
        {
            if (audio == null)
                return;

            audio.StopMusic();
        }

        private void AddVisualClips(IReadOnlyList<VisualClipDefinition> definitions)
        {
            if (definitions == null)
                return;

            foreach (var definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                    continue;

                clips[definition.Id] = new VisualClip(
                    definition.Id,
                    definition.Frames,
                    definition.FramesPerSecond,
                    definition.Loop,
                    definition.Fallback);
            }
        }

        private void AddAudioCues(IReadOnlyList<AudioCueDefinition> definitions)
        {
            if (definitions == null)
                return;

            foreach (var definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                    continue;

                cues[definition.Id] = new AudioCue(
                    definition.Id,
                    definition.Clip,
                    definition.Volume,
                    definition.Loop,
                    definition.Channel);
            }
        }

        private Sprite ResolveSprite(Dictionary<string, string> mapping, string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !mapping.TryGetValue(id, out var spriteId))
                return MissingSprite;

            return GetSprite(spriteId);
        }

        private bool TryResolveCue(string cueId, out AudioCue cue)
        {
            if (IsAvailable && !string.IsNullOrWhiteSpace(cueId) && cues.TryGetValue(cueId, out cue))
                return true;

            cue = null;
            LogPlaybackWarning("Presentation cue is unavailable: " + (cueId ?? string.Empty));
            return false;
        }

        private void LogPlaybackWarning(string message)
        {
            if (playbackWarningLogged)
                return;

            playbackWarningLogged = true;
            Debug.LogWarning(message);
        }

        private static void Fill(Dictionary<string, string> target, IReadOnlyList<StringPair> pairs)
        {
            if (pairs == null)
                return;

            foreach (var pair in pairs)
            {
                if (pair == null || string.IsNullOrWhiteSpace(pair.Key))
                    continue;

                target[pair.Key] = pair.Value;
            }
        }

        private static Sprite CreateMissingSprite()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "PresentationMissingTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels(new[] { Color.magenta, Color.magenta, Color.magenta, Color.magenta });
            texture.Apply(false, false);

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f),
                1f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "PresentationMissingSprite";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
