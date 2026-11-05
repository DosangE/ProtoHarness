namespace ProtoHarness.ChainRush.Audio
{
    // The race's sound effects, by the moment they play. The values are the clip numbers: ChainRushAudio makes one clip
    // for each (its seed and length come from the number), and ChainRushGame's plain fallback has clips for the first four.
    // New cues go at the end, so the clips before them stay as they are.
    public enum RaceCue
    {
        // A jump left the ground.
        Jump,
        // The run finished.
        Finish,
        // An attack swung.
        Attack,
        // The runner took a hit.
        Damage,
        // Never played since the table was made (4e6f12b); named after how it sounds. Removing it changes the table.
        Beeps,
        // A guard hit hard enough to hear (RunnerMotor.GuardHitCueSpeed). A rub along a guard stays quiet (user 2026-11-05).
        GuardHit,
        // Never played, as Beeps.
        Buzz,
        // A foot touched down while running (RunnerAnimation).
        Footstep,
        // Back on the ground after the air (RunnerAnimation).
        Land,
        // A gauge slot went on a boost: the slingshot fired or the grapple was empowered.
        Boost,
        // The grapple hooked an anchor.
        Grapple,
        // A corner or straight pole was hooked.
        PoleHook,
        // A boost zone was crossed.
        BoostZone,
        // A fall put the runner back on the track.
        Respawn,
    }
}
