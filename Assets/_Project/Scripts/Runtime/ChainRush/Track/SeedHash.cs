namespace ProtoHarness.ChainRush.Track
{
    // Stateless, platform-independent random numbers for course generation: the same (seed, index, salt)
    // always gives the same bits, using only 64-bit integer operations. SplitMix64 is the fixed-increment
    // generator from https://prng.di.unimi.it/splitmix64.c (Vigna, public domain): z = (x += 0x9e3779b97f4a7c15);
    // z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9; z = (z ^ (z >> 27)) * 0x94d049bb133111eb; return z ^ (z >> 31).
    public static class SeedHash
    {
        private const ulong Increment = 0x9E3779B97F4A7C15UL;
        private const double UnitScale = 1.0 / (1UL << 53);

        // One SplitMix64 step: the output the reference generator gives when its state is `state`.
        public static ulong SplitMix64(ulong state)
        {
            unchecked
            {
                ulong z = state + Increment;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        // Mixes the three inputs one after another, so changing any of them changes every output bit.
        public static ulong Hash(ulong seed, int index, int salt)
        {
            ulong h = SplitMix64(seed);
            h = SplitMix64(h ^ (uint)index);
            return SplitMix64(h ^ ((ulong)(uint)salt << 32));
        }

        // The top 53 bits as a double in [0, 1).
        public static double Unit(ulong hash) => (hash >> 11) * UnitScale;

        public static double Unit(ulong seed, int index, int salt) => Unit(Hash(seed, index, salt));

        // A value in [min, max); min may equal max.
        public static double Range(ulong seed, int index, int salt, double min, double max)
        {
            if (!(min <= max)) throw new System.ArgumentException($"Range needs min <= max, got [{min}, {max}].");
            return min + Unit(seed, index, salt) * (max - min);
        }
    }
}
