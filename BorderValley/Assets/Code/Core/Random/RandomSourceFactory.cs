namespace BorderValley.Core.Random
{
    public static class RandomSourceFactory
    {
        public static IRandomSource FromSeed(string seed) => new XorShiftRandom(StableHash(seed));

        public static uint StableHash(string value)
        {
            unchecked
            {
                const uint offset = 2166136261u;
                const uint prime = 16777619u;
                var hash = offset;
                foreach (var character in value)
                {
                    hash ^= character;
                    hash *= prime;
                }
                return hash;
            }
        }
    }
}
