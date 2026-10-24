using UnityEngine;
using ProtoHarness.ChainRush.Track;

namespace ProtoHarness.ChainRush.Motion
{
    // The chain slingshot booster (chain action when not grappling): the chain hooks a point Lead ahead on the centerline
    // and pulls hard, turning the runner toward it, then extra speed carries for a moment. Its countdown is
    // RacerState.SlingTicks: above the carry ticks it pulls, at or below them it carries.
    public static class Slingshot
    {
        // The values the rules run on (RunnerMotor's, the speeds already scaled by the runner's profile). Treat it as
        // read-only.
        public struct Settings
        {
            public float Lead;
            public float PullTime;
            public float PullAcceleration;
            public float TopSpeed;
            public float TurnRate;
            public float CarryTime;
            public float CarrySpeed;
            public float OverspeedFalloff;
        }

        public static bool IsPulling(int slingTicks, in Settings settings) => slingTicks > Ticks.FromSeconds(settings.CarryTime);

        // Fires the slingshot, which needs the throttle (KartRider style, 2026-10-19): without it, or with one already
        // running, or without a whole gauge slot, nothing happens and nothing is spent.
        public static bool TryFire(RacerState racer, bool coasting, in Settings settings)
        {
            if (coasting || racer.SlingTicks > 0 || !racer.TrySpendGaugeSlot()) return false;
            racer.SlingTicks = Ticks.FromSeconds(settings.PullTime) + Ticks.FromSeconds(settings.CarryTime);
            return true;
        }

        // Letting go of the throttle cuts a booster at once, as in KartRider: the slingshot's pull and carry and the
        // speed a swing let go with. The speed already gained then falls off like any coasting.
        public static void Cut(RacerState racer)
        {
            racer.SlingTicks = 0;
            racer.SwingCarrySpeed = 0f;
        }

        // While the slingshot pulls, the facing turns toward the hook point, which rides the centerline ahead so the pull
        // follows curves. Returns the hook point for presentation.
        public static Vector3 SteerToward(RacerState racer, Centerline track, TrackFrame frame, Vector3 position, float dt, in Settings settings)
        {
            ref float heading = ref racer.Heading;
            Vector3 target = track.FrameAt(frame.S + settings.Lead).Position + Vector3.up * (position.y - frame.Position.y);
            Vector3 toward = target - position;
            toward.y = 0f;
            if (toward.sqrMagnitude > 0.01f)
                heading = Mathf.Repeat(Mathf.MoveTowardsAngle(heading, Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg, settings.TurnRate * dt) + 180f, 360f) - 180f;
            return target;
        }

        // A booster's push toward its speed: PullAcceleration at rest, shrinking to nothing at the boost speed. Above it
        // the speed falls back like any overspeed. The boost speed is never zero (TopSpeed, CarrySpeed > the run speed).
        public static float BoostAcceleration(float speed, float boostSpeed, in Settings settings) =>
            speed < boostSpeed ? settings.PullAcceleration * (1f - speed / boostSpeed) : settings.OverspeedFalloff;

        // A running booster lifts the run target to its speed: CarrySpeed while carrying, TopSpeed while pulling.
        public static float RaiseTarget(float runTarget, bool pulling, bool carrying, in Settings settings)
        {
            if (carrying) runTarget = Mathf.Max(runTarget, settings.CarrySpeed);
            if (pulling) runTarget = Mathf.Max(runTarget, settings.TopSpeed);
            return runTarget;
        }
    }
}
