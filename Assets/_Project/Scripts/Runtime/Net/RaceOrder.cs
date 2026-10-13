using System;
using System.Collections.Generic;
using System.Text;

namespace ProtoHarness.Net
{
    // Who is ahead among the racers still running, as the server sees it (DESIGN.md P3: the race's standings are the
    // server's). Every racer starts at a different real time (RaceServer), and RaceStandings places finishers by the ticks
    // their runs took, so running racers are compared the same way: by how far each had gone (LapCounter.Progress, metres
    // from the start line counting laps) on the same tick of its own run, the newest tick every running racer has reached.
    // One racer more than `margin` metres ahead of another is ahead of it; equal progress is a tie; in between, the two
    // keep the order they last had, so racers side by side do not swap on every tick (a pair compared for the first time
    // goes by which is further). A racer's place is one more than the number of racers ahead of it, so a tie shares a place.
    // A racer that is not running, or has no state yet, has no place (0). Events out of order throw.
    public sealed class RaceOrder
    {
        private enum Phase : byte
        {
            Waiting,
            Running,
            Ended,
        }

        private readonly double margin;
        private readonly Phase[] phases;
        // Each racer's progress by tick of its own run (index = tick), since it started.
        private readonly List<double>[] progress;
        // ahead[a * n + b]: +1 when a is ahead of b, -1 when behind, 0 when level; kept while both run (`paired`).
        private readonly sbyte[] ahead;
        private readonly bool[] paired;
        private readonly int[] places;

        public RaceOrder(int racers, double margin)
        {
            if (racers < 1) throw new ArgumentOutOfRangeException(nameof(racers), racers, "At least one racer.");
            if (!(margin >= 0d) || double.IsInfinity(margin)) throw new ArgumentOutOfRangeException(nameof(margin), margin, "The margin is zero or more metres, and finite.");
            this.margin = margin;
            phases = new Phase[racers];
            progress = new List<double>[racers];
            for (int i = 0; i < racers; i++) progress[i] = new List<double>(4096);
            ahead = new sbyte[racers * racers];
            paired = new bool[racers * racers];
            places = new int[racers];
            ComparedTick = -1;
        }

        public int Count => places.Length;
        public double Margin => margin;
        public int this[int racer] => places[Checked(racer)];
        // Goes up by one each time any racer's place changes, and only then.
        public int Version { get; private set; }
        // The tick of their own runs the running racers were last compared on, or -1 while none has a state.
        public int ComparedTick { get; private set; }

        // The racer's run has started. It has no place until its state for tick 0 (Advance) arrives.
        public void Start(int racer)
        {
            Checked(racer);
            if (phases[racer] != Phase.Waiting)
                throw new InvalidOperationException($"RaceOrder: racer {racer} started twice (it is {phases[racer]}).");
            phases[racer] = Phase.Running;
            progress[racer].Clear();
        }

        // A running racer's progress after `tick` of its run (the server's state for that tick). States come one per tick
        // from tick 0. Returns whether any place changed.
        public bool Advance(int racer, int tick, double metres)
        {
            Checked(racer);
            if (phases[racer] != Phase.Running)
                throw new InvalidOperationException($"RaceOrder: racer {racer} advanced to tick {tick} but it is {phases[racer]}.");
            if (tick != progress[racer].Count)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, $"RaceOrder: racer {racer}'s next state is for tick {progress[racer].Count}.");
            if (double.IsNaN(metres) || double.IsInfinity(metres))
                throw new ArgumentOutOfRangeException(nameof(metres), metres, $"RaceOrder: racer {racer}'s progress at tick {tick} is not a number.");
            progress[racer].Add(metres);
            return Recompute();
        }

        // A running racer's run ended (a fall or a finish): it leaves the order. Returns whether any place changed.
        public bool End(int racer)
        {
            Checked(racer);
            if (phases[racer] != Phase.Running)
                throw new InvalidOperationException($"RaceOrder: racer {racer} ended but it is {phases[racer]}.");
            phases[racer] = Phase.Ended;
            return Recompute();
        }

        private bool Compared(int racer) => phases[racer] == Phase.Running && progress[racer].Count > 0;

        private bool Recompute()
        {
            int n = places.Length;
            int tick = int.MaxValue;
            for (int r = 0; r < n; r++) if (Compared(r)) tick = Math.Min(tick, progress[r].Count - 1);
            ComparedTick = tick == int.MaxValue ? -1 : tick;
            for (int a = 0; a < n; a++)
            {
                for (int b = a + 1; b < n; b++)
                {
                    if (!Compared(a) || !Compared(b))
                    {
                        paired[a * n + b] = false;
                        continue;
                    }
                    double difference = progress[a][tick] - progress[b][tick];
                    sbyte relation;
                    if (difference == 0d) relation = 0;
                    else if (Math.Abs(difference) > margin || !paired[a * n + b]) relation = difference > 0d ? (sbyte)1 : (sbyte)-1;
                    else relation = ahead[a * n + b];
                    ahead[a * n + b] = relation;
                    ahead[b * n + a] = (sbyte)-relation;
                    paired[a * n + b] = true;
                }
            }
            bool changed = false;
            for (int r = 0; r < n; r++)
            {
                int place = 0;
                if (Compared(r))
                {
                    place = 1;
                    for (int o = 0; o < n; o++) if (o != r && Compared(o) && ahead[o * n + r] > 0) place++;
                }
                if (place == places[r]) continue;
                places[r] = place;
                changed = true;
            }
            if (changed) Version++;
            return changed;
        }

        private int Checked(int racer)
        {
            if (racer < 0 || racer >= places.Length) throw new ArgumentOutOfRangeException(nameof(racer), racer, $"RaceOrder: there are {places.Length} racers.");
            return racer;
        }

        // A copy of every racer's place, by racer (0 = no place).
        public int[] ToArray() => (int[])places.Clone();

        // "0=1 1=2 2=-": every racer's place, by racer; "-" for none.
        public static string Describe(IReadOnlyList<int> places)
        {
            if (places == null) throw new ArgumentNullException(nameof(places));
            var text = new StringBuilder(places.Count * 5);
            for (int i = 0; i < places.Count; i++)
            {
                if (i > 0) text.Append(' ');
                text.Append(i).Append('=');
                if (places[i] > 0) text.Append(places[i]);
                else text.Append('-');
            }
            return text.ToString();
        }
    }
}
