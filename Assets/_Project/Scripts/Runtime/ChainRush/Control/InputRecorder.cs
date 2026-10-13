using System;

namespace ProtoHarness.ChainRush.Control
{
    // Wraps another input source and writes down what the simulation consumed from it, one TickInput per
    // tick. Clear (ChainRushGame calls it when the source is set and at every StartRun) is the start of a
    // new run: it clears the inner source and the log.
    public sealed class InputRecorder : IInputSource
    {
        private readonly IInputSource inner;
        private readonly InputLog log;

        public InputRecorder(IInputSource inner, InputLog log)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Poll() => inner.Poll();

        public TickInput Consume()
        {
            TickInput input = inner.Consume();
            log.Add(input);
            return input;
        }

        public void Clear()
        {
            inner.Clear();
            log.Clear();
        }
    }
}
