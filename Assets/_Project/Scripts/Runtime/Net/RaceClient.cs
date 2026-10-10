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
    // Finished.
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
        // True once the server's state for the last tick has been compared.
        public bool Finished => predictor != null && predictor.LastComparedTick >= targetTicks;

        // Raised for every position of another racer that arrives, with its tick (before it is drawn).
        public event Action<int, Vector3> RemoteReceived;

        // timeoutSeconds: with quitWhenDone, how long after connecting the client waits for the server's last state before it
        // gives up and ends the process (0 = forever). The server does not tell a client that its run has ended, so a run the
        // server ended early (a fall after a missed input) would otherwise leave the client waiting for good.
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
            if (Finished && !reported)
            {
                reported = true;
                ClientPredictor p = predictor;
                Debug.Log($"RACE-CLIENT-DONE compared={p.StatesCompared} corrections={p.Corrections} firstMismatch={p.FirstMismatchTick} replayed={p.ReplayedTicks} longest={p.LongestReplay} ms={p.ReplayMilliseconds:F3} jolt={p.LargestJolt:F4} remote={remoteSamples}");
                if (quitWhenDone) StartCoroutine(QuitSoon());
            }
            else if (started && quitWhenDone && timeoutSeconds > 0f && !Finished && !gaveUp && Time.realtimeSinceStartup - connectedAt > timeoutSeconds)
            {
                gaveUp = true;
                ClientPredictor p = predictor;
                Debug.LogError($"RACE-CLIENT-TIMEOUT no state for tick {targetTicks} after {timeoutSeconds:F0} s (last compared tick {p.LastComparedTick}, corrections {p.Corrections}, remote {remoteSamples}); the server's run may have ended.", this);
                StartCoroutine(QuitSoon());
            }
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
