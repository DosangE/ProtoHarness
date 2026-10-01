namespace ProtoHarness.ChainRush.Control
{
    // Where gameplay controls come from. ChainRushGame calls Poll once per rendered frame while a run is
    // active and Consume once per simulation tick, so a source decides how frame-rate input becomes tick input.
    public interface IInputSource
    {
        void Poll();
        TickInput Consume();
        void Clear();
    }
}
