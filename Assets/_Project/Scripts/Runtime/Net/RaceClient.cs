using System;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ProtoHarness.Net
{
    // The predicting side: runs the game at once on its own controls, sends every tick's controls to the server through a
    // DelayedSender, and hands the server's states for this racer to ClientPredictor, which corrects the game when they
    // disagree (DESIGN.md P3). The other racers' positions go to a RemoteInterpolator and are drawn by a RemoteGhost. After
    // `targetTicks` ticks it stops sending and, once the server's state for the last of them has been compared, it is
    // Finished. It is Finished too once the server has said that this racer's run ended within those ticks (race.end).
    // An end the predictor cannot take (any exception from OnServerEnd) leaves it Faulted: the run is then stopped for good
    // (RACE-CLIENT-FAULT), not Finished.
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

        public ClientPredictor Predictor => predictor;
        public RemoteInterpolator Remote => remote;
        // The picture of the other racer, or null when the client was made without one.
        public RemoteGhost Ghost { get; private set; }
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
            predictor = new ClientPredictor(game, live);
            delayed = new DelayedSender(SendInput, delaySeconds, jitterSeconds, seed);
            predictor.InputConsumed += OnInputConsumed;
            remote = new RemoteInterpolator();
            if (showRemote)
            {
                Ghost = gameObject.AddComponent<RemoteGhost>();
                Ghost.Initialize(remote);
            }
            manager = RaceNetwork.Create(address, port);
            manager.OnClientConnectedCallback += OnConnected;
            if (!manager.StartClient()) throw new InvalidOperationException($"RaceClient: could not start a client for {address}:{port}.");
            manager.CustomMessagingManager.RegisterNamedMessageHandler(RaceNetwork.StateMessage, OnState);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(RaceNetwork.EndMessage, OnEnd);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(RaceNetwork.RemoteMessage, OnRemote);
        }

        // Closes the connection (the server notices when its client leaves).
        public void Disconnect()
        {
            if (manager != null && manager.IsListening) manager.Shutdown();
        }

        private void OnConnected(ulong id)
        {
            if (started) return;
            started = true;
            connectedAt = Time.realtimeSinceStartup;
            game.SetInputSource(predictor);
            game.StartRun();
            predictor.AfterTick();
            Debug.Log($"RACE-CLIENT connected as {id}; run started.");
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
            predictor.OnServerState(payload);
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
                Debug.Log($"RACE-CLIENT-DONE compared={p.StatesCompared} corrections={p.Corrections} firstMismatch={p.FirstMismatchTick} replayed={p.ReplayedTicks} longest={p.LongestReplay} ms={p.ReplayMilliseconds:F3} jolt={p.LargestJolt:F4} remote={remoteSamples} ended={ended}");
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

        // The predictor broke (the server's end could not be taken). No tick may run on it again: each would throw, and an input
        // for a tick the server already has would be sent a second time. So the run is held paused, the fault is told once, and
        // with quitWhenDone the process ends as on a timeout. Safe to call every frame.
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
            predictor?.Detach();
            RaceNetwork.Destroy(manager);
        }
    }
}
