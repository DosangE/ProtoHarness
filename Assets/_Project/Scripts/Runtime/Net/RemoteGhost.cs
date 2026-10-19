using System;
using ProtoHarness.ChainRush;
using UnityEngine;

namespace ProtoHarness.Net
{
    // Shows another racer as a copy of the local runner's model that follows a RemoteInterpolator, so both racers look
    // the same and the model has one source. The copy is made here, with no collider, so the scene needs no edit and the
    // ghost cannot touch the local runner. It is only a picture: the local simulation never reads it, and its pose stays
    // as copied (RunnerAnimation does not drive it). Once that racer's run has ended (a fall or a finish) it is hidden
    // for good (Hide).
    public sealed class RemoteGhost : MonoBehaviour
    {
        private RemoteInterpolator interpolator;
        private Transform body;
        private Vector3 lastPosition;
        private bool hasPosition;

        public Vector3 Position => body != null ? body.position : default;
        public bool HasPosition => hasPosition;
        // False once hidden; the model then stays where it was and no longer follows.
        public bool Visible => body != null && body.gameObject.activeSelf;

        // model: the local runner's visual (RunnerTilt.Body). The root follows the racer's position; the copy sits under it
        // where the model sits under the runner, facing the root's forward.
        public void Initialize(RemoteInterpolator interpolator, Transform model)
        {
            if (interpolator == null) throw new ArgumentNullException(nameof(interpolator));
            if (model == null) throw new ArgumentNullException(nameof(model));
            this.interpolator = interpolator;
            body = new GameObject("Remote racer").transform;
            body.SetParent(transform, false);
            GameObject copy = Instantiate(model.gameObject, body, false);
            copy.name = model.name;
            copy.transform.localPosition = model.localPosition;
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = model.localScale;
            copy.SetActive(true);
            // Visual only: it must never collide, even in the frame before the colliders are destroyed.
            foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Destroy(collider);
            }
        }

        // The racer's run has ended: its last position is no longer where it is. Safe to call again.
        public void Hide()
        {
            if (body == null) throw new InvalidOperationException("RemoteGhost: call Initialize before Hide.");
            body.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (interpolator == null || !Visible) return;
            interpolator.Advance(Time.deltaTime, 1f / Ticks.Seconds);
            if (!interpolator.TryEvaluate(out Vector3 position)) return;
            // Face the way it is moving; a standing racer keeps its last facing.
            Vector3 move = position - lastPosition;
            move.y = 0f;
            if (hasPosition && move.sqrMagnitude > 1e-6f) body.rotation = Quaternion.LookRotation(move);
            body.position = position;
            lastPosition = position;
            hasPosition = true;
        }
    }
}
