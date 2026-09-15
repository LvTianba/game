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
        private const string SouthIdle = "world.player.south.idle";
        private const string SouthWalk = "world.player.south.walk";
        private const string EastIdle = "world.player.east.idle";
        private const string EastWalk = "world.player.east.walk";
        private const string NorthIdle = "world.player.north.idle";
        private const string NorthWalk = "world.player.north.walk";
        private const string WestIdle = "world.player.west.idle";
        private const string WestWalk = "world.player.west.walk";

        public static WorldFacing Resolve(Vector2 input)
        {
            if (!IsMoving(input))
                return WorldFacing.South;

            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
                return input.x >= 0f ? WorldFacing.East : WorldFacing.West;
            return input.y >= 0f ? WorldFacing.North : WorldFacing.South;
        }

        public static string BuildClipId(WorldFacing facing, bool moving) => facing switch
        {
            WorldFacing.South => moving ? SouthWalk : SouthIdle,
            WorldFacing.East => moving ? EastWalk : EastIdle,
            WorldFacing.North => moving ? NorthWalk : NorthIdle,
            WorldFacing.West => moving ? WestWalk : WestIdle,
            _ => SouthIdle
        };

        public static bool IsMoving(Vector2 input) => input.magnitude > 0.1f;
    }
}
