using UnityEngine;

namespace ProtoHarness.ChainRush
{
    // One runner's character (DESIGN.md §5-1 M2, DECISIONS 2026-10-19 "M2-b 러너 프로필"): scales on the runner values
    // saved in the scene's RunnerMotor, so a profile of all 1s runs exactly as the scene does (x * 1f is x).
    // RunnerMotor.Configure takes it before a run.
    [CreateAssetMenu(menuName = "ProtoHarness/ChainRush/Runner Profile")]
    public sealed class RunnerProfile : ScriptableObject
    {
        [SerializeField] private string displayName = "RUNNER";
        [Tooltip("Run speed and the booster speeds (slingshot pull and carry, empowered release).")]
        [SerializeField] private float topSpeedScale = 1f;
        [Tooltip("Grounded run acceleration.")]
        [SerializeField] private float accelerationScale = 1f;
        [Tooltip("How fast swinging fills the gauge: 1.2 fills a slot in 1/1.2 of the metres.")]
        [SerializeField] private float gaugeChargeScale = 1f;
        [Tooltip("For contact between runners (M7 C2). No effect until then.")]
        [SerializeField] private float weight = 1f;
        // Presentation only: RaceAssembler copies its meshes and materials onto the race's runner model (RunnerLook);
        // the simulation never reads it. Empty means the scene's model as saved, by definition (not a fallback).
        [Tooltip("The runner's look: a model with the same hierarchy as the scene's runner model (Art/Models/Runner*.prefab). Empty keeps the scene's model as saved.")]
        [SerializeField] private GameObject look;

        public string DisplayName => displayName;
        public float TopSpeedScale => topSpeedScale;
        public float AccelerationScale => accelerationScale;
        public float GaugeChargeScale => gaugeChargeScale;
        public float Weight => weight;
        public GameObject Look => look;

        // Whether every value is usable; RunnerMotor.Configure refuses a profile that is not.
        public bool TryValidate(out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(displayName)) error = $"RunnerProfile '{name}': a display name is required.";
            else if (!(topSpeedScale > 0f) || !(accelerationScale > 0f) || !(gaugeChargeScale > 0f) || !(weight > 0f))
                error = $"RunnerProfile '{name}': every scale and the weight must be positive.";
            return error == null;
        }

        private void OnValidate()
        {
            if (!TryValidate(out string error)) Debug.LogError(error, this);
        }
    }
}
