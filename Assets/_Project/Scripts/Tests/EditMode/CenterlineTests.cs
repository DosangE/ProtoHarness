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
    }
}
