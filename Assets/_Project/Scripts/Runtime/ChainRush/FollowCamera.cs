using UnityEngine;
using ProtoHarness.ChainRush.Track;

namespace ProtoHarness.ChainRush
{
    [RequireComponent(typeof(Camera))]
    public sealed class FollowCamera : MonoBehaviour
    {
        // The point the camera looks at, relative to the runner in track terms.
        private static readonly Vector3 LookAhead = new Vector3(0f, 1.5f, 9f);
        [SerializeField] private RunnerMotor target;
        [SerializeField] private ChainRushGame game;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Vector3 offset = new Vector3(0f, 5.5f, -10f);
        private Vector3 smoothingVelocity;

        private void Awake()
        {
            if (target == null || game == null || viewCamera == null)
            {
                Debug.LogError("FollowCamera: target, game and camera must be assigned.", this);
                enabled = false;
                return;
            }
        }

        // The game builds its track in Awake, so the first snap waits until every Awake has run.
        private void Start() => Snap();

        private void LateUpdate()
        {
            if (game.IsPaused) return;
            Vector3 targetPosition = target.transform.position;
            TrackFrame frame = game.Track.Frame(targetPosition);
            // Offsets are in track terms: x = right of the centerline, y = up, z = along the track.
            Vector3 local = frame.InverseTransformPoint(targetPosition);
            Vector3 desired = local + offset;
            desired.x = local.x * 0.55f;
            desired.y = Mathf.Max(5.5f, desired.y);
            transform.position = Vector3.SmoothDamp(transform.position, frame.TransformPoint(desired), ref smoothingVelocity, 0.16f);
            Vector3 lookPoint = targetPosition + frame.TransformDirection(LookAhead);
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(lookPoint - transform.position), 8f * Time.deltaTime);
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, 62f + Mathf.Min(target.Speed, 20f) * 0.5f, Time.deltaTime * 3f);
        }

        public void ShiftOrigin(Vector3 offset) => transform.position += offset;

        public void Snap()
        {
            smoothingVelocity = Vector3.zero;
            Vector3 targetPosition = target.transform.position;
            TrackFrame frame = game.Track.Frame(targetPosition);
            transform.position = targetPosition + frame.TransformDirection(offset);
            transform.LookAt(targetPosition + frame.TransformDirection(LookAhead));
        }
    }
}
