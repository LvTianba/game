using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BorderValley.Presentation.Tests
{
    public sealed class AudioDirectorTests
    {
        private sealed class FakeOutput : IAudioOutput
        {
            public readonly List<string> Calls = new();
            public string CurrentMusicCueId { get; private set; }

            public void Play(AudioCue cue)
            {
                Calls.Add("play:" + cue.Id);
                if (cue.Channel == AudioChannel.Music)
                    CurrentMusicCueId = cue.Id;
            }

            public void Stop(AudioChannel channel)
            {
                Calls.Add("stop:" + channel);
                if (channel == AudioChannel.Music)
                    CurrentMusicCueId = string.Empty;
            }

            public void SetPaused(bool paused) => Calls.Add("pause:" + paused);
        }

        [Test]
        public void PlayMusic_SameCue_DoesNotRestart()
        {
            var output = new FakeOutput();
            var director = new AudioDirector(output);
            var cue = new AudioCue("bgm.menu", null, 1f, true, AudioChannel.Music);

            director.PlayMusic(cue);
            director.PlayMusic(cue);

            Assert.That(output.Calls, Is.EqualTo(new[] { "play:bgm.menu" }));
        }

        [Test]
        public void PlayMusic_NewCue_StopsCurrentMusicBeforePlaying()
        {
            var output = new FakeOutput();
            var director = new AudioDirector(output);
            director.PlayMusic(new AudioCue("bgm.menu", null, 1f, true, AudioChannel.Music));
            output.Calls.Clear();

            director.PlayMusic(new AudioCue("bgm.battle", null, 1f, true, AudioChannel.Music));

            Assert.That(output.Calls, Is.EqualTo(new[] { "stop:Music", "play:bgm.battle" }));
        }

        [Test]
        public void PlayUi_NonMusicCue_OnlyPlaysCue()
        {
            var output = new FakeOutput();
            var director = new AudioDirector(output);

            director.PlayUi(new AudioCue("sfx.ui.click", null, 1f, false, AudioChannel.Ui));

            Assert.That(output.Calls, Is.EqualTo(new[] { "play:sfx.ui.click" }));
        }

        [Test]
        public void Pause_True_PausesOutputAndRemembersDesiredMusic()
        {
            var output = new FakeOutput();
            var director = new AudioDirector(output);
            director.PlayMusic(new AudioCue("bgm.menu", null, 1f, true, AudioChannel.Music));

            director.SetPaused(true);

            Assert.That(output.Calls, Does.Contain("pause:True"));
            Assert.That(director.DesiredMusicCueId, Is.EqualTo("bgm.menu"));
        }

        [Test]
        public void Pause_False_SameDesiredMusic_ResumesWithoutRestarting()
        {
            var output = new FakeOutput();
            var director = new AudioDirector(output);
            director.PlayMusic(new AudioCue("bgm.menu", null, 1f, true, AudioChannel.Music));
            director.SetPaused(true);
            output.Calls.Clear();

            director.SetPaused(false);

            Assert.That(output.Calls, Is.EqualTo(new[] { "pause:False" }));
        }

        [Test]
        public void Pause_MusicRequestedWhilePaused_ResumesNewCue()
        {
            var output = new FakeOutput();
            var director = new AudioDirector(output);
            director.PlayMusic(new AudioCue("bgm.menu", null, 1f, true, AudioChannel.Music));
            director.SetPaused(true);
            output.Calls.Clear();

            director.PlayMusic(new AudioCue("bgm.battle", null, 1f, true, AudioChannel.Music));
            Assert.That(output.Calls, Is.Empty);
            Assert.That(director.DesiredMusicCueId, Is.EqualTo("bgm.battle"));

            director.SetPaused(false);

            Assert.That(output.Calls, Is.EqualTo(new[]
            {
                "pause:False",
                "stop:Music",
                "play:bgm.battle"
            }));
        }

        [Test]
        public void StopMusic_StopsOutputAndClearsDesiredMusic()
        {
            var output = new FakeOutput();
            var director = new AudioDirector(output);
            director.PlayMusic(new AudioCue("bgm.menu", null, 1f, true, AudioChannel.Music));
            output.Calls.Clear();

            director.StopMusic();

            Assert.That(output.Calls, Is.EqualTo(new[] { "stop:Music" }));
            Assert.That(director.DesiredMusicCueId, Is.Empty);
        }

        [Test]
        public void Play_RepeatedCueWithinGuard_IgnoresSecondClip()
        {
            var root = new GameObject("audio-output");
            var first = AudioClip.Create("first", 16, 1, 44100, false);
            var second = AudioClip.Create("second", 16, 1, 44100, false);
            try
            {
                var output = root.AddComponent<UnityAudioOutput>();
                output.Initialize();

                output.Play(new AudioCue("sfx.ui.click", first, 1f, false, AudioChannel.Ui));
                output.Play(new AudioCue("sfx.ui.click", second, 1f, false, AudioChannel.Ui));

                var uiSources = root.GetComponentsInChildren<AudioSource>(true)
                    .Where(candidate => candidate.gameObject.name.StartsWith("Ui Audio"))
                    .ToArray();
                Assert.That(uiSources.Count(candidate => candidate.clip != null), Is.EqualTo(1));
                Assert.That(
                    uiSources.Any(candidate => ReferenceEquals(candidate.clip, second)),
                    Is.False);
            }
            finally
            {
                AudioListener.pause = false;
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Play_SeventhUiCue_WhenAllSourcesBusy_DropsNewCue()
        {
            var root = new GameObject("audio-output");
            var clips = Enumerable.Range(0, 7)
                .Select(index => AudioClip.Create("clip-" + index, 44100, 1, 44100, false))
                .ToArray();
            try
            {
                var output = root.AddComponent<UnityAudioOutput>();
                output.Initialize();

                for (var index = 0; index < 6; index++)
                {
                    output.Play(new AudioCue("sfx.ui." + index, clips[index], 1f, false, AudioChannel.Ui));
                }

                output.Play(new AudioCue("sfx.ui.dropped", clips[6], 1f, false, AudioChannel.Ui));

                var uiSources = root.GetComponentsInChildren<AudioSource>(true)
                    .Where(source => source.gameObject.name.StartsWith("Ui Audio"))
                    .ToArray();
                Assert.That(uiSources.All(source => source.isPlaying), Is.True);
                Assert.That(uiSources.Any(source => ReferenceEquals(source.clip, clips[6])), Is.False);
            }
            finally
            {
                AudioListener.pause = false;
                foreach (var clip in clips)
                    Object.DestroyImmediate(clip);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StopMusic_StopsLoopingAndVictorySources()
        {
            var root = new GameObject("audio-output");
            var clip = AudioClip.Create("test", 16, 1, 44100, false);
            try
            {
                var output = root.AddComponent<UnityAudioOutput>();
                output.Initialize();
                var sources = root.GetComponentsInChildren<AudioSource>(true);
                var music = sources.Single(source => source.gameObject.name == "Music Audio 1");
                var victory = sources.Single(source => source.gameObject.name == "Victory Audio 1");
                music.clip = clip;
                victory.clip = clip;

                output.Stop(AudioChannel.Music);

                Assert.That(music.clip, Is.Null);
                Assert.That(victory.clip, Is.Null);
            }
            finally
            {
                AudioListener.pause = false;
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Initialize_CreatesExpectedSourcesWithPlayOnAwakeDisabled()
        {
            var root = new GameObject("audio-output");
            try
            {
                var output = root.AddComponent<UnityAudioOutput>();

                output.Initialize();

                var sources = root.GetComponentsInChildren<AudioSource>(true);
                Assert.That(sources, Has.Length.EqualTo(17));
                Assert.That(sources.All(source => !source.playOnAwake), Is.True);
            }
            finally
            {
                AudioListener.pause = false;
                Object.DestroyImmediate(root);
            }
        }
    }
}
