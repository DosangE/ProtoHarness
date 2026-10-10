using System;
using ProtoHarness.ChainRush;
using UnityEngine;

namespace ProtoHarness.Net
{
    // Shows another racer as a capsule that follows a RemoteInterpolator. The capsule is made here, with no collider, so
    // the scene needs no edit and the ghost cannot touch the local runner. It is only a picture: the local simulation never
    // reads it.
    public sealed class RemoteGhost : MonoBehaviour
    {
        private RemoteInterpolator interpolator;
        private Transform body;
        private Vector3 lastPosition;
        private bool hasPosition;

        public Vector3 Position => body != null ? body.position : default;
        public bool HasPosition => hasPosition;

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

        private void Update()
        {
            if (interpolator == null) return;
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
