using System;
using System.Collections.Generic;
using UnityEngine;

namespace BorderValley.Presentation
{
    public sealed class UnityAudioOutput : MonoBehaviour, IAudioOutput
    {
        private const float DuplicateGuardSeconds = 0.05f;

        private readonly List<AudioSource> musicSources = new List<AudioSource>();
        private readonly List<AudioSource> battleSources = new List<AudioSource>();
        private readonly List<AudioSource> uiSources = new List<AudioSource>();
        private readonly List<AudioSource> worldSources = new List<AudioSource>();
        private readonly List<AudioSource> victorySources = new List<AudioSource>();
        private readonly Dictionary<string, float> lastPlayedAt = new Dictionary<string, float>(StringComparer.Ordinal);
        private bool initialized;

        public string CurrentMusicCueId { get; private set; } = string.Empty;

        public void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            CreatePool("Music", musicSources, 1);
            CreatePool("Battle", battleSources, 8);
            CreatePool("Ui", uiSources, 6);
            CreatePool("World", worldSources, 1);
            CreatePool("Victory", victorySources, 1);
        }

        public void Play(AudioCue cue)
        {
            if (cue == null || cue.Clip == null)
                return;

            var now = Time.unscaledTime;
            if (!string.IsNullOrWhiteSpace(cue.Id) &&
                lastPlayedAt.TryGetValue(cue.Id, out var lastPlayed) &&
                now - lastPlayed < DuplicateGuardSeconds)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(cue.Id))
                lastPlayedAt[cue.Id] = now;

            var source = FindFreeSource(cue);
            if (source == null)
                return;

            source.Stop();
            source.clip = cue.Clip;
            source.volume = Mathf.Clamp01(cue.Volume);
            source.loop = cue.Channel == AudioChannel.Music && cue.Loop;
            source.Play();

            if (cue.Channel == AudioChannel.Music)
                CurrentMusicCueId = cue.Id;
        }

        public void Stop(AudioChannel channel)
        {
            if (channel == AudioChannel.Music)
            {
                StopSources(musicSources);
                StopSources(victorySources);
                CurrentMusicCueId = string.Empty;
                return;
            }

            StopSources(GetPool(channel, false));
        }

        public void SetPaused(bool paused)
        {
            AudioListener.pause = paused;
        }

        private void Awake()
        {
            Initialize();
        }

        private void CreatePool(string poolName, ICollection<AudioSource> pool, int count)
        {
            for (var index = 0; index < count; index++)
            {
                var child = new GameObject(poolName + " Audio " + (index + 1));
                child.transform.SetParent(transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                pool.Add(source);
            }
        }

        private static void StopSources(IEnumerable<AudioSource> sources)
        {
            foreach (var source in sources)
            {
                source.Stop();
                source.clip = null;
            }
        }

        private AudioSource FindFreeSource(AudioCue cue)
        {
            foreach (var source in GetPool(cue.Channel, cue.Loop))
            {
                if (!source.isPlaying)
                    return source;
            }

            return null;
        }

        private IEnumerable<AudioSource> GetPool(AudioChannel channel, bool loop)
        {
            switch (channel)
            {
                case AudioChannel.Music:
                    return loop ? musicSources : victorySources;
                case AudioChannel.Battle:
                    return battleSources;
                case AudioChannel.Ui:
                    return uiSources;
                case AudioChannel.World:
                    return worldSources;
                default:
                    return Array.Empty<AudioSource>();
            }
        }
    }
}
