using System;
using System.Collections.Generic;
using System.IO;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Visuals;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ProtoHarness.Net
{
    // The predicting side: once the server says every racer is here (race.go) it counts down and runs the game on its own
    // controls (the keyboard cannot start or restart it: ManualStart off), sends every tick's controls to the server through a
    // DelayedSender, and hands the server's states for this racer to ClientPredictor, which corrects the game when they
    // disagree (DESIGN.md P3). The other racers' positions go to a RemoteInterpolator and are drawn by a RemoteGhost. After
    // `targetTicks` ticks it stops sending and, once the server's state for the last of them has been compared, it is
    // Finished. It is Finished too once the server has said that this racer's run ended within those ticks (race.end).
    // A state or an end the predictor cannot take (any exception from OnServerState or OnServerEnd), or a restart (StartRun
    // called on the game mid-race), leaves it Faulted: the run is then stopped for good (RACE-CLIENT-FAULT), not Finished. The standings the
    // server sends (race.standings) are kept as they come (Standings, You), and so is the order of the racers still running
    // (race.order: Order, OrderTick); they only tell, the run never reads them. With showRemote they are drawn
    // (RaceStandingsView), and the ghost is hidden once the standings say the other racer's run has ended.
    public sealed class RaceClient : MonoBehaviour
    {
        private ChainRushGame game;
        private IInputSource live;
        private string address;
        private ushort port;
        private double delaySeconds;
        private double jitterSeconds;
        private int seed;
        private int targetTicks;
        private bool showRemote;
        private bool quitWhenDone;
        private float timeoutSeconds;
        private float connectedAt;
        private bool gaveUp;
        private bool faultReported;
        private NetworkManager manager;
        private ClientPredictor predictor;
        private DelayedSender delayed;
        private RemoteInterpolator remote;
        private bool started;
        private bool paused;
        private bool reported;
        private int remoteSamples;
        private RaceStandings.Entry[] standings;
        private int you = -1;
        private int standingsReceived;
        private int[] order;
        private int orderTick = -1;
        private int orderReceived;
        private bool goReceived;

        public ClientPredictor Predictor => predictor;
        // True once the server has said every racer has a client (race.go) and the countdown has begun.
        public bool GoReceived => goReceived;
        // The newest standings from the server, by racer, or null before the first arrive.
        public IReadOnlyList<RaceStandings.Entry> Standings => standings;
        // Which racer the server made this client (from the standings), or -1 before the first arrive.
        public int You => you;
        public int StandingsReceived => standingsReceived;
        // The newest order from the server: each racer's place among those running (RaceOrder, 0 = none), or null before the
        // first arrives; and the tick of their own runs it compared them on.
        public IReadOnlyList<int> Order => order;
        public int OrderTick => orderTick;
        public int OrderReceived => orderReceived;
        public RemoteInterpolator Remote => remote;
        // The picture of the other racer, or null when the client was made without one.
        public RemoteGhost Ghost { get; private set; }
        // The standings panel, or null when the client was made without one (as the ghost).
        public RaceStandingsView StandingsView { get; private set; }
        public bool Connected => started;
        public int RemoteSamples => remoteSamples;
        // True once the server's state for the last tick has been compared, or the server's end has been taken.
        public bool Finished => predictor != null && (predictor.ServerEnded || predictor.LastComparedTick >= targetTicks);

        // Raised for every position of another racer that arrives, with its tick (before it is drawn).
        public event Action<int, Vector3> RemoteReceived;

        // timeoutSeconds: with quitWhenDone, how long after connecting the client waits for the server's last state or its end
        // before it gives up and ends the process (0 = forever). The server says when this racer's run ends, so a timeout means
        // something did not arrive.
        public void Configure(ChainRushGame game, IInputSource live, string address, ushort port, double delaySeconds, double jitterSeconds, int seed, int targetTicks, bool showRemote, bool quitWhenDone, float timeoutSeconds = 0f)
        {
            this.game = game != null ? game : throw new ArgumentNullException(nameof(game));
            this.live = live ?? throw new ArgumentNullException(nameof(live));
            if (string.IsNullOrEmpty(address)) throw new ArgumentException("An address is needed.", nameof(address));
            if (targetTicks < 1) throw new ArgumentOutOfRangeException(nameof(targetTicks), targetTicks, "The client has to run at least one tick.");
            this.address = address;
            this.port = port;
            this.delaySeconds = delaySeconds;
            this.jitterSeconds = jitterSeconds;
            this.seed = seed;
            this.targetTicks = targetTicks;
            this.showRemote = showRemote;
            this.quitWhenDone = quitWhenDone;
            if (timeoutSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), timeoutSeconds, "The timeout must be zero or more.");
            this.timeoutSeconds = timeoutSeconds;
        }

        public void Begin()
        {
            if (game == null) throw new InvalidOperationException("RaceClient: call Configure before Begin.");
            // Controls past the target are not sent (OnInputConsumed), so a catch-up never runs past it.
            predictor = new ClientPredictor(game, live, targetTicks);
            delayed = new DelayedSender(SendInput, delaySeconds, jitterSeconds, seed);
            predictor.InputConsumed += OnInputConsumed;
            // The server starts the run (race.go); the keyboard and the HUD may not start or restart it.
            game.ManualStart = false;
            game.CountdownFinished += OnCountdownFinished;
            remote = new RemoteInterpolator();
            if (showRemote)
            {
                RunnerTilt tilt = game.Player.GetComponent<RunnerTilt>();
                if (tilt == null) throw new InvalidOperationException("RaceClient: the runner has no RunnerTilt, so there is no model to draw the other racer with.");
                Ghost = gameObject.AddComponent<RemoteGhost>();
                Ghost.Initialize(remote, tilt.Body);
                StandingsView = gameObject.AddComponent<RaceStandingsView>();
                StandingsView.Initialize(this);
            }
            manager = RaceNetwork.Create(address, port);
            manager.OnClientConnectedCallback += OnConnected;
            if (!manager.StartClient()) throw new InvalidOperationException($"RaceClient: could not start a client for {address}:{port}.");
            manager.CustomMessagingManager.RegisterNamedMessageHandler(RaceNetwork.StateMessage, OnState);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(RaceNetwork.EndMessage, OnEnd);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(RaceNetwork.RemoteMessage, OnRemote);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(RaceNetwork.StandingsMessage, OnStandings);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(RaceNetwork.OrderMessage, OnOrder);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(RaceNetwork.GoMessage, OnGo);
        }

        // Closes the connection (the server notices when its client leaves).
        public void Disconnect()
        {
            if (manager != null && manager.IsListening) manager.Shutdown();
        }

        // Connected: the run waits for the server's go (OnGo), which comes once every racer has a client.
        private void OnConnected(ulong id)
        {
            if (started) return;
            started = true;
            connectedAt = Time.realtimeSinceStartup;
            Debug.Log($"RACE-CLIENT connected as {id}; waiting for the go.");
        }

        // Every racer has a client: count down (ChainRushGame.StartCountdown), then the run starts (OnCountdownFinished).
        // The server starts every racer together once each has sent enough inputs, so a client that hears the go a little
        // later than another only makes the other wait in the server's buffer. A second go is a bug and throws (Netcode logs it).
        private void OnGo(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte[] payload);
            int racers = RaceWire.DecodeGo(payload);
            if (goReceived) throw new InvalidDataException($"RaceClient: a second go (for {racers} racers).");
            goReceived = true;
            game.SetInputSource(predictor);
            game.StartCountdown();
            Debug.Log($"RACE-CLIENT-GO {racers} racers; counting down {ChainRushGame.CountdownSeconds:F0} s.");
        }

        private void OnCountdownFinished()
        {
            predictor.AfterTick();
            Debug.Log("RACE-CLIENT-COUNTDOWN done; run started.");
        }

        private void OnInputConsumed(int tick, TickInput input)
        {
            if (tick > targetTicks) return;
            delayed.Send(RaceWire.EncodeInput(tick, input), Time.realtimeSinceStartupAsDouble);
        }

        private void SendInput(byte[] bytes)
        {
            using var writer = new FastBufferWriter(bytes.Length + 8, Allocator.Temp);
            writer.WriteValueSafe(bytes);
            manager.CustomMessagingManager.SendNamedMessage(RaceNetwork.InputMessage, NetworkManager.ServerClientId, writer);
        }

        // Runs between ticks (the network update), never inside StepTick.
        private void OnState(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte[] payload);
            // The run goes on past the target with idle controls; the server never saw those, so they are not compared.
            if (SimSnapshotCodec.TickOf(payload) > targetTicks) return;
            try
            {
                predictor.OnServerState(payload);
            }
            finally
            {
                // As in OnEnd: the exception goes on to Netcode, and the game is stopped before the next tick.
                if (predictor.Faulted) HaltOnFault();
            }
        }

        // Runs between ticks (the network update), never inside StepTick. Arrives after the state for the tick before it.
        private void OnEnd(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte[] payload);
            TickInput input = RaceWire.DecodeEnd(payload, out int tick, out RaceWire.EndReason reason);
            // An end past the target ran on controls the client never sent; like a state there, it is not compared.
            if (tick > targetTicks) return;
            bool corrected;
            try
            {
                corrected = predictor.OnServerEnd(tick, reason, input);
            }
            finally
            {
                // The exception itself goes on to Netcode, which logs it. The game is stopped here, before the next
                // FixedUpdate can run a tick on the broken prediction and send an input the server already has.
                if (predictor.Faulted) HaltOnFault();
            }
            Debug.Log($"RACE-CLIENT the server's run ended: {reason} at tick {tick} (corrected {corrected}).");
        }

        // The server sends this racer's own end before any standings that hold it, on the same ordered queue. So an end
        // within the target ticks that the predictor did not take, or took differently, is a bug and throws (Netcode logs it).
        // A broken predictor is not checked: its end may never have been taken.
        private void OnStandings(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte[] payload);
            RaceStandings.Entry[] entries = RaceWire.DecodeStandings(payload, out int racer);
            if (you >= 0 && (racer != you || entries.Length != standings.Length))
                throw new InvalidDataException($"RaceClient: standings for racer {racer} of {entries.Length}, but earlier ones were for racer {you} of {standings.Length}.");
            RaceStandings.Entry own = entries[racer];
            bool ended = own.Status == RaceStandings.Status.Finished || own.Status == RaceStandings.Status.Failed;
            if (ended && own.EndTick <= targetTicks && !predictor.Faulted)
            {
                RaceWire.EndReason reason = own.Status == RaceStandings.Status.Finished ? RaceWire.EndReason.Finished : RaceWire.EndReason.Failed;
                if (!predictor.ServerEnded || predictor.EndTick != own.EndTick || predictor.EndReason != reason)
                    throw new InvalidDataException($"RaceClient: the standings say racer {racer} ended {reason} at tick {own.EndTick}, but the end this client took was {(predictor.ServerEnded ? $"{predictor.EndReason} at tick {predictor.EndTick}" : "none")}.");
            }
            // The remote positions carry no racer number: the ghost is "the other one", which only means something with two.
            if (Ghost != null && entries.Length != 2)
                throw new InvalidOperationException($"RaceClient: the ghost shows the one other racer, but the standings have {entries.Length} racers.");
            standings = entries;
            you = racer;
            standingsReceived++;
            Debug.Log($"RACE-CLIENT-STANDINGS you={racer} {RaceStandings.Describe(entries)}");
            if (Ghost != null && Ghost.Visible)
            {
                int other = 1 - racer;
                RaceStandings.Status status = entries[other].Status;
                if (status == RaceStandings.Status.Finished || status == RaceStandings.Status.Failed)
                {
                    Ghost.Hide();
                    Debug.Log($"RACE-CLIENT-GHOST hidden: racer {other} {RaceStandings.Describe(entries[other])}");
                }
            }
        }

        // The server sends the standings of a race before any order (a racer's start is in the standings first), so an order
        // for a different number of racers than the standings or the order before it is a bug and throws (Netcode logs it).
        private void OnOrder(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte[] payload);
            int[] places = RaceWire.DecodeOrder(payload, out int tick);
            int expected = standings != null ? standings.Length : order != null ? order.Length : places.Length;
            if (places.Length != expected)
                throw new InvalidDataException($"RaceClient: an order for {places.Length} racers, but the race has {expected}.");
            order = places;
            orderTick = tick;
            orderReceived++;
            Debug.Log($"RACE-CLIENT-ORDER tick={tick} {RaceOrder.Describe(places)}");
        }

        private void OnRemote(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte[] payload);
            Vector3 position = RaceWire.DecodeRemote(payload, out int tick);
            remoteSamples++;
            remote.Add(tick, position);
            RemoteReceived?.Invoke(tick, position);
        }

        private void Update()
        {
            if (delayed != null) delayed.Pump(Time.realtimeSinceStartupAsDouble);
            if (predictor != null && predictor.Faulted)
            {
                HaltOnFault();
                return;
            }
            if (Finished && !reported)
            {
                reported = true;
                ClientPredictor p = predictor;
                // Always present, so a log without it is from a build that predates race.end.
                string ended = p.ServerEnded ? $"{p.EndReason}@{p.EndTick}" : "none";
                Debug.Log($"RACE-CLIENT-DONE compared={p.StatesCompared} corrections={p.Corrections} firstMismatch={p.FirstMismatchTick} replayed={p.ReplayedTicks} longest={p.LongestReplay} ms={p.ReplayMilliseconds:F3} jolt={p.LargestJolt:F4} remote={remoteSamples} ended={ended} catchUps={p.CatchUps} caughtUp={p.CaughtUpTicks}");
                if (quitWhenDone) StartCoroutine(QuitSoon());
            }
            else if (started && quitWhenDone && timeoutSeconds > 0f && !Finished && !gaveUp && Time.realtimeSinceStartup - connectedAt > timeoutSeconds)
            {
                gaveUp = true;
                ClientPredictor p = predictor;
                Debug.LogError($"RACE-CLIENT-TIMEOUT neither the state for tick {targetTicks} nor the server's end arrived after {timeoutSeconds:F0} s (last compared tick {p.LastComparedTick}, corrections {p.Corrections}, remote {remoteSamples}).", this);
                StartCoroutine(QuitSoon());
            }
        }

        // The predictor broke (a state or the server's end could not be taken, or the run was restarted). No tick may run on it
        // again: each would throw, and an input for a tick the server already has would be sent a second time. So the run is
        // held paused, the fault is told once, and with quitWhenDone the process ends as on a timeout. Safe to call every frame.
        private void HaltOnFault()
        {
            if (game.IsRunning) game.TogglePause();
            if (faultReported) return;
            faultReported = true;
            Debug.LogError($"RACE-CLIENT-FAULT {predictor.FaultReason}", this);
            if (quitWhenDone) StartCoroutine(QuitSoon());
        }

        // A moment for the last messages to leave before the process ends.
        private System.Collections.IEnumerator QuitSoon()
        {
            yield return new WaitForSecondsRealtime(1f);
            Disconnect();
            yield return null;
            Application.Quit();
        }

        private void FixedUpdate()
        {
            if (!started) return;
            // A paused run can be resumed from the keyboard (ChainRushGame.Update), so a broken one is held every tick.
            if (predictor.Faulted)
            {
                HaltOnFault();
                return;
            }
            // After the last tick stop the game; a correction near the end puts the run back to going, so check every tick.
            if (predictor.LastInputTick >= targetTicks && game.IsRunning)
            {
                game.TogglePause();
                if (!paused) Debug.Log($"RACE-CLIENT reached tick {targetTicks}.");
                paused = true;
            }
        }

        private void OnDestroy()
        {
            if (game != null)
            {
                game.CountdownFinished -= OnCountdownFinished;
                game.ManualStart = true;
            }
            predictor?.Detach();
            RaceNetwork.Destroy(manager);
        }
    }
}
