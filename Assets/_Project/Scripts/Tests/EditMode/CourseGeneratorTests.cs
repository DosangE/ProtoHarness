using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using ProtoHarness.ChainRush.Track;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace ProtoHarness.Tests.EditMode
{
    // The rule checks here are written from the rule text (docs/COURSE.md 6-6) and integrate the module
    // pieces with their own geometry, independently of CourseGenerator's code.
    public sealed class CourseGeneratorTests
    {
        private const int SeedCount = 20;
        private const double RampDistance = 1400d;
        private const double LongDistance = 10000d;
        private const double Epsilon = 1e-4;

        private CourseTuning tuning;

        [SetUp]
        public void SetUp() => tuning = ScriptableObject.CreateInstance<CourseTuning>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(tuning);

        private static List<CourseModule> Generate(CourseGenerator generator, double distance)
        {
            var modules = new List<CourseModule>(256);
            while (generator.EndS < distance) modules.Add(generator.Next());
            return modules;
        }

        [Test]
        public void FindProblem_Defaults_ReturnsNull()
        {
            Assert.That(tuning.FindProblem(), Is.Null);
        }

        [Test]
        public void Weight_SharpCurve_RampsLinearlyThenHolds()
        {
            Assert.That(tuning.Weight(ModuleKind.SharpCurve, 0d), Is.EqualTo(0f));
            Assert.That(tuning.Weight(ModuleKind.SharpCurve, 700d), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(tuning.Weight(ModuleKind.SharpCurve, 1400d), Is.EqualTo(2f).Within(1e-5f));
            Assert.That(tuning.Weight(ModuleKind.SharpCurve, 50000d), Is.EqualTo(2f).Within(1e-5f));
            Assert.That(tuning.Weight(ModuleKind.Rest, 700d), Is.EqualTo(0f));
        }

        [Test]
        public void Constructor_NullTuning_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CourseGenerator(1UL, null));
        }

        [Test]
        public void Constructor_JumpGapBeyondLimit_ThrowsArgumentException()
        {
            SetVector2("jumpGapFull", new Vector2(5f, 9f));
            var error = Assert.Throws<ArgumentException>(() => new CourseGenerator(1UL, tuning));
            StringAssert.Contains("jumpGap", error.Message);
        }

        [Test]
        public void Constructor_RestSpacingShorterThanModules_ThrowsArgumentException()
        {
            SetFloat("restSpacing", 100f);
            var error = Assert.Throws<ArgumentException>(() => new CourseGenerator(1UL, tuning));
            StringAssert.Contains("restSpacing", error.Message);
        }

        [Test]
        public void Constructor_RadiusBelowMinimum_ThrowsArgumentException()
        {
            SetVector2("sharpRadiusFull", new Vector2(20f, 50f));
            var error = Assert.Throws<ArgumentException>(() => new CourseGenerator(1UL, tuning));
            StringAssert.Contains("sharpRadius", error.Message);
        }

        [Test]
        public void Next_FirstModule_IsSpawnRest()
        {
            CourseModule first = new CourseGenerator(3UL, tuning).Next();
            Assert.That(first.Kind, Is.EqualTo(ModuleKind.Rest));
            Assert.That(first.StartS, Is.EqualTo(0d));
            Assert.That(first.Length, Is.EqualTo((double)tuning.SpawnRestLength));
        }

        [Test]
        public void Next_SameSeedInterleaved_GivesIdenticalModulesTo1400m()
        {
            var a = new CourseGenerator(11UL, tuning);
            var b = new CourseGenerator(11UL, tuning);
            int modules = 0;
            while (a.EndS < RampDistance)
            {
                CourseModule fromA = a.Next();
                CourseModule fromB = b.Next();
                Assert.That(fromB, Is.EqualTo(fromA), $"module {modules}");
                modules++;
            }
            Assert.That(b.EndS, Is.EqualTo(a.EndS));
            Assert.That(b.EndX, Is.EqualTo(a.EndX));
            Assert.That(b.EndZ, Is.EqualTo(a.EndZ));
            Assert.That(modules, Is.GreaterThan(10));
        }

        [Test]
        public void Next_Seeds0To19_GiveDifferentSequences()
        {
            var sequences = new List<List<CourseModule>>(SeedCount);
            for (ulong seed = 0; seed < SeedCount; seed++)
                sequences.Add(Generate(new CourseGenerator(seed, tuning), RampDistance));
            for (int i = 0; i < SeedCount; i++)
            for (int j = i + 1; j < SeedCount; j++)
            {
                int shared = Math.Min(sequences[i].Count, sequences[j].Count);
                bool differs = sequences[i].Count != sequences[j].Count;
                for (int k = 1; k < shared && !differs; k++) differs = !sequences[i][k].Equals(sequences[j][k]);
                Assert.That(differs, Is.True, $"seeds {i} and {j} produced the same course");
            }
        }

        [Test]
        public void Next_TwentySeeds1400m_EveryKindAppears()
        {
            var seen = new HashSet<ModuleKind>();
            for (ulong seed = 0; seed < SeedCount; seed++)
                foreach (CourseModule module in Generate(new CourseGenerator(seed, tuning), RampDistance))
                    seen.Add(module.Kind);
            foreach (ModuleKind kind in Enum.GetValues(typeof(ModuleKind)))
                Assert.That(seen, Does.Contain(kind), $"{kind} never appeared in {SeedCount} seeds x {RampDistance}m");
        }

        [Test]
        public void Next_TwentySeedsTenKilometres_FollowRulesAndRanges()
        {
            int modules = 0, escapes = 0, open = 0, rests = 0;
            for (ulong seed = 0; seed < SeedCount; seed++)
            {
                var generator = new CourseGenerator(seed, tuning);
                List<CourseModule> course = Generate(generator, LongDistance);
                Check(seed, course);
                modules += course.Count;
                escapes += generator.EscapeCount;
                open += generator.OpenEdgeCount;
                foreach (CourseModule module in course)
                    if (module.Kind == ModuleKind.Rest) rests++;
            }
            Debug.Log($"[CourseGenerator] {SeedCount} seeds x {LongDistance}m: {modules} modules, {rests} rests, {open} open-edge, {escapes} escapes.");
            // Escapes are a fallback, not the normal path: at most 5% of modules.
            Assert.That(escapes, Is.LessThanOrEqualTo(modules / 20), "escape rests");
        }

        [Test]
        public void AppendTo_TenKilometres_CenterlineEndMatchesGeneratorPose()
        {
            var generator = new CourseGenerator(7UL, tuning);
            var line = new Centerline(Vector3.zero, 0f);
            foreach (CourseModule module in Generate(generator, LongDistance)) module.AppendTo(line);

            TrackFrame end = line.FrameAt(line.EndS);
            double lineYaw = Math.Atan2(end.Forward.x, end.Forward.z) * 180d / Math.PI;
            double yawError = Math.Abs(Mathf.DeltaAngle((float)lineYaw, (float)(generator.EndYaw % 360d)));
            double positionError = Math.Sqrt(Square(end.Position.x - generator.EndX) + Square(end.Position.z - generator.EndZ));
            double heightError = Math.Abs(end.Position.y - generator.EndHeight);
            Debug.Log($"[CourseGenerator] AppendTo {line.EndS:F1}m, {line.PieceCount} pieces: position error {positionError:F4}m, height error {heightError:F4}m, yaw error {yawError:F5} deg.");

            Assert.That(line.EndS, Is.EqualTo(generator.EndS).Within(1e-6), "S");
            Assert.That(positionError, Is.LessThan(0.5d), "position");
            Assert.That(heightError, Is.LessThan(0.05d), "height");
            Assert.That(yawError, Is.LessThan(0.05d), "yaw");
        }

        [Test]
        public void AppendTo_OutOfOrder_Throws()
        {
            var generator = new CourseGenerator(7UL, tuning);
            generator.Next();
            CourseModule second = generator.Next();
            var line = new Centerline(Vector3.zero, 0f);
            Assert.Throws<InvalidOperationException>(() => second.AppendTo(line));
        }

        [Test]
        public void Next_TenKilometres_LogsGenerationTime()
        {
            var watch = Stopwatch.StartNew();
            var generator = new CourseGenerator(1UL, tuning);
            Generate(generator, LongDistance);
            watch.Stop();
            Debug.Log($"[CourseGenerator] {LongDistance}m ({generator.Count} modules) generated in {watch.Elapsed.TotalMilliseconds:F1} ms.");
            Assert.That(generator.EndS, Is.GreaterThanOrEqualTo(LongDistance));
        }

        // ---- Independent rule checker ----

        private void Check(ulong seed, List<CourseModule> course)
        {
            double x = 0d, z = 0d, yaw = 0d, height = 0d;
            double lastRestEnd = 0d;
            int openCount = 0;
            var points = new List<List<double>>(course.Count);
            for (int i = 0; i < course.Count; i++)
            {
                CourseModule m = course[i];
                string at = $"seed {seed} {m}";

                // Chain and shape basics.
                Assert.That(m.Index, Is.EqualTo(i), at);
                Assert.That(m.StartS, Is.EqualTo(i == 0 ? 0d : course[i - 1].EndS), at + " start S");
                double pieceSum = 0d;
                for (int p = 0; p < m.PieceCount; p++) pieceSum += m.PieceLength(p);
                Assert.That(m.Length, Is.EqualTo(pieceSum), at + " length");
                Assert.That(m.PieceEndGrade(m.PieceCount - 1), Is.EqualTo(0f), at + " ends flat");
                CheckShape(m, at, i);

                // Physical limits regardless of tuning.
                for (int p = 0; p < m.PieceCount; p++)
                {
                    Assert.That(Math.Abs(m.PieceEndGrade(p)), Is.LessThanOrEqualTo(CourseTuning.GradeLimit), at + " grade");
                    if (m.PieceRadius(p) > 0f) Assert.That(m.PieceRadius(p), Is.GreaterThanOrEqualTo(CourseTuning.MinRadius - Epsilon), at + " radius");
                }

                // R1, R8: gaps sit on straight pieces with 15m of straight road on both sides.
                if (m.HasGap)
                {
                    for (int p = 0; p < m.PieceCount; p++) Assert.That(m.PieceRadius(p), Is.EqualTo(0f), at + " R8");
                    Assert.That(m.GapStart, Is.GreaterThanOrEqualTo(CourseTuning.MinGapApproach), at + " R1 run-up");
                    Assert.That(m.Length - m.GapStart - m.GapLength, Is.GreaterThanOrEqualTo(CourseTuning.MinGapApproach - Epsilon), at + " R1 landing");
                }

                // R2
                if (m.Kind == ModuleKind.GrappleGap && i > 0)
                    Assert.That(course[i - 1].Kind, Is.Not.EqualTo(ModuleKind.SharpCurve), at + " R2");

                // R3: no non-rest module reaches past restSpacing from the last rest's end.
                if (m.Kind == ModuleKind.Rest)
                {
                    Assert.That(m.StartS - lastRestEnd, Is.LessThanOrEqualTo(tuning.RestSpacing + Epsilon), at + " R3");
                    lastRestEnd = m.EndS;
                }
                else
                {
                    Assert.That(m.EndS - lastRestEnd, Is.LessThanOrEqualTo(tuning.RestSpacing + Epsilon), at + " R3");
                }

                // R4
                if (!m.IsEscape && i >= 2)
                    Assert.That(m.Kind == course[i - 1].Kind && m.Kind == course[i - 2].Kind, Is.False, at + " R4");

                // R5: net turn over the window ending at this module's end. Escape rests are exempt: they add no
                // turn, but the window sliding past older turns can still raise the net.
                double turn = 0d;
                for (int j = i; j >= 0 && course[j].EndS > m.EndS - tuning.TurnWindow; j--) turn += course[j].TurnDegrees;
                if (!m.IsEscape)
                    Assert.That(Math.Abs(turn), Is.LessThanOrEqualTo(tuning.MaxNetTurn + 1e-3), at + " R5");

                // Walk the pieces: heights for R6, samples for R7.
                var mine = new List<double> { x, z };
                double grade = 0d;
                for (int p = 0; p < m.PieceCount; p++)
                {
                    double length = m.PieceLength(p);
                    double endGrade = m.PieceEndGrade(p);
                    for (double t = 1d; t < length; t += 1d)
                    {
                        double h = height + grade * t + (endGrade - grade) * t * t / (2d * length);
                        Assert.That(Math.Abs(h), Is.LessThanOrEqualTo(tuning.MaxHeightOffset + Epsilon), at + " R6");
                    }
                    height += (grade + endGrade) * length / 2d;
                    Assert.That(Math.Abs(height), Is.LessThanOrEqualTo(tuning.MaxHeightOffset + Epsilon), at + " R6");
                    grade = endGrade;

                    int samples = Math.Max(1, (int)Math.Ceiling(length / tuning.OverlapSampleStep));
                    double radians = yaw * Math.PI / 180d;
                    double fx = Math.Sin(radians), fz = Math.Cos(radians), rx = fz, rz = -fx;
                    double radius = m.PieceRadius(p);
                    double degrees = m.PieceDegrees(p);
                    for (int k = 1; k <= samples; k++)
                    {
                        double u = (double)k / samples;
                        double ahead, aside;
                        if (radius > 0d)
                        {
                            double phi = Math.Abs(degrees) * u * Math.PI / 180d;
                            ahead = radius * Math.Sin(phi);
                            aside = Math.Sign(degrees) * radius * (1d - Math.Cos(phi));
                        }
                        else
                        {
                            ahead = length * u;
                            aside = 0d;
                        }
                        double px = x + fx * ahead + rx * aside;
                        double pz = z + fz * ahead + rz * aside;
                        mine.Add(px);
                        mine.Add(pz);
                        if (k == samples)
                        {
                            x = px;
                            z = pz;
                        }
                    }
                    yaw += degrees;
                }
                // R7: every centerline sample stays at least twice overlapMargin from every sample of the modules
                // before the previous one that end within the window.
                double clearance = 2d * tuning.OverlapMargin;
                for (int j = 0; j < i - 1; j++)
                {
                    if (course[j].EndS < m.StartS - tuning.OverlapWindow) continue;
                    List<double> other = points[j];
                    double closest = double.MaxValue;
                    for (int a = 0; a < mine.Count; a += 2)
                    for (int b = 0; b < other.Count; b += 2)
                        closest = Math.Min(closest, Math.Sqrt(Square(mine[a] - other[b]) + Square(mine[a + 1] - other[b + 1])));
                    Assert.That(closest, Is.GreaterThanOrEqualTo(clearance), $"{at} R7 too close to module {j}");
                }
                points.Add(mine);

                // Open edges: past openEdgeStart, straights and gentle curves only, one side, a gentle curve's inside,
                // and never more than openEdgeMaxShare of the modules so far.
                if (m.OpenLeft || m.OpenRight)
                {
                    openCount++;
                    Assert.That(m.OpenLeft && m.OpenRight, Is.False, at + " one open side");
                    Assert.That(m.Kind == ModuleKind.Straight || m.Kind == ModuleKind.GentleCurve, Is.True, at + " open kind");
                    Assert.That(m.StartS, Is.GreaterThanOrEqualTo((double)tuning.OpenEdgeStart), at + " open start");
                    if (m.Kind == ModuleKind.GentleCurve)
                        Assert.That(m.OpenRight, Is.EqualTo(m.TurnDegrees > 0f), at + " open inside");
                    Assert.That(openCount, Is.LessThanOrEqualTo(tuning.OpenEdgeMaxShare * (i + 1) + Epsilon), at + " open share");
                }
            }
        }

        // Kind-specific pieces and parameter ranges at the module's start S.
        private void CheckShape(CourseModule m, string at, int index)
        {
            double s = m.StartS;
            bool noGap = !m.HasGap && !m.HasAnchor;
            switch (m.Kind)
            {
                case ModuleKind.Straight:
                    Assert.That(m.PieceCount, Is.EqualTo(1), at);
                    AssertStraight(m, 0, 0f, at);
                    AssertIn(m.PieceLength(0), tuning.StraightLength(s), at + " length");
                    Assert.That(noGap, Is.True, at);
                    break;
                case ModuleKind.Rest:
                    Assert.That(m.PieceCount, Is.EqualTo(1), at);
                    AssertStraight(m, 0, 0f, at);
                    Assert.That(m.OpenLeft || m.OpenRight, Is.False, at + " rest keeps guards");
                    if (index == 0) Assert.That(m.PieceLength(0), Is.EqualTo(tuning.SpawnRestLength), at);
                    else if (m.IsEscape) Assert.That(m.PieceLength(0), Is.EqualTo(tuning.EscapeRestLength), at);
                    else AssertIn(m.PieceLength(0), tuning.RestLength(s), at + " length");
                    Assert.That(noGap, Is.True, at);
                    break;
                case ModuleKind.GentleCurve:
                case ModuleKind.SharpCurve:
                {
                    bool gentle = m.Kind == ModuleKind.GentleCurve;
                    Assert.That(m.PieceCount, Is.EqualTo(1), at);
                    AssertIn(m.PieceRadius(0), gentle ? tuning.GentleRadius(s) : tuning.SharpRadius(s), at + " radius");
                    AssertIn(Math.Abs(m.PieceDegrees(0)), gentle ? tuning.GentleDegrees(s) : tuning.SharpDegrees(s), at + " degrees");
                    Assert.That(m.PieceEndGrade(0), Is.EqualTo(0f), at);
                    Assert.That(noGap, Is.True, at);
                    break;
                }
                case ModuleKind.SCurve:
                    Assert.That(m.PieceCount, Is.EqualTo(2), at);
                    Assert.That(m.PieceRadius(1), Is.EqualTo(m.PieceRadius(0)), at + " same radius");
                    AssertIn(m.PieceRadius(0), tuning.SCurveRadius(s), at + " radius");
                    Assert.That(Math.Sign(m.PieceDegrees(0)), Is.EqualTo(-Math.Sign(m.PieceDegrees(1))), at + " opposite turns");
                    AssertIn(Math.Abs(m.PieceDegrees(0)), tuning.SCurveDegrees(s), at + " degrees 0");
                    AssertIn(Math.Abs(m.PieceDegrees(1)), tuning.SCurveDegrees(s), at + " degrees 1");
                    Assert.That(m.PieceEndGrade(0), Is.EqualTo(0f), at);
                    Assert.That(noGap, Is.True, at);
                    break;
                case ModuleKind.Uphill:
                case ModuleKind.Downhill:
                {
                    Assert.That(m.PieceCount, Is.EqualTo(3), at);
                    float grade = m.PieceEndGrade(0);
                    bool up = m.Kind == ModuleKind.Uphill;
                    AssertIn(up ? grade : -grade, up ? tuning.UphillGrade(s) : tuning.DownhillGrade(s), at + " grade");
                    AssertStraight(m, 0, grade, at);
                    AssertStraight(m, 1, grade, at);
                    AssertStraight(m, 2, 0f, at);
                    Assert.That(m.PieceLength(0), Is.EqualTo(tuning.SlopeRampLength), at + " ramp");
                    Assert.That(m.PieceLength(2), Is.EqualTo(tuning.SlopeRampLength), at + " ramp");
                    AssertIn(m.PieceLength(1), tuning.SlopeLength(s), at + " hold");
                    Assert.That(noGap, Is.True, at);
                    break;
                }
                case ModuleKind.Hill:
                {
                    Assert.That(m.PieceCount, Is.EqualTo(3), at);
                    float grade = m.PieceEndGrade(0);
                    AssertIn(grade, tuning.HillGrade(s), at + " grade");
                    AssertStraight(m, 0, grade, at);
                    AssertStraight(m, 1, -grade, at);
                    AssertStraight(m, 2, 0f, at);
                    AssertIn(m.PieceLength(0), tuning.HillPieceLength(s), at + " piece");
                    Assert.That(m.PieceLength(1), Is.EqualTo(m.PieceLength(0)), at + " equal pieces");
                    Assert.That(m.PieceLength(2), Is.EqualTo(m.PieceLength(0)), at + " equal pieces");
                    Assert.That(noGap, Is.True, at);
                    break;
                }
                case ModuleKind.JumpGap:
                case ModuleKind.GrappleGap:
                {
                    Assert.That(m.PieceCount, Is.EqualTo(3), at);
                    for (int p = 0; p < 3; p++) AssertStraight(m, p, 0f, at);
                    Assert.That(m.HasGap, Is.True, at);
                    Assert.That(m.GapStart, Is.EqualTo(m.PieceLength(0)), at + " gap start");
                    Assert.That(m.GapLength, Is.EqualTo(m.PieceLength(1)), at + " gap length");
                    Assert.That(m.PieceLength(0), Is.EqualTo(tuning.GapApproachLength), at + " run-up");
                    Assert.That(m.PieceLength(2), Is.EqualTo(tuning.GapApproachLength), at + " landing");
                    Assert.That(m.OpenLeft || m.OpenRight, Is.False, at + " gap keeps guards");
                    if (m.Kind == ModuleKind.JumpGap)
                    {
                        AssertIn(m.GapLength, tuning.JumpGap(s), at + " jump gap");
                        Assert.That(m.GapLength, Is.LessThanOrEqualTo(CourseTuning.JumpGapLimit), at + " jump limit");
                        Assert.That(m.HasAnchor, Is.False, at);
                    }
                    else
                    {
                        AssertIn(m.GapLength, tuning.GrappleGap(s), at + " grapple gap");
                        Assert.That(m.HasAnchor, Is.True, at);
                        Assert.That(m.AnchorAlong, Is.EqualTo(m.GapStart + m.GapLength / 2f).Within(1e-4f), at + " anchor at gap middle");
                        Assert.That(m.AnchorHeight, Is.EqualTo(tuning.GrappleAnchorHeight), at + " anchor height");
                        AssertIn(m.AnchorSide, tuning.GrappleAnchorSide(s), at + " anchor side");
                    }
                    break;
                }
                default:
                    Assert.Fail($"{at}: unexpected kind");
                    break;
            }
        }

        private static void AssertStraight(CourseModule m, int piece, float endGrade, string at)
        {
            Assert.That(m.PieceRadius(piece), Is.EqualTo(0f), $"{at} piece {piece} straight");
            Assert.That(m.PieceDegrees(piece), Is.EqualTo(0f), $"{at} piece {piece} straight");
            Assert.That(m.PieceEndGrade(piece), Is.EqualTo(endGrade), $"{at} piece {piece} grade");
        }

        // Parameters are drawn as doubles and stored as floats, so allow a float rounding step at each end.
        private static void AssertIn(double value, Vector2 range, string at)
        {
            Assert.That(value, Is.InRange(range.x - Epsilon, range.y + Epsilon), $"{at} not in [{range.x}, {range.y}]");
        }

        private static double Square(double value) => value * value;

        // Reflection instead of SerializedObject so OnValidate does not log the error the generator should throw.
        private void SetFloat(string field, float value) => Set(field, value);

        private void SetVector2(string field, Vector2 value) => Set(field, value);

        private void Set(string field, object value)
        {
            FieldInfo info = typeof(CourseTuning).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, field);
            info.SetValue(tuning, value);
        }
    }
}
