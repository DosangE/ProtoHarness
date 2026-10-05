using UnityEngine;

namespace ProtoHarness.ChainRush.Track
{
    // The track's local axes at one centerline point. Local vectors read (x = right, y = up, z = forward),
    // the same layout as a Transform, so world-axis code ports by swapping in these calls.
    // Up is world up: the track has no bank or pitch yet.
    public readonly struct TrackFrame
    {
        public readonly double S;
        public readonly Vector3 Position;
        public readonly Vector3 Forward;
        public readonly Vector3 Right;

        public TrackFrame(double s, Vector3 position, Vector3 forward, Vector3 right)
        {
            S = s;
            Position = position;
            Forward = forward;
            Right = right;
        }

        public Vector3 TransformDirection(Vector3 local) => Right * local.x + Vector3.up * local.y + Forward * local.z;

        public Vector3 InverseTransformDirection(Vector3 world) =>
            new Vector3(Vector3.Dot(world, Right), world.y, Vector3.Dot(world, Forward));

        public Vector3 TransformPoint(Vector3 local) => Position + TransformDirection(local);

        public Vector3 InverseTransformPoint(Vector3 world) => InverseTransformDirection(world - Position);
    }
}
