using UnityEngine;
using ProtoHarness.ChainRush.Audio;
using ProtoHarness.ChainRush.Combat;

namespace ProtoHarness.ChainRush.Visuals
{
    [DefaultExecutionOrder(120)]
    public sealed class RunnerAnimation : MonoBehaviour
    {
        [SerializeField] private ChainRushGame game;
        [SerializeField] private RunnerMotor player;
        [SerializeField] private GrappleController grapple;
        [SerializeField] private ChainRushAudio sound;
        [SerializeField] private Transform model;
        [SerializeField] private Transform torso;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        private Vector3 modelRest;
        private float phase;
        private float landing;
        private int lastStep;
        private bool wasGrounded;
        private float lastElapsed;
        public float GaitPhase => phase;
        public Quaternion RightArmPose => rightArm.localRotation;

        private void Awake()
        {
            if (game == null || player == null || grapple == null || sound == null || model == null || torso == null || leftArm == null || rightArm == null || leftLeg == null || rightLeg == null)
            {
                Debug.LogError("RunnerAnimation: all runtime and joint references are required.", this);
                enabled = false;
                return;
            }
            modelRest = model.localPosition;
        }

        private void LateUpdate()
        {
            if (game.IsPaused) return;
            if (game.Elapsed < lastElapsed) ResetPose();
            lastElapsed = game.Elapsed;
            if (!game.IsRunning)
            {
                if (game.HasFailed)
                {
                    torso.localRotation = Quaternion.Slerp(torso.localRotation, Quaternion.Euler(35f, 0f, -15f), Time.deltaTime * 5f);
                }
                return;
            }
            bool ground = player.IsGrounded;
            float dt = Time.deltaTime;
            if (ground && !wasGrounded && game.Elapsed > 0.2f) { landing = 1f; sound.PlayCue(8); }
            wasGrounded = ground;
            landing = Mathf.MoveTowards(landing, 0f, dt * 5f);
            if (ground) phase += dt * Mathf.Max(0f, player.ForwardSpeed) * 1.65f;
            int step = Mathf.FloorToInt(phase / Mathf.PI);
            if (ground && step != lastStep) { sound.PlayCue(7); lastStep = step; }
            float stride = Mathf.Sin(phase) * 38f;
            float armLeft = ground ? -stride * 0.8f - 12f : -35f;
            float armRight = ground ? stride * 0.8f - 12f : -35f;
            float legLeft = ground ? stride : -25f;
            float legRight = ground ? -stride : 18f;
            float lean = ground ? 8f : -5f;
            if (grapple.IsAttached) { armRight = -155f; armLeft = -75f; legLeft = -35f; legRight = 28f; lean = -12f; }
            var encounter = game.Enemies.State;
            if (encounter == EnemyDirector.EncounterState.Firing || encounter == EnemyDirector.EncounterState.Retracting)
            {
                armRight = -95f; armLeft = -40f; lean = encounter == EnemyDirector.EncounterState.Firing ? 16f : -12f;
            }
            float blend = 1f - Mathf.Exp(-dt * 18f);
            Pose(leftArm, armLeft, -7f, blend); Pose(rightArm, armRight, 7f, blend);
            Pose(leftLeg, legLeft + landing * 22f, 0f, blend); Pose(rightLeg, legRight + landing * 22f, 0f, blend);
            Pose(torso, lean + landing * 14f, -player.SideSpeed * 1.2f, blend);
            Vector3 position = modelRest;
            position.y += (ground ? Mathf.Abs(Mathf.Sin(phase)) * 0.045f : 0f) - landing * 0.1f;
            model.localPosition = position;
        }

        private static void Pose(Transform joint, float pitch, float roll, float blend)
            => joint.localRotation = Quaternion.Slerp(joint.localRotation, Quaternion.Euler(pitch, 0f, roll), blend);

        public void ResetPose()
        {
            phase = 0f; landing = 0f; lastStep = 0; wasGrounded = true; lastElapsed = 0f;
            model.localPosition = modelRest;
            torso.localRotation = leftArm.localRotation = rightArm.localRotation = leftLeg.localRotation = rightLeg.localRotation = Quaternion.identity;
        }
    }
}
