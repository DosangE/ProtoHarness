using NUnit.Framework;
using ProtoHarness.ChainRush.Track;
using ProtoHarness.ChainRush.Visuals;
using UnityEngine;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class RunnerTiltTests
    {
        private const float Tolerance = 1e-3f;

        [Test]
        public void Evaluate_AtRest_ReturnsIdentity()
        {
            Quaternion tilt = RunnerTilt.Evaluate(Vector3.zero, 0f);
            Assert.That(Quaternion.Angle(tilt, Quaternion.identity), Is.LessThan(Tolerance));
        }

        [Test]
        public void Evaluate_SteerLeft_YawsLeftAndRollsRight()
        {
            Quaternion tilt = RunnerTilt.Evaluate(Vector3.zero, -1f);
            Assert.That(Quaternion.Angle(tilt, Quaternion.Euler(0f, -12f, 16f)), Is.LessThan(Tolerance));
        }

        [Test]
        public void Evaluate_Rising_PitchesBack()
        {
            Quaternion tilt = RunnerTilt.Evaluate(new Vector3(3f, 10f, 12f), 0f);
            Assert.That(Quaternion.Angle(tilt, Quaternion.Euler(-9f, 0f, 0f)), Is.LessThan(Tolerance));
        }

        [Test]
        public void Evaluate_HorizontalVelocity_DoesNotTilt()
        {
            Quaternion tilt = RunnerTilt.Evaluate(new Vector3(7f, 0f, 16f), 0f);
            Assert.That(Quaternion.Angle(tilt, Quaternion.identity), Is.LessThan(Tolerance));
        }

        // ---- corner swing look (SwingLook) ----------------------------------------------------------

        [Test]
        public void SwingLook_RailPoint_RightHandArc_SitsOnTheInsideRailTop()
        {
            // Straight 10 m then an R30 right-hand quarter arc: the inside rail is 30 - 6 = 24 m from the center.
            var line = new Centerline(Vector3.zero, 0f);
            line.AppendStraight(10f);
            line.AppendArc(30f, 90f);
            TrackFrame frame = line.FrameAt(30d);
            Vector3 rail = SwingLook.RailPoint(frame, 6f);
            Vector3 center = frame.CurveCenter;
            Assert.That(Vector2.Distance(new Vector2(rail.x, rail.z), new Vector2(center.x, center.z)), Is.EqualTo(24f).Within(Tolerance));
            Assert.That(rail.y - frame.Position.y, Is.EqualTo(RoadMeshBuilder.GuardRailHeight).Within(Tolerance));
            Assert.That(Vector3.Dot(rail - frame.Position, frame.Right), Is.GreaterThan(0f), "A right-hand arc's inside is on the right.");
        }

        [Test]
        public void SwingLook_RailPoint_LeftHandArc_IsOnTheLeft()
        {
            var line = new Centerline(Vector3.zero, 0f);
            line.AppendStraight(10f);
            line.AppendArc(30f, -90f);
            TrackFrame frame = line.FrameAt(30d);
            Vector3 rail = SwingLook.RailPoint(frame, 6f);
            Assert.That(Vector3.Dot(rail - frame.Position, frame.Right), Is.EqualTo(-6f).Within(Tolerance));
        }

        [Test]
        public void SwingLook_RailPoint_Straight_Throws()
        {
            var line = new Centerline(Vector3.zero, 0f);
            line.AppendStraight(10f);
            Assert.Throws<System.ArgumentException>(() => SwingLook.RailPoint(line.FrameAt(5d), 6f));
        }

        [Test]
        public void SwingLook_InsideSign_FollowsTheCenterSide()
        {
            // Heading 0 faces +z, so +x is right.
            Assert.That(SwingLook.InsideSign(Vector3.zero, 0f, new Vector3(30f, 0f, 5f)), Is.EqualTo(1f));
            Assert.That(SwingLook.InsideSign(Vector3.zero, 0f, new Vector3(-30f, 0f, 5f)), Is.EqualTo(-1f));
            // Heading 90 faces +x, so -z is right.
            Assert.That(SwingLook.InsideSign(Vector3.zero, 90f, new Vector3(0f, 0f, -30f)), Is.EqualTo(1f));
        }

        [Test]
        public void SwingLook_Blend_ReachesFullAndBackOverBlendTime()
        {
            float half = SwingLook.Blend(0f, true, SwingLook.BlendTime * 0.5f);
            Assert.That(half, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(SwingLook.Blend(half, true, SwingLook.BlendTime), Is.EqualTo(1f));
            Assert.That(SwingLook.Blend(1f, false, SwingLook.BlendTime), Is.EqualTo(0f));
        }
    }
}
