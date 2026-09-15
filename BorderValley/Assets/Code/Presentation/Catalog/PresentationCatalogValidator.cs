using System;
using System.Collections.Generic;

namespace BorderValley.Presentation
{
    public readonly struct PresentationValidationIssue
    {
        public PresentationValidationIssue(string code, string message, object context)
        {
            Code = code;
            Message = message;
            Context = context;
        }

        public string Code { get; }
        public string Message { get; }
        public object Context { get; }
    }

    public static class PresentationCatalogValidator
    {
        public static IEnumerable<PresentationValidationIssue> Validate(PresentationCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            return ValidateCore(catalog);
        }

        private static IEnumerable<PresentationValidationIssue> ValidateCore(PresentationCatalog catalog)
        {
            var visualIds = new HashSet<string>(StringComparer.Ordinal);
            if (catalog.VisualClips != null)
            {
                foreach (var visual in catalog.VisualClips)
                {
                    if (visual == null)
                    {
                        yield return new PresentationValidationIssue(
                            "missing_visual_frames",
                            "A visual clip definition is null.",
                            null);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(visual.Id))
                    {
                        yield return new PresentationValidationIssue(
                            "invalid_visual_id",
                            "Visual clip ID must not be blank.",
                            visual);
                    }
                    else if (!visualIds.Add(visual.Id))
                    {
                        yield return new PresentationValidationIssue(
                            "duplicate_visual_id",
                            "Duplicate visual clip ID: " + visual.Id,
                            visual);
                    }

                    if (visual.Frames == null || visual.Frames.Length == 0)
                    {
                        yield return new PresentationValidationIssue(
                            "missing_visual_frames",
                            "Visual clip has no frames: " + visual.Id,
                            visual);
                    }

                    if (!IsValidFramesPerSecond(visual.FramesPerSecond))
                    {
                        yield return new PresentationValidationIssue(
                            "invalid_visual_fps",
                            "Visual clip FPS must be finite and greater than zero: " + visual.Id,
                            visual);
                    }
                }
            }

            var audioIds = new HashSet<string>(StringComparer.Ordinal);
            if (catalog.AudioCues != null)
            {
                foreach (var audio in catalog.AudioCues)
                {
                    if (audio == null)
                    {
                        yield return new PresentationValidationIssue(
                            "invalid_audio_volume",
                            "An audio cue definition is null.",
                            null);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(audio.Id))
                    {
                        yield return new PresentationValidationIssue(
                            "invalid_audio_id",
                            "Audio cue ID must not be blank.",
                            audio);
                    }
                    else if (!audioIds.Add(audio.Id))
                    {
                        yield return new PresentationValidationIssue(
                            "duplicate_audio_id",
                            "Duplicate audio cue ID: " + audio.Id,
                            audio);
                    }

                    if (!IsValidVolume(audio.Volume))
                    {
                        yield return new PresentationValidationIssue(
                            "invalid_audio_volume",
                            "Audio cue volume must be between zero and one: " + audio.Id,
                            audio);
                    }
                }
            }
        }

        private static bool IsValidFramesPerSecond(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsValidVolume(float value)
        {
            return value >= 0f && value <= 1f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
