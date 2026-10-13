using System;
using NUnit.Framework;
using ProtoHarness.ChainRush.Track;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class SeedHashTests
    {
        private const ulong Increment = 0x9E3779B97F4A7C15UL;

        // Reference values: the SplitMix64 source (https://prng.di.unimi.it/splitmix64.c) re-implemented in
        // Python 3.12 and run from state 0, step by step. Hash vectors come from the same Python port of Hash.
        [Test]
        public void SplitMix64_StatesFromZero_MatchReferenceStream()
        {
            Assert.That(SeedHash.SplitMix64(0UL), Is.EqualTo(0xE220A8397B1DCDAFUL));
            Assert.That(SeedHash.SplitMix64(Increment), Is.EqualTo(0x6E789E6AA1B965F4UL));
            Assert.That(SeedHash.SplitMix64(unchecked(Increment * 2UL)), Is.EqualTo(0x06C45D188009454FUL));
            Assert.That(SeedHash.SplitMix64(unchecked(Increment * 3UL)), Is.EqualTo(0xF88BB8A8724C81ECUL));
            Assert.That(SeedHash.SplitMix64(1UL), Is.EqualTo(0x910A2DEC89025CC1UL));
        }

        [Test]
        public void Hash_ReferenceInputs_MatchPortedValues()
        {
            Assert.That(SeedHash.Hash(0UL, 0, 0), Is.EqualTo(0x238275BC38FCBE91UL));
            Assert.That(SeedHash.Hash(12345UL, 7, 3), Is.EqualTo(0xEA09716EAA8269B9UL));
            Assert.That(SeedHash.Hash(12345UL, 8, 3), Is.EqualTo(0x7021094B0F7FDF11UL));
            Assert.That(SeedHash.Hash(12345UL, 7, 4), Is.EqualTo(0x7F36D83D9259CB47UL));
            Assert.That(SeedHash.Hash(12346UL, 7, 3), Is.EqualTo(0x72A85FBAAAC93C0CUL));
            Assert.That(SeedHash.Hash(ulong.MaxValue, int.MaxValue, 31), Is.EqualTo(0xAE2168117BCF5B56UL));
        }

        [Test]
        public void Hash_SameInputs_ReturnsSameValue()
        {
            Assert.That(SeedHash.Hash(99UL, 5, 2), Is.EqualTo(SeedHash.Hash(99UL, 5, 2)));
        }

        [Test]
        public void Hash_ChangingAnyInput_ChangesValue()
        {
            ulong baseline = SeedHash.Hash(99UL, 5, 2);
            Assert.That(SeedHash.Hash(100UL, 5, 2), Is.Not.EqualTo(baseline));
            Assert.That(SeedHash.Hash(99UL, 6, 2), Is.Not.EqualTo(baseline));
            Assert.That(SeedHash.Hash(99UL, 5, 3), Is.Not.EqualTo(baseline));
            Assert.That(SeedHash.Hash(99UL, 2, 5), Is.Not.EqualTo(baseline), "index and salt must not be interchangeable");
        }

        [Test]
        public void Unit_EdgeHashes_StaysInHalfOpenUnitInterval()
        {
            Assert.That(SeedHash.Unit(0UL), Is.EqualTo(0d));
            Assert.That(SeedHash.Unit(ulong.MaxValue), Is.LessThan(1d));
            Assert.That(SeedHash.Unit(ulong.MaxValue), Is.GreaterThan(0.999999999));
        }

        [Test]
        public void Unit_ReferenceInput_MatchesPortedValue()
        {
            Assert.That(SeedHash.Unit(12345UL, 7, 3), Is.EqualTo(0.9142065901928703d));
        }

        [Test]
        public void Unit_ManySamples_StayInRangeWithPlausibleMean()
        {
            const int count = 100000;
            double sum = 0d;
            for (int i = 0; i < count; i++)
            {
                double value = SeedHash.Unit(7UL, i, 1);
                Assert.That(value, Is.GreaterThanOrEqualTo(0d).And.LessThan(1d));
                sum += value;
            }
            Assert.That(sum / count, Is.EqualTo(0.5d).Within(0.01d));
        }

        [Test]
        public void Range_Samples_StayInsideBounds()
        {
            for (int i = 0; i < 1000; i++)
            {
                double value = SeedHash.Range(3UL, i, 0, -2d, 5d);
                Assert.That(value, Is.GreaterThanOrEqualTo(-2d).And.LessThan(5d));
            }
        }

        [Test]
        public void Range_EqualBounds_ReturnsThatValue()
        {
            Assert.That(SeedHash.Range(3UL, 1, 1, 16d, 16d), Is.EqualTo(16d));
        }

        [Test]
        public void Range_MinAboveMax_Throws()
        {
            Assert.Throws<ArgumentException>(() => SeedHash.Range(3UL, 1, 1, 2d, 1d));
        }
    }
}
