using System;
using UnityEngine;

namespace ProtoHarness.ChainRush.Visuals
{
    // Presentation timing and sizes shared by the HUD and world effects, with no simulation state or clock of their own.
    public static class FeedbackLook
    {
        public const float PolePulsePeriod = 0.5f;
        public const float PolePulseMin = 1f;
        public const float PolePulseMax = 1.3f;
        public const float PoleEnterTime = 0.3f;
        public const float PoleEnterScale = 1.8f;
        // The faint fill inside the pole's screen target, breathing with the ring between these alphas.
        public const float TargetFillMin = 0.08f;
        public const float TargetFillMax = 0.22f;
        // An off-screen pole's target is pinned this far inside the screen's edge, this much smaller, with an arrow this
        // far beyond it pointing at the pole (user 2026-10-29 "화면 밖에 있어도 그 방향에 있다고").
        public const float EdgeInset = 60f;
        public const float EdgeTargetScale = 0.8f;
        public const float ArrowGap = 8f;
        public const float BoostRingTime = 0.3f;
        public const float BoostFlashTime = 0.4f;
        public const float GaugeBurstTime = 0.4f;
        public const float HookRingTime = 0.3f;
        public const float BracketTightenTime = 0.25f;
        public const float BracketStartScale = 1.6f;
        public const float MissShakeTime = 0.3f;
        public const float MissShakeSize = 12f;

        // One at the event, zero once its duration has passed or before it has ever happened.
        public static float Fade(float since, float duration)
        {
            RequireFinite(since, nameof(since));
            if (!(duration > 0f) || float.IsInfinity(duration))
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "Duration must be finite and positive.");
            return since < 0f ? 0f : Mathf.Clamp01(1f - since / duration);
        }

        // A smooth 0..1 pulse starting at its smallest, repeating every half second.
        public static float Pulse(float time)
        {
            RequireFinite(time, nameof(time));
            return (1f - Mathf.Cos(time * (Mathf.PI * 2f / PolePulsePeriod))) * 0.5f;
        }

        // The entry flash settles into the regular pulse instead of snapping to it.
        public static float PoleScale(float since)
        {
            float pulse = Mathf.Lerp(PolePulseMin, PolePulseMax, Pulse(since));
            return Mathf.Lerp(pulse, PoleEnterScale, Fade(since, PoleEnterTime));
        }

        public static float BracketScale(float since)
            => Mathf.Lerp(1f, BracketStartScale, Fade(since, BracketTightenTime));

        // Three sideways shakes, fading to the bracket's original position.
        public static float ShakeOffset(float since)
            => Mathf.Sin(since * (Mathf.PI * 6f / MissShakeTime)) * MissShakeSize * Fade(since, MissShakeTime);

        // Whether a point in HUD coordinates (y down) lies on a screen this wide and high.
        public static bool IsOnScreen(Vector2 point, float width, float height)
            => point.x >= 0f && point.x <= width && point.y >= 0f && point.y <= height;

        // Where an off-screen target is pinned: where the line from the screen's center toward point leaves the screen
        // inset by EdgeInset, and that line's unit direction. A point at the center itself points right.
        public static Vector2 PinToEdge(Vector2 point, float width, float height, out Vector2 direction)
        {
            if (!(width > EdgeInset * 2f) || !(height > EdgeInset * 2f))
                throw new ArgumentOutOfRangeException(nameof(width), $"The screen ({width} x {height}) must be wider and higher than twice EdgeInset.");
            var center = new Vector2(width * 0.5f, height * 0.5f);
            Vector2 offset = point - center;
            direction = offset.sqrMagnitude > 1e-6f ? offset.normalized : Vector2.right;
            float across = direction.x != 0f ? (center.x - EdgeInset) / Mathf.Abs(direction.x) : float.PositiveInfinity;
            float down = direction.y != 0f ? (center.y - EdgeInset) / Mathf.Abs(direction.y) : float.PositiveInfinity;
            return center + direction * Mathf.Min(across, down);
        }

        private static void RequireFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, value, "Time must be finite.");
        }
    }
}
