using System;

namespace ProtoHarness.ChainRush.Flow
{
    // How much each speed class scales the runner's speeds and acceleration (RunnerMotor.Configure, on top of the runner's
    // profile). Fast is the circuit as tuned (exactly 1, so its runs are the scene's bit for bit); the others are the
    // 2026-10-19 candidates, chosen with the M3 speed classes (DECISIONS 2026-10-25 "M3 속도 단계").
    public static class SpeedScale
    {
        public const float Normal = 0.8f;
        public const float Fast = 1f;
        public const float VeryFast = 1.2f;

        public static float Of(SpeedClass speed)
        {
            switch (speed)
            {
                case SpeedClass.Normal: return Normal;
                case SpeedClass.Fast: return Fast;
                case SpeedClass.VeryFast: return VeryFast;
                default: throw new ArgumentOutOfRangeException(nameof(speed), speed, "Unknown speed class.");
            }
        }
    }
}
