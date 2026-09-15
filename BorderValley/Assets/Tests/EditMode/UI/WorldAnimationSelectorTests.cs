using BorderValley.UI.World;
using NUnit.Framework;
using UnityEngine;

namespace BorderValley.UI.Tests
{
    public sealed class WorldAnimationSelectorTests
    {
        [TestCase(1f, 0f, WorldFacing.East)]
        [TestCase(-1f, 0f, WorldFacing.West)]
        [TestCase(0f, 1f, WorldFacing.North)]
        [TestCase(0f, -1f, WorldFacing.South)]
        public void Resolve_CardinalInput_ReturnsExpectedFacing(
            float x,
            float y,
            WorldFacing expected)
        {
            Assert.That(WorldAnimationSelector.Resolve(new Vector2(x, y)), Is.EqualTo(expected));
        }

        [Test]
        public void Resolve_DiagonalAndNeutralInput_UsesStablePriority()
        {
            Assert.That(
                WorldAnimationSelector.Resolve(new Vector2(1f, 1f)),
                Is.EqualTo(WorldFacing.North));
            Assert.That(
                WorldAnimationSelector.Resolve(new Vector2(-1f, -1f)),
                Is.EqualTo(WorldFacing.South));
            Assert.That(
                WorldAnimationSelector.Resolve(new Vector2(0.2f, 0.9f)),
                Is.EqualTo(WorldFacing.North));
            Assert.That(
                WorldAnimationSelector.Resolve(Vector2.zero),
                Is.EqualTo(WorldFacing.South));
        }

        [TestCase(0f, 0f, false)]
        [TestCase(0.1f, 0f, false)]
        [TestCase(0.1001f, 0f, true)]
        public void IsMoving_AppliesInputThreshold(float x, float y, bool expected)
        {
            Assert.That(WorldAnimationSelector.IsMoving(new Vector2(x, y)), Is.EqualTo(expected));
        }

        [TestCase(WorldFacing.South, false, "world.player.south.idle")]
        [TestCase(WorldFacing.East, true, "world.player.east.walk")]
        [TestCase(WorldFacing.North, false, "world.player.north.idle")]
        [TestCase(WorldFacing.West, true, "world.player.west.walk")]
        public void BuildClipId_UsesFacingAndMovement(
            WorldFacing facing,
            bool moving,
            string expected)
        {
            Assert.That(WorldAnimationSelector.BuildClipId(facing, moving), Is.EqualTo(expected));
        }

        [Test]
        public void BuildClipId_RepeatedCalls_ReturnsCachedIds()
        {
            foreach (var facing in new[]
                     {
                         WorldFacing.South,
                         WorldFacing.East,
                         WorldFacing.North,
                         WorldFacing.West
                     })
            {
                foreach (var moving in new[] { false, true })
                {
                    Assert.That(
                        WorldAnimationSelector.BuildClipId(facing, moving),
                        Is.SameAs(WorldAnimationSelector.BuildClipId(facing, moving)));
                }
            }
        }
    }
}
