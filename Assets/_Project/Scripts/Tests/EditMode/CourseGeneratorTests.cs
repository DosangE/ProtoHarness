using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using ProtoHarness.ChainRush.Track;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ProtoHarness.Tests.EditMode
{
    // The rule checks below do not use the generator's own bookkeeping: they rebuild a Centerline from the
    // modules and measure it (heights, distances between road sections), or read the module data directly.
    public sealed class CourseGeneratorTests
    {
        private const int SeedCount = 20;
        private const double TenKilometres = 10000d;
        private const double RampDistance = 1400d;
        private const float Eps = 1e-3f;
        // Two roads whose centerlines are closer than this touch: 2 x (half width 6 + guard 0.3).
        private const float RoadClearance = 12.6f;
        private const double SampleSpacing = 2.5d;

        private CourseTuning tuning;

        [SetUp]
        public void SetUp() => tuning = ScriptableObject.CreateInstance<CourseTuning>();

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(tuning);

        private List<CourseModule> Generate(ulong seed, double length, out CourseGenerator generator)
        {
            generator = new CourseGenerator(seed, tuning);
            var modules = new List<CourseModule>();
            while (generator.EndS < length) modules.Add(generator.Next());
            return modules;
        }

        private List<CourseModule> Generate(ulong seed, double length) => Generate(seed, length, out _);

        private static Centerline Build(List<CourseModule> modules)
        {
            var line = new Centerline(Vector3.zero, 0f);
            foreach (CourseModule module in modules) module.AppendTo(line);
            return line;
        }

        private static bool IsRest(in CourseModule module, float restMin) => module.IsFlatStraight && module.Length >= restMin;

        private static string Describe(in CourseModule m) => $"#{m.Index} {m.Kind} S {m.StartS:F1}..{m.EndS:F1}";

        private static string Fingerprint(List<CourseModule> modules)
        {
            var text = new StringBuilder();
            foreach (CourseModule m in modules)
            {
                text.Append((int)m.Kind).Append(':').Append(m.Length.ToString("R")).Append(':').Append(m.TotalTurnDegrees.ToString("R")).Append(';');
            }
            return text.ToString();
        }

        private static void AssertSame(CourseModule a, CourseModule b)
        {
            Assert.That(b.Index, Is.EqualTo(a.Index));
            Assert.That(b.Kind, Is.EqualTo(a.Kind));
            Assert.That(b.IsEscape, Is.EqualTo(a.IsEscape));
            Assert.That(b.StartS, Is.EqualTo(a.StartS));
            Assert.That(b.Length, Is.EqualTo(a.Length));
            Assert.That(b.PieceCount, Is.EqualTo(a.PieceCount));
            for (int i = 0; i < a.PieceCount; i++)
            {
                Assert.That(b.GetPiece(i).Length, Is.EqualTo(a.GetPiece(i).Length));
                Assert.That(b.GetPiece(i).Radius, Is.EqualTo(a.GetPiece(i).Radius));
                Assert.That(b.GetPiece(i).Degrees, Is.EqualTo(a.GetPiece(i).Degrees));
                Assert.That(b.GetPiece(i).EndGrade, Is.EqualTo(a.GetPiece(i).EndGrade));
            }
            Assert.That(b.GapStartS, Is.EqualTo(a.GapStartS));
            Assert.That(b.GapLength, Is.EqualTo(a.GapLength));
            Assert.That(b.HasAnchor, Is.EqualTo(a.HasAnchor));
            Assert.That(b.AnchorS, Is.EqualTo(a.AnchorS));
            Assert.That(b.AnchorOffset, Is.EqualTo(a.AnchorOffset));
            Assert.That(b.AnchorHeight, Is.EqualTo(a.AnchorHeight));
            Assert.That(b.LeftOpen, Is.EqualTo(a.LeftOpen));
            Assert.That(b.RightOpen, Is.EqualTo(a.RightOpen));
        }

        // ---- determinism -------------------------------------------------------------------------------

        [Test]
        public void Next_SameSeed_GivesBitIdenticalModuleSequence()
        {
            List<CourseModule> first = Generate(7UL, RampDistance);
            List<CourseModule> second = Generate(7UL, RampDistance);
            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++) AssertSame(first[i], second[i]);
        }

        [Test]
        public void Next_TwoGeneratorsInterleaved_StayIdentical()
        {
            var a = new CourseGenerator(11UL, tuning);
            var b = new CourseGenerator(11UL, tuning);
            var other = new CourseGenerator(12UL, tuning);
            for (int i = 0; i < 60; i++)
            {
                other.Next();
                CourseModule fromA = a.Next();
                other.Next();
                AssertSame(fromA, b.Next());
            }
        }

        [Test]
        public void Next_TwentySeeds_GiveTwentyDifferentCourses()
        {
            var seen = new HashSet<string>();
            for (ulong seed = 0; seed < SeedCount; seed++) seen.Add(Fingerprint(Generate(seed, RampDistance)));
            Assert.That(seen.Count, Is.EqualTo(SeedCount));
        }

        [Test]
        public void Next_TwentySeedsFirstFourteenHundredMetres_ProduceEveryModuleKind()
        {
            var seen = new HashSet<ModuleKind>();
            for (ulong seed = 0; seed < SeedCount; seed++)
            {
                foreach (CourseModule module in Generate(seed, RampDistance)) seen.Add(module.Kind);
            }
            foreach (ModuleKind kind in Enum.GetValues(typeof(ModuleKind)))
                Assert.That(seen, Does.Contain(kind), $"{kind} never appeared in 20 seeds x 1400 m");
        }

        // ---- structure ---------------------------------------------------------------------------------

        [Test]
        public void Next_FromSpawn_StartsWithSixtyMetreRestAndChainsContinuously()
        {
            List<CourseModule> modules = Generate(3UL, 2000d);
            Assert.That(modules[0].Kind, Is.EqualTo(ModuleKind.Rest));
            Assert.That(modules[0].Length, Is.EqualTo(60d));
            Assert.That(modules[0].StartS, Is.EqualTo(0d));
            for (int i = 1; i < modules.Count; i++)
            {
                Assert.That(modules[i].Index, Is.EqualTo(i));
                Assert.That(modules[i].StartS, Is.EqualTo(modules[i - 1].EndS), Describe(modules[i]));
            }
        }

        [Test]
        public void AppendTo_TenKilometres_EndsWhereTheGeneratorSaysItDoes()
        {
            for (ulong seed = 0; seed < SeedCount; seed++)
            {
                List<CourseModule> modules = Generate(seed, TenKilometres, out CourseGenerator generator);
                Centerline line = Build(modules);
                Assert.That(line.EndS, Is.EqualTo(generator.EndS).Within(1e-6), $"seed {seed} S");
                TrackFrame end = line.FrameAt(line.EndS);
                Assert.That(end.Position.x, Is.EqualTo(generator.EndX).Within(0.5), $"seed {seed} x");
                Assert.That(end.Position.z, Is.EqualTo(generator.EndZ).Within(0.5), $"seed {seed} z");
                Assert.That(end.Position.y, Is.EqualTo(generator.EndHeight).Within(0.05), $"seed {seed} height");
                double yaw = generator.EndYawDegrees * Math.PI / 180d;
                Assert.That(end.Forward.x * Math.Sin(yaw) + end.Forward.z * Math.Cos(yaw), Is.GreaterThan(Math.Cos(0.1 * Math.PI / 180d)), $"seed {seed} heading");
            }
        }

        // ---- parameter ranges --------------------------------------------------------------------------

        [Test]
        public void Modules_TwentySeedsTenKilometres_StayInsideTheDocumentedParameterRanges()
        {
            var problems = new List<string>();
            for (ulong seed = 0; seed < SeedCount; seed++)
            {
                foreach (CourseModule m in Generate(seed, TenKilometres))
                {
                    string where = $"seed {seed} {Describe(m)}";
                    for (int i = 0; i < m.PieceCount; i++)
                    {
                        CourseModule.Piece p = m.GetPiece(i);
                        if (p.IsArc && p.Radius < 30f - Eps) problems.Add($"{where} radius {p.Radius} below 30 m");
                        if (Mathf.Abs(p.EndGrade) > 0.12f + Eps) problems.Add($"{where} grade {p.EndGrade} beyond 12%");
                    }
                    if (m.GetPiece(m.PieceCount - 1).EndGrade != 0f) problems.Add($"{where} does not end flat");
                    CourseModule.Piece first = m.GetPiece(0);
                    switch (m.Kind)
                    {
                        case ModuleKind.Straight:
                            Check(problems, where, m.PieceCount == 1 && !first.IsArc && first.EndGrade == 0f, "straight shape");
                            Check(problems, where, InRange(first.Length, 30f, 80f), $"straight length {first.Length}");
                            break;
                        case ModuleKind.Rest:
                            Check(problems, where, m.IsFlatStraight, "rest shape");
                            Check(problems, where, m.Index == 0 ? first.Length == 60f : InRange(first.Length, 40f, 60f), $"rest length {first.Length}");
                            break;
                        case ModuleKind.GentleCurve:
                            Check(problems, where, m.PieceCount == 1 && first.IsArc, "gentle curve shape");
                            Check(problems, where, InRange(first.Radius, 60f, 120f) && InRange(Mathf.Abs(first.Degrees), 30f, 90f), $"gentle R {first.Radius} / {first.Degrees} deg");
                            break;
                        case ModuleKind.SharpCurve:
                            Check(problems, where, m.PieceCount == 1 && first.IsArc, "sharp curve shape");
                            Check(problems, where, InRange(first.Radius, 30f, 60f) && InRange(Mathf.Abs(first.Degrees), 60f, 120f), $"sharp R {first.Radius} / {first.Degrees} deg");
                            Check(problems, where, InRange(first.Radius, tuning.SharpRadius(m.StartS).x, tuning.SharpRadius(m.StartS).y), $"sharp R {first.Radius} outside the blend at S {m.StartS:F0}");
                            break;
                        case ModuleKind.SCurve:
                            Check(problems, where, m.PieceCount == 2 && m.GetPiece(1).IsArc && first.IsArc, "S-curve shape");
                            if (m.PieceCount == 2)
                            {
                                CourseModule.Piece second = m.GetPiece(1);
                                Check(problems, where, first.Radius == second.Radius && InRange(first.Radius, 50f, 80f), $"S R {first.Radius}/{second.Radius}");
                                Check(problems, where, Mathf.Sign(first.Degrees) != Mathf.Sign(second.Degrees), "S-curve arcs must turn opposite ways");
                                Check(problems, where, InRange(Mathf.Abs(first.Degrees), 30f, 60f) && InRange(Mathf.Abs(second.Degrees), 30f, 60f), $"S angles {first.Degrees}/{second.Degrees}");
                            }
                            break;
                        case ModuleKind.Uphill:
                            Check(problems, where, m.PieceCount == 3 && InRange(first.EndGrade, 0.04f, 0.10f), $"uphill grade {first.EndGrade}");
                            Check(problems, where, InRange((float)m.Length, 40f, 80f), $"uphill length {m.Length}");
                            break;
                        case ModuleKind.Downhill:
                            Check(problems, where, m.PieceCount == 3 && InRange(-first.EndGrade, 0.04f, 0.12f), $"downhill grade {first.EndGrade}");
                            Check(problems, where, InRange((float)m.Length, 40f, 80f), $"downhill length {m.Length}");
                            break;
                        case ModuleKind.Hill:
                            Check(problems, where, m.PieceCount == 3 && InRange(first.EndGrade, 0.10f, 0.12f) && m.GetPiece(1).EndGrade == -first.EndGrade, "hill grades");
                            Check(problems, where, InRange((float)m.Length, 120f, 160f), $"hill length {m.Length}");
                            break;
                        case ModuleKind.JumpGap:
                            Check(problems, where, m.HasGap && !m.HasAnchor && InRange(m.GapLength, 5f, 7.7f), $"jump gap {m.GapLength}");
                            break;
                        case ModuleKind.GrappleGap:
                            Check(problems, where, m.HasGap && m.HasAnchor && InRange(m.GapLength, 14f, 18f), $"grapple gap {m.GapLength}");
                            Check(problems, where, InRange(m.AnchorOffset, -2f, 2f) && InRange(m.AnchorHeight, 10f, 10f), $"anchor {m.AnchorOffset} / {m.AnchorHeight}");
                            Check(problems, where, m.AnchorS > m.GapStartS && m.AnchorS < m.GapStartS + m.GapLength, "anchor must be over the gap");
                            break;
                    }
                }
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void Tuning_Defaults_BlendFromStartToFullOverFourteenHundredMetres()
        {
            Assert.That(tuning.GrappleGapLength(0d), Is.EqualTo(new Vector2(16f, 16f)), "scene's passing gap at the start");
            Assert.That(tuning.AnchorOffset(0d), Is.EqualTo(Vector2.zero));
            Assert.That(tuning.SharpRadius(0d), Is.EqualTo(new Vector2(50f, 60f)));
            Assert.That(tuning.SharpRadius(RampDistance), Is.EqualTo(new Vector2(30f, 50f)));
            Assert.That(tuning.SharpRadius(RampDistance * 5d), Is.EqualTo(new Vector2(30f, 50f)), "clamped past the ramp");
            Assert.That(tuning.SharpRadius(RampDistance / 2d).x, Is.EqualTo(40f).Within(1e-4f));
            Assert.That(tuning.Weight(ModuleKind.SharpCurve, 0d), Is.EqualTo(0f));
            Assert.That(tuning.Weight(ModuleKind.SharpCurve, RampDistance), Is.EqualTo(2f));
            Assert.That(tuning.Weight(ModuleKind.Rest, RampDistance / 2d), Is.EqualTo(0f));
        }

        private static bool InRange(float value, float min, float max) => value >= min - Eps && value <= max + Eps;

        private static void Check(List<string> problems, string where, bool ok, string what)
        {
            if (!ok) problems.Add($"{where}: {what}");
        }

        // ---- connection rules --------------------------------------------------------------------------

        [Test]
        public void Rules_TwentySeedsTenKilometres_AllHold()
        {
            var problems = new List<string>();
            long moduleTotal = 0;
            long escapes = 0;
            for (ulong seed = 0; seed < SeedCount; seed++)
            {
                List<CourseModule> modules = Generate(seed, TenKilometres, out CourseGenerator generator);
                moduleTotal += modules.Count;
                escapes += generator.EscapeCount;
                int escapeFlags = 0;
                foreach (CourseModule m in modules) if (m.IsEscape) escapeFlags++;
                if (escapeFlags != generator.EscapeCount) problems.Add($"seed {seed}: {escapeFlags} escape modules but EscapeCount {generator.EscapeCount}");

                CheckGaps(problems, seed, modules);
                CheckSharpThenGrapple(problems, seed, modules);
                CheckRestSpacing(problems, seed, modules);
                CheckSameKindRuns(problems, seed, modules);
                CheckNetTurn(problems, seed, modules);
                CheckOpenEdges(problems, seed, modules);
                Centerline line = Build(modules);
                CheckHeight(problems, seed, line);
                CheckSeparation(problems, seed, modules, line);
            }
            Debug.Log($"CourseGeneratorTests: 20 seeds x 10 km = {moduleTotal} modules, {escapes} escape rests ({100d * escapes / moduleTotal:F2}%).");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(escapes, Is.LessThanOrEqualTo(moduleTotal / 50), "escape rests should stay under 2% of modules");
        }

        // R1 + R8: gaps are a straight runway of at least 15 m, the gap, a runway of at least 15 m.
        private static void CheckGaps(List<string> problems, ulong seed, List<CourseModule> modules)
        {
            foreach (CourseModule m in modules)
            {
                if (!m.HasGap) continue;
                string where = $"seed {seed} {Describe(m)}";
                bool shape = m.PieceCount == 3;
                for (int i = 0; shape && i < 3; i++) shape = !m.GetPiece(i).IsArc && m.GetPiece(i).EndGrade == 0f;
                Check(problems, where, shape, "R1/R8: gap module must be three flat straight pieces");
                if (!shape) continue;
                Check(problems, where, m.GetPiece(0).Length >= 15f && m.GetPiece(2).Length >= 15f, "R1: runway under 15 m");
                Check(problems, where, m.GapStartS == m.StartS + m.GetPiece(0).Length && m.GapLength == m.GetPiece(1).Length, "gap must be the middle piece");
            }
        }

        // R2
        private static void CheckSharpThenGrapple(List<string> problems, ulong seed, List<CourseModule> modules)
        {
            for (int i = 1; i < modules.Count; i++)
            {
                if (modules[i - 1].Kind == ModuleKind.SharpCurve && modules[i].Kind == ModuleKind.GrappleGap)
                    problems.Add($"seed {seed} {Describe(modules[i])}: R2 grapple gap right after a sharp curve");
            }
        }

        // R3: never more than 200 m of non-rest modules in a row.
        private void CheckRestSpacing(List<string> problems, ulong seed, List<CourseModule> modules)
        {
            double sinceRest = 0d;
            foreach (CourseModule m in modules)
            {
                if (IsRest(m, 40f)) { sinceRest = 0d; continue; }
                sinceRest += m.Length;
                if (sinceRest > 200d + 1e-6) problems.Add($"seed {seed} {Describe(m)}: R3 {sinceRest:F1} m without a rest");
            }
        }

        // R4: three of a kind in a row is only allowed when the third is the escape rest.
        private static void CheckSameKindRuns(List<string> problems, ulong seed, List<CourseModule> modules)
        {
            for (int i = 2; i < modules.Count; i++)
            {
                if (modules[i].Kind == modules[i - 1].Kind && modules[i].Kind == modules[i - 2].Kind && !modules[i].IsEscape)
                    problems.Add($"seed {seed} {Describe(modules[i])}: R4 third {modules[i].Kind} in a row");
            }
        }

        // R5: modules ending within 600 m of a module's end turn at most 180 degrees net.
        private static void CheckNetTurn(List<string> problems, ulong seed, List<CourseModule> modules)
        {
            for (int j = 0; j < modules.Count; j++)
            {
                double net = 0d;
                for (int i = j; i >= 0 && modules[i].EndS > modules[j].EndS - 600d; i--) net += modules[i].TotalTurnDegrees;
                if (Math.Abs(net) > 180d + 1e-3) problems.Add($"seed {seed} {Describe(modules[j])}: R5 net turn {net:F1} deg in 600 m");
            }
        }

        // R6, measured on the built centerline.
        private static void CheckHeight(List<string> problems, ulong seed, Centerline line)
        {
            float lowest = float.MaxValue;
            float highest = float.MinValue;
            for (double s = 0d; s <= line.EndS; s += SampleSpacing)
            {
                float y = line.FrameAt(s).Position.y;
                lowest = Mathf.Min(lowest, y);
                highest = Mathf.Max(highest, y);
            }
            if (lowest < -40f - Eps || highest > 40f + Eps) problems.Add($"seed {seed}: R6 height {lowest:F1}..{highest:F1} leaves +-40 m");
        }

        // R7, measured on the built centerline: no two road sections that are not neighbours and lie within
        // 1200 m of each other may come closer than the width of the road with its guards.
        private static void CheckSeparation(List<string> problems, ulong seed, List<CourseModule> modules, Centerline line)
        {
            var samples = new Vector2[modules.Count][];
            for (int i = 0; i < modules.Count; i++)
            {
                var list = new List<Vector2>();
                for (double s = modules[i].StartS; s < modules[i].EndS; s += SampleSpacing)
                {
                    Vector3 p = line.FrameAt(s).Position;
                    list.Add(new Vector2(p.x, p.z));
                }
                Vector3 e = line.FrameAt(modules[i].EndS).Position;
                list.Add(new Vector2(e.x, e.z));
                samples[i] = list.ToArray();
            }
            for (int j = 2; j < modules.Count; j++)
            {
                for (int i = j - 2; i >= 0 && modules[i].EndS >= modules[j].StartS - 1200d; i--)
                {
                    float nearest = float.MaxValue;
                    foreach (Vector2 a in samples[i])
                    {
                        foreach (Vector2 b in samples[j]) nearest = Mathf.Min(nearest, (a - b).magnitude);
                    }
                    if (nearest < RoadClearance)
                        problems.Add($"seed {seed}: R7 {Describe(modules[i])} and {Describe(modules[j])} come within {nearest:F1} m");
                }
            }
        }

        // Open edges: only from 600 m, only straights and gentle curves, one side, at most 10% of all modules so far.
        private static void CheckOpenEdges(List<string> problems, ulong seed, List<CourseModule> modules)
        {
            int open = 0;
            for (int i = 0; i < modules.Count; i++)
            {
                CourseModule m = modules[i];
                if (!m.LeftOpen && !m.RightOpen) continue;
                open++;
                string where = $"seed {seed} {Describe(m)}";
                Check(problems, where, !(m.LeftOpen && m.RightOpen), "open edge on both sides");
                Check(problems, where, m.StartS >= 600d, "open edge before 600 m");
                Check(problems, where, m.Kind == ModuleKind.Straight || m.Kind == ModuleKind.GentleCurve, $"open edge on a {m.Kind}");
                Check(problems, where, open <= 0.10 * (i + 1) + 1e-6, $"{open} open edges in {i + 1} modules exceeds 10%");
            }
        }

        [Test]
        public void OpenEdges_TwentySeedsTenKilometres_ActuallyAppear()
        {
            int open = 0;
            int total = 0;
            for (ulong seed = 0; seed < SeedCount; seed++)
            {
                foreach (CourseModule m in Generate(seed, TenKilometres))
                {
                    total++;
                    if (m.LeftOpen || m.RightOpen) open++;
                }
            }
            Debug.Log($"CourseGeneratorTests: open edges on {open} of {total} modules ({100d * open / total:F1}%).");
            Assert.That(open, Is.GreaterThan(0));
        }

        // ---- fallback and failure ----------------------------------------------------------------------

        [Test]
        public void Next_OnlySharpCurvesAllowed_FallsBackToEscapeRest()
        {
            SetField(tuning, "weightsAtStart", new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f });
            SetField(tuning, "weightsAtFull", new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f });
            SetField(tuning, "candidatesPerModule", 1);
            var generator = new CourseGenerator(1UL, tuning);
            var modules = new List<CourseModule>();
            for (int i = 0; i < 12; i++) modules.Add(generator.Next());
            int flagged = 0;
            foreach (CourseModule m in modules) if (m.IsEscape) flagged++;
            Assert.That(generator.EscapeCount, Is.GreaterThan(0), "a third sharp curve in a row must be refused");
            Assert.That(flagged, Is.EqualTo(generator.EscapeCount));
            foreach (CourseModule m in modules)
            {
                if (m.IsEscape) Assert.That(m.Length, Is.EqualTo(40d));
            }
        }

        [Test]
        public void Constructor_NullTuning_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CourseGenerator(1UL, null));
        }

        [Test]
        public void Constructor_DefaultTuning_IsValid()
        {
            Assert.That(tuning.TryValidate(out string error), Is.True, error);
            Assert.DoesNotThrow(() => new CourseGenerator(1UL, tuning));
        }

        [Test]
        public void Constructor_RadiusBelowThirty_ThrowsArgumentException()
        {
            SetField(tuning, "gentleRadiusStart", new Vector2(20f, 40f));
            Assert.Throws<ArgumentException>(() => new CourseGenerator(1UL, tuning));
        }

        [Test]
        public void Constructor_JumpGapBeyondLimit_ThrowsArgumentException()
        {
            SetField(tuning, "jumpGapLengthFull", new Vector2(5f, 9f));
            Assert.Throws<ArgumentException>(() => new CourseGenerator(1UL, tuning));
        }

        [Test]
        public void Constructor_WrongWeightCount_ThrowsArgumentException()
        {
            SetField(tuning, "weightsAtStart", new[] { 1f, 2f });
            Assert.Throws<ArgumentException>(() => new CourseGenerator(1UL, tuning));
        }

        [Test]
        public void Constructor_RestSpacingNotAboveRestMinimum_ThrowsArgumentException()
        {
            SetField(tuning, "restSpacing", 30f);
            Assert.Throws<ArgumentException>(() => new CourseGenerator(1UL, tuning));
        }

        [Test]
        public void CourseModule_PieceCountOutOfRange_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CourseModule(0, ModuleKind.Straight, false, 0d, ReadOnlySpan<CourseModule.Piece>.Empty,
                0d, 0f, false, 0d, 0f, 0f, false, false));
        }

        [Test]
        public void AppendTo_NullCenterline_Throws()
        {
            CourseModule module = Generate(1UL, 10d)[0];
            Assert.Throws<ArgumentNullException>(() => module.AppendTo(null));
        }

        // ---- cost --------------------------------------------------------------------------------------

        [Test]
        public void Next_TenKilometresPerSeed_LogsGenerationTime()
        {
            var watch = Stopwatch.StartNew();
            int modules = 0;
            for (ulong seed = 0; seed < SeedCount; seed++) modules += Generate(seed, TenKilometres).Count;
            watch.Stop();
            Debug.Log($"CourseGeneratorTests: generated 20 x 10 km ({modules} modules) in {watch.ElapsedMilliseconds} ms, " +
                      $"{(double)watch.ElapsedMilliseconds / SeedCount:F1} ms per 10 km course.");
            Assert.That(modules, Is.GreaterThan(SeedCount * 100));
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"no field {name}");
            field.SetValue(target, value);
        }
    }
}
