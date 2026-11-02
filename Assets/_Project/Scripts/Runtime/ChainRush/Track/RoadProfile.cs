using System;

namespace ProtoHarness.ChainRush.Track
{
    // One road cross-section: the surface spans +-HalfWidth around the centerline, is Thickness deep below
    // it, and each long edge either has a guard or is open so a runner can fall off (COURSE.md 3-2, 4-4).
    // The surface is level across: no bank. A tapering profile widens or narrows linearly over the stretch it is
    // built for, from HalfWidth at its start to EndHalfWidth at its end (user 2026-10-22, the Skyway's wide straight).
    // A section may sit to one side of the centerline, its middle Offset metres right of it (EndOffset at the end), and
    // may close either end with a guard square across it (user 2026-11-02, the Skyway's bay and existing highway).
    public readonly struct RoadProfile
    {
        // The scene decks (ChainRushSceneBuilder): 12 m wide, 1.2 m thick.
        public const float DeckHalfWidth = 6f;
        public const float DeckThickness = 1.2f;

        public readonly float HalfWidth;
        public readonly float EndHalfWidth;
        public readonly float Offset;
        public readonly float EndOffset;
        public readonly float Thickness;
        public readonly bool LeftGuard;
        public readonly bool RightGuard;
        public readonly bool EndGuard;
        public readonly bool StartGuard;

        public RoadProfile(float halfWidth, float thickness, bool leftGuard, bool rightGuard)
            : this(halfWidth, halfWidth, thickness, leftGuard, rightGuard)
        {
        }

        public RoadProfile(float halfWidth, float endHalfWidth, float thickness, bool leftGuard, bool rightGuard)
            : this(halfWidth, endHalfWidth, 0f, 0f, thickness, leftGuard, rightGuard, false)
        {
        }

        public RoadProfile(float halfWidth, float endHalfWidth, float offset, float endOffset, float thickness, bool leftGuard, bool rightGuard, bool endGuard, bool startGuard = false)
        {
            if (float.IsNaN(offset) || float.IsInfinity(offset)) throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be finite.");
            if (float.IsNaN(endOffset) || float.IsInfinity(endOffset)) throw new ArgumentOutOfRangeException(nameof(endOffset), endOffset, "End offset must be finite.");
            if (!(halfWidth > 0f) || float.IsInfinity(halfWidth))
                throw new ArgumentOutOfRangeException(nameof(halfWidth), halfWidth, "Half width must be positive and finite.");
            if (!(endHalfWidth > 0f) || float.IsInfinity(endHalfWidth))
                throw new ArgumentOutOfRangeException(nameof(endHalfWidth), endHalfWidth, "End half width must be positive and finite.");
            if (!(thickness > 0f) || float.IsInfinity(thickness))
                throw new ArgumentOutOfRangeException(nameof(thickness), thickness, "Thickness must be positive and finite.");
            HalfWidth = halfWidth;
            EndHalfWidth = endHalfWidth;
            Offset = offset;
            EndOffset = endOffset;
            Thickness = thickness;
            LeftGuard = leftGuard;
            RightGuard = rightGuard;
            EndGuard = endGuard;
            StartGuard = startGuard;
        }

        // The scene deck section with guards on both edges.
        public static RoadProfile Guarded => new RoadProfile(DeckHalfWidth, DeckThickness, true, true);

        public bool HasGuard => LeftGuard || RightGuard || EndGuard || StartGuard;

        // default(RoadProfile) has zero width; builders reject it instead of making an invisible road.
        public bool IsValid => HalfWidth > 0f && EndHalfWidth > 0f && Thickness > 0f;
    }
}
