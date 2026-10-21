using System;

namespace ProtoHarness.ChainRush.Track
{
    // One road cross-section: the surface spans +-HalfWidth around the centerline, is Thickness deep below
    // it, and each long edge either has a guard or is open so a runner can fall off (COURSE.md 3-2, 4-4).
    // The surface is level across: no bank. A tapering profile widens or narrows linearly over the stretch it is
    // built for, from HalfWidth at its start to EndHalfWidth at its end (user 2026-10-22, the Skyway's wide straight).
    public readonly struct RoadProfile
    {
        // The scene decks (ChainRushSceneBuilder): 12 m wide, 1.2 m thick.
        public const float DeckHalfWidth = 6f;
        public const float DeckThickness = 1.2f;

        public readonly float HalfWidth;
        public readonly float EndHalfWidth;
        public readonly float Thickness;
        public readonly bool LeftGuard;
        public readonly bool RightGuard;

        public RoadProfile(float halfWidth, float thickness, bool leftGuard, bool rightGuard)
            : this(halfWidth, halfWidth, thickness, leftGuard, rightGuard)
        {
        }

        public RoadProfile(float halfWidth, float endHalfWidth, float thickness, bool leftGuard, bool rightGuard)
        {
            if (!(halfWidth > 0f) || float.IsInfinity(halfWidth))
                throw new ArgumentOutOfRangeException(nameof(halfWidth), halfWidth, "Half width must be positive and finite.");
            if (!(endHalfWidth > 0f) || float.IsInfinity(endHalfWidth))
                throw new ArgumentOutOfRangeException(nameof(endHalfWidth), endHalfWidth, "End half width must be positive and finite.");
            if (!(thickness > 0f) || float.IsInfinity(thickness))
                throw new ArgumentOutOfRangeException(nameof(thickness), thickness, "Thickness must be positive and finite.");
            HalfWidth = halfWidth;
            EndHalfWidth = endHalfWidth;
            Thickness = thickness;
            LeftGuard = leftGuard;
            RightGuard = rightGuard;
        }

        // The scene deck section with guards on both edges.
        public static RoadProfile Guarded => new RoadProfile(DeckHalfWidth, DeckThickness, true, true);

        public bool HasGuard => LeftGuard || RightGuard;

        // default(RoadProfile) has zero width; builders reject it instead of making an invisible road.
        public bool IsValid => HalfWidth > 0f && EndHalfWidth > 0f && Thickness > 0f;
    }
}
