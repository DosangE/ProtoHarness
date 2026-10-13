using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoHarness.Net
{
    // Draws another racer smoothly from the server's positions, which arrive late and unevenly. A display clock counts
    // in ticks and runs at the tick rate, but never closer than `delayTicks` to the newest position received and never
    // further behind than `maxLagTicks`; between the two positions around the display tick it interpolates linearly. So a
    // late message makes the racer wait on its last position instead of jumping, and a long silence is caught up in one
    // step. It only draws; it knows nothing about the simulation, and the racer's own run does not use it.
    public sealed class RemoteInterpolator
    {
        private struct Sample
        {
            public int Tick;
            public Vector3 Position;
        }

        private readonly List<Sample> samples = new List<Sample>(64);
        private readonly float delayTicks;
        private readonly float maxLagTicks;
        private float displayTick;
        private bool started;

        public RemoteInterpolator(float delayTicks = 4f, float maxLagTicks = 25f)
        {
            if (!(delayTicks >= 0f)) throw new ArgumentOutOfRangeException(nameof(delayTicks), delayTicks, "The delay must be zero or more.");
            if (!(maxLagTicks >= delayTicks)) throw new ArgumentOutOfRangeException(nameof(maxLagTicks), maxLagTicks, "The longest lag cannot be shorter than the delay.");
            this.delayTicks = delayTicks;
            this.maxLagTicks = maxLagTicks;
        }

        public int Count => samples.Count;
        public int NewestTick => samples.Count == 0 ? -1 : samples[samples.Count - 1].Tick;
        public float DisplayTick => displayTick;

        // Positions come in order, one per tick at most; anything else is a bug upstream and throws.
        public void Add(int tick, Vector3 position)
        {
            if (samples.Count > 0 && tick <= NewestTick)
                throw new ArgumentException($"RemoteInterpolator: tick {tick} after tick {NewestTick}; positions must arrive in order.", nameof(tick));
            samples.Add(new Sample { Tick = tick, Position = position });
            // Only what the display clock can still reach is kept: one sample before it, for the interpolation.
            int drop = 0;
            while (drop + 1 < samples.Count && samples[drop + 1].Tick <= displayTick - maxLagTicks) drop++;
            if (drop > 0) samples.RemoveRange(0, drop);
        }

        // Moves the display clock on by `seconds` of real time at `ticksPerSecond`, within the limits above.
        public void Advance(float seconds, float ticksPerSecond)
        {
            if (samples.Count == 0) return;
            float newest = NewestTick;
            float latest = newest - delayTicks;
            if (!started)
            {
                displayTick = latest;
                started = true;
                return;
            }
            displayTick += seconds * ticksPerSecond;
            if (displayTick > latest) displayTick = latest;
            float oldest = newest - maxLagTicks;
            if (displayTick < oldest) displayTick = oldest;
        }

        // The position at the display tick: before the first sample the first sample, after the last the last.
        public bool TryEvaluate(out Vector3 position)
        {
            position = default;
            if (samples.Count == 0) return false;
            if (displayTick <= samples[0].Tick)
            {
                position = samples[0].Position;
                return true;
            }
            for (int i = 1; i < samples.Count; i++)
            {
                if (samples[i].Tick < displayTick) continue;
                Sample a = samples[i - 1];
                Sample b = samples[i];
                float t = (displayTick - a.Tick) / (b.Tick - a.Tick);
                position = Vector3.LerpUnclamped(a.Position, b.Position, t);
                return true;
            }
            position = samples[samples.Count - 1].Position;
            return true;
        }
    }
}
