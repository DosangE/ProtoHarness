using UnityEngine;
using ProtoHarness.ChainRush.Control;

namespace ProtoHarness.ChainRush
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class RunnerMotor : MonoBehaviour
    {
        [SerializeField] private CharacterController controller;
        [SerializeField] private ChainRushGame game;
        [SerializeField] private GrappleController grapple;
        [SerializeField] private float runSpeed = 10f;
        [SerializeField] private float jumpSpeed = 11.5f;
        [SerializeField] private float gravity = 22f;
        [SerializeField] private float lateralSpeed = 7f;
        private Vector3 spawnPosition;
        private Vector3 velocity;
        private float steer;
        private bool jumpQueued;
        private float coyoteTime;

        public Vector3 Velocity => velocity;
        public float Steer => steer;
        public bool IsGrounded => controller.isGrounded;
        public float Speed => velocity.magnitude;

        private void Awake()
        {
            if (controller == null || game == null || grapple == null)
            {
                Debug.LogError("RunnerMotor: controller, game and grapple must be assigned.", this);
                enabled = false;
                return;
            }
            spawnPosition = transform.position;
        }

        private void OnValidate()
        {
            if (runSpeed <= 0f || jumpSpeed <= 0f || gravity <= 0f || lateralSpeed <= 0f)
                Debug.LogError("RunnerMotor: movement settings must be positive.", this);
        }

        public void PrimaryAction()
        {
            if (!game.IsRunning) return;
            if (IsGrounded || coyoteTime > 0f) jumpQueued = true;
            else grapple.TryAttach();
        }

        // One simulation tick, called only by ChainRushGame.FixedUpdate.
        public void Step(in TickInput input)
        {
            if (!game.IsRunning) return;
            steer = input.Steer;
            if (input.PrimaryPressed) PrimaryAction();
            if (input.ReleasePressed) grapple.Release(true);
            float dt = Ticks.Seconds;
            bool grounded = controller.isGrounded;
            coyoteTime = grounded ? 0.1f : Mathf.Max(0f, coyoteTime - dt);
            if (grounded && velocity.y < 0f) velocity.y = -2f;
            if (jumpQueued)
            {
                if (coyoteTime > 0f)
                {
                    velocity.y = jumpSpeed;
                    coyoteTime = 0f;
                    game.PlayCue(0);
                }
                jumpQueued = false;
            }
            velocity.x = Mathf.MoveTowards(velocity.x, steer * lateralSpeed, (grounded ? 60f : 18f) * dt);
            velocity.z = Mathf.MoveTowards(velocity.z, grapple.IsAttached ? 16f : runSpeed,
                (grounded ? 30f : 5f) * dt);
            velocity.y = Mathf.Max(velocity.y - gravity * dt, -28f);
            Vector3 displacement = velocity * dt;
            grapple.ConstrainMotion(transform.position, ref displacement, ref velocity, dt);
            CollisionFlags flags = controller.Move(displacement);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
            if (controller.isGrounded && grapple.IsAttached) grapple.Release(false);
            if (grapple.IsAttached && transform.position.z > grapple.AnchorPosition.z + 0.5f)
                grapple.Release(true);
            if (transform.position.y < -12f) game.FailRun();
        }

        public void AddReleaseBoost()
        {
            // A full takeoff impulse keeps the capsule above the next platform lip.
            velocity.y = Mathf.Max(velocity.y, jumpSpeed);
            velocity.z = Mathf.Max(velocity.z, 13f);
        }

        public void ShiftOrigin(Vector3 offset)
        {
            controller.enabled = false;
            transform.position += offset;
            controller.enabled = true;
            grapple.ShiftOrigin(offset);
        }

        public void ResetAtSpawn()
        {
            grapple.Release(false);
            grapple.ClearMiss();
            controller.enabled = false;
            transform.position = spawnPosition;
            controller.enabled = true;
            controller.Move(Vector3.down * 0.3f);
            velocity = Vector3.zero;
            steer = 0f;
            jumpQueued = false;
            coyoteTime = 0f;
        }
    }
}
