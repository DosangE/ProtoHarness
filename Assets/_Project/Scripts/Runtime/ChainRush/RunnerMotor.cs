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
        private Vector3 spawnPosition;
        // Scratch for one Move call: the most head-on side contact OnControllerColliderHit reported.
        private Vector3 guardNormal;
        private bool hitGuard;

        // Motion state lives in the racer's RacerState; the motor only integrates it.
        public Vector3 Velocity => game.Racer.Velocity;
        public float Steer => game.Racer.Steer;
        public float Heading => game.Racer.Heading;
        public Vector3 Facing => FacingOf(game.Racer.Heading);
        public float ForwardSpeed => Vector3.Dot(game.Racer.Velocity, Facing);
        public float SideSpeed => Vector3.Dot(game.Racer.Velocity, RightOf(game.Racer.Heading));
        public bool IsGrounded => controller.isGrounded;
        public float Speed => game.Racer.Velocity.magnitude;

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
            RacerState racer = game.Racer;
            ref Vector3 velocity = ref racer.Velocity;
            ref float steer = ref racer.Steer;
            ref bool jumpQueued = ref racer.JumpQueued;
            ref float coyoteTime = ref racer.CoyoteTime;
            steer = input.Steer;
            if (input.PrimaryPressed) PrimaryAction();
            if (input.ReleasePressed) grapple.Release(true);
            float dt = Ticks.Seconds;
            bool grounded = controller.isGrounded;
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
            // Free steering: input turns the facing, and running and grip act along the facing. The track
            // only supplies the grade here; it no longer decides which way is ahead.
            ref float heading = ref racer.Heading;
            ref float turnRate = ref racer.TurnRate;
            turnRate = Mathf.MoveTowards(turnRate, steer * maxTurnRate * (grounded ? 1f : airTurnScale), turnAcceleration * dt);
            heading = Mathf.Repeat(heading + turnRate * dt + 180f, 360f) - 180f;
            TrackFrame frame = game.Track.Frame(transform.position);
            Vector3 facing = FacingOf(heading);
            Vector3 right = RightOf(heading);
            float sideSpeed = Mathf.MoveTowards(Vector3.Dot(velocity, right), 0f, (grounded ? sideGrip : airSideGrip) * dt);
            float runTarget = grounded
                ? runSpeed * Mathf.Clamp(1f - slopeSpeedFactor * frame.Grade, minSlopeSpeedScale, maxSlopeSpeedScale)
                : runSpeed;
            float forwardSpeed = Mathf.MoveTowards(Vector3.Dot(velocity, facing), grapple.IsAttached ? 16f : runTarget,
                (grounded ? 30f : 5f) * dt);
            velocity = facing * forwardSpeed + right * sideSpeed + Vector3.up * velocity.y;
            velocity.y = Mathf.Max(velocity.y - gravity * dt, -28f);
            Vector3 displacement = velocity * dt;
            grapple.ConstrainMotion(transform.position, ref displacement, ref velocity, dt);
            hitGuard = false;
            CollisionFlags flags = controller.Move(displacement);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
            if (hitGuard) GlanceOffGuard(ref velocity, ref heading, frame.Forward);
            if (grounded && !controller.isGrounded && velocity.y <= 0f && !grapple.IsAttached) SnapToGround();
            if (controller.isGrounded && grapple.IsAttached) grapple.Release(false);
            if (grapple.IsAttached && Vector3.Dot(transform.position - grapple.AnchorPosition, frame.Forward) > 0.5f)
                grapple.Release(true);
            // Falling is measured from the track surface, so a long downhill is not a fall.
            if (game.Track.Project(transform.position).H < -12f) game.FailRun();
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
        }

        public void AddReleaseBoost()
        {
            // A full takeoff impulse keeps the capsule above the next platform lip.
            ref Vector3 velocity = ref game.Racer.Velocity;
            float heading = game.Racer.Heading;
            Vector3 facing = FacingOf(heading);
            Vector3 right = RightOf(heading);
            float forwardSpeed = Mathf.Max(Vector3.Dot(velocity, facing), 13f);
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
            grapple.ShiftOrigin(offset);
        }

        public void ResetAtSpawn()
        {
            grapple.Release(false);
            grapple.ClearMiss();
            controller.enabled = false;
            transform.position = spawnPosition;
            controller.enabled = true;
            controller.Move(Vector3.down * 0.3f);
            RacerState racer = game.Racer;
            racer.Velocity = Vector3.zero;
            racer.Steer = 0f;
            racer.JumpQueued = false;
            racer.CoyoteTime = 0f;
        }
    }
}
