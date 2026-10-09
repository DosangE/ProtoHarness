namespace ProtoHarness.ChainRush.Track
{
    // Stateless integer hashing for seeded course generation, built on SplitMix64 (Steele, Lea & Flood;
    // reference C by Vigna, xoshiro.di.unimi.it/splitmix64.c). Only 64-bit integer adds, xors, shifts and
    // multiplies, so the bits are the same on every platform; doubles come only from Unit's exact scaling.
    public static class SeedHash
    {
        private const ulong Golden = 0x9E3779B97F4A7C15UL;
        private const double UnitScale = 1.0 / (1UL << 53);

        // One SplitMix64 step: advances state by the golden gamma and returns the mixed output.
        public static ulong Next(ref ulong state)
        {
            state += Golden;
            return Mix(state);
        }

        // Mixes seed, index and salt so that changing any of them gives an unrelated value.
        public static ulong Hash(ulong seed, ulong index, ulong salt)
        {
            ulong h = Mix(seed + Golden);
            h = Mix((h ^ index) + Golden);
            return Mix((h ^ salt) + Golden);
        }

        // The top 53 bits as a double in [0, 1).
        public static double Unit(ulong value) => (value >> 11) * UnitScale;

        // A double in [min, max]. Unit stays below 1, but min + (max - min) * Unit can round up to max itself.
        public static double Range(ulong value, double min, double max) => min + (max - min) * Unit(value);

        private static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
