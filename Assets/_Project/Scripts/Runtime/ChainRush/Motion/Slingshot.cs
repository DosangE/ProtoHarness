using UnityEngine;

namespace ProtoHarness.ChainRush.Motion
{
    // The chain slingshot booster (chain action when not grappling): the chain hooks a point Lead ahead along the facing
    // and pushes that way without turning the runner, then extra speed carries for a moment. Its countdown is
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
        // speed a swing or a boost pad left. The speed already gained then falls off like any coasting.
        public static void Cut(RacerState racer)
        {
            racer.SlingTicks = 0;
            racer.SwingCarrySpeed = 0f;
            racer.BoostZoneCarrySpeed = 0f;
        }

        // The hook point for presentation, Lead ahead along the runner's facing. The booster does not change the facing.
        public static Vector3 HookPoint(float heading, Vector3 position, in Settings settings) =>
            position + RunDrive.FacingOf(heading) * settings.Lead;

        // A booster's push toward its speed: PullAcceleration at rest, shrinking to nothing at the boost speed. Above it
        // the speed falls back like any overspeed. The boost speed is never zero (TopSpeed, CarrySpeed > the run speed).
        public static float BoostAcceleration(float speed, float boostSpeed, in Settings settings) =>
            speed < boostSpeed ? settings.PullAcceleration * (1f - speed / boostSpeed) : settings.OverspeedFalloff;

        // A running booster lifts the run target to its speed: CarrySpeed while carrying, TopSpeed while pulling. turnCost is
        // the share turning leaves (RunDrive.TurnCost): steering costs a booster speed too (user 2026-11-04).
        public static float RaiseTarget(float runTarget, bool pulling, bool carrying, float turnCost, in Settings settings)
        {
            if (carrying) runTarget = Mathf.Max(runTarget, settings.CarrySpeed * turnCost);
            if (pulling) runTarget = Mathf.Max(runTarget, settings.TopSpeed * turnCost);
            return runTarget;
        }
    }
}
