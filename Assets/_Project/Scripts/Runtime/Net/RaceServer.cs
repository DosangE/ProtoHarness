using System;
using System.Collections;
using System.Collections.Generic;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Race;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProtoHarness.Net
{
    // The authoritative side: one racer per client, each in a world of its own (RaceWorld, DESIGN.md P3 C1). A client's
    // inputs go to its racer's ServerInputBuffer; the racer's run starts once enough inputs are buffered. After every
    // tick its state goes back to its owner (to reconcile with) and its position to every other client (to draw it),
    // both through a DelayedSender. Racers do not touch each other, so each world simply runs on its own.
    public sealed class RaceServer : MonoBehaviour
    {
        private sealed class Slot
        {
            public RaceWorld World;
            public ServerInputBuffer Buffer;
            public DelayedSender ToOwner;
            public DelayedSender[] ToOthers;
            public ulong ClientId;
            public bool HasClient;
            public bool Running;
            public bool EndReported;
            // Messages for this racer's client that had nowhere to go (it was not connected, or had left).
            public int Dropped;
        }

        private Slot[] slots;
        private ushort port;
        private int bufferTicks;
        private double delaySeconds;
        private double jitterSeconds;
        private int seed;
        private float connectTimeoutSeconds;
        private NetworkManager manager;
        private bool began;
        private bool anyClientEver;
        private int connectedClients;
        private float startedAt;

        public int SlotCount => slots == null ? 0 : slots.Length;
        public ServerInputBuffer BufferOf(int slot) => slots[slot].Buffer;
        public bool IsRunning(int slot) => slots[slot].Running;

        public void Configure(IReadOnlyList<RaceWorld> worlds, ushort port, int bufferTicks, double delaySeconds, double jitterSeconds, int seed, float connectTimeoutSeconds)
        {
            if (worlds == null || worlds.Count == 0) throw new ArgumentException("The server needs at least one world.", nameof(worlds));
            if (bufferTicks < 1) throw new ArgumentOutOfRangeException(nameof(bufferTicks), bufferTicks, "The server needs at least one buffered input.");
            slots = new Slot[worlds.Count];
            for (int i = 0; i < slots.Length; i++)
            {
                if (worlds[i] == null) throw new ArgumentException($"World {i} is missing.", nameof(worlds));
                slots[i] = new Slot { World = worlds[i] };
            }
            this.port = port;
            this.bufferTicks = bufferTicks;
            this.delaySeconds = delaySeconds;
            this.jitterSeconds = jitterSeconds;
            this.seed = seed;
            this.connectTimeoutSeconds = connectTimeoutSeconds;
        }

        // For a player started on one circuit scene: loads the other racers' worlds as isolated copies of it, then begins.
        // A failure here is logged and ends the process, so a half-set-up server never sits waiting.
        public void Launch(int racers, ushort port, int bufferTicks, double delaySeconds, double jitterSeconds, int seed, float connectTimeoutSeconds)
        {
            if (racers < 1) throw new ArgumentOutOfRangeException(nameof(racers), racers, "At least one racer.");
            StartCoroutine(LaunchRoutine(racers, port, bufferTicks, delaySeconds, jitterSeconds, seed, connectTimeoutSeconds));
        }

        private IEnumerator LaunchRoutine(int racers, ushort port, int bufferTicks, double delaySeconds, double jitterSeconds, int seed, float connectTimeoutSeconds)
        {
            Scene firstScene = SceneManager.GetActiveScene();
            var worlds = new List<RaceWorld>(racers);
            bool ok = TryAdd(worlds, firstScene);
            // Each copy brings its own camera and AudioListener, and Unity logs every frame that there are two. A server has no use for either.
            if (ok) SwitchOffViews(firstScene);
            for (int i = 1; i < racers && ok; i++)
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(firstScene.name, RaceWorld.IsolatedLoad);
                if (load == null)
                {
                    Debug.LogError($"RaceServer: could not load a copy of scene '{firstScene.name}' (is it in the build?).", this);
                    ok = false;
                    break;
                }
                yield return load;
                Scene copy = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
                ok = TryAdd(worlds, copy);
                if (ok) SwitchOffViews(copy);
            }
            if (ok)
            {
                try
                {
                    Configure(worlds, port, bufferTicks, delaySeconds, jitterSeconds, seed, connectTimeoutSeconds);
                    Begin();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                    ok = false;
                }
            }
            if (!ok) Application.Quit(1);
        }

        private bool TryAdd(List<RaceWorld> worlds, Scene scene)
        {
            try
            {
                worlds.Add(RaceWorld.FromScene(scene));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                return false;
            }
        }

        private static void SwitchOffViews(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            }
        }

        public void Begin()
        {
            if (slots == null) throw new InvalidOperationException("RaceServer: call Configure before Begin.");
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                Slot slot = slots[i];
                slot.Buffer = new ServerInputBuffer(slot.World.Game);
                slot.ToOwner = new DelayedSender(bytes => SendTo(RaceNetwork.StateMessage, index, bytes), delaySeconds, jitterSeconds, seed + i * 101);
                slot.ToOthers = new DelayedSender[slots.Length];
                for (int j = 0; j < slots.Length; j++)
                {
                    if (j == i) continue;
                    int target = j;
                    slot.ToOthers[j] = new DelayedSender(bytes => SendTo(RaceNetwork.RemoteMessage, target, bytes), delaySeconds, jitterSeconds, seed + i * 101 + j * 7 + 1);
                }
                slot.Buffer.StateReady += bytes => OnState(index, bytes);
            }
            manager = RaceNetwork.Create("127.0.0.1", port);
            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;
            if (!manager.StartServer()) throw new InvalidOperationException($"RaceServer: could not listen on port {port}.");
            manager.CustomMessagingManager.RegisterNamedMessageHandler(RaceNetwork.InputMessage, OnInput);
            startedAt = Time.realtimeSinceStartup;
            began = true;
            Debug.Log($"RACE-SERVER listening on {port}: {slots.Length} racers, buffer {bufferTicks} ticks, delay {delaySeconds * 1000d:F0} ms + jitter {jitterSeconds * 1000d:F0} ms.");
        }

        private int SlotOfClient(ulong id)
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i].HasClient && slots[i].ClientId == id) return i;
            return -1;
        }

        private void OnClientConnected(ulong id)
        {
            if (SlotOfClient(id) >= 0) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].HasClient) continue;
                slots[i].ClientId = id;
                slots[i].HasClient = true;
                anyClientEver = true;
                connectedClients++;
                Debug.Log($"RACE-SERVER client {id} is racer {i}.");
                return;
            }
            Debug.LogError($"RaceServer: client {id} connected but all {slots.Length} racers have a client; sending it away.", this);
            manager.DisconnectClient(id);
        }

        private void OnClientDisconnected(ulong id)
        {
            int slot = SlotOfClient(id);
            if (slot < 0) return;
            slots[slot].HasClient = false;
            connectedClients--;
            Debug.Log($"RACE-SERVER client {id} (racer {slot}) disconnected.");
            Report(slot);
            if (connectedClients == 0) Application.Quit();
        }

        private void OnInput(ulong sender, FastBufferReader reader)
        {
            int slot = SlotOfClient(sender);
            if (slot < 0)
            {
                Debug.LogError($"RaceServer: input from client {sender}, who has no racer.", this);
                return;
            }
            reader.ReadValueSafe(out byte[] payload);
            TickInput input = RaceWire.DecodeInput(payload, out int tick);
            slots[slot].Buffer.Receive(tick, input);
        }

        // A racer's state after a tick: to its owner whole, to the others as a position.
        private void OnState(int index, byte[] bytes)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            Slot slot = slots[index];
            slot.ToOwner.Send(bytes, now);
            SimSnapshot snapshot = SimSnapshotCodec.Decode(bytes);
            byte[] remote = RaceWire.EncodeRemote(snapshot.Tick, snapshot.Position);
            for (int j = 0; j < slots.Length; j++) if (j != index) slot.ToOthers[j].Send(remote, now);
        }

        private void SendTo(string name, int slotIndex, byte[] bytes)
        {
            Slot target = slots[slotIndex];
            if (!target.HasClient)
            {
                // Nobody to send to (not connected yet, or gone): the message is dropped, and counted for the report.
                target.Dropped++;
                return;
            }
            using var writer = new FastBufferWriter(bytes.Length + 8, Allocator.Temp);
            writer.WriteValueSafe(bytes);
            manager.CustomMessagingManager.SendNamedMessage(name, target.ClientId, writer);
        }

        private void Update()
        {
            if (!began) return;
            double now = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                slot.ToOwner.Pump(now);
                for (int j = 0; j < slots.Length; j++) if (j != i) slot.ToOthers[j].Pump(now);
            }
            if (!anyClientEver && connectTimeoutSeconds > 0f && Time.realtimeSinceStartup - startedAt > connectTimeoutSeconds)
            {
                Debug.LogError($"RaceServer: no client within {connectTimeoutSeconds:F0} s; quitting.", this);
                Application.Quit(2);
                return;
            }
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                if (!slot.Running && slot.HasClient && slot.Buffer.HasBuffered(bufferTicks))
                {
                    ChainRushGame game = slot.World.Game;
                    game.SetInputSource(slot.Buffer);
                    game.StartRun();
                    slot.Buffer.AfterTick();
                    slot.Running = true;
                    Debug.Log($"RACE-SERVER racer {i} started with {slot.Buffer.Buffered} inputs buffered.");
                }
                else if (slot.Running && !slot.EndReported && !slot.World.Game.IsRunning)
                {
                    slot.EndReported = true;
                    ChainRushGame game = slot.World.Game;
                    Debug.Log($"RACE-SERVER racer {i}: the run is no longer going at tick {game.Tick} (failed {game.HasFailed}, finished {game.HasFinished}).");
                }
            }
        }

        private void Report(int i)
        {
            Slot slot = slots[i];
            if (slot.Buffer == null) return;
            Debug.Log($"RACE-SERVER-DONE racer={i} ticks={slot.World.Game.Tick} received={slot.Buffer.Received} missed={slot.Buffer.Missed} late={slot.Buffer.Late} buffered={slot.Buffer.Buffered} dropped={slot.Dropped}");
        }

        private void OnDestroy()
        {
            if (slots != null)
                for (int i = 0; i < slots.Length; i++) slots[i].Buffer?.Detach();
            RaceNetwork.Destroy(manager);
        }
    }
}
