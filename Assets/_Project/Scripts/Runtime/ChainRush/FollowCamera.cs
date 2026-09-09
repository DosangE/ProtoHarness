using UnityEngine;

namespace ProtoHarness.ChainRush
{
    [RequireComponent(typeof(Camera))]
    public sealed class FollowCamera : MonoBehaviour
    {
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
            Snap();
        }

        private void LateUpdate()
        {
            if (game.IsPaused) return;
            Vector3 desired = target.transform.position + offset;
            desired.x = target.transform.position.x * 0.55f;
            desired.y = Mathf.Max(5.5f, desired.y);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref smoothingVelocity, 0.16f);
            Vector3 lookPoint = target.transform.position + Vector3.up * 1.5f + Vector3.forward * 9f;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(lookPoint - transform.position), 8f * Time.deltaTime);
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, 62f + Mathf.Min(target.Speed, 20f) * 0.5f, Time.deltaTime * 3f);
        }

        public void ShiftOrigin(Vector3 offset) => transform.position += offset;

        public void Snap()
        {
            smoothingVelocity = Vector3.zero;
            transform.position = target.transform.position + offset;
            transform.LookAt(target.transform.position + Vector3.up * 1.5f + Vector3.forward * 9f);
        }
    }
}
