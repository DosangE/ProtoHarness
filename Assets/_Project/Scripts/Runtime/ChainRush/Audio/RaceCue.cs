namespace ProtoHarness.ChainRush.Audio
{
    // The race's sound effects, by the moment they play. The values are the clip numbers: ChainRushAudio makes one clip
    // for each (its seed and length come from the number), and ChainRushGame's plain fallback has clips for the first four.
    public enum RaceCue
    {
        // A jump left the ground.
        Jump,
        // A move went through: grapple hooked or empowered, booster fired, corner pole hooked, run finished.
        Action,
        // An attack swung.
        Attack,
        // The runner took a hit.
        Damage,
        // Never played since the table was made (4e6f12b); named after how they sound. Removing them changes the table.
        Beeps,
        Crash,
        Buzz,
        // A foot touched down while running (RunnerAnimation).
        Footstep,
        // Back on the ground after the air (RunnerAnimation).
        Land,
    }
}
