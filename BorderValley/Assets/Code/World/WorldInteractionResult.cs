using BorderValley.Data.World;
using UnityEngine;

namespace BorderValley.World
{
    public sealed class WorldInteractionResult
    {
        public WorldInteractionResult(
            WorldInteractableKind kind,
            string definitionId,
            string targetId,
            Vector2 arrivalPosition,
            string sourceId)
        {
            Kind = kind;
            DefinitionId = definitionId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            ArrivalPosition = arrivalPosition;
            SourceId = sourceId ?? string.Empty;
        }

        public WorldInteractableKind Kind { get; }
        public string DefinitionId { get; }
        public string TargetId { get; }
        public Vector2 ArrivalPosition { get; }
        public string SourceId { get; }
    }
}
