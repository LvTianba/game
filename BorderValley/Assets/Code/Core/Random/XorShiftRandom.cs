using System;

namespace BorderValley.Core.Random
{
    public sealed class XorShiftRandom : IRandomSource
    {
        private uint state;

        public XorShiftRandom(uint seed)
        {
            state = seed == 0 ? 0x6D2B79F5u : seed;
        }

        public uint NextUInt()
        {
            var value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return value;
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            var span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % span);
        }

        public float Value01() => (NextUInt() & 0x00FFFFFFu) / 16777216f;

        public IRandomSource Fork(string label)
        {
            return new XorShiftRandom(RandomSourceFactory.StableHash(label + ":" + state));
        }
    }
}
