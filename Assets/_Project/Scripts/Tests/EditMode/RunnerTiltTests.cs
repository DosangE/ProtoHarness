using NUnit.Framework;
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
    }
}
