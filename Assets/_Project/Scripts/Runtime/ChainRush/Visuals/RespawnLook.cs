using UnityEngine;

namespace ProtoHarness.ChainRush.Visuals
{
    // Presentation only: how coming back from a fall looks (M4, DECISIONS 2026-10-29 "M4 복귀 연출"). The moment a fallen
    // runner is put back on the track (RunnerMotor.IsRespawning turns true) the screen comes up from black over FadeTime
    // (ChainRushHud) while the camera cuts behind the runner (FollowCamera), and the runner's model blinks every
    // BlinkInterval for as long as it is held (RunnerTilt). Everything here reads motor state and never writes back, so
    // the simulation (replay hashes, rollback, netcode) cannot change, and a rollback that repeats the tick is harmless.
    public static class RespawnLook
    {
        // Seconds for the screen to come up from black.
        public const float FadeTime = 0.3f;
        // Seconds the held model is shown, then hidden, then shown again: five blinks in the one-second hold.
        public const float BlinkInterval = 0.1f;

        // How dark the screen still is this long after the runner came back: 1 at that moment, 0 from FadeTime on.
        public static float FadeAlpha(float sinceRespawn) => sinceRespawn < 0f ? 0f : Mathf.Clamp01(1f - sinceRespawn / FadeTime);

        // Whether the held model shows this long after it came back: shown first, then hidden every other interval.
        public static bool Shown(float sinceRespawn) => sinceRespawn < 0f || Mathf.FloorToInt(sinceRespawn / BlinkInterval) % 2 == 0;

        // Notices the moment a runner is put back, for each presentation that reacts to it on its own frame.
        public struct Watch
        {
            private bool wasRespawning;
            private bool hasRespawned;
            private float respawnedAt;

            // True on the frame the runner was put back (held now, not on the last frame).
            public bool Update(bool respawning, float now)
            {
                bool started = respawning && !wasRespawning;
                wasRespawning = respawning;
                if (started)
                {
                    hasRespawned = true;
                    respawnedAt = now;
                }
                return started;
            }

            // Seconds since the runner was last put back; negative when it never was.
            public float Since(float now) => hasRespawned ? now - respawnedAt : -1f;
        }
    }
}
