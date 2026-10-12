using System;
using System.Collections.Generic;
using System.Globalization;
using ProtoHarness.ChainRush;

namespace ProtoHarness.Net
{
    // The standings a client holds (RaceClient.Standings, You) as the lines RaceStandingsView draws, kept apart so they can be
    // checked without a screen. Finishers come first by place, then racers still running, then waiting, then fallen; racers
    // in the same group keep their order by number. A finish shows the run's time from its own start (its tick count, the
    // clock the places go by) and whether the place is still provisional.
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

        public static Line[] Lines(IReadOnlyList<RaceStandings.Entry> entries, int you)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (you < 0 || you >= entries.Count) throw new ArgumentOutOfRangeException(nameof(you), you, $"RaceStandingsText: there are {entries.Count} racers.");
            var order = new int[entries.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            // Insertion sort: stable, so racers that tie keep their order by number.
            for (int i = 1; i < order.Length; i++)
            {
                int racer = order[i];
                int j = i - 1;
                while (j >= 0 && Compare(entries[order[j]], entries[racer]) > 0)
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = racer;
            }
            var lines = new Line[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                int racer = order[i];
                RaceStandings.Entry e = entries[racer];
                string place = e.Status == RaceStandings.Status.Finished ? e.Place.ToString(CultureInfo.InvariantCulture) : "–";
                string name = racer == you ? "YOU" : "RACER " + racer.ToString(CultureInfo.InvariantCulture);
                lines[i] = new Line(place, name, StatusOf(e), racer, racer == you, e.Status);
            }
            return lines;
        }

        private static int Compare(RaceStandings.Entry a, RaceStandings.Entry b)
        {
            int group = Group(a.Status).CompareTo(Group(b.Status));
            if (group != 0) return group;
            return a.Status == RaceStandings.Status.Finished ? a.Place.CompareTo(b.Place) : 0;
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
