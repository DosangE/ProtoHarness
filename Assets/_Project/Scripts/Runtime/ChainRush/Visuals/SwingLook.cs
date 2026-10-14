using System;
using UnityEngine;
using ProtoHarness.ChainRush.Track;

namespace ProtoHarness.ChainRush.Visuals
{
    // Presentation only: how a corner swing looks (docs/PROPOSAL_CORNER_SWING_LOOK.md S1). The chain bites the inside
    // guard rail beside the runner instead of the empty arc center, the body slews into the curve like a drifting kart,
    // and the camera swings out. Everything here reads motor and track state and never writes back, so the simulation
    // (replay hashes, rollback, netcode) cannot change.
    public static class SwingLook
    {
        // Degrees the body turns toward the inside of the curve while swinging.
        public const float SlipAngle = 25f;
        // Seconds for the pose to swing in and out, so it never snaps.
        public const float BlendTime = 0.15f;
        // Metres the camera moves toward the outside of the curve, and degrees of extra field of view.
        public const float CameraShift = 0.6f;
        public const float CameraFieldOfView = 4f;

        // +1 when the swing's center lies to the right of the heading, -1 when it lies to the left.
        public static float InsideSign(Vector3 position, float heading, Vector3 swingCenter)
        {
            Vector3 right = Quaternion.Euler(0f, heading, 0f) * Vector3.right;
            Vector3 toCenter = swingCenter - position;
            return toCenter.x * right.x + toCenter.z * right.z >= 0f ? 1f : -1f;
        }

        // The top of the inside guard rail beside this centerline point. Positive curvature turns right, so its
        // center (and the inside rail) is on the frame's right.
        public static Vector3 RailPoint(TrackFrame frame, float halfWidth)
        {
            if (frame.Curvature == 0f) throw new ArgumentException("A straight frame has no inside rail.", nameof(frame));
            if (halfWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(halfWidth), halfWidth, "Half width must be positive.");
            return frame.Position + frame.Right * (Mathf.Sign(frame.Curvature) * halfWidth) + Vector3.up * RoadMeshBuilder.GuardRailHeight;
        }

        // Moves a 0..1 blend toward 1 while swinging and back to 0 after, over BlendTime.
        public static float Blend(float current, bool swinging, float deltaTime)
            => Mathf.MoveTowards(current, swinging ? 1f : 0f, deltaTime / BlendTime);
    }
}
