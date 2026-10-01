using System;
using NUnit.Framework;
using ProtoHarness.ChainRush;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class TicksTests
    {
        [TestCase(0f, 0)]
        [TestCase(0.02f, 1)]
        [TestCase(0.3f, 15)]
        [TestCase(0.4f, 20)]
        [TestCase(1.2f, 60)]
        [TestCase(0.18f, 9)]
        [TestCase(0.15f, 8)]
        [TestCase(0.35f, 18)]
        [TestCase(1.25f, 63)]
        [TestCase(0.2f, 10)]
        [TestCase(0.75f, 38)]
        public void FromSeconds_Duration_RoundsUpToWholeTicks(float seconds, int expected)
        {
            Assert.That(Ticks.FromSeconds(seconds), Is.EqualTo(expected));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void FromSeconds_InvalidDuration_Throws(float seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Ticks.FromSeconds(seconds));
        }

        [Test]
        public void ToSeconds_FiftyTicks_IsOneSecond()
        {
            Assert.That(Ticks.ToSeconds(50), Is.EqualTo(1f).Within(1e-5f));
        }
    }
}
