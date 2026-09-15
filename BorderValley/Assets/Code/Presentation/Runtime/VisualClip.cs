using System;
using UnityEngine;

namespace BorderValley.Presentation
{
    public sealed class VisualClip
    {
        public VisualClip(string id, Sprite[] frames, float framesPerSecond, bool loop, Sprite fallback)
        {
            Id = id ?? string.Empty;
            Frames = frames ?? Array.Empty<Sprite>();
            FramesPerSecond = framesPerSecond;
            Loop = loop;
            Fallback = fallback;
        }

        public string Id { get; }
        public Sprite[] Frames { get; }
        public float FramesPerSecond { get; }
        public bool Loop { get; }
        public Sprite Fallback { get; }
    }
}
