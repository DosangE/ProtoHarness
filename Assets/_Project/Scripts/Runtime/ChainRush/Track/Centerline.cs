using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoHarness.ChainRush.Track
{
    // The track's centerline as a chain of straight pieces. S is absolute (double) so it keeps counting
    // across origin shifts and trimmed pieces; each piece stores its own world start, keeping float
    // offsets small. Positions before the first or past the last piece extend along that piece's
    // tangent, so a runner crossing the finish line still gains S.
    // Height follows a vertical curve per piece: grade (rise over horizontal run) changes linearly from
    // the previous piece's end grade to this piece's end grade, so height is a parabola and both stay
    // continuous across joints. S and the piece axes are horizontal; Up is world up.
    public sealed class Centerline
    {
        private struct Piece
        {
            public double StartS;
            public Vector3 Start;
            public Vector3 Forward;
            public Vector3 Right;
            public float Length;
            public float StartGrade;
            public float EndGrade;
        }

        private readonly List<Piece> pieces = new List<Piece>(16);
        private readonly Vector3 origin;
        private readonly float originYaw;
        private Vector3 end;
        private float endYaw;
        private float endGrade;
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
        public float EndGrade => endGrade;

        // Back to the construction origin with no pieces; S restarts at zero and the grade at flat.
        public void Clear()
        {
            pieces.Clear();
            end = origin;
            endYaw = originYaw;
            endGrade = 0f;
            endS = 0d;
        }

        // A straight piece that keeps the current grade.
        public void AppendStraight(float length) => AppendStraight(length, endGrade);

        // A straight piece whose grade eases from the current end grade to toGrade over its length.
        public void AppendStraight(float length, float toGrade)
        {
            if (!(length > 0f) || float.IsInfinity(length))
                throw new ArgumentOutOfRangeException(nameof(length), length, "Piece length must be positive and finite.");
            if (!IsFinite(toGrade) || Mathf.Abs(toGrade) > 1f)
                throw new ArgumentOutOfRangeException(nameof(toGrade), toGrade, "Grade must be finite and within [-1, 1] (45 degrees).");
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
                StartGrade = endGrade,
                EndGrade = toGrade,
            };
            pieces.Add(piece);
            end = piece.Start + piece.Forward * length;
            end.y = HeightOn(piece, length);
            endGrade = toGrade;
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
            return new TrackCoord(piece.StartS + along, Vector3.Dot(relative, piece.Right), world.y - HeightOn(piece, along));
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

        private static TrackFrame FrameOn(Piece piece, float along)
        {
            Vector3 position = piece.Start + piece.Forward * along;
            position.y = HeightOn(piece, along);
            return new TrackFrame(piece.StartS + along, position, piece.Forward, piece.Right, GradeOn(piece, along));
        }

        // Before the piece or past its end the height continues along the end grade.
        private static float HeightOn(Piece piece, float along)
        {
            if (along <= 0f) return piece.Start.y + piece.StartGrade * along;
            float run = Mathf.Min(along, piece.Length);
            float rise = piece.StartGrade * run + (piece.EndGrade - piece.StartGrade) * run * run / (2f * piece.Length);
            return piece.Start.y + rise + piece.EndGrade * (along - run);
        }

        private static float GradeOn(Piece piece, float along)
        {
            if (along <= 0f) return piece.StartGrade;
            if (along >= piece.Length) return piece.EndGrade;
            return piece.StartGrade + (piece.EndGrade - piece.StartGrade) * along / piece.Length;
        }

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
