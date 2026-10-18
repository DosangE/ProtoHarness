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
        // follows it by turnAcceleration, and running follows the facing. Air control is scaled down. With a
        // minTurnRadius above 0 the turn rate is also kept to speed / minTurnRadius, so at speed steering alone turns wide
        // (KartRider style: tight corners are for the pole swing); 0 leaves only maxTurnRate.
        [SerializeField] private float maxTurnRate = 120f;
        [SerializeField] private float turnAcceleration = 1200f;
        [SerializeField] private float airTurnScale = 0.5f;
        [SerializeField] private float minTurnRadius = 0f;
        // Turning costs speed on the ground (KartRider style): the run target drops by turnSpeedLoss times how hard the
        // runner turns (its turn rate over the turn limit). 0 turns it off.
        [SerializeField] private float turnSpeedLoss = 0f;
        // How fast a grounded runner below its run target speeds up to it, in m/s a second.
        [SerializeField] private float runAcceleration = 30f;
        // Velocity across the facing (from rope swings, wall glances) bleeds off at these rates.
        [SerializeField] private float sideGrip = 60f;
        [SerializeField] private float airSideGrip = 18f;
        // Share of speed lost in a head-on guard hit; glancing hits lose proportionally less. Every tick in contact with a
        // guard also scrapes off guardScrapeDecel (m/s a second) of speed; 0 turns that off.
        [SerializeField] private float guardSpeedLoss = 0.15f;
        [SerializeField] private float guardScrapeDecel = 0f;
        // Grounded run speed scales by 1 - factor * grade, clamped: uphill slows, downhill speeds up.
        [SerializeField] private float slopeSpeedFactor = 1.5f;
        [SerializeField] private float minSlopeSpeedScale = 0.7f;
        [SerializeField] private float maxSlopeSpeedScale = 1.3f;
        // How fast speed above the grounded run target (a slingshot or swing let go) falls back to it, in m/s a second.
        [SerializeField] private float overspeedFalloff = 30f;
        // How far below the capsule a downhill road may drop in one tick and still be followed.
        [SerializeField] private float groundSnapDistance = 0.3f;
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
        // Corner pole swing, the drift of this game (NEXT_REQUESTS.md R4b): every arc has a pole at its center. Pressing
        // the swing key hooks the nearest pole within poleMaxAngle either side of the facing and no farther than its arc's
        // radius plus poleReach (holding the key into reach does not); the runner circles it for as long as the key is
        // held, with no running force and no time limit. Steering works the rope instead of the facing: the key toward
        // the pole shortens it, the other lets it out, by ropeRate a second, never below minRopeLength. The speed is the
        // hook's speed times the hooked rope length over the rope length now, kept within minSwingSpeedScale ..
        // maxSwingSpeedScale of the hook's speed. Every swingMetersPerSlot swung fills one gauge slot. Letting go carries
        // the runner straight on at that speed, the part above running speed falling off by swingCarryFalloff a second.
        // Touching a guard, or a grapple taking hold, ends the swing; a guard ends it without the carry.
        [SerializeField] private float poleMaxAngle = 95f;
        [SerializeField] private float poleReach = 6f;
        [SerializeField] private float ropeRate = 6f;
        [SerializeField] private float minRopeLength = 2f;
        [SerializeField] private float minSwingSpeedScale = 0.8f;
        [SerializeField] private float maxSwingSpeedScale = 1.5f;
        [SerializeField] private float swingMetersPerSlot = 35f;
        [SerializeField] private float swingCarryFalloff = 3f;
        // Falling in a circuit race (KartRider style): the runner comes back on the centerline respawnBackoff metres
        // before where it last stood, at rest, and stands there for respawnHoldTime. The time lost is the penalty.
        [SerializeField] private float respawnBackoff = 4f;
        [SerializeField] private float respawnHoldTime = 1f;
        private Vector3 spawnPosition;
        // Scratch for one Move call: the most head-on side contact OnControllerColliderHit reported.
        private Vector3 guardNormal;
        private bool hitGuard;
        // Presentation reads these; the simulation recomputes them every tick.
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
        public float Gauge => game.Racer.Gauge;
        public bool IsSlingPulling => game.Racer.SlingTicks > Ticks.FromSeconds(slingCarryTime);
        public Vector3 SlingTarget => slingTarget;
        public bool IsSwinging => game.Racer.SwingTicks > 0;
        public Vector3 SwingAnchor => swingAnchor;
        public bool IsRespawning => game.Racer.RespawnHoldTicks > 0;

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
            if (maxTurnRate <= 0f || turnAcceleration <= 0f || airTurnScale < 0f || airTurnScale > 1f || minTurnRadius < 0f)
                Debug.LogError("RunnerMotor: turn rate and acceleration must be positive, air turn scale within [0, 1], the turn radius not negative.", this);
            if (turnSpeedLoss < 0f || turnSpeedLoss >= 1f || runAcceleration <= 0f)
                Debug.LogError("RunnerMotor: the turn speed loss must be within [0, 1), the run acceleration positive.", this);
            if (sideGrip < 0f || airSideGrip < 0f || guardSpeedLoss < 0f || guardSpeedLoss > 1f || guardScrapeDecel < 0f)
                Debug.LogError("RunnerMotor: grips and the guard scrape cannot be negative, guard speed loss within [0, 1].", this);
            if (slopeSpeedFactor < 0f || minSlopeSpeedScale <= 0f || minSlopeSpeedScale > 1f || maxSlopeSpeedScale < 1f)
                Debug.LogError("RunnerMotor: slope factor cannot be negative, and the speed scale range must be within (0, 1] .. [1, inf).", this);
            if (groundSnapDistance < 0f || overspeedFalloff <= 0f)
                Debug.LogError("RunnerMotor: ground snap distance cannot be negative, the overspeed falloff must be positive.", this);
            if (slingLead <= 0f || slingPullTime <= 0f || slingPullAcceleration <= 0f || slingTopSpeed <= runSpeed
                || slingTurnRate < 0f || slingCarryTime < 0f || slingCarrySpeed < runSpeed || empoweredReleaseSpeed < 13f)
                Debug.LogError("RunnerMotor: slingshot settings must be positive, its speeds above run speed, and the empowered release at least the normal 13.", this);
            if (!(poleMaxAngle > 0f && poleMaxAngle < 180f) || poleReach < 0f || ropeRate < 0f || minRopeLength <= 0f)
                Debug.LogError("RunnerMotor: the pole angle must be within (0, 180) degrees, the pole reach and rope rate not negative, the shortest rope positive.", this);
            if (!(minSwingSpeedScale > 0f && minSwingSpeedScale <= 1f) || maxSwingSpeedScale < 1f || swingMetersPerSlot <= 0f || swingCarryFalloff <= 0f)
                Debug.LogError("RunnerMotor: the swing speed scale range must be within (0, 1] .. [1, inf), the metres per slot and carry falloff positive.", this);
            if (respawnBackoff < 0f || respawnHoldTime < 0f)
                Debug.LogError("RunnerMotor: respawn backoff and hold time cannot be negative.", this);
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
            // A runner just put back on the track stands still and ignores its controls until the hold runs out.
            if (racer.RespawnHoldTicks > 0)
            {
                racer.RespawnHoldTicks--;
                racer.Steer = 0f;
                return;
            }
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
            ref float heading = ref racer.Heading;
            TrackFrame frame = game.Track.Frame(transform.position);
            ref int slingTicks = ref racer.SlingTicks;
            ref int swingTicks = ref racer.SwingTicks;
            if (input.ChainActionPressed) ChainAction();
            if (input.SwingPressed) TryHookPole();
            // A swing lasts while the swing key is held. A press counts as held for its own tick, so a tap that comes and
            // goes between two ticks still swings for one tick.
            bool swingHeld = input.SwingHeld || input.SwingPressed;
            if (swingTicks > 0 && (!swingHeld || grapple.IsAttached)) EndSwing(velocity, heading);
            bool swinging = swingTicks > 0;
            int carryTicks = Ticks.FromSeconds(slingCarryTime);
            bool pulling = slingTicks > carryTicks;
            bool carrying = slingTicks > 0 && !pulling;
            // Free steering: input turns the facing, and running and grip act along the facing. The track
            // only supplies the grade here; it no longer decides which way is ahead.
            ref float turnRate = ref racer.TurnRate;
            float turnScale = grounded ? 1f : airTurnScale;
            // A swing ignores steering for the facing (steering works the rope there): the rope sets the facing
            // (SwingStep), and turning the facing first would split the velocity across it and let the side grip eat
            // the swing's speed.
            float turnLimit = maxTurnRate;
            if (minTurnRadius > 0f)
                turnLimit = Mathf.Min(turnLimit, new Vector2(velocity.x, velocity.z).magnitude / minTurnRadius * Mathf.Rad2Deg);
            turnRate = swinging ? 0f : Mathf.MoveTowards(turnRate, steer * turnLimit * turnScale, turnAcceleration * dt);
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
            float grip = grounded ? sideGrip : airSideGrip;
            float sideSpeed = Mathf.MoveTowards(Vector3.Dot(velocity, right), 0f, grip * dt);
            float runTarget = grounded
                ? runSpeed * Mathf.Clamp(1f - slopeSpeedFactor * frame.Grade, minSlopeSpeedScale, maxSlopeSpeedScale)
                : runSpeed;
            if (turnSpeedLoss > 0f && grounded && !swinging && turnLimit > 0f)
                runTarget *= 1f - turnSpeedLoss * Mathf.Min(1f, Mathf.Abs(turnRate) / turnLimit);
            // The speed a swing let go with falls off slowly; once it is down to the running speed it is gone.
            ref float swingCarry = ref racer.SwingCarrySpeed;
            if (swingCarry > 0f)
            {
                swingCarry = Mathf.Max(0f, swingCarry - swingCarryFalloff * dt);
                if (swingCarry > runTarget) runTarget = swingCarry;
                else swingCarry = 0f;
            }
            if (carrying) runTarget = Mathf.Max(runTarget, slingCarrySpeed);
            if (pulling) runTarget = Mathf.Max(runTarget, slingTopSpeed);
            float currentForward = Vector3.Dot(velocity, facing);
            float acceleration = pulling ? slingPullAcceleration
                : !grounded ? 5f
                : currentForward > runTarget ? overspeedFalloff : runAcceleration;
            // The swing sets its own speed (SwingStep): no running force acts on it.
            if (swinging) acceleration = 0f;
            // A grapple reels at 16 m/s, or at running speed when that is faster.
            float forwardSpeed = Mathf.MoveTowards(currentForward, grapple.IsAttached ? Mathf.Max(16f, runSpeed) : runTarget,
                acceleration * dt);
            velocity = facing * forwardSpeed + right * sideSpeed + Vector3.up * velocity.y;
            velocity.y = Mathf.Max(velocity.y - gravity * dt, -28f);
            Vector3 displacement = velocity * dt;
            grapple.ConstrainMotion(transform.position, ref displacement, ref velocity, dt);
            if (swinging) SwingStep(ref velocity, ref heading, ref displacement, dt);
            hitGuard = false;
            CollisionFlags flags = controller.Move(displacement);
            SyncGrounded();
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
            if (hitGuard)
            {
                // A swing that carries the runner into a guard snaps there, without the carry.
                swingTicks = 0;
                GlanceOffGuard(ref velocity, ref heading, frame.Forward);
                if (guardScrapeDecel > 0f) ScrapeAlongGuard(ref velocity, dt);
            }
            if (grounded && !racer.Grounded && velocity.y <= 0f && !grapple.IsAttached) SnapToGround();
            if (racer.Grounded && grapple.IsAttached) grapple.Release(false);
            if (grapple.IsAttached && Vector3.Dot(transform.position - grapple.AnchorPosition, frame.Forward) > 0.5f)
                grapple.Release(true);
            if (slingTicks > 0) slingTicks--;
            if (swingTicks > 0) swingTicks++;
            // Falling is measured from the track surface, so a long downhill is not a fall. A circuit race puts the runner
            // back on the track; the other courses end the run.
            TrackCoord here = game.Track.Project(transform.position);
            if (here.H < -12f)
            {
                if (game.IsCircuit) RespawnOnTrack();
                else game.FailRun();
                return;
            }
            if (racer.Grounded) racer.LastGroundedS = here.S;
        }

        // Puts a fallen runner on the centerline respawnBackoff metres before where it last stood, as high as the start
        // slot, facing along the track and at rest, and starts the hold. The chain gauge is kept; a grapple, slingshot or
        // swing ends without its boost. LastGroundedS stays, so a second fall before landing comes back to the same place.
        private void RespawnOnTrack()
        {
            RacerState racer = game.Racer;
            grapple.Release(false);
            TrackFrame frame = game.Track.FrameAt(racer.LastGroundedS - respawnBackoff);
            controller.enabled = false;
            transform.position = frame.TransformPoint(new Vector3(0f, 1.05f, 0f));
            controller.enabled = true;
            controller.Move(Vector3.down * 0.3f);
            SyncGrounded();
            Physics.SyncTransforms();
            racer.Velocity = Vector3.zero;
            racer.JumpQueued = false;
            racer.CoyoteTime = 0f;
            racer.SlingTicks = 0;
            racer.SwingTicks = 0;
            racer.SwingCarrySpeed = 0f;
            racer.Heading = Mathf.Atan2(frame.Forward.x, frame.Forward.z) * Mathf.Rad2Deg;
            racer.TurnRate = 0f;
            racer.RespawnHoldTicks = Ticks.FromSeconds(respawnHoldTime);
        }

        // One gauge slot buys the chain action that fits the moment: while grappling it empowers the grapple;
        // otherwise it fires the slingshot. Without a whole slot, or with that action already running, nothing
        // happens and nothing is spent. Corner pole swings are the swing key's, not the chain action's.
        private void ChainAction()
        {
            RacerState racer = game.Racer;
            if (grapple.IsAttached)
            {
                if (racer.GrappleEmpowered || !racer.TrySpendGaugeSlot()) return;
                racer.EmpowerGrapple();
                game.PlayCue(1);
                return;
            }
            if (racer.SlingTicks > 0 || !racer.TrySpendGaugeSlot()) return;
            racer.SlingTicks = Ticks.FromSeconds(slingPullTime) + Ticks.FromSeconds(slingCarryTime);
            game.PlayCue(1);
        }

        // A swing key press hooks the corner pole in reach, if there is one, at the rope length and speed of this moment.
        // It costs nothing. While grappling or already swinging, a press does nothing.
        private void TryHookPole()
        {
            RacerState racer = game.Racer;
            if (grapple.IsAttached || racer.SwingTicks > 0 || !TryFindPole(out Vector3 pole)) return;
            Vector3 radial = transform.position - pole;
            radial.y = 0f;
            Vector3 velocity = racer.Velocity;
            velocity.y = 0f;
            racer.SwingCenter = pole;
            racer.SwingRadius = radial.magnitude;
            racer.SwingHookRadius = radial.magnitude;
            racer.SwingHookSpeed = velocity.magnitude;
            racer.SwingCarrySpeed = 0f;
            racer.SwingTicks = 1;
            game.PlayCue(1);
        }

        // The corner pole a swing key press would hook from here and now, if any (see poleMaxAngle and poleReach). The
        // simulation asks it on a press; presentation and bots may ask it any time, since it only reads.
        public bool TryFindPole(out Vector3 pole) =>
            game.Track.TryFindPole(transform.position, game.Racer.Heading, poleMaxAngle, poleReach, out pole);

        // Circles the pole: heading and velocity turn onto the tangent on the side the runner already faces, steering
        // works the rope (the key toward the pole shortens it), the speed follows the rope (see ropeRate), the step is
        // pulled onto the circle, and the distance swung fills the gauge.
        private void SwingStep(ref Vector3 velocity, ref float heading, ref Vector3 displacement, float dt)
        {
            RacerState racer = game.Racer;
            Vector3 center = racer.SwingCenter;
            center.y = transform.position.y;
            swingAnchor = center + Vector3.up * 0.5f;
            Vector3 outward = transform.position - center;
            float distance = outward.magnitude;
            if (distance < 0.01f) return;
            outward /= distance;
            Vector3 tangent = Vector3.Cross(Vector3.up, outward);
            if (Vector3.Dot(tangent, FacingOf(heading)) < 0f) tangent = -tangent;
            heading = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
            // +1 when the pole is on the right of the new facing (a right-hand corner, where D is the key toward it).
            float poleSide = Vector3.Dot(-outward, RightOf(heading)) >= 0f ? 1f : -1f;
            racer.SwingRadius = Mathf.Max(minRopeLength, racer.SwingRadius - racer.Steer * poleSide * ropeRate * dt);
            float scale = Mathf.Clamp(racer.SwingHookRadius / racer.SwingRadius, minSwingSpeedScale, maxSwingSpeedScale);
            float speed = racer.SwingHookSpeed * scale;
            velocity = tangent * speed + Vector3.up * velocity.y;
            displacement = velocity * dt;
            Vector3 next = transform.position + displacement - center;
            next.y = 0f;
            float reach = next.magnitude;
            if (reach > 0.01f) displacement += next / reach * (racer.SwingRadius - reach);
            racer.AddGauge(speed * dt / swingMetersPerSlot);
        }

        // Ends the swing: the runner goes straight on along its facing at the speed it swung with, and what that is above
        // running speed falls off by swingCarryFalloff a second (Step).
        private void EndSwing(Vector3 velocity, float heading)
        {
            RacerState racer = game.Racer;
            racer.SwingTicks = 0;
            racer.SwingCarrySpeed = Mathf.Max(0f, Vector3.Dot(velocity, FacingOf(heading)));
        }

        // Downhill the road can drop away faster than one tick of grounded fall. When ground is still within
        // reach, pull the capsule back onto it so grounded checks (jump, encounters) do not flicker.
        // Past a ledge the cast finds nothing and the runner falls as before.
        private void SnapToGround()
        {
            float radius = controller.radius;
            Vector3 bottom = transform.TransformPoint(controller.center) + Vector3.down * (controller.height * 0.5f - radius);
            float reach = groundSnapDistance + controller.skinWidth;
            // The physics world of the scene this runner lives in: a race world loaded on its own physics scene must
            // not see another racer's world (for the main scene this is the default physics scene).
            PhysicsScene world = gameObject.scene.GetPhysicsScene();
            if (!world.SphereCast(bottom, radius, Vector3.down, out RaycastHit hit, reach,
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

        // Rubbing along a guard: the level speed drops by guardScrapeDecel for this tick of contact.
        private void ScrapeAlongGuard(ref Vector3 velocity, float dt)
        {
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            float speed = flat.magnitude;
            if (speed <= 0f) return;
            flat *= Mathf.Max(0f, speed - guardScrapeDecel * dt) / speed;
            velocity = new Vector3(flat.x, velocity.y, flat.z);
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
            // A swing's pole is a world position like the track it came from (Centerline.ShiftOrigin moves that).
            if (game.Racer.SwingTicks > 0) game.Racer.SwingCenter += offset;
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
