using UnityEngine;
using ProtoHarness.ChainRush.Track;

namespace ProtoHarness.ChainRush
{
    [RequireComponent(typeof(Camera))]
    public sealed class FollowCamera : MonoBehaviour
    {
        // The point the camera looks at, relative to the runner and the camera's yaw.
        private static readonly Vector3 LookAhead = new Vector3(0f, 1.5f, 9f);
        [SerializeField] private RunnerMotor target;
        [SerializeField] private ChainRushGame game;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Vector3 offset = new Vector3(0f, 5.5f, -10f);
        // The camera turns toward the runner's facing no faster than this (degrees per second).
        [SerializeField] private float maxYawSpeed = 180f;
        private Vector3 smoothingVelocity;
        private float yaw;

        private void Awake()
        {
            if (target == null || game == null || viewCamera == null)
            {
                Debug.LogError("FollowCamera: target, game and camera must be assigned.", this);
                enabled = false;
                return;
            }
        }

        private void OnValidate()
        {
            if (maxYawSpeed <= 0f) Debug.LogError("FollowCamera: max yaw speed must be positive.", this);
        }

        // The game builds its track in Awake, so the first snap waits until every Awake has run.
        private void Start() => Snap();

        // Sits behind the runner's facing (free steering), never below the track surface plus the offset height.
        private void LateUpdate()
        {
            if (game.IsPaused) return;
            Vector3 targetPosition = target.transform.position;
            yaw = Mathf.MoveTowardsAngle(yaw, target.Heading, maxYawSpeed * Time.deltaTime);
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 desired = targetPosition + turn * offset;
            desired.y = Mathf.Max(game.Track.Frame(targetPosition).Position.y + offset.y, desired.y);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref smoothingVelocity, 0.16f);
            Vector3 lookPoint = targetPosition + turn * LookAhead;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(lookPoint - transform.position), 8f * Time.deltaTime);
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, 62f + Mathf.Min(target.Speed, 20f) * 0.5f, Time.deltaTime * 3f);
        }

        public void ShiftOrigin(Vector3 offset) => transform.position += offset;

        // Lines up behind the track direction at the runner, which is where a run starts facing.
        public void Snap()
        {
            smoothingVelocity = Vector3.zero;
            Vector3 targetPosition = target.transform.position;
            Vector3 forward = game.Track.Frame(targetPosition).Forward;
            yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            transform.position = targetPosition + turn * offset;
            transform.LookAt(targetPosition + turn * LookAhead);
        }
    }
}
