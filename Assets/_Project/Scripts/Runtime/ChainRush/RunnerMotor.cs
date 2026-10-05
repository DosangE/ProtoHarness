using UnityEngine;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Track;

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

        // Motion state lives in the racer's RacerState; the motor only integrates it.
        public Vector3 Velocity => game.Racer.Velocity;
        public float Steer => game.Racer.Steer;
        public bool IsGrounded => controller.isGrounded;
        public float Speed => game.Racer.Velocity.magnitude;

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
            RacerState racer = game.Racer;
            if (IsGrounded || racer.CoyoteTime > 0f) racer.JumpQueued = true;
            else grapple.TryAttach();
        }

        // One simulation tick, called only by ChainRushGame.FixedUpdate.
        public void Step(in TickInput input)
        {
            if (!game.IsRunning) return;
            RacerState racer = game.Racer;
            ref Vector3 velocity = ref racer.Velocity;
            ref float steer = ref racer.Steer;
            ref bool jumpQueued = ref racer.JumpQueued;
            ref float coyoteTime = ref racer.CoyoteTime;
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
            // Steering and running act along the track (x = right, z = forward), not world x/z.
            TrackFrame frame = game.Track.Frame(transform.position);
            Vector3 local = frame.InverseTransformDirection(velocity);
            local.x = Mathf.MoveTowards(local.x, steer * lateralSpeed, (grounded ? 60f : 18f) * dt);
            local.z = Mathf.MoveTowards(local.z, grapple.IsAttached ? 16f : runSpeed,
                (grounded ? 30f : 5f) * dt);
            velocity = frame.TransformDirection(local);
            velocity.y = Mathf.Max(velocity.y - gravity * dt, -28f);
            Vector3 displacement = velocity * dt;
            grapple.ConstrainMotion(transform.position, ref displacement, ref velocity, dt);
            CollisionFlags flags = controller.Move(displacement);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
            if (controller.isGrounded && grapple.IsAttached) grapple.Release(false);
            if (grapple.IsAttached && Vector3.Dot(transform.position - grapple.AnchorPosition, frame.Forward) > 0.5f)
                grapple.Release(true);
            // Falling is measured from the track surface, so a long downhill is not a fall.
            if (game.Track.Project(transform.position).H < -12f) game.FailRun();
        }

        public void AddReleaseBoost()
        {
            // A full takeoff impulse keeps the capsule above the next platform lip.
            ref Vector3 velocity = ref game.Racer.Velocity;
            TrackFrame frame = game.Track.Frame(transform.position);
            Vector3 local = frame.InverseTransformDirection(velocity);
            local.y = Mathf.Max(local.y, jumpSpeed);
            local.z = Mathf.Max(local.z, 13f);
            velocity = frame.TransformDirection(local);
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
            RacerState racer = game.Racer;
            racer.Velocity = Vector3.zero;
            racer.Steer = 0f;
            racer.JumpQueued = false;
            racer.CoyoteTime = 0f;
        }
    }
}
