using System;
using NUnit.Framework;
using ProtoHarness.ChainRush.Track;
using UnityEngine;

namespace ProtoHarness.Tests.EditMode
{
    // A stadium: straight A (+z) 140 m, a right half turn of R40, straight B (-z) 140 m, another right half
    // turn. The two half turns shift it sideways by 80 m each way, so it closes exactly; a lap is
    // 2 x 140 + 2 x pi x 40 m.
    public sealed class CenterlineLoopTests
    {
        private const float Straight = 140f;
        private const float Radius = 40f;
        private static readonly double LapLength = 2d * Straight + 2d * Math.PI * Radius;
        private static readonly double StraightBStart = Straight + Math.PI * Radius;

        private static Centerline Stadium(float secondStraight = Straight, float secondTurn = 180f)
        {
            var line = new Centerline(Vector3.zero, 0f);
            line.AppendStraight(Straight);
            line.AppendArc(Radius, 180f);
            line.AppendStraight(secondStraight);
            line.AppendArc(Radius, secondTurn);
            return line;
        }

        [Test]
        public void ProjectWithoutLoop_PointOnReturnStraight_ChoosesTheFirstPieceAndIsWrong()
        {
            // The rule the loop mode replaces (Centerline.PieceAt): the first piece whose span the point has
            // not passed. A return leg lies "behind" the first straight's end line, so it wins wrongly.
            Centerline line = Stadium();
            TrackCoord coord = line.Project(new Vector3(81.5f, 0.3f, 70f));
            Assert.That(coord.S, Is.EqualTo(70d).Within(1e-3), "Old rule: the point is taken for a spot on straight A.");
            Assert.That(Math.Abs(coord.S - (StraightBStart + 70d)), Is.GreaterThan(100d));
        }

        [Test]
        public void Close_Stadium_SetsLoopAndKeepsTheLapLength()
        {
            Centerline line = Stadium();
            Assert.That(line.IsLoop, Is.False);
            line.Close();
            Assert.That(line.IsLoop, Is.True);
            Assert.That(line.EndS, Is.EqualTo(LapLength).Within(1e-3));
        }

        [Test]
        public void Close_EndOffByOneMetre_ThrowsAndTellsTheOffset()
        {
            Centerline line = Stadium(secondStraight: 141f);
            var error = Assert.Throws<InvalidOperationException>(() => line.Close());
            Assert.That(error.Message, Does.Contain("1.0").And.Contain("m"));
            Assert.That(line.IsLoop, Is.False);
        }

        [Test]
        public void Close_HeadingOffByOneDegree_Throws()
        {
            // A second half turn of 181 degrees is rejected by AppendArc itself, so shorten it instead.
            Centerline line = Stadium(secondTurn: 178f);
            Assert.Throws<InvalidOperationException>(() => line.Close());
        }

        [Test]
        public void Close_EndsHigherThanItStarted_Throws()
        {
            var line = new Centerline(Vector3.zero, 0f);
            line.AppendStraight(40f, 0.1f);
            line.AppendStraight(40f, 0f);
            line.AppendArc(Radius, 180f);
            line.AppendStraight(80f);
            line.AppendArc(Radius, 180f);
            Assert.Throws<InvalidOperationException>(() => line.Close());
        }

        [Test]
        public void Close_EndGradeNotFlat_Throws()
        {
            var line = new Centerline(Vector3.zero, 0f);
            line.AppendStraight(Straight);
            line.AppendArc(Radius, 180f);
            line.AppendStraight(Straight, 0.05f);
            line.AppendArc(Radius, 180f);
            Assert.Throws<InvalidOperationException>(() => line.Close());
        }

        [Test]
        public void TryClose_OpenTrack_ReturnsFalseWithAMessageAndNoLoop()
        {
            Centerline line = Stadium(secondStraight: 150f);
            Assert.That(line.TryClose(out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(line.IsLoop, Is.False);
        }

        [Test]
        public void Append_AfterClose_Throws()
        {
            Centerline line = Stadium();
            line.Close();
            Assert.Throws<InvalidOperationException>(() => line.AppendStraight(10f));
            Assert.Throws<InvalidOperationException>(() => line.AppendArc(Radius, 90f));
        }

        [Test]
        public void Clear_AfterClose_OpensTheTrackAgain()
        {
            Centerline line = Stadium();
            line.Close();
            line.Clear();
            Assert.That(line.IsLoop, Is.False);
            line.AppendStraight(10f);
        }

        [Test]
        public void FrameAt_BeyondALap_SameSpotAsOneLapEarlier()
        {
            Centerline line = Stadium();
            line.Close();
            foreach (double s in new[] { 10d, 200d, 340d, 500d })
            {
                Assert.That(Vector3.Distance(line.FrameAt(s + LapLength).Position, line.FrameAt(s).Position), Is.LessThan(1e-2f), $"S {s}");
                Assert.That(Vector3.Distance(line.FrameAt(s - LapLength).Position, line.FrameAt(s).Position), Is.LessThan(1e-2f), $"S {s}");
            }
            Assert.That(line.FrameAt(-5d).S, Is.EqualTo(LapLength - 5d).Within(1e-3));
        }

        [Test]
        public void FrameAt_LapEnd_HasTheStartHeadingPositionAndHeight()
        {
            Centerline line = Stadium();
            line.Close();
            TrackFrame start = line.FrameAt(0d);
            TrackFrame end = line.FrameAt(LapLength - 1e-4);
            Assert.That(Vector3.Distance(end.Position, start.Position), Is.LessThan(0.01f));
            Assert.That(Vector3.Dot(end.Forward, start.Forward), Is.GreaterThan(0.99999f));
        }

        [Test]
        public void Project_PointOnTheReturnStraight_FindsTheReturnStraight()
        {
            Centerline line = Stadium();
            line.Close();
            TrackCoord coord = line.Project(new Vector3(81.5f, 0.3f, 70f));
            Assert.That(coord.S, Is.EqualTo(StraightBStart + 70d).Within(1e-2));
            Assert.That(coord.D, Is.EqualTo(-1.5f).Within(1e-3f));
            Assert.That(coord.H, Is.EqualTo(0.3f).Within(1e-3f));
            TrackFrame frame = line.Frame(new Vector3(81.5f, 0.3f, 70f));
            Assert.That(frame.Forward.z, Is.EqualTo(-1f).Within(1e-4f));
        }

        [Test]
        public void Project_OnAnArc_UsesTheArcAndItsCurvature()
        {
            Centerline line = Stadium();
            line.Close();
            // The first half turn's far point: x = 40 + 40 = 80, z = 140 + 40 = 180... its apex is (40, 0, 180).
            TrackFrame frame = line.Frame(new Vector3(40f, 0f, 180f));
            Assert.That(frame.S, Is.EqualTo(Straight + Math.PI * Radius / 2d).Within(1e-2));
            Assert.That(frame.Curvature, Is.EqualTo(1f / Radius).Within(1e-5f));
        }

        [Test]
        public void Project_AcrossTheStartLine_IsContinuousAndWrapsAtTheLapLength()
        {
            Centerline line = Stadium();
            line.Close();
            Assert.That(line.Project(new Vector3(0.5f, 0f, -1f)).S, Is.EqualTo(LapLength - 1d).Within(0.05));
            Assert.That(line.Project(new Vector3(0.5f, 0f, 1f)).S, Is.EqualTo(1d).Within(0.05));
            Assert.That(line.Project(new Vector3(0.5f, 0f, -1f)).D, Is.EqualTo(0.5f).Within(0.02f));
        }

        [Test]
        public void Focus_NarrowWindow_OnlyLooksAtPiecesNearIt()
        {
            Centerline line = Stadium();
            line.Close();
            var onStraightA = new Vector3(0f, 0f, 70f);
            Assert.That(line.Project(onStraightA).S, Is.EqualTo(70d).Within(1e-2));
            line.FocusWindow = 40f;
            line.SetFocus(StraightBStart + 70d);
            Assert.That(line.HasFocus, Is.True);
            double s = line.Project(onStraightA).S;
            Assert.That(s, Is.GreaterThan(StraightBStart - 1d).And.LessThan(StraightBStart + Straight + 1d), "Only straight B is within the window.");
            line.ClearFocus();
            Assert.That(line.HasFocus, Is.False);
            Assert.That(line.Project(onStraightA).S, Is.EqualTo(70d).Within(1e-2));
        }

        [Test]
        public void Focus_NearTheSeam_SeesBothSidesOfTheStartLine()
        {
            Centerline line = Stadium();
            line.Close();
            line.SetFocus(2d);
            Assert.That(line.Project(new Vector3(0f, 0f, -3f)).S, Is.EqualTo(LapLength - 3d).Within(0.05));
            line.SetFocus(LapLength - 2d);
            Assert.That(line.Project(new Vector3(0f, 0f, 3f)).S, Is.EqualTo(3d).Within(0.05));
        }

        [Test]
        public void Focus_OnAnOpenTrack_RestrictsToo()
        {
            var line = new Centerline(Vector3.zero, 0f);
            line.AppendStraight(100f);
            line.AppendStraight(100f);
            line.SetFocus(150d);
            Assert.That(line.Project(new Vector3(0f, 0f, 120f)).S, Is.EqualTo(120d).Within(1e-3));
        }

        [Test]
        public void SetFocus_NaN_Throws()
        {
            Centerline line = Stadium();
            Assert.Throws<ArgumentOutOfRangeException>(() => line.SetFocus(double.NaN));
        }

        [Test]
        public void Project_OpenTrackWithoutFocus_KeepsTheOldBehaviour()
        {
            var line = new Centerline(new Vector3(10f, 0f, 5f), 90f);
            line.AppendStraight(50f);
            TrackCoord coord = line.Project(new Vector3(30f, 2f, 1f));
            Assert.That(coord.S, Is.EqualTo(20d).Within(1e-4));
            Assert.That(coord.D, Is.EqualTo(4f).Within(1e-4f));
        }
    }
}
