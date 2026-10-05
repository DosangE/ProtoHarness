using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoHarness.ChainRush.Track
{
    // The track's centerline as a chain of straight pieces. S is absolute (double) so it keeps counting
    // across origin shifts and trimmed pieces; each piece stores its own world start, keeping float
    // offsets small. Positions before the first or past the last piece extend along that piece's
    // tangent, so a runner crossing the finish line still gains S.
    public sealed class Centerline
    {
        private struct Piece
        {
            public double StartS;
            public Vector3 Start;
            public Vector3 Forward;
            public Vector3 Right;
            public float Length;
        }

        private readonly List<Piece> pieces = new List<Piece>(16);
        private readonly Vector3 origin;
        private readonly float originYaw;
        private Vector3 end;
        private float endYaw;
        private double endS;

        public Centerline(Vector3 start, float yawDegrees)
        {
            if (!IsFinite(start)) throw new ArgumentOutOfRangeException(nameof(start), start, "Start must be finite.");
            if (!IsFinite(yawDegrees)) throw new ArgumentOutOfRangeException(nameof(yawDegrees), yawDegrees, "Yaw must be finite.");
            origin = start;
            originYaw = yawDegrees;
            Clear();
        }

        public int PieceCount => pieces.Count;
        public double StartS => pieces.Count > 0 ? pieces[0].StartS : endS;
        public double EndS => endS;

        // Back to the construction origin with no pieces; S restarts at zero.
        public void Clear()
        {
            pieces.Clear();
            end = origin;
            endYaw = originYaw;
            endS = 0d;
        }

        public void AppendStraight(float length)
        {
            if (!(length > 0f) || float.IsInfinity(length))
                throw new ArgumentOutOfRangeException(nameof(length), length, "Piece length must be positive and finite.");
            float radians = endYaw * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            var piece = new Piece
            {
                StartS = endS,
                Start = end,
                Forward = new Vector3(sin, 0f, cos),
                Right = new Vector3(cos, 0f, -sin),
                Length = length,
            };
            pieces.Add(piece);
            end = piece.Start + piece.Forward * length;
            endS += length;
        }

        // Drops leading pieces that end before s. The last piece always stays.
        public void TrimBefore(double s)
        {
            if (double.IsNaN(s)) throw new ArgumentOutOfRangeException(nameof(s), s, "S cannot be NaN.");
            int count = 0;
            while (count < pieces.Count - 1 && pieces[count].StartS + pieces[count].Length < s) count++;
            if (count > 0) pieces.RemoveRange(0, count);
        }

        public void ShiftOrigin(Vector3 offset)
        {
            if (!IsFinite(offset)) throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be finite.");
            for (int i = 0; i < pieces.Count; i++)
            {
                Piece piece = pieces[i];
                piece.Start += offset;
                pieces[i] = piece;
            }
            end += offset;
        }

        public TrackCoord Project(Vector3 world)
        {
            Piece piece = PieceAt(world, out float along);
            Vector3 relative = world - piece.Start;
            return new TrackCoord(piece.StartS + along, Vector3.Dot(relative, piece.Right), relative.y);
        }

        // The frame at the centerline point nearest to world.
        public TrackFrame Frame(Vector3 world)
        {
            Piece piece = PieceAt(world, out float along);
            return FrameOn(piece, along);
        }

        public TrackFrame FrameAt(double s)
        {
            if (double.IsNaN(s)) throw new ArgumentOutOfRangeException(nameof(s), s, "S cannot be NaN.");
            RequirePieces();
            int index = 0;
            while (index < pieces.Count - 1 && s > pieces[index].StartS + pieces[index].Length) index++;
            Piece piece = pieces[index];
            return FrameOn(piece, (float)(s - piece.StartS));
        }

        private static TrackFrame FrameOn(Piece piece, float along) =>
            new TrackFrame(piece.StartS + along, piece.Start + piece.Forward * along, piece.Forward, piece.Right);

        // The first piece whose span the point has not passed; before the first piece or past the
        // last, that end piece, with along running negative or beyond its length.
        private Piece PieceAt(Vector3 world, out float along)
        {
            if (!IsFinite(world)) throw new ArgumentOutOfRangeException(nameof(world), world, "Position must be finite.");
            RequirePieces();
            int last = pieces.Count - 1;
            for (int i = 0; i < last; i++)
            {
                along = Vector3.Dot(world - pieces[i].Start, pieces[i].Forward);
                if (along <= pieces[i].Length) return pieces[i];
            }
            along = Vector3.Dot(world - pieces[last].Start, pieces[last].Forward);
            return pieces[last];
        }

        private void RequirePieces()
        {
            if (pieces.Count == 0) throw new InvalidOperationException("Centerline has no pieces; append one before querying it.");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
