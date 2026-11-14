namespace ProtoHarness.ChainRush.Flow
{
    // Who a race is run with. Multi is shown locked in the lobby until the network race comes back (DESIGN.md §5-1 M7).
    public enum RaceMode
    {
        Single,
        Multi,
        // The mission map (Tutorial/TutorialCourse): no laps, records or ghost.
        Tutorial,
        // Race against bots; no records or ghost.
        Bots
    }
}
