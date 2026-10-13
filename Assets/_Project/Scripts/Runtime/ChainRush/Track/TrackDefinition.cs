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
    [CreateAssetMenu(menuName = "ProtoHarness/ChainRush/Track Definition")]
    public sealed class TrackDefinition : ScriptableObject
    {
        public const float MinRadius = 30f;
        public const float MinGapRunway = 15f;
        public const float MaxGrade = 0.2f;

        // One piece of the circuit. A straight (Radius 0) keeps its Length; an arc turns Degrees (positive
        // right) at Radius and its length follows from those. EndGrade is where the grade eases to over the piece.
        // A gap is a flat straight with no road surface.
        [Serializable]
        public struct Segment
        {
            [SerializeField] private float length;
            [SerializeField] private float radius;
            [SerializeField] private float degrees;
            [SerializeField] private float endGrade;
            [SerializeField] private bool gap;

            private Segment(float length, float radius, float degrees, float endGrade, bool gap)
            {
                this.length = length;
                this.radius = radius;
                this.degrees = degrees;
                this.endGrade = endGrade;
                this.gap = gap;
            }

            public static Segment Straight(float length, float endGrade = 0f) => new Segment(length, 0f, 0f, endGrade, false);
            public static Segment Gap(float length) => new Segment(length, 0f, 0f, 0f, true);
            public static Segment Arc(float radius, float degrees, float endGrade = 0f) => new Segment(0f, radius, degrees, endGrade, false);

            public bool IsArc => radius != 0f;
            public bool IsGap => gap;
            public float Radius => radius;
            public float Degrees => degrees;
            public float EndGrade => endGrade;

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

        [SerializeField] private List<Segment> segments = new List<Segment>
        {
            // Straight A (140 m): a hill (0 -> +10% -> -10% -> 0, 2 m high) and a jump gap of 6 m.
            Segment.Straight(15f), Segment.Straight(20f, 0.1f), Segment.Straight(40f, -0.1f), Segment.Straight(20f, 0f),
            Segment.Straight(9f), Segment.Straight(15f), Segment.Gap(6f), Segment.Straight(15f),
            Segment.Arc(40f, 180f),
            // Straight B (140 m): a grapple gap of 16 m with its runways.
            Segment.Straight(30f), Segment.Straight(15f), Segment.Gap(16f), Segment.Straight(15f), Segment.Straight(64f),
            Segment.Arc(40f, 180f),
        };

        // The anchor hangs over the middle of the grapple gap: 140 + pi x 40 + 30 + 15 + 8.
        [SerializeField] private List<Anchor> anchors = new List<Anchor> { new Anchor(318.66f, 0f, 10f) };
        [Tooltip("Checkpoints as fractions of a lap, ascending, strictly between 0 and 1.")]
        [SerializeField] private float[] checkpointFractions = { 0.25f, 0.5f, 0.75f };
        [Tooltip("Start slots: metres behind the start line. A single runner uses the first.")]
        [SerializeField] private float[] startSlots = { 0f };
        [SerializeField] private int lapCount = 3;
        [SerializeField] private float roadHalfWidth = RoadProfile.DeckHalfWidth;
        [SerializeField] private float roadThickness = RoadProfile.DeckThickness;

        public IReadOnlyList<Segment> Segments => segments;
        public IReadOnlyList<Anchor> Anchors => anchors;
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

        // The closed centerline of one lap starting at origin heading yawDegrees (0 = +z).
        public Centerline BuildCenterline(Vector3 origin, float yawDegrees)
        {
            if (!TryValidate(out string error)) throw new InvalidOperationException(error);
            var line = new Centerline(origin, yawDegrees);
            for (int i = 0; i < segments.Count; i++) segments[i].AppendTo(line);
            line.Close();
            return line;
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
                if (!(anchor.Height >= 1f) || Mathf.Abs(anchor.Offset) > roadHalfWidth) return Fail($"anchor {i}: the height must be at least 1 m and the offset within the road.", out error);
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
