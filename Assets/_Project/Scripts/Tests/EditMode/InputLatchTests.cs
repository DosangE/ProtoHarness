using System;
using NUnit.Framework;
using ProtoHarness.ChainRush.Control;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class InputLatchTests
    {
        private InputLatch latch;

        [SetUp]
        public void SetUp() => latch = new InputLatch();

        [Test]
        public void Consume_NothingRecorded_ReturnsNeutralInput()
        {
            TickInput input = latch.Consume();
            Assert.That(input.Steer, Is.Zero);
            Assert.That(input.PrimaryPressed, Is.False);
            Assert.That(input.ReleasePressed, Is.False);
            Assert.That(input.AttackPressed, Is.False);
        }

        [Test]
        public void Consume_EdgeRecorded_DeliversOnceThenClears()
        {
            latch.Record(0f, true, false, true);
            TickInput first = latch.Consume();
            TickInput second = latch.Consume();
            Assert.That(first.PrimaryPressed, Is.True);
            Assert.That(first.AttackPressed, Is.True);
            Assert.That(first.ReleasePressed, Is.False);
            Assert.That(second.PrimaryPressed, Is.False);
            Assert.That(second.AttackPressed, Is.False);
        }

        [Test]
        public void Consume_EdgeThenQuietFrame_KeepsEdgeUntilConsumed()
        {
            latch.Record(0f, false, true, false);
            latch.Record(0f, false, false, false);
            Assert.That(latch.Consume().ReleasePressed, Is.True);
        }

        [Test]
        public void Consume_SteerRecordedTwice_ReturnsLatestAndKeepsItAfterConsume()
        {
            latch.Record(-1f, false, false, false);
            latch.Record(0.5f, false, false, false);
            Assert.That(latch.Consume().Steer, Is.EqualTo(0.5f));
            Assert.That(latch.Consume().Steer, Is.EqualTo(0.5f));
        }

        [Test]
        public void Clear_AfterRecord_ResetsSteerAndEdges()
        {
            latch.Record(1f, true, true, true);
            latch.Clear();
            TickInput input = latch.Consume();
            Assert.That(input.Steer, Is.Zero);
            Assert.That(input.PrimaryPressed, Is.False);
            Assert.That(input.ReleasePressed, Is.False);
            Assert.That(input.AttackPressed, Is.False);
        }

        [TestCase(1.01f)]
        [TestCase(-1.01f)]
        [TestCase(float.NaN)]
        public void Record_SteerOutOfRange_Throws(float steer)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => latch.Record(steer, false, false, false));
        }
    }
}
