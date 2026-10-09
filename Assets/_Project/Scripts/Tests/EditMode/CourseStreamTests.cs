using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush.Endless;
using ProtoHarness.ChainRush.Track;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class CourseStreamTests
    {
        private const double Ahead = 300d;
        private const double Behind = 60d;
        private const double Tolerance = 1e-6;

        private CourseTuning tuning;

        [SetUp]
        public void SetUp() => tuning = ScriptableObject.CreateInstance<CourseTuning>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(tuning);

        private CourseStream NewStream(ulong seed)
        {
            var stream = new CourseStream(new Centerline(Vector3.zero, 0f), tuning, Ahead, Behind);
            stream.Reset(seed);
            return stream;
        }

        // Advances in runner-sized steps, checking the stream after every one.
        private static void Run(CourseStream stream, double to, Action<double> check)
        {
            for (double s = 0d; s <= to; s += 7d)
            {
                stream.Advance(s);
                check(s);
            }
        }

        [Test]
        public void Advance_BeforeReset_Throws()
        {
            var stream = new CourseStream(new Centerline(Vector3.zero, 0f), tuning, Ahead, Behind);
            Assert.Throws<InvalidOperationException>(() => stream.Advance(0d));
        }

        [Test]
        public void Reset_FirstSpan_StartsAtLeadInBeforeSpawnRest()
        {
            CourseStream stream = NewStream(1UL);
            Assert.That(stream.Module(0).Kind, Is.EqualTo(ModuleKind.Rest));
            Assert.That(stream.SpanFrom(0), Is.EqualTo(-CourseStream.LeadIn));
            Assert.That(stream.RestRemaining(-5d), Is.EqualTo(65d).Within(Tolerance));
            Assert.That(stream.RestRemaining(5d), Is.EqualTo(55d).Within(Tolerance));
        }

        [Test]
        public void Advance_ThreeKilometres_KeepsAheadAndDropsBehind()
        {
            CourseStream stream = NewStream(4UL);
            Run(stream, 3000d, s =>
            {
                Assert.That(stream.Line.EndS, Is.GreaterThanOrEqualTo(s + Ahead), $"ahead at {s}");
                for (int i = 1; i < stream.ModuleCount; i++)
                {
                    Assert.That(stream.Module(i).StartS, Is.EqualTo(stream.Module(i - 1).EndS), $"modules chain at {s}");
                    Assert.That(stream.Module(i).EndS, Is.GreaterThanOrEqualTo(s - Behind), $"module kept at {s}");
                }
                for (int i = 0; i < stream.SpanCount; i++)
                    Assert.That(stream.SpanTo(i), Is.GreaterThanOrEqualTo(s - Behind), $"span kept at {s}");
            });
        }

        [Test]
        public void Spans_ThreeKilometres_CoverRoadExceptGapsInPiecesOfAtMostFiftyMetres()
        {
            CourseStream stream = NewStream(9UL);
            int gaps = 0;
            Run(stream, 3000d, s =>
            {
                for (int i = 0; i < stream.SpanCount; i++)
                {
                    double length = stream.SpanTo(i) - stream.SpanFrom(i);
                    Assert.That(length, Is.GreaterThan(0d).And.LessThanOrEqualTo(CourseStream.MaxSpanLength + Tolerance), $"span {i} at {s}");
                    if (i == 0) continue;
                    double hole = stream.SpanFrom(i) - stream.SpanTo(i - 1);
                    if (Math.Abs(hole) <= Tolerance) continue;
                    // A hole between spans must be exactly one module's gap.
                    Assert.That(stream.TryNextGap(stream.SpanTo(i - 1), out double start, out double gap, out _), Is.True, $"hole at {stream.SpanTo(i - 1)}");
                    Assert.That(start, Is.EqualTo(stream.SpanTo(i - 1)).Within(Tolerance), "hole starts at the gap");
                    Assert.That(hole, Is.EqualTo(gap).Within(1e-4), "hole is the gap");
                    gaps++;
                }
            });
            Assert.That(gaps, Is.GreaterThan(0), "3 km should contain gaps");
        }

        [Test]
        public void Spans_ProfileGuards_FollowModuleOpenEdges()
        {
            int open = 0;
            for (ulong seed = 0; seed < 5 && open == 0; seed++)
            {
                CourseStream stream = NewStream(seed);
                Run(stream, 3000d, s =>
                {
                    for (int i = 0; i < stream.SpanCount; i++)
                    {
                        double middle = (stream.SpanFrom(i) + stream.SpanTo(i)) / 2d;
                        Assert.That(stream.TryModuleAt(middle, out CourseModule module), Is.True);
                        RoadProfile profile = stream.SpanProfile(i);
                        Assert.That(profile.LeftGuard, Is.EqualTo(!module.OpenLeft), $"{module} left guard");
                        Assert.That(profile.RightGuard, Is.EqualTo(!module.OpenRight), $"{module} right guard");
                        Assert.That(profile.HalfWidth, Is.EqualTo(RoadProfile.DeckHalfWidth));
                        if (module.OpenLeft || module.OpenRight) open++;
                    }
                });
            }
            Assert.That(open, Is.GreaterThan(0), "some span should have an open edge");
        }

        [Test]
        public void Anchors_Positions_MatchCenterlineFrameOfTheirGrappleGap()
        {
            CourseStream stream = NewStream(2UL);
            int checkedAnchors = 0;
            Run(stream, 3000d, s =>
            {
                for (int i = 0; i < stream.AnchorCount; i++)
                {
                    double anchorS = stream.AnchorS(i);
                    Assert.That(stream.TryModuleAt(anchorS, out CourseModule module), Is.True);
                    Assert.That(module.Kind, Is.EqualTo(ModuleKind.GrappleGap));
                    Assert.That(anchorS, Is.EqualTo(module.StartS + module.AnchorAlong).Within(Tolerance));
                    TrackFrame frame = stream.Line.FrameAt(anchorS);
                    Vector3 expected = frame.Position + frame.Right * module.AnchorSide + Vector3.up * module.AnchorHeight;
                    Assert.That(Vector3.Distance(stream.AnchorPosition(i), expected), Is.LessThan(1e-4f));
                    checkedAnchors++;
                }
            });
            Assert.That(checkedAnchors, Is.GreaterThan(0), "3 km should contain grapple gaps");
        }

        [Test]
        public void Reset_SameSeed_GivesSameSpansAndOtherSeedDiffers()
        {
            var first = Snapshot(NewStream(6UL));
            var again = Snapshot(NewStream(6UL));
            var other = Snapshot(NewStream(7UL));
            Assert.That(again, Is.EqualTo(first));
            Assert.That(other, Is.Not.EqualTo(first));
        }

        [Test]
        public void Reset_AfterRunning_StartsTheSameCourseAgain()
        {
            CourseStream stream = NewStream(6UL);
            List<double> start = Snapshot(stream);
            Run(stream, 1500d, _ => { });
            stream.Reset(6UL);
            Assert.That(Snapshot(stream), Is.EqualTo(start));
            Assert.That(stream.Line.FrameAt(0d).Position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void DistanceToGap_BeforeAndInsideGap_CountsDownToZero()
        {
            CourseStream stream = NewStream(3UL);
            double s = 0d;
            while (!stream.TryNextGap(s, out _, out _, out _))
            {
                s += 50d;
                stream.Advance(s);
            }
            Assert.That(stream.TryNextGap(s, out double start, out double length, out ModuleKind kind), Is.True);
            Assert.That(kind == ModuleKind.JumpGap || kind == ModuleKind.GrappleGap, Is.True);
            Assert.That(stream.DistanceToGap(start - 3d), Is.EqualTo(3d).Within(Tolerance));
            Assert.That(stream.DistanceToGap(start + length / 2d), Is.EqualTo(0d));
            Assert.That(stream.RestRemaining(start), Is.EqualTo(0d), "a gap module is not a rest");
        }

        [Test]
        public void AnchorPosition_AfterOriginShift_MovesWithTheCenterline()
        {
            CourseStream stream = NewStream(2UL);
            double s = 0d;
            while (stream.AnchorCount == 0)
            {
                s += 50d;
                stream.Advance(s);
            }
            Vector3 before = stream.AnchorPosition(0);
            var offset = new Vector3(-300f, 0f, -420f);
            stream.Line.ShiftOrigin(offset);
            Assert.That(Vector3.Distance(stream.AnchorPosition(0), before + offset), Is.LessThan(1e-3f));
        }

        private static List<double> Snapshot(CourseStream stream)
        {
            var values = new List<double>();
            for (int i = 0; i < stream.SpanCount; i++)
            {
                values.Add(stream.SpanFrom(i));
                values.Add(stream.SpanTo(i));
            }
            return values;
        }
    }
}
