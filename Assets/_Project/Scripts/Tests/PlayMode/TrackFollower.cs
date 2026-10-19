using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Track;
using UnityEngine;

namespace ProtoHarness.Tests.PlayMode
{
    // A test bot that steers toward a centerline: feed-forward for the curve, plus heading and offset
    // correction. Steering off sends Override instead. SwingHold is held as set; SwingPress and ChainAction fire once.
    internal sealed class TrackFollower : IInputSource
    {
        private readonly Centerline line;
        private readonly RunnerMotor runner;
        public bool Steering = true;
        public float Override;
        public bool ChainAction;
        // The swing key: a press hooks a corner pole in reach, and the swing lasts while it is held.
        public bool SwingPress;
        public bool SwingHold;

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
            bool swingPress = SwingPress;
            SwingPress = false;
            if (!Steering) return new TickInput(Override, false, false, false, SwingHold, chainAction, swingPress);
            Vector3 position = runner.transform.position;
            TrackFrame frame = line.Frame(position);
            float offset = line.Project(position).D;
            float trackYaw = Mathf.Atan2(frame.Forward.x, frame.Forward.z) * Mathf.Rad2Deg;
            float wanted = trackYaw - Mathf.Clamp(offset * 4f, -20f, 20f);
            // The feed-forward converts the needed turn rate into input against the runner's real limit now (2026-10-19:
            // with a minimum turn radius that is speed / radius, well under maxTurnRate at circuit speed).
            float turnLimit = runner.TurnLimit;
            float feedForward = turnLimit > 0f ? runner.ForwardSpeed * frame.Curvature * Mathf.Rad2Deg / turnLimit : 0f;
            float steer = feedForward + Mathf.DeltaAngle(runner.Heading, wanted) / 20f;
            return new TickInput(Mathf.Clamp(steer, -1f, 1f), false, false, false, SwingHold, chainAction, swingPress);
        }
    }
}
