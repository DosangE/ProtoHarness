using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoHarness.ChainRush.Track
{
    // Builds the endless course one module at a time from a seed. Candidate a of module k is drawn from
    // SeedHash.Hash(seed, k, a); the first candidate that passes the connection rules (docs/COURSE.md 6-6,
    // R1-R8) is used, so the same seed and tuning always give the same modules. The rules look at recent
    // history, so module k cannot be computed on its own: replay from module 0.
    // The generator tracks its own pose in doubles, starting at the origin facing +Z on flat ground with the
    // same axes as Centerline (yaw clockwise from +Z, right = +X at yaw 0). Module 0 is the spawn rest.
    // A candidate is also rejected unless a straight of lookaheadLength after it keeps clear of the course,
    // so the course does not coil into its own past and escape rests have room. If every candidate is rejected that escape rest is placed. It is exempt
    // from R4 and R5: it adds no turn, but the R5 window sliding past older opposite turns can still push the
    // net turn over the limit. If it overlaps anyway the generator throws, since the course cannot continue.
    public sealed class CourseGenerator
    {
        private struct Placed
        {
            public ModuleKind Kind;
            public double EndS;
            public float Turn;
            public Bounds2 Box;
            // Centerline samples as x, z pairs, for rule R7.
            public double[] Samples;
        }

        private struct Bounds2
        {
            public double MinX;
            public double MaxX;
            public double MinZ;
            public double MaxZ;

            public bool Near(Bounds2 other, double distance) =>
                MinX - distance <= other.MaxX && other.MinX <= MaxX + distance
                && MinZ - distance <= other.MaxZ && other.MinZ <= MaxZ + distance;
        }

        private struct Trace
        {
            public double EndX;
            public double EndZ;
            public double EndYaw;
            public double EndHeight;
            public double MinHeight;
            public double MaxHeight;
            public Bounds2 Box;
        }

        private readonly ulong seed;
        private readonly CourseTuning tuning;
        private readonly List<Placed> recent = new List<Placed>(64);
        private readonly List<double> samples = new List<double>(128);
        private readonly List<double> escapeSamples = new List<double>(32);
        private double endX;
        private double endZ;
        private double endYaw;
        private double endHeight;
        private double endS;
        private double lastRestEndS;
        private int count;
        private int escapeCount;
        private int openEdgeCount;

        public CourseGenerator(ulong seed, CourseTuning tuning)
        {
            if (tuning == null) throw new ArgumentNullException(nameof(tuning));
            string problem = tuning.FindProblem();
            if (problem != null) throw new ArgumentException($"CourseTuning '{tuning.name}' is invalid: {problem}", nameof(tuning));
            this.seed = seed;
            this.tuning = tuning;
        }

        public ulong Seed => seed;
        public int Count => count;
        public double EndS => endS;
        public double EndX => endX;
        public double EndZ => endZ;
        public double EndHeight => endHeight;

        // Accumulated yaw in degrees, not wrapped.
        public double EndYaw => endYaw;

        // Modules that fell back to the escape rest because every candidate broke a rule.
        public int EscapeCount => escapeCount;
        public int OpenEdgeCount => openEdgeCount;

        public CourseModule Next()
        {
            int index = count;
            CourseModule module;
            Trace trace;
            if (index == 0)
            {
                module = RestModule(index, tuning.SpawnRestLength);
                trace = TraceOf(module);
            }
            else if (!TryCandidates(index, out module, out trace))
            {
                module = RestModule(index, tuning.EscapeRestLength).AsEscape();
                trace = TraceOf(module);
                if (TooClose(samples, trace.Box, endS - tuning.OverlapWindow, recent.Count - 1))
                    throw new InvalidOperationException(
                        $"Seed {seed}: module {index} at S {endS:F1} has no candidate and the escape rest overlaps an earlier module.");
                escapeCount++;
            }
            Commit(module, trace);
            return module;
        }

        private bool TryCandidates(int index, out CourseModule module, out Trace trace)
        {
            for (int attempt = 0; attempt < tuning.CandidateAttempts; attempt++)
            {
                ulong stream = SeedHash.Hash(seed, (ulong)index, (ulong)attempt);
                module = Draw(index, ref stream);
                // R3: a rest must start within restSpacing of the previous rest's end.
                if (endS - lastRestEndS + module.Length > tuning.RestSpacing)
                    module = RestModule(index, Value(ref stream, tuning.RestLength(endS)));
                trace = TraceOf(module);
                if (Allowed(module, trace)) return true;
            }
            module = default;
            trace = default;
            return false;
        }

        private CourseModule Draw(int index, ref ulong stream)
        {
            double s = endS;
            ModuleKind kind = PickKind(SeedHash.Unit(SeedHash.Next(ref stream)), s);
            CourseModule module = CourseModule.Create(index, kind, s);
            float approach = tuning.GapApproachLength;
            switch (kind)
            {
                case ModuleKind.Straight:
                    module = module.WithStraight(Value(ref stream, tuning.StraightLength(s)), 0f);
                    break;
                case ModuleKind.GentleCurve:
                    module = Arc(module, ref stream, tuning.GentleRadius(s), tuning.GentleDegrees(s));
                    break;
                case ModuleKind.SharpCurve:
                    module = Arc(module, ref stream, tuning.SharpRadius(s), tuning.SharpDegrees(s));
                    break;
                case ModuleKind.SCurve:
                {
                    float radius = Value(ref stream, tuning.SCurveRadius(s));
                    float first = Value(ref stream, tuning.SCurveDegrees(s));
                    float second = Value(ref stream, tuning.SCurveDegrees(s));
                    float sign = Sign(ref stream);
                    module = module.WithArc(radius, sign * first, 0f).WithArc(radius, -sign * second, 0f);
                    break;
                }
                case ModuleKind.Uphill:
                case ModuleKind.Downhill:
                {
                    float grade = Value(ref stream, kind == ModuleKind.Uphill ? tuning.UphillGrade(s) : tuning.DownhillGrade(s));
                    if (kind == ModuleKind.Downhill) grade = -grade;
                    float hold = Value(ref stream, tuning.SlopeLength(s));
                    module = module.WithStraight(tuning.SlopeRampLength, grade).WithStraight(hold, grade)
                        .WithStraight(tuning.SlopeRampLength, 0f);
                    break;
                }
                case ModuleKind.Hill:
                {
                    float grade = Value(ref stream, tuning.HillGrade(s));
                    float piece = Value(ref stream, tuning.HillPieceLength(s));
                    module = module.WithStraight(piece, grade).WithStraight(piece, -grade).WithStraight(piece, 0f);
                    break;
                }
                case ModuleKind.JumpGap:
                {
                    float gap = Value(ref stream, tuning.JumpGap(s));
                    module = module.WithStraight(approach, 0f).WithStraight(gap, 0f).WithStraight(approach, 0f)
                        .WithGap(approach, gap);
                    break;
                }
                case ModuleKind.GrappleGap:
                {
                    float gap = Value(ref stream, tuning.GrappleGap(s));
                    float side = Value(ref stream, tuning.GrappleAnchorSide(s));
                    module = module.WithStraight(approach, 0f).WithStraight(gap, 0f).WithStraight(approach, 0f)
                        .WithGap(approach, gap).WithAnchor(approach + gap * 0.5f, tuning.GrappleAnchorHeight, side);
                    break;
                }
                default:
                    throw new InvalidOperationException($"Kind {kind} cannot be drawn.");
            }
            return WithOpenEdge(module, ref stream);
        }

        // Past openEdgeStart a straight may drop either guard and a gentle curve its inside guard, while
        // open-edge modules stay within openEdgeMaxShare of all modules.
        private CourseModule WithOpenEdge(CourseModule module, ref ulong stream)
        {
            if (module.Kind != ModuleKind.Straight && module.Kind != ModuleKind.GentleCurve) return module;
            if (module.StartS < tuning.OpenEdgeStart) return module;
            if (openEdgeCount + 1 > tuning.OpenEdgeMaxShare * (module.Index + 1)) return module;
            if (SeedHash.Unit(SeedHash.Next(ref stream)) >= tuning.OpenEdgeChance) return module;
            bool right = module.Kind == ModuleKind.GentleCurve ? module.TurnDegrees > 0f : Sign(ref stream) > 0f;
            return module.WithOpenEdges(!right, right);
        }

        private ModuleKind PickKind(double unit, double s)
        {
            float total = 0f;
            for (var kind = ModuleKind.Straight; kind <= ModuleKind.GrappleGap; kind++) total += tuning.Weight(kind, s);
            double pick = unit * total;
            ModuleKind last = ModuleKind.Straight;
            for (var kind = ModuleKind.Straight; kind <= ModuleKind.GrappleGap; kind++)
            {
                float weight = tuning.Weight(kind, s);
                if (weight <= 0f) continue;
                last = kind;
                if (pick < weight) return kind;
                pick -= weight;
            }
            return last;
        }

        private bool Allowed(CourseModule module, Trace trace)
        {
            int previous = recent.Count - 1;
            // R2: no grapple gap straight after a sharp curve.
            if (module.Kind == ModuleKind.GrappleGap && recent[previous].Kind == ModuleKind.SharpCurve) return false;
            // R4: no kind three times in a row.
            if (previous >= 1 && recent[previous].Kind == module.Kind && recent[previous - 1].Kind == module.Kind) return false;
            // R5: net turn over the window ending at this module's end.
            double windowStart = module.EndS - tuning.TurnWindow;
            double turn = module.TurnDegrees;
            for (int i = 0; i < recent.Count; i++)
                if (recent[i].EndS > windowStart) turn += recent[i].Turn;
            if (Math.Abs(turn) > tuning.MaxNetTurn) return false;
            // R6: height stays near the start.
            if (trace.MinHeight < -tuning.MaxHeightOffset || trace.MaxHeight > tuning.MaxHeightOffset) return false;
            // R7: keeps clear of recent modules before the previous one.
            if (TooClose(samples, trace.Box, endS - tuning.OverlapWindow, recent.Count - 1)) return false;
            return LeavesEscape(module, trace);
        }

        // Lookahead: a straight continuing from this module must keep clear (R7) of every recent module, the
        // current last one included, since this module will be the straight's previous one.
        private bool LeavesEscape(CourseModule module, Trace trace)
        {
            double length = tuning.LookaheadLength;
            int steps = Math.Max(1, (int)Math.Ceiling(length / tuning.OverlapSampleStep));
            double a = trace.EndYaw * Math.PI / 180d;
            double fx = Math.Sin(a), fz = Math.Cos(a);
            var box = new Bounds2 { MinX = trace.EndX, MaxX = trace.EndX, MinZ = trace.EndZ, MaxZ = trace.EndZ };
            escapeSamples.Clear();
            for (int i = 0; i <= steps; i++)
            {
                double px = trace.EndX + fx * length * i / steps;
                double pz = trace.EndZ + fz * length * i / steps;
                escapeSamples.Add(px);
                escapeSamples.Add(pz);
                Grow(ref box, px, pz);
            }
            return !TooClose(escapeSamples, box, module.EndS - tuning.OverlapWindow, recent.Count);
        }

        // R7: some of points (x, z pairs within box) comes closer than twice overlapMargin to a centerline sample
        // of one of the first count recent modules that ends at or after windowStart.
        private bool TooClose(List<double> points, Bounds2 box, double windowStart, int count)
        {
            double clearance = 2d * tuning.OverlapMargin;
            double limit = clearance * clearance;
            for (int i = 0; i < count; i++)
            {
                Placed placed = recent[i];
                if (placed.EndS < windowStart || !placed.Box.Near(box, clearance)) continue;
                double[] other = placed.Samples;
                for (int p = 0; p < points.Count; p += 2)
                for (int q = 0; q < other.Length; q += 2)
                {
                    double dx = points[p] - other[q];
                    double dz = points[p + 1] - other[q + 1];
                    if (dx * dx + dz * dz < limit) return true;
                }
            }
            return false;
        }

        private void Commit(CourseModule module, Trace trace)
        {
            recent.Add(new Placed
            {
                Kind = module.Kind, EndS = module.EndS, Turn = module.TurnDegrees, Box = trace.Box, Samples = samples.ToArray(),
            });
            endX = trace.EndX;
            endZ = trace.EndZ;
            endYaw = trace.EndYaw;
            endHeight = trace.EndHeight;
            endS = module.EndS;
            if (module.Kind == ModuleKind.Rest) lastRestEndS = endS;
            if (module.OpenLeft || module.OpenRight) openEdgeCount++;
            count++;
            double keep = endS - Math.Max(tuning.OverlapWindow, tuning.TurnWindow);
            int stale = 0;
            while (stale < recent.Count - 2 && recent[stale].EndS < keep) stale++;
            if (stale > 0) recent.RemoveRange(0, stale);
        }

        // Walks the module from the current end pose: end pose, height extremes, and centerline samples (the
        // start, then evenly spaced at most overlapSampleStep apart on each piece, ending on each piece end)
        // into the samples buffer with their horizontal bounds.
        private Trace TraceOf(CourseModule module)
        {
            double x = endX, z = endZ, yaw = endYaw, height = endHeight, grade = 0d;
            samples.Clear();
            samples.Add(x);
            samples.Add(z);
            var trace = new Trace
            {
                MinHeight = height,
                MaxHeight = height,
                Box = new Bounds2 { MinX = x, MaxX = x, MinZ = z, MaxZ = z },
            };
            for (int p = 0; p < module.PieceCount; p++)
            {
                double length = module.PieceLength(p);
                double degrees = module.PieceDegrees(p);
                double radius = module.PieceRadius(p);
                double endGrade = module.PieceEndGrade(p);
                int steps = Math.Max(1, (int)Math.Ceiling(length / tuning.OverlapSampleStep));
                double centerX = 0d, centerZ = 0d, sign = Math.Sign(degrees);
                if (radius > 0d)
                {
                    double a = yaw * Math.PI / 180d;
                    double rightX = Math.Cos(a), rightZ = -Math.Sin(a);
                    centerX = x + rightX * radius * sign;
                    centerZ = z + rightZ * radius * sign;
                }
                double startX = x, startZ = z, startYaw = yaw;
                for (int i = 1; i <= steps; i++)
                {
                    double t = (double)i / steps;
                    double px, pz;
                    if (radius > 0d)
                    {
                        double a = (startYaw + degrees * t) * Math.PI / 180d;
                        px = centerX - Math.Cos(a) * radius * sign;
                        pz = centerZ + Math.Sin(a) * radius * sign;
                    }
                    else
                    {
                        double a = startYaw * Math.PI / 180d;
                        px = startX + Math.Sin(a) * length * t;
                        pz = startZ + Math.Cos(a) * length * t;
                    }
                    Grow(ref trace.Box, px, pz);
                    samples.Add(px);
                    samples.Add(pz);
                    if (i == steps)
                    {
                        x = px;
                        z = pz;
                    }
                }
                yaw = startYaw + degrees;
                // Height is a parabola: grade eases linearly from grade to endGrade over the piece.
                double startHeight = height;
                height = startHeight + (grade + endGrade) * 0.5d * length;
                trace.MinHeight = Math.Min(trace.MinHeight, height);
                trace.MaxHeight = Math.Max(trace.MaxHeight, height);
                if (grade * endGrade < 0d)
                {
                    double along = grade / (grade - endGrade) * length;
                    double vertex = startHeight + grade * along + (endGrade - grade) * along * along / (2d * length);
                    trace.MinHeight = Math.Min(trace.MinHeight, vertex);
                    trace.MaxHeight = Math.Max(trace.MaxHeight, vertex);
                }
                grade = endGrade;
            }
            trace.EndX = x;
            trace.EndZ = z;
            trace.EndYaw = yaw;
            trace.EndHeight = height;
            return trace;
        }

        private static void Grow(ref Bounds2 box, double x, double z)
        {
            box.MinX = Math.Min(box.MinX, x);
            box.MaxX = Math.Max(box.MaxX, x);
            box.MinZ = Math.Min(box.MinZ, z);
            box.MaxZ = Math.Max(box.MaxZ, z);
        }

        private static CourseModule Arc(CourseModule module, ref ulong stream, Vector2 radiusRange, Vector2 degreesRange)
        {
            float radius = Value(ref stream, radiusRange);
            float degrees = Value(ref stream, degreesRange);
            return module.WithArc(radius, Sign(ref stream) * degrees, 0f);
        }

        private CourseModule RestModule(int index, float length) =>
            CourseModule.Create(index, ModuleKind.Rest, endS).WithStraight(length, 0f);

        private static float Value(ref ulong stream, Vector2 range) =>
            (float)SeedHash.Range(SeedHash.Next(ref stream), range.x, range.y);

        private static float Sign(ref ulong stream) => (SeedHash.Next(ref stream) >> 63) == 0UL ? 1f : -1f;
    }
}
