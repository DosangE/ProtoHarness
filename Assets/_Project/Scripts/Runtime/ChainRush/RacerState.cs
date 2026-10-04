using System;

namespace ProtoHarness.ChainRush
{
    // One racer's combat state, kept apart from the session so a race can hold several racers.
    // Times are tick deadlines; every query takes the current tick instead of reading a clock.
    public sealed class RacerState
    {
        private readonly RunRules rules;
        private int health;
        private int hits;
        private int grapples;
        private int damageUntilTick;
        private int attackUntilTick;
        private int nextAttackTick;

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

        public void Reset()
        {
            health = rules.MaxHealth;
            hits = 0;
            grapples = 0;
            damageUntilTick = 0;
            attackUntilTick = 0;
            nextAttackTick = 0;
        }

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
