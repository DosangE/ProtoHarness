using System;
using ProtoHarness.ChainRush;
using UnityEngine;

namespace ProtoHarness.Net
{
    // Shows another racer as a capsule that follows a RemoteInterpolator. The capsule is made here, with no collider, so
    // the scene needs no edit and the ghost cannot touch the local runner. It is only a picture: the local simulation never
    // reads it. Once that racer's run has ended (a fall or a finish) it is hidden for good (Hide).
    public sealed class RemoteGhost : MonoBehaviour
    {
        private RemoteInterpolator interpolator;
        private Transform body;
        private Vector3 lastPosition;
        private bool hasPosition;

        public Vector3 Position => body != null ? body.position : default;
        public bool HasPosition => hasPosition;
        // False once hidden; the capsule then stays where it was and no longer follows.
        public bool Visible => body != null && body.gameObject.activeSelf;

        public void Initialize(RemoteInterpolator interpolator)
        {
            this.interpolator = interpolator ?? throw new ArgumentNullException(nameof(interpolator));
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Remote racer";
            // Visual only: it must never collide, even in the frame before the collider is destroyed.
            Collider collider = capsule.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            body = capsule.transform;
            body.SetParent(transform, false);
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
