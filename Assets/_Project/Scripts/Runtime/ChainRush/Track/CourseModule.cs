using System;
using UnityEngine;

namespace ProtoHarness.ChainRush.Track
{
    // One generated course module: a named bundle of at most three Centerline pieces (COURSE.md 5-1).
    // Every module starts and ends flat (grade 0), so any order joins smoothly. S is the absolute course
    // distance, continuing through gaps (the centerline is unbroken; only the road surface is missing).
    // Anchor and gap positions are track coordinates, so they do not depend on where the course sits.
    public readonly struct CourseModule
    {
        public const int MaxPieces = 3;

        // A straight piece (Radius 0) or a horizontal arc (Radius > 0, signed Degrees: positive turns right).
        // EndGrade is the grade at the piece's end; it eases from the previous piece's end grade.
        public readonly struct Piece
        {
            public readonly float Length;
            public readonly float Radius;
            public readonly float Degrees;
            public readonly float EndGrade;

            private Piece(float length, float radius, float degrees, float endGrade)
            {
                Length = length;
                Radius = radius;
                Degrees = degrees;
                EndGrade = endGrade;
            }

            public bool IsArc => Radius > 0f;

            public static Piece Straight(float length, float endGrade)
            {
                if (!(length > 0f) || float.IsInfinity(length))
                    throw new ArgumentOutOfRangeException(nameof(length), length, "Piece length must be positive and finite.");
                return new Piece(length, 0f, 0f, endGrade);
            }

            // The length uses the same float expression as Centerline.AppendArc, so S sums match exactly.
            public static Piece Arc(float radius, float degrees, float endGrade)
            {
                if (!(radius > 0f) || float.IsInfinity(radius))
                    throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius must be positive and finite.");
                if (!(Mathf.Abs(degrees) > 0f) || Mathf.Abs(degrees) > 180f)
                    throw new ArgumentOutOfRangeException(nameof(degrees), degrees, "Arc turn must be non-zero and at most 180 degrees.");
                return new Piece(radius * Mathf.Abs(degrees) * Mathf.Deg2Rad, radius, degrees, endGrade);
            }
        }

        public readonly int Index;
        public readonly ModuleKind Kind;
        // True for the fallback rest the generator emits when every candidate broke a rule.
        public readonly bool IsEscape;
        public readonly double StartS;
        public readonly double Length;
        public readonly int PieceCount;
        // Road surface is missing on [GapStartS, GapStartS + GapLength); GapLength is 0 for modules without a gap.
        public readonly double GapStartS;
        public readonly float GapLength;
        // Grapple anchor in track coordinates: AnchorS along the centerline, AnchorOffset to its right,
        // AnchorHeight above the road. Only meaningful when HasAnchor.
        public readonly bool HasAnchor;
        public readonly double AnchorS;
        public readonly float AnchorOffset;
        public readonly float AnchorHeight;
        // An open edge has no guard, so a runner can fall off that side (COURSE.md 4-4).
        public readonly bool LeftOpen;
        public readonly bool RightOpen;

        private readonly Piece piece0;
        private readonly Piece piece1;
        private readonly Piece piece2;

        public CourseModule(int index, ModuleKind kind, bool isEscape, double startS, ReadOnlySpan<Piece> pieces,
            double gapStartS, float gapLength, bool hasAnchor, double anchorS, float anchorOffset, float anchorHeight,
            bool leftOpen, bool rightOpen)
        {
            if (pieces.Length < 1 || pieces.Length > MaxPieces)
                throw new ArgumentException($"A module needs 1 to {MaxPieces} pieces, got {pieces.Length}.", nameof(pieces));
            if (gapLength < 0f) throw new ArgumentOutOfRangeException(nameof(gapLength), gapLength, "Gap length cannot be negative.");
            Index = index;
            Kind = kind;
            IsEscape = isEscape;
            StartS = startS;
            PieceCount = pieces.Length;
            piece0 = pieces[0];
            piece1 = pieces.Length > 1 ? pieces[1] : default;
            piece2 = pieces.Length > 2 ? pieces[2] : default;
            double length = 0d;
            for (int i = 0; i < pieces.Length; i++) length += pieces[i].Length;
            Length = length;
            GapStartS = gapStartS;
            GapLength = gapLength;
            HasAnchor = hasAnchor;
            AnchorS = anchorS;
            AnchorOffset = anchorOffset;
            AnchorHeight = anchorHeight;
            LeftOpen = leftOpen;
            RightOpen = rightOpen;
        }

        public double EndS => StartS + Length;
        public bool HasGap => GapLength > 0f;

        public Piece GetPiece(int i)
        {
            if (i < 0 || i >= PieceCount) throw new ArgumentOutOfRangeException(nameof(i), i, $"Module has {PieceCount} pieces.");
            return i == 0 ? piece0 : (i == 1 ? piece1 : piece2);
        }

        // Signed total turn in degrees (positive right).
        public float TotalTurnDegrees
        {
            get
            {
                float turn = 0f;
                for (int i = 0; i < PieceCount; i++) turn += GetPiece(i).Degrees;
                return turn;
            }
        }

        // One flat straight piece: the shape that can serve as a rest when long enough.
        public bool IsFlatStraight => PieceCount == 1 && !piece0.IsArc && piece0.EndGrade == 0f;

        public void AppendTo(Centerline line)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            for (int i = 0; i < PieceCount; i++)
            {
                Piece piece = GetPiece(i);
                if (piece.IsArc) line.AppendArc(piece.Radius, piece.Degrees, piece.EndGrade);
                else line.AppendStraight(piece.Length, piece.EndGrade);
            }
        }
    }
}
