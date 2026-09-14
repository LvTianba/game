using BorderValley.Data.World;
using UnityEngine;

namespace BorderValley.World
{
    public static class WorldMovementSolver
    {
        public static bool TryMove(
            Vector2 current,
            Vector2 delta,
            float radius,
            WorldAreaDefinition area,
            out Vector2 result)
        {
            result = current;
            if (area == null || !IsFinite(current) || !IsFinite(delta) || !IsFinite(radius) || radius < 0f)
                return false;

            var bounds = area.Bounds;
            if (!IsFinite(bounds) || bounds.width < 0f || bounds.height < 0f)
                return false;
            if (bounds.width < radius * 2f || bounds.height < radius * 2f)
                return false;

            var minX = bounds.xMin + radius;
            var maxX = bounds.xMax - radius;
            var minY = bounds.yMin + radius;
            var maxY = bounds.yMax - radius;
            result = new Vector2(
                Mathf.Clamp(current.x, minX, maxX),
                Mathf.Clamp(current.y, minY, maxY));

            var xCandidate = new Vector2(
                Mathf.Clamp(result.x + delta.x, minX, maxX),
                result.y);
            if (!IntersectsAnyObstacle(xCandidate, radius, area.Obstacles))
                result.x = xCandidate.x;

            var yCandidate = new Vector2(
                result.x,
                Mathf.Clamp(result.y + delta.y, minY, maxY));
            if (!IntersectsAnyObstacle(yCandidate, radius, area.Obstacles))
                result.y = yCandidate.y;

            return true;
        }

        private static bool IntersectsAnyObstacle(Vector2 center, float radius, Rect[] obstacles)
        {
            if (obstacles == null) return false;
            foreach (var obstacle in obstacles)
            {
                if (!IntersectsObstacle(center, radius, obstacle)) continue;
                return true;
            }
            return false;
        }

        private static bool IntersectsObstacle(Vector2 center, float radius, Rect obstacle)
        {
            if (!IsFinite(obstacle)) return false;

            var minX = Mathf.Min(obstacle.xMin, obstacle.xMax);
            var maxX = Mathf.Max(obstacle.xMin, obstacle.xMax);
            var minY = Mathf.Min(obstacle.yMin, obstacle.yMax);
            var maxY = Mathf.Max(obstacle.yMin, obstacle.yMax);
            var closestX = Mathf.Clamp(center.x, minX, maxX);
            var closestY = Mathf.Clamp(center.y, minY, maxY);
            var offsetX = center.x - closestX;
            var offsetY = center.y - closestY;
            return offsetX * offsetX + offsetY * offsetY <= radius * radius;
        }

        private static bool IsFinite(Vector2 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Rect value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.width) && !float.IsInfinity(value.width) &&
            !float.IsNaN(value.height) && !float.IsInfinity(value.height);
    }
}
