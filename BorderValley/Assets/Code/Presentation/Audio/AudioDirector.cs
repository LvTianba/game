using System;

namespace BorderValley.Presentation
{
    public sealed class AudioDirector : IPresentationAudio
    {
        private readonly IAudioOutput output;
        private AudioCue desiredMusic;
        private bool paused;

        public AudioDirector(IAudioOutput output)
        {
            this.output = output;
        }

        public string DesiredMusicCueId => desiredMusic?.Id ?? string.Empty;

        public void PlayMusic(AudioCue cue)
        {
            if (cue == null || string.IsNullOrWhiteSpace(cue.Id) || cue.Channel != AudioChannel.Music)
                return;

            desiredMusic = cue;
            if (paused || output == null)
                return;

            if (string.Equals(output.CurrentMusicCueId, cue.Id, StringComparison.Ordinal))
                return;

            if (!string.IsNullOrEmpty(output.CurrentMusicCueId))
                output.Stop(AudioChannel.Music);
            output.Play(cue);
        }

        public void PlaySfx(AudioCue cue)
        {
            if (cue == null || cue.Channel == AudioChannel.Music)
                return;

            output?.Play(cue);
        }

        public void PlayUi(AudioCue cue) => PlaySfx(cue);

        public void PlayWorld(AudioCue cue) => PlaySfx(cue);

        public void PlayBattle(AudioCue cue) => PlaySfx(cue);

        public void StopMusic()
        {
            desiredMusic = null;
            output?.Stop(AudioChannel.Music);
        }

        public void SetPaused(bool value)
        {
            paused = value;
            output?.SetPaused(value);
            if (value || desiredMusic == null || output == null)
                return;

            if (string.Equals(output.CurrentMusicCueId, desiredMusic.Id, StringComparison.Ordinal))
                return;

            output.Stop(AudioChannel.Music);
            output.Play(desiredMusic);
        }
    }
}
