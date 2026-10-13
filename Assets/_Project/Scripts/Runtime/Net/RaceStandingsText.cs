using System;
using System.Collections.Generic;
using System.Globalization;
using ProtoHarness.ChainRush;

namespace ProtoHarness.Net
{
    // The standings a client holds (RaceClient.Standings, You) as the lines RaceStandingsView draws, kept apart so they can be
    // checked without a screen. Finishers come first by place, then racers still running, then waiting, then fallen; racers
    // in the same group keep their order by number. A finish shows the run's time from its own start (its tick count, the
    // clock the places go by) and whether the place is still provisional. With the order of the racers still running
    // (RaceClient.Order), a running racer is placed after every finisher by that order, and running racers go by it. The
    // order and the standings come in separate messages, so for a moment they can disagree: a racer the standings have
    // running but the order does not place shows "–" (its place is on the way), and a racer the order places but the
    // standings have ended shows its standings line and is left out of the running racers' places.
    public static class RaceStandingsText
    {
        public readonly struct Line
        {
            // "1", or "–" for a racer with no place.
            public readonly string Place;
            // "YOU", or "RACER <n>".
            public readonly string Name;
            // "대기", "주행 중", "낙하", or "02:38.5  잠정" / "02:38.5  확정".
            public readonly string Status;
            public readonly int Racer;
            public readonly bool IsYou;
            public readonly RaceStandings.Status Of;

            public Line(string place, string name, string status, int racer, bool isYou, RaceStandings.Status of)
            {
                Place = place;
                Name = name;
                Status = status;
                Racer = racer;
                IsYou = isYou;
                Of = of;
            }

            public override string ToString() => Place + " " + Name + " " + Status;
        }

        // "RACE  /  2 RACERS"
        public static string Header(int racers) => "RACE  /  " + racers + (racers == 1 ? " RACER" : " RACERS");

        public static Line[] Lines(IReadOnlyList<RaceStandings.Entry> entries, int you) => Lines(entries, null, you);

        // order: each racer's place among those running (RaceOrder, 0 = none), or null before any has arrived.
        public static Line[] Lines(IReadOnlyList<RaceStandings.Entry> entries, IReadOnlyList<int> order, int you)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (you < 0 || you >= entries.Count) throw new ArgumentOutOfRangeException(nameof(you), you, $"RaceStandingsText: there are {entries.Count} racers.");
            if (order != null && order.Count != entries.Count)
                throw new ArgumentException($"RaceStandingsText: an order for {order.Count} racers, but the standings have {entries.Count}.", nameof(order));
            // A running racer's place: after every finisher, then one more than the racers the standings also have running that
            // the order puts ahead of it (0 while the order does not place it). Counting only those keeps the places right while
            // the order still holds a racer the standings have ended.
            int finishers = 0;
            for (int i = 0; i < entries.Count; i++) if (entries[i].Status == RaceStandings.Status.Finished) finishers++;
            var places = new int[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                RaceStandings.Entry e = entries[i];
                if (e.Status == RaceStandings.Status.Finished) places[i] = e.Place;
                else if (PlacedRunning(entries, order, i))
                {
                    int place = finishers + 1;
                    for (int o = 0; o < entries.Count; o++) if (o != i && PlacedRunning(entries, order, o) && order[o] < order[i]) place++;
                    places[i] = place;
                }
            }
            var sorted = new int[entries.Count];
            for (int i = 0; i < sorted.Length; i++) sorted[i] = i;
            // Insertion sort: stable, so racers that tie keep their order by number.
            for (int i = 1; i < sorted.Length; i++)
            {
                int racer = sorted[i];
                int j = i - 1;
                while (j >= 0 && Compare(entries, places, sorted[j], racer) > 0)
                {
                    sorted[j + 1] = sorted[j];
                    j--;
                }
                sorted[j + 1] = racer;
            }
            var lines = new Line[sorted.Length];
            for (int i = 0; i < sorted.Length; i++)
            {
                int racer = sorted[i];
                RaceStandings.Entry e = entries[racer];
                string place = places[racer] > 0 ? places[racer].ToString(CultureInfo.InvariantCulture) : "–";
                string name = racer == you ? "YOU" : "RACER " + racer.ToString(CultureInfo.InvariantCulture);
                lines[i] = new Line(place, name, StatusOf(e), racer, racer == you, e.Status);
            }
            return lines;
        }

        private static bool PlacedRunning(IReadOnlyList<RaceStandings.Entry> entries, IReadOnlyList<int> order, int racer) =>
            entries[racer].Status == RaceStandings.Status.Running && order != null && order[racer] > 0;

        // Within a group, by place; a racer with no place goes after those with one.
        private static int Compare(IReadOnlyList<RaceStandings.Entry> entries, int[] places, int a, int b)
        {
            int group = Group(entries[a].Status).CompareTo(Group(entries[b].Status));
            if (group != 0) return group;
            int placeA = places[a] > 0 ? places[a] : int.MaxValue;
            int placeB = places[b] > 0 ? places[b] : int.MaxValue;
            return placeA.CompareTo(placeB);
        }

        private static int Group(RaceStandings.Status status)
        {
            switch (status)
            {
                case RaceStandings.Status.Finished: return 0;
                case RaceStandings.Status.Running: return 1;
                case RaceStandings.Status.Waiting: return 2;
                case RaceStandings.Status.Failed: return 3;
                default: throw new ArgumentOutOfRangeException(nameof(status), status, "RaceStandingsText: not a racer status.");
            }
        }

        private static string StatusOf(RaceStandings.Entry e)
        {
            switch (e.Status)
            {
                case RaceStandings.Status.Waiting: return "대기";
                case RaceStandings.Status.Running: return "주행 중";
                case RaceStandings.Status.Failed: return "낙하";
                case RaceStandings.Status.Finished: return FormatTime(Ticks.ToSeconds(e.EndTick)) + (e.Final ? "  확정" : "  잠정");
                default: throw new ArgumentOutOfRangeException(nameof(e), e.Status, "RaceStandingsText: not a racer status.");
            }
        }

        // "02:38.5", as the HUD writes race times.
        private static string FormatTime(float seconds)
        {
            int minutes = (int)Math.Floor(seconds / 60f);
            return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + (seconds - minutes * 60f).ToString("00.0", CultureInfo.InvariantCulture);
        }
    }
}
