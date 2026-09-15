namespace BorderValley.Presentation
{
    public sealed class AudioCue
    {
        public AudioCue(
            string id,
            UnityEngine.AudioClip clip,
            float volume,
            bool loop,
            AudioChannel channel)
        {
            Id = id ?? string.Empty;
            Clip = clip;
            Volume = volume;
            Loop = loop;
            Channel = channel;
        }

        public string Id { get; }
        public UnityEngine.AudioClip Clip { get; }
        public float Volume { get; }
        public bool Loop { get; }
        public AudioChannel Channel { get; }
    }
}
