using UnityEngine;

namespace ProtoHarness.ChainRush.Visuals
{
    // Presentation only: leans the body from motor state. The motor never touches visuals, so it can run headless.
    // Runs before RunnerAnimation (120) and GrappleController (150) so the hand-mounted chain sees this frame's lean.
    [DefaultExecutionOrder(100)]
    public sealed class RunnerTilt : MonoBehaviour
    {
        [SerializeField] private RunnerMotor motor;
        [SerializeField] private Transform body;

        private void Awake()
        {
            if (motor == null || body == null)
            {
                Debug.LogError("RunnerTilt: motor and body must be assigned.", this);
                enabled = false;
            }
        }

        // Motor state only changes on ticks, so recomputing every frame yields the tick's value.
        private void LateUpdate() => body.localRotation = Evaluate(motor.Velocity, motor.Steer);

        public static Quaternion Evaluate(Vector3 velocity, float steer)
            => Quaternion.Euler(velocity.y * -0.9f, steer * 12f, steer * -16f);
    }
}
