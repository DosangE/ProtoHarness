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
    // the controls typed since are not lost. When the server's run ends (OnServerEnd), the client plays the same
    // ending from the server's state before it, and predicts nothing after it. Nothing here knows about the network.
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
        // While OnServerEnd replays the server's last tick: that tick (0 otherwise) and the controls the server ran it on.
        private int endReplayTick;
        private TickInput endReplayInput;

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
        // True once the server's end has been taken: the client's run ended on EndTick for EndReason, as the server's did,
        // and nothing is predicted after it. EndTick is -1 and EndReason meaningless until then.
        public bool ServerEnded { get; private set; }
        public int EndTick { get; private set; }
        public RaceWire.EndReason EndReason { get; private set; }
        // True once the server's end could not be taken (OnServerEnd threw, for any reason): the client can no longer end its
        // run as the server's did, and a failed replay leaves the game on a tick that is neither run. Consume, OnServerState
        // and OnServerEnd throw from then on, with FaultReason (the first cause, with its tick; null until then). Only Clear
        // lifts it.
        public bool Faulted { get; private set; }
        public string FaultReason { get; private set; }

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
            endReplayTick = 0;
            endReplayInput = default;
            ServerEnded = false;
            EndTick = -1;
            EndReason = default;
            Faulted = false;
            FaultReason = null;
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
            if (Faulted) throw FaultedError($"the controls for tick {tick} were asked for");
            if (endReplayTick > 0)
            {
                if (tick != endReplayTick)
                    throw new InvalidOperationException($"ClientPredictor: replaying the server's last tick {endReplayTick}, but the game asked for the controls of tick {tick}.");
                return endReplayInput;
            }
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
            if (Faulted) throw FaultedError("a server state arrived");
            if (replaying) throw new InvalidOperationException("ClientPredictor: OnServerState called during a replay.");
            int serverTick = SimSnapshotCodec.TickOf(encoded);
            if (ServerEnded)
                throw new InvalidOperationException($"ClientPredictor: the server's state for tick {serverTick} arrived after its run ended at tick {EndTick}.");
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

        // The server's word that its run ended on tick `tick`, for `reason`, with the controls it ran that tick on. The
        // server's state for the tick before is sent first on the same ordered channel, so it must have been compared already:
        // the client then stands where the server stood before its last tick. If the client's run also ended on that tick for
        // that reason, nothing happens. Otherwise the game goes back to that state and runs the last tick again on the
        // server's controls (a correction). Either way the run is over and nothing is predicted after it. Call it between
        // ticks, never from inside StepTick. Returns true when the game was corrected.
        // An end that cannot be taken is a broken prediction, whatever the reason: one out of order or for an unknown reason,
        // a second end, a replay that throws, or a replay that does not end the run as the server's did (the two simulations
        // differ). Every exception that leaves here leaves the predictor Faulted first, with the cause and the tick in
        // FaultReason, so the caller stops the run (RaceClient). A refused end does not touch the game; a replay that threw
        // or ended differently leaves it where the replay stopped. A call on a predictor that is already Faulted throws
        // without changing the first cause.
        public bool OnServerEnd(int tick, RaceWire.EndReason reason, in TickInput serverInput)
        {
            if (Faulted) throw FaultedError($"the server's end for tick {tick} arrived");
            try
            {
                return TakeServerEnd(tick, reason, serverInput);
            }
            catch (Exception e)
            {
                // Not handled here: the cause is recorded, and the exception goes on unchanged. A fault TakeServerEnd set
                // itself keeps its own, fuller cause.
                if (!Faulted)
                {
                    FaultReason = $"the server's end for tick {tick} ({reason}) could not be taken: {e.GetType().Name}: {e.Message}";
                    Faulted = true;
                }
                throw;
            }
        }

        private bool TakeServerEnd(int tick, RaceWire.EndReason reason, in TickInput serverInput)
        {
            if (replaying) throw new InvalidOperationException("ClientPredictor: OnServerEnd called during a replay.");
            if (!RaceWire.IsKnown(reason))
                throw new ArgumentOutOfRangeException(nameof(reason), reason, $"ClientPredictor: not an end reason (tick {tick}).");
            if (ServerEnded)
                throw new InvalidOperationException($"ClientPredictor: the server's run already ended at tick {EndTick}; another end for tick {tick} arrived.");
            if (ackedTick != tick - 1)
                throw new InvalidOperationException($"ClientPredictor: the server's run ended at tick {tick}, but the newest server state compared is for tick {ackedTick}, not {tick - 1}.");
            if (game.Tick < tick)
                throw new InvalidOperationException($"ClientPredictor: the server's run ended at tick {tick} but the client is at tick {game.Tick}.");
            int before = tick - 1;
            if (stateTicks[before % Window] != before)
                throw new InvalidOperationException($"ClientPredictor: the state for tick {before}, before the server's end at tick {tick}, is gone from the history.");

            bool corrected = false;
            if (!EndedAt(tick, reason))
            {
                if (FirstMismatchTick < 0) FirstMismatchTick = tick;
                Corrections++;
                long start = Stopwatch.GetTimestamp();
                game.RestoreSnapshot(SimSnapshotCodec.Decode(states[before % Window]));
                endReplayTick = tick;
                endReplayInput = serverInput;
                replaying = true;
                try
                {
                    game.StepTick(this);
                }
                finally
                {
                    replaying = false;
                    endReplayTick = 0;
                }
                ReplayedTicks++;
                if (LongestReplay < 1) LongestReplay = 1;
                ReplayMilliseconds += (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
                if (!EndedAt(tick, reason))
                {
                    // The game now stands after a replayed tick that is neither the server's run nor the client's: nothing
                    // after this can be trusted, so the predictor refuses everything until Clear.
                    FaultReason = $"replaying tick {tick} on the server's controls from the state after tick {before} did not end the run as the server's did ({reason}); the client is at tick {game.Tick} (running {game.IsRunning}, failed {game.HasFailed}, finished {game.HasFinished}).";
                    Faulted = true;
                    throw new InvalidOperationException("ClientPredictor: " + FaultReason);
                }
                corrected = true;
            }
            ServerEnded = true;
            EndTick = tick;
            EndReason = reason;
            return corrected;
        }

        // Built only once Faulted is set, so a healthy tick makes no message.
        private InvalidOperationException FaultedError(string what) =>
            new InvalidOperationException($"ClientPredictor: {what} after the prediction broke: {FaultReason}");

        private bool EndedAt(int tick, RaceWire.EndReason reason) =>
            game.Tick == tick && (reason == RaceWire.EndReason.Failed ? game.HasFailed : game.HasFinished);

        private void Store(int tick, byte[] encoded)
        {
            states[tick % Window] = encoded;
            stateTicks[tick % Window] = tick;
        }
    }
}
