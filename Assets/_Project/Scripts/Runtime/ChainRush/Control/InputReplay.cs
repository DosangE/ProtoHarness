using System;

namespace ProtoHarness.ChainRush.Control
{
    // Plays an InputLog back one tick at a time. Past the end it gives an idle input. It reads no device:
    // Poll does nothing. Clear (the game calls it at SetInputSource and StartRun) rewinds to the first tick,
    // so SetInputSource(replay) followed by StartRun() replays the whole log.
    public sealed class InputReplay : IInputSource
    {
        private readonly InputLog log;
        private int position;

        public InputReplay(InputLog log) => this.log = log ?? throw new ArgumentNullException(nameof(log));

        // Ticks played so far.
        public int Position => position;

        // True once every recorded tick has been played (also for an empty log).
        public bool Finished => position >= log.Count;

        public void Poll() { }

        public TickInput Consume() => position < log.Count ? log[position++] : default;

        public void Clear() => position = 0;
    }
}
