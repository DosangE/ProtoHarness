using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoHarness.ChainRush.Track
{
    // The track's centerline as a chain of straight pieces and horizontal arcs. S is absolute (double) so
    // it keeps counting across origin shifts and trimmed pieces; each piece stores its own world start,
    // keeping float offsets small. Positions before the first or past the last piece extend along that
    // piece's end tangent as a straight line, so a runner crossing the finish line still gains S.
    // Height follows a vertical curve per piece: grade (rise over horizontal run) changes linearly from
    // the previous piece's end grade to this piece's end grade, so height is a parabola and both stay
    // continuous across joints. Heading is continuous too: each piece starts where the last one points.
    // S and the piece axes are horizontal; Up is world up.
    // A track whose end meets its start can be closed (Close): it becomes a loop, S wraps at one lap, and a
    // query finds the nearest piece instead of the first one not yet passed, which a return leg would fool
    // (COURSE.md T4). SetFocus narrows any query to the pieces around an S the caller is tracking.
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
            // Signed 1/radius: positive turns right, negative left, zero is straight.
            public float Curvature;
            public float StartYaw;
            public Vector3 Center;
            public Vector3 End;
            public Vector3 EndForward;
            public Vector3 EndRight;
        }

        private readonly List<Piece> pieces = new List<Piece>(16);
        private readonly Vector3 origin;
        private readonly float originYaw;
        private Vector3 end;
        private float endYaw;
        private float endGrade;
        private double endS;
        private bool isLoop;
        private bool hasFocus;
        private double focusS;
        private float focusWindow = 150f;

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
        public bool IsLoop => isLoop;
        public bool HasFocus => hasFocus;

        // Pieces within this many metres of the focus S (either side) are the ones a focused query looks at.
        public float FocusWindow
        {
            get => focusWindow;
            set
            {
                if (!(value > 0f) || float.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Focus window must be positive and finite.");
                focusWindow = value;
            }
        }

        // Back to the construction origin with no pieces; S restarts at zero and the grade at flat.
        public void Clear()
        {
            pieces.Clear();
            end = origin;
            endYaw = originYaw;
            endGrade = 0f;
            endS = 0d;
            isLoop = false;
            hasFocus = false;
        }

        // A straight piece that keeps the current grade.
        public void AppendStraight(float length) => AppendStraight(length, endGrade);

        // A straight piece whose grade eases from the current end grade to toGrade over its length.
        public void AppendStraight(float length, float toGrade)
        {
            if (!(length > 0f) || float.IsInfinity(length))
                throw new ArgumentOutOfRangeException(nameof(length), length, "Piece length must be positive and finite.");
            Append(length, 0f, toGrade);
        }

        // A horizontal arc of the given radius turning by degrees (positive right, negative left, at most a
        // half turn per piece), with the grade easing to toGrade like a straight piece.
        public void AppendArc(float radius, float degrees, float toGrade)
        {
            if (!(radius > 0f) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius must be positive and finite.");
            if (!(Mathf.Abs(degrees) > 0f) || Mathf.Abs(degrees) > 180f)
                throw new ArgumentOutOfRangeException(nameof(degrees), degrees, "Arc turn must be non-zero and at most 180 degrees.");
            Append(radius * Mathf.Abs(degrees) * Mathf.Deg2Rad, Mathf.Sign(degrees) / radius, toGrade);
        }

        // A horizontal arc that keeps the current grade.
        public void AppendArc(float radius, float degrees) => AppendArc(radius, degrees, endGrade);

        private void Append(float length, float curvature, float toGrade)
        {
            if (isLoop) throw new InvalidOperationException("Centerline is closed; Clear it before appending pieces.");
            if (!IsFinite(toGrade) || Mathf.Abs(toGrade) > 1f)
                throw new ArgumentOutOfRangeException(nameof(toGrade), toGrade, "Grade must be finite and within [-1, 1] (45 degrees).");
            var piece = new Piece
            {
                StartS = endS,
                Start = end,
                Forward = ForwardOf(endYaw),
                Right = RightOf(endYaw),
                Length = length,
                StartGrade = endGrade,
                EndGrade = toGrade,
                Curvature = curvature,
                StartYaw = endYaw,
            };
            float endTurn = curvature * length * Mathf.Rad2Deg;
            piece.EndForward = ForwardOf(endYaw + endTurn);
            piece.EndRight = RightOf(endYaw + endTurn);
            if (curvature == 0f)
            {
                piece.End = piece.Start + piece.Forward * length;
            }
            else
            {
                piece.Center = piece.Start + piece.Right / curvature;
                piece.End = piece.Center - piece.EndRight / curvature;
            }
            piece.End.y = HeightOn(piece, length);
            pieces.Add(piece);
            end = piece.End;
            endYaw += endTurn;
            endGrade = toGrade;
            endS += length;
        }

        // Drops leading pieces that end before s. The last piece always stays.
        public void TrimBefore(double s)
        {
            if (isLoop) throw new InvalidOperationException("A closed centerline keeps every piece; Clear it to trim.");
            if (double.IsNaN(s)) throw new ArgumentOutOfRangeException(nameof(s), s, "S cannot be NaN.");
            int count = 0;
            while (count < pieces.Count - 1 && pieces[count].StartS + pieces[count].Length < s) count++;
            if (count > 0) pieces.RemoveRange(0, count);
        }

        // Makes the track a loop when its end meets its start: horizontal position, height and heading
        // within the tolerances and a flat end grade (the start is flat). Throws when it does not, saying how far off.
        public void Close(float positionTolerance = 0.5f, float yawToleranceDegrees = 1f, float heightTolerance = 0.1f)
        {
            if (!TryClose(out string error, positionTolerance, yawToleranceDegrees, heightTolerance))
                throw new InvalidOperationException("Centerline cannot be closed: " + error);
        }

        public bool TryClose(out string error, float positionTolerance = 0.5f, float yawToleranceDegrees = 1f, float heightTolerance = 0.1f)
        {
            if (isLoop)
            {
                error = null;
                return true;
            }
            if (pieces.Count == 0)
            {
                error = "there are no pieces.";
                return false;
            }
            if (pieces[0].StartS != 0d)
            {
                error = "the first pieces were trimmed away.";
                return false;
            }
            Vector3 start = pieces[0].Start;
            Vector3 miss = end - start;
            float height = Mathf.Abs(miss.y);
            miss.y = 0f;
            float distance = miss.magnitude;
            float yaw = Mathf.Abs(Mathf.DeltaAngle(endYaw, pieces[0].StartYaw));
            if (distance > positionTolerance || height > heightTolerance || yaw > yawToleranceDegrees)
            {
                error = $"the end is {distance:F2} m (level), {height:F2} m (height) and {yaw:F2} degrees away from the start; " +
                        $"the limits are {positionTolerance:F2} m, {heightTolerance:F2} m and {yawToleranceDegrees:F2} degrees.";
                return false;
            }
            if (Mathf.Abs(endGrade) > 0.001f)
            {
                error = $"the end grade is {endGrade:F3}, but the start is flat.";
                return false;
            }
            isLoop = true;
            error = null;
            return true;
        }

        // Narrows Project and Frame to the pieces within FocusWindow of s (on a loop, either side of the seam).
        public void SetFocus(double s)
        {
            if (double.IsNaN(s) || double.IsInfinity(s)) throw new ArgumentOutOfRangeException(nameof(s), s, "Focus S must be finite.");
            focusS = s;
            hasFocus = true;
        }

        public void ClearFocus() => hasFocus = false;

        public void ShiftOrigin(Vector3 offset)
        {
            if (!IsFinite(offset)) throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be finite.");
            for (int i = 0; i < pieces.Count; i++)
            {
                Piece piece = pieces[i];
                piece.Start += offset;
                piece.Center += offset;
                piece.End += offset;
                pieces[i] = piece;
            }
            end += offset;
        }

        public TrackCoord Project(Vector3 world)
        {
            Piece piece = PieceAt(world, out float along, out float right);
            return new TrackCoord(WrapIfLoop(piece.StartS + along), right, world.y - HeightOn(piece, along));
        }

        // The frame at the centerline point nearest to world.
        public TrackFrame Frame(Vector3 world)
        {
            Piece piece = PieceAt(world, out float along, out _);
            TrackFrame frame = FrameOn(piece, along);
            if (!isLoop) return frame;
            return new TrackFrame(Wrap(frame.S), frame.Position, frame.Forward, frame.Right, frame.Grade, frame.Curvature);
        }

        public TrackFrame FrameAt(double s)
        {
            if (double.IsNaN(s)) throw new ArgumentOutOfRangeException(nameof(s), s, "S cannot be NaN.");
            RequirePieces();
            s = WrapIfLoop(s);
            int index = 0;
            while (index < pieces.Count - 1 && s > pieces[index].StartS + pieces[index].Length) index++;
            Piece piece = pieces[index];
            return FrameOn(piece, (float)(s - piece.StartS));
        }

        private static TrackFrame FrameOn(Piece piece, float along)
        {
            Vector3 position;
            Vector3 forward;
            Vector3 right;
            float curvature = 0f;
            if (along <= 0f || piece.Curvature == 0f)
            {
                position = piece.Start + piece.Forward * along;
                forward = piece.Forward;
                right = piece.Right;
            }
            else if (along >= piece.Length)
            {
                position = piece.End + piece.EndForward * (along - piece.Length);
                forward = piece.EndForward;
                right = piece.EndRight;
            }
            else
            {
                float yaw = piece.StartYaw + piece.Curvature * along * Mathf.Rad2Deg;
                forward = ForwardOf(yaw);
                right = RightOf(yaw);
                position = piece.Center - right / piece.Curvature;
                curvature = piece.Curvature;
            }
            position.y = HeightOn(piece, along);
            return new TrackFrame(piece.StartS + along, position, forward, right, GradeOn(piece, along), curvature);
        }

        // Distance along the piece and signed offset to the right of it. Straight pieces and the
        // extensions before an arc's start or past its end use the tangent line; inside an arc both come
        // from the angle and distance around its center.
        private static void LocalOn(Piece piece, Vector3 world, out float along, out float right)
        {
            Vector3 fromStart = world - piece.Start;
            along = Vector3.Dot(fromStart, piece.Forward);
            right = Vector3.Dot(fromStart, piece.Right);
            if (piece.Curvature == 0f || along <= 0f) return;
            Vector3 fromEnd = world - piece.End;
            float beyond = Vector3.Dot(fromEnd, piece.EndForward);
            if (beyond >= 0f)
            {
                along = piece.Length + beyond;
                right = Vector3.Dot(fromEnd, piece.EndRight);
                return;
            }
            Vector3 radial = world - piece.Center;
            radial.y = 0f;
            float distance = radial.magnitude;
            float sign = Mathf.Sign(piece.Curvature);
            // The point's right axis is the outward radius for a left turn and the inward one for a right turn.
            Vector3 pointRight = radial * (-sign / Mathf.Max(distance, 1e-6f));
            float yaw = Mathf.Atan2(-pointRight.z, pointRight.x) * Mathf.Rad2Deg;
            along = Mathf.DeltaAngle(piece.StartYaw, yaw) * Mathf.Deg2Rad / piece.Curvature;
            right = sign * (1f / Mathf.Abs(piece.Curvature) - distance);
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
        private Piece PieceAt(Vector3 world, out float along, out float right)
        {
            if (!IsFinite(world)) throw new ArgumentOutOfRangeException(nameof(world), world, "Position must be finite.");
            RequirePieces();
            if (isLoop || hasFocus) return NearestPiece(world, out along, out right);
            int last = pieces.Count - 1;
            for (int i = 0; i < last; i++)
            {
                LocalOn(pieces[i], world, out along, out right);
                if (along <= pieces[i].Length) return pieces[i];
            }
            LocalOn(pieces[last], world, out along, out right);
            return pieces[last];
        }

        // The piece whose centerline is closest to the point (its span extended along the end tangents), among
        // the pieces near the focus when there is one. Ties go to the earlier piece.
        private Piece NearestPiece(Vector3 world, out float along, out float right)
        {
            int best = -1;
            float bestScore = float.MaxValue;
            along = 0f;
            right = 0f;
            for (int pass = 0; pass < 2 && best < 0; pass++)
            {
                // The second pass ignores the focus, so a window that holds no piece still answers.
                bool windowed = hasFocus && pass == 0;
                for (int i = 0; i < pieces.Count; i++)
                {
                    if (windowed && !InFocusWindow(pieces[i])) continue;
                    LocalOn(pieces[i], world, out float candidateAlong, out float candidateRight);
                    float overshoot = candidateAlong < 0f ? -candidateAlong : Mathf.Max(0f, candidateAlong - pieces[i].Length);
                    float score = candidateRight * candidateRight + overshoot * overshoot;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    best = i;
                    along = candidateAlong;
                    right = candidateRight;
                }
            }
            return pieces[best];
        }

        private bool InFocusWindow(Piece piece)
        {
            double from = piece.StartS;
            double to = piece.StartS + piece.Length;
            double lap = isLoop ? endS : 0d;
            for (int k = isLoop ? -1 : 0; k <= (isLoop ? 1 : 0); k++)
            {
                double focus = focusS + k * lap;
                double distance = focus < from ? from - focus : (focus > to ? focus - to : 0d);
                if (distance <= focusWindow) return true;
            }
            return false;
        }

        // S folded into [0, lap length) on a loop.
        private double Wrap(double s)
        {
            double wrapped = s - Math.Floor(s / endS) * endS;
            return wrapped >= endS ? 0d : wrapped;
        }

        private double WrapIfLoop(double s) => isLoop ? Wrap(s) : s;

        private void RequirePieces()
        {
            if (pieces.Count == 0) throw new InvalidOperationException("Centerline has no pieces; append one before querying it.");
        }

        private static Vector3 ForwardOf(float yawDegrees)
        {
            float radians = yawDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }

        private static Vector3 RightOf(float yawDegrees)
        {
            float radians = yawDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), 0f, -Mathf.Sin(radians));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
