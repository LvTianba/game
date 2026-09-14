using System;
using System.Collections.Generic;
using BorderValley.Data.World;
using UnityEngine;

namespace BorderValley.UI.World
{
    public sealed class WorldMapView : MonoBehaviour
    {
        private static Sprite whiteSprite;
        private readonly List<GameObject> rendered = new();
        private readonly Dictionary<WorldInteractableKind, int> markerCounts = new();

        public bool HasBackground { get; private set; }
        public int ObstacleCount { get; private set; }
        public int EncounterMarkerCount { get; private set; }

        public void Render(WorldAreaDefinition area)
        {
            Clear();
            if (area == null)
                return;

            var bounds = area.Bounds;
            CreateBox(
                "Background",
                bounds.center,
                new Vector2(Mathf.Abs(bounds.width), Mathf.Abs(bounds.height)),
                new Color(0.18f, 0.24f, 0.18f, 1f),
                -20);
            HasBackground = true;

            foreach (var obstacle in area.Obstacles)
            {
                CreateBox(
                    "Obstacle_" + ObstacleCount,
                    obstacle.center,
                    new Vector2(Mathf.Abs(obstacle.width), Mathf.Abs(obstacle.height)),
                    new Color(0.10f, 0.12f, 0.13f, 1f),
                    -10);
                ObstacleCount++;
            }

            foreach (var interactable in area.Interactables)
            {
                if (interactable == null || interactable.Kind == WorldInteractableKind.Encounter)
                    continue;
                var name = interactable.Kind + "_" + interactable.Id;
                var color = ColorFor(interactable.Kind);
                CreateBox(name, interactable.Position, Vector2.one * MarkerSize(interactable.Kind), color, 2);
                markerCounts.TryGetValue(interactable.Kind, out var count);
                markerCounts[interactable.Kind] = count + 1;
            }

            foreach (var encounter in area.Encounters)
            {
                if (encounter == null)
                    continue;
                CreateBox(
                    "Encounter_" + encounter.EncounterId,
                    encounter.Position,
                    new Vector2(0.9f, 0.9f),
                    new Color(0.88f, 0.26f, 0.24f, 1f),
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

        private void CreateBox(
            string name,
            Vector2 position,
            Vector2 size,
            Color color,
            int sortingOrder)
        {
            var value = new GameObject(name, typeof(SpriteRenderer));
            value.transform.SetParent(transform, false);
            value.transform.position = new Vector3(position.x, position.y, 0f);
            value.transform.localScale = new Vector3(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y), 1f);
            var renderer = value.GetComponent<SpriteRenderer>();
            renderer.sprite = WhiteSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            rendered.Add(value);
        }

        private static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite == null)
                {
                    whiteSprite = Sprite.Create(
                        Texture2D.whiteTexture,
                        new Rect(0f, 0f, 1f, 1f),
                        new Vector2(0.5f, 0.5f),
                        1f);
                }
                return whiteSprite;
            }
        }

        private static float MarkerSize(WorldInteractableKind kind) => kind switch
        {
            WorldInteractableKind.Npc => 0.8f,
            WorldInteractableKind.Encounter => 0.9f,
            WorldInteractableKind.AreaExit => 1.0f,
            _ => 0.55f
        };

        private static Color ColorFor(WorldInteractableKind kind) => kind switch
        {
            WorldInteractableKind.Npc => new Color(0.38f, 0.72f, 0.92f, 1f),
            WorldInteractableKind.Chest => new Color(0.92f, 0.72f, 0.18f, 1f),
            WorldInteractableKind.Gather => new Color(0.42f, 0.86f, 0.38f, 1f),
            WorldInteractableKind.Investigate => new Color(0.78f, 0.54f, 0.92f, 1f),
            WorldInteractableKind.AreaExit => new Color(0.90f, 0.90f, 0.90f, 1f),
            WorldInteractableKind.Encounter => new Color(0.88f, 0.26f, 0.24f, 1f),
            _ => Color.white
        };
    }
}
