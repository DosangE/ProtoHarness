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
        // The steepest lip a kicker has: the runner leaves it climbing at this times its forward speed.
        public const float MaxLaunch = 0.5f;
        public const float MinShortcutRadius = 10f;

        // One piece of the circuit. A straight (Radius 0) keeps its Length; an arc turns Degrees (positive
        // right) at Radius and its length follows from those. EndGrade is where the grade eases to over the piece.
        // EndHalfWidth is where the road's half width goes to over it; 0 means the track's roadHalfWidth.
        // A gap is a flat straight with no road surface. A kicker is a straight whose end is a lip: a runner on the road
        // leaves it climbing at Launch times its forward speed, so the faster it comes the farther it flies (user
        // 2026-10-25, Finger Lanes: the bridge and the clock tower throw a kart by its speed). Without it the ground snap
        // holds a runner to any crest (RunnerMotor).
        [Serializable]
        public struct Segment
        {
            [SerializeField] private float length;
            [SerializeField] private float radius;
            [SerializeField] private float degrees;
            [SerializeField] private float endGrade;
            [SerializeField] private float endHalfWidth;
            [SerializeField] private bool gap;
            [SerializeField] private float launch;

            private Segment(float length, float radius, float degrees, float endGrade, float endHalfWidth, bool gap, float launch)
            {
                this.length = length;
                this.radius = radius;
                this.degrees = degrees;
                this.endGrade = endGrade;
                this.endHalfWidth = endHalfWidth;
                this.gap = gap;
                this.launch = launch;
            }

            public static Segment Straight(float length, float endGrade = 0f, float endHalfWidth = 0f) => new Segment(length, 0f, 0f, endGrade, endHalfWidth, false, 0f);
            public static Segment Gap(float length) => new Segment(length, 0f, 0f, 0f, 0f, true, 0f);
            public static Segment Arc(float radius, float degrees, float endGrade = 0f, float endHalfWidth = 0f) => new Segment(0f, radius, degrees, endGrade, endHalfWidth, false, 0f);
            public static Segment Kicker(float length, float endGrade, float launch, float endHalfWidth = 0f) => new Segment(length, 0f, 0f, endGrade, endHalfWidth, false, launch);

            public bool IsArc => radius != 0f;
            public bool IsGap => gap;
            public float Radius => radius;
            public float Degrees => degrees;
            public float EndGrade => endGrade;
            public float EndHalfWidth => endHalfWidth;
            public float Launch => launch;

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

        // A shortcut off the road's right side (user 2026-10-25, Finger Lanes' nails): from the main road at FromS a right
        // quarter turn of Radius, Straight metres, another right quarter turn, back onto the main road at ToS. Its road is
        // HalfWidth either side; the main road's right guard opens where the two roads cross (ShortcutGuardGaps).
        [Serializable]
        public struct Shortcut
        {
            [SerializeField] private float fromS;
            [SerializeField] private float toS;
            [SerializeField] private float radius;
            [SerializeField] private float straight;
            [SerializeField] private float halfWidth;

            public Shortcut(float fromS, float toS, float radius, float straight, float halfWidth)
            {
                this.fromS = fromS;
                this.toS = toS;
                this.radius = radius;
                this.straight = straight;
                this.halfWidth = halfWidth;
            }

            public float FromS => fromS;
            public float ToS => toS;
            public float Radius => radius;
            public float Straight => straight;
            public float HalfWidth => halfWidth;
            public float Length => Mathf.PI * radius + straight;
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
        [SerializeField] private List<Shortcut> shortcuts = new List<Shortcut>();
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
        public IReadOnlyList<Shortcut> Shortcuts => shortcuts;
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

        // The closed centerline of one lap starting at origin heading yawDegrees (0 = +z), with the swing poles, the
        // kickers' crests and the shortcuts added.
        public Centerline BuildCenterline(Vector3 origin, float yawDegrees)
        {
            if (!TryValidate(out string error)) throw new InvalidOperationException(error);
            var line = new Centerline(origin, yawDegrees);
            for (int i = 0; i < segments.Count; i++) segments[i].AppendTo(line);
            line.Close();
            for (int i = 0; i < poles.Count; i++)
                line.AddPole(line.FrameAt(poles[i].S).TransformPoint(new Vector3(poles[i].Offset, 0f, 0f)), poles[i].Reach);
            double s = 0d;
            for (int i = 0; i < segments.Count; i++)
            {
                s += segments[i].Length;
                if (segments[i].Launch > 0f) line.AddLaunch(s, segments[i].Launch);
            }
            for (int i = 0; i < shortcuts.Count; i++)
                line.AddBranch(shortcuts[i].FromS, shortcuts[i].ToS, ShortcutLine(line, shortcuts[i]), HalfWidthAt(shortcuts[i].FromS));
            return line;
        }

        // A shortcut's own road as an open line: from the main line's frame at its FromS, a right quarter turn, the
        // straight, a right quarter turn, all flat. The race (Centerline.AddBranch) and the road (CircuitScenery) use it.
        public static Centerline ShortcutLine(Centerline main, Shortcut shortcut)
        {
            if (main == null) throw new ArgumentNullException(nameof(main));
            TrackFrame frame = main.FrameAt(shortcut.FromS);
            var line = new Centerline(frame.Position, Mathf.Atan2(frame.Forward.x, frame.Forward.z) * Mathf.Rad2Deg);
            line.AppendArc(shortcut.Radius, 90f, 0f);
            line.AppendStraight(shortcut.Straight, 0f);
            line.AppendArc(shortcut.Radius, 90f, 0f);
            return line;
        }

        // Where a shortcut's road meets a main road of mainHalfWidth, measured from the junction: along the main road, the
        // inner and the outer edge of the shortcut (Radius -+ HalfWidth round its corner pole) cross the main road's edge
        // at inner and outer metres; along the shortcut, its inner and outer edge leave the main road at innerS and outerS.
        public static void Junction(Shortcut shortcut, float mainHalfWidth, out float inner, out float outer, out float innerS, out float outerS)
        {
            float r = shortcut.Radius, w = shortcut.HalfWidth, edge = r - mainHalfWidth;
            inner = Mathf.Sqrt((r - w) * (r - w) - edge * edge);
            outer = Mathf.Sqrt((r + w) * (r + w) - edge * edge);
            innerS = r * Mathf.Acos(edge / (r - w));
            outerS = r * Mathf.Acos(edge / (r + w));
        }

        // The S stretches of the main road with no right guard: where each shortcut leaves and where it comes back, the
        // crossing of the two roads widened by a guard's thickness either side. In lap order of the shortcuts.
        public List<(double From, double To)> ShortcutGuardGaps()
        {
            var gaps = new List<(double From, double To)>(shortcuts.Count * 2);
            const float guard = RoadMeshBuilder.GuardThickness;
            for (int i = 0; i < shortcuts.Count; i++)
            {
                Shortcut shortcut = shortcuts[i];
                Junction(shortcut, HalfWidthAt(shortcut.FromS), out float inner, out float outer, out _, out _);
                gaps.Add((shortcut.FromS + inner - guard, shortcut.FromS + outer + guard));
                Junction(shortcut, HalfWidthAt(shortcut.ToS), out inner, out outer, out _, out _);
                gaps.Add((shortcut.ToS - outer - guard, shortcut.ToS - inner + guard));
            }
            return gaps;
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
                if (segment.Launch != 0f && (segment.IsArc || segment.IsGap || !(segment.Launch > 0f && segment.Launch <= MaxLaunch)))
                    return Fail($"segment {i}: a launch ({segment.Launch}) must be on a road straight and within (0, {MaxLaunch}].", out error);
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
            for (int i = 0; i < shortcuts.Count; i++)
                if (!TryValidateShortcut(i, line, lap, out error)) return false;
            error = null;
            return true;
        }

        // A shortcut leaves and rejoins the closed line where the main road is a flat straight for the whole crossing, is
        // narrower than the main road and wider than a corner pole's reach allows, and its end meets the main line at ToS.
        private bool TryValidateShortcut(int i, Centerline line, double lap, out string error)
        {
            Shortcut shortcut = shortcuts[i];
            const float guard = RoadMeshBuilder.GuardThickness;
            double from = shortcut.FromS, to = shortcut.ToS;
            if (!(from > guard) || !(to > from) || !(to < lap - guard))
                return Fail($"shortcut {i}: it must leave at S {from} and come back at S {to}, later in the same lap.", out error);
            if (!(shortcut.Radius >= MinShortcutRadius) || float.IsInfinity(shortcut.Radius) || !(shortcut.Straight > 0f) || float.IsInfinity(shortcut.Straight))
                return Fail($"shortcut {i}: the radius must be at least {MinShortcutRadius} m and the straight positive.", out error);
            float mainWidth = Mathf.Min(HalfWidthAt(from), HalfWidthAt(to));
            if (!(shortcut.HalfWidth >= MinHalfWidth && shortcut.HalfWidth < mainWidth) || !(shortcut.Radius - shortcut.HalfWidth > mainWidth))
                return Fail($"shortcut {i}: the half width {shortcut.HalfWidth} must be within [{MinHalfWidth}, {mainWidth}) and the radius beyond the main road ({mainWidth} m) by more than it.", out error);
            Junction(shortcut, HalfWidthAt(from), out _, out float leaving, out _, out _);
            Junction(shortcut, HalfWidthAt(to), out _, out float joining, out _, out _);
            if (from + leaving + guard >= to - joining - guard)
                return Fail($"shortcut {i}: where it leaves and where it comes back overlap.", out error);
            for (double s = from; s <= from + leaving + guard; s += 0.5d)
                if (!FlatStraightAt(line, s)) return Fail($"shortcut {i}: the main road must be a flat straight where it leaves (S {s:F1}).", out error);
            for (double s = to - joining - guard; s <= to; s += 0.5d)
                if (!FlatStraightAt(line, s)) return Fail($"shortcut {i}: the main road must be a flat straight where it comes back (S {s:F1}).", out error);
            List<(double Start, double End)> gaps = Gaps();
            for (int g = 0; g < gaps.Count; g++)
                if ((gaps[g].End > from && gaps[g].Start < from + leaving + guard) || (gaps[g].End > to - joining - guard && gaps[g].Start < to))
                    return Fail($"shortcut {i}: a crossing is over a gap.", out error);
            Centerline own = ShortcutLine(line, shortcut);
            TrackFrame end = own.FrameAt(own.EndS), join = line.FrameAt(to);
            Vector3 miss = end.Position - join.Position;
            float yaw = Vector3.Angle(end.Forward, join.Forward);
            if (miss.magnitude > 0.5f || yaw > 1f)
                return Fail($"shortcut {i}: it ends {miss.magnitude:F2} m and {yaw:F2} degrees away from the main road at S {to}; the limits are 0.50 m and 1.00 degrees.", out error);
            error = null;
            return true;
        }

        private static bool FlatStraightAt(Centerline line, double s)
        {
            TrackFrame frame = line.FrameAt(s);
            return frame.Curvature == 0f && Mathf.Abs(frame.Grade) < 1e-4f;
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
