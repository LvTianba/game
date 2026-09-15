namespace BorderValley.Presentation
{
    public interface IAudioOutput
    {
        void Play(AudioCue cue);
        void Stop(AudioChannel channel);
        void SetPaused(bool paused);
        string CurrentMusicCueId { get; }
    }
}
