using System;
using System.Globalization;

namespace ProtoHarness.ChainRush.Track
{
    // One generated course module as plain values: up to three centerline pieces (straight or horizontal arc,
    // each easing its grade to an end grade), an optional gap and grapple anchor, and which road edges have no
    // guard. Offsets (gap start, anchor) are along the module from its start. Every module starts and ends at
    // grade zero, so modules join in any order. Only CourseGenerator builds them.
    public readonly struct CourseModule : IEquatable<CourseModule>
    {
        public const int MaxPieces = 3;

        private readonly float length0, length1, length2;
        private readonly float radius0, radius1, radius2;
        private readonly float degrees0, degrees1, degrees2;
        private readonly float grade0, grade1, grade2;

        public readonly int Index;
        public readonly ModuleKind Kind;
        public readonly double StartS;
        public readonly double Length;
        public readonly int PieceCount;
        public readonly float GapStart;
        public readonly float GapLength;
        public readonly float AnchorAlong;
        public readonly float AnchorHeight;
        public readonly float AnchorSide;
        public readonly bool HasAnchor;
        public readonly bool OpenLeft;
        public readonly bool OpenRight;
        public readonly bool IsEscape;

        private CourseModule(int index, ModuleKind kind, double startS, double length, int pieceCount,
            float length0, float length1, float length2, float radius0, float radius1, float radius2,
            float degrees0, float degrees1, float degrees2, float grade0, float grade1, float grade2,
            float gapStart, float gapLength, bool hasAnchor, float anchorAlong, float anchorHeight, float anchorSide,
            bool openLeft, bool openRight, bool isEscape)
        {
            Index = index;
            Kind = kind;
            StartS = startS;
            Length = length;
            PieceCount = pieceCount;
            this.length0 = length0;
            this.length1 = length1;
            this.length2 = length2;
            this.radius0 = radius0;
            this.radius1 = radius1;
            this.radius2 = radius2;
            this.degrees0 = degrees0;
            this.degrees1 = degrees1;
            this.degrees2 = degrees2;
            this.grade0 = grade0;
            this.grade1 = grade1;
            this.grade2 = grade2;
            GapStart = gapStart;
            GapLength = gapLength;
            HasAnchor = hasAnchor;
            AnchorAlong = anchorAlong;
            AnchorHeight = anchorHeight;
            AnchorSide = anchorSide;
            OpenLeft = openLeft;
            OpenRight = openRight;
            IsEscape = isEscape;
        }

        public double EndS => StartS + Length;
        public bool HasGap => GapLength > 0f;

        // Signed total turn: positive right, negative left.
        public float TurnDegrees => degrees0 + degrees1 + degrees2;

        public float PieceLength(int piece) => Pick(piece, length0, length1, length2);

        // Zero for a straight piece.
        public float PieceRadius(int piece) => Pick(piece, radius0, radius1, radius2);

        // Signed turn of the piece, zero for a straight piece.
        public float PieceDegrees(int piece) => Pick(piece, degrees0, degrees1, degrees2);

        public float PieceEndGrade(int piece) => Pick(piece, grade0, grade1, grade2);

        // Appends this module's pieces to line, which must end at this module's start S on flat ground.
        public void AppendTo(Centerline line)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (Math.Abs(line.EndS - StartS) > 1e-3)
                throw new InvalidOperationException(
                    $"Module {Index} starts at S {StartS:F3} but the centerline ends at S {line.EndS:F3}; append modules in order.");
            if (line.EndGrade != 0f)
                throw new InvalidOperationException($"Module {Index} starts flat but the centerline ends at grade {line.EndGrade}.");
            for (int i = 0; i < PieceCount; i++)
            {
                if (PieceRadius(i) > 0f) line.AppendArc(PieceRadius(i), PieceDegrees(i), PieceEndGrade(i));
                else line.AppendStraight(PieceLength(i), PieceEndGrade(i));
            }
        }

        internal static CourseModule Create(int index, ModuleKind kind, double startS)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index), index, "Module index cannot be negative.");
            if (double.IsNaN(startS) || double.IsInfinity(startS))
                throw new ArgumentOutOfRangeException(nameof(startS), startS, "Start S must be finite.");
            return new CourseModule(index, kind, startS, 0d, 0, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
                0f, 0f, false, 0f, 0f, 0f, false, false, false);
        }

        internal CourseModule WithStraight(float length, float endGrade)
        {
            if (!(length > 0f) || float.IsInfinity(length))
                throw new ArgumentOutOfRangeException(nameof(length), length, "Piece length must be positive and finite.");
            return WithPiece(length, 0f, 0f, endGrade);
        }

        // Arc length is computed exactly as Centerline.AppendArc does, so S stays identical after AppendTo.
        internal CourseModule WithArc(float radius, float degrees, float endGrade)
        {
            if (!(radius > 0f) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius must be positive and finite.");
            if (!(Math.Abs(degrees) > 0f) || Math.Abs(degrees) > 180f)
                throw new ArgumentOutOfRangeException(nameof(degrees), degrees, "Arc turn must be non-zero and at most 180 degrees.");
            return WithPiece(ArcLength(radius, degrees), radius, degrees, endGrade);
        }

        internal static float ArcLength(float radius, float degrees) =>
            radius * Math.Abs(degrees) * UnityEngine.Mathf.Deg2Rad;

        internal CourseModule WithGap(float start, float length)
        {
            if (!(start >= 0f) || !(length > 0f) || start + length > Length)
                throw new ArgumentOutOfRangeException(nameof(length), length, $"Gap [{start}, {start + length}] must lie inside the module (length {Length}).");
            return new CourseModule(Index, Kind, StartS, Length, PieceCount, length0, length1, length2,
                radius0, radius1, radius2, degrees0, degrees1, degrees2, grade0, grade1, grade2,
                start, length, HasAnchor, AnchorAlong, AnchorHeight, AnchorSide, OpenLeft, OpenRight, IsEscape);
        }

        internal CourseModule WithAnchor(float along, float height, float side)
        {
            if (!(along >= 0f) || along > Length || float.IsNaN(height) || float.IsInfinity(height) || float.IsNaN(side) || float.IsInfinity(side))
                throw new ArgumentOutOfRangeException(nameof(along), along, $"Anchor must lie along the module (length {Length}) with finite height and side.");
            return new CourseModule(Index, Kind, StartS, Length, PieceCount, length0, length1, length2,
                radius0, radius1, radius2, degrees0, degrees1, degrees2, grade0, grade1, grade2,
                GapStart, GapLength, true, along, height, side, OpenLeft, OpenRight, IsEscape);
        }

        internal CourseModule WithOpenEdges(bool left, bool right) =>
            new CourseModule(Index, Kind, StartS, Length, PieceCount, length0, length1, length2,
                radius0, radius1, radius2, degrees0, degrees1, degrees2, grade0, grade1, grade2,
                GapStart, GapLength, HasAnchor, AnchorAlong, AnchorHeight, AnchorSide, left, right, IsEscape);

        internal CourseModule AsEscape() =>
            new CourseModule(Index, Kind, StartS, Length, PieceCount, length0, length1, length2,
                radius0, radius1, radius2, degrees0, degrees1, degrees2, grade0, grade1, grade2,
                GapStart, GapLength, HasAnchor, AnchorAlong, AnchorHeight, AnchorSide, OpenLeft, OpenRight, true);

        private CourseModule WithPiece(float length, float radius, float degrees, float endGrade)
        {
            if (PieceCount >= MaxPieces)
                throw new InvalidOperationException($"Module {Index} already has {MaxPieces} pieces.");
            if (float.IsNaN(endGrade) || Math.Abs(endGrade) > 1f)
                throw new ArgumentOutOfRangeException(nameof(endGrade), endGrade, "Grade must be within [-1, 1].");
            float l0 = length0, l1 = length1, l2 = length2;
            float r0 = radius0, r1 = radius1, r2 = radius2;
            float d0 = degrees0, d1 = degrees1, d2 = degrees2;
            float g0 = grade0, g1 = grade1, g2 = grade2;
            switch (PieceCount)
            {
                case 0: l0 = length; r0 = radius; d0 = degrees; g0 = endGrade; break;
                case 1: l1 = length; r1 = radius; d1 = degrees; g1 = endGrade; break;
                default: l2 = length; r2 = radius; d2 = degrees; g2 = endGrade; break;
            }
            return new CourseModule(Index, Kind, StartS, Length + length, PieceCount + 1, l0, l1, l2, r0, r1, r2,
                d0, d1, d2, g0, g1, g2, GapStart, GapLength, HasAnchor, AnchorAlong, AnchorHeight, AnchorSide,
                OpenLeft, OpenRight, IsEscape);
        }

        private float Pick(int piece, float first, float second, float third)
        {
            if (piece < 0 || piece >= PieceCount)
                throw new ArgumentOutOfRangeException(nameof(piece), piece, $"Module {Index} has {PieceCount} pieces.");
            return piece == 0 ? first : piece == 1 ? second : third;
        }

        public bool Equals(CourseModule other) =>
            Index == other.Index && Kind == other.Kind && StartS.Equals(other.StartS) && Length.Equals(other.Length)
            && PieceCount == other.PieceCount
            && length0.Equals(other.length0) && length1.Equals(other.length1) && length2.Equals(other.length2)
            && radius0.Equals(other.radius0) && radius1.Equals(other.radius1) && radius2.Equals(other.radius2)
            && degrees0.Equals(other.degrees0) && degrees1.Equals(other.degrees1) && degrees2.Equals(other.degrees2)
            && grade0.Equals(other.grade0) && grade1.Equals(other.grade1) && grade2.Equals(other.grade2)
            && GapStart.Equals(other.GapStart) && GapLength.Equals(other.GapLength)
            && HasAnchor == other.HasAnchor && AnchorAlong.Equals(other.AnchorAlong)
            && AnchorHeight.Equals(other.AnchorHeight) && AnchorSide.Equals(other.AnchorSide)
            && OpenLeft == other.OpenLeft && OpenRight == other.OpenRight && IsEscape == other.IsEscape;

        public override bool Equals(object obj) => obj is CourseModule other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Index;
                hash = hash * 397 ^ (int)Kind;
                hash = hash * 397 ^ StartS.GetHashCode();
                hash = hash * 397 ^ Length.GetHashCode();
                hash = hash * 397 ^ TurnDegrees.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "#{0} {1} S {2:F1} L {3:F1} turn {4:F1}{5}{6}{7}", Index, Kind, StartS, Length, TurnDegrees,
            HasGap ? $" gap {GapLength:F1}" : "", OpenLeft || OpenRight ? (OpenLeft ? " open L" : " open R") : "",
            IsEscape ? " escape" : "");
    }
}
