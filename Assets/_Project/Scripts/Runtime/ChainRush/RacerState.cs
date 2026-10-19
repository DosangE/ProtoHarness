using System;
using UnityEngine;

namespace ProtoHarness.ChainRush
{
    // One racer's simulation state, kept apart from the session so a race can hold several racers.
    // Combat times are tick deadlines; every query takes the current tick instead of reading a clock.
    // Position stays on the Transform/CharacterController for now.
    public sealed class RacerState
    {
        public const int NoAnchor = -1;
        // The chain gauge holds up to two slots (KartRider keeps two boosters); one chain action costs one.
        public const float MaxGauge = 2f;

        private readonly RunRules rules;
        private float gauge;
        private int slingTicks;
        private int swingTicks;
        private float swingRadius;
        private Vector3 swingCenter;
        private float swingHookSpeed;
        private float swingHookRadius;
        private float swingCarrySpeed;
        private bool grappleEmpowered;
        private bool airGrappleUsed;
        private int health;
        private int hits;
        private int grapples;
        private int damageUntilTick;
        private int attackUntilTick;
        private int nextAttackTick;
        private Vector3 velocity;
        private float steer;
        private float heading;
        private float turnRate;
        private bool jumpQueued;
        private float coyoteTime;
        private int anchorIndex;
        private float ropeLength;
        private int missUntilTick;
        private bool grounded;
        private double lastGroundedS;
        private int respawnHoldTicks;

        // Every field of this class except the rules, by value (the guard test in SnapshotTests keeps the two in step).
        // Treat it as read-only. A copy taken between ticks puts the racer back exactly where it was.
        public struct Snapshot
        {
            public float Gauge;
            public int SlingTicks;
            public int SwingTicks;
            public float SwingRadius;
            public Vector3 SwingCenter;
            public float SwingHookSpeed;
            public float SwingHookRadius;
            public float SwingCarrySpeed;
            public bool GrappleEmpowered;
            public bool AirGrappleUsed;
            public int Health;
            public int Hits;
            public int Grapples;
            public int DamageUntilTick;
            public int AttackUntilTick;
            public int NextAttackTick;
            public Vector3 Velocity;
            public float Steer;
            public float Heading;
            public float TurnRate;
            public bool JumpQueued;
            public float CoyoteTime;
            public int AnchorIndex;
            public float RopeLength;
            public int MissUntilTick;
            public bool Grounded;
            public double LastGroundedS;
            public int RespawnHoldTicks;
        }

        public RacerState(RunRules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            this.rules = rules;
            Reset();
        }

        public int Health => health;
        public int Hits => hits;
        public int Grapples => grapples;
        public bool IsDown => health <= 0;

        // Motion is exposed by ref: the motor writes velocity components in place, and the grapple's
        // mid-tick release boost must hit the same memory the motor is integrating.
        public ref Vector3 Velocity => ref velocity;
        public ref float Steer => ref steer;
        // Facing as world yaw in degrees (0 = +z, positive turns right) and its rate in degrees per second.
        // Steering turns the facing; running follows it.
        public ref float Heading => ref heading;
        public ref float TurnRate => ref turnRate;
        public ref bool JumpQueued => ref jumpQueued;
        public ref float CoyoteTime => ref coyoteTime;
        public ref float RopeLength => ref ropeLength;
        // Whether the runner stood on something after its last move. CharacterController.isGrounded is the result of
        // the last Move and cannot be set, so the motor copies it here after everything that moves or re-enables
        // the controller, and every simulation read goes through this copy. That makes it restorable.
        // Reset does not clear it: it is the controller's answer, not a rule of the run.
        public ref bool Grounded => ref grounded;
        // The track S where the runner last stood after a move; a circuit runner who falls comes back near it.
        public ref double LastGroundedS => ref lastGroundedS;
        // Ticks left in which a runner just put back on the track stands still and ignores its controls; zero otherwise.
        public ref int RespawnHoldTicks => ref respawnHoldTicks;

        // Index into the grapple's anchor array rather than a Transform, so the state stays plain data.
        public int AnchorIndex => anchorIndex;
        public bool HasAnchor => anchorIndex != NoAnchor;
        public int MissUntilTick => missUntilTick;

        public float Gauge => gauge;
        // Ticks left in the current chain slingshot (pull, then carry); zero when none is running.
        public ref int SlingTicks => ref slingTicks;
        // Ticks the current corner pole swing has lasted so far (zero when none; it has no time limit), its rope length
        // now (the level distance to the pole when it hooked, then shortened or let out by steering, NEXT_REQUESTS.md R4b)
        // and the pole (an arc's center, PROPOSAL_CORNER_POLE.md 2-1), moved with the world on an origin shift.
        public ref int SwingTicks => ref swingTicks;
        public ref float SwingRadius => ref swingRadius;
        public ref Vector3 SwingCenter => ref swingCenter;
        // The speed and rope length at the moment the swing hooked; the swing's speed is worked out from them.
        public ref float SwingHookSpeed => ref swingHookSpeed;
        public ref float SwingHookRadius => ref swingHookRadius;
        // After a swing lets go, the speed it carried above running speed, falling off over time; zero when none.
        public ref float SwingCarrySpeed => ref swingCarrySpeed;
        // The current grapple was empowered by a chain action: faster reel, stronger release.
        public bool GrappleEmpowered => grappleEmpowered;
        // A grapple was hooked since the runner last stood on something: one grapple per time in the air.
        public bool AirGrappleUsed => airGrappleUsed;

        public void AddGauge(float slots)
        {
            if (!(slots >= 0f) || float.IsInfinity(slots))
                throw new ArgumentOutOfRangeException(nameof(slots), slots, "Gauge gain must be finite and not negative.");
            gauge = Mathf.Min(MaxGauge, gauge + slots);
        }

        // Spends one whole slot; returns false and spends nothing when less than one is stored.
        public bool TrySpendGaugeSlot()
        {
            if (gauge < 1f) return false;
            gauge -= 1f;
            return true;
        }

        public void EmpowerGrapple()
        {
            if (!HasAnchor) throw new InvalidOperationException("Only an attached grapple can be empowered.");
            grappleEmpowered = true;
        }

        public void Reset()
        {
            health = rules.MaxHealth;
            hits = 0;
            grapples = 0;
            damageUntilTick = 0;
            attackUntilTick = 0;
            nextAttackTick = 0;
            velocity = Vector3.zero;
            steer = 0f;
            heading = 0f;
            turnRate = 0f;
            gauge = 0f;
            slingTicks = 0;
            swingTicks = 0;
            swingRadius = 0f;
            swingCenter = Vector3.zero;
            swingHookSpeed = 0f;
            swingHookRadius = 0f;
            swingCarrySpeed = 0f;
            grappleEmpowered = false;
            airGrappleUsed = false;
            jumpQueued = false;
            coyoteTime = 0f;
            anchorIndex = NoAnchor;
            ropeLength = 0f;
            missUntilTick = 0;
            lastGroundedS = 0d;
            respawnHoldTicks = 0;
        }

        public Snapshot Capture() => new Snapshot
        {
            Gauge = gauge,
            SlingTicks = slingTicks,
            SwingTicks = swingTicks,
            SwingRadius = swingRadius,
            SwingCenter = swingCenter,
            SwingHookSpeed = swingHookSpeed,
            SwingHookRadius = swingHookRadius,
            SwingCarrySpeed = swingCarrySpeed,
            GrappleEmpowered = grappleEmpowered,
            AirGrappleUsed = airGrappleUsed,
            Health = health,
            Hits = hits,
            Grapples = grapples,
            DamageUntilTick = damageUntilTick,
            AttackUntilTick = attackUntilTick,
            NextAttackTick = nextAttackTick,
            Velocity = velocity,
            Steer = steer,
            Heading = heading,
            TurnRate = turnRate,
            JumpQueued = jumpQueued,
            CoyoteTime = coyoteTime,
            AnchorIndex = anchorIndex,
            RopeLength = ropeLength,
            MissUntilTick = missUntilTick,
            Grounded = grounded,
            LastGroundedS = lastGroundedS,
            RespawnHoldTicks = respawnHoldTicks,
        };

        public void Restore(in Snapshot snapshot)
        {
            gauge = snapshot.Gauge;
            slingTicks = snapshot.SlingTicks;
            swingTicks = snapshot.SwingTicks;
            swingRadius = snapshot.SwingRadius;
            swingCenter = snapshot.SwingCenter;
            swingHookSpeed = snapshot.SwingHookSpeed;
            swingHookRadius = snapshot.SwingHookRadius;
            swingCarrySpeed = snapshot.SwingCarrySpeed;
            grappleEmpowered = snapshot.GrappleEmpowered;
            airGrappleUsed = snapshot.AirGrappleUsed;
            health = snapshot.Health;
            hits = snapshot.Hits;
            grapples = snapshot.Grapples;
            damageUntilTick = snapshot.DamageUntilTick;
            attackUntilTick = snapshot.AttackUntilTick;
            nextAttackTick = snapshot.NextAttackTick;
            velocity = snapshot.Velocity;
            steer = snapshot.Steer;
            heading = snapshot.Heading;
            turnRate = snapshot.TurnRate;
            jumpQueued = snapshot.JumpQueued;
            coyoteTime = snapshot.CoyoteTime;
            anchorIndex = snapshot.AnchorIndex;
            ropeLength = snapshot.RopeLength;
            missUntilTick = snapshot.MissUntilTick;
            grounded = snapshot.Grounded;
            lastGroundedS = snapshot.LastGroundedS;
            respawnHoldTicks = snapshot.RespawnHoldTicks;
        }

        public void Attach(int index, float length)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index), index, "Anchor index cannot be negative.");
            if (!(length > 0f)) throw new ArgumentOutOfRangeException(nameof(length), length, "Rope length must be positive.");
            anchorIndex = index;
            ropeLength = length;
            airGrappleUsed = true;
        }

        // Standing on something again gives the next time in the air its grapple back.
        public void ClearAirGrapple() => airGrappleUsed = false;

        public void Detach()
        {
            anchorIndex = NoAnchor;
            grappleEmpowered = false;
        }
        public void MarkMiss(int untilTick) => missUntilTick = RequireTick(untilTick);
        public void ClearMiss() => missUntilTick = 0;

        public bool IsInvulnerable(int tick) => RequireTick(tick) < damageUntilTick;

        // Returns false when the hit landed inside the invulnerability window and was ignored.
        public bool TryTakeDamage(int tick)
        {
            if (IsInvulnerable(tick)) return false;
            health--;
            damageUntilTick = tick + rules.DamageInvulnerabilityTicks;
            return true;
        }

        public bool CanAttack(int tick) => RequireTick(tick) >= nextAttackTick;
        public void BeginAttackCooldown(int tick) => nextAttackTick = RequireTick(tick) + rules.AttackCooldownTicks;
        public void ShowAttack(int tick) => attackUntilTick = RequireTick(tick) + rules.AttackVisualTicks;
        public bool IsAttackShown(int tick) => RequireTick(tick) < attackUntilTick;
        public int AttackTicksLeft(int tick) => Math.Max(0, attackUntilTick - RequireTick(tick));

        public void AddHit() => hits++;
        public void AddGrapple() => grapples++;

        private static int RequireTick(int tick)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "Tick cannot be negative.");
            return tick;
        }
    }
}
