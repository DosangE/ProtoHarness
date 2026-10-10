using System;
using System.Diagnostics;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using UnityEngine;

namespace ProtoHarness.Net
{
    // The client's side of prediction and reconciliation (DESIGN.md P3). It is the game's input source: each
    // tick it takes the controls from the real source and remembers them; the caller calls AfterTick once the
    // tick is done, and the state it left behind is remembered too (StepTick bumps the tick and prepares the
    // circuit before it asks for the input, so a snapshot taken inside Consume is not a state between ticks).
    // When the server's state for a past tick arrives (OnServerState) it compares it with
    // what was predicted. Equal: nothing happens. Different: the game goes back to the server's state and the
    // remembered controls are run again up to the present tick (a correction), so the server's word wins and
    // the controls typed since are not lost. Nothing here knows about the network.
    public sealed class ClientPredictor : IInputSource
    {
        // How many ticks the client may run ahead of the newest server state it has compared.
        public const int Window = 512;

        private readonly ChainRushGame game;
        private readonly IInputSource live;
        private readonly TickInput[] inputs = new TickInput[Window];
        private readonly int[] inputTicks = new int[Window];
        private readonly byte[][] states = new byte[Window][];
        private readonly int[] stateTicks = new int[Window];
        private bool replaying;
        private int lastInputTick;
        private int ackedTick;

        public ClientPredictor(ChainRushGame game, IInputSource live)
        {
            this.game = game != null ? game : throw new ArgumentNullException(nameof(game));
            this.live = live ?? throw new ArgumentNullException(nameof(live));
            // The state after each tick is remembered by the game's own tick-completed event, for live ticks and replays alike.
            game.TickCompleted += OnTickCompleted;
            Clear();
        }

        // Stops listening to the game. Call it when the predictor is replaced.
        public void Detach() => game.TickCompleted -= OnTickCompleted;

        private void OnTickCompleted(int tick) => AfterTick();

        // Raised once per live tick (not on replays) with the tick and its controls: the client sends them to the server.
        public event Action<int, TickInput> InputConsumed;

        public int Corrections { get; private set; }
        public int StatesCompared { get; private set; }
        public int ReplayedTicks { get; private set; }
        public int LongestReplay { get; private set; }
        public double ReplayMilliseconds { get; private set; }
        // The most a correction moved the runner on screen, in metres (position before the correction vs after the replay).
        public float LargestJolt { get; private set; }
        // The first server tick that did not match the prediction, or -1.
        public int FirstMismatchTick { get; private set; }
        public int LastComparedTick => ackedTick;
        public int LastInputTick => lastInputTick;

        public void Poll() => live.Poll();

        public void Clear()
        {
            live.Clear();
            for (int i = 0; i < Window; i++)
            {
                inputTicks[i] = -1;
                stateTicks[i] = -1;
                states[i] = null;
            }
            replaying = false;
            lastInputTick = 0;
            ackedTick = 0;
            Corrections = 0;
            StatesCompared = 0;
            ReplayedTicks = 0;
            LongestReplay = 0;
            ReplayMilliseconds = 0d;
            LargestJolt = 0f;
            FirstMismatchTick = -1;
        }

        public TickInput Consume()
        {
            int tick = game.Tick;
            if (!replaying && tick - ackedTick >= Window)
                throw new InvalidOperationException($"ClientPredictor: {tick - ackedTick} ticks without a server state to compare (window {Window}); the history would be overwritten.");
            TickInput input;
            if (replaying)
            {
                if (inputTicks[tick % Window] != tick)
                    throw new InvalidOperationException($"ClientPredictor: the controls for tick {tick} are gone from the history.");
                input = inputs[tick % Window];
            }
            else
            {
                input = live.Consume();
                inputs[tick % Window] = input;
                inputTicks[tick % Window] = tick;
                lastInputTick = tick;
                InputConsumed?.Invoke(tick, input);
            }
            return input;
        }

        // Remembers the state the game is in after the tick that just ran. TickCompleted calls it after every tick; call it
        // once yourself after StartRun, for tick 0. A run that has just ended has no state to remember; the server's states
        // stop there too.
        public void AfterTick()
        {
            if (!game.IsRunning) return;
            Store(game.Tick, SimSnapshotCodec.Encode(game.CaptureSnapshot()));
        }

        // The server's state after tick N (encoded). Call it between ticks, never from inside StepTick.
        // Returns true when it differed from the prediction and the game was corrected.
        public bool OnServerState(byte[] encoded)
        {
            if (replaying) throw new InvalidOperationException("ClientPredictor: OnServerState called during a replay.");
            int serverTick = SimSnapshotCodec.TickOf(encoded);
            if (serverTick > game.Tick)
                throw new InvalidOperationException($"ClientPredictor: the server's state is for tick {serverTick} but the client is at tick {game.Tick}.");
            if (serverTick < ackedTick)
                throw new InvalidOperationException($"ClientPredictor: the server's state for tick {serverTick} arrived after tick {ackedTick} was compared.");
            // Between ticks, so the present state can be taken now if AfterTick was not called for it.
            if (serverTick == game.Tick && stateTicks[serverTick % Window] != serverTick)
                Store(serverTick, SimSnapshotCodec.Encode(game.CaptureSnapshot()));
            if (stateTicks[serverTick % Window] != serverTick)
                throw new InvalidOperationException($"ClientPredictor: the predicted state for tick {serverTick} is gone from the history.");

            StatesCompared++;
            ackedTick = serverTick;
            if (SimSnapshotCodec.SameState(states[serverTick % Window], encoded)) return false;

            if (FirstMismatchTick < 0) FirstMismatchTick = serverTick;
            Corrections++;
            long start = Stopwatch.GetTimestamp();
            // Where the runner is drawn now, to measure how far a correction moves it.
            Vector3 before = game.IsRunning ? game.CaptureSnapshot().Position : default;
            game.RestoreSnapshot(SimSnapshotCodec.Decode(encoded));
            Store(serverTick, encoded);
            int target = lastInputTick;
            int replayed = 0;
            replaying = true;
            try
            {
                while (game.Tick < target && game.IsRunning)
                {
                    game.StepTick(this);
                    replayed++;
                }
            }
            finally
            {
                replaying = false;
            }
            if (game.IsRunning)
            {
                float jolt = Vector3.Distance(before, game.CaptureSnapshot().Position);
                if (jolt > LargestJolt) LargestJolt = jolt;
            }
            ReplayedTicks += replayed;
            if (replayed > LongestReplay) LongestReplay = replayed;
            ReplayMilliseconds += (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
            return true;
        }

        private void Store(int tick, byte[] encoded)
        {
            states[tick % Window] = encoded;
            stateTicks[tick % Window] = tick;
        }
    }
}
