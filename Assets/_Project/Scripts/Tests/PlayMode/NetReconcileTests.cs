using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Track;
using ProtoHarness.Net;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    // P3 spike, the parts that need no network (DESIGN.md 7-5): the snapshot as bytes, the client's prediction and
    // correction, the server's input buffer and the delay queue. One simulation plays all the roles in turn, by
    // hand (Time.timeScale = 0, ChainRushGame.StepTick), so the server's run, the truth and the client's run can be
    // held against each other tick by tick, bit for bit.
    [Category("Net")]
    public sealed class NetReconcileTests
    {
        private const string CircuitScene = "Assets/_Project/Scenes/ChainRushCircuit.unity";
        private const int BotTicks = 1700;

        private ChainRushGame game;
        private RunnerMotor player;
        private GrappleController grapple;
        private CircuitRace circuit;
        private float timeScale;
        // The predictor and buffer of the last run: they listen to the game, so they are let go before the next run and at the end.
        private ClientPredictor activePredictor;
        private ServerInputBuffer activeBuffer;

        // Plays a log, but gives tick `dropTick` the previous tick's input: what the server does when an input is late.
        private sealed class DroppedInputReplay : IInputSource
        {
            private readonly InputLog log;
            private readonly int dropTick;
            private int position;
            private TickInput last;

            public DroppedInputReplay(InputLog log, int dropTick)
            {
                this.log = log;
                this.dropTick = dropTick;
            }

            public void Poll() { }
            public void Clear() { position = 0; last = default; }

            public TickInput Consume()
            {
                int tick = position + 1;
                TickInput input = position < log.Count ? log[position] : default;
                position++;
                if (tick == dropTick) return last;
                last = input;
                return input;
            }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            timeScale = Time.timeScale;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // The game is still running when a test ends; give it an idle source so FixedUpdate does not keep asking a
            // finished test's predictor or buffer for controls.
            activePredictor?.Detach();
            activeBuffer?.Detach();
            activePredictor = null;
            activeBuffer = null;
            if (game != null) game.SetInputSource(new InputReplay(new InputLog()));
            Time.timeScale = timeScale;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // ---- the snapshot as bytes ---------------------------------------------------------------

        [UnityTest, Timeout(120000)]
        public IEnumerator Codec_SnapshotsFromARealRun_RoundTripToTheSameBytes()
        {
            yield return Load();
            InputLog log = RecordBot(BotTicks);
            byte[][] states = RunTruth(log, BotTicks);
            int checkedStates = 0;
            for (int t = 0; t <= BotTicks; t += 50)
            {
                SimSnapshot decoded = SimSnapshotCodec.Decode(states[t]);
                Assert.That(decoded.Tick, Is.EqualTo(t));
                Assert.That(SimSnapshotCodec.TickOf(states[t]), Is.EqualTo(t));
                Assert.That(SimSnapshotCodec.SameState(SimSnapshotCodec.Encode(decoded), states[t]), Is.True, $"tick {t} does not round trip.");
                checkedStates++;
            }
            // A decoded snapshot puts the game back where the original did: running on gives the same next tick.
            game.RestoreSnapshot(SimSnapshotCodec.Decode(states[400]));
            game.StepTick(new InputReplay(SingleInput(log, 400)));
            Assert.That(SimSnapshotCodec.SameState(SimSnapshotCodec.Encode(game.CaptureSnapshot()), states[401]), Is.True, "Restoring a decoded snapshot and stepping must give the original tick 401.");
            Debug.Log($"Net codec: {checkedStates} snapshots of a {BotTicks}-tick run round trip; {states[400].Length} bytes each at tick 400.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Codec_EveryFieldOfTheSnapshotChangesTheBytes()
        {
            yield return Load();
            InputLog log = RecordBot(BotTicks);
            SimSnapshot baseline = SimSnapshotCodec.Decode(RunTruth(log, BotTicks)[BotTicks]);
            byte[] reference = SimSnapshotCodec.Encode(baseline);
            int fields = 0;

            foreach (FieldInfo field in typeof(RacerState.Snapshot).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object boxed = baseline.Racer;
                field.SetValue(boxed, Mutate(field.GetValue(boxed)));
                var changed = new SimSnapshot(baseline.Tick, (RacerState.Snapshot)boxed, baseline.Position, baseline.Circuit);
                Assert.That(SimSnapshotCodec.SameState(SimSnapshotCodec.Encode(changed), reference), Is.False, $"RacerState.Snapshot.{field.Name} is not in the encoding.");
                fields++;
            }
            foreach (FieldInfo field in typeof(LapCounter.Snapshot).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object boxed = baseline.Circuit.Laps;
                field.SetValue(boxed, Mutate(field.GetValue(boxed)));
                var circuitState = new CircuitRace.Snapshot { Laps = (LapCounter.Snapshot)boxed, LastS = baseline.Circuit.LastS };
                var changed = new SimSnapshot(baseline.Tick, baseline.Racer, baseline.Position, circuitState);
                Assert.That(SimSnapshotCodec.SameState(SimSnapshotCodec.Encode(changed), reference), Is.False, $"LapCounter.Snapshot.{field.Name} is not in the encoding.");
                fields++;
            }
            var lastS = new CircuitRace.Snapshot { Laps = baseline.Circuit.Laps, LastS = baseline.Circuit.LastS + 1d };
            Assert.That(SimSnapshotCodec.SameState(SimSnapshotCodec.Encode(new SimSnapshot(baseline.Tick, baseline.Racer, baseline.Position, lastS)), reference), Is.False, "CircuitRace.Snapshot.LastS is not in the encoding.");
            Assert.That(SimSnapshotCodec.SameState(SimSnapshotCodec.Encode(new SimSnapshot(baseline.Tick + 1, baseline.Racer, baseline.Position, baseline.Circuit)), reference), Is.False, "Tick is not in the encoding.");
            Assert.That(SimSnapshotCodec.SameState(SimSnapshotCodec.Encode(new SimSnapshot(baseline.Tick, baseline.Racer, baseline.Position + new Vector3(0f, 1e-3f, 0f), baseline.Circuit)), reference), Is.False, "Position is not in the encoding.");
            Debug.Log($"Net codec: all {fields + 3} snapshot fields change the bytes.");
        }

        [Test]
        public void Codec_DefaultSnapshotWithNullArrays_RoundTripsAndBadBytesThrow()
        {
            SimSnapshot empty = default;
            byte[] bytes = SimSnapshotCodec.Encode(empty);
            SimSnapshot back = SimSnapshotCodec.Decode(bytes);
            Assert.That(back.Circuit.Laps.LapEndTicks, Is.Null);
            Assert.That(SimSnapshotCodec.SameState(SimSnapshotCodec.Encode(back), bytes), Is.True);
            var shortBytes = new byte[bytes.Length - 1];
            Array.Copy(bytes, shortBytes, shortBytes.Length);
            Assert.Throws<EndOfStreamException>(() => SimSnapshotCodec.Decode(shortBytes));
            var longBytes = new byte[bytes.Length + 1];
            Array.Copy(bytes, longBytes, bytes.Length);
            Assert.Throws<InvalidDataException>(() => SimSnapshotCodec.Decode(longBytes));
            Assert.Throws<InvalidDataException>(() => SimSnapshotCodec.TickOf(new byte[2]));
        }

        [Test]
        public void Wire_InputsRoundTripAndCorruptOnesThrow()
        {
            var input = new TickInput(-0.375f, true, false, true, true, false);
            byte[] bytes = RaceWire.EncodeInput(1234, input);
            TickInput back = RaceWire.DecodeInput(bytes, out int tick);
            Assert.That(tick, Is.EqualTo(1234));
            Assert.That(back.Steer, Is.EqualTo(-0.375f));
            Assert.That((back.PrimaryPressed, back.ReleasePressed, back.AttackPressed, back.Drift, back.ChainActionPressed), Is.EqualTo((true, false, true, true, false)));
            bytes[bytes.Length - 1] = 0xFF;
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeInput(bytes, out _), "unknown flag bits");
            byte[] nan = RaceWire.EncodeInput(1, default);
            BitConverter.GetBytes(float.NaN).CopyTo(nan, 4);
            Assert.Throws<ArgumentOutOfRangeException>(() => RaceWire.DecodeInput(nan, out _), "NaN steer");
        }

        // A saved input log is the same run: the file holds exactly what the simulation consumed (DESIGN.md P2).
        [UnityTest, Timeout(120000)]
        public IEnumerator InputLogFile_ARecordedRun_ReplaysTheSameTicksFromTheFileBytes()
        {
            yield return Load();
            InputLog log = RecordBot(BotTicks);
            byte[][] truth = RunTruth(log, BotTicks);
            InputLog loaded = InputLogFile.Decode(InputLogFile.Encode(log));
            Assert.That(loaded.Count, Is.EqualTo(log.Count));
            byte[][] fromFile = RunTruth(loaded, BotTicks);
            for (int t = 0; t <= BotTicks; t++)
                Assert.That(SimSnapshotCodec.SameState(truth[t], fromFile[t]), Is.True, $"tick {t} differs after the log went through the file format.");
        }

        // ---- the delay queue -----------------------------------------------------------------------

        [Test]
        public void DelayedSender_HoldsMessagesForTheDelayAndKeepsTheirOrder()
        {
            var got = new List<byte>();
            var sender = new DelayedSender(p => got.Add(p[0]), 0.05d, 0.04d, 7);
            for (byte i = 0; i < 20; i++) sender.Send(new[] { i }, i * 0.02d);
            sender.Pump(0.049d);
            Assert.That(got, Is.Empty, "Nothing is due before the delay has passed.");
            sender.Pump(100d);
            Assert.That(got.Count, Is.EqualTo(20));
            for (int i = 0; i < 20; i++) Assert.That(got[i], Is.EqualTo(i), "The channel is ordered: no message overtakes another.");
            Assert.That(sender.Pending, Is.EqualTo(0));

            // Same seed, same schedule.
            var first = DueTimes(3);
            var second = DueTimes(3);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(DueTimes(4), Is.Not.EqualTo(first), "Another seed must change the jitter.");
            foreach (double due in first) Assert.That(due, Is.GreaterThanOrEqualTo(0.05d));
        }

        [Test]
        public void DelayedSender_RejectsNegativeDelayOrJitter()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DelayedSender(_ => { }, -0.001d, 0d, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DelayedSender(_ => { }, 0d, -0.001d, 1));
        }

        // ---- the server's buffer ----------------------------------------------------------------------

        [UnityTest, Timeout(120000)]
        public IEnumerator ServerBuffer_ALateInput_RepeatsTheLastOneAndIsCounted()
        {
            yield return Load();
            InputLog log = RecordBot(BotTicks);
            int dropTick = BiggestSteerChange(log, 700, 800);
            byte[][] manual = RunWith(new DroppedInputReplay(log, dropTick), BotTicks);
            byte[][] viaBuffer = RunServer(log, BotTicks, dropTick, out ServerInputBuffer buffer);
            Assert.That(buffer.Missed, Is.EqualTo(1));
            Assert.That(buffer.Late, Is.EqualTo(1), "The dropped input arrives after its tick and is thrown away.");
            Assert.That(buffer.Buffered, Is.EqualTo(0));
            for (int t = 0; t <= BotTicks; t++)
                Assert.That(SimSnapshotCodec.SameState(manual[t], viaBuffer[t]), Is.True, $"The buffer's state after tick {t} differs from repeating the last input by hand.");
            byte[][] truth = RunTruth(log, BotTicks);
            Assert.That(FirstDifference(truth, viaBuffer), Is.EqualTo(dropTick), "Dropping the input of tick " + dropTick + " must change the state after that very tick.");
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Receive(0, default));
        }

        // ---- prediction and correction ----------------------------------------------------------------

        // The server missed one input, so its run differs from what the client predicted. The server's state for each tick
        // reaches the client `latency` ticks later. The client must keep the truth until the first different state
        // arrives, then take the server's run, and correct exactly once.
        [UnityTest, Timeout(180000)]
        public IEnumerator Client_WhenTheServerMissedAnInput_CorrectsOnceAndTakesTheServersRun()
        {
            yield return Load();
            InputLog log = RecordBot(BotTicks);
            int dropTick = BiggestSteerChange(log, 700, 800);
            byte[][] truth = RunTruth(log, BotTicks);
            byte[][] server = RunServer(log, BotTicks, dropTick, out _);
            int diverge = FirstDifference(truth, server);
            Assert.That(diverge, Is.EqualTo(dropTick));

            foreach (int latency in new[] { 5, 15, 40 })
            {
                byte[][] client = RunClient(log, BotTicks, server, latency, out ClientPredictor predictor);
                int arrival = diverge + latency;
                Assert.That(predictor.Corrections, Is.EqualTo(1), $"latency {latency}: one missed input is one correction.");
                Assert.That(predictor.FirstMismatchTick, Is.EqualTo(diverge));
                Assert.That(predictor.ReplayedTicks, Is.EqualTo(latency), $"latency {latency}: the correction replays the ticks since the server's state.");
                Assert.That(predictor.StatesCompared, Is.EqualTo(BotTicks - latency + 1));
                for (int t = 0; t < arrival; t++)
                    Assert.That(SimSnapshotCodec.SameState(client[t], truth[t]), Is.True, $"latency {latency}: before the correction the client predicts the truth (tick {t}).");
                for (int t = arrival; t <= BotTicks; t++)
                    Assert.That(SimSnapshotCodec.SameState(client[t], server[t]), Is.True, $"latency {latency}: after the correction the client follows the server's run (tick {t}).");
                Debug.Log($"Net client: latency {latency} ticks ({latency * 20} ms): diverged at {diverge}, corrected at tick {arrival}, replayed {predictor.ReplayedTicks} ticks in {predictor.ReplayMilliseconds:F3} ms.");
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Client_WhenTheServerAgrees_NeverCorrects()
        {
            yield return Load();
            InputLog log = RecordBot(BotTicks);
            byte[][] truth = RunTruth(log, BotTicks);
            byte[][] client = RunClient(log, BotTicks, truth, 15, out ClientPredictor predictor);
            Assert.That(predictor.Corrections, Is.EqualTo(0));
            Assert.That(predictor.StatesCompared, Is.EqualTo(BotTicks - 15 + 1));
            Assert.That(predictor.ReplayedTicks, Is.EqualTo(0));
            for (int t = 0; t <= BotTicks; t++) Assert.That(SimSnapshotCodec.SameState(client[t], truth[t]), Is.True, $"tick {t}");
        }

        // The control: without the corrections the client stays on its prediction and ends up somewhere else.
        [UnityTest, Timeout(180000)]
        public IEnumerator Client_WhenTheServersStatesAreIgnored_StaysOnItsOwnWrongRun()
        {
            yield return Load();
            InputLog log = RecordBot(BotTicks);
            int dropTick = BiggestSteerChange(log, 700, 800);
            byte[][] truth = RunTruth(log, BotTicks);
            byte[][] server = RunServer(log, BotTicks, dropTick, out _);
            // The server's states are delivered up to the missed input and not after, and the run stops well inside the
            // predictor's window (a longer silence is refused loudly).
            int ticks = dropTick + 400;
            byte[][] client = RunClient(log, ticks, server, 15, out ClientPredictor predictor, dropTick);
            Assert.That(predictor.Corrections, Is.EqualTo(0));
            for (int t = 0; t <= ticks; t++) Assert.That(SimSnapshotCodec.SameState(client[t], truth[t]), Is.True, $"tick {t}");
            Assert.That(FirstDifference(client, server), Is.EqualTo(dropTick), "Without corrections the client disagrees with the server from the missed input on.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Client_WithAServerStateFromTheFuture_ThrowsLoudly()
        {
            yield return Load();
            InputLog log = RecordBot(BotTicks);
            byte[][] truth = RunTruth(log, BotTicks);
            var predictor = new ClientPredictor(game, new InputReplay(log));
            activePredictor = predictor;
            Time.timeScale = 0f;
            game.SetInputSource(predictor);
            game.StartRun();
            predictor.AfterTick();
            for (int i = 0; i < 10; i++)
            {
                game.StepTick(predictor);
            }
            Assert.Throws<InvalidOperationException>(() => predictor.OnServerState(truth[11]));
            Assert.That(predictor.OnServerState(truth[10]), Is.False, "A state for the present tick is compared with the live one.");
            Assert.Throws<InvalidOperationException>(() => predictor.OnServerState(truth[9]), "A state older than the newest compared one is a bug.");
        }

        // ---- helpers -------------------------------------------------------------------------------

        private IEnumerator Load()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(CircuitScene, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("Spike tests require the Unity Editor.");
            yield break;
#endif
            game = null;
            player = null;
            circuit = null;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame g)) game = g;
                if (root.TryGetComponent(out RunnerMotor p)) { player = p; grapple = p.GetComponent<GrappleController>(); }
                if (root.TryGetComponent(out CircuitRace c)) circuit = c;
            }
            Assert.That(game, Is.Not.Null);
            yield return null;
        }

        private InputLog RecordBot(int ticks)
        {
            var log = new InputLog();
            var recorder = new InputRecorder(new CircuitBot(game, player, grapple, circuit), log);
            Time.timeScale = 0f;
            game.SetInputSource(recorder);
            game.StartRun();
            while (game.IsRunning && game.Tick < ticks) game.StepTick(recorder);
            Assert.That(game.IsRunning, Is.True, $"The bot's run ended at tick {game.Tick}.");
            Assert.That(log.Count, Is.GreaterThanOrEqualTo(ticks));
            return log;
        }

        // The state after t ticks, for t = 0..ticks, of a run on this source.
        private byte[][] RunWith(IInputSource source, int ticks)
        {
            var states = new byte[ticks + 1][];
            Time.timeScale = 0f;
            game.SetInputSource(source);
            game.StartRun();
            states[0] = SimSnapshotCodec.Encode(game.CaptureSnapshot());
            for (int t = 1; t <= ticks; t++)
            {
                game.StepTick(source);
                Assert.That(game.IsRunning, Is.True, $"The run ended at tick {game.Tick}.");
                states[t] = SimSnapshotCodec.Encode(game.CaptureSnapshot());
            }
            return states;
        }

        private byte[][] RunTruth(InputLog log, int ticks) => RunWith(new InputReplay(log), ticks);

        // The server's run: every input but dropTick arrives in time (0 = none dropped), the dropped one afterwards.
        private byte[][] RunServer(InputLog log, int ticks, int dropTick, out ServerInputBuffer buffer)
        {
            activeBuffer?.Detach();
            buffer = new ServerInputBuffer(game);
            activeBuffer = buffer;
            var states = new List<byte[]>(ticks + 1);
            buffer.StateReady += bytes => states.Add(bytes);
            for (int t = 1; t <= ticks; t++) if (t != dropTick) buffer.Receive(t, log[t - 1]);
            Time.timeScale = 0f;
            game.SetInputSource(buffer);
            game.StartRun();
            buffer.AfterTick();
            for (int t = 1; t <= ticks; t++)
            {
                game.StepTick(buffer);
            }
            if (dropTick > 0) buffer.Receive(dropTick, log[dropTick - 1]);
            Assert.That(states.Count, Is.EqualTo(ticks + 1));
            return states.ToArray();
        }

        // The client's run with the server's states arriving `latency` ticks late (null: never). Returns the client's
        // state after each tick as the client held it at the end of that tick.
        private byte[][] RunClient(InputLog log, int ticks, byte[][] server, int latency, out ClientPredictor predictor, int deliverBelow = int.MaxValue)
        {
            activePredictor?.Detach();
            predictor = new ClientPredictor(game, new InputReplay(log));
            activePredictor = predictor;
            var states = new byte[ticks + 1][];
            Time.timeScale = 0f;
            game.SetInputSource(predictor);
            game.StartRun();
            predictor.AfterTick();
            states[0] = SimSnapshotCodec.Encode(game.CaptureSnapshot());
            for (int t = 1; t <= ticks; t++)
            {
                game.StepTick(predictor);
                Assert.That(game.IsRunning, Is.True, $"The run ended at tick {game.Tick}.");
                if (server != null && t - latency >= 0 && t - latency < deliverBelow) predictor.OnServerState(server[t - latency]);
                states[t] = SimSnapshotCodec.Encode(game.CaptureSnapshot());
            }
            return states;
        }

        private static InputLog SingleInput(InputLog log, int index)
        {
            var one = new InputLog();
            one.Add(log[index]);
            return one;
        }

        // The tick in [from, to] whose steer differs most from the tick before: dropping that input changes the state at once,
        // and it only nudges the heading, so the rest of the bot's run still works (a dropped jump would fall into the gap).
        private static int BiggestSteerChange(InputLog log, int from, int to)
        {
            int best = -1;
            float bestChange = 0f;
            for (int t = from; t <= to; t++)
            {
                float change = Mathf.Abs(log[t - 1].Steer - log[t - 2].Steer);
                if (change > bestChange) { bestChange = change; best = t; }
            }
            var seen = new System.Text.StringBuilder();
            for (int t = 2; t <= log.Count && seen.Length < 600; t++)
                if (log[t - 1].Steer != log[t - 2].Steer) seen.Append($" {t}:{log[t - 2].Steer:F3}>{log[t - 1].Steer:F3}");
            Assert.That(best, Is.GreaterThan(0), $"The bot never changes its steer between ticks {from} and {to}. Steer changes in the log:{seen}");
            return best;
        }

        private static int FirstDifference(byte[][] a, byte[][] b)
        {
            for (int t = 0; t < a.Length; t++) if (!SimSnapshotCodec.SameState(a[t], b[t])) return t;
            return -1;
        }

        private static object Mutate(object value)
        {
            switch (value)
            {
                case float f: return f + 1f;
                case double d: return d + 1d;
                case int i: return i + 1;
                case bool b: return !b;
                case Vector3 v: return v + new Vector3(1f, 0f, 0f);
                case int[] a: return new int[(a?.Length ?? 0) + 1];
                case null: return new int[1];
                default: throw new NotSupportedException("Mutate: no rule for " + value.GetType());
            }
        }

        private static List<double> DueTimes(int seed)
        {
            var dues = new List<double>();
            double clock = 0d;
            var sender = new DelayedSender(_ => dues.Add(clock), 0.05d, 0.04d, seed);
            for (int i = 0; i < 20; i++) sender.Send(new byte[1], 0d);
            for (clock = 0d; clock < 1d; clock += 0.001d) sender.Pump(clock);
            return dues;
        }
    }
}
