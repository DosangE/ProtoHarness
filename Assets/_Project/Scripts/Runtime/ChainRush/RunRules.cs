using UnityEngine;

namespace ProtoHarness.ChainRush
{
    [CreateAssetMenu(menuName = "ProtoHarness/ChainRush/Run Rules")]
    public sealed class RunRules : ScriptableObject
    {
        [SerializeField] private int maxHealth = 3;
        [SerializeField] private float damageInvulnerability = 1.25f;
        [SerializeField] private float attackCooldown = 0.35f;
        [SerializeField] private float attackVisualDuration = 0.18f;

        public int MaxHealth => maxHealth;
        public float DamageInvulnerability => damageInvulnerability;
        public float AttackCooldown => attackCooldown;
        public float AttackVisualDuration => attackVisualDuration;
        public int DamageInvulnerabilityTicks => Ticks.FromSeconds(damageInvulnerability);
        public int AttackCooldownTicks => Ticks.FromSeconds(attackCooldown);
        public int AttackVisualTicks => Ticks.FromSeconds(attackVisualDuration);

        private void OnValidate()
        {
            if (maxHealth < 1)
                Debug.LogError("RunRules: maxHealth must be at least 1.", this);
            if (damageInvulnerability < 0f || attackCooldown < 0f)
                Debug.LogError("RunRules: invulnerability and attack cooldown cannot be negative.", this);
            if (attackVisualDuration <= 0f)
                Debug.LogError("RunRules: attack visual duration must be positive.", this);
        }
    }
}
