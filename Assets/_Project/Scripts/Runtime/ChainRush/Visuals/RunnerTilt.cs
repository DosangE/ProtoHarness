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
        private float swingBlend;
        private float insideSign = 1f;
        // The body's renderers that were showing when it woke, which the respawn blink turns off and on (RespawnLook).
        // Renderers made later (the chain's mesh) or hidden by their owner then are never touched.
        private Renderer[] blinking;
        private bool blinkHidden;
        private RespawnLook.Watch respawn;

        // The runner's visual model (also what a network race copies to draw the other racer, RemoteGhost).
        public Transform Body => body;
        // Whether the respawn blink has the model hidden right now.
        public bool BlinkHidden => blinkHidden;

        private void Awake()
        {
            if (motor == null || body == null)
            {
                Debug.LogError("RunnerTilt: motor and body must be assigned.", this);
                enabled = false;
                return;
            }
            Renderer[] all = body.GetComponentsInChildren<Renderer>(true);
            int showing = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].enabled) all[showing++] = all[i];
            blinking = new Renderer[showing];
            System.Array.Copy(all, blinking, showing);
        }

        // Motor state only changes on ticks, so recomputing every frame yields the tick's value.
        // World rotation, so a root already turned in the scene (the circuit's start line) is not added on top.
        // A corner swing slews the body into the curve like a drifting kart (SwingLook), blended in and out.
        private void LateUpdate()
        {
            bool swinging = motor.IsSwinging;
            if (swinging) insideSign = SwingLook.InsideSign(motor.transform.position, motor.Heading, motor.SwingAnchor);
            swingBlend = SwingLook.Blend(swingBlend, swinging, Time.deltaTime);
            float slip = insideSign * SwingLook.SlipAngle * swingBlend;
            body.rotation = Quaternion.Euler(0f, motor.Heading + slip, 0f) * Evaluate(motor.Velocity, motor.Steer);
            bool held = motor.IsRespawning;
            respawn.Update(held, Time.time);
            SetBlinkHidden(held && !RespawnLook.Shown(respawn.Since(Time.time)));
        }

        // A model never stays hidden because this stopped mid-blink.
        private void OnDisable()
        {
            if (blinking != null) SetBlinkHidden(false);
        }

        private void SetBlinkHidden(bool hidden)
        {
            if (hidden == blinkHidden) return;
            blinkHidden = hidden;
            for (int i = 0; i < blinking.Length; i++) blinking[i].enabled = !hidden;
        }

        public static Quaternion Evaluate(Vector3 velocity, float steer)
            => Quaternion.Euler(velocity.y * -0.9f, steer * 12f, steer * -16f);
    }
}
