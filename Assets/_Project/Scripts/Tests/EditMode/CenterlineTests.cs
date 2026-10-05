using System;
using NUnit.Framework;
using ProtoHarness.ChainRush.Track;
using UnityEngine;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class CenterlineTests
    {
        private const float Tolerance = 1e-4f;

        private static Centerline StraightAlongZ(float length)
        {
            var line = new Centerline(Vector3.zero, 0f);
            line.AppendStraight(length);
            return line;
        }

        [Test]
        public void Project_AlongPlusZ_MatchesWorldAxesExactly()
        {
            Centerline line = StraightAlongZ(496f);
            TrackCoord coord = line.Project(new Vector3(-2.75f, 1.3f, 123.456f));
            Assert.That(coord.S, Is.EqualTo((double)123.456f));
            Assert.That(coord.D, Is.EqualTo(-2.75f));
            Assert.That(coord.H, Is.EqualTo(1.3f));
        }

        [Test]
        public void Frame_AlongPlusZ_HasWorldAxesExactly()
        {
            TrackFrame frame = StraightAlongZ(100f).Frame(new Vector3(3f, 2f, 40f));
            Assert.That(frame.Forward, Is.EqualTo(Vector3.forward));
            Assert.That(frame.Right, Is.EqualTo(Vector3.right));
            Assert.That(frame.Position, Is.EqualTo(new Vector3(0f, 0f, 40f)));
            Assert.That(frame.S, Is.EqualTo(40d));
        }

        [Test]
        public void TransformDirection_AlongPlusZ_ReturnsSameVector()
        {
            TrackFrame frame = StraightAlongZ(100f).FrameAt(10d);
            var local = new Vector3(0.3f, -7.1f, 12.9f);
            Assert.That(frame.TransformDirection(local), Is.EqualTo(local));
            Assert.That(frame.InverseTransformDirection(local), Is.EqualTo(local));
        }

        [Test]
        public void Project_YawNinety_ForwardIsPlusXAndRightIsMinusZ()
        {
            var line = new Centerline(new Vector3(10f, 0f, 5f), 90f);
            line.AppendStraight(50f);
            TrackCoord coord = line.Project(new Vector3(30f, 2f, 1f));
            Assert.That(coord.S, Is.EqualTo(20d).Within(Tolerance));
            Assert.That(coord.D, Is.EqualTo(4f).Within(Tolerance));
            Assert.That(coord.H, Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void TransformPoint_YawNinety_RoundTripsThroughInverse()
        {
            var line = new Centerline(new Vector3(10f, 0f, 5f), 90f);
            line.AppendStraight(50f);
            TrackFrame frame = line.FrameAt(12d);
            var local = new Vector3(-1.5f, 2.5f, 3f);
            Vector3 world = frame.TransformPoint(local);
            Assert.That(Vector3.Distance(frame.InverseTransformPoint(world), local), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(world, new Vector3(25f, 2.5f, 6.5f)), Is.LessThan(Tolerance));
        }

        [Test]
        public void Project_BeforeStartAndPastEnd_ExtendsAlongEndTangents()
        {
            Centerline line = StraightAlongZ(56f);
            line.AppendStraight(56f);
            Assert.That(line.Project(new Vector3(0f, 0f, -8f)).S, Is.EqualTo(-8d).Within(Tolerance));
            Assert.That(line.Project(new Vector3(0f, 0f, 130f)).S, Is.EqualTo(130d).Within(Tolerance));
        }

        [Test]
        public void AppendStraight_SecondPiece_ContinuesFromFirstEnd()
        {
            Centerline line = StraightAlongZ(56f);
            line.AppendStraight(56f);
            Assert.That(line.PieceCount, Is.EqualTo(2));
            Assert.That(line.EndS, Is.EqualTo(112d));
            TrackFrame frame = line.FrameAt(84d);
            Assert.That(frame.Position, Is.EqualTo(new Vector3(0f, 0f, 84f)));
            Assert.That(line.Project(new Vector3(0f, 0f, 84f)).S, Is.EqualTo(84d).Within(Tolerance));
        }

        [Test]
        public void TrimBefore_PassedPieces_KeepsSAndDropsThem()
        {
            Centerline line = StraightAlongZ(56f);
            line.AppendStraight(56f);
            line.AppendStraight(56f);
            line.TrimBefore(120d);
            Assert.That(line.PieceCount, Is.EqualTo(1));
            Assert.That(line.StartS, Is.EqualTo(112d));
            Assert.That(line.Project(new Vector3(0f, 0f, 150f)).S, Is.EqualTo(150d).Within(Tolerance));
        }

        [Test]
        public void TrimBefore_PastEverything_KeepsLastPiece()
        {
            Centerline line = StraightAlongZ(56f);
            line.AppendStraight(56f);
            line.TrimBefore(1000d);
            Assert.That(line.PieceCount, Is.EqualTo(1));
        }

        [Test]
        public void ShiftOrigin_MovedWorldPoint_KeepsSameS()
        {
            Centerline line = StraightAlongZ(56f);
            for (int i = 0; i < 8; i++) line.AppendStraight(56f);
            var point = new Vector3(1f, 0.5f, 450f);
            double before = line.Project(point).S;
            var shift = new Vector3(0f, 0f, -448f);
            line.ShiftOrigin(shift);
            line.AppendStraight(56f);
            Assert.That(line.Project(point + shift).S, Is.EqualTo(before).Within(Tolerance));
            Assert.That(line.EndS, Is.EqualTo(560d));
            Assert.That(line.FrameAt(line.EndS).Position.z, Is.EqualTo(560f - 448f).Within(Tolerance));
        }

        [Test]
        public void Clear_AfterShift_RestartsAtConstructionOrigin()
        {
            Centerline line = StraightAlongZ(56f);
            line.ShiftOrigin(new Vector3(0f, 0f, -448f));
            line.Clear();
            line.AppendStraight(56f);
            Assert.That(line.StartS, Is.EqualTo(0d));
            Assert.That(line.Project(new Vector3(0f, 0f, 10f)).S, Is.EqualTo(10d));
        }

        [Test]
        public void Project_NoPieces_Throws()
        {
            var line = new Centerline(Vector3.zero, 0f);
            Assert.Throws<InvalidOperationException>(() => line.Project(Vector3.zero));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void AppendStraight_InvalidLength_Throws(float length)
        {
            var line = new Centerline(Vector3.zero, 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => line.AppendStraight(length));
        }

        [Test]
        public void Project_NaNPosition_Throws()
        {
            Centerline line = StraightAlongZ(10f);
            Assert.Throws<ArgumentOutOfRangeException>(() => line.Project(new Vector3(float.NaN, 0f, 0f)));
        }

        [Test]
        public void FrameAt_NaN_Throws()
        {
            Centerline line = StraightAlongZ(10f);
            Assert.Throws<ArgumentOutOfRangeException>(() => line.FrameAt(double.NaN));
        }

        [Test]
        public void FrameAt_FlatLine_KeepsStartHeightExactly()
        {
            var line = new Centerline(new Vector3(0f, 1.25f, 0f), 0f);
            line.AppendStraight(56f);
            line.AppendStraight(56f);
            TrackFrame frame = line.FrameAt(80d);
            Assert.That(frame.Position.y, Is.EqualTo(1.25f));
            Assert.That(frame.Grade, Is.EqualTo(0f));
            Assert.That(line.Project(new Vector3(0f, 3f, 80f)).H, Is.EqualTo(3f - 1.25f));
        }

        [Test]
        public void FrameAt_VerticalCurve_FollowsParabola()
        {
            Centerline line = StraightAlongZ(40f, 0.1f);
            TrackFrame middle = line.FrameAt(20d);
            Assert.That(middle.Position.y, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(middle.Grade, Is.EqualTo(0.05f).Within(Tolerance));
            TrackFrame end = line.FrameAt(40d);
            Assert.That(end.Position.y, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(end.Grade, Is.EqualTo(0.1f).Within(Tolerance));
        }

        [Test]
        public void AppendStraight_AfterCurve_KeepsGradeAndHeightContinuous()
        {
            Centerline line = StraightAlongZ(40f, 0.1f);
            line.AppendStraight(20f);
            Assert.That(line.EndGrade, Is.EqualTo(0.1f));
            Assert.That(line.FrameAt(40d).Position.y, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(line.FrameAt(50d).Position.y, Is.EqualTo(3f).Within(Tolerance));
            Assert.That(line.FrameAt(50d).Grade, Is.EqualTo(0.1f).Within(Tolerance));
            Assert.That(line.FrameAt(60d).Position.y, Is.EqualTo(4f).Within(Tolerance));
        }

        [Test]
        public void FrameAt_PastEnd_ExtendsAlongEndGrade()
        {
            Centerline line = StraightAlongZ(40f, -0.2f);
            TrackFrame frame = line.FrameAt(50d);
            Assert.That(frame.Position.y, Is.EqualTo(-4f - 2f).Within(Tolerance));
            Assert.That(frame.Grade, Is.EqualTo(-0.2f).Within(Tolerance));
        }

        [Test]
        public void Project_OnSlope_HeightIsAboveCenterline()
        {
            Centerline line = StraightAlongZ(40f, 0.1f);
            TrackCoord coord = line.Project(new Vector3(0.5f, 1.5f, 20f));
            Assert.That(coord.S, Is.EqualTo(20d).Within(Tolerance));
            Assert.That(coord.H, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Clear_AfterSlope_RestartsFlat()
        {
            Centerline line = StraightAlongZ(40f, 0.2f);
            line.Clear();
            line.AppendStraight(10f);
            Assert.That(line.EndGrade, Is.EqualTo(0f));
            Assert.That(line.FrameAt(10d).Position.y, Is.EqualTo(0f));
        }

        [TestCase(1.01f)]
        [TestCase(-1.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.NegativeInfinity)]
        public void AppendStraight_InvalidGrade_Throws(float grade)
        {
            var line = new Centerline(Vector3.zero, 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => line.AppendStraight(10f, grade));
        }

        private static readonly float QuarterR30 = 30f * Mathf.PI / 2f;

        private static Centerline ArcR30(float degrees)
        {
            var line = new Centerline(Vector3.zero, 0f);
            line.AppendArc(30f, degrees);
            return line;
        }

        [Test]
        public void AppendArc_RightQuarterR30_EndsTurnedRight()
        {
            Centerline line = ArcR30(90f);
            Assert.That(line.EndS, Is.EqualTo((double)QuarterR30).Within(Tolerance));
            TrackFrame end = line.FrameAt(line.EndS);
            Assert.That(Vector3.Distance(end.Position, new Vector3(30f, 0f, 30f)), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(end.Forward, Vector3.right), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(end.Right, Vector3.back), Is.LessThan(Tolerance));
        }

        [Test]
        public void FrameAt_MidArc_LiesOnCircleWithCurvature()
        {
            TrackFrame middle = ArcR30(90f).FrameAt(QuarterR30 / 2f);
            float half = 30f * Mathf.Sqrt(0.5f);
            Assert.That(Vector3.Distance(middle.Position, new Vector3(30f - half, 0f, half)), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(middle.Forward, new Vector3(Mathf.Sqrt(0.5f), 0f, Mathf.Sqrt(0.5f))), Is.LessThan(Tolerance));
            Assert.That(middle.Curvature, Is.EqualTo(1f / 30f).Within(Tolerance));
        }

        [Test]
        public void AppendArc_LeftQuarter_MirrorsRight()
        {
            Centerline line = ArcR30(-90f);
            TrackFrame end = line.FrameAt(line.EndS);
            Assert.That(Vector3.Distance(end.Position, new Vector3(-30f, 0f, 30f)), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(end.Forward, Vector3.left), Is.LessThan(Tolerance));
            Assert.That(line.FrameAt(10d).Curvature, Is.EqualTo(-1f / 30f).Within(Tolerance));
        }

        [TestCase(90f, 2f)]
        [TestCase(90f, -2f)]
        [TestCase(-90f, 2f)]
        [TestCase(-90f, -2f)]
        public void Project_OnArc_RoundTripsThroughFrame(float degrees, float right)
        {
            Centerline line = ArcR30(degrees);
            double s = QuarterR30 * 0.3f;
            Vector3 world = line.FrameAt(s).TransformPoint(new Vector3(right, 1.5f, 0f));
            TrackCoord coord = line.Project(world);
            Assert.That(coord.S, Is.EqualTo(s).Within(Tolerance));
            Assert.That(coord.D, Is.EqualTo(right).Within(Tolerance));
            Assert.That(coord.H, Is.EqualTo(1.5f).Within(Tolerance));
        }

        [Test]
        public void Chain_StraightArcStraight_StaysContinuous()
        {
            var line = new Centerline(Vector3.zero, 0f);
            line.AppendStraight(20f);
            line.AppendArc(30f, 90f);
            line.AppendStraight(40f);
            double exitS = 20d + QuarterR30;
            Assert.That(Vector3.Distance(line.FrameAt(exitS).Position, new Vector3(30f, 0f, 50f)), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(line.FrameAt(exitS + 10d).Position, new Vector3(40f, 0f, 50f)), Is.LessThan(Tolerance));
            Assert.That(line.FrameAt(exitS + 10d).Curvature, Is.Zero);
            Assert.That(line.Project(new Vector3(40f, 0f, 49f)).S, Is.EqualTo(exitS + 10d).Within(Tolerance));
            Assert.That(line.Project(new Vector3(40f, 0f, 49f)).D, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Project_PastArcEnd_ExtendsAlongEndTangent()
        {
            Centerline line = ArcR30(90f);
            TrackCoord coord = line.Project(new Vector3(35f, 0f, 29f));
            Assert.That(coord.S, Is.EqualTo(QuarterR30 + 5d).Within(Tolerance));
            Assert.That(coord.D, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void AppendArc_WithGrade_RisesAlongArcLength()
        {
            var line = new Centerline(Vector3.zero, 0f);
            line.AppendArc(30f, 90f, 0.1f);
            Assert.That(line.FrameAt(line.EndS).Position.y, Is.EqualTo(0.05f * QuarterR30).Within(Tolerance));
        }

        [Test]
        public void ShiftOrigin_OnArc_KeepsSameS()
        {
            Centerline line = ArcR30(90f);
            var point = new Vector3(10f, 0f, 20f);
            double before = line.Project(point).S;
            var shift = new Vector3(-448f, 0f, 12f);
            line.ShiftOrigin(shift);
            Assert.That(line.Project(point + shift).S, Is.EqualTo(before).Within(Tolerance));
        }

        [Test]
        public void CurveCenter_OnArcs_IsTheCircleCenter()
        {
            Assert.That(Vector3.Distance(ArcR30(90f).FrameAt(10d).CurveCenter, new Vector3(30f, 0f, 0f)), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(ArcR30(90f).FrameAt(40d).CurveCenter, new Vector3(30f, 0f, 0f)), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(ArcR30(-90f).FrameAt(25d).CurveCenter, new Vector3(-30f, 0f, 0f)), Is.LessThan(Tolerance));
        }

        [Test]
        public void CurveCenter_OnStraight_Throws()
        {
            TrackFrame frame = StraightAlongZ(10f).FrameAt(5d);
            Assert.Throws<InvalidOperationException>(() => { Vector3 unused = frame.CurveCenter; });
        }

        [TestCase(0f, 90f)]
        [TestCase(-5f, 90f)]
        [TestCase(float.NaN, 90f)]
        [TestCase(30f, 0f)]
        [TestCase(30f, 181f)]
        [TestCase(30f, float.NaN)]
        public void AppendArc_InvalidRadiusOrTurn_Throws(float radius, float degrees)
        {
            var line = new Centerline(Vector3.zero, 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => line.AppendArc(radius, degrees));
        }

        private static Centerline StraightAlongZ(float length, float toGrade)
        {
            var line = new Centerline(Vector3.zero, 0f);
            line.AppendStraight(length, toGrade);
            return line;
        }
    }
}
