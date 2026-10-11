using System;
using System.Collections.Generic;
using System.Text;

namespace ProtoHarness.Net
{
    // Who is where in the race, as the server sees it (DESIGN.md P3: the race's standings are the server's). Each racer is
    // waiting, running, finished or failed. Finishers are placed by how many ticks their run took: every racer starts when
    // its own inputs are buffered (RaceServer), at a different real time, and counts its ticks from that start, so the tick
    // count is the fair clock. Equal ticks share a place (1, 1, 3). A fall has no place. A finisher's place is provisional
    // while another racer could still finish in fewer ticks: one still waiting, or one running that has not yet got past
    // that tick. Once every other racer has ended or run past it, the place is final. Events out of order throw.
    public sealed class RaceStandings
    {
        // The values are the bytes on the wire (RaceWire).
        public enum Status : byte
        {
            Waiting = 0,
            Running = 1,
            Finished = 2,
            Failed = 3,
        }

        // One racer's line. EndTick is -1 until the run ends; Place is 0 for a racer with no place (not finished).
        public readonly struct Entry
        {
            public readonly Status Status;
            public readonly int EndTick;
            public readonly int Place;
            public readonly bool Final;

            public Entry(Status status, int endTick, int place, bool final)
            {
                Status = status;
                EndTick = endTick;
                Place = place;
                Final = final;
            }

            public override string ToString() => Describe(this);
        }

        private readonly Entry[] entries;
        // The newest tick each running racer is known to have run past (its newest state).
        private readonly int[] ticks;

        public RaceStandings(int racers)
        {
            if (racers < 1) throw new ArgumentOutOfRangeException(nameof(racers), racers, "At least one racer.");
            entries = new Entry[racers];
            ticks = new int[racers];
            for (int i = 0; i < racers; i++) entries[i] = new Entry(Status.Waiting, -1, 0, false);
        }

        public int Count => entries.Length;
        public Entry this[int racer] => entries[Checked(racer)];
        // Goes up by one each time any racer's line changes, and only then.
        public int Version { get; private set; }

        // The racer's run has started (its tick 0). Returns whether the standings changed.
        public bool Start(int racer)
        {
            Checked(racer);
            if (entries[racer].Status != Status.Waiting)
                throw new InvalidOperationException($"RaceStandings: racer {racer} started twice (it is {entries[racer].Status}).");
            entries[racer] = new Entry(Status.Running, -1, 0, false);
            ticks[racer] = 0;
            return Recompute(true);
        }

        // A running racer has run past `tick` and is still going (the server's state for that tick). Ticks never go back.
        // Returns whether the standings changed: only when it lets a finisher's place become final.
        public bool Advance(int racer, int tick)
        {
            Checked(racer);
            if (entries[racer].Status != Status.Running)
                throw new InvalidOperationException($"RaceStandings: racer {racer} advanced to tick {tick} but it is {entries[racer].Status}.");
            if (tick < ticks[racer])
                throw new ArgumentOutOfRangeException(nameof(tick), tick, $"RaceStandings: racer {racer} went back from tick {ticks[racer]}.");
            if (tick == ticks[racer]) return false;
            ticks[racer] = tick;
            return Recompute(false);
        }

        // A running racer's run ended on `tick`, after the last tick it ran past. Returns whether the standings changed (always).
        public bool End(int racer, int tick, RaceWire.EndReason reason)
        {
            Checked(racer);
            if (entries[racer].Status != Status.Running)
                throw new InvalidOperationException($"RaceStandings: racer {racer} ended on tick {tick} but it is {entries[racer].Status}.");
            if (tick <= ticks[racer])
                throw new ArgumentOutOfRangeException(nameof(tick), tick, $"RaceStandings: racer {racer} ended on tick {tick}, but it had already run past tick {ticks[racer]}.");
            if (!RaceWire.IsKnown(reason)) throw new ArgumentOutOfRangeException(nameof(reason), reason, "RaceStandings: not an end reason.");
            Status status = reason == RaceWire.EndReason.Finished ? Status.Finished : Status.Failed;
            entries[racer] = new Entry(status, tick, 0, status == Status.Failed);
            ticks[racer] = tick;
            return Recompute(true);
        }

        // Places and finality from the statuses and ticks. `changed` is true when the caller already changed a line.
        private bool Recompute(bool changed)
        {
            for (int f = 0; f < entries.Length; f++)
            {
                Entry e = entries[f];
                if (e.Status != Status.Finished) continue;
                int place = 1;
                bool final = true;
                for (int r = 0; r < entries.Length; r++)
                {
                    if (r == f) continue;
                    Entry other = entries[r];
                    if (other.Status == Status.Finished && other.EndTick < e.EndTick) place++;
                    // A racer still going can finish in no fewer ticks than one more than it has run past.
                    if (other.Status == Status.Waiting || (other.Status == Status.Running && ticks[r] < e.EndTick)) final = false;
                }
                if (place == e.Place && final == e.Final) continue;
                entries[f] = new Entry(Status.Finished, e.EndTick, place, final);
                changed = true;
            }
            if (changed) Version++;
            return changed;
        }

        private int Checked(int racer)
        {
            if (racer < 0 || racer >= entries.Length) throw new ArgumentOutOfRangeException(nameof(racer), racer, $"RaceStandings: there are {entries.Length} racers.");
            return racer;
        }

        // A copy of every line, by racer.
        public Entry[] ToArray() => (Entry[])entries.Clone();

        // "Waiting", "Running", "Failed@657", "Finished@7924#1", or "Finished@7924#1?" while the place is provisional.
        public static string Describe(Entry e)
        {
            switch (e.Status)
            {
                case Status.Waiting:
                case Status.Running:
                    return e.Status.ToString();
                case Status.Failed:
                    return $"Failed@{e.EndTick}";
                default:
                    return $"Finished@{e.EndTick}#{e.Place}{(e.Final ? "" : "?")}";
            }
        }

        // "0=Running 1=Failed@657": every racer's line, by racer.
        public static string Describe(IReadOnlyList<Entry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            var text = new StringBuilder(entries.Count * 20);
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0) text.Append(' ');
                text.Append(i).Append('=').Append(Describe(entries[i]));
            }
            return text.ToString();
        }
    }
}
