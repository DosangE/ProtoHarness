using NUnit.Framework;
using ProtoHarness.ChainRush.Track;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class SeedHashTests
    {
        // Reference outputs from rust-random rand_xoshiro src/splitmix64.rs, test `reference`
        // (seed_from_u64 stores the seed as the state), first five of fifty. Checked 2026-10-09.
        [Test]
        public void Next_ReferenceSeed_MatchesPublishedSplitMix64()
        {
            ulong state = 1477776061723855037UL;
            ulong[] expected =
            {
                1985237415132408290UL,
                2979275885539914483UL,
                13511426838097143398UL,
                8488337342461049707UL,
                15141737807933549159UL,
            };
            for (int i = 0; i < expected.Length; i++)
                Assert.That(SeedHash.Next(ref state), Is.EqualTo(expected[i]), $"output {i}");
        }

        // Pinned values from an independent Python implementation of the same composition
        // (Mix(Mix(Mix(seed) ^ index) ^ salt) with SplitMix64 steps), 2026-10-09. Changing Hash changes every course.
        [Test]
        public void Hash_KnownInputs_MatchPinnedValues()
        {
            Assert.That(SeedHash.Hash(0UL, 0UL, 0UL), Is.EqualTo(2558736989570252433UL));
            Assert.That(SeedHash.Hash(7UL, 3UL, 1UL), Is.EqualTo(1173472824657711729UL));
            Assert.That(SeedHash.Hash(19UL, 1000UL, 7UL), Is.EqualTo(11837551187196038842UL));
        }

        [Test]
        public void Hash_SameInputs_ReturnsSameValue()
        {
            Assert.That(SeedHash.Hash(42UL, 17UL, 3UL), Is.EqualTo(SeedHash.Hash(42UL, 17UL, 3UL)));
        }

        [Test]
        public void Hash_SeedIndexOrSaltChanged_ReturnsDifferentValue()
        {
            ulong baseline = SeedHash.Hash(42UL, 17UL, 3UL);
            Assert.That(SeedHash.Hash(43UL, 17UL, 3UL), Is.Not.EqualTo(baseline));
            Assert.That(SeedHash.Hash(42UL, 18UL, 3UL), Is.Not.EqualTo(baseline));
            Assert.That(SeedHash.Hash(42UL, 17UL, 4UL), Is.Not.EqualTo(baseline));
            // Swapping index and salt must not collide either.
            Assert.That(SeedHash.Hash(42UL, 3UL, 17UL), Is.Not.EqualTo(baseline));
        }

        [Test]
        public void Unit_Extremes_StayInsideZeroToOne()
        {
            Assert.That(SeedHash.Unit(0UL), Is.EqualTo(0d));
            Assert.That(SeedHash.Unit(ulong.MaxValue), Is.LessThan(1d));
            Assert.That(SeedHash.Unit(ulong.MaxValue), Is.EqualTo(1d - 1d / (1UL << 53)));
        }

        [Test]
        public void Unit_ManyHashes_StayInsideZeroToOneAndSpreadEvenly()
        {
            const int count = 10000;
            int lowerHalf = 0;
            for (ulong i = 0; i < count; i++)
            {
                double unit = SeedHash.Unit(SeedHash.Hash(5UL, i, 0UL));
                Assert.That(unit, Is.GreaterThanOrEqualTo(0d).And.LessThan(1d));
                if (unit < 0.5d) lowerHalf++;
            }
            // Binomial(10000, 0.5) has standard deviation 50; 300 is six of them.
            Assert.That(lowerHalf, Is.InRange(count / 2 - 300, count / 2 + 300));
        }

        [Test]
        public void Range_Extremes_StayInsideMinToMax()
        {
            Assert.That(SeedHash.Range(0UL, 5d, 8d), Is.EqualTo(5d));
            // 5 + 3 * (1 - 2^-53) rounds to 8 in double, so max is reachable (observed 2026-10-09).
            Assert.That(SeedHash.Range(ulong.MaxValue, 5d, 8d), Is.LessThanOrEqualTo(8d).And.GreaterThan(7.999d));
            Assert.That(SeedHash.Range(ulong.MaxValue, 16d, 16d), Is.EqualTo(16d));
        }
    }
}
