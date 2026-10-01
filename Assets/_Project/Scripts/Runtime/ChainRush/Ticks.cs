using System;
using UnityEngine;

namespace ProtoHarness.ChainRush
{
    // Gameplay simulation advances in whole ticks of this length. ChainRushGame verifies
    // that Time.fixedDeltaTime matches it, so a tick is one FixedUpdate.
    public static class Ticks
    {
        public const float Seconds = 0.02f;

        // Float division can land just above an exact multiple (0.3f / 0.02f), so a small
        // tolerance keeps exact multiples from rounding up an extra tick.
        private const float CeilTolerance = 1e-3f;

        public static int FromSeconds(float seconds)
        {
            if (seconds < 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Duration must be a finite, non-negative number of seconds.");
            return Mathf.CeilToInt(seconds / Seconds - CeilTolerance);
        }

        public static float ToSeconds(int ticks) => ticks * Seconds;
    }
}
