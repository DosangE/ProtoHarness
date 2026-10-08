using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoHarness.ChainRush.Track
{
    // Builds a course as a deterministic sequence of modules from a seed (COURSE.md 5-1, 6-5, 7-1). Each
    // module is chosen from up to CandidatesPerModule candidates drawn with SeedHash(seed, index, attempt);
    // the first one that keeps the connection rules wins, so the same seed and tuning always give the same
    // course. Pure C#: it keeps its own double coordinates (course space: start at the origin heading +z,
    // height 0) and never touches a Centerline; callers attach modules with CourseModule.AppendTo.
    //
    // The hash is stateless but the rules look at recent modules (last kind, net turn, footprints), so a
    // module cannot be computed alone: Next() always continues from module 0.
    //
    // Rules (each module starts and ends flat, so grade always joins):
    //   R1 every gap module has a flat straight runway before and after the gap (built in)
    //   R2 no grapple gap right after a sharp curve
    //   R3 at most RestSpacing metres of non-rest modules in a row: a longer candidate becomes a rest
    //   R4 no more than MaxSameKindRun modules of one kind in a row (the escape rest is exempt)
    //   R5 net turn over the modules ending within the last TurnWindow metres stays within TurnLimit
    //   R6 height stays within HeightLimit of the start
    //   R7 no footprint overlap with modules of the last IntersectionWindow metres, except the previous one
    //   R8 gaps are built from straight pieces only (no gap inside a curve)
    //   R9 (not in COURSE.md) a candidate must leave room for the escape rest after it, so Next() cannot dead-end in one step
    public sealed class CourseGenerator
    {
        private const double DegToRad = Math.PI / 180d;
        private const double RadToDeg = 180d / Math.PI;
        private const int SaltStride = 32;

        // Salt slots inside one attempt.
        private const int SlotKind = 0;
        private const int SlotA = 1;
        private const int SlotB = 2;
        private const int SlotC = 3;
        private const int SlotSign = 8;
        private const int SlotOpenChance = 10;
        private const int SlotOpenSide = 11;
        private const int SlotAnchorOffset = 12;
        private const int SlotAnchorHeight = 13;

        private struct Pose
        {
            public double X;
            public double Z;
            public double Yaw;
            public double Height;
        }

        private sealed class Footprint
        {
            public double StartS;
            public double EndS;
            public double Turn;
            public double MinX;
            public double MaxX;
            public double MinZ;
            public double MaxZ;
            public double[] Xs;
            public double[] Zs;
        }

        private readonly ulong seed;
        private readonly CourseTuning tuning;
        private readonly List<Footprint> history = new List<Footprint>(64);
        private Pose pose;
        private int nextIndex;
        private double nextS;
        private double sinceRest;
        private ModuleKind lastKind;
        private int lastRun;
        private int openCount;

        public CourseGenerator(ulong seed, CourseTuning tuning)
        {
            if (tuning == null) throw new ArgumentNullException(nameof(tuning));
            if (!tuning.TryValidate(out string error)) throw new ArgumentException(error, nameof(tuning));
            this.seed = seed;
            this.tuning = tuning;
        }

        public ulong Seed => seed;
        public int ModuleCount => nextIndex;
        // The course position, heading and height where the next module starts.
        public double EndS => nextS;
        public double EndX => pose.X;
        public double EndZ => pose.Z;
        public double EndHeight => pose.Height;
        public double EndYawDegrees => pose.Yaw;
        // How many times every candidate broke a rule and the fallback rest was used.
        public int EscapeCount { get; private set; }

        public CourseModule Next()
        {
            int index = nextIndex;
            if (index == 0) return Commit(BuildKind(ModuleKind.Rest, 0, 0, 0d, false, tuning.SpawnRestLength));

            for (int attempt = 0; attempt < tuning.CandidatesPerModule; attempt++)
            {
                CourseModule candidate = BuildKind(PickKind(index, attempt), index, attempt, nextS, false, 0f);
                if (!IsRest(candidate) && sinceRest + candidate.Length > tuning.RestSpacing)
                    candidate = BuildKind(ModuleKind.Rest, index, attempt, nextS, false, 0f);
                Footprint footprint = Trace(candidate, pose, out Pose end, out double minHeight, out double maxHeight);
                if (!HeightOk(minHeight, maxHeight)) continue;
                if (candidate.Kind == ModuleKind.GrappleGap && lastKind == ModuleKind.SharpCurve) continue;
                if (candidate.Kind == lastKind && lastRun >= tuning.MaxSameKindRun) continue;
                if (!TurnOk(footprint)) continue;
                if (Intersects(footprint)) continue;
                if (!ExitViable(candidate, footprint, end)) continue;
                return Commit(candidate, footprint, end);
            }

            CourseModule escape = BuildKind(ModuleKind.Rest, index, 0, nextS, true, tuning.EscapeRestLength);
            Footprint escapeFootprint = Trace(escape, pose, out Pose escapeEnd, out double escapeMin, out double escapeMax);
            if (!HeightOk(escapeMin, escapeMax) || !TurnOk(escapeFootprint) || Intersects(escapeFootprint))
                throw new InvalidOperationException(
                    $"CourseGenerator: module {index} (seed {seed}, S {nextS:F1}) has no valid candidate and the escape rest also breaks a rule " +
                    $"(height {escapeMin:F1}..{escapeMax:F1}, net turn, or footprint overlap). Loosen CourseTuning or change the seed.");
            EscapeCount++;
            return Commit(escape, escapeFootprint, escapeEnd);
        }

        private bool IsRest(in CourseModule module) => module.IsFlatStraight && module.Length >= tuning.RestMinLength;

        private bool HeightOk(double minHeight, double maxHeight) => minHeight >= -tuning.HeightLimit && maxHeight <= tuning.HeightLimit;

        // R5: the modules ending within the window of the candidate's end, plus the candidate.
        private bool TurnOk(Footprint candidate)
        {
            double net = candidate.Turn;
            for (int i = 0; i < history.Count; i++)
            {
                if (history[i].EndS > candidate.EndS - tuning.TurnWindow) net += history[i].Turn;
            }
            return Math.Abs(net) <= tuning.TurnLimit;
        }

        // R7: a box of half-size FootprintMargin around every sample (samples at most FootprintSpacing apart,
        // so the boxes cover the whole path) must not overlap the boxes of earlier modules in the window.
        private bool Intersects(Footprint candidate)
        {
            double margin = tuning.FootprintMargin;
            double reach = 2d * margin;
            for (int i = 0; i < history.Count - 1; i++)
            {
                Footprint other = history[i];
                if (other.EndS < candidate.StartS - tuning.IntersectionWindow) continue;
                if (candidate.MinX - reach >= other.MaxX || candidate.MaxX + reach <= other.MinX) continue;
                if (candidate.MinZ - reach >= other.MaxZ || candidate.MaxZ + reach <= other.MinZ) continue;
                for (int a = 0; a < candidate.Xs.Length; a++)
                {
                    for (int b = 0; b < other.Xs.Length; b++)
                    {
                        if (Math.Abs(candidate.Xs[a] - other.Xs[b]) < reach && Math.Abs(candidate.Zs[a] - other.Zs[b]) < reach) return true;
                    }
                }
            }
            return false;
        }

        // R9 (added after seed 2 trapped itself at 7.6 km): a candidate must leave room for the escape rest
        // behind it. Then, at the next Next(), either a candidate passes or the escape does, so a single
        // dead end cannot throw. It does not look further ahead: a chain of escapes can still trap the course.
        private bool ExitViable(in CourseModule candidate, Footprint footprint, Pose end)
        {
            CourseModule exit = BuildKind(ModuleKind.Rest, candidate.Index + 1, 0, candidate.EndS, true, tuning.EscapeRestLength);
            Footprint exitFootprint = Trace(exit, end, out _, out double minHeight, out double maxHeight);
            history.Add(footprint);
            try
            {
                return HeightOk(minHeight, maxHeight) && TurnOk(exitFootprint) && !Intersects(exitFootprint);
            }
            finally
            {
                history.RemoveAt(history.Count - 1);
            }
        }

        private CourseModule Commit(CourseModule module)
        {
            Footprint footprint = Trace(module, pose, out Pose end, out _, out _);
            return Commit(module, footprint, end);
        }

        private CourseModule Commit(CourseModule module, Footprint footprint, Pose end)
        {
            history.Add(footprint);
            double keepFrom = module.EndS - Math.Max(tuning.IntersectionWindow, tuning.TurnWindow);
            int drop = 0;
            while (drop < history.Count - 1 && history[drop].EndS < keepFrom) drop++;
            if (drop > 0) history.RemoveRange(0, drop);

            pose = end;
            nextIndex++;
            nextS = module.EndS;
            sinceRest = IsRest(module) ? 0d : sinceRest + module.Length;
            lastRun = module.Kind == lastKind && module.Index > 0 ? lastRun + 1 : 1;
            lastKind = module.Kind;
            if (module.LeftOpen || module.RightOpen) openCount++;
            return module;
        }

        private ModuleKind PickKind(int index, int attempt)
        {
            int count = CourseTuning.KindCount;
            double total = 0d;
            for (int i = 0; i < count; i++) total += tuning.Weight((ModuleKind)i, nextS);
            double pick = SeedHash.Unit(seed, index, Salt(attempt, SlotKind)) * total;
            ModuleKind last = ModuleKind.Straight;
            for (int i = 0; i < count; i++)
            {
                double weight = tuning.Weight((ModuleKind)i, nextS);
                if (weight <= 0d) continue;
                last = (ModuleKind)i;
                if (pick < weight) return last;
                pick -= weight;
            }
            return last;
        }

        private static int Salt(int attempt, int slot) => attempt * SaltStride + slot;

        private float Draw(int index, int attempt, int slot, Vector2 range) =>
            (float)SeedHash.Range(seed, index, Salt(attempt, slot), range.x, range.y);

        // restLength > 0 overrides the drawn length of a rest (spawn rest, escape rest).
        private CourseModule BuildKind(ModuleKind kind, int index, int attempt, double startS, bool escape, float restLength)
        {
            var pieces = new CourseModule.Piece[CourseModule.MaxPieces];
            int count = 1;
            float sign = SeedHash.Unit(seed, index, Salt(attempt, SlotSign)) < 0.5d ? 1f : -1f;
            double gapStart = 0d;
            float gapLength = 0f;
            bool hasAnchor = false;
            double anchorS = 0d;
            float anchorOffset = 0f;
            float anchorHeight = 0f;

            switch (kind)
            {
                case ModuleKind.Straight:
                    pieces[0] = CourseModule.Piece.Straight(Draw(index, attempt, SlotA, tuning.StraightLength(startS)), 0f);
                    break;
                case ModuleKind.Rest:
                    float length = restLength > 0f ? restLength : Draw(index, attempt, SlotA, tuning.RestLength(startS));
                    pieces[0] = CourseModule.Piece.Straight(length, 0f);
                    break;
                case ModuleKind.GentleCurve:
                    pieces[0] = CourseModule.Piece.Arc(
                        Draw(index, attempt, SlotA, tuning.GentleRadius(startS)),
                        sign * Draw(index, attempt, SlotB, tuning.GentleAngle(startS)), 0f);
                    break;
                case ModuleKind.SharpCurve:
                    pieces[0] = CourseModule.Piece.Arc(
                        Draw(index, attempt, SlotA, tuning.SharpRadius(startS)),
                        sign * Draw(index, attempt, SlotB, tuning.SharpAngle(startS)), 0f);
                    break;
                case ModuleKind.SCurve:
                    float radius = Draw(index, attempt, SlotA, tuning.SCurveRadius(startS));
                    pieces[0] = CourseModule.Piece.Arc(radius, sign * Draw(index, attempt, SlotB, tuning.SCurveAngle(startS)), 0f);
                    pieces[1] = CourseModule.Piece.Arc(radius, -sign * Draw(index, attempt, SlotC, tuning.SCurveAngle(startS)), 0f);
                    count = 2;
                    break;
                case ModuleKind.Uphill:
                    Ramp(pieces, Draw(index, attempt, SlotA, tuning.UphillGrade(startS)), Draw(index, attempt, SlotB, tuning.UphillLength(startS)));
                    count = 3;
                    break;
                case ModuleKind.Downhill:
                    Ramp(pieces, -Draw(index, attempt, SlotA, tuning.DownhillGrade(startS)), Draw(index, attempt, SlotB, tuning.DownhillLength(startS)));
                    count = 3;
                    break;
                case ModuleKind.Hill:
                    float hillGrade = Draw(index, attempt, SlotA, tuning.HillGrade(startS));
                    float hillLength = Draw(index, attempt, SlotB, tuning.HillLength(startS));
                    pieces[0] = CourseModule.Piece.Straight(hillLength * 0.25f, hillGrade);
                    pieces[1] = CourseModule.Piece.Straight(hillLength * 0.5f, -hillGrade);
                    pieces[2] = CourseModule.Piece.Straight(hillLength * 0.25f, 0f);
                    count = 3;
                    break;
                case ModuleKind.JumpGap:
                case ModuleKind.GrappleGap:
                    bool grapple = kind == ModuleKind.GrappleGap;
                    float runway = tuning.GapRunway;
                    gapLength = Draw(index, attempt, SlotA, grapple ? tuning.GrappleGapLength(startS) : tuning.JumpGapLength(startS));
                    pieces[0] = CourseModule.Piece.Straight(runway, 0f);
                    pieces[1] = CourseModule.Piece.Straight(gapLength, 0f);
                    pieces[2] = CourseModule.Piece.Straight(runway, 0f);
                    count = 3;
                    gapStart = startS + runway;
                    if (grapple)
                    {
                        hasAnchor = true;
                        anchorS = gapStart + gapLength * 0.5d;
                        anchorOffset = Draw(index, attempt, SlotAnchorOffset, tuning.AnchorOffset(startS));
                        anchorHeight = Draw(index, attempt, SlotAnchorHeight, tuning.AnchorHeight(startS));
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown module kind.");
            }

            bool leftOpen = false;
            bool rightOpen = false;
            bool eligible = kind == ModuleKind.Straight || kind == ModuleKind.GentleCurve;
            if (eligible && startS >= tuning.OpenEdgeStartDistance
                && openCount + 1 <= (double)tuning.OpenEdgeMaxFraction * (index + 1)
                && SeedHash.Unit(seed, index, Salt(attempt, SlotOpenChance)) < tuning.OpenEdgeChance)
            {
                bool left = SeedHash.Unit(seed, index, Salt(attempt, SlotOpenSide)) < 0.5d;
                leftOpen = left;
                rightOpen = !left;
            }

            return new CourseModule(index, kind, escape, startS, new ReadOnlySpan<CourseModule.Piece>(pieces, 0, count),
                gapStart, gapLength, hasAnchor, anchorS, anchorOffset, anchorHeight, leftOpen, rightOpen);
        }

        // Grade 0 -> grade over a quarter of the length, held for half, back to 0 over the last quarter.
        private static void Ramp(CourseModule.Piece[] pieces, float grade, float length)
        {
            pieces[0] = CourseModule.Piece.Straight(length * 0.25f, grade);
            pieces[1] = CourseModule.Piece.Straight(length * 0.5f, grade);
            pieces[2] = CourseModule.Piece.Straight(length * 0.25f, 0f);
        }

        // Walks the module from `start`, collecting path samples at most FootprintSpacing apart, the end
        // pose and the lowest and highest road height.
        private Footprint Trace(in CourseModule module, Pose start, out Pose end, out double minHeight, out double maxHeight)
        {
            var xs = new List<double>(16) { start.X };
            var zs = new List<double>(16) { start.Z };
            Pose walk = start;
            double grade = 0d;
            minHeight = maxHeight = start.Height;
            double turn = 0d;
            for (int i = 0; i < module.PieceCount; i++)
            {
                CourseModule.Piece piece = module.GetPiece(i);
                int steps = Math.Max(1, (int)Math.Ceiling(piece.Length / (double)tuning.FootprintSpacing));
                for (int step = 1; step <= steps; step++)
                {
                    double t = step == steps ? piece.Length : piece.Length * step / steps;
                    Pose sample = Advance(walk, piece, grade, t);
                    xs.Add(sample.X);
                    zs.Add(sample.Z);
                }
                Pose next = Advance(walk, piece, grade, piece.Length);
                minHeight = Math.Min(minHeight, next.Height);
                maxHeight = Math.Max(maxHeight, next.Height);
                if (grade * piece.EndGrade < 0d)
                {
                    double crest = Advance(walk, piece, grade, piece.Length * grade / (grade - piece.EndGrade)).Height;
                    minHeight = Math.Min(minHeight, crest);
                    maxHeight = Math.Max(maxHeight, crest);
                }
                turn += piece.Degrees;
                walk = next;
                grade = piece.EndGrade;
            }
            end = walk;

            var footprint = new Footprint
            {
                StartS = module.StartS,
                EndS = module.EndS,
                Turn = turn,
                Xs = xs.ToArray(),
                Zs = zs.ToArray(),
                MinX = double.MaxValue,
                MaxX = double.MinValue,
                MinZ = double.MaxValue,
                MaxZ = double.MinValue,
            };
            for (int i = 0; i < footprint.Xs.Length; i++)
            {
                footprint.MinX = Math.Min(footprint.MinX, footprint.Xs[i]);
                footprint.MaxX = Math.Max(footprint.MaxX, footprint.Xs[i]);
                footprint.MinZ = Math.Min(footprint.MinZ, footprint.Zs[i]);
                footprint.MaxZ = Math.Max(footprint.MaxZ, footprint.Zs[i]);
            }
            return footprint;
        }

        // The pose t metres into a piece that starts at p with grade `startGrade`. Same geometry as Centerline.
        private static Pose Advance(Pose p, in CourseModule.Piece piece, double startGrade, double t)
        {
            Pose q = p;
            double yaw = p.Yaw * DegToRad;
            if (!piece.IsArc)
            {
                q.X += Math.Sin(yaw) * t;
                q.Z += Math.Cos(yaw) * t;
            }
            else
            {
                double curvature = Math.Sign(piece.Degrees) / (double)piece.Radius;
                double yawT = p.Yaw + curvature * t * RadToDeg;
                double yawTRad = yawT * DegToRad;
                double centerX = p.X + Math.Cos(yaw) / curvature;
                double centerZ = p.Z - Math.Sin(yaw) / curvature;
                q.X = centerX - Math.Cos(yawTRad) / curvature;
                q.Z = centerZ + Math.Sin(yawTRad) / curvature;
                q.Yaw = yawT;
            }
            q.Height = p.Height + startGrade * t + (piece.EndGrade - startGrade) * t * t / (2d * piece.Length);
            return q;
        }
    }
}
