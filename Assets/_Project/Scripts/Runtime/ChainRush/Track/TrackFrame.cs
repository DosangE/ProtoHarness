using UnityEngine;

namespace ProtoHarness.ChainRush.Track
{
    // The track's local axes at one centerline point. Local vectors read (x = right, y = up, z = forward),
    // the same layout as a Transform, so world-axis code ports by swapping in these calls.
    // Up is world up and Forward stays horizontal: the track has no bank, and slope is carried as Grade
    // (rise over horizontal run) rather than by pitching the axes. Position includes the centerline height.
    public readonly struct TrackFrame
    {
        public readonly double S;
        public readonly Vector3 Position;
        public readonly Vector3 Forward;
        public readonly Vector3 Right;
        public readonly float Grade;
        // Signed 1/radius of the centerline here: positive turns right, negative left, zero on straights.
        public readonly float Curvature;

        public TrackFrame(double s, Vector3 position, Vector3 forward, Vector3 right, float grade, float curvature)
        {
            S = s;
            Position = position;
            Forward = forward;
            Right = right;
            Grade = grade;
            Curvature = curvature;
        }

        // Center of the arc this point lies on, at the centerline's height. Only arcs have one.
        public Vector3 CurveCenter
        {
            get
            {
                if (Curvature == 0f) throw new System.InvalidOperationException("A straight frame has no curve center.");
                return Position + Right / Curvature;
            }
        }

        public Vector3 TransformDirection(Vector3 local) => Right * local.x + Vector3.up * local.y + Forward * local.z;

        public Vector3 InverseTransformDirection(Vector3 world) =>
            new Vector3(Vector3.Dot(world, Right), world.y, Vector3.Dot(world, Forward));

        public Vector3 TransformPoint(Vector3 local) => Position + TransformDirection(local);

        public Vector3 InverseTransformPoint(Vector3 world) => InverseTransformDirection(world - Position);
    }
}
