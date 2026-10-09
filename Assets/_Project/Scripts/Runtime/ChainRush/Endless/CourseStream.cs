using System;
using System.Collections.Generic;
using ProtoHarness.ChainRush.Track;
using UnityEngine;

namespace ProtoHarness.ChainRush.Endless
{
    // Streams generated modules (CourseGenerator) onto a centerline around a moving S, and works out what the
    // scene needs from them without touching any scene object: road spans (each gap left out, cut into
    // pieces of at most MaxSpanLength, guards from the module's open edges), grapple anchor positions, and
    // lookups for the next gap and the rest the runner is on. Spans and anchors are keyed by their S, which
    // never changes for a given seed, so a pool can tell which ones it already built.
    public sealed class CourseStream
    {
        public const double MaxSpanLength = 50d;
        // Road laid before S 0 so the spawn rest also covers the run-in behind the spawn point.
        public const double LeadIn = 10d;

        private readonly Centerline line;
        private readonly CourseTuning tuning;
        private readonly double ahead;
        private readonly double behind;
        private readonly List<CourseModule> modules = new List<CourseModule>(32);
        private readonly List<double> spanFrom = new List<double>(64);
        private readonly List<double> spanTo = new List<double>(64);
        private readonly List<RoadProfile> spanProfile = new List<RoadProfile>(64);
        private readonly List<int> anchorModule = new List<int>(8);
        private CourseGenerator generator;

        public CourseStream(Centerline line, CourseTuning tuning, double ahead, double behind)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (tuning == null) throw new ArgumentNullException(nameof(tuning));
            if (!(ahead > 0d) || double.IsInfinity(ahead)) throw new ArgumentOutOfRangeException(nameof(ahead), ahead, "Ahead distance must be positive and finite.");
            if (!(behind >= LeadIn) || double.IsInfinity(behind))
                throw new ArgumentOutOfRangeException(nameof(behind), behind, $"Behind distance must be finite and at least the {LeadIn} m lead-in.");
            this.line = line;
            this.tuning = tuning;
            this.ahead = ahead;
            this.behind = behind;
        }

        public Centerline Line => line;
        public ulong Seed => generator != null ? generator.Seed : 0UL;
        public int ModuleCount => modules.Count;
        public int SpanCount => spanFrom.Count;
        public int AnchorCount => anchorModule.Count;
        // Changes whenever spans or anchors are added or dropped, so a pool only re-syncs then.
        public int Revision { get; private set; }

        public CourseModule Module(int index) => modules[index];
        public double SpanFrom(int index) => spanFrom[index];
        public double SpanTo(int index) => spanTo[index];
        public RoadProfile SpanProfile(int index) => spanProfile[index];

        // Clears the centerline back to its origin and starts the course for seed, generated up to ahead.
        public void Reset(ulong seed)
        {
            line.Clear();
            generator = new CourseGenerator(seed, tuning);
            modules.Clear();
            spanFrom.Clear();
            spanTo.Clear();
            spanProfile.Clear();
            anchorModule.Clear();
            Revision++;
            Advance(0d);
        }

        // Generates modules until the centerline reaches s + ahead, then drops modules, spans and centerline
        // pieces that ended more than behind before s.
        public void Advance(double s)
        {
            if (generator == null) throw new InvalidOperationException("CourseStream: call Reset with a seed before Advance.");
            if (double.IsNaN(s)) throw new ArgumentOutOfRangeException(nameof(s), s, "S cannot be NaN.");
            while (line.PieceCount == 0 || line.EndS < s + ahead)
            {
                CourseModule module = generator.Next();
                module.AppendTo(line);
                modules.Add(module);
                AddSpans(module);
                if (module.HasAnchor) anchorModule.Add(module.Index);
                Revision++;
            }
            double keep = s - behind;
            int dropped = 0;
            while (dropped < modules.Count - 1 && modules[dropped].EndS < keep) dropped++;
            if (dropped > 0)
            {
                int firstKept = modules[dropped].Index;
                modules.RemoveRange(0, dropped);
                while (anchorModule.Count > 0 && anchorModule[0] < firstKept) anchorModule.RemoveAt(0);
                Revision++;
            }
            int stale = 0;
            while (stale < spanFrom.Count && spanTo[stale] < keep) stale++;
            if (stale > 0)
            {
                spanFrom.RemoveRange(0, stale);
                spanTo.RemoveRange(0, stale);
                spanProfile.RemoveRange(0, stale);
                Revision++;
            }
            line.TrimBefore(keep);
        }

        // S of anchor index (its key) and its world position on the current centerline.
        public double AnchorS(int index)
        {
            CourseModule module = ModuleByIndex(anchorModule[index]);
            return module.StartS + module.AnchorAlong;
        }

        public Vector3 AnchorPosition(int index)
        {
            CourseModule module = ModuleByIndex(anchorModule[index]);
            TrackFrame frame = line.FrameAt(module.StartS + module.AnchorAlong);
            return frame.Position + frame.Right * module.AnchorSide + Vector3.up * module.AnchorHeight;
        }

        // The module under s. Before the first module (the lead-in) that is the first module; past the last, none.
        public bool TryModuleAt(double s, out CourseModule module)
        {
            for (int i = 0; i < modules.Count; i++)
                if (s < modules[i].EndS)
                {
                    module = modules[i];
                    return true;
                }
            module = default;
            return false;
        }

        // The first gap that ends after s, among generated modules.
        public bool TryNextGap(double s, out double start, out double length, out ModuleKind kind)
        {
            for (int i = 0; i < modules.Count; i++)
            {
                CourseModule module = modules[i];
                if (!module.HasGap) continue;
                double gapStart = module.StartS + module.GapStart;
                if (gapStart + module.GapLength <= s) continue;
                start = gapStart;
                length = module.GapLength;
                kind = module.Kind;
                return true;
            }
            start = 0d;
            length = 0d;
            kind = default;
            return false;
        }

        // Road left before the next gap starts: zero inside a gap, infinity when no gap is generated yet.
        public double DistanceToGap(double s)
        {
            if (!TryNextGap(s, out double start, out _, out _)) return double.PositiveInfinity;
            return Math.Max(0d, start - s);
        }

        // Rest road left ahead of s when s is on a rest module, otherwise zero.
        public double RestRemaining(double s)
        {
            if (!TryModuleAt(s, out CourseModule module) || module.Kind != ModuleKind.Rest) return 0d;
            return module.EndS - s;
        }

        private CourseModule ModuleByIndex(int index)
        {
            for (int i = 0; i < modules.Count; i++)
                if (modules[i].Index == index) return modules[i];
            throw new InvalidOperationException($"CourseStream: module {index} is no longer streamed.");
        }

        private void AddSpans(CourseModule module)
        {
            var profile = new RoadProfile(RoadProfile.DeckHalfWidth, RoadProfile.DeckThickness, !module.OpenLeft, !module.OpenRight);
            double start = module.Index == 0 ? module.StartS - LeadIn : module.StartS;
            if (module.HasGap)
            {
                double gapStart = module.StartS + module.GapStart;
                AddSegment(start, gapStart, profile);
                AddSegment(gapStart + module.GapLength, module.EndS, profile);
            }
            else
            {
                AddSegment(start, module.EndS, profile);
            }
        }

        // Cuts [from, to) into equal spans of at most MaxSpanLength.
        private void AddSegment(double from, double to, RoadProfile profile)
        {
            int count = Math.Max(1, (int)Math.Ceiling((to - from) / MaxSpanLength - 1e-9));
            for (int i = 0; i < count; i++)
            {
                spanFrom.Add(from + (to - from) * i / count);
                spanTo.Add(i == count - 1 ? to : from + (to - from) * (i + 1) / count);
                spanProfile.Add(profile);
            }
        }
    }
}
