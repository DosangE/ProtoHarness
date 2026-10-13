using System;
using System.IO;
using NUnit.Framework;
using ProtoHarness.Net;

namespace ProtoHarness.Tests.PlayMode
{
    // The order of the racers still running as the server keeps it (DESIGN.md P3 C1c-3), and the message that carries it.
    // Racers are compared on the same tick of their own runs, the newest every running racer has reached; one has to get
    // more than the margin ahead to swap places. Needs neither a scene nor a connection.
    [Category("Net")]
    public sealed class RaceOrderTests
    {
        // Racer `racer` runs ticks from..to, `metresPerTick` a tick from the start line (tick t at t * metresPerTick).
        private static void Run(RaceOrder o, int racer, int from, int to, double metresPerTick)
        {
            for (int t = from; t <= to; t++) o.Advance(racer, t, t * metresPerTick);
        }

        [Test]
        public void Order_ComparesOnTheSameTickOfEachRun_SoAnEarlierStartIsNoLead()
        {
            var o = new RaceOrder(2, 1d);
            o.Start(0);
            Run(o, 0, 0, 100, 1d);
            Assert.That(RaceOrder.Describe(o.ToArray()), Is.EqualTo("0=1 1=-"), "Alone, the one running is first.");
            o.Start(1);
            Run(o, 1, 0, 50, 2d);
            // On tick 50 of each run racer 1 has gone 100 m and racer 0 50 m, though racer 0 is 50 ticks further on.
            Assert.That(o.ComparedTick, Is.EqualTo(50));
            Assert.That(RaceOrder.Describe(o.ToArray()), Is.EqualTo("0=2 1=1"));
        }

        [Test]
        public void Order_WithinTheMargin_KeepsTheOrderItHad_AndSwapsOnlyPastIt()
        {
            var o = new RaceOrder(2, 1d);
            o.Start(0);
            o.Start(1);
            o.Advance(0, 0, 0.5d);
            o.Advance(1, 0, 0d);
            Assert.That(RaceOrder.Describe(o.ToArray()), Is.EqualTo("0=1 1=2"), "Compared for the first time, the one further on is ahead.");
            o.Advance(0, 1, 10d);
            o.Advance(1, 1, 10.9d);
            Assert.That(RaceOrder.Describe(o.ToArray()), Is.EqualTo("0=1 1=2"), "0.9 m ahead is within the margin.");
            int version = o.Version;
            o.Advance(0, 2, 20d);
            o.Advance(1, 2, 21.01d);
            Assert.That(RaceOrder.Describe(o.ToArray()), Is.EqualTo("0=2 1=1"), "1.01 m ahead is past it.");
            Assert.That(o.Version, Is.EqualTo(version + 1));
            o.Advance(0, 3, 30.5d);
            o.Advance(1, 3, 30d);
            Assert.That(RaceOrder.Describe(o.ToArray()), Is.EqualTo("0=2 1=1"), "Back within the margin the other way, the order stays.");
        }

        [Test]
        public void Order_EqualProgress_IsATie_AndATieSharesAPlace()
        {
            var o = new RaceOrder(3, 1d);
            for (int i = 0; i < 3; i++) o.Start(i);
            Run(o, 0, 0, 10, 2d);
            Run(o, 1, 0, 10, 2d);
            Run(o, 2, 0, 10, 1d);
            Assert.That(RaceOrder.Describe(o.ToArray()), Is.EqualTo("0=1 1=1 2=3"));
        }

        [Test]
        public void Order_IsComparedOnTheNewestTickEveryRunningRacerHasReached()
        {
            var o = new RaceOrder(2, 1d);
            o.Start(0);
            o.Start(1);
            Run(o, 0, 0, 5, 1d);
            Run(o, 1, 0, 2, 3d);
            Assert.That(o.ComparedTick, Is.EqualTo(2));
            Assert.That(RaceOrder.Describe(o.ToArray()), Is.EqualTo("0=2 1=1"));
            // Racer 0 pulls away later in its run, but racer 1 has not got there yet.
            o.Advance(0, 6, 100d);
            Assert.That(o.ComparedTick, Is.EqualTo(2));
            Assert.That(RaceOrder.Describe(o.ToArray()), Is.EqualTo("0=2 1=1"));
        }

        [Test]
        public void Order_ARacerNotRunningOrWithNoStateYet_HasNoPlace_AndAnEndLeavesTheOrder()
        {
            var o = new RaceOrder(3, 1d);
            Assert.That(RaceOrder.Describe(o.ToArray()), Is.EqualTo("0=- 1=- 2=-"));
            Assert.That(o.ComparedTick, Is.EqualTo(-1));
            o.Start(0);
            o.Start(1);
            Assert.That(RaceOrder.Describe(o.ToArray()), Is.EqualTo("0=- 1=- 2=-"), "Started, but no state yet.");
            Assert.That(o.Version, Is.EqualTo(0));
            Run(o, 0, 0, 3, 2d);
            Run(o, 1, 0, 3, 1d);
            Assert.That(RaceOrder.Describe(o.ToArray()), Is.EqualTo("0=1 1=2 2=-"));
            Assert.That(o.End(0), Is.True);
            Assert.That(RaceOrder.Describe(o.ToArray()), Is.EqualTo("0=- 1=1 2=-"));
            Assert.That(o.End(1), Is.True);
            Assert.That(RaceOrder.Describe(o.ToArray()), Is.EqualTo("0=- 1=- 2=-"));
            Assert.That(o.ComparedTick, Is.EqualTo(-1));
        }

        [Test]
        public void Order_AdvanceWithoutAChange_ReturnsFalse_AndKeepsTheVersion()
        {
            var o = new RaceOrder(2, 1d);
            o.Start(0);
            o.Start(1);
            o.Advance(0, 0, 0d);
            Assert.That(o.Advance(1, 0, 0d), Is.True, "Racer 1 gets its place.");
            int version = o.Version;
            Assert.That(o.Advance(0, 1, 1d), Is.False, "Compared on tick 0 still.");
            Assert.That(o.Advance(1, 1, 1d), Is.False, "Still level.");
            Assert.That(o.Version, Is.EqualTo(version));
        }

        [Test]
        public void Order_EventsOutOfOrder_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RaceOrder(0, 1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RaceOrder(2, -1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RaceOrder(2, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RaceOrder(2, double.PositiveInfinity));
            var o = new RaceOrder(2, 1d);
            Assert.Throws<InvalidOperationException>(() => o.Advance(0, 0, 0d), "Not started.");
            Assert.Throws<InvalidOperationException>(() => o.End(0), "Not started.");
            Assert.Throws<ArgumentOutOfRangeException>(() => o.Start(2), "Racer 2 of 2.");
            Assert.Throws<ArgumentOutOfRangeException>(() => { int unused = o[-1]; });
            o.Start(0);
            Assert.Throws<InvalidOperationException>(() => o.Start(0), "Started twice.");
            Assert.Throws<ArgumentOutOfRangeException>(() => o.Advance(0, 1, 0d), "The first state is for tick 0.");
            o.Advance(0, 0, 0d);
            Assert.Throws<ArgumentOutOfRangeException>(() => o.Advance(0, 0, 0d), "Tick 0 again.");
            Assert.Throws<ArgumentOutOfRangeException>(() => o.Advance(0, 2, 0d), "Tick 1 skipped.");
            Assert.Throws<ArgumentOutOfRangeException>(() => o.Advance(0, 1, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => o.Advance(0, 1, double.NegativeInfinity));
            o.End(0);
            Assert.Throws<InvalidOperationException>(() => o.End(0), "Ended twice.");
            Assert.Throws<InvalidOperationException>(() => o.Advance(0, 1, 1d), "An ended run does not advance.");
            Assert.Throws<ArgumentNullException>(() => RaceOrder.Describe(null));
        }

        // ---- the order message -------------------------------------------------------------------

        [Test]
        public void Wire_OrderRoundTrips()
        {
            var o = new RaceOrder(3, 1d);
            o.Start(0);
            o.Start(2);
            Run(o, 0, 0, 40, 1d);
            Run(o, 2, 0, 30, 2d);
            int[] places = o.ToArray();
            Assert.That(RaceOrder.Describe(places), Is.EqualTo("0=2 1=- 2=1"));
            byte[] bytes = RaceWire.EncodeOrder(o.ComparedTick, places);
            Assert.That(bytes.Length, Is.EqualTo(5 + 3));
            int[] back = RaceWire.DecodeOrder(bytes, out int tick);
            Assert.That(tick, Is.EqualTo(30));
            Assert.That(back, Is.EqualTo(places));
            Assert.That(RaceWire.DecodeOrder(RaceWire.EncodeOrder(-1, new[] { 0, 0 }), out int none), Is.EqualTo(new[] { 0, 0 }));
            Assert.That(none, Is.EqualTo(-1));
        }

        [Test]
        public void Wire_CorruptOrders_Throw()
        {
            byte[] bytes = RaceWire.EncodeOrder(30, new[] { 2, 0, 1 });
            // Layout: tick (4) | count (1) | per racer: place (1).
            var shortBytes = new byte[bytes.Length - 1];
            Array.Copy(bytes, shortBytes, shortBytes.Length);
            Assert.Throws<EndOfStreamException>(() => RaceWire.DecodeOrder(shortBytes, out _));
            var longBytes = new byte[bytes.Length + 1];
            Array.Copy(bytes, longBytes, bytes.Length);
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeOrder(longBytes, out _));
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeOrder(With(bytes, 4, 0), out _), "No racers.");
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeOrder(With(bytes, 5, 4), out _), "Fourth of three.");
            byte[] noneCompared = RaceWire.EncodeOrder(-1, new[] { 0, 0 });
            // Tick -1 is FF FF FF FF (little-endian); FE in the first byte makes it -2.
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeOrder(With(noneCompared, 0, 0xFE), out _), "Compared on tick -2.");
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeOrder(With(noneCompared, 5, 1), out _), "A place with nobody compared.");

            Assert.Throws<ArgumentNullException>(() => RaceWire.EncodeOrder(0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => RaceWire.EncodeOrder(0, new int[0]));
            Assert.Throws<ArgumentException>(() => RaceWire.EncodeOrder(0, new[] { 3, 1 }), "Third of two.");
            Assert.Throws<ArgumentException>(() => RaceWire.EncodeOrder(0, new[] { -1, 1 }), "A negative place.");
            Assert.Throws<ArgumentException>(() => RaceWire.EncodeOrder(-1, new[] { 1, 0 }), "A place with nobody compared.");
            Assert.Throws<ArgumentException>(() => RaceWire.EncodeOrder(-2, new[] { 0, 0 }), "Compared on tick -2.");
            Assert.Throws<ArgumentNullException>(() => RaceWire.DecodeOrder(null, out _));
        }

        private static byte[] With(byte[] bytes, int index, byte value)
        {
            var copy = (byte[])bytes.Clone();
            copy[index] = value;
            return copy;
        }
    }
}
