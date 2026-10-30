using UnityEngine;

namespace ProtoHarness.ChainRush.Race
{
    // One track's own road and wall materials (user 2026-10-30, Palm Coast "트랙 서킷의 바닥이나 벽도 디자인"): the lobby's
    // track choice carries it to the race (RaceSetup.Look) and CircuitRace lays the road with it instead of the circuit
    // scene's materials. Presentation only: the simulation never reads it. A track without one keeps the scene's.
    [CreateAssetMenu(menuName = "ProtoHarness/ChainRush/Track Look")]
    public sealed class TrackLook : ScriptableObject
    {
        [Tooltip("The road surface (CircuitScenery's road pieces).")]
        [SerializeField] private Material road;
        [Tooltip("The guard walls on both edges of the road.")]
        [SerializeField] private Material rail;

        public Material Road => road;
        public Material Rail => rail;

        // Whether both materials are set; CircuitRace.UseLook refuses a look that is not.
        public bool TryValidate(out string error)
        {
            error = road == null || rail == null ? $"TrackLook '{name}': a road and a rail material are required." : null;
            return error == null;
        }

        private void OnValidate()
        {
            if (!TryValidate(out string error)) Debug.LogError(error, this);
        }
    }
}
