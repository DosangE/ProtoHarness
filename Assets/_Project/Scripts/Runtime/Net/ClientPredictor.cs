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
    // ending from the server's state before it, and predicts nothing after it. When the client's own run ends first and
    // the server's state for that tick or a later one arrives, the server's run was still going there: the game goes back
    // to that state, runs the remembered controls again, and then runs on live controls until it is as far ahead of the
    // server as it was when its run ended (a catch-up); an older state whose correction puts the ended run back to going
    // catches up the same way. Anything that cannot be taken, or a restart, breaks the prediction for good (Faulted).
    // Nothing here knows about the network.
    public sealed class ClientPredictor : IInputSource
    {
        // How many ticks the client may run ahead of the newest server state it has compared.
        public const int Window = 512;

        private readonly ChainRushGame game;
        private readonly IInputSource live;
        // The last tick a catch-up may run (the caller's last tick; int.MaxValue: no limit).
        private readonly int catchUpLimit;
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
        // True once the predictor has given the controls of a live tick: from then on a Clear is a restart, not a start. Not
        // set by AfterTick, which hears every tick of the game, also those run on another source before this one is set.
        private bool armed;

        // catchUpLimit: the last tick a catch-up may run. The caller that stops sending controls after some tick passes that
        // tick, so a catch-up never runs past it (RaceClient's target).
        public ClientPredictor(ChainRushGame game, IInputSource live, int catchUpLimit = int.MaxValue)
        {
            this.game = game != null ? game : throw new ArgumentNullException(nameof(game));
            this.live = live ?? throw new ArgumentNullException(nameof(live));
            if (catchUpLimit < 1) throw new ArgumentOutOfRangeException(nameof(catchUpLimit), catchUpLimit, "ClientPredictor: a catch-up limit is a tick, 1 or more.");
            this.catchUpLimit = catchUpLimit;
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
        // True once the prediction broke: a server state or the server's end could not be taken (OnServerState or OnServerEnd
        // threw, for any reason), or the run was restarted (Clear after the run began). The client can no longer follow the
        // server's run, and a failed replay leaves the game on a tick that is neither run. Consume, OnServerState and
        // OnServerEnd throw from then on, with FaultReason (the first cause, with its tick; null until then). Nothing lifts
        // it: a new race needs a new predictor.
        public bool Faulted { get; private set; }
        public string FaultReason { get; private set; }
        // The tick the client's run ended on (a fall or a finish, seen after the tick that ended it, whichever way that tick
        // ran), and how far ahead of the newest compared server state the client then was (LastInputTick - LastComparedTick).
        // Both -1 while the run is going: a correction that puts the run back to going forgets them.
        public int ClientEndTick { get; private set; }
        public int ClientEndLead { get; private set; }
        // Catch-ups that ran at least one tick, and the live ticks they ran in all. Not part of ReplayedTicks: those ticks
        // are new controls, sent to the server like any live tick.
        public int CatchUps { get; private set; }
        public int CaughtUpTicks { get; private set; }

        public void Poll() => live.Poll();

        // Starts the predictor over, before its run begins (SetInputSource and StartRun both call it). Once the predictor
        // has given the controls of a tick it is a restart (R or Enter, ChainRushGame.Update), which a network race does
        // not have: the server's run goes on. So it then breaks the prediction instead (Faulted) and clears nothing; on a
        // predictor that is already Faulted it does nothing.
        public void Clear()
        {
            if (Faulted) return;
            if (armed)
            {
                FaultReason = $"the run was restarted after tick {lastInputTick} (the server's state for tick {ackedTick} was the newest compared); a network race has no restart.";
                Faulted = true;
                return;
            }
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
            ClientEndTick = -1;
            ClientEndLead = -1;
            CatchUps = 0;
            CaughtUpTicks = 0;
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
                armed = true;
                InputConsumed?.Invoke(tick, input);
            }
            return input;
        }

        // Remembers the state the game is in after the tick that just ran. TickCompleted calls it after every tick; call it
        // once yourself after StartRun, for tick 0. A run that has just ended has no state to remember (the server's states
        // stop there too, if its run ended there as well); the first call after a fall or a finish remembers the tick instead,
        // with how far ahead of the server the client was (ClientEndTick, ClientEndLead). A paused run is not an ended one.
        public void AfterTick()
        {
            if (!game.IsRunning)
            {
                if ((game.HasFailed || game.HasFinished) && ClientEndTick != game.Tick)
                {
                    ClientEndTick = game.Tick;
                    ClientEndLead = lastInputTick - ackedTick;
                }
                return;
            }
            Store(game.Tick, SimSnapshotCodec.Encode(game.CaptureSnapshot()));
        }

        // The server's state after tick N (encoded). Call it between ticks, never from inside StepTick.
        // Returns true when it differed from the prediction and the game was corrected.
        // If the client's run has ended on tick E and the state is for E or later (even past the client's tick), the
        // server's run was still going on that tick: had it ended there too it would have sent its end instead of a state.
        // That is always a correction: the game goes back to the state, the remembered controls are run again up to the
        // last tick that has them, and then live controls are run (and sent, InputConsumed) until the client is
        // ClientEndLead ticks past the state, as far ahead as when its run ended, but never past the catch-up limit. A run
        // that ends again during either stops there and is remembered again. Such a correction is not in LargestJolt (an
        // ended run has no position to measure from).
        // A state for a tick before the client's end that puts the run back to going is an ordinary correction, but the client
        // stood still while its run was over, so after the replay it catches up the same way, to ClientEndLead ticks past
        // the state.
        // A state that cannot be taken is a broken prediction, as an end that cannot be: every exception that leaves here
        // leaves the predictor Faulted first, with the cause and the tick in FaultReason, so the caller stops the run
        // (RaceClient). A refused state does not touch the game; a replay that threw leaves it where the replay stopped.
        public bool OnServerState(byte[] encoded)
        {
            if (Faulted) throw FaultedError("a server state arrived");
            int serverTick = -1;
            try
            {
                serverTick = SimSnapshotCodec.TickOf(encoded);
                return TakeServerState(serverTick, encoded);
            }
            catch (Exception e)
            {
                // Not handled here: the cause is recorded, and the exception goes on unchanged.
                if (!Faulted)
                {
                    string which = serverTick >= 0 ? $"the server's state for tick {serverTick}" : "a server state";
                    FaultReason = $"{which} could not be taken (the client at tick {game.Tick}): {e.GetType().Name}: {e.Message}";
                    Faulted = true;
                }
                throw;
            }
        }

        private bool TakeServerState(int serverTick, byte[] encoded)
        {
            if (replaying) throw new InvalidOperationException("ClientPredictor: OnServerState called during a replay.");
            if (ServerEnded)
                throw new InvalidOperationException($"ClientPredictor: the server's state for tick {serverTick} arrived after its run ended at tick {EndTick}.");
            bool pastClientEnd = ClientEndTick >= 0 && serverTick >= ClientEndTick;
            if (serverTick > game.Tick && !pastClientEnd)
                throw new InvalidOperationException($"ClientPredictor: the server's state is for tick {serverTick} but the client is at tick {game.Tick}.");
            if (serverTick < ackedTick)
                throw new InvalidOperationException($"ClientPredictor: the server's state for tick {serverTick} arrived after tick {ackedTick} was compared.");
            if (pastClientEnd) return TakeStatePastClientEnd(serverTick, encoded);
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
            // Where the runner is drawn now, to measure how far a correction moves it: the remembered state of the present
            // tick, running or paused (RaceClient pauses on its last tick). An ended run remembers no state for its last tick.
            int now = game.Tick;
            bool drawnKnown = (game.IsRunning || game.IsPaused) && stateTicks[now % Window] == now;
            Vector3 before = drawnKnown ? SimSnapshotCodec.Decode(states[now % Window]).Position : default;
            // An ended run stood still while the server's states came; if the replay puts it back to going it catches up.
            bool wasEnded = ClientEndTick >= 0;
            int endLead = ClientEndLead;
            game.RestoreSnapshot(SimSnapshotCodec.Decode(encoded));
            // The run is going again; an end the replay reaches is remembered afresh.
            ForgetClientEnd();
            Store(serverTick, encoded);
            int replayed = ReplayTo(lastInputTick);
            if (drawnKnown && game.IsRunning)
            {
                float jolt = Vector3.Distance(before, game.CaptureSnapshot().Position);
                if (jolt > LargestJolt) LargestJolt = jolt;
            }
            ReplayedTicks += replayed;
            if (replayed > LongestReplay) LongestReplay = replayed;
            ReplayMilliseconds += (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
            if (wasEnded && game.IsRunning)
            {
                if (endLead < 0)
                    throw new InvalidOperationException($"ClientPredictor: the client's run had ended with a lead of {endLead} ticks over the server's states.");
                CatchUp(serverTick + endLead);
            }
            return true;
        }

        // The server's state for a tick at or past the one the client's run ended on (OnServerState checked the order).
        private bool TakeStatePastClientEnd(int serverTick, byte[] encoded)
        {
            if (game.IsRunning || game.Tick != ClientEndTick || !(game.HasFailed || game.HasFinished))
                throw new InvalidOperationException($"ClientPredictor: the client's run was seen to end at tick {ClientEndTick}, but the game is at tick {game.Tick} (running {game.IsRunning}, failed {game.HasFailed}, finished {game.HasFinished}).");
            int lead = ClientEndLead;
            if (lead < 0)
                throw new InvalidOperationException($"ClientPredictor: the client's run ended at tick {ClientEndTick} with a lead of {lead} ticks over the server's states.");
            StatesCompared++;
            ackedTick = serverTick;
            if (FirstMismatchTick < 0) FirstMismatchTick = serverTick;
            Corrections++;
            long start = Stopwatch.GetTimestamp();
            game.RestoreSnapshot(SimSnapshotCodec.Decode(encoded));
            ForgetClientEnd();
            Store(serverTick, encoded);
            int replayed = ReplayTo(lastInputTick);
            ReplayedTicks += replayed;
            if (replayed > LongestReplay) LongestReplay = replayed;
            ReplayMilliseconds += (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
            CatchUp(serverTick + lead);
            return true;
        }

        // Runs the remembered controls up to `target`, or until the run ends.
        private int ReplayTo(int target)
        {
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
            return replayed;
        }

        // Runs live ticks (controls taken, remembered and sent, as on any live tick) up to `target` but never past the
        // catch-up limit, or until the run ends. Only ticks past the last one that has controls: a tick whose controls were
        // sent once is never sent again.
        private void CatchUp(int target)
        {
            int until = Math.Min(target, catchUpLimit);
            if (game.IsRunning && game.Tick < lastInputTick)
                throw new InvalidOperationException($"ClientPredictor: a catch-up from tick {game.Tick} would take new controls for ticks up to {lastInputTick}, which were already sent.");
            int ran = 0;
            while (game.IsRunning && game.Tick < until)
            {
                game.StepTick(this);
                ran++;
            }
            CaughtUpTicks += ran;
            if (ran > 0) CatchUps++;
        }

        private void ForgetClientEnd()
        {
            ClientEndTick = -1;
            ClientEndLead = -1;
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
