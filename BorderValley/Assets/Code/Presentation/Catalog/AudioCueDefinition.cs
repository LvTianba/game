using System;
using UnityEngine;

namespace BorderValley.Presentation
{
    public enum AudioChannel
    {
        Music,
        Ui,
        World,
        Battle
    }

    [Serializable]
    public sealed class AudioCueDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private bool loop;
        [SerializeField] private AudioChannel channel = AudioChannel.Ui;

        public AudioCueDefinition(
            string id,
            AudioClip clip,
            float volume,
            bool loop,
            AudioChannel channel)
        {
            this.id = id;
            this.clip = clip;
            this.volume = volume;
            this.loop = loop;
            this.channel = channel;
        }

        public string Id => id;
        public AudioClip Clip => clip;
        public float Volume => volume;
        public bool Loop => loop;
        public AudioChannel Channel => channel;

        public static AudioCueDefinition CreateForTests(
            string id,
            AudioClip clip,
            float volume = 1f,
            bool loop = false,
            AudioChannel channel = AudioChannel.Ui)
        {
            return new AudioCueDefinition(id, clip, volume, loop, channel);
        }
    }
}
