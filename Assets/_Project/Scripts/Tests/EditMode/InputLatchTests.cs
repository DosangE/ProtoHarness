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
        public void Consume_ChainActionEdge_DeliversOnceAndSurvivesQuietFrame()
        {
            latch.Record(0f, false, false, false, false, true);
            latch.Record(0f, false, false, false);
            Assert.That(latch.Consume().ChainActionPressed, Is.True);
            Assert.That(latch.Consume().ChainActionPressed, Is.False);
        }

        [Test]
        public void Consume_SwingHeldLevel_KeepsLatestAndClearResetsIt()
        {
            latch.Record(0f, false, false, false, true);
            Assert.That(latch.Consume().SwingHeld, Is.True);
            Assert.That(latch.Consume().SwingHeld, Is.True, "Holding the swing key is a level, not an edge.");
            latch.Record(0f, false, false, false, false);
            Assert.That(latch.Consume().SwingHeld, Is.False);
            latch.Record(0f, false, false, false, true, true);
            latch.Clear();
            TickInput cleared = latch.Consume();
            Assert.That(cleared.SwingHeld, Is.False);
            Assert.That(cleared.ChainActionPressed, Is.False);
        }

        [Test]
        public void Consume_SwingPressEdge_KeptUntilConsumedOnceAndClearResetsIt()
        {
            latch.Record(0f, false, false, false, true, false, true);
            latch.Record(0f, false, false, false, true, false, false);
            TickInput pressed = latch.Consume();
            Assert.That(pressed.SwingPressed, Is.True, "A press seen between two ticks is kept for the next one.");
            Assert.That(pressed.SwingHeld, Is.True);
            TickInput still = latch.Consume();
            Assert.That(still.SwingPressed, Is.False, "The press is an edge.");
            Assert.That(still.SwingHeld, Is.True, "Holding the key is a level, kept until a new record says otherwise.");
            latch.Record(0f, false, false, false, false, false, true);
            latch.Clear();
            Assert.That(latch.Consume().SwingPressed, Is.False);
        }

        [Test]
        public void Consume_CoastingLevel_KeepsLatestAndClearResetsIt()
        {
            Assert.That(latch.Consume().Coasting, Is.False, "Nothing recorded reads as full throttle, like every older source.");
            latch.Record(0f, false, false, false, coasting: true);
            Assert.That(latch.Consume().Coasting, Is.True);
            Assert.That(latch.Consume().Coasting, Is.True, "Letting go of the throttle is a level, not an edge.");
            latch.Record(0f, false, false, false, coasting: false);
            Assert.That(latch.Consume().Coasting, Is.False);
            latch.Record(0f, false, false, false, coasting: true);
            latch.Clear();
            Assert.That(latch.Consume().Coasting, Is.False);
        }

        [Test]
        public void Consume_ReversingLevel_KeepsLatestAndClearResetsIt()
        {
            Assert.That(latch.Consume().Reversing, Is.False, "Nothing recorded reads as not backing up, like every older source.");
            latch.Record(0f, false, false, false, coasting: true, reversing: true);
            Assert.That(latch.Consume().Reversing, Is.True);
            Assert.That(latch.Consume().Reversing, Is.True, "Holding the down arrow is a level, not an edge.");
            latch.Record(0f, false, false, false, coasting: true, reversing: false);
            Assert.That(latch.Consume().Reversing, Is.False);
            latch.Record(0f, false, false, false, coasting: true, reversing: true);
            latch.Clear();
            Assert.That(latch.Consume().Reversing, Is.False);
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
