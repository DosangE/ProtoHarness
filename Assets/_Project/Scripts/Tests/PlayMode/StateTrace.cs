using System;
using System.Collections.Generic;
using System.Text;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Track;
using UnityEngine;

namespace ProtoHarness.Tests.PlayMode
{
    // Wraps an input source and, each time the simulation asks for a tick's input, writes down the state the
    // previous tick left behind: position, velocity, facing, gauge, grapple, health and so on, each value
    // as its exact bits. Sample k is the state after k ticks (sample 0 is the state right after StartRun).
    // It never changes what the simulation sees, so a recorded run and a replayed run carry the same wrapper.
    internal sealed class StateTrace : IInputSource
    {
        private static readonly string[] BaseNames =
        {
            "tick", "posX", "posY", "posZ", "velX", "velY", "velZ", "heading", "turnRate", "gauge", "coyote", "ropeLength",
            "health", "hits", "grapples", "anchor", "sling", "swing", "grounded", "S",
        };

        private readonly IInputSource inner;
        private readonly ChainRushGame game;
        private readonly RunnerMotor player;
        private readonly (string Name, Func<double> Read)[] extras;
        private readonly List<ulong> bits = new List<ulong>(4096);

        public StateTrace(ChainRushGame game, RunnerMotor player, IInputSource inner, params (string Name, Func<double> Read)[] extras)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.extras = extras ?? Array.Empty<(string, Func<double>)>();
        }

        public int FieldCount => BaseNames.Length + extras.Length;
        // Samples taken so far.
        public int Samples => bits.Count / FieldCount;

        public void Poll() => inner.Poll();

        public TickInput Consume()
        {
            Sample();
            return inner.Consume();
        }

        public void Clear()
        {
            bits.Clear();
            inner.Clear();
        }

        private static ulong Bits(float value) => (uint)BitConverter.SingleToInt32Bits(value);
        private static ulong Bits(double value) => (ulong)BitConverter.DoubleToInt64Bits(value);

        private void Sample()
        {
            RacerState racer = game.Racer;
            Vector3 position = player.transform.position;
            Vector3 velocity = racer.Velocity;
            bits.Add((ulong)game.Tick);
            bits.Add(Bits(position.x)); bits.Add(Bits(position.y)); bits.Add(Bits(position.z));
            bits.Add(Bits(velocity.x)); bits.Add(Bits(velocity.y)); bits.Add(Bits(velocity.z));
            bits.Add(Bits(racer.Heading));
            bits.Add(Bits(racer.TurnRate));
            bits.Add(Bits(racer.Gauge));
            bits.Add(Bits(racer.CoyoteTime));
            bits.Add(Bits(racer.RopeLength));
            bits.Add((ulong)racer.Health + 1000UL);
            bits.Add((ulong)racer.Hits);
            bits.Add((ulong)racer.Grapples);
            bits.Add((ulong)(racer.AnchorIndex + 1));
            bits.Add((ulong)racer.SlingTicks);
            bits.Add((ulong)racer.SwingTicks);
            bits.Add(player.IsGrounded ? 1UL : 0UL);
            bits.Add(Bits(game.Track.Project(position).S));
            for (int i = 0; i < extras.Length; i++) bits.Add(Bits(extras[i].Read()));
        }

        // One hash over the first `samples` samples (SplitMix64 chained over every value).
        public ulong Hash(int samples)
        {
            ulong hash = 0UL;
            int count = Math.Min(samples, Samples) * FieldCount;
            for (int i = 0; i < count; i++) hash = SeedHash.SplitMix64(hash ^ bits[i]);
            return hash;
        }

        // Index of the first sample (within the first `samples`) in which the two traces differ, or -1.
        public static int FirstDifferentSample(StateTrace a, StateTrace b, int samples) => FirstDifferentSample(a, 0, b, 0, samples);

        // The same for `samples` samples of a starting at aStart against samples of b starting at bStart; the
        // result counts from the start of the compared range. Lets a replay that begins at a restored snapshot
        // be held against the original run from the same tick.
        public static int FirstDifferentSample(StateTrace a, int aStart, StateTrace b, int bStart, int samples)
        {
            if (a.FieldCount != b.FieldCount) throw new ArgumentException("The traces carry different fields.");
            int count = Math.Min(samples, Math.Min(a.Samples - aStart, b.Samples - bStart));
            for (int sample = 0; sample < count; sample++)
            {
                for (int field = 0; field < a.FieldCount; field++)
                {
                    if (a.bits[(aStart + sample) * a.FieldCount + field] != b.bits[(bStart + sample) * a.FieldCount + field]) return sample;
                }
            }
            return -1;
        }

        // null when the first `samples` samples of both traces are identical, else which value differs first.
        public static string Compare(StateTrace a, StateTrace b, int samples) => Compare(a, 0, b, 0, samples);

        public static string Compare(StateTrace a, int aStart, StateTrace b, int bStart, int samples)
        {
            if (a.Samples - aStart < samples || b.Samples - bStart < samples)
                return $"a trace is too short: {a.Samples - aStart} and {b.Samples - bStart} samples from the start of the range, {samples} wanted";
            int sample = FirstDifferentSample(a, aStart, b, bStart, samples);
            if (sample < 0) return null;
            var text = new StringBuilder($"first difference at sample {sample} of the range (state after {aStart + sample} ticks of the first trace):");
            for (int field = 0; field < a.FieldCount; field++)
            {
                ulong left = a.bits[(aStart + sample) * a.FieldCount + field];
                ulong right = b.bits[(bStart + sample) * a.FieldCount + field];
                if (left == right) continue;
                string name = field < BaseNames.Length ? BaseNames[field] : a.extras[field - BaseNames.Length].Name;
                text.Append($" {name} 0x{left:X} vs 0x{right:X};");
            }
            if (sample > 0) text.Append(" The sample before it was identical.");
            return text.ToString();
        }
    }
}
