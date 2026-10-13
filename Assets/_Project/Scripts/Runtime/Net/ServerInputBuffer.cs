using System;
using System.Collections.Generic;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;

namespace ProtoHarness.Net
{
    // The server's input source: the controls clients sent, by tick. The server runs at its own pace, so an
    // input that has not arrived when its tick is due is replaced by the previous tick's input (counted in
    // Missed) and the input is dropped when it does arrive (Late). The server's word is final, so the client
    // finds out through the state the server sends. After each tick the caller calls AfterTick, which hands
    // out the state that tick left behind (StateReady): what the clients compare their predictions with. The
    // tick that ends the run (a fall or a finish) leaves no state; AfterTick raises RunEnded for it instead, once.
    public sealed class ServerInputBuffer : IInputSource
    {
        private readonly ChainRushGame game;
        private readonly Dictionary<int, TickInput> pending = new Dictionary<int, TickInput>(256);
        // The input Consume handed out last (for consumedTick), whether it arrived or was repeated.
        private TickInput last;
        private int consumedTick;
        private bool endRaised;

        public ServerInputBuffer(ChainRushGame game)
        {
            this.game = game != null ? game : throw new ArgumentNullException(nameof(game));
            // The state after each tick is handed out by the game's own tick-completed event.
            game.TickCompleted += OnTickCompleted;
        }

        // Stops listening to the game. Call it when the buffer is replaced.
        public void Detach() => game.TickCompleted -= OnTickCompleted;

        private void OnTickCompleted(int tick) => AfterTick();

        // The encoded state after the tick that just ran (tick 0 is the state right after StartRun).
        public event Action<byte[]> StateReady;

        // Raised once, after the tick that ended the run, with that tick, why it ended, and the controls the tick ran on
        // (the repeated ones when its own input was missed): what a client needs to play the same ending.
        public event Action<int, RaceWire.EndReason, TickInput> RunEnded;

        public int Received { get; private set; }
        public int Missed { get; private set; }
        public int Late { get; private set; }
        public int Buffered => pending.Count;

        public void Receive(int tick, in TickInput input)
        {
            if (tick < 1) throw new ArgumentOutOfRangeException(nameof(tick), tick, "Ticks count from 1.");
            Received++;
            if (tick <= consumedTick)
            {
                Late++;
                return;
            }
            if (!pending.TryAdd(tick, input)) throw new InvalidOperationException($"ServerInputBuffer: two inputs for tick {tick}.");
        }

        // True once the first tick's input is here and at least `ticks` inputs are waiting: the run can start
        // with that much slack against late inputs.
        public bool HasBuffered(int ticks) => pending.ContainsKey(1) && pending.Count >= ticks;

        public void Poll() { }

        // StartRun calls this: the inputs already received stay, the run's own bookkeeping starts over.
        public void Clear()
        {
            last = default;
            consumedTick = 0;
            endRaised = false;
            Missed = 0;
            Late = 0;
        }

        // Hands out the state the game is in after the tick that just ran. TickCompleted calls it after every tick; call it
        // once yourself after StartRun, for tick 0. A run that has just ended has no state to hand out: the first call after
        // a fall or a finish raises RunEnded instead, and later calls do nothing.
        public void AfterTick()
        {
            if (game.IsRunning)
            {
                StateReady?.Invoke(SimSnapshotCodec.Encode(game.CaptureSnapshot()));
                return;
            }
            if (endRaised || !(game.HasFailed || game.HasFinished)) return;
            if (consumedTick != game.Tick)
                throw new InvalidOperationException($"ServerInputBuffer: the run ended at tick {game.Tick}, but the last input this buffer handed out was for tick {consumedTick}.");
            endRaised = true;
            RunEnded?.Invoke(game.Tick, game.HasFailed ? RaceWire.EndReason.Failed : RaceWire.EndReason.Finished, last);
        }

        public TickInput Consume()
        {
            int tick = game.Tick;
            consumedTick = tick;
            if (pending.Remove(tick, out TickInput input))
            {
                last = input;
                return input;
            }
            Missed++;
            return last;
        }
    }
}
