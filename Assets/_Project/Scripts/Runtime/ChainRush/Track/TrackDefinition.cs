using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoHarness.ChainRush.Track
{
    // A hand-built closed circuit (COURSE.md 7-2, T4): straight, arc and vertical-curve segments in driving
    // order from the start line, grapple anchors over the gaps, checkpoints, start slots and the lap count.
    // The defaults are the "stadium": a 140 m straight with a hill and a jump gap, a half turn of R40, a 140 m
    // straight with a grapple gap, and a second half turn, about 531 m a lap. TryValidate is the one source of
    // truth for OnValidate, the builder and CircuitRace: it checks the segment rules and that the end meets the start.
    // The road is roadHalfWidth wide unless a piece names its own end width: then the width goes linearly from the
    // previous piece's end width to it over the piece (the first piece starts from the last one's, the lap being closed).
    // Swing poles beside the road (Pole) join the corner poles at the arcs' centers (user 2026-10-22, the Skyway).
    [CreateAssetMenu(menuName = "ProtoHarness/ChainRush/Track Definition")]
    public sealed class TrackDefinition : ScriptableObject
    {
        public const float MinRadius = 20f;
        public const float MinGapRunway = 15f;
        public const float MaxGrade = 0.2f;
        public const float MinHalfWidth = 3f;
        public const float MaxHalfWidth = 20f;
        // Half width gained or lost per metre of a piece: 0.25 turns a guard at most 14 degrees off the track, well
        // inside the 60 degrees RunnerMotor still takes for a guard (|normal . track forward| <= 0.5).
        public const float MaxWidthChange = 0.25f;
        public const float MinPoleReach = 2f;
        public const float MaxPoleReach = 40f;

        // One piece of the circuit. A straight (Radius 0) keeps its Length; an arc turns Degrees (positive
        // right) at Radius and its length follows from those. EndGrade is where the grade eases to over the piece.
        // EndHalfWidth is where the road's half width goes to over it; 0 means the track's roadHalfWidth.
        // A gap is a flat straight with no road surface.
        [Serializable]
        public struct Segment
        {
            [SerializeField] private float length;
            [SerializeField] private float radius;
            [SerializeField] private float degrees;
            [SerializeField] private float endGrade;
            [SerializeField] private float endHalfWidth;
            [SerializeField] private bool gap;

            private Segment(float length, float radius, float degrees, float endGrade, float endHalfWidth, bool gap)
            {
                this.length = length;
                this.radius = radius;
                this.degrees = degrees;
                this.endGrade = endGrade;
                this.endHalfWidth = endHalfWidth;
                this.gap = gap;
            }

            public static Segment Straight(float length, float endGrade = 0f, float endHalfWidth = 0f) => new Segment(length, 0f, 0f, endGrade, endHalfWidth, false);
            public static Segment Gap(float length) => new Segment(length, 0f, 0f, 0f, 0f, true);
            public static Segment Arc(float radius, float degrees, float endGrade = 0f, float endHalfWidth = 0f) => new Segment(0f, radius, degrees, endGrade, endHalfWidth, false);

            public bool IsArc => radius != 0f;
            public bool IsGap => gap;
            public float Radius => radius;
            public float Degrees => degrees;
            public float EndGrade => endGrade;
            public float EndHalfWidth => endHalfWidth;

            // The same float expression as Centerline.AppendArc, so the S values match exactly.
            public float Length => IsArc ? radius * Mathf.Abs(degrees) * Mathf.Deg2Rad : length;

            public void AppendTo(Centerline line)
            {
                if (IsArc) line.AppendArc(radius, degrees, endGrade);
                else line.AppendStraight(length, endGrade);
            }
        }

        // A grapple anchor over a gap: S along the lap, Offset to the right of the centerline, Height above the road.
        [Serializable]
        public struct Anchor
        {
            [SerializeField] private float s;
            [SerializeField] private float offset;
            [SerializeField] private float height;

            public Anchor(float s, float offset, float height)
            {
                this.s = s;
                this.offset = offset;
                this.height = height;
            }

            public float S => s;
            public float Offset => offset;
            public float Height => height;
        }

        // A swing pole that is not an arc's center: S along the lap, Offset to the right of the centerline (on the road),
        // standing at road level; a runner can hook it from Reach metres plus the motor's poleReach (Centerline.AddPole).
        [Serializable]
        public struct Pole
        {
            [SerializeField] private float s;
            [SerializeField] private float offset;
            [SerializeField] private float reach;

            public Pole(float s, float offset, float reach)
            {
                this.s = s;
                this.offset = offset;
                this.reach = reach;
            }

            public float S => s;
            public float Offset => offset;
            public float Reach => reach;
        }

        [SerializeField] private List<Segment> segments = new List<Segment>
        {
            // Straight A (140 m): a hill (0 -> +10% -> -10% -> 0, 2 m high) and a jump gap of 6 m.
            Segment.Straight(15f), Segment.Straight(20f, 0.1f), Segment.Straight(40f, -0.1f), Segment.Straight(20f, 0f),
            Segment.Straight(9f), Segment.Straight(15f), Segment.Gap(6f), Segment.Straight(15f),
            Segment.Arc(22f, 90f),
            // Straight B (60 m), then the second corner (R4c 2026-10-18: four R22 quarter turns, a square circuit).
            Segment.Straight(60f), Segment.Arc(22f, 90f),
            // Straight C (140 m): a grapple gap of 16 m with its runways.
            Segment.Straight(30f), Segment.Straight(15f), Segment.Gap(16f), Segment.Straight(15f), Segment.Straight(64f),
            Segment.Arc(22f, 90f),
            // Straight D (60 m), then the last corner to the start line.
            Segment.Straight(60f), Segment.Arc(22f, 90f),
        };

        // The anchor hangs over the middle of the grapple gap: 140 + 60 + pi x 22 + 30 + 15 + 8.
        [SerializeField] private List<Anchor> anchors = new List<Anchor> { new Anchor(322.12f, 0f, 10f) };
        [SerializeField] private List<Pole> poles = new List<Pole>();
        [Tooltip("Checkpoints as fractions of a lap, ascending, strictly between 0 and 1.")]
        [SerializeField] private float[] checkpointFractions = { 0.25f, 0.5f, 0.75f };
        [Tooltip("Start slots: metres behind the start line. A single runner uses the first.")]
        [SerializeField] private float[] startSlots = { 0f };
        [SerializeField] private int lapCount = 3;
        [SerializeField] private float roadHalfWidth = RoadProfile.DeckHalfWidth;
        [SerializeField] private float roadThickness = RoadProfile.DeckThickness;

        public IReadOnlyList<Segment> Segments => segments;
        public IReadOnlyList<Anchor> Anchors => anchors;
        public IReadOnlyList<Pole> Poles => poles;
        public int LapCount => lapCount;
        public float RoadHalfWidth => roadHalfWidth;
        public float RoadThickness => roadThickness;
        public int CheckpointCount => checkpointFractions.Length;
        public int StartSlotCount => startSlots.Length;

        // Metres in one lap, summed in double like Centerline does, so it equals the closed centerline's EndS.
        public double LapLength
        {
            get
            {
                double length = 0d;
                for (int i = 0; i < segments.Count; i++) length += segments[i].Length;
                return length;
            }
        }

        public double CheckpointS(int index)
        {
            if (index < 0 || index >= checkpointFractions.Length) throw new ArgumentOutOfRangeException(nameof(index), index, "No such checkpoint.");
            return checkpointFractions[index] * LapLength;
        }

        // How far behind the start line a start slot sits (0 for the first slot of a single runner).
        public float StartSlot(int index)
        {
            if (index < 0 || index >= startSlots.Length) throw new ArgumentOutOfRangeException(nameof(index), index, "No such start slot.");
            return startSlots[index];
        }

        // The S range of every gap, in lap order.
        public List<(double Start, double End)> Gaps()
        {
            var gaps = new List<(double Start, double End)>();
            double s = 0d;
            for (int i = 0; i < segments.Count; i++)
            {
                double next = s + segments[i].Length;
                if (segments[i].IsGap) gaps.Add((s, next));
                s = next;
            }
            return gaps;
        }

        // The closed centerline of one lap starting at origin heading yawDegrees (0 = +z), with the swing poles added.
        public Centerline BuildCenterline(Vector3 origin, float yawDegrees)
        {
            if (!TryValidate(out string error)) throw new InvalidOperationException(error);
            var line = new Centerline(origin, yawDegrees);
            for (int i = 0; i < segments.Count; i++) segments[i].AppendTo(line);
            line.Close();
            for (int i = 0; i < poles.Count; i++)
                line.AddPole(line.FrameAt(poles[i].S).TransformPoint(new Vector3(poles[i].Offset, 0f, 0f)), poles[i].Reach);
            return line;
        }

        // A piece's end half width, the track's roadHalfWidth where it names none.
        public float EndHalfWidth(int index)
        {
            if (index < 0 || index >= segments.Count) throw new ArgumentOutOfRangeException(nameof(index), index, "No such segment.");
            float own = segments[index].EndHalfWidth;
            return own > 0f ? own : roadHalfWidth;
        }

        // A piece's start half width: the end width of the piece before it, going round the lap.
        public float StartHalfWidth(int index) => EndHalfWidth(index == 0 ? segments.Count - 1 : index - 1);

        // The road's half width at S along the lap (S is folded into one lap).
        public float HalfWidthAt(double s)
        {
            double lap = LapLength;
            if (double.IsNaN(s) || double.IsInfinity(s)) throw new ArgumentOutOfRangeException(nameof(s), s, "S must be finite.");
            s -= Math.Floor(s / lap) * lap;
            double start = 0d;
            for (int i = 0; i < segments.Count; i++)
            {
                double length = segments[i].Length;
                if (s <= start + length || i == segments.Count - 1)
                {
                    float t = (float)Math.Min(1d, Math.Max(0d, (s - start) / length));
                    float from = StartHalfWidth(i);
                    return from + (EndHalfWidth(i) - from) * t;
                }
                start += length;
            }
            return roadHalfWidth;
        }

        // The widest the road gets anywhere on the lap.
        public float WidestHalfWidth
        {
            get
            {
                float widest = roadHalfWidth;
                for (int i = 0; i < segments.Count; i++) widest = Mathf.Max(widest, EndHalfWidth(i));
                return widest;
            }
        }

        public bool TryValidate(out string error)
        {
            if (segments == null || segments.Count < 2) return Fail("a circuit needs at least two segments.", out error);
            var startGrades = new float[segments.Count];
            float grade = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                Segment segment = segments[i];
                startGrades[i] = grade;
                if (float.IsNaN(segment.EndGrade) || Mathf.Abs(segment.EndGrade) > MaxGrade) return Fail($"segment {i}: the end grade {segment.EndGrade} must be within +-{MaxGrade}.", out error);
                if (segment.IsArc)
                {
                    if (segment.IsGap) return Fail($"segment {i}: a gap must be a straight.", out error);
                    if (!(segment.Radius >= MinRadius) || float.IsInfinity(segment.Radius)) return Fail($"segment {i}: an arc radius must be at least {MinRadius} m.", out error);
                    if (!(Mathf.Abs(segment.Degrees) > 0f) || Mathf.Abs(segment.Degrees) > 180f) return Fail($"segment {i}: an arc turns between 0 and 180 degrees, not {segment.Degrees}.", out error);
                }
                else
                {
                    if (!(segment.Length > 0f) || float.IsInfinity(segment.Length)) return Fail($"segment {i}: a straight needs a positive length.", out error);
                    if (segment.IsGap && (grade != 0f || segment.EndGrade != 0f)) return Fail($"segment {i}: a gap must be flat, with a flat road before it.", out error);
                }
                grade = segment.EndGrade;
            }
            for (int i = 0; i < segments.Count; i++)
            {
                if (!segments[i].IsGap) continue;
                float before = FlatRun(i - 1, -1, startGrades);
                float after = FlatRun(i + 1, 1, startGrades);
                if (before < MinGapRunway || after < MinGapRunway)
                    return Fail($"segment {i}: a gap needs at least {MinGapRunway} m of flat straight before ({before:F1} m) and after ({after:F1} m).", out error);
            }
            double lap = LapLength;
            if (lap < 100d) return Fail($"a lap of {lap:F1} m is too short (at least 100 m).", out error);
            if (lapCount < 1 || lapCount > 20) return Fail($"the lap count {lapCount} must be within [1, 20].", out error);
            if (!(roadHalfWidth >= 3f) || roadHalfWidth > 12f || !(roadThickness > 0f)) return Fail("the road half width must be within [3, 12] m and the thickness positive.", out error);
            for (int i = 0; i < segments.Count; i++)
            {
                float own = segments[i].EndHalfWidth;
                if (own != 0f && !(own >= MinHalfWidth && own <= MaxHalfWidth))
                    return Fail($"segment {i}: the end half width {own} must be 0 (the road's) or within [{MinHalfWidth}, {MaxHalfWidth}] m.", out error);
            }
            for (int i = 0; i < segments.Count; i++)
            {
                float change = Mathf.Abs(EndHalfWidth(i) - StartHalfWidth(i));
                if (segments[i].IsGap && change != 0f) return Fail($"segment {i}: the road must keep its width across a gap.", out error);
                if (change > MaxWidthChange * segments[i].Length)
                    return Fail($"segment {i}: the half width changes {change:F2} m over {segments[i].Length:F1} m, more than {MaxWidthChange} a metre.", out error);
            }
            float previous = 0f;
            for (int i = 0; i < checkpointFractions.Length; i++)
            {
                if (!(checkpointFractions[i] > previous) || !(checkpointFractions[i] < 1f))
                    return Fail($"checkpoint {i} ({checkpointFractions[i]}) must be after the one before it and below 1.", out error);
                previous = checkpointFractions[i];
            }
            if (startSlots == null || startSlots.Length < 1) return Fail("at least one start slot is needed.", out error);
            for (int i = 0; i < startSlots.Length; i++)
                if (!(startSlots[i] >= 0f) || startSlots[i] > 40f) return Fail($"start slot {i} ({startSlots[i]}) must be within [0, 40] m behind the line.", out error);
            List<(double Start, double End)> gaps = Gaps();
            for (int i = 0; i < anchors.Count; i++)
            {
                Anchor anchor = anchors[i];
                bool overGap = false;
                for (int g = 0; g < gaps.Count; g++)
                    if (anchor.S >= gaps[g].Start && anchor.S <= gaps[g].End) overGap = true;
                if (!overGap) return Fail($"anchor {i} at S {anchor.S} is not over a gap.", out error);
                if (!(anchor.Height >= 1f) || !(Mathf.Abs(anchor.Offset) <= HalfWidthAt(anchor.S))) return Fail($"anchor {i}: the height must be at least 1 m and the offset within the road.", out error);
            }
            for (int i = 0; i < poles.Count; i++)
            {
                Pole pole = poles[i];
                if (!(pole.S >= 0f) || !(pole.S < lap)) return Fail($"pole {i}: S {pole.S} must be within the lap [0, {lap:F1}).", out error);
                if (!(Mathf.Abs(pole.Offset) <= HalfWidthAt(pole.S))) return Fail($"pole {i}: the offset {pole.Offset} must be within the road ({HalfWidthAt(pole.S):F1} m there).", out error);
                if (!(pole.Reach >= MinPoleReach && pole.Reach <= MaxPoleReach)) return Fail($"pole {i}: the reach {pole.Reach} must be within [{MinPoleReach}, {MaxPoleReach}] m.", out error);
            }
            var line = new Centerline(Vector3.zero, 0f);
            for (int i = 0; i < segments.Count; i++) segments[i].AppendTo(line);
            if (!line.TryClose(out string closeError)) return Fail("the circuit does not close: " + closeError, out error);
            error = null;
            return true;
        }

        // The length of consecutive flat straights (no gap, flat at both ends) from segment `from` in direction
        // `step`, going round the lap.
        private float FlatRun(int from, int step, float[] startGrades)
        {
            float run = 0f;
            int count = segments.Count;
            for (int n = 0; n < count; n++)
            {
                int i = (((from + step * n) % count) + count) % count;
                Segment segment = segments[i];
                if (segment.IsArc || segment.IsGap || startGrades[i] != 0f || segment.EndGrade != 0f) break;
                run += segment.Length;
            }
            return run;
        }

        private static bool Fail(string message, out string error)
        {
            error = "TrackDefinition: " + message;
            return false;
        }

        private void OnValidate()
        {
            if (!TryValidate(out string error)) Debug.LogError(error, this);
        }
    }
}
