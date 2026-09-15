using System;
using System.Collections.Generic;
using System.Linq;
using BorderValley.Data.World;
using BorderValley.Narrative;
using UnityEngine;

namespace BorderValley.World
{
    public static class WorldInteractionResolver
    {
        public static bool FindNearest(
            Vector2 position,
            WorldAreaDefinition area,
            NarrativeStateService state,
            out WorldInteractionResult result,
            ISet<string> suppressedInteractableIds = null,
            bool allowEncounter = true)
        {
            result = null;
            if (area == null || state == null || !IsFinite(position)) return false;

            WorldInteractableDefinition nearest = null;
            var nearestDistance = float.PositiveInfinity;
            var interactables = area.Interactables;
            if (interactables == null) return false;

            foreach (var interactable in interactables)
            {
                if (!IsEligible(
                        interactable,
                        area,
                        state,
                        suppressedInteractableIds,
                        allowEncounter))
                    continue;

                var offset = interactable.Position - position;
                var distance = offset.sqrMagnitude;
                if (distance > interactable.Radius * interactable.Radius) continue;
                if (distance > nearestDistance) continue;
                if (distance == nearestDistance &&
                    nearest != null &&
                    StringComparer.Ordinal.Compare(interactable.Id, nearest.Id) >= 0)
                    continue;

                nearest = interactable;
                nearestDistance = distance;
            }

            if (nearest == null) return false;
            result = new WorldInteractionResult(
                nearest.Kind,
                nearest.Id,
                nearest.TargetId,
                nearest.ArrivalPosition,
                area.Id);
            return true;
        }

        private static bool IsEligible(
            WorldInteractableDefinition interactable,
            WorldAreaDefinition area,
            NarrativeStateService state,
            ISet<string> suppressedInteractableIds,
            bool allowEncounter)
        {
            if (interactable == null ||
                string.IsNullOrWhiteSpace(interactable.Id) ||
                suppressedInteractableIds != null && suppressedInteractableIds.Contains(interactable.Id) ||
                !IsFinite(interactable.Position) ||
                !IsFinite(interactable.Radius) ||
                interactable.Radius < 0f)
                return false;

            if (!allowEncounter && interactable.Kind == WorldInteractableKind.Encounter)
                return false;

            if (!string.IsNullOrWhiteSpace(interactable.RequiredEventId) &&
                !state.HasEvent(interactable.RequiredEventId))
                return false;

            if (interactable.Kind == WorldInteractableKind.Encounter)
            {
                var encounter = area.Encounters.FirstOrDefault(value =>
                    value != null &&
                    string.Equals(value.EncounterId, interactable.TargetId, StringComparison.Ordinal));
                if (encounter != null &&
                    !encounter.Repeatable &&
                    !string.IsNullOrWhiteSpace(encounter.CompletionEventId) &&
                    state.HasEvent(encounter.CompletionEventId))
                    return false;
            }

            return !IsResolvable(interactable.Kind) ||
                   !state.IsInteractableResolved(interactable.Id);
        }

        private static bool IsResolvable(WorldInteractableKind kind) =>
            kind == WorldInteractableKind.Chest ||
            kind == WorldInteractableKind.Gather ||
            kind == WorldInteractableKind.Investigate;

        private static bool IsFinite(Vector2 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
