namespace NAV.World.Generation
{
    /// <summary>
    /// Order-independent random numbers for world content: every value is a hash of (seed, what,
    /// where), so a chunk can be built in any order, any number of times, and always gets the same
    /// trees in the same places - which chunk streaming needs (a System.Random walked across the
    /// whole map can't be resumed in the middle). Murmur3-style integer finalizer per input.
    /// </summary>
    public static class WorldHash
    {
        public static uint Hash(int seed, int a, int b, int c, int d = 0)
        {
            uint h = Mix((uint)seed ^ 0x9E3779B9u);
            h = Mix(h ^ (uint)a);
            h = Mix(h ^ (uint)b * 0x85EBCA6Bu);
            h = Mix(h ^ (uint)c * 0xC2B2AE35u);
            return Mix(h ^ (uint)d * 0x27D4EB2Fu);
        }

        /// <summary>A float in [0, 1) from a hash.</summary>
        public static float Value(uint hash) => (hash >> 8) * (1f / 16777216f);

        /// <summary>The n-th independent value [0, 1) for one (seed, a, b, c).</summary>
        public static float Value(int seed, int a, int b, int c, int n) => Value(Hash(seed, a, b, c, n));

        private static uint Mix(uint x)
        {
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }
    }
}
