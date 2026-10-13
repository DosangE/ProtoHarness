using System;
using NUnit.Framework;
using ProtoHarness.ChainRush.Control;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class TickInputTests
    {
        [Test]
        public void Constructor_ValidValues_ExposesThem()
        {
            var input = new TickInput(-0.25f, true, false, true);
            Assert.That(input.Steer, Is.EqualTo(-0.25f));
            Assert.That(input.PrimaryPressed, Is.True);
            Assert.That(input.ReleasePressed, Is.False);
            Assert.That(input.AttackPressed, Is.True);
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        [TestCase(1f)]
        public void Constructor_SteerAtBoundary_Accepts(float steer)
        {
            Assert.That(new TickInput(steer, false, false, false).Steer, Is.EqualTo(steer));
        }

        [TestCase(1.0001f)]
        [TestCase(-1.0001f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Constructor_SteerOutOfRange_Throws(float steer)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TickInput(steer, false, false, false));
        }

        [Test]
        public void Constructor_DriftAndChainAction_DefaultFalseAndExposeWhenGiven()
        {
            var plain = new TickInput(0f, false, false, false);
            Assert.That(plain.Drift, Is.False);
            Assert.That(plain.ChainActionPressed, Is.False);
            var full = new TickInput(0.5f, false, false, false, true, true);
            Assert.That(full.Drift, Is.True);
            Assert.That(full.ChainActionPressed, Is.True);
        }

        [Test]
        public void Default_Value_IsNeutral()
        {
            TickInput input = default;
            Assert.That(input.Steer, Is.Zero);
            Assert.That(input.PrimaryPressed, Is.False);
            Assert.That(input.ReleasePressed, Is.False);
            Assert.That(input.AttackPressed, Is.False);
        }
    }
}
