using System;
using System.Collections.Generic;

namespace ProtoHarness.Net
{
    // Holds messages back before they reach the real sender, to stand in for a slow network (the transport's
    // own debug simulator does nothing in this Netcode version). Each message waits `delay` plus a seeded
    // random 0..jitter seconds, and never overtakes an earlier one, as on a reliable ordered channel. The clock
    // is passed in, so a test can drive it. It delays at the application level only: no packet loss.
    public sealed class DelayedSender
    {
        private struct Item
        {
            public double Due;
            public byte[] Payload;
        }

        private readonly Queue<Item> queue = new Queue<Item>(64);
        private readonly Action<byte[]> sink;
        private readonly double delay;
        private readonly double jitter;
        private readonly Random random;
        private double lastDue;

        public DelayedSender(Action<byte[]> sink, double delaySeconds, double jitterSeconds, int seed)
        {
            if (delaySeconds < 0d || double.IsNaN(delaySeconds)) throw new ArgumentOutOfRangeException(nameof(delaySeconds), delaySeconds, "Delay must be zero or more.");
            if (jitterSeconds < 0d || double.IsNaN(jitterSeconds)) throw new ArgumentOutOfRangeException(nameof(jitterSeconds), jitterSeconds, "Jitter must be zero or more.");
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
            delay = delaySeconds;
            jitter = jitterSeconds;
            random = new Random(seed);
        }

        public int Pending => queue.Count;

        public void Send(byte[] payload, double now)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            double due = now + delay + (jitter > 0d ? random.NextDouble() * jitter : 0d);
            if (due < lastDue) due = lastDue;
            lastDue = due;
            queue.Enqueue(new Item { Due = due, Payload = payload });
        }

        // Hands every message that is due by `now` to the sink, in the order they were sent.
        public void Pump(double now)
        {
            while (queue.Count > 0 && queue.Peek().Due <= now) sink(queue.Dequeue().Payload);
        }
    }
}
