using UnityEngine;

namespace ProtoHarness.ChainRush.Motion
{
    // The corner pole swing, the drift of this game (NEXT_REQUESTS.md R4b): the runner circles a pole for as long as the
    // swing key is held, with no running force. Steering works the rope instead of the facing: the key toward the pole
    // shortens it, the other lets it out, by RopeRate a second, never below MinRopeLength. The speed is the hook's speed
    // times the hooked rope length over the rope length now, kept within MinSpeedScale .. MaxSpeedScale of the hook's
    // speed. Every MetersPerSlot swung fills one gauge slot. The swing's state is RacerState's Swing* values.
    public static class PoleSwing
    {
        // The values the rules run on (RunnerMotor's, the speeds already scaled by the runner's profile). Treat it as
        // read-only.
        public struct Settings
        {
            public float RopeRate;
            public float MinRopeLength;
            public float MinSpeedScale;
            public float MaxSpeedScale;
            public float ThrottleSpeedScale;
            public float MetersPerSlot;
            public float RunSpeed;
            public float RunAcceleration;
        }

        // Hooks the pole at the rope length and speed of this moment.
        public static void Hook(RacerState racer, Vector3 position, Vector3 pole)
        {
            Vector3 radial = position - pole;
            radial.y = 0f;
            Vector3 velocity = racer.Velocity;
            velocity.y = 0f;
            racer.SwingCenter = pole;
            racer.SwingRadius = radial.magnitude;
            racer.SwingHookRadius = radial.magnitude;
            racer.SwingHookSpeed = velocity.magnitude;
            racer.SwingCarrySpeed = 0f;
            racer.SwingTicks = 1;
        }

        public static void SpeedUp(RacerState racer, bool swinging, bool coasting, bool pulling, bool carrying, float dt,
            in Settings settings, in Slingshot.Settings sling)
        {
            if (!swinging) return;
            ref float hookSpeed = ref racer.SwingHookSpeed;
            // A booster on a swing speeds the swing up (user 2026-10-19, "C"): the hook's speed, which the rope's ratio
            // multiplies (Step), rises toward the booster's speed by the same push a booster gives on the ground.
            // The rope keeps the facing, so the slingshot's pull does not turn it.
            if (pulling || carrying)
            {
                float boostSpeed = pulling ? sling.TopSpeed : sling.CarrySpeed;
                if (hookSpeed < boostSpeed) hookSpeed = Mathf.Min(boostSpeed, hookSpeed + Slingshot.BoostAcceleration(hookSpeed, boostSpeed, sling) * dt);
            }
            // Without a booster the throttle speeds the swing up too (user 2026-10-22), by the run acceleration and up to
            // ThrottleSpeedScale times the run speed. A faster hook (a booster's) is kept; let go, the speed stays.
            else if (!coasting)
            {
                float throttleSpeed = settings.RunSpeed * settings.ThrottleSpeedScale;
                if (hookSpeed < throttleSpeed) hookSpeed = Mathf.Min(throttleSpeed, hookSpeed + settings.RunAcceleration * dt);
            }
        }

        // Circles the pole: heading and velocity turn onto the tangent on the side the runner already faces, steering
        // works the rope, the speed follows the rope, the step is pulled onto the circle, and the distance swung fills
        // the gauge. anchor is where presentation draws the rope's end.
        public static void Step(RacerState racer, Vector3 position, ref Vector3 velocity, ref float heading, ref Vector3 displacement,
            float dt, in Settings settings, out Vector3 anchor)
        {
            Vector3 center = racer.SwingCenter;
            center.y = position.y;
            anchor = center + Vector3.up * 0.5f;
            Vector3 outward = position - center;
            float distance = outward.magnitude;
            if (distance < 0.01f) return;
            outward /= distance;
            Vector3 tangent = Vector3.Cross(Vector3.up, outward);
            if (Vector3.Dot(tangent, RunDrive.FacingOf(heading)) < 0f) tangent = -tangent;
            heading = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
            // +1 when the pole is on the right of the new facing (a right-hand corner, where D is the key toward it).
            float poleSide = Vector3.Dot(-outward, RunDrive.RightOf(heading)) >= 0f ? 1f : -1f;
            racer.SwingRadius = Mathf.Max(settings.MinRopeLength, racer.SwingRadius - racer.Steer * poleSide * settings.RopeRate * dt);
            float scale = Mathf.Clamp(racer.SwingHookRadius / racer.SwingRadius, settings.MinSpeedScale, settings.MaxSpeedScale);
            float speed = racer.SwingHookSpeed * scale;
            velocity = tangent * speed + Vector3.up * velocity.y;
            displacement = velocity * dt;
            Vector3 next = position + displacement - center;
            next.y = 0f;
            float reach = next.magnitude;
            if (reach > 0.01f) displacement += next / reach * (racer.SwingRadius - reach);
            racer.AddGauge(speed * dt / settings.MetersPerSlot);
        }

        // Ends the swing: the runner goes straight on along its facing at the speed it swung with, and what that is above
        // running speed falls off by RunDrive.Settings.SwingCarryFalloff a second.
        public static void End(RacerState racer, Vector3 velocity, float heading)
        {
            racer.SwingTicks = 0;
            racer.SwingCarrySpeed = Mathf.Max(0f, Vector3.Dot(velocity, RunDrive.FacingOf(heading)));
        }
    }
}
