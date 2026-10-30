using UnityEngine;
using UnityEngine.Serialization;
using ProtoHarness.ChainRush.Audio;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Motion;
using ProtoHarness.ChainRush.Track;

namespace ProtoHarness.ChainRush
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class RunnerMotor : MonoBehaviour
    {
        // Fixed rules of the motion, the same for every runner and scene (not serialized). The running rules' own are in
        // Motion/RunDrive.
        // How long after leaving the ground a jump still counts.
        private const float CoyoteSeconds = 0.1f;
        // How far below the track surface a runner has fallen.
        private const float FallDepth = 12f;
        // The least forward speed a grapple release throws the runner with (before the speed class); an empowered one uses
        // EmpoweredReleaseSpeed.
        private const float ReleaseSpeed = 13f;
        // How fast a grapple reels the runner in, unless running is faster (before the speed class).
        private const float GrappleReelSpeed = 16f;
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
        // How fast a grounded runner with the throttle let go slows toward a stop, in m/s a second (2026-10-19).
        [SerializeField] private float coastDeceleration = 6f;
        // Down held without the throttle (user 2026-10-22): a grounded runner still moving forward brakes by brakeDeceleration,
        // then backs up toward reverseSpeed by reverseAcceleration (m/s a second). While it moves backward with the key held,
        // steering turns the other way, as a car's does. In the air it is only coasting.
        [SerializeField] private float brakeDeceleration = 20f;
        [SerializeField] private float reverseSpeed = 6f;
        [SerializeField] private float reverseAcceleration = 8f;
        // An airborne runner (not on a grapple) with the throttle held speeds up as on the ground, toward the run target the
        // road below sets (so not past an uphill's); above it, it slows by airDeceleration in m/s a second. 0 keeps that
        // speed through the jump. With the throttle let go the air keeps the speed as it is (user 2026-10-19).
        [FormerlySerializedAs("airAcceleration")]
        [SerializeField] private float airDeceleration = 5f;
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
        // While it runs and the runner is below its boost speed, it accelerates by slingPullAcceleration scaled by how far
        // below that speed it is (2026-10-19): strong from low speed, easing off as it closes in.
        [SerializeField] private float slingLead = 18f;
        [SerializeField] private float slingPullTime = 0.4f;
        [SerializeField] private float slingPullAcceleration = 45f;
        [SerializeField] private float slingTopSpeed = 20f;
        [SerializeField] private float slingTurnRate = 90f;
        [SerializeField] private float slingCarryTime = 0.8f;
        [SerializeField] private float slingCarrySpeed = 16f;
        // A road pad's instant forward speed, far above the slingshot. It is the same for every runner and speed class
        // (user 2026-10-30 "55까지만"), so a jump that needs the pads (Palm Coast) flies as far in every class. On the
        // ground the part above running speed then falls off by boostZoneFalloff of itself a second (user 2026-10-30
        // "순간에 폭발적인 부스트만": hard at first, easing near running speed; about 5 s from 55 to running speed).
        [SerializeField] private float boostZoneSpeed = 55f;
        [SerializeField] private float boostZoneFalloff = 0.8f;
        // Empowered grapple (chain action while attached): the release throws harder along the facing.
        [SerializeField] private float empoweredReleaseSpeed = 18f;
        // Corner pole swing, the drift of this game (NEXT_REQUESTS.md R4b): every arc has a pole at its center. Pressing
        // the swing key on the ground hooks the nearest pole within poleMaxAngle either side of the track's direction and no farther than its arc's
        // radius plus poleReach (holding the key into reach does not), and only up to poleHookArcShare of the way round its
        // arc, never a corner already passed (user 2026-10-30); the runner circles it for as long as the key is
        // held, with no running force and no time limit. Steering works the rope instead of the facing: the key toward
        // the pole shortens it, the other lets it out, by ropeRate a second, never below minRopeLength. The speed is the
        // hook's speed times the hooked rope length over the rope length now, kept within minSwingSpeedScale ..
        // maxSwingSpeedScale of the hook's speed, the rope and the throttle together adding no faster than the run
        // acceleration above it (user 2026-10-27); a booster while swinging raises the hook's speed toward its own (PoleSwing.SpeedUp), and
        // without one the throttle held raises it toward swingThrottleSpeedScale times the run speed by the run acceleration
        // (user 2026-10-22); let go, the hook's speed stays. Every swingMetersPerSlot swung fills one gauge slot. Letting go carries
        // the runner straight on at that speed, the part above running speed falling off by swingCarryFalloff a second.
        // Touching a guard, or a grapple taking hold, ends the swing; a guard ends it without the carry.
        [SerializeField] private float poleMaxAngle = 95f;
        [SerializeField] private float poleReach = 6f;
        [SerializeField] private float poleHookArcShare = 0.5f;
        [SerializeField] private float ropeRate = 6f;
        [SerializeField] private float minRopeLength = 2f;
        [SerializeField] private float minSwingSpeedScale = 0.8f;
        [SerializeField] private float maxSwingSpeedScale = 1.5f;
        [SerializeField] private float swingThrottleSpeedScale = 1.2f;
        [SerializeField] private float swingMetersPerSlot = 35f;
        [SerializeField] private float swingCarryFalloff = 3f;
        // Falling in a circuit race (KartRider style): the runner comes back on the centerline respawnBackoff metres
        // before where it last stood, at rest, and stands there for respawnHoldTime. The time lost is the penalty.
        [SerializeField] private float respawnBackoff = 4f;
        [SerializeField] private float respawnHoldTime = 1f;
        // A fall within respawnPadReach metres after a boost pad comes back respawnBackoff before that pad instead: a jump
        // only a pad's speed clears (Palm Coast) would otherwise put the runner back past the pad, at rest, to fall again.
        [SerializeField] private float respawnPadReach = 50f;
        // The runner's character and the race's speed class (Configure): scales on the values above, all 1 until given.
        private RunnerProfile profile;
        private float topSpeedScale = 1f;
        private float accelerationScale = 1f;
        private float gaugeChargeScale = 1f;
        private float speedClassScale = 1f;
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
        public Vector3 Facing => RunDrive.FacingOf(game.Racer.Heading);
        public float ForwardSpeed => Vector3.Dot(game.Racer.Velocity, Facing);
        // The ground turn rate full steering reaches at the current speed, in degrees a second (Step's limit, before the
        // air scale): maxTurnRate, held to speed / minTurnRadius when that is set.
        public float TurnLimit => RunDrive.TurnLimit(game.Racer.Velocity, DriveSettings());
        public float SideSpeed => Vector3.Dot(game.Racer.Velocity, RunDrive.RightOf(game.Racer.Heading));
        public bool IsGrounded => game.Racer.Grounded;
        public float Speed => game.Racer.Velocity.magnitude;
        // Speed along the ground only: a grounded runner keeps a small downward velocity that holds it to the road
        // (-2 m/s plus one tick of gravity), which Speed counts and a speedometer must not.
        public float HorizontalSpeed => new Vector2(game.Racer.Velocity.x, game.Racer.Velocity.z).magnitude;
        public float Gauge => game.Racer.Gauge;
        public bool IsSlingPulling => Slingshot.IsPulling(game.Racer.SlingTicks, SlingSettings());
        public Vector3 SlingTarget => slingTarget;
        public bool IsSwinging => game.Racer.SwingTicks > 0;
        public Vector3 SwingAnchor => swingAnchor;
        public bool IsRespawning => game.Racer.RespawnHoldTicks > 0;
        // The profile given by Configure; null runs on the scene's values.
        public RunnerProfile Profile => profile;
        // The speed class's scale given by Configure (Flow.SpeedScale); 1 is the scene's speeds.
        public float SpeedClassScale => speedClassScale;
        // The values the simulation runs on: the scene's, scaled by the profile and the speed class.
        public float RunSpeed => runSpeed * topSpeedScale * speedClassScale;
        public float RunAcceleration => runAcceleration * accelerationScale * speedClassScale;
        public float SlingTopSpeed => slingTopSpeed * topSpeedScale * speedClassScale;
        public float SlingCarrySpeed => slingCarrySpeed * topSpeedScale * speedClassScale;
        public float BoostZoneSpeed => boostZoneSpeed;
        public float EmpoweredReleaseSpeed => empoweredReleaseSpeed * topSpeedScale * speedClassScale;
        public float SwingMetersPerSlot => swingMetersPerSlot / gaugeChargeScale;

        // Gives the runner a character for the runs to come (the lobby's choice, RaceAssembler), at the scene's speeds.
        public void Configure(RunnerProfile runner) => Configure(runner, 1f);

        // Gives the runner a character and the race's speed class scale (Flow.SpeedScale) for the runs to come. The scale
        // goes on the speeds and the run acceleration, as the profile's do, and on the grapple's fixed release and reel
        // speeds; jumps, gravity, decelerations and the turn radius stay. Not while a run is going: the run would change halfway.
        public void Configure(RunnerProfile runner, float speedScale)
        {
            if (runner == null) throw new System.ArgumentNullException(nameof(runner));
            if (!runner.TryValidate(out string error)) throw new System.ArgumentException(error, nameof(runner));
            if (!(speedScale > 0f) || float.IsInfinity(speedScale))
                throw new System.ArgumentOutOfRangeException(nameof(speedScale), speedScale, "RunnerMotor: the speed class scale must be positive.");
            if (game.IsRunning || game.IsPaused) throw new System.InvalidOperationException("RunnerMotor: configure the runner before the run, not during it.");
            profile = runner;
            topSpeedScale = runner.TopSpeedScale;
            accelerationScale = runner.AccelerationScale;
            gaugeChargeScale = runner.GaugeChargeScale;
            speedClassScale = speedScale;
        }

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
            if (turnSpeedLoss < 0f || turnSpeedLoss >= 1f || runAcceleration <= 0f || airDeceleration < 0f)
                Debug.LogError("RunnerMotor: the turn speed loss must be within [0, 1), the run acceleration positive, the air deceleration not negative.", this);
            if (sideGrip < 0f || airSideGrip < 0f || guardSpeedLoss < 0f || guardSpeedLoss > 1f || guardScrapeDecel < 0f)
                Debug.LogError("RunnerMotor: grips and the guard scrape cannot be negative, guard speed loss within [0, 1].", this);
            if (slopeSpeedFactor < 0f || minSlopeSpeedScale <= 0f || minSlopeSpeedScale > 1f || maxSlopeSpeedScale < 1f)
                Debug.LogError("RunnerMotor: slope factor cannot be negative, and the speed scale range must be within (0, 1] .. [1, inf).", this);
            if (groundSnapDistance < 0f || overspeedFalloff <= 0f || coastDeceleration <= 0f)
                Debug.LogError("RunnerMotor: ground snap distance cannot be negative, the overspeed falloff and coast deceleration must be positive.", this);
            if (brakeDeceleration <= 0f || reverseSpeed <= 0f || reverseAcceleration <= 0f)
                Debug.LogError("RunnerMotor: the brake deceleration, reverse speed and reverse acceleration must be positive.", this);
            if (slingLead <= 0f || slingPullTime <= 0f || slingPullAcceleration <= 0f || slingTopSpeed <= runSpeed
                || slingTurnRate < 0f || slingCarryTime < 0f || slingCarrySpeed < runSpeed || empoweredReleaseSpeed < ReleaseSpeed)
                Debug.LogError($"RunnerMotor: slingshot settings must be positive, its speeds above run speed, and the empowered release at least the normal {ReleaseSpeed}.", this);
            if (!(boostZoneSpeed > slingTopSpeed) || float.IsInfinity(boostZoneSpeed) || !(boostZoneFalloff > 0f) || float.IsInfinity(boostZoneFalloff))
                Debug.LogError("RunnerMotor: boost zone speed must be finite and above slingshot top speed, its falloff positive and finite.", this);
            if (!(poleMaxAngle > 0f && poleMaxAngle < 180f) || poleReach < 0f || !(poleHookArcShare > 0f && poleHookArcShare <= 1f) || ropeRate < 0f || minRopeLength <= 0f)
                Debug.LogError("RunnerMotor: the pole angle must be within (0, 180) degrees, the pole reach and rope rate not negative, the hook's arc share within (0, 1], the shortest rope positive.", this);
            if (!(minSwingSpeedScale > 0f && minSwingSpeedScale <= 1f) || maxSwingSpeedScale < 1f || swingMetersPerSlot <= 0f || swingCarryFalloff <= 0f)
                Debug.LogError("RunnerMotor: the swing speed scale range must be within (0, 1] .. [1, inf), the metres per slot and carry falloff positive.", this);
            if (swingThrottleSpeedScale < 1f)
                Debug.LogError("RunnerMotor: the swing throttle speed scale must be at least 1 (the run speed).", this);
            if (respawnBackoff < 0f || respawnHoldTime < 0f || !(respawnPadReach >= 0f))
                Debug.LogError("RunnerMotor: respawn backoff, hold time and pad reach cannot be negative.", this);
        }

        public void PrimaryAction()
        {
            if (!game.IsRunning) return;
            RacerState racer = game.Racer;
            if (IsGrounded || racer.CoyoteTime > 0f) racer.JumpQueued = true;
            else grapple.TryAttach();
        }

        // One simulation tick, called only by ChainRushGame.FixedUpdate. The stages run in the order below, each on what
        // the ones before it left in the racer's state; the order is part of the replay fingerprint. The rules live in
        // Motion/; the motor reads the input, moves the controller and keeps the order.
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
            RunDrive.Settings drive = DriveSettings();
            Slingshot.Settings sling = SlingSettings();
            racer.Steer = input.Steer;
            if (input.PrimaryPressed) PrimaryAction();
            if (input.ReleasePressed) grapple.Release(true);
            float dt = Ticks.Seconds;
            bool grounded = racer.Grounded;
            ApplyJump(racer, grounded, dt);
            TrackFrame frame = game.Track.Frame(transform.position);
            if (input.ChainActionPressed) ChainAction(input.Coasting);
            if (input.SwingPressed) TryHookPole();
            // A swing lasts while the swing key is held. A press counts as held for its own tick, so a tap that comes and
            // goes between two ticks still swings for one tick.
            bool swingHeld = input.SwingHeld || input.SwingPressed;
            if (racer.SwingTicks > 0 && (!swingHeld || grapple.IsAttached)) PoleSwing.End(racer, racer.Velocity, racer.Heading);
            bool coasting = input.Coasting;
            // Backing up is coasting with the down arrow, so it cuts a booster too.
            bool reversing = coasting && input.Reversing;
            if (coasting) Slingshot.Cut(racer);
            bool swinging = racer.SwingTicks > 0;
            bool pulling = Slingshot.IsPulling(racer.SlingTicks, sling);
            bool carrying = racer.SlingTicks > 0 && !pulling;
            float turnLimit = RunDrive.Turn(racer, grounded, swinging, reversing, dt, drive);
            float hookSpeedBefore = racer.SwingHookSpeed;
            PoleSwing.SpeedUp(racer, swinging, coasting, pulling, carrying, dt, SwingSettings(), sling);
            float boostRise = pulling || carrying ? racer.SwingHookSpeed - hookSpeedBefore : 0f;
            if (pulling && !swinging) slingTarget = Slingshot.SteerToward(racer, game.Track, frame, transform.position, dt, sling);
            var now = new RunDrive.Conditions
            {
                Grounded = grounded,
                Swinging = swinging,
                Coasting = coasting,
                Reversing = reversing,
                Pulling = pulling,
                Carrying = carrying,
                GrappleAttached = grapple.IsAttached,
            };
            RunDrive.Drive(racer, frame.Grade, now, turnLimit, dt, drive, sling);
            MoveAndCollide(racer, frame, grounded, swinging, boostRise, dt);
            CountDownAndCheckFall(racer);
        }

        // Coyote time, then a queued jump if it still counts.
        private void ApplyJump(RacerState racer, bool grounded, float dt)
        {
            ref Vector3 velocity = ref racer.Velocity;
            ref float coyoteTime = ref racer.CoyoteTime;
            coyoteTime = grounded ? CoyoteSeconds : Mathf.Max(0f, coyoteTime - dt);
            if (grounded && velocity.y < 0f) velocity.y = -2f;
            if (!racer.JumpQueued) return;
            if (coyoteTime > 0f)
            {
                velocity.y = jumpSpeed;
                coyoteTime = 0f;
                game.PlayCue(RaceCue.Jump);
            }
            racer.JumpQueued = false;
        }

        // Moves the capsule by this tick's velocity, held by the grapple's rope and the swing, then settles the contacts:
        // the ceiling, a guard, a kicker's crest, the ground and the grapple.
        private void MoveAndCollide(RacerState racer, TrackFrame frame, bool grounded, bool swinging, float boostRise, float dt)
        {
            ref Vector3 velocity = ref racer.Velocity;
            ref float heading = ref racer.Heading;
            Vector3 before = transform.position;
            Vector3 displacement = velocity * dt;
            grapple.ConstrainMotion(transform.position, ref displacement, ref velocity, dt);
            if (swinging) PoleSwing.Step(racer, transform.position, ref velocity, ref heading, ref displacement, boostRise, dt, SwingSettings(), out swingAnchor);
            hitGuard = false;
            CollisionFlags flags = controller.Move(displacement);
            SyncGrounded();
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
            if (hitGuard)
            {
                // A swing that carries the runner into a guard snaps there, without the carry.
                racer.SwingTicks = 0;
                GuardContact.Glance(ref velocity, ref heading, guardNormal, frame.Forward, guardSpeedLoss);
                if (guardScrapeDecel > 0f) GuardContact.Scrape(ref velocity, guardScrapeDecel, dt);
            }
            // A runner that ran over a kicker's lip this tick leaves the road climbing by its forward speed (user 2026-10-25,
            // Finger Lanes: the faster, the farther); the up speed also keeps the ground snap below from pulling it back.
            // The lip is found by the S moved along the frame, so nothing new is kept between ticks.
            if (grounded && !swinging && !grapple.IsAttached
                && game.Track.TryLaunchBetween(frame.S, frame.S + Vector3.Dot(transform.position - before, frame.Forward), out float lift))
                velocity.y = Mathf.Max(velocity.y, lift * Mathf.Max(0f, velocity.x * frame.Forward.x + velocity.z * frame.Forward.z));
            // A pad uses the same S moved as a kicker. Only the horizontal facing component rises; sideways and vertical
            // speed stay. The speed it gives is kept as the pad's carry (RacerState.BoostZoneCarrySpeed), which the next
            // ticks' drive wears off (RunDrive.BoostZoneTarget).
            if (grounded && !swinging && !grapple.IsAttached
                && game.Track.TryBoostBetween(frame.S, frame.S + Vector3.Dot(transform.position - before, frame.Forward)))
            {
                Slingshot.Cut(racer);
                Vector3 facing = Facing;
                float forwardSpeed = Vector3.Dot(velocity, facing);
                float boosted = Mathf.Max(forwardSpeed, BoostZoneSpeed);
                velocity += facing * (boosted - forwardSpeed);
                racer.BoostZoneCarrySpeed = boosted;
            }
            if (grounded && !racer.Grounded && velocity.y <= 0f && !grapple.IsAttached) SnapToGround();
            if (racer.Grounded && grapple.IsAttached) grapple.Release(false);
            if (racer.Grounded) racer.ClearAirGrapple();
            if (grapple.IsAttached && Vector3.Dot(transform.position - grapple.AnchorPosition, frame.Forward) > 0.5f)
                grapple.Release(true);
        }

        // Counts the slingshot down and the swing up, then checks for a fall. Falling is measured from the track surface,
        // so a long downhill is not a fall. A circuit race puts the runner back on the track; the other courses end the run.
        private void CountDownAndCheckFall(RacerState racer)
        {
            if (racer.SlingTicks > 0) racer.SlingTicks--;
            if (racer.SwingTicks > 0) racer.SwingTicks++;
            TrackCoord here = game.Track.Project(transform.position);
            if (here.H < -FallDepth)
            {
                if (game.IsCircuit) RespawnOnTrack();
                else game.FailRun();
                return;
            }
            if (racer.Grounded) racer.LastGroundedS = here.S;
        }

        // The values the motion rules run on, read from the serialized fields on every call so a change made in the
        // inspector or by a test applies at once. Speeds come scaled by the runner's profile.
        private RunDrive.Settings DriveSettings() => new RunDrive.Settings
        {
            RunSpeed = RunSpeed,
            GrappleReelSpeed = GrappleReelSpeed * speedClassScale,
            RunAcceleration = RunAcceleration,
            MaxTurnRate = maxTurnRate,
            TurnAcceleration = turnAcceleration,
            AirTurnScale = airTurnScale,
            MinTurnRadius = minTurnRadius,
            TurnSpeedLoss = turnSpeedLoss,
            CoastDeceleration = coastDeceleration,
            BrakeDeceleration = brakeDeceleration,
            ReverseSpeed = reverseSpeed,
            ReverseAcceleration = reverseAcceleration,
            AirDeceleration = airDeceleration,
            SideGrip = sideGrip,
            AirSideGrip = airSideGrip,
            SlopeSpeedFactor = slopeSpeedFactor,
            MinSlopeSpeedScale = minSlopeSpeedScale,
            MaxSlopeSpeedScale = maxSlopeSpeedScale,
            OverspeedFalloff = overspeedFalloff,
            SwingCarryFalloff = swingCarryFalloff,
            BoostZoneFalloff = boostZoneFalloff,
            Gravity = gravity,
        };

        private Slingshot.Settings SlingSettings() => new Slingshot.Settings
        {
            Lead = slingLead,
            PullTime = slingPullTime,
            PullAcceleration = slingPullAcceleration,
            TopSpeed = SlingTopSpeed,
            TurnRate = slingTurnRate,
            CarryTime = slingCarryTime,
            CarrySpeed = SlingCarrySpeed,
            OverspeedFalloff = overspeedFalloff,
        };

        private PoleSwing.Settings SwingSettings() => new PoleSwing.Settings
        {
            RopeRate = ropeRate,
            MinRopeLength = minRopeLength,
            MinSpeedScale = minSwingSpeedScale,
            MaxSpeedScale = maxSwingSpeedScale,
            ThrottleSpeedScale = swingThrottleSpeedScale,
            MetersPerSlot = SwingMetersPerSlot,
            RunSpeed = RunSpeed,
            RunAcceleration = RunAcceleration,
        };

        // Puts a fallen runner on the centerline respawnBackoff metres before where it last stood, as high as the start
        // slot, facing along the track and at rest, and starts the hold. The chain gauge is kept; a grapple, slingshot or
        // swing ends without its boost. LastGroundedS stays, so a second fall before landing comes back to the same place.
        // Just after a boost pad it comes back before the pad (respawnPadReach).
        private void RespawnOnTrack()
        {
            RacerState racer = game.Racer;
            grapple.Release(false);
            double s = racer.LastGroundedS - respawnBackoff;
            if (game.Track.TryBoostZoneBehind(s, respawnPadReach, out double pad)) s = pad - respawnBackoff;
            TrackFrame frame = game.Track.FrameAt(s);
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
            racer.BoostZoneCarrySpeed = 0f;
            racer.Heading = Mathf.Atan2(frame.Forward.x, frame.Forward.z) * Mathf.Rad2Deg;
            racer.TurnRate = 0f;
            racer.RespawnHoldTicks = Ticks.FromSeconds(respawnHoldTime);
        }

        // One gauge slot buys the chain action that fits the moment: while grappling it empowers the grapple;
        // otherwise it fires the slingshot. Without a whole slot, or with that action already running, nothing
        // happens and nothing is spent. Corner pole swings are the swing key's, not the chain action's.
        // On a grapple the chain action empowers it; otherwise it fires the slingshot booster, which needs the throttle
        // (KartRider style, 2026-10-19): without it the press does nothing and the slot is kept.
        private void ChainAction(bool coasting)
        {
            RacerState racer = game.Racer;
            if (grapple.IsAttached)
            {
                if (racer.GrappleEmpowered || !racer.TrySpendGaugeSlot()) return;
                racer.EmpowerGrapple();
                game.PlayCue(RaceCue.Action);
                return;
            }
            if (Slingshot.TryFire(racer, coasting, SlingSettings())) game.PlayCue(RaceCue.Action);
        }

        // A swing key press hooks the corner pole in reach, if there is one, at the rope length and speed of this moment.
        // It costs nothing. In the air (a jump, or past the coyote time off a ledge), while grappling or already swinging,
        // a press does nothing. Step calls it after the jump, which clears the coyote time, so a press on the takeoff tick
        // does not hook either.
        private void TryHookPole()
        {
            RacerState racer = game.Racer;
            if (racer.CoyoteTime <= 0f || grapple.IsAttached || racer.SwingTicks > 0 || !TryFindPole(out Vector3 pole)) return;
            PoleSwing.Hook(racer, transform.position, pole);
            game.PlayCue(RaceCue.Action);
        }

        // The corner pole a swing key press would hook from here and now, if any (see poleMaxAngle, poleReach and poleHookArcShare). The
        // angle is taken from the track's direction here, not the facing: in a corner the facing falls behind the track (the
        // turn limit), which put the pole past the angle halfway round (measured 2026-10-25, DECISIONS "측정: 코너 중간에 …").
        // The simulation asks it on a press; presentation and bots may ask it any time, since it only reads.
        public bool TryFindPole(out Vector3 pole)
        {
            Vector3 forward = game.Track.Frame(transform.position).Forward;
            return game.Track.TryFindPole(transform.position, Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, poleMaxAngle, poleReach, out pole, poleHookArcShare);
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
            Vector3 facing = RunDrive.FacingOf(heading);
            Vector3 right = RunDrive.RightOf(heading);
            float forwardSpeed = Mathf.Max(Vector3.Dot(velocity, facing), empowered ? EmpoweredReleaseSpeed : ReleaseSpeed * speedClassScale);
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

        public void ShiftOrigin(Vector3 offset)
        {
            controller.enabled = false;
            transform.position += offset;
            controller.enabled = true;
            SyncGrounded();
            // A swing's pole is a world position like the track it came from (Centerline.ShiftOrigin moves that).
            if (game.Racer.SwingTicks > 0) game.Racer.SwingCenter += offset;
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
