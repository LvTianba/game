using System;
using System.Collections.Generic;
using BorderValley.Data.World;
using BorderValley.Narrative;
using BorderValley.Presentation;
using UnityEngine;

namespace BorderValley.UI.World
{
    public sealed class WorldMapView : MonoBehaviour
    {
        private readonly List<GameObject> rendered = new();
        private readonly Dictionary<WorldInteractableKind, int> markerCounts = new();
        private IPresentationService presentation;

        public bool HasBackground { get; private set; }
        public int ObstacleCount { get; private set; }
        public int EncounterMarkerCount { get; private set; }
        public int RenderedSpriteCount => rendered.Count;

        public void Render(WorldAreaDefinition area) => Render(area, null);

        public void Render(WorldAreaDefinition area, NarrativeStateService state) =>
            Render(area, state, null);

        public void Render(
            WorldAreaDefinition area,
            NarrativeStateService state,
            IPresentationService service)
        {
            if (service != null)
                presentation = service;
            if (presentation == null)
                presentation = new NullPresentationService();

            Clear();
            if (area == null)
                return;

            var bounds = area.Bounds;
            var groundClipId = "world.ground." + AreaSuffix(area.Id);
            var groundClip = presentation.GetVisualClip(groundClipId);
            CreateVisual(
                "Background",
                bounds.center,
                new Vector2(Mathf.Abs(bounds.width), Mathf.Abs(bounds.height)),
                groundClip,
                true,
                -20);
            HasBackground = true;

            foreach (var obstacle in area.Obstacles)
            {
                CreateVisual(
                    "Obstacle_" + ObstacleCount,
                    obstacle.center,
                    new Vector2(Mathf.Abs(obstacle.width), Mathf.Abs(obstacle.height)),
                    groundClip,
                    true,
                    -10);
                ObstacleCount++;
            }

            foreach (var interactable in area.Interactables)
            {
                if (interactable == null || interactable.Kind == WorldInteractableKind.Encounter)
                    continue;
                var name = interactable.Kind + "_" + interactable.Id;
                CreateVisual(
                    name,
                    interactable.Position,
                    Vector2.one,
                    presentation.GetVisualClip(ClipIdFor(interactable)),
                    false,
                    2);
                markerCounts.TryGetValue(interactable.Kind, out var count);
                markerCounts[interactable.Kind] = count + 1;
            }

            foreach (var encounter in area.Encounters)
            {
                if (!ShouldRenderEncounter(encounter, state))
                    continue;
                CreateVisual(
                    "Encounter_" + encounter.EncounterId,
                    encounter.Position,
                    Vector2.one,
                    presentation.GetVisualClip("world.marker.encounter.idle"),
                    false,
                    5);
                EncounterMarkerCount++;
            }
        }

        public int MarkerCount(WorldInteractableKind kind) =>
            markerCounts.TryGetValue(kind, out var count) ? count : 0;

        private void Clear()
        {
            for (var index = rendered.Count - 1; index >= 0; index--)
            {
                var value = rendered[index];
                if (value == null)
                    continue;
                value.SetActive(false);
                if (Application.isPlaying)
                    Destroy(value);
                else
                    DestroyImmediate(value);
            }
            rendered.Clear();
            markerCounts.Clear();
            HasBackground = false;
            ObstacleCount = 0;
            EncounterMarkerCount = 0;
        }

        private void CreateVisual(
            string name,
            Vector2 position,
            Vector2 size,
            VisualClip clip,
            bool tiled,
            int sortingOrder)
        {
            var value = new GameObject(name, typeof(SpriteRenderer));
            value.transform.SetParent(transform, false);
            value.transform.position = new Vector3(position.x, position.y, 0f);
            var renderer = value.GetComponent<SpriteRenderer>();
            renderer.sprite = FirstFrame(clip);
            renderer.color = Color.white;
            renderer.drawMode = tiled ? SpriteDrawMode.Tiled : SpriteDrawMode.Simple;
            if (tiled)
                renderer.size = new Vector2(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y));
            renderer.sortingOrder = sortingOrder;
            rendered.Add(value);

            if (clip != null && clip.Frames != null && clip.Frames.Length > 1)
                value.AddComponent<SpriteAnimator>().Play(clip);
        }

        private static bool ShouldRenderEncounter(
            WorldEncounterDefinition encounter,
            NarrativeStateService state)
        {
            if (encounter == null)
                return false;
            return encounter.Repeatable ||
                   string.IsNullOrWhiteSpace(encounter.CompletionEventId) ||
                   state == null ||
                   !state.HasEvent(encounter.CompletionEventId);
        }
        private static Sprite FirstFrame(VisualClip clip)
        {
            if (clip?.Frames != null)
            {
                foreach (var frame in clip.Frames)
                {
                    if (frame != null)
                        return frame;
                }
            }

            return clip?.Fallback;
        }

        private static string ClipIdFor(WorldInteractableDefinition interactable) => interactable.Kind switch
        {
            WorldInteractableKind.Npc =>
                "world.npc." +
                WithoutPrefix(interactable.TargetId, "npc.").Replace("_companion", string.Empty) +
                ".idle",
            WorldInteractableKind.Chest => "world.marker.chest.idle",
            WorldInteractableKind.Gather => "world.marker.gather.idle",
            WorldInteractableKind.Investigate => "world.marker.investigate.idle",
            WorldInteractableKind.AreaExit => "world.marker.area_exit.idle",
            _ => "world.marker." + interactable.Kind.ToString().ToLowerInvariant() + ".idle"
        };

        private static string AreaSuffix(string areaId)
        {
            const string prefix = "area.";
            return !string.IsNullOrWhiteSpace(areaId) &&
                   areaId.StartsWith(prefix, StringComparison.Ordinal)
                ? areaId.Substring(prefix.Length)
                : areaId;
        }

        private static string WithoutPrefix(string value, string prefix)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.StartsWith(prefix, StringComparison.Ordinal)
                ? value.Substring(prefix.Length)
                : value;
        }
    }
}
