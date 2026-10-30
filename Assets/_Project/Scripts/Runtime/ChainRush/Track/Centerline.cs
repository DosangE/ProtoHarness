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

        // A pole that is not an arc's center (AddPole): where it stands and how far from it a runner can hook it.
        private struct ExtraPole
        {
            public Vector3 Position;
            public float Reach;
        }

        // A kicker's lip (AddLaunch): where a runner on the road leaves it, and how steeply (rise per metre forward).
        private struct Launch
        {
            public double S;
            public float Lift;
        }

        // A shortcut's road beside the loop (AddBranch): its own pieces, whose S runs from FromS to ToS of the loop in
        // proportion to the distance along them, and the half width of the main road it leaves.
        private sealed class Branch
        {
            public double FromS;
            public double Scale;
            public float MainHalfWidth;
            public readonly List<Piece> Pieces = new List<Piece>(3);

            public double MapS(double along) => FromS + along * Scale;
        }

        private readonly List<Piece> pieces = new List<Piece>(16);
        private readonly List<ExtraPole> extraPoles = new List<ExtraPole>(8);
        private readonly List<Launch> launches = new List<Launch>(4);
        private readonly List<double> boostZones = new List<double>(4);
        private readonly List<Branch> branches = new List<Branch>(2);
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

        // Back to the construction origin with no pieces, added poles, launches, boost zones or branches; S restarts at zero
        // and the grade at flat.
        public void Clear()
        {
            pieces.Clear();
            extraPoles.Clear();
            launches.Clear();
            boostZones.Clear();
            branches.Clear();
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
            for (int i = 0; i < extraPoles.Count; i++)
            {
                ExtraPole pole = extraPoles[i];
                pole.Position += offset;
                extraPoles[i] = pole;
            }
            for (int b = 0; b < branches.Count; b++)
            {
                List<Piece> own = branches[b].Pieces;
                for (int i = 0; i < own.Count; i++)
                {
                    Piece piece = own[i];
                    piece.Start += offset;
                    piece.Center += offset;
                    piece.End += offset;
                    own[i] = piece;
                }
            }
            end += offset;
        }

        // A point on a branch (AddBranch) gets the branch's coordinates and S; any other the loop's.
        public TrackCoord Project(Vector3 world)
        {
            Piece piece = PieceAt(world, out float along, out float right);
            if (TryBranchAt(world, piece, along, right, out Branch branch, out Piece onBranch, out float branchAlong, out float branchRight))
                return new TrackCoord(Wrap(branch.MapS(onBranch.StartS + branchAlong)), branchRight, world.y - HeightOn(onBranch, branchAlong));
            return new TrackCoord(WrapIfLoop(piece.StartS + along), right, world.y - HeightOn(piece, along));
        }

        // The frame at the centerline point nearest to world (a branch's, with the branch's S, on a branch).
        public TrackFrame Frame(Vector3 world)
        {
            Piece piece = PieceAt(world, out float along, out float right);
            if (TryBranchAt(world, piece, along, right, out Branch branch, out Piece onBranch, out float branchAlong, out _))
            {
                TrackFrame own = FrameOn(onBranch, branchAlong);
                return new TrackFrame(Wrap(branch.MapS(own.S)), own.Position, own.Forward, own.Right, own.Grade, own.Curvature);
            }
            TrackFrame frame = FrameOn(piece, along);
            if (!isLoop) return frame;
            return new TrackFrame(Wrap(frame.S), frame.Position, frame.Forward, frame.Right, frame.Grade, frame.Curvature);
        }

        public int BranchCount => branches.Count;

        // A shortcut's road beside the loop (user 2026-10-25, Finger Lanes): line is its open centerline from the loop at
        // fromS back onto it at toS (TrackDefinition.ShortcutLine), mainHalfWidth the loop's road half width there. A point
        // off the loop's road (farther to the side than mainHalfWidth) and nearer to the branch than to the loop is on the
        // branch: Project and Frame answer with the branch's pieces, and S goes from fromS to toS in proportion along it, so
        // the laps keep counting across. The branch's arcs have corner poles like the loop's.
        public void AddBranch(double fromS, double toS, Centerline line, float mainHalfWidth)
        {
            if (!isLoop) throw new InvalidOperationException("Only a closed centerline takes branches.");
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (line.isLoop || line.pieces.Count == 0) throw new ArgumentException("A branch is an open line with pieces.", nameof(line));
            if (!(fromS >= 0d) || !(toS > fromS) || !(toS < endS))
                throw new ArgumentOutOfRangeException(nameof(toS), toS, $"A branch leaves at {fromS} and comes back later in the lap [0, {endS}).");
            if (!(mainHalfWidth > 0f) || float.IsInfinity(mainHalfWidth))
                throw new ArgumentOutOfRangeException(nameof(mainHalfWidth), mainHalfWidth, "The main road's half width must be positive and finite.");
            var branch = new Branch { FromS = fromS, Scale = (toS - fromS) / line.endS, MainHalfWidth = mainHalfWidth };
            branch.Pieces.AddRange(line.pieces);
            branches.Add(branch);
        }

        // The branch piece a point is on, if any: only off the loop's road on the loop's nearest piece, and only nearer to
        // the branch piece (measured as NearestPiece does) than to that loop piece.
        private bool TryBranchAt(Vector3 world, Piece main, float mainAlong, float mainRight, out Branch branch, out Piece piece, out float along, out float right)
        {
            branch = null;
            piece = default;
            along = 0f;
            right = 0f;
            if (branches.Count == 0) return false;
            float best = Score(main, mainAlong, mainRight);
            for (int b = 0; b < branches.Count; b++)
            {
                if (Mathf.Abs(mainRight) <= branches[b].MainHalfWidth) continue;
                List<Piece> own = branches[b].Pieces;
                for (int i = 0; i < own.Count; i++)
                {
                    LocalOn(own[i], world, out float candidateAlong, out float candidateRight);
                    float score = Score(own[i], candidateAlong, candidateRight);
                    if (score >= best) continue;
                    best = score;
                    branch = branches[b];
                    piece = own[i];
                    along = candidateAlong;
                    right = candidateRight;
                }
            }
            return branch != null;
        }

        private static float Score(Piece piece, float along, float right)
        {
            float overshoot = along < 0f ? -along : Mathf.Max(0f, along - piece.Length);
            return right * right + overshoot * overshoot;
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

        // Appends the pole of every arc piece (its center, at the arc's start height), in piece order, then the added
        // poles (AddPole) in the order they were added: the same points TryFindPole chooses from, for presentation to
        // stand a pole model on (R5).
        public void GetPoles(List<Vector3> poles)
        {
            if (poles == null) throw new ArgumentNullException(nameof(poles));
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i].Curvature != 0f) poles.Add(pieces[i].Center);
            for (int b = 0; b < branches.Count; b++)
                for (int i = 0; i < branches[b].Pieces.Count; i++)
                    if (branches[b].Pieces[i].Curvature != 0f) poles.Add(branches[b].Pieces[i].Center);
            for (int i = 0; i < extraPoles.Count; i++) poles.Add(extraPoles[i].Position);
        }

        // A pole that is not an arc's center, such as one beside a straight (user 2026-10-22, the Skyway's wide straight):
        // TryFindPole offers it like a corner pole, with reach in place of an arc's radius. It is a world position like the
        // pieces' and moves with ShiftOrigin; Clear removes it, TrimBefore keeps it.
        public void AddPole(Vector3 position, float reach)
        {
            if (!IsFinite(position)) throw new ArgumentOutOfRangeException(nameof(position), position, "Position must be finite.");
            if (!(reach > 0f) || float.IsInfinity(reach)) throw new ArgumentOutOfRangeException(nameof(reach), reach, "Reach must be positive and finite.");
            extraPoles.Add(new ExtraPole { Position = position, Reach = reach });
        }

        // A kicker's lip at s (TrackDefinition.Segment.Kicker): a runner on the road that crosses it going forward leaves it
        // climbing at lift times its forward speed (RunnerMotor). It is kept by S, so ShiftOrigin does not move it; Clear
        // removes it.
        public void AddLaunch(double s, float lift)
        {
            if (double.IsNaN(s) || double.IsInfinity(s)) throw new ArgumentOutOfRangeException(nameof(s), s, "S must be finite.");
            if (!(lift > 0f) || float.IsInfinity(lift)) throw new ArgumentOutOfRangeException(nameof(lift), lift, "Lift must be positive and finite.");
            launches.Add(new Launch { S = s, Lift = lift });
        }

        public int LaunchCount => launches.Count;

        // A boost pad at s (TrackDefinition.Segment.BoostStraight): a runner on the road crossing it forward cuts its
        // booster and takes the zone's speed (RunnerMotor). Like a launch it stays by S through ShiftOrigin and is cleared.
        public void AddBoostZone(double s)
        {
            if (double.IsNaN(s) || double.IsInfinity(s)) throw new ArgumentOutOfRangeException(nameof(s), s, "S must be finite.");
            boostZones.Add(s);
        }

        public int BoostZoneCount => boostZones.Count;

        // Appends the pads' S in AddBoostZone order, for presentation to lay a pad on each.
        public void GetBoostZones(List<double> zones)
        {
            if (zones == null) throw new ArgumentNullException(nameof(zones));
            for (int i = 0; i < boostZones.Count; i++) zones.Add(boostZones[i]);
        }

        // A pad crossed moving forward: past fromS and no farther than toS, including a move across a loop's seam,
        // as TryLaunchBetween. A move backward or nowhere crosses none.
        public bool TryBoostBetween(double fromS, double toS)
        {
            if (boostZones.Count == 0) return false;
            double moved = toS - fromS;
            if (isLoop && moved < -endS * 0.5d) moved += endS;
            if (!(moved > 0d)) return false;
            for (int i = 0; i < boostZones.Count; i++)
            {
                double ahead = boostZones[i] - fromS;
                if (isLoop) ahead -= Math.Floor(ahead / endS) * endS;
                if (ahead > 0d && ahead <= moved) return true;
            }
            return false;
        }

        // The first launch (AddLaunch order) a runner crosses moving forward from fromS to toS, S as Frame and Project
        // give it: past fromS and no farther than toS. On a loop the move may wrap the seam and so may the launch's S. A move
        // backward or nowhere crosses none.
        public bool TryLaunchBetween(double fromS, double toS, out float lift)
        {
            lift = 0f;
            if (launches.Count == 0) return false;
            double moved = toS - fromS;
            if (isLoop && moved < -endS * 0.5d) moved += endS;
            if (!(moved > 0d)) return false;
            for (int i = 0; i < launches.Count; i++)
            {
                double ahead = launches[i].S - fromS;
                if (isLoop) ahead -= Math.Floor(ahead / endS) * endS;
                if (ahead > 0d && ahead <= moved)
                {
                    lift = launches[i].Lift;
                    return true;
                }
            }
            return false;
        }

        // Every arc piece has one pole at its center (the corner pole swing, PROPOSAL_CORNER_POLE.md 2-1), and AddPole
        // adds others. Of the poles within maxAngleDegrees either side of headingDegrees (world yaw, as RacerState.Heading)
        // and no farther than their arc's radius (an added pole's reach) plus extraReach, the nearest, measured level; ties
        // go to the earlier piece, and arcs come before added poles. False when there is none. An arc's pole sits at the
        // arc's start height. All poles are searched, focus or not: a runner asks only when it acts.
        public bool TryFindPole(Vector3 world, float headingDegrees, float maxAngleDegrees, float extraReach, out Vector3 pole)
        {
            if (!IsFinite(world)) throw new ArgumentOutOfRangeException(nameof(world), world, "Position must be finite.");
            if (!IsFinite(headingDegrees)) throw new ArgumentOutOfRangeException(nameof(headingDegrees), headingDegrees, "Heading must be finite.");
            if (!(maxAngleDegrees >= 0f && maxAngleDegrees <= 180f))
                throw new ArgumentOutOfRangeException(nameof(maxAngleDegrees), maxAngleDegrees, "The angle must be within [0, 180] degrees.");
            if (!(extraReach >= 0f) || float.IsInfinity(extraReach))
                throw new ArgumentOutOfRangeException(nameof(extraReach), extraReach, "The extra reach must be finite and not negative.");
            pole = default;
            bool found = false;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < pieces.Count; i++)
            {
                Piece piece = pieces[i];
                if (piece.Curvature == 0f) continue;
                if (!InReach(piece.Center, 1f / Mathf.Abs(piece.Curvature) + extraReach, world, headingDegrees, maxAngleDegrees, ref bestDistance)) continue;
                pole = piece.Center;
                found = true;
            }
            for (int b = 0; b < branches.Count; b++)
                for (int i = 0; i < branches[b].Pieces.Count; i++)
                {
                    Piece piece = branches[b].Pieces[i];
                    if (piece.Curvature == 0f) continue;
                    if (!InReach(piece.Center, 1f / Mathf.Abs(piece.Curvature) + extraReach, world, headingDegrees, maxAngleDegrees, ref bestDistance)) continue;
                    pole = piece.Center;
                    found = true;
                }
            for (int i = 0; i < extraPoles.Count; i++)
            {
                if (!InReach(extraPoles[i].Position, extraPoles[i].Reach + extraReach, world, headingDegrees, maxAngleDegrees, ref bestDistance)) continue;
                pole = extraPoles[i].Position;
                found = true;
            }
            return found;
        }

        // Whether a pole is within reach, nearer than the best so far and inside the angle; if so it becomes the best.
        private static bool InReach(Vector3 pole, float reach, Vector3 world, float headingDegrees, float maxAngleDegrees, ref float bestDistance)
        {
            Vector3 toward = pole - world;
            toward.y = 0f;
            float distance = toward.magnitude;
            if (distance > reach || distance >= bestDistance) return false;
            float yaw = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(Mathf.DeltaAngle(headingDegrees, yaw)) > maxAngleDegrees) return false;
            bestDistance = distance;
            return true;
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
