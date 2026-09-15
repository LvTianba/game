using UnityEngine;

namespace BorderValley.UI.World
{
    public enum WorldFacing
    {
        South,
        East,
        North,
        West
    }

    public static class WorldAnimationSelector
    {
        public static WorldFacing Resolve(Vector2 input)
        {
            if (!IsMoving(input))
                return WorldFacing.South;

            if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
                return input.x >= 0f ? WorldFacing.East : WorldFacing.West;
            return input.y >= 0f ? WorldFacing.North : WorldFacing.South;
        }

        public static string BuildClipId(WorldFacing facing, bool moving)
        {
            return "world.player." + facing.ToString().ToLowerInvariant() + (moving ? ".walk" : ".idle");
        }

        public static bool IsMoving(Vector2 input) => input.magnitude > 0.1f;
    }
}
