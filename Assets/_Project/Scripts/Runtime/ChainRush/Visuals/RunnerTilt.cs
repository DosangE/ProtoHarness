using UnityEngine;

namespace ProtoHarness.ChainRush.Visuals
{
    // Presentation only: faces the body along the heading and leans it from motor state. The motor never touches
    // visuals, so it can run headless. Only the body turns: the root carries the physics capsule, and turning it
    // between ticks changes the physics results by an ULP, which breaks bit-for-bit replay and rollback.
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
        // World rotation, so a root already turned in the scene (the circuit's start line) is not added on top.
        private void LateUpdate()
            => body.rotation = Quaternion.Euler(0f, motor.Heading, 0f) * Evaluate(motor.Velocity, motor.Steer);

        public static Quaternion Evaluate(Vector3 velocity, float steer)
            => Quaternion.Euler(velocity.y * -0.9f, steer * 12f, steer * -16f);
    }
}
