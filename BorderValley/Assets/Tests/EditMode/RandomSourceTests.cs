using BorderValley.Core.Random;
using NUnit.Framework;

namespace BorderValley.Core.Tests
{
    public sealed class RandomSourceTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var first = RandomSourceFactory.FromSeed("loot-floor-1");
            var second = RandomSourceFactory.FromSeed("loot-floor-1");
            for (var i = 0; i < 20; i++)
                Assert.That(first.NextUInt(), Is.EqualTo(second.NextUInt()));
        }

        [Test]
        public void Range_IsHalfOpen()
        {
            var random = RandomSourceFactory.FromSeed("range-test");
            for (var i = 0; i < 1000; i++)
            {
                var value = random.Range(3, 8);
                Assert.That(value, Is.GreaterThanOrEqualTo(3));
                Assert.That(value, Is.LessThan(8));
            }
        }

        [Test]
        public void Fork_DifferentLabels_ProducesDifferentStreams()
        {
            var root = RandomSourceFactory.FromSeed("world-42");
            Assert.That(root.Fork("item").NextUInt(), Is.Not.EqualTo(root.Fork("ai").NextUInt()));
        }
    }
}
