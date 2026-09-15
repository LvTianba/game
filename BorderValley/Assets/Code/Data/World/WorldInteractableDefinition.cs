using System;
using UnityEngine;

namespace BorderValley.Data.World
{
    [Serializable]
    public sealed class WorldInteractableDefinition
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private WorldInteractableKind kind;
        [SerializeField] private string labelKey = string.Empty;
        [SerializeField] private Vector2 position;
        [SerializeField] private float radius = 1f;
        [SerializeField] private string targetId = string.Empty;
        [SerializeField] private Vector2 arrivalPosition;
        [SerializeField] private string requiredEventId = string.Empty;

        public WorldInteractableDefinition()
        {
        }

        public WorldInteractableDefinition(
            string id,
            WorldInteractableKind kind,
            string labelKey,
            Vector2 position,
            float radius,
            string targetId,
            Vector2 arrivalPosition,
            string requiredEventId)
        {
            EditorConfigure(id, kind, labelKey, position, radius, targetId, arrivalPosition, requiredEventId);
        }

        public string Id => id;
        public WorldInteractableKind Kind => kind;
        public string LabelKey => labelKey;
        public Vector2 Position => position;
        public float Radius => radius;
        public string TargetId => targetId;
        public Vector2 ArrivalPosition => arrivalPosition;
        public string RequiredEventId => requiredEventId;

        public void EditorConfigure(
            string id,
            WorldInteractableKind kind,
            string labelKey,
            Vector2 position,
            float radius,
            string targetId,
            Vector2 arrivalPosition,
            string requiredEventId)
        {
            this.id = id ?? string.Empty;
            this.kind = kind;
            this.labelKey = labelKey ?? string.Empty;
            this.position = position;
            this.radius = radius;
            this.targetId = targetId ?? string.Empty;
            this.arrivalPosition = arrivalPosition;
            this.requiredEventId = requiredEventId ?? string.Empty;
        }
    }
}
