using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Track;
using UnityEngine;

namespace ProtoHarness.Tests.PlayMode
{
    // A test bot that steers toward a centerline: feed-forward for the curve, plus heading and offset
    // correction. Steering off sends Override instead. Drift is held as set; ChainAction fires once.
    internal sealed class TrackFollower : IInputSource
    {
        // RunnerMotor defaults (T2a): the feed-forward converts the needed turn rate into input.
        private const float MaxTurnRate = 120f;
        private readonly Centerline line;
        private readonly RunnerMotor runner;
        public bool Steering = true;
        public float Override;
        public bool Drift;
        public bool ChainAction;

        public TrackFollower(Centerline line, RunnerMotor runner)
        {
            this.line = line;
            this.runner = runner;
        }

        public void Poll() { }
        public void Clear() { }

        public TickInput Consume()
        {
            bool chainAction = ChainAction;
            ChainAction = false;
            if (!Steering) return new TickInput(Override, false, false, false, Drift, chainAction);
            Vector3 position = runner.transform.position;
            TrackFrame frame = line.Frame(position);
            float offset = line.Project(position).D;
            float trackYaw = Mathf.Atan2(frame.Forward.x, frame.Forward.z) * Mathf.Rad2Deg;
            float wanted = trackYaw - Mathf.Clamp(offset * 4f, -20f, 20f);
            float feedForward = runner.ForwardSpeed * frame.Curvature * Mathf.Rad2Deg / MaxTurnRate;
            float steer = feedForward + Mathf.DeltaAngle(runner.Heading, wanted) / 20f;
            return new TickInput(Mathf.Clamp(steer, -1f, 1f), false, false, false, Drift, chainAction);
        }
    }
}
