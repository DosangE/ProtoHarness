using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoHarness.ChainRush.Track
{
    // A hand-built circuit or one-way course (COURSE.md 7-2, T4): straight, arc and vertical-curve segments in driving
    // order from the start line, grapple anchors over the gaps, checkpoints, start slots and the lap count.
    // The defaults are the "stadium": a 140 m straight with a hill and a jump gap, a half turn of R40, a 140 m
    // straight with a grapple gap, and a second half turn, about 531 m a lap. TryValidate is the one source of
    // truth for OnValidate, the builder and CircuitRace: it checks the segment rules and, on a circuit, closure.
    // The road is roadHalfWidth wide unless a piece names its own end width: then the width goes linearly from the
    // previous piece's end width to it over the piece (the first starts from the last only on a closed lap).
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
        // A boost zone is BoostAt metres inside a road straight, without a kicker; crossing it cuts the runner's booster
        // and raises its forward speed (user 2026-10-30, Palm Coast). Its flag also lets validation reject a pad at zero.
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
            [SerializeField] private bool boostZone;
            [SerializeField] private float boostAt;

            private Segment(float length, float radius, float degrees, float endGrade, float endHalfWidth, bool gap, float launch, bool boostZone = false, float boostAt = 0f)
            {
                this.length = length;
                this.radius = radius;
                this.degrees = degrees;
                this.endGrade = endGrade;
                this.endHalfWidth = endHalfWidth;
                this.gap = gap;
                this.launch = launch;
                this.boostZone = boostZone;
                this.boostAt = boostAt;
            }

            public static Segment Straight(float length, float endGrade = 0f, float endHalfWidth = 0f) => new Segment(length, 0f, 0f, endGrade, endHalfWidth, false, 0f);
            public static Segment Gap(float length) => new Segment(length, 0f, 0f, 0f, 0f, true, 0f);
            public static Segment Arc(float radius, float degrees, float endGrade = 0f, float endHalfWidth = 0f) => new Segment(0f, radius, degrees, endGrade, endHalfWidth, false, 0f);
            public static Segment Kicker(float length, float endGrade, float launch, float endHalfWidth = 0f) => new Segment(length, 0f, 0f, endGrade, endHalfWidth, false, launch);
            public static Segment BoostStraight(float length, float boostAt, float endGrade = 0f, float endHalfWidth = 0f) => new Segment(length, 0f, 0f, endGrade, endHalfWidth, false, 0f, true, boostAt);

            public bool IsArc => radius != 0f;
            public bool IsGap => gap;
            public float Radius => radius;
            public float Degrees => degrees;
            public float EndGrade => endGrade;
            public float EndHalfWidth => endHalfWidth;
            public float Launch => launch;
            public bool HasBoostZone => boostZone;
            public float BoostAt => boostAt;

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

        // A road edge without a guard over [FromS, ToS], on either a circuit or an open course.
        [Serializable]
        public struct OpenEdge
        {
            [SerializeField] private float fromS;
            [SerializeField] private float toS;
            [SerializeField] private bool left;

            public OpenEdge(float fromS, float toS, bool left)
            {
                this.fromS = fromS;
                this.toS = toS;
                this.left = left;
            }

            public float FromS => fromS;
            public float ToS => toS;
            public bool Left => left;
        }

        // A dead-end widening beside the road (user 2026-11-02, the Skyway's on-ramp: "넓은 지상도로 … 정면은 가드로
        // 막혀있어야"): from FromS the road grows Width metres wider on one side over Taper metres, runs on as one wide road
        // and ends at ToS against a guard square across the widening. The main road's guard on that side opens over it
        // (BayGuardGaps); the racing line stays the main road's.
        [Serializable]
        public struct Bay
        {
            [SerializeField] private float fromS;
            [SerializeField] private float toS;
            [SerializeField] private float width;
            [SerializeField] private float taper;
            [SerializeField] private bool left;
            [SerializeField] private bool straightDeck;
            [SerializeField] private float extension;

            public Bay(float fromS, float toS, float width, float taper, bool left)
            {
                this.fromS = fromS;
                this.toS = toS;
                this.width = width;
                this.taper = taper;
                this.left = left;
                straightDeck = false;
                extension = 0f;
            }

            // User 2026-11-02: "기존 넓은 고가도로에 합류". The racing straight uses the rightmost 12 m;
            // the rest lies on its left. The full deck keeps its own straight beyond both junctions, closed at both ends.
            public static Bay Highway(float fromS, float toS, float width, float extension)
            {
                var bay = new Bay(fromS, toS, width, 0f, true);
                bay.straightDeck = true;
                bay.extension = extension;
                return bay;
            }

            public float FromS => fromS;
            public float ToS => toS;
            public float Width => width;
            public float Taper => taper;
            public bool Left => left;
            public bool StraightDeck => straightDeck;
            public float Extension => extension;

            public Centerline DeckLine(Centerline main)
            {
                if (main == null) throw new ArgumentNullException(nameof(main));
                if (!straightDeck) throw new InvalidOperationException("Only a straight deck has its own line.");
                TrackFrame frame = main.FrameAt(fromS);
                var line = new Centerline(frame.Position - frame.Forward * extension,
                    Mathf.Atan2(frame.Forward.x, frame.Forward.z) * Mathf.Rad2Deg);
                line.AppendStraight(toS - fromS + 2f * extension);
                return line;
            }

            // A 12 m quarter-turn ramp meets the deck's right edge. Inner is the ramp distance to the
            // first touching edge; outer is the distance along the straight to that same point.
            public static void DeckJunction(float radius, float main, out float inner, out float outer, float margin = 0f)
            {
                if (!(margin >= 0f) || !(radius > main + margin) || !(main > 0f) || float.IsInfinity(radius))
                    throw new ArgumentException("A deck junction needs a finite radius beyond the ramp width and nonnegative margin.");
                inner = radius * Mathf.Acos((radius - main) / (radius + main + margin));
                outer = Mathf.Sqrt((radius + main + margin) * (radius + main + margin) - (radius - main - margin) * (radius - main - margin));
            }

            // How wide the bay is at s (0 outside it): growing linearly over the taper, then Width.
            public float WidthAt(double s)
            {
                if (s < fromS || s > toS) return 0f;
                return taper > 0f ? width * Mathf.Min(1f, (float)((s - fromS) / taper)) : width;
            }
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
        [SerializeField] private List<Bay> bays = new List<Bay>();
        [SerializeField] private List<OpenEdge> openEdges = new List<OpenEdge>();
        [Tooltip("Checkpoints as fractions of a lap, ascending, strictly between 0 and 1.")]
        [SerializeField] private float[] checkpointFractions = { 0.25f, 0.5f, 0.75f };
        [Tooltip("Start slots: metres behind the start line. A single runner uses the first.")]
        [SerializeField] private float[] startSlots = { 0f };
        [SerializeField] private int lapCount = 3;
        [SerializeField] private bool openCourse;
        [Tooltip("Flat road after the finish, excluded from LapLength (open courses only).")]
        [SerializeField] private float finishRunout = 40f;
        [Tooltip("Flat road behind the start, ending at a transverse guard (open courses only).")]
        [SerializeField] private float startRunway = 10f;
        [SerializeField] private float roadHalfWidth = RoadProfile.DeckHalfWidth;
        [SerializeField] private float roadThickness = RoadProfile.DeckThickness;

        // A closed ring of two right half turns round one point, made in code with no anchors, checkpoints or asset (the
        // tutorial, user 2026-11-06). From the origin heading +z its centre, where the corner pole stands, is radius to
        // the right. Both arcs share that pole, so it comes twice from Centerline.GetPoles.
        public static TrackDefinition Ring(string name, float radius)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A ring needs a name.", nameof(name));
            var ring = CreateInstance<TrackDefinition>();
            ring.name = name;
            ring.segments = new List<Segment> { Segment.Arc(radius, 180f), Segment.Arc(radius, 180f) };
            ring.anchors = new List<Anchor>();
            ring.checkpointFractions = new float[0];
            ring.lapCount = 1;
            if (ring.TryValidate(out string error)) return ring;
            Destroy(ring);
            throw new ArgumentException(error, nameof(radius));
        }

        // An in-memory one-way course, like Ring: no asset is created or saved. The supplied pieces end at the
        // finish; the flat runout is road only. Anchors and poles use the same coordinates as a circuit's.
        public static TrackDefinition OpenCourse(string name, IReadOnlyList<Segment> pieces, float finishRunout = 40f,
            float startRunway = 10f, IReadOnlyList<Anchor> anchors = null, IReadOnlyList<Pole> poles = null)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("An open course needs a name.", nameof(name));
            if (pieces == null) throw new ArgumentNullException(nameof(pieces));
            var course = CreateInstance<TrackDefinition>();
            course.name = name;
            course.openCourse = true;
            course.lapCount = 1;
            course.segments = new List<Segment>(pieces);
            course.anchors = anchors == null ? new List<Anchor>() : new List<Anchor>(anchors);
            course.poles = poles == null ? new List<Pole>() : new List<Pole>(poles);
            course.finishRunout = finishRunout;
            course.startRunway = startRunway;
            if (course.TryValidate(out string error)) return course;
            Destroy(course);
            throw new ArgumentException(error, nameof(pieces));
        }

        public IReadOnlyList<Segment> Segments => segments;
        public IReadOnlyList<Anchor> Anchors => anchors;
        public IReadOnlyList<Pole> Poles => poles;
        public IReadOnlyList<Shortcut> Shortcuts => shortcuts;
        public IReadOnlyList<Bay> Bays => bays;
        public IReadOnlyList<OpenEdge> OpenEdges => openEdges;
        public int LapCount => lapCount;
        public bool IsOpenCourse => openCourse;
        public float FinishRunout => finishRunout;
        public float StartRunway => startRunway;
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

        // The centerline starting at origin heading yawDegrees (0 = +z), with poles, kickers, pads and shortcuts.
        // An open course ends at LapLength, followed by its flat runout; only a circuit calls Close.
        public Centerline BuildCenterline(Vector3 origin, float yawDegrees)
        {
            if (!TryValidate(out string error)) throw new InvalidOperationException(error);
            var line = new Centerline(origin, yawDegrees);
            for (int i = 0; i < segments.Count; i++) segments[i].AppendTo(line);
            if (openCourse) line.AppendStraight(finishRunout, 0f);
            else line.Close();
            for (int i = 0; i < poles.Count; i++)
                line.AddPole(line.FrameAt(poles[i].S).TransformPoint(new Vector3(poles[i].Offset, 0f, 0f)), poles[i].Reach);
            double s = 0d;
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i].HasBoostZone) line.AddBoostZone(s + segments[i].BoostAt);
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

        // The S stretches of the main road with no guard on one side (Left), because a bay widens it there. In the
        // order of the bays.
        public List<(double From, double To, bool Left)> BayGuardGaps()
        {
            var gaps = new List<(double From, double To, bool Left)>(bays.Count);
            for (int i = 0; i < bays.Count; i++)
            {
                Bay bay = bays[i];
                if (!bay.StraightDeck) gaps.Add((bay.FromS, bay.ToS, bay.Left));
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

        // A piece's start half width: the previous piece's end, or the base width at an open course's start.
        public float StartHalfWidth(int index) => openCourse && index == 0 ? roadHalfWidth : EndHalfWidth(index == 0 ? segments.Count - 1 : index - 1);

        // Width at S: folded on a circuit, held at each end's width outside an open course.
        public float HalfWidthAt(double s)
        {
            double lap = LapLength;
            if (double.IsNaN(s) || double.IsInfinity(s)) throw new ArgumentOutOfRangeException(nameof(s), s, "S must be finite.");
            if (openCourse) s = Math.Max(0d, Math.Min(lap, s));
            else s -= Math.Floor(s / lap) * lap;
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
            if (openCourse)
            {
                if (lapCount != 1) return Fail("an open course needs exactly one lap.", out error);
                if (bays.Count != 0) return Fail("an open course cannot have bays.", out error);
                if (!(finishRunout >= 2f) || float.IsInfinity(finishRunout) || !(startRunway > 0f) || float.IsInfinity(startRunway))
                    return Fail("an open course needs finite end road lengths, with at least 2 m runout for finish clearance and positive start runway.", out error);
                if (segments[0].IsGap || segments[segments.Count - 1].IsGap || segments[segments.Count - 1].EndGrade != 0f)
                    return Fail("an open course must start and finish on road, with a flat end grade for its runout.", out error);
            }
            var startGrades = new float[segments.Count];
            float grade = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                Segment segment = segments[i];
                startGrades[i] = grade;
                if (float.IsNaN(segment.EndGrade) || Mathf.Abs(segment.EndGrade) > MaxGrade) return Fail($"segment {i}: the end grade {segment.EndGrade} must be within +-{MaxGrade}.", out error);
                if (segment.Launch != 0f && (segment.IsArc || segment.IsGap || !(segment.Launch > 0f && segment.Launch <= MaxLaunch)))
                    return Fail($"segment {i}: a launch ({segment.Launch}) must be on a road straight and within (0, {MaxLaunch}].", out error);
                if (segment.HasBoostZone && (segment.IsArc || segment.IsGap || segment.Launch != 0f || !(segment.BoostAt > 0f && segment.BoostAt < segment.Length)))
                    return Fail($"segment {i}: a boost zone ({segment.BoostAt}) must be inside a road straight (0, {segment.Length}), without a launch.", out error);
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
            {
                if (!(startSlots[i] >= 0f) || startSlots[i] > 40f) return Fail($"start slot {i} ({startSlots[i]}) must be within [0, 40] m behind the line.", out error);
                if (openCourse && startSlots[i] + 2f >= startRunway) return Fail($"start slot {i} needs 2 m clearance from the start guard.", out error);
            }
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
                bool onRoad = Mathf.Abs(pole.Offset) <= HalfWidthAt(pole.S);
                foreach (Bay bay in bays)
                    if (bay.StraightDeck && pole.S >= bay.FromS && pole.S <= bay.ToS)
                        onRoad |= pole.Offset >= -HalfWidthAt(pole.S) - bay.Width && pole.Offset <= HalfWidthAt(pole.S);
                if (!onRoad) return Fail($"pole {i}: the offset {pole.Offset} must be within the road ({HalfWidthAt(pole.S):F1} m there).", out error);
                if (!(pole.Reach >= MinPoleReach && pole.Reach <= MaxPoleReach)) return Fail($"pole {i}: the reach {pole.Reach} must be within [{MinPoleReach}, {MaxPoleReach}] m.", out error);
            }
            var line = new Centerline(Vector3.zero, 0f);
            for (int i = 0; i < segments.Count; i++) segments[i].AppendTo(line);
            if (!openCourse && !line.TryClose(out string closeError)) return Fail("the circuit does not close: " + closeError, out error);
            for (int i = 0; i < shortcuts.Count; i++)
                if (!TryValidateShortcut(i, line, lap, out error)) return false;
            for (int i = 0; i < bays.Count; i++)
                if (!TryValidateBay(i, line, lap, gaps, out error)) return false;
            for (int i = 0; i < openEdges.Count; i++)
                if (!TryValidateOpenEdge(i, lap, gaps, out error)) return false;
            error = null;
            return true;
        }

        // Guard openings may touch but may not overlap on the same side, including existing junction openings.
        private bool TryValidateOpenEdge(int i, double lap, List<(double Start, double End)> gaps, out string error)
        {
            OpenEdge edge = openEdges[i];
            if (!(edge.FromS >= 0f) || !(edge.ToS > edge.FromS) || !(edge.ToS <= lap))
                return Fail($"open edge {i}: [{edge.FromS}, {edge.ToS}] must be an ordered finite range within [0, {lap:F1}].", out error);
            for (int k = 0; k < i; k++)
                if (openEdges[k].Left == edge.Left && openEdges[k].ToS > edge.FromS && openEdges[k].FromS < edge.ToS)
                    return Fail($"open edge {i}: it overlaps another open edge on the same side.", out error);
            foreach ((double from, double to, bool left) in BayGuardGaps())
                if (left == edge.Left && to > edge.FromS && from < edge.ToS)
                    return Fail($"open edge {i}: it overlaps a bay opening.", out error);
            if (!edge.Left)
                foreach ((double from, double to) in ShortcutGuardGaps())
                    if (to > edge.FromS && from < edge.ToS)
                        return Fail($"open edge {i}: it overlaps a shortcut crossing.", out error);
            foreach ((double from, double to) in gaps)
                if (to > edge.FromS && from < edge.ToS)
                    return Fail($"open edge {i}: it overlaps a road gap.", out error);
            error = null;
            return true;
        }

        // A bay lies on a flat straight of one width within the lap, off any gap, its guard square across its end clear
        // of its taper; its outer edge turns away no faster than a tapering road's guard may (MaxWidthChange per metre
        // on each side); it overlaps no other bay or shortcut crossing on its side.
        private bool TryValidateBay(int i, Centerline line, double lap, List<(double Start, double End)> gaps, out string error)
        {
            Bay bay = bays[i];
            const float guard = RoadMeshBuilder.GuardThickness;
            if (!(bay.FromS >= 0f) || !(bay.ToS > bay.FromS) || !(bay.ToS < lap))
                return Fail($"bay {i}: it must run from S {bay.FromS} to a later S {bay.ToS} within the lap.", out error);
            if (!(bay.Width >= MinHalfWidth && bay.Width <= 2f * MaxHalfWidth))
                return Fail($"bay {i}: the width {bay.Width} must be within [{MinHalfWidth}, {2f * MaxHalfWidth}] m.", out error);
            if (bay.StraightDeck) return TryValidateDeck(i, out error);
            if (!(bay.Taper > 0f) || !(bay.Width <= 2f * MaxWidthChange * bay.Taper) || !(bay.FromS + bay.Taper < bay.ToS - guard))
                return Fail($"bay {i}: the taper {bay.Taper} m must be at least {bay.Width / (2f * MaxWidthChange):F1} m and end before the bay does.", out error);
            float width = HalfWidthAt(bay.FromS);
            for (double s = bay.FromS; s <= bay.ToS; s += 0.5d)
                if (!FlatStraightAt(line, s) || HalfWidthAt(s) != width)
                    return Fail($"bay {i}: the main road must be a flat straight of one width beside it (S {s:F1}).", out error);
            for (int g = 0; g < gaps.Count; g++)
                if (gaps[g].End > bay.FromS && gaps[g].Start < bay.ToS) return Fail($"bay {i}: it is beside a gap.", out error);
            foreach ((double from, double to, bool left) in BayGuardGaps())
                if (left == bay.Left && (from != bay.FromS || to != bay.ToS) && to > bay.FromS && from < bay.ToS)
                    return Fail($"bay {i}: it overlaps another bay on the same side.", out error);
            if (!bay.Left)
                foreach ((double from, double to) in ShortcutGuardGaps())
                    if (to > bay.FromS && from < bay.ToS) return Fail($"bay {i}: it overlaps a shortcut's crossing.", out error);
            error = null;
            return true;
        }

        // Independent decks are deliberately restricted to a flat straight between two flat quarter-turn ramps,
        // arriving from and leaving to its right. This keeps the clipping plane and open guard intervals exact.
        private bool TryValidateDeck(int i, out string error)
        {
            Bay bay = bays[i];
            if (!bay.Left || bay.Taper != 0f || !(bay.Extension > 0f) || float.IsInfinity(bay.ToS - bay.FromS + 2f * bay.Extension))
                return Fail($"bay {i}: a straight deck needs a left widening, zero taper and finite positive extension.", out error);
            double start = 0d;
            int straight = -1;
            for (int k = 0; k < segments.Count; k++)
            {
                double end = start + segments[k].Length;
                if (Math.Abs(start - bay.FromS) < 0.001d && Math.Abs(end - bay.ToS) < 0.001d) straight = k;
                start = end;
            }
            if (straight < 1 || straight >= segments.Count - 1 || segments[straight].IsArc || segments[straight].IsGap)
                return Fail($"bay {i}: a straight deck must span exactly one flat straight between ramps.", out error);
            Segment before = segments[straight - 1], after = segments[straight + 1];
            float main = HalfWidthAt(bay.FromS);
            if (!before.IsArc || before.Degrees != 90f || !after.IsArc || after.Degrees != 90f ||
                before.Radius <= main + RoadMeshBuilder.GuardThickness || after.Radius <= main + RoadMeshBuilder.GuardThickness ||
                (straight > 1 ? segments[straight - 2].EndGrade : segments[segments.Count - 1].EndGrade) != 0f || before.EndGrade != 0f ||
                segments[straight].EndGrade != 0f || after.EndGrade != 0f ||
                StartHalfWidth(straight - 1) != main || EndHalfWidth(straight - 1) != main ||
                EndHalfWidth(straight) != main || EndHalfWidth(straight + 1) != main)
                return Fail($"bay {i}: a straight deck needs flat quarter-turn ramps to its right, all of one width.", out error);
            Bay.DeckJunction(before.Radius, main, out _, out float join, RoadMeshBuilder.GuardThickness);
            Bay.DeckJunction(after.Radius, main, out _, out float leave, RoadMeshBuilder.GuardThickness);
            if (!(bay.Extension > Mathf.Max(join, leave) + RoadMeshBuilder.GuardThickness))
                return Fail($"bay {i}: the extension must put both end guards beyond the ramp crossings.", out error);
            for (int k = 0; k < bays.Count; k++)
                if (k != i && bays[k].ToS > bay.FromS - before.Length && bays[k].FromS < bay.ToS + after.Length)
                    return Fail($"bay {i}: another bay overlaps its ramps or straight.", out error);
            foreach ((double from, double to) in ShortcutGuardGaps())
                if (to > bay.FromS - before.Length && from < bay.ToS + after.Length)
                    return Fail($"bay {i}: a shortcut overlaps its ramps or straight.", out error);
            error = null;
            return true;
        }

        // A shortcut leaves and rejoins the main line where the road is a flat straight for the whole crossing, is
        // narrower than the main road and wider than a corner pole's reach allows, and its end meets the main line at ToS.
        private bool TryValidateShortcut(int i, Centerline line, double lap, out string error)
        {
            Shortcut shortcut = shortcuts[i];
            const float guard = RoadMeshBuilder.GuardThickness;
            double from = shortcut.FromS, to = shortcut.ToS;
            if (!(openCourse ? from >= 0d : from > guard) || !(to > from) || !(to < (openCourse ? lap : lap - guard)))
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
                if (openCourse && (from + step * n < 0 || from + step * n >= count)) break;
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
