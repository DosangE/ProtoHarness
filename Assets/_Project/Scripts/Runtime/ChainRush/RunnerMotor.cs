using UnityEngine;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Track;

namespace ProtoHarness.ChainRush
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class RunnerMotor : MonoBehaviour
    {
        [SerializeField] private CharacterController controller;
        [SerializeField] private ChainRushGame game;
        [SerializeField] private GrappleController grapple;
        [SerializeField] private float runSpeed = 10f;
        [SerializeField] private float jumpSpeed = 11.5f;
        [SerializeField] private float gravity = 22f;
        // Steering turns the runner (free steering, COURSE.md 4-3): input sets a target turn rate, the rate
        // follows it by turnAcceleration, and running follows the facing. Air control is scaled down.
        [SerializeField] private float maxTurnRate = 120f;
        [SerializeField] private float turnAcceleration = 1200f;
        [SerializeField] private float airTurnScale = 0.5f;
        // Velocity across the facing (from rope swings, wall glances) bleeds off at these rates.
        [SerializeField] private float sideGrip = 60f;
        [SerializeField] private float airSideGrip = 18f;
        // Share of speed lost in a head-on guard hit; glancing hits lose proportionally less.
        [SerializeField] private float guardSpeedLoss = 0.15f;
        // Grounded run speed scales by 1 - factor * grade, clamped: uphill slows, downhill speeds up.
        [SerializeField] private float slopeSpeedFactor = 1.5f;
        [SerializeField] private float minSlopeSpeedScale = 0.7f;
        [SerializeField] private float maxSlopeSpeedScale = 1.3f;
        // How far below the capsule a downhill road may drop in one tick and still be followed.
        [SerializeField] private float groundSnapDistance = 0.3f;
        // Drift (KartRider style, held on the ground): grip drops so turns slide, turning sharpens, run speed
        // eases, and sliding sideways fills the chain gauge (one slot per slideMetersPerSlot of slide).
        [SerializeField] private float driftGrip = 12f;
        [SerializeField] private float driftTurnScale = 1.3f;
        [SerializeField] private float driftSpeedScale = 0.9f;
        [SerializeField] private float slideMetersPerSlot = 6f;
        // Chain slingshot (chain action when not grappling): the chain hooks a point slingLead ahead on the
        // centerline and pulls hard, turning the runner toward it, then extra speed carries for a moment.
        [SerializeField] private float slingLead = 18f;
        [SerializeField] private float slingPullTime = 0.4f;
        [SerializeField] private float slingPullAcceleration = 45f;
        [SerializeField] private float slingTopSpeed = 20f;
        [SerializeField] private float slingTurnRate = 90f;
        [SerializeField] private float slingCarryTime = 0.8f;
        [SerializeField] private float slingCarrySpeed = 16f;
        // Empowered grapple (chain action while attached): the release throws harder along the facing.
        [SerializeField] private float empoweredReleaseSpeed = 18f;
        // Corner swing (chain action while drifting into a curve): the chain hooks the arc's center and the
        // runner circles it at its current line, speeding toward swingSpeed. Releasing drift, leaving the
        // arc or running out of time throws the runner along the tangent at swingExitSpeed or more, then
        // the slingshot's carry speed holds for swingExitCarryTime.
        [SerializeField] private float swingSpeed = 14f;
        [SerializeField] private float swingAcceleration = 20f;
        [SerializeField] private float swingMaxTime = 2f;
        [SerializeField] private float swingExitSpeed = 17f;
        [SerializeField] private float swingExitCarryTime = 0.6f;
        private Vector3 spawnPosition;
        // Scratch for one Move call: the most head-on side contact OnControllerColliderHit reported.
        private Vector3 guardNormal;
        private bool hitGuard;
        // Presentation reads these; the simulation recomputes them every tick.
        private bool drifting;
        private Vector3 slingTarget;
        private Vector3 swingAnchor;

        // Motion state lives in the racer's RacerState; the motor only integrates it.
        public Vector3 Velocity => game.Racer.Velocity;
        public float Steer => game.Racer.Steer;
        public float Heading => game.Racer.Heading;
        public Vector3 Facing => FacingOf(game.Racer.Heading);
        public float ForwardSpeed => Vector3.Dot(game.Racer.Velocity, Facing);
        public float SideSpeed => Vector3.Dot(game.Racer.Velocity, RightOf(game.Racer.Heading));
        public bool IsGrounded => game.Racer.Grounded;
        public float Speed => game.Racer.Velocity.magnitude;
        public bool IsDrifting => drifting;
        public float Gauge => game.Racer.Gauge;
        public bool IsSlingPulling => game.Racer.SlingTicks > Ticks.FromSeconds(slingCarryTime);
        public Vector3 SlingTarget => slingTarget;
        public bool IsSwinging => game.Racer.SwingTicks > 0;
        public Vector3 SwingAnchor => swingAnchor;

        private void Awake()
        {
            if (controller == null || game == null || grapple == null)
            {
                Debug.LogError("RunnerMotor: controller, game and grapple must be assigned.", this);
                enabled = false;
                return;
            }
            spawnPosition = transform.position;
        }

        private void OnValidate()
        {
            if (runSpeed <= 0f || jumpSpeed <= 0f || gravity <= 0f)
                Debug.LogError("RunnerMotor: movement settings must be positive.", this);
            if (maxTurnRate <= 0f || turnAcceleration <= 0f || airTurnScale < 0f || airTurnScale > 1f)
                Debug.LogError("RunnerMotor: turn rate and acceleration must be positive, air turn scale within [0, 1].", this);
            if (sideGrip < 0f || airSideGrip < 0f || guardSpeedLoss < 0f || guardSpeedLoss > 1f)
                Debug.LogError("RunnerMotor: grips cannot be negative, guard speed loss within [0, 1].", this);
            if (slopeSpeedFactor < 0f || minSlopeSpeedScale <= 0f || minSlopeSpeedScale > 1f || maxSlopeSpeedScale < 1f)
                Debug.LogError("RunnerMotor: slope factor cannot be negative, and the speed scale range must be within (0, 1] .. [1, inf).", this);
            if (groundSnapDistance < 0f)
                Debug.LogError("RunnerMotor: ground snap distance cannot be negative.", this);
            if (driftGrip < 0f || driftTurnScale <= 0f || driftSpeedScale <= 0f || driftSpeedScale > 1f || slideMetersPerSlot <= 0f)
                Debug.LogError("RunnerMotor: drift grip cannot be negative, turn scale and slide per slot must be positive, speed scale within (0, 1].", this);
            if (slingLead <= 0f || slingPullTime <= 0f || slingPullAcceleration <= 0f || slingTopSpeed <= runSpeed
                || slingTurnRate < 0f || slingCarryTime < 0f || slingCarrySpeed < runSpeed || empoweredReleaseSpeed < 13f)
                Debug.LogError("RunnerMotor: slingshot settings must be positive, its speeds above run speed, and the empowered release at least the normal 13.", this);
            if (swingSpeed <= runSpeed || swingAcceleration <= 0f || swingMaxTime <= 0f || swingExitSpeed < swingSpeed || swingExitCarryTime < 0f)
                Debug.LogError("RunnerMotor: swing speed must beat run speed, acceleration and time must be positive, exit speed at least the swing speed.", this);
        }

        public void PrimaryAction()
        {
            if (!game.IsRunning) return;
            RacerState racer = game.Racer;
            if (IsGrounded || racer.CoyoteTime > 0f) racer.JumpQueued = true;
            else grapple.TryAttach();
        }

        // One simulation tick, called only by ChainRushGame.FixedUpdate.
        public void Step(in TickInput input)
        {
            if (!game.IsRunning) return;
            NormalizeController();
            RacerState racer = game.Racer;
            ref Vector3 velocity = ref racer.Velocity;
            ref float steer = ref racer.Steer;
            ref bool jumpQueued = ref racer.JumpQueued;
            ref float coyoteTime = ref racer.CoyoteTime;
            steer = input.Steer;
            if (input.PrimaryPressed) PrimaryAction();
            if (input.ReleasePressed) grapple.Release(true);
            float dt = Ticks.Seconds;
            bool grounded = racer.Grounded;
            coyoteTime = grounded ? 0.1f : Mathf.Max(0f, coyoteTime - dt);
            if (grounded && velocity.y < 0f) velocity.y = -2f;
            if (jumpQueued)
            {
                if (coyoteTime > 0f)
                {
                    velocity.y = jumpSpeed;
                    coyoteTime = 0f;
                    game.PlayCue(0);
                }
                jumpQueued = false;
            }
            drifting = input.Drift && grounded;
            ref float heading = ref racer.Heading;
            TrackFrame frame = game.Track.Frame(transform.position);
            ref int slingTicks = ref racer.SlingTicks;
            ref int swingTicks = ref racer.SwingTicks;
            if (input.ChainActionPressed) ChainAction(frame);
            // A swing lasts while drift is held on the same arc it hooked.
            if (swingTicks > 0 && (!input.Drift || grapple.IsAttached || frame.Curvature * racer.SwingRadius <= 0f))
                EndSwing(ref velocity, heading);
            bool swinging = swingTicks > 0;
            int carryTicks = Ticks.FromSeconds(slingCarryTime);
            bool pulling = slingTicks > carryTicks;
            bool carrying = slingTicks > 0 && !pulling;
            // Free steering: input turns the facing, and running and grip act along the facing. The track
            // only supplies the grade here; it no longer decides which way is ahead.
            ref float turnRate = ref racer.TurnRate;
            float turnScale = grounded ? (drifting ? driftTurnScale : 1f) : airTurnScale;
            turnRate = Mathf.MoveTowards(turnRate, steer * maxTurnRate * turnScale, turnAcceleration * dt);
            heading = Mathf.Repeat(heading + turnRate * dt + 180f, 360f) - 180f;
            if (pulling)
            {
                // The hook point rides the centerline ahead, so the pull follows curves.
                slingTarget = game.Track.FrameAt(frame.S + slingLead).Position + Vector3.up * (transform.position.y - frame.Position.y);
                Vector3 toward = slingTarget - transform.position;
                toward.y = 0f;
                if (toward.sqrMagnitude > 0.01f)
                    heading = Mathf.Repeat(Mathf.MoveTowardsAngle(heading, Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg, slingTurnRate * dt) + 180f, 360f) - 180f;
            }
            Vector3 facing = FacingOf(heading);
            Vector3 right = RightOf(heading);
            float grip = grounded ? (drifting ? driftGrip : sideGrip) : airSideGrip;
            float sideSpeed = Mathf.MoveTowards(Vector3.Dot(velocity, right), 0f, grip * dt);
            if (drifting && !swinging) racer.AddGauge(Mathf.Abs(sideSpeed) * dt / slideMetersPerSlot);
            float runTarget = grounded
                ? runSpeed * Mathf.Clamp(1f - slopeSpeedFactor * frame.Grade, minSlopeSpeedScale, maxSlopeSpeedScale)
                : runSpeed;
            if (drifting) runTarget *= driftSpeedScale;
            if (carrying) runTarget = Mathf.Max(runTarget, slingCarrySpeed);
            if (pulling) runTarget = Mathf.Max(runTarget, slingTopSpeed);
            float acceleration = pulling ? slingPullAcceleration : grounded ? 30f : 5f;
            if (swinging)
            {
                // The swing owns the speed: no drift slow-down pulling against it.
                runTarget = swingSpeed;
                acceleration = swingAcceleration;
            }
            float forwardSpeed = Mathf.MoveTowards(Vector3.Dot(velocity, facing), grapple.IsAttached ? 16f : runTarget,
                acceleration * dt);
            velocity = facing * forwardSpeed + right * sideSpeed + Vector3.up * velocity.y;
            velocity.y = Mathf.Max(velocity.y - gravity * dt, -28f);
            Vector3 displacement = velocity * dt;
            grapple.ConstrainMotion(transform.position, ref displacement, ref velocity, dt);
            if (swinging) SwingStep(frame, ref velocity, ref heading, ref displacement, dt);
            hitGuard = false;
            CollisionFlags flags = controller.Move(displacement);
            SyncGrounded();
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
            if (hitGuard) GlanceOffGuard(ref velocity, ref heading, frame.Forward);
            if (grounded && !racer.Grounded && velocity.y <= 0f && !grapple.IsAttached) SnapToGround();
            if (racer.Grounded && grapple.IsAttached) grapple.Release(false);
            if (grapple.IsAttached && Vector3.Dot(transform.position - grapple.AnchorPosition, frame.Forward) > 0.5f)
                grapple.Release(true);
            if (slingTicks > 0) slingTicks--;
            if (swingTicks > 0 && --swingTicks == 0) EndSwing(ref velocity, heading);
            // Falling is measured from the track surface, so a long downhill is not a fall.
            if (game.Track.Project(transform.position).H < -12f) game.FailRun();
        }

        // One gauge slot buys the chain action that fits the moment, in this order: while grappling it
        // empowers the grapple; while drifting into a curve it hooks the curve for a corner swing; otherwise
        // it fires the slingshot. Without a whole slot, or with that action already running, nothing happens
        // and nothing is spent.
        private void ChainAction(TrackFrame frame)
        {
            RacerState racer = game.Racer;
            if (grapple.IsAttached)
            {
                if (racer.GrappleEmpowered || !racer.TrySpendGaugeSlot()) return;
                racer.EmpowerGrapple();
                game.PlayCue(1);
                return;
            }
            if (racer.SwingTicks > 0) return;
            bool intoCurve = frame.Curvature != 0f && racer.Steer * frame.Curvature > 0f;
            if (drifting && intoCurve)
            {
                if (!racer.TrySpendGaugeSlot()) return;
                Vector3 radial = transform.position - frame.CurveCenter;
                radial.y = 0f;
                racer.SwingRadius = Mathf.Sign(frame.Curvature) * radial.magnitude;
                racer.SwingTicks = Ticks.FromSeconds(swingMaxTime);
                game.PlayCue(1);
                return;
            }
            if (racer.SlingTicks > 0 || !racer.TrySpendGaugeSlot()) return;
            racer.SlingTicks = Ticks.FromSeconds(slingPullTime) + Ticks.FromSeconds(slingCarryTime);
            game.PlayCue(1);
        }

        // Circles the hooked arc's center at the swing radius: heading and velocity turn onto the tangent in
        // the track's direction (the speed itself was already eased toward swingSpeed this tick), and the
        // step is pulled back onto the circle.
        private void SwingStep(TrackFrame frame, ref Vector3 velocity, ref float heading, ref Vector3 displacement, float dt)
        {
            Vector3 center = frame.CurveCenter;
            center.y = transform.position.y;
            swingAnchor = center + Vector3.up * 0.5f;
            Vector3 outward = transform.position - center;
            float distance = outward.magnitude;
            if (distance < 0.01f) return;
            outward /= distance;
            Vector3 tangent = frame.Forward - outward * Vector3.Dot(frame.Forward, outward);
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 1e-6f) return;
            tangent.Normalize();
            heading = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
            float speed = Mathf.Max(0f, Vector3.Dot(velocity, tangent));
            velocity = tangent * speed + Vector3.up * velocity.y;
            displacement = velocity * dt;
            Vector3 next = transform.position + displacement - center;
            next.y = 0f;
            float reach = next.magnitude;
            if (reach > 0.01f) displacement += next / reach * (Mathf.Abs(game.Racer.SwingRadius) - reach);
        }

        // Ends the swing and throws the runner along its facing at swingExitSpeed or more, with the
        // slingshot carry holding extra speed for swingExitCarryTime.
        private void EndSwing(ref Vector3 velocity, float heading)
        {
            RacerState racer = game.Racer;
            racer.SwingTicks = 0;
            Vector3 facing = FacingOf(heading);
            Vector3 right = RightOf(heading);
            float forwardSpeed = Mathf.Max(Vector3.Dot(velocity, facing), swingExitSpeed);
            velocity = facing * forwardSpeed + right * Vector3.Dot(velocity, right) + Vector3.up * velocity.y;
            racer.SlingTicks = Mathf.Max(racer.SlingTicks, Ticks.FromSeconds(swingExitCarryTime));
        }

        // Downhill the road can drop away faster than one tick of grounded fall. When ground is still within
        // reach, pull the capsule back onto it so grounded checks (jump, encounters) do not flicker.
        // Past a ledge the cast finds nothing and the runner falls as before.
        private void SnapToGround()
        {
            float radius = controller.radius;
            Vector3 bottom = transform.TransformPoint(controller.center) + Vector3.down * (controller.height * 0.5f - radius);
            float reach = groundSnapDistance + controller.skinWidth;
            if (!Physics.SphereCast(bottom, radius, Vector3.down, out RaycastHit hit, reach,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            controller.Move(Vector3.down * (hit.distance + controller.skinWidth));
            SyncGrounded();
        }

        // The physics controller keeps its own position at higher precision than transform.position, so a runner put
        // back at a saved transform position (a rollback) would start the next Move from a slightly different place
        // than the run that was saved. Re-enabling the controller makes its position exactly the transform's, so
        // every tick starts from the float position alone and a restored tick is the same tick.
        // Grounded is not touched: it is the racer's copy, taken after the last Move.
        private void NormalizeController()
        {
            controller.enabled = false;
            controller.enabled = true;
        }

        // The racer's copy of the controller's grounded answer (see RacerState.Grounded). Call it right after anything
        // that moves or re-enables the controller.
        private void SyncGrounded() => game.Racer.Grounded = controller.isGrounded;

        // empowered: the grapple was empowered by a chain action before this release.
        public void AddReleaseBoost(bool empowered)
        {
            // A full takeoff impulse keeps the capsule above the next platform lip.
            ref Vector3 velocity = ref game.Racer.Velocity;
            float heading = game.Racer.Heading;
            Vector3 facing = FacingOf(heading);
            Vector3 right = RightOf(heading);
            float forwardSpeed = Mathf.Max(Vector3.Dot(velocity, facing), empowered ? empoweredReleaseSpeed : 13f);
            velocity = facing * forwardSpeed + right * Vector3.Dot(velocity, right) + Vector3.up * Mathf.Max(velocity.y, jumpSpeed);
        }

        // Turns the facing to the track's direction here. StartRun calls it after the racer state resets.
        public void FaceTrack()
        {
            Vector3 forward = game.Track.Frame(transform.position).Forward;
            game.Racer.Heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            game.Racer.TurnRate = 0f;
        }

        // Guards are the walls along the road edges. A contact counts when its normal is level and runs
        // across the track; deck lips and fronts face along the track and are left to the controller.
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Vector3 normal = hit.normal;
            if (Mathf.Abs(normal.y) > 0.3f) return;
            normal.y = 0f;
            normal.Normalize();
            if (Mathf.Abs(Vector3.Dot(normal, game.Track.Frame(transform.position).Forward)) > 0.5f) return;
            if (hitGuard && Vector3.Dot(normal, Facing) >= Vector3.Dot(guardNormal, Facing)) return;
            guardNormal = normal;
            hitGuard = true;
        }

        // Removes the push into the guard, loses speed by how head-on the hit was, and turns a facing that
        // points into the guard to run along it in the track's direction.
        private void GlanceOffGuard(ref Vector3 velocity, ref float heading, Vector3 trackForward)
        {
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            float speed = flat.magnitude;
            float into = -Vector3.Dot(flat, guardNormal);
            if (speed > 0f && into > 0f)
            {
                flat = (flat + guardNormal * into) * (1f - guardSpeedLoss * into / speed);
                velocity = new Vector3(flat.x, velocity.y, flat.z);
            }
            if (Vector3.Dot(FacingOf(heading), guardNormal) >= 0f) return;
            Vector3 along = Vector3.Cross(Vector3.up, guardNormal);
            if (Vector3.Dot(along, trackForward) < 0f) along = -along;
            heading = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg;
        }

        private static Vector3 FacingOf(float heading)
        {
            float radians = heading * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }

        private static Vector3 RightOf(float heading)
        {
            float radians = heading * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), 0f, -Mathf.Sin(radians));
        }

        public void ShiftOrigin(Vector3 offset)
        {
            controller.enabled = false;
            transform.position += offset;
            controller.enabled = true;
            SyncGrounded();
            grapple.ShiftOrigin(offset);
        }

        // Puts the runner at a position without moving it through the world (a rollback, a test). The grounded
        // copy is refreshed from the controller; a rollback then overwrites it with the saved answer.
        public void SetPosition(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            SyncGrounded();
            Physics.SyncTransforms();
        }

        public void ResetAtSpawn()
        {
            grapple.Release(false);
            grapple.ClearMiss();
            controller.enabled = false;
            transform.position = spawnPosition;
            controller.enabled = true;
            controller.Move(Vector3.down * 0.3f);
            SyncGrounded();
            RacerState racer = game.Racer;
            racer.Velocity = Vector3.zero;
            racer.Steer = 0f;
            racer.JumpQueued = false;
            racer.CoyoteTime = 0f;
        }
    }
}
