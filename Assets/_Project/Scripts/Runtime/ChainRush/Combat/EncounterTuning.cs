using UnityEngine;

namespace ProtoHarness.ChainRush.Combat
{
    [CreateAssetMenu(menuName = "ProtoHarness/ChainRush/Encounter Tuning")]
    public sealed class EncounterTuning : ScriptableObject
    {
        [SerializeField] private float warningDuration = 0.3f;
        [SerializeField] private float entranceDuration = 0.4f;
        [SerializeField] private float attackWindow = 1.2f;
        [SerializeField] private float flightDuration = 0.15f;
        [SerializeField] private float recoveryDuration = 0.3f;
        [SerializeField] private float gapStart = 2.5f;
        [SerializeField] private float gapMin = 0.6f;
        [SerializeField] private float gapDistanceScale = 1200f;

        public float WarningDuration => warningDuration;
        public float EntranceDuration => entranceDuration;
        public float AttackWindow => attackWindow;
        public float FlightDuration => flightDuration;
        public float RecoveryDuration => recoveryDuration;
        public float EncounterDuration => warningDuration + entranceDuration + attackWindow + flightDuration + recoveryDuration;
        public int WarningTicks => Ticks.FromSeconds(warningDuration);
        public int EntranceTicks => Ticks.FromSeconds(entranceDuration);
        public int AttackWindowTicks => Ticks.FromSeconds(attackWindow);
        public int FlightTicks => Ticks.FromSeconds(flightDuration);
        public int RecoveryTicks => Ticks.FromSeconds(recoveryDuration);
        public int NextGapTicks(double distance) => Ticks.FromSeconds(NextGap(distance));

        // Seconds to wait after an encounter ends; shrinks with distance down to gapMin.
        public float NextGap(double distance) => Mathf.Max(gapMin, gapStart - (float)distance / gapDistanceScale);

        private void OnValidate()
        {
            if (warningDuration <= 0f || entranceDuration <= 0f || attackWindow <= 0f || flightDuration <= 0f || recoveryDuration <= 0f)
                Debug.LogError("EncounterTuning: all encounter durations must be positive.", this);
            if (gapMin <= 0f || gapStart < gapMin || gapDistanceScale <= 0f)
                Debug.LogError("EncounterTuning: gap values must satisfy 0 < gapMin <= gapStart and gapDistanceScale > 0.", this);
        }
    }
}
