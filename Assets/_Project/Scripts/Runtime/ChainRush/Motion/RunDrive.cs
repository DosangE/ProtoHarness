using UnityEngine;

namespace ProtoHarness.ChainRush.Motion
{
    // Running rules (free steering, COURSE.md 4-3): the turn, the forward speed the runner heads for, how fast it gets
    // there, and gravity. The state they work on lives in RacerState, so a rollback restores it; RunnerMotor fills
    // Settings from its serialized values every tick, so a value changed in the inspector or by a test applies at once.
    public static class RunDrive
    {
        // Forward acceleration while hanging from a grapple in the air.
        private const float GrappleAirAcceleration = 5f;
        private const float MaxFallSpeed = 28f;
        // A carried speed (a swing let go, a boost pad) ends once it is this close to the running speed (its falloff only
        // nears it).
        private const float CarryEnd = 0.5f;

        // The values the rules run on (RunnerMotor's, the speeds already scaled by the runner's profile and the speed
        // class). Treat it as read-only.
        public struct Settings
        {
            public float RunSpeed;
            // How fast a grapple reels the runner in, unless running is faster.
            public float GrappleReelSpeed;
            public float RunAcceleration;
            public float MaxTurnRate;
            public float TurnAcceleration;
            public float AirTurnScale;
            public float MinTurnRadius;
            public float TurnSpeedLoss;
            public float CoastDeceleration;
            public float BrakeDeceleration;
            public float ReverseSpeed;
            public float ReverseAcceleration;
            public float AirDeceleration;
            public float SideGrip;
            public float AirSideGrip;
            public float SlopeSpeedFactor;
            public float MinSlopeSpeedScale;
            public float MaxSlopeSpeedScale;
            public float OverspeedFalloff;
            // The share of a swing's let-go speed, and of a boost pad's, above running speed lost each second on the ground.
            public float SwingCarryFalloff;
            public float BoostZoneFalloff;
            public float Gravity;
        }

        // What holds for the runner this tick, as Step worked it out before driving.
        public struct Conditions
        {
            public bool Grounded;
            public bool Swinging;
            public bool Coasting;
            public bool Reversing;
            public bool Pulling;
            public bool Carrying;
            public bool GrappleAttached;
        }

        public static Vector3 FacingOf(float heading)
        {
            float radians = heading * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }

        public static Vector3 RightOf(float heading)
        {
            float radians = heading * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), 0f, -Mathf.Sin(radians));
        }

        // The ground turn rate full steering reaches at this velocity, in degrees a second: MaxTurnRate, held to
        // speed / MinTurnRadius when that is set.
        public static float TurnLimit(Vector3 velocity, in Settings settings) => settings.MinTurnRadius > 0f
            ? Mathf.Min(settings.MaxTurnRate, new Vector2(velocity.x, velocity.z).magnitude / settings.MinTurnRadius * Mathf.Rad2Deg)
            : settings.MaxTurnRate;

        // Input turns the facing, and running and grip act along the facing. The track only supplies the grade; it no
        // longer decides which way is ahead. Returns the turn rate limit at the current speed.
        public static float Turn(RacerState racer, bool grounded, bool swinging, bool reversing, float dt, in Settings settings)
        {
            ref float turnRate = ref racer.TurnRate;
            ref float heading = ref racer.Heading;
            float turnScale = grounded ? 1f : settings.AirTurnScale;
            // A swing ignores steering for the facing (steering works the rope there): the rope sets the facing
            // (PoleSwing.Step), and turning the facing first would split the velocity across it and let the side grip eat
            // the swing's speed.
            float turnLimit = TurnLimit(racer.Velocity, settings);
            // Backing up, the facing turns the other way, so the key still says which way the runner goes. Only while the
            // down arrow is held: a runner turned round against its velocity some other way steers as it always did.
            float steerSign = reversing && Vector3.Dot(racer.Velocity, FacingOf(heading)) < 0f ? -1f : 1f;
            turnRate = swinging ? 0f : Mathf.MoveTowards(turnRate, steerSign * racer.Steer * turnLimit * turnScale, settings.TurnAcceleration * dt);
            heading = Mathf.Repeat(heading + turnRate * dt + 180f, 360f) - 180f;
            return turnLimit;
        }

        // The running force: grip takes out the side speed, the forward speed moves toward the target at the chosen
        // acceleration, and gravity pulls the vertical speed down. grade is the track's under the runner.
        public static void Drive(RacerState racer, float grade, in Conditions now, float turnLimit, float dt, in Settings settings,
            in Slingshot.Settings sling)
        {
            ref Vector3 velocity = ref racer.Velocity;
            Vector3 facing = FacingOf(racer.Heading);
            Vector3 right = RightOf(racer.Heading);
            float grip = now.Grounded ? settings.SideGrip : settings.AirSideGrip;
            float sideSpeed = Mathf.MoveTowards(Vector3.Dot(velocity, right), 0f, grip * dt);
            float baseTarget = RunTarget(racer, grade, now, turnLimit, dt, settings);
            float carried = Mathf.Max(CarryTarget(ref racer.SwingCarrySpeed, baseTarget, settings.SwingCarryFalloff, dt),
                CarryTarget(ref racer.BoostZoneCarrySpeed, baseTarget, settings.BoostZoneFalloff, dt));
            float runTarget = Slingshot.RaiseTarget(carried, now.Pulling, now.Carrying, sling);
            float currentForward = Vector3.Dot(velocity, facing);
            float acceleration = ForwardAcceleration(currentForward, runTarget, now, settings, sling);
            // A carried speed wears off in proportion to what is left above running speed (user 2026-10-30, the inverse of
            // the slingshot's pull; a pad first, then a swing let go): hard at first, easing as it nears running speed. In
            // the air it keeps, so a jump flies on the speed it left with.
            float falloff = Mathf.Max(racer.SwingCarrySpeed > 0f ? settings.SwingCarryFalloff : 0f,
                racer.BoostZoneCarrySpeed > 0f ? settings.BoostZoneFalloff : 0f);
            if (falloff > 0f && now.Grounded && !now.Swinging && !now.GrappleAttached && currentForward > runTarget)
                acceleration = Mathf.Max(acceleration, falloff * (currentForward - baseTarget));
            // A grapple reels at GrappleReelSpeed, or at running speed when that is faster.
            float forwardSpeed = Mathf.MoveTowards(currentForward, now.GrappleAttached ? Mathf.Max(settings.GrappleReelSpeed, settings.RunSpeed) : runTarget,
                acceleration * dt);
            velocity = facing * forwardSpeed + right * sideSpeed + Vector3.up * velocity.y;
            velocity.y = Mathf.Max(velocity.y - settings.Gravity * dt, -MaxFallSpeed);
        }

        // The forward speed the runner heads for this tick before a booster raises it (Slingshot.RaiseTarget).
        private static float RunTarget(RacerState racer, float grade, in Conditions now, float turnLimit, float dt, in Settings settings)
        {
            // In the air too the road below sets the target, so jumping off an uphill does not reach for the flat speed.
            float runTarget = settings.RunSpeed * Mathf.Clamp(1f - settings.SlopeSpeedFactor * grade, settings.MinSlopeSpeedScale, settings.MaxSlopeSpeedScale);
            if (settings.TurnSpeedLoss > 0f && now.Grounded && !now.Swinging && turnLimit > 0f)
                runTarget *= 1f - settings.TurnSpeedLoss * Mathf.Min(1f, Mathf.Abs(racer.TurnRate) / turnLimit);
            // Without the throttle a grounded runner rolls to a stop; in the air the jump keeps what it took off with.
            if (now.Coasting && now.Grounded) runTarget = now.Reversing ? -settings.ReverseSpeed : 0f;
            return runTarget;
        }

        // A carried speed (RacerState.SwingCarrySpeed, BoostZoneCarrySpeed) falls off by falloff of its part above
        // runTarget a second and holds the target up while it lasts; within CarryEnd of running speed it is gone.
        private static float CarryTarget(ref float carry, float runTarget, float falloff, float dt)
        {
            if (carry <= 0f) return runTarget;
            carry -= falloff * Mathf.Max(0f, carry - runTarget) * dt;
            if (carry > runTarget + CarryEnd) return carry;
            carry = 0f;
            return runTarget;
        }

        // How fast the forward speed moves toward the target this tick.
        private static float ForwardAcceleration(float currentForward, float runTarget, in Conditions now, in Settings settings,
            in Slingshot.Settings sling)
        {
            // In the air the throttle speeds the runner up as on the ground, never past the target; let go, the speed stays.
            float acceleration = now.Pulling ? Slingshot.BoostAcceleration(currentForward, runTarget, sling)
                : !now.Grounded && now.GrappleAttached ? GrappleAirAcceleration
                : !now.Grounded && now.Coasting ? 0f
                // Backing up (grounded here): brake while still going forward, then speed up backward.
                : now.Reversing ? (currentForward > 0f ? settings.BrakeDeceleration : settings.ReverseAcceleration)
                : currentForward > runTarget ? (!now.Grounded ? settings.AirDeceleration : now.Coasting ? settings.CoastDeceleration : settings.OverspeedFalloff)
                : now.Carrying ? Slingshot.BoostAcceleration(currentForward, runTarget, sling) : settings.RunAcceleration;
            // The swing sets its own speed (PoleSwing.Step): no running force acts on it.
            return now.Swinging ? 0f : acceleration;
        }
    }
}
