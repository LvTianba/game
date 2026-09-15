using System;
using UnityEngine;

namespace BorderValley.Presentation
{
    [Serializable]
    public sealed class VisualClipDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private Sprite[] frames = new Sprite[0];
        [SerializeField] private float framesPerSecond = 1f;
        [SerializeField] private bool loop;
        [SerializeField] private Sprite fallback;

        public VisualClipDefinition(
            string id,
            Sprite[] frames,
            float framesPerSecond,
            bool loop,
            Sprite fallback)
        {
            this.id = id;
            this.frames = frames ?? new Sprite[0];
            this.framesPerSecond = framesPerSecond;
            this.loop = loop;
            this.fallback = fallback;
        }

        public string Id => id;
        public Sprite[] Frames => frames;
        public float FramesPerSecond => framesPerSecond;
        public bool Loop => loop;
        public Sprite Fallback => fallback;

        public static VisualClipDefinition CreateForTests(string id, Sprite fallback)
        {
            var frames = fallback == null ? new Sprite[0] : new[] { fallback };
            return new VisualClipDefinition(id, frames, 1f, false, fallback);
        }
    }
}
