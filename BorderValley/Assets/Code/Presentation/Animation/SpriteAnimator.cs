using System;
using UnityEngine;

namespace BorderValley.Presentation
{
    public sealed class SpriteAnimator : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private VisualClip currentClip;
        private Sprite[] frames = Array.Empty<Sprite>();

        public string CurrentClipId { get; private set; } = string.Empty;
        public bool IsPlaying { get; private set; }
        public float ElapsedForTests { get; private set; }

        private void Update() => Tick(Time.unscaledDeltaTime);

        public void Play(VisualClip clip)
        {
            EnsureRenderer();
            currentClip = clip;
            CurrentClipId = clip == null ? string.Empty : clip.Id;
            frames = ResolveFrames(clip);
            ElapsedForTests = 0f;
            IsPlaying = clip != null && clip.FramesPerSecond > 0f && frames.Length > 0;
            ApplyFrame(0);
        }

        public void PlayIfChanged(VisualClip clip)
        {
            if (currentClip != null && clip != null &&
                string.Equals(CurrentClipId, clip.Id, StringComparison.Ordinal))
            {
                return;
            }

            Play(clip);
        }

        public void Stop()
        {
            EnsureRenderer();
            IsPlaying = false;
            currentClip = null;
            frames = Array.Empty<Sprite>();
            CurrentClipId = string.Empty;
            ElapsedForTests = 0f;
        }

        public void Tick(float deltaTime)
        {
            EnsureRenderer();
            if (!IsPlaying || currentClip == null || frames.Length == 0 || deltaTime <= 0f)
                return;

            ElapsedForTests += deltaTime;
            var nextIndex = Mathf.FloorToInt(ElapsedForTests * currentClip.FramesPerSecond);
            if (currentClip.Loop)
            {
                nextIndex %= frames.Length;
            }
            else if (nextIndex >= frames.Length)
            {
                nextIndex = frames.Length - 1;
                IsPlaying = false;
            }

            ApplyFrame(nextIndex);
        }

        private void ApplyFrame(int index)
        {
            if (frames.Length == 0)
                return;

            var frameIndex = Mathf.Clamp(index, 0, frames.Length - 1);
            spriteRenderer.sprite = frames[frameIndex] != null
                ? frames[frameIndex]
                : currentClip?.Fallback;
        }

        private void EnsureRenderer()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                throw new InvalidOperationException("SpriteAnimator requires a SpriteRenderer on the same GameObject.");
        }

        private static Sprite[] ResolveFrames(VisualClip clip)
        {
            if (clip == null)
                return Array.Empty<Sprite>();
            if (clip.Frames != null && clip.Frames.Length > 0)
                return clip.Frames;
            return clip.Fallback == null ? Array.Empty<Sprite>() : new[] { clip.Fallback };
        }
    }
}
