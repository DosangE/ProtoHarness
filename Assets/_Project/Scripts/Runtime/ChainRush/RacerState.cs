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
        private bool grappleEmpowered;
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

        // Index into the grapple's anchor array rather than a Transform, so the state stays plain data.
        public int AnchorIndex => anchorIndex;
        public bool HasAnchor => anchorIndex != NoAnchor;
        public int MissUntilTick => missUntilTick;

        public float Gauge => gauge;
        // Ticks left in the current chain slingshot (pull, then carry); zero when none is running.
        public ref int SlingTicks => ref slingTicks;
        // Ticks left in the current corner swing (zero when none) and its rope length, signed by the turn:
        // positive swings around a right-hand curve's center, negative a left-hand one.
        public ref int SwingTicks => ref swingTicks;
        public ref float SwingRadius => ref swingRadius;
        // The current grapple was empowered by a chain action: faster reel, stronger release.
        public bool GrappleEmpowered => grappleEmpowered;

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
            grappleEmpowered = false;
            jumpQueued = false;
            coyoteTime = 0f;
            anchorIndex = NoAnchor;
            ropeLength = 0f;
            missUntilTick = 0;
        }

        public void Attach(int index, float length)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index), index, "Anchor index cannot be negative.");
            if (!(length > 0f)) throw new ArgumentOutOfRangeException(nameof(length), length, "Rope length must be positive.");
            anchorIndex = index;
            ropeLength = length;
        }

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
