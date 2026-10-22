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
        // More than the bot's three laps take (158.46 s = 7923 ticks, DECISIONS 2026-10-08 "코스 T4").
        private const int RaceTicksLimit = 12000;

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

        // Plays a log by the game's own tick, so it can go on from a restored snapshot. Tick `dropTick` gets the tick before's
        // input, as DroppedInputReplay and the server's buffer give it.
        private sealed class TickReplay : IInputSource
        {
            private readonly ChainRushGame game;
            private readonly InputLog log;
            private readonly int dropTick;

            public TickReplay(ChainRushGame game, InputLog log, int dropTick)
            {
                if (dropTick < 2) throw new ArgumentOutOfRangeException(nameof(dropTick), dropTick, "The dropped tick needs a tick before it.");
                this.game = game;
                this.log = log;
                this.dropTick = dropTick;
            }

            public void Poll() { }
            public void Clear() { }

            public TickInput Consume()
            {
                int tick = game.Tick;
                if (tick < 1 || tick > log.Count) throw new InvalidOperationException($"TickReplay: no input for tick {tick} in a log of {log.Count}.");
                return tick == dropTick ? log[tick - 2] : log[tick - 1];
            }
        }

        // Plays a log by the game's own tick, idle past its end (as InputReplay is), so a variant can go on from a restored
        // snapshot of the recorded run.
        private sealed class LogAtTick : IInputSource
        {
            private readonly ChainRushGame game;
            private readonly InputLog log;

            public LogAtTick(ChainRushGame game, InputLog log)
            {
                this.game = game;
                this.log = log;
            }

            public void Poll() { }
            public void Clear() { }
            public TickInput Consume() => InputFor(game.Tick);

            public TickInput InputFor(int tick)
            {
                if (tick < 1) throw new ArgumentOutOfRangeException(nameof(tick), tick, "Ticks start at 1.");
                return tick <= log.Count ? log[tick - 1] : default;
            }
        }

        // What the server's buffer raised when its run ended.
        private struct ServerEnd
        {
            public int Tick;
            public RaceWire.EndReason Reason;
            public TickInput Input;
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
            Assert.That((back.PrimaryPressed, back.ReleasePressed, back.AttackPressed, back.SwingHeld, back.ChainActionPressed), Is.EqualTo((true, false, true, true, false)));
            // No flag byte is refused any more: since reversing (2026-10-22) all eight bits are known.
            byte[] nan = RaceWire.EncodeInput(1, default);
            BitConverter.GetBytes(float.NaN).CopyTo(nan, 4);
            Assert.Throws<ArgumentOutOfRangeException>(() => RaceWire.DecodeInput(nan, out _), "NaN steer");
        }

        [Test]
        public void Wire_EndsRoundTripAndCorruptOnesThrow()
        {
            var input = new TickInput(0.625f, false, true, false, true, true);
            foreach (RaceWire.EndReason reason in new[] { RaceWire.EndReason.Failed, RaceWire.EndReason.Finished })
            {
                byte[] bytes = RaceWire.EncodeEnd(812, reason, input);
                TickInput back = RaceWire.DecodeEnd(bytes, out int tick, out RaceWire.EndReason backReason);
                Assert.That(tick, Is.EqualTo(812));
                Assert.That(backReason, Is.EqualTo(reason));
                Assert.That(back.Steer, Is.EqualTo(0.625f));
                Assert.That((back.PrimaryPressed, back.ReleasePressed, back.AttackPressed, back.SwingHeld, back.ChainActionPressed), Is.EqualTo((false, true, false, true, true)));
            }
            byte[] good = RaceWire.EncodeEnd(812, RaceWire.EndReason.Failed, input);
            var shortBytes = new byte[good.Length - 1];
            Array.Copy(good, shortBytes, shortBytes.Length);
            Assert.Throws<EndOfStreamException>(() => RaceWire.DecodeEnd(shortBytes, out _, out _), "a short end");
            var longBytes = new byte[good.Length + 1];
            Array.Copy(good, longBytes, good.Length);
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeEnd(longBytes, out _, out _), "a long end");
            // The reason is the byte after the tick.
            foreach (byte unknown in new byte[] { 0, 3, 0xFF })
            {
                var badReason = (byte[])good.Clone();
                badReason[4] = unknown;
                Assert.Throws<InvalidDataException>(() => RaceWire.DecodeEnd(badReason, out _, out _), $"end reason {unknown}");
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => RaceWire.EncodeEnd(1, (RaceWire.EndReason)3, input), "an end reason that does not exist is not sent");
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
            int dropTick = BiggestSteerChange(log, 600, 1000);
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
            int dropTick = BiggestSteerChange(log, 600, 1000);
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
            int dropTick = BiggestSteerChange(log, 600, 1000);
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
            // Each case on its own fresh client: a refused state breaks the prediction for good
            // (Client_AStateThatCannotBeTaken_LeavesThePredictorFaulted).
            Assert.Throws<InvalidOperationException>(() => PredictorAt10(log).OnServerState(truth[11]));
            Assert.That(PredictorAt10(log).OnServerState(truth[10]), Is.False, "A state for the present tick is compared with the live one.");
            ClientPredictor predictor = PredictorAt10(log);
            Assert.That(predictor.OnServerState(truth[10]), Is.False);
            Assert.Throws<InvalidOperationException>(() => predictor.OnServerState(truth[9]), "A state older than the newest compared one is a bug.");
        }

        // ---- the end of the server's run ----------------------------------------------------------------

        // The server missed a press (a jump or a grapple), so its run falls into a gap where the client's run, on the controls
        // the client really typed, goes on. A fall does not end a circuit run: the server's run is put back on the track and
        // held there (RunnerMotor.RespawnOnTrack), and its states correct the client once, after which the client follows
        // the server's run through the fall, the respawn and the hold, bit for bit. With the longer latency the correction's
        // replay runs the respawn itself.
        [UnityTest, Timeout(300000)]
        public IEnumerator Client_WhenTheServersRunFellAfterAMissedInput_CorrectsOnceAndFollowsItsRespawn()
        {
            yield return Load();
            InputLog log = RecordBot(BotTicks);
            byte[][] truth = RunTruth(log, BotTicks);
            int dropTick = FindRespawningDrop(log, truth, out int respawnTick);
            byte[][] server = RunServer(log, BotTicks, dropTick, out _);
            Assert.That(RespawnHoldOf(server[respawnTick - 1]), Is.Zero, "The buffer's run must not be held before it falls.");
            Assert.That(RespawnHoldOf(server[respawnTick]), Is.GreaterThan(0), "The buffer's run must be put back on the tick repeating the input by hand was.");
            int diverge = FirstDifference(truth, server);
            Assert.That(diverge, Is.EqualTo(dropTick), "The missed press must change the server's state on that very tick.");

            foreach (int latency in new[] { 5, respawnTick - dropTick + 5 })
            {
                Assert.That(latency, Is.LessThan(ClientPredictor.Window - 8), "precondition: the client must not outrun its window while it waits.");
                byte[][] client = RunClient(log, BotTicks, server, latency, out ClientPredictor predictor);
                int arrival = diverge + latency;
                Assert.That(predictor.Corrections, Is.EqualTo(1), $"latency {latency}: one missed input is one correction.");
                Assert.That(predictor.FirstMismatchTick, Is.EqualTo(diverge), $"latency {latency}");
                Assert.That(predictor.ReplayedTicks, Is.EqualTo(latency), $"latency {latency}: the correction replays the ticks since the server's state.");
                Assert.That(predictor.Faulted, Is.False, $"latency {latency}");
                for (int t = 0; t < arrival; t++)
                    Assert.That(SimSnapshotCodec.SameState(client[t], truth[t]), Is.True, $"latency {latency}: before the correction the client predicts the truth (tick {t}).");
                for (int t = arrival; t <= BotTicks; t++)
                    Assert.That(SimSnapshotCodec.SameState(client[t], server[t]), Is.True, $"latency {latency}: after the correction the client follows the server's run (tick {t}).");
                Debug.Log($"Net respawn: missed press at tick {dropTick}, server put back on the track at tick {respawnTick}; latency {latency} ticks: client corrected at tick {arrival}, replayed {predictor.ReplayedTicks} ticks.");
            }
        }

        // The server missed the input of the very tick its run ended on, and ran that tick on the tick before's controls,
        // while the client's own controls for it keep the client's run going. Every state the server sent agrees with the
        // prediction (the inputs before were all the same), so it is the end that corrects: the client goes back to the
        // state before that tick, runs it on the controls the end carries, and ends on the same tick for the same reason
        // (OnServerEnd returns true). The end must carry the repeated controls, not the ones the client typed.
        // The finish: in a circuit race a fall puts the runner back on the track and does not end the run
        // (RunnerMotor.RespawnOnTrack), so the finish is the only end. A finish is the lap counter's progress reaching
        // the race length on the tick, so a run that crosses the line by less than one tick's controls can change stays short
        // of it on the client's controls; FindFinishReplayCase looks for such a run (a second slingshot is the lever).
        // NetEndReplay: out of the gate until multiplayer reopens (M7). Since a booster speeds a swing up, the search finds
        // no such run (docs/DECISIONS.md 2026-10-19 "스윙 중 부스터"); it is redesigned then.
        [UnityTest, Category("NetEndReplay"), Timeout(300000)]
        public IEnumerator ClientEnd_WhenTheServerMissedTheInputOfItsLastTick_ReplaysThatTickOnTheServersControlsAndEndsTheSame()
        {
            yield return Load();
            InputLog log = RecordSwingBotToFinish(RaceTicksLimit, ShortRelease);
            InputLog clientLog = FindFinishReplayCase(log, game.Tick, out int endTick, out string found);
            RaceWire.EndReason reason = RaceWire.EndReason.Finished;
            TickInput typed = clientLog[endTick - 1];
            TickInput repeated = clientLog[endTick - 2];
            Assert.That(SameInput(typed, repeated), Is.False, "precondition: the client's controls for the last tick must differ from the tick before's, or the check on the end's controls could not tell them apart.");

            byte[][] server = RunServerToEnd(clientLog, RaceTicksLimit, endTick, out ServerEnd end);
            Assert.That((end.Tick, end.Reason), Is.EqualTo((endTick, reason)), "The buffer's run must end where repeating the input by hand did.");
            Assert.That(end.Input.Steer, Is.EqualTo(repeated.Steer), "The end carries the controls the server ran its last tick on: the tick before's, repeated.");
            Assert.That(TickInputCodec.ToFlags(end.Input), Is.EqualTo(TickInputCodec.ToFlags(repeated)), "The end carries the repeated controls, not the ones the client typed.");
            Assert.That(server.Length, Is.EqualTo(endTick), "One state for every tick before the end.");

            foreach (int latency in new[] { 5, 15, 40 })
            {
                bool corrected = RunClientToEnd(clientLog, server, end, latency, out ClientPredictor predictor);
                Assert.That(corrected, Is.True, $"latency {latency}: the client's run did not end on tick {endTick}, so the end must correct it.");
                Assert.That(predictor.Corrections, Is.EqualTo(1), $"latency {latency}: the states all agreed; the end is the one correction.");
                Assert.That(predictor.FirstMismatchTick, Is.EqualTo(endTick), $"latency {latency}: the first disagreement is the end's tick.");
                Assert.That(predictor.ReplayedTicks, Is.EqualTo(1), $"latency {latency}: the end replays its own tick only.");
                Assert.That(predictor.LongestReplay, Is.EqualTo(1), $"latency {latency}");
                Assert.That(predictor.StatesCompared, Is.EqualTo(endTick), $"latency {latency}: every state the server sent (ticks 0..{endTick - 1}) is compared.");
                Assert.That(predictor.Faulted, Is.False, $"latency {latency}");
                Assert.That(predictor.ServerEnded, Is.True, $"latency {latency}");
                Assert.That((predictor.EndTick, predictor.EndReason), Is.EqualTo((endTick, reason)), $"latency {latency}");
                Assert.That(game.Tick, Is.EqualTo(endTick), $"latency {latency}: the client's run must end on the server's tick.");
                Assert.That(game.IsRunning, Is.False, $"latency {latency}");
                Assert.That(game.HasFinished, Is.True, $"latency {latency}: the client's run must end for the server's reason ({reason}).");
                Debug.Log($"Net end replay: {found}; the server missed tick {endTick} and ended there ({reason}) on the repeated controls (steer {repeated.Steer:F3}, flags {TickInputCodec.ToFlags(repeated)}), where the client's (steer {typed.Steer:F3}, flags {TickInputCodec.ToFlags(typed)}) kept it going; latency {latency} ticks: the end corrected the client to {reason} at tick {game.Tick}.");
            }
        }

        // The server and the client run on the same controls to the end of the race: the client finishes on the server's
        // tick by itself, so the end changes nothing.
        [UnityTest, Timeout(600000)]
        public IEnumerator ClientEnd_WhenTheServerFinishesOnTheSameControls_NeverCorrects()
        {
            yield return Load();
            InputLog log = RecordBotToFinish(RaceTicksLimit);
            int finishTick = game.Tick;
            byte[][] server = RunServerToEnd(log, RaceTicksLimit, 0, out ServerEnd end);
            Assert.That((end.Tick, end.Reason), Is.EqualTo((finishTick, RaceWire.EndReason.Finished)), "The server's run must finish where the recorded run did.");
            bool corrected = RunClientToEnd(log, server, end, 15, out ClientPredictor predictor);
            Assert.That(corrected, Is.False);
            Assert.That(predictor.Corrections, Is.EqualTo(0));
            Assert.That(predictor.ReplayedTicks, Is.EqualTo(0));
            Assert.That(predictor.StatesCompared, Is.EqualTo(end.Tick));
            Assert.That(predictor.ServerEnded, Is.True);
            Assert.That((predictor.EndTick, predictor.EndReason), Is.EqualTo((finishTick, RaceWire.EndReason.Finished)));
            Assert.That(game.HasFinished, Is.True);
            Assert.That(game.Tick, Is.EqualTo(finishTick));
            Debug.Log($"Net end: the race finished at tick {finishTick} on the server and on the client, with no correction.");
        }

        // The end must come after the server's state for the tick before it; anything else is a bug, and so is an end the
        // client cannot play the same way. Either way the end cannot be taken, so the predictor is Faulted (with the cause)
        // and refuses everything after; a refused end leaves the game as it was. Each case starts from its own fresh client.
        [UnityTest, Timeout(120000)]
        public IEnumerator ClientEnd_BeforeTheStateForTheTickBeforeIt_ThrowsLoudly()
        {
            yield return Load();
            const int ticks = 60;
            InputLog log = RecordBot(ticks);
            byte[][] truth = RunTruth(log, ticks);

            // Out of order: an end whose tick before has no compared state yet, and an end older than the newest compared state.
            foreach ((int endTick, string cause, string why) in new[]
            {
                (12, "not 11", "The state for tick 11 has not been compared."),
                (9, "not 8", "An end older than the newest compared state."),
            })
            {
                ClientPredictor refused = PredictorAt20(log, truth);
                byte[] stateBefore = SimSnapshotCodec.Encode(game.CaptureSnapshot());
                Assert.Throws<InvalidOperationException>(() => refused.OnServerEnd(endTick, RaceWire.EndReason.Failed, log[endTick - 1]), why);
                Assert.That(refused.ServerEnded, Is.False, $"end {endTick}");
                Assert.That(refused.Corrections, Is.EqualTo(0), $"end {endTick}");
                Assert.That(game.Tick, Is.EqualTo(20), $"end {endTick}: a refused end must not touch the game.");
                Assert.That(game.IsRunning, Is.True, $"end {endTick}");
                Assert.That(SimSnapshotCodec.SameState(SimSnapshotCodec.Encode(game.CaptureSnapshot()), stateBefore), Is.True, $"end {endTick}: a refused end must leave the game's state as it was.");
                // The refusal still breaks the prediction: the client cannot end its run as the server's did.
                Assert.That(refused.Faulted, Is.True, $"end {endTick}: a refused end must leave the predictor faulted.");
                string firstCause = refused.FaultReason;
                Assert.That(firstCause, Does.Contain($"tick {endTick}").And.Contain(cause), $"end {endTick}: the fault keeps its cause and tick.");
                var refusedConsume = Assert.Throws<InvalidOperationException>(() => refused.Consume(), $"end {endTick}: a faulted predictor gives no controls.");
                Assert.That(refusedConsume.Message, Does.Contain(firstCause), $"end {endTick}: the refusal carries the cause.");
                Assert.Throws<InvalidOperationException>(() => refused.OnServerState(truth[10]), $"end {endTick}: a faulted predictor compares no more states.");
                Assert.Throws<InvalidOperationException>(() => refused.OnServerEnd(10, RaceWire.EndReason.Failed, log[9]), $"end {endTick}: a faulted predictor takes no other end, not even one in order.");
                Assert.That(refused.FaultReason, Is.EqualTo(firstCause), $"end {endTick}: later refusals keep the first cause.");
                Assert.That(refused.Corrections, Is.EqualTo(0), $"end {endTick}: the end in order after the fault was not replayed.");
                Assert.That(game.Tick, Is.EqualTo(20), $"end {endTick}");
            }

            // In order, but the run does not fall on tick 10 on these controls: the replay cannot play the server's ending.
            ClientPredictor predictor = PredictorAt20(log, truth);
            Assert.Throws<InvalidOperationException>(() => predictor.OnServerEnd(10, RaceWire.EndReason.Failed, log[9]), "An end the client's simulation does not reproduce.");
            Assert.That(predictor.ServerEnded, Is.False);

            // That leaves the game on a replayed tick 10 that is neither run: the predictor is broken for good, and says so on
            // everything that would go on from there (a tick, a state, another end), not just once.
            Assert.That(predictor.Faulted, Is.True, "An end that could not be played must leave the predictor faulted.");
            Assert.That(predictor.FaultReason, Does.Contain("tick 10"), "The fault keeps its cause.");
            var onConsume = Assert.Throws<InvalidOperationException>(() => predictor.Consume(), "A faulted predictor gives no controls.");
            Assert.That(onConsume.Message, Does.Contain(predictor.FaultReason), "The refusal carries the cause.");
            Assert.That(game.IsRunning, Is.True, "precondition: the replayed tick 10 left the run going, so a step would ask for controls.");
            Assert.Throws<InvalidOperationException>(() => game.StepTick(predictor), "A tick on a faulted predictor must not run quietly.");
            Assert.Throws<InvalidOperationException>(() => predictor.OnServerState(truth[10]), "A faulted predictor compares no more states.");
            Assert.Throws<InvalidOperationException>(() => predictor.OnServerEnd(11, RaceWire.EndReason.Failed, log[10]), "A faulted predictor takes no other end.");
            Assert.That(predictor.Faulted, Is.True, "Nothing lifts the fault.");
            Assert.That(predictor.ServerEnded, Is.False);

            // A new race needs a new predictor: Clear is a restart, which a network race does not have
            // (Client_RestartedDuringARace_IsFaultedAndStaysFaulted).
            string replayFault = predictor.FaultReason;
            predictor.Clear();
            Assert.That(predictor.Faulted, Is.True, "Clear does not lift the fault.");
            Assert.That(predictor.FaultReason, Is.EqualTo(replayFault), "The first cause stays.");
        }

        // ---- the client's run ended first ---------------------------------------------------------------

        // The mirror of the replay test above: the client fired the slingshot on the tick F its run finishes (FindClientOnlyFinish),
        // the server missed F's controls, ran F on the tick before's (no press) and stayed short of the line. The
        // server's state for F reaches the client after its own run has finished there: the server's run was still going, so
        // the state is a correction. The client goes back to it, catches up on live controls (sent like any other), and ends
        // where the server's run ends, on the same tick, for the same reason, at the same position, with no other correction.
        // The server is given the client's controls after F as if they arrived in time.
        [UnityTest, Timeout(300000)]
        public IEnumerator ClientEnd_WhenOnlyTheClientsRunFinished_GoesBackToTheServersStateAndEndsWhereTheServersRunDoes()
        {
            yield return Load();
            InputLog log = RecordSwingBotToFinish(RaceTicksLimit, LongRelease);
            ClientOnlyFinish found = FindClientOnlyFinish(log, game.Tick, false);
            int finish = found.FinishTick;
            byte[][] server = RunServerToEnd(found.ClientLog, RaceTicksLimit, found.ServerMisses, out ServerEnd end);
            var serverEnded = EndedAt();
            Assert.That(end.Reason, Is.EqualTo(RaceWire.EndReason.Finished), "The server's run must finish too, later.");
            Assert.That(end.Tick, Is.GreaterThan(finish), $"The server missed the press on tick {finish} and must still have been running on it.");
            Assert.That(server.Length, Is.EqualTo(end.Tick), "One state for every tick before the server's end, F's among them.");

            foreach (int latency in new[] { 5, 15, 40 })
            {
                var sent = new List<int>();
                ClientPredictor predictor = StartClient(found.ClientLog, int.MaxValue, sent);
                bool tookFinishState = false;
                bool corrected = StepClientToEnd(predictor, server, end, latency, tick =>
                {
                    if (tick == finish - 1)
                    {
                        Assert.That((game.HasFinished, game.Tick), Is.EqualTo((true, finish)), $"latency {latency}: precondition: the client's own run finishes on tick {finish}.");
                        // At the tick that finished it the client had sent `latency` + 1 ticks past the newest state compared:
                        // a step runs its tick before that step's state arrives.
                        Assert.That((predictor.ClientEndTick, predictor.ClientEndLead), Is.EqualTo((finish, latency + 1)), $"latency {latency}: the client's end and its lead then are remembered.");
                        Assert.That(predictor.Corrections, Is.EqualTo(0), $"latency {latency}: every state before F agrees.");
                    }
                    else if (tick == finish)
                    {
                        tookFinishState = true;
                        int caughtUpTo = Math.Min(end.Tick, finish + latency + 1);
                        Assert.That(predictor.Corrections, Is.EqualTo(1), $"latency {latency}: the server's state for F is a correction.");
                        Assert.That(predictor.FirstMismatchTick, Is.EqualTo(finish), $"latency {latency}");
                        Assert.That(predictor.LastComparedTick, Is.EqualTo(finish), $"latency {latency}");
                        Assert.That(predictor.ReplayedTicks, Is.EqualTo(0), $"latency {latency}: no controls past F were taken, so nothing is replayed.");
                        Assert.That((predictor.CatchUps, predictor.CaughtUpTicks), Is.EqualTo((1, caughtUpTo - finish)), $"latency {latency}: the client catches up on live controls until it is as far ahead as when its run ended, or its run ends.");
                        Assert.That(game.Tick, Is.EqualTo(caughtUpTo), $"latency {latency}");
                        Assert.That(sent, Is.EqualTo(TickRange(1, game.Tick)), $"latency {latency}: every tick's controls are sent once and in order, the caught-up ones too.");
                    }
                });
                Assert.That(tookFinishState, Is.True, $"latency {latency}: the server's state for F was never delivered.");
                Assert.That(corrected, Is.False, $"latency {latency}: from the server's state the client runs on as the server did, so the end changes nothing.");
                Assert.That(predictor.Corrections, Is.EqualTo(1), $"latency {latency}");
                Assert.That(predictor.Faulted, Is.False, $"latency {latency}");
                Assert.That((predictor.ServerEnded, predictor.EndTick, predictor.EndReason), Is.EqualTo((true, end.Tick, end.Reason)), $"latency {latency}");
                Assert.That(predictor.StatesCompared, Is.EqualTo(end.Tick), $"latency {latency}: every state the server sent (ticks 0..{end.Tick - 1}) is taken, F's too.");
                Assert.That(EndedAt(), Is.EqualTo(serverEnded), $"latency {latency}: the client's run must end exactly where the server's did (tick, reason, position, progress).");
                Debug.Log($"Net client-only finish: {found.Found}; the server missed tick {finish} and finished on tick {end.Tick}; latency {latency} ticks: the client went back to the server's state {finish} and caught up {predictor.CaughtUpTicks} ticks, ending on tick {game.Tick}.");
            }
        }

        // A correction's replay can be what finishes the client's run, with controls left past the tick it finishes on. The
        // client steered toward the corner pole on D - 1 and away on D during the last swing, which left its rope as it was,
        // and pressed the chain action on F (FindClientOnlyFinish with a swing tick missed): its own run is still going after
        // F. The server missed D (a rope 0.24 m shorter, a faster swing) and F (no press): its state for D corrects the client
        // after the client has run past F, and the replay, from the server's state on the client's own later controls,
        // finishes on F, short of the last tick with controls. Then the server's state for F arrives: the server's
        // run was still going there, so the client goes back to it and runs its remembered controls after F again (they
        // were sent, so they are not taken anew), and ends where the server's run ends.
        [UnityTest, Timeout(300000)]
        public IEnumerator ClientEnd_WhenACorrectionsReplayFinishedBeforeItsLastControls_GoesBackToTheServersStateAndReplaysTheRest()
        {
            yield return Load();
            InputLog log = RecordSwingBotToFinish(RaceTicksLimit, ShortRelease);
            ClientOnlyFinish found = FindClientOnlyFinish(log, game.Tick, true);
            int finish = found.FinishTick;
            int missed = finish - found.MissLead;
            // The state for D arrives once the client has run its own tick F + 2.
            int latency = found.MissLead + 3;
            Assert.That(latency, Is.LessThan(ClientPredictor.Window), "precondition: the predictor must still hold the ticks since D.");
            byte[][] server = RunServerToEnd(found.ClientLog, RaceTicksLimit, found.ServerMisses, out ServerEnd end);
            var serverEnded = EndedAt();
            Assert.That(end.Reason, Is.EqualTo(RaceWire.EndReason.Finished), "The server's run must finish too, later.");
            Assert.That(end.Tick, Is.GreaterThan(finish), $"The server missed the press on tick {finish} and must still have been running on it.");

            var sent = new List<int>();
            ClientPredictor predictor = StartClient(found.ClientLog, int.MaxValue, sent);
            int lastInput = -1;
            int lead = -1;
            int replayedBefore = -1;
            bool tookFinishState = false;
            bool corrected = StepClientToEnd(predictor, server, end, latency, tick =>
            {
                if (tick < missed)
                {
                    Assert.That(predictor.Corrections, Is.EqualTo(0), $"tick {tick}: the server's states before the missed swing tick agree.");
                }
                else if (tick == missed)
                {
                    lastInput = predictor.LastInputTick;
                    lead = predictor.ClientEndLead;
                    Assert.That(predictor.Corrections, Is.EqualTo(1), $"The server's state for the missed swing tick {missed} corrects the client.");
                    Assert.That(predictor.FirstMismatchTick, Is.EqualTo(missed));
                    Assert.That((game.HasFinished, game.Tick), Is.EqualTo((true, finish)), $"The replay from the server's state {missed}, on the client's own later controls, must finish on tick {finish}.");
                    Assert.That(lastInput, Is.GreaterThan(finish), "precondition: the client had controls past F when its replay finished there.");
                    Assert.That((predictor.ClientEndTick, lead), Is.EqualTo((finish, lastInput - missed)), "The replay's end and the lead then are remembered.");
                    Assert.That(predictor.ReplayedTicks, Is.EqualTo(finish - missed), "The replay stops on the tick the run ends.");
                    replayedBefore = predictor.ReplayedTicks;
                }
                else if (tick < finish)
                {
                    Assert.That(predictor.Corrections, Is.EqualTo(1), $"tick {tick}: after the replay the client is on the server's run.");
                }
                else if (tick == finish)
                {
                    tookFinishState = true;
                    // The remembered controls run up to the last one, or until the run ends; live ones only after that.
                    int replayedTo = Math.Min(end.Tick, lastInput);
                    int caughtUpTo = Math.Max(replayedTo, Math.Min(end.Tick, finish + lead));
                    Assert.That(predictor.Corrections, Is.EqualTo(2), $"The server's state for F, past the client's end, is the second correction.");
                    Assert.That(predictor.LastComparedTick, Is.EqualTo(finish));
                    Assert.That(predictor.ReplayedTicks - replayedBefore, Is.EqualTo(replayedTo - finish), "The remembered controls after F are run again.");
                    Assert.That(game.Tick, Is.EqualTo(caughtUpTo));
                    Assert.That(predictor.CaughtUpTicks, Is.EqualTo(caughtUpTo - replayedTo), "Only ticks past the last remembered controls are caught up.");
                    Assert.That(sent, Is.EqualTo(TickRange(1, Math.Max(lastInput, game.Tick))), "No tick's controls are sent twice.");
                }
            });
            Assert.That(tookFinishState, Is.True, "The server's state for F was never delivered.");
            Assert.That(corrected, Is.False, "From the server's state the client runs on as the server did, so the end changes nothing.");
            Assert.That(predictor.Corrections, Is.EqualTo(2));
            Assert.That(predictor.Faulted, Is.False);
            Assert.That((predictor.ServerEnded, predictor.EndTick, predictor.EndReason), Is.EqualTo((true, end.Tick, end.Reason)));
            Assert.That(predictor.StatesCompared, Is.EqualTo(end.Tick));
            Assert.That(EndedAt(), Is.EqualTo(serverEnded), "The client's run must end exactly where the server's did (tick, reason, position, progress).");
            Debug.Log($"Net replay-ended client: {found.Found}; the server missed ticks {missed} and {finish} and finished on tick {end.Tick}; latency {latency} ticks: the state for {missed} replayed the client to a finish on {finish} with controls up to {lastInput}, the state for {finish} put it back on the server's run, ending on tick {game.Tick}.");
        }

        // The catch-up never runs past the limit it was given (RaceClient's target: controls past it are not sent). With the
        // limit on F itself the client, after the server's state for F, stands on that state exactly, though it was
        // `latency` + 1 ticks ahead when its run ended, and takes no controls past F until its next live tick.
        [UnityTest, Timeout(300000)]
        public IEnumerator ClientEnd_CatchingUpAfterTheClientsOwnFinish_StopsAtTheCatchUpLimit()
        {
            yield return Load();
            InputLog log = RecordSwingBotToFinish(RaceTicksLimit, LongRelease);
            ClientOnlyFinish found = FindClientOnlyFinish(log, game.Tick, false);
            int finish = found.FinishTick;
            const int latency = 15;
            byte[][] server = RunServerToEnd(found.ClientLog, RaceTicksLimit, found.ServerMisses, out ServerEnd end);
            var serverEnded = EndedAt();
            Assert.That(end.Tick, Is.GreaterThan(finish), $"The server missed the press on tick {finish} and must still have been running on it.");

            var sent = new List<int>();
            ClientPredictor predictor = StartClient(found.ClientLog, finish, sent);
            bool tookFinishState = false;
            bool corrected = StepClientToEnd(predictor, server, end, latency, tick =>
            {
                if (tick == finish - 1)
                {
                    Assert.That((predictor.ClientEndTick, predictor.ClientEndLead), Is.EqualTo((finish, latency + 1)), "precondition: without the limit the client would catch up past F.");
                }
                else if (tick == finish)
                {
                    tookFinishState = true;
                    Assert.That(predictor.Corrections, Is.EqualTo(1));
                    Assert.That((game.Tick, game.IsRunning), Is.EqualTo((finish, true)), "The catch-up stops at the limit: the client stands on the server's state for F, running.");
                    Assert.That(SimSnapshotCodec.SameState(SimSnapshotCodec.Encode(game.CaptureSnapshot()), server[finish]), Is.True, "The client's state is the server's, bit for bit.");
                    Assert.That((predictor.CatchUps, predictor.CaughtUpTicks), Is.EqualTo((0, 0)));
                    Assert.That(predictor.ClientEndTick, Is.EqualTo(-1), "The run is going again; its end is forgotten.");
                    Assert.That(predictor.LastInputTick, Is.EqualTo(finish));
                    Assert.That(sent, Is.EqualTo(TickRange(1, finish)), "No controls past the limit are taken by the catch-up.");
                }
            });
            Assert.That(tookFinishState, Is.True, "The server's state for F was never delivered.");
            Assert.That(corrected, Is.False, "From the server's state the client's next live ticks run as the server's did.");
            Assert.That(predictor.Corrections, Is.EqualTo(1));
            Assert.That(predictor.CaughtUpTicks, Is.EqualTo(0), "Live ticks after a stopped catch-up are not catch-up ticks.");
            Assert.That(EndedAt(), Is.EqualTo(serverEnded), "The client's run must end exactly where the server's did (tick, reason, position, progress).");
            Debug.Log($"Net catch-up limit: {found.Found}; latency {latency} ticks: the catch-up stopped at the limit {finish}; the client then finished on tick {game.Tick} with the server.");
        }

        // ---- a broken prediction stays broken -------------------------------------------------------------

        // A state the predictor refuses (from the future, or older than one already compared) is a broken stream, like an end
        // out of order: the predictor is Faulted with the cause and the tick, refuses everything after, and the refused state
        // leaves the game as it was. Each case starts from its own fresh client.
        [UnityTest, Timeout(120000)]
        public IEnumerator Client_AStateThatCannotBeTaken_LeavesThePredictorFaulted()
        {
            yield return Load();
            const int ticks = 60;
            InputLog log = RecordBot(ticks);
            byte[][] truth = RunTruth(log, ticks);

            foreach ((int stateTick, string why) in new[]
            {
                (21, "A state for a tick the client has not run yet."),
                (8, "A state older than the newest compared one."),
            })
            {
                ClientPredictor refused = PredictorAt20(log, truth);
                byte[] stateBefore = SimSnapshotCodec.Encode(game.CaptureSnapshot());
                Assert.Throws<InvalidOperationException>(() => refused.OnServerState(truth[stateTick]), why);
                Assert.That(game.Tick, Is.EqualTo(20), $"state {stateTick}: a refused state must not touch the game.");
                Assert.That(SimSnapshotCodec.SameState(SimSnapshotCodec.Encode(game.CaptureSnapshot()), stateBefore), Is.True, $"state {stateTick}: a refused state must leave the game's state as it was.");
                Assert.That(refused.Faulted, Is.True, $"state {stateTick}: a refused state must leave the predictor faulted.");
                string firstCause = refused.FaultReason;
                Assert.That(firstCause, Does.Contain($"tick {stateTick}"), $"state {stateTick}: the fault keeps its cause and tick.");
                var refusedConsume = Assert.Throws<InvalidOperationException>(() => refused.Consume(), $"state {stateTick}: a faulted predictor gives no controls.");
                Assert.That(refusedConsume.Message, Does.Contain(firstCause), $"state {stateTick}: the refusal carries the cause.");
                Assert.Throws<InvalidOperationException>(() => refused.OnServerState(truth[10]), $"state {stateTick}: a faulted predictor compares no more states, not even one in order.");
                Assert.Throws<InvalidOperationException>(() => refused.OnServerEnd(11, RaceWire.EndReason.Failed, log[10]), $"state {stateTick}: a faulted predictor takes no end.");
                Assert.That(refused.FaultReason, Is.EqualTo(firstCause), $"state {stateTick}: later refusals keep the first cause.");
                Assert.That((refused.StatesCompared, refused.LastComparedTick), Is.EqualTo((10, 9)), $"state {stateTick}: nothing after the fault was compared.");
            }
        }

        // R or Enter (ChainRushGame.Update) restarts the run, and StartRun clears the input source, the predictor. A network
        // race has no restart (the server's run goes on), so once the predictor has remembered its run's first state a Clear
        // breaks the prediction instead of starting it over; and nothing lifts a fault, not even another Clear.
        [UnityTest, Timeout(120000)]
        public IEnumerator Client_RestartedDuringARace_IsFaultedAndStaysFaulted()
        {
            yield return Load();
            const int ticks = 60;
            InputLog log = RecordBot(ticks);
            byte[][] truth = RunTruth(log, ticks);

            // Made while the game still runs on another source (RaceClient makes it in Begin, before it connects): it hears
            // those ticks too, and setting it and starting the run afterwards is still a start, not a restart.
            activePredictor?.Detach();
            var early = new ClientPredictor(game, new InputReplay(log));
            activePredictor = early;
            var other = new InputReplay(log);
            game.SetInputSource(other);
            game.StartRun();
            for (int i = 0; i < 5; i++) game.StepTick(other);
            game.SetInputSource(early);
            game.StartRun();
            early.AfterTick();
            Assert.That(early.Faulted, Is.False, "Ticks run on another source before the predictor was set are not its run.");

            ClientPredictor predictor = PredictorAt20(log, truth);
            game.StartRun();
            Assert.That(predictor.Faulted, Is.True, "A restart in the middle of a network race must break the prediction.");
            string cause = predictor.FaultReason;
            Assert.That(cause, Does.Contain("restart").And.Contain("tick 20"), "The fault says what happened and where the run was.");
            Assert.That((predictor.LastInputTick, predictor.LastComparedTick, predictor.StatesCompared), Is.EqualTo((20, 9, 10)), "The broken prediction is kept as it was, not cleared.");
            Assert.That(game.IsRunning, Is.True, "precondition: the restarted run is going, so a step would ask for controls.");
            var onConsume = Assert.Throws<InvalidOperationException>(() => game.StepTick(predictor), "A tick on a restarted network race must not run quietly.");
            Assert.That(onConsume.Message, Does.Contain(cause), "The refusal carries the cause.");
            Assert.Throws<InvalidOperationException>(() => predictor.OnServerState(truth[10]), "A faulted predictor compares no more states.");

            predictor.Clear();
            game.SetInputSource(predictor);
            Assert.That((predictor.Faulted, predictor.FaultReason), Is.EqualTo((true, cause)), "Nothing lifts the fault, and the first cause stays.");
        }

        // A correction that arrives while the client stands paused (RaceClient pauses the run on its last tick and waits for the
        // server's last states) moves the runner from where it is drawn, the state of the paused tick, not from the origin.
        [UnityTest, Timeout(180000)]
        public IEnumerator Client_ACorrectionWhilePaused_MeasuresTheJoltFromWhereTheRunnerStands()
        {
            yield return Load();
            InputLog log = RecordBot(BotTicks);
            int dropTick = BiggestSteerChange(log, 600, 1000);
            byte[][] server = RunServer(log, BotTicks, dropTick, out _);
            const int latency = 15;
            ClientPredictor predictor = StartClient(log);
            for (int step = 1; step <= dropTick + latency; step++)
            {
                game.StepTick(predictor);
                int arrived = step - latency;
                if (arrived >= 0 && arrived < dropTick) predictor.OnServerState(server[arrived]);
            }
            Assert.That(predictor.Corrections, Is.EqualTo(0), "precondition: every state before the missed input agrees.");
            game.TogglePause();
            Assert.That(game.IsPaused, Is.True);
            Vector3 drawn = player.transform.position;

            Assert.That(predictor.OnServerState(server[dropTick]), Is.True, "The missed input's state is a correction.");
            Assert.That((game.IsRunning, game.Tick), Is.EqualTo((true, dropTick + latency)), "The replay runs up to the last tick with controls.");
            Vector3 after = game.CaptureSnapshot().Position;
            float expected = Vector3.Distance(drawn, after);
            Assert.That(Vector3.Distance(Vector3.zero, after), Is.Not.EqualTo(expected), "precondition: a jolt measured from the origin must be told apart.");
            Assert.That(predictor.LargestJolt, Is.EqualTo(expected), "The jolt is measured from where the paused runner stood.");
            Debug.Log($"Net paused correction: missed input at tick {dropTick}, corrected while paused at tick {dropTick + latency}; jolt {predictor.LargestJolt:F4} m (from the origin it would read {Vector3.Distance(Vector3.zero, after):F4} m).");
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

        // A fresh client 10 ticks into the log, with no server state compared.
        private ClientPredictor PredictorAt10(InputLog log)
        {
            activePredictor?.Detach();
            var predictor = new ClientPredictor(game, new InputReplay(log));
            activePredictor = predictor;
            Time.timeScale = 0f;
            game.SetInputSource(predictor);
            game.StartRun();
            predictor.AfterTick();
            for (int i = 0; i < 10; i++) game.StepTick(predictor);
            return predictor;
        }

        // A fresh client 20 ticks into the log, with the server's states for ticks 0..9 compared (all the truth's).
        private ClientPredictor PredictorAt20(InputLog log, byte[][] truth)
        {
            activePredictor?.Detach();
            var predictor = new ClientPredictor(game, new InputReplay(log));
            activePredictor = predictor;
            Time.timeScale = 0f;
            game.SetInputSource(predictor);
            game.StartRun();
            predictor.AfterTick();
            for (int i = 0; i < 20; i++) game.StepTick(predictor);
            for (int t = 0; t <= 9; t++) predictor.OnServerState(truth[t]);
            Assert.That((game.Tick, predictor.LastComparedTick, predictor.Faulted), Is.EqualTo((20, 9, false)));
            return predictor;
        }

        // The bot's run to the finish of the race, recorded; the game is left finished.
        private InputLog RecordBotToFinish(int maxTicks)
        {
            var log = new InputLog();
            var recorder = new InputRecorder(new CircuitBot(game, player, grapple, circuit), log);
            Time.timeScale = 0f;
            game.SetInputSource(recorder);
            game.StartRun();
            while (game.IsRunning && game.Tick < maxTicks) game.StepTick(recorder);
            Assert.That(game.HasFinished, Is.True, $"The bot's run did not finish within {maxTicks} ticks: tick {game.Tick}, failed {game.HasFailed}.");
            Assert.That(log.Count, Is.GreaterThanOrEqualTo(game.Tick));
            return log;
        }

        // A tick whose input, when the server misses it, makes the server's run fall and be put back on the track: a press of
        // the primary control (a jump or a grapple) that the repeated input of the tick before loses. Each press is tried from
        // the truth's state before it; respawnTick is the tick the run was put back on.
        private int FindRespawningDrop(InputLog log, byte[][] truth, out int respawnTick)
        {
            Time.timeScale = 0f;
            var tried = new System.Text.StringBuilder();
            for (int d = 3; d < BotTicks; d++)
            {
                if (!log[d - 1].PrimaryPressed || log[d - 2].PrimaryPressed) continue;
                var source = new TickReplay(game, log, d);
                game.RestoreSnapshot(SimSnapshotCodec.Decode(truth[d - 1]));
                while (game.IsRunning && !player.IsRespawning && game.Tick < BotTicks) game.StepTick(source);
                tried.Append($" {d}:{(player.IsRespawning ? "fell" : "ran")}@{game.Tick}");
                if (player.IsRespawning)
                {
                    respawnTick = game.Tick;
                    return d;
                }
            }
            Assert.Fail($"No single missed press makes the run fall within {BotTicks} ticks. Presses tried (tick:outcome@last tick):{tried}");
            respawnTick = -1;
            return -1;
        }

        private static int RespawnHoldOf(byte[] state) => SimSnapshotCodec.Decode(state).Racer.RespawnHoldTicks;

        // ---- finish cases: the levers (R4b) ---------------------------------------------------------------
        //
        // A run finishes on the tick the lap counter's progress reaches the race length (LapCounter.Update, CircuitRace.Step,
        // ChainRushGame.RunTick), by however far it passed the line on that tick. For the client and the server to split on
        // one tick's controls, that tick's controls must move the runner by more than the margin. Until R4b drift was the
        // lever (it slowed a grounded runner for the ticks it was held); drift is gone, and steering moves the runner along
        // the track by far less than a millimetre in one tick. These searches use the slingshot instead: a press of the chain
        // action with a gauge slot fires it and speeds the runner up on that very tick by about 0.02 to 0.03 m.
        // The gauge comes from the run itself: PoleSwingBot swings every arc (35 m swung = one slot), so the gauge is full
        // after the first arc and again after the last, and it lets go releaseMargin before each arc ends, so the finish line
        // (the end of the last arc) is crossed running, where a slingshot has its effect.
        // Where the run is when it reaches the line is tuned by two ticks of rope: during the last swing, steering toward the
        // pole on two ticks shortens the rope by 0.24 m, and the swing runs about 0.6 % faster for the rest of it (about
        // 1.2 mm further a tick). Trying that tweak at TuneTicks places, one tick apart, moves the margin in steps far finer
        // than the lever, over more than one tick's travel. The search is bounded by this fixed set of tries, not by the
        // clock, so its outcome does not depend on the machine; the test's Timeout guards the wall clock.

        // How far before each arc's end PoleSwingBot lets go: far enough for a slingshot pressed shortly before the finish
        // line to find the runner off the pole, for the searches that fire one there. Less than an arc (R4c 2026-10-18:
        // R22 quarter turns, 34.6 m), or the bot never hooks a pole.
        private const float LongRelease = 12f;
        // Just before the end: a swing that lasts until shortly before the finish line, for the searches that need the server's
        // run to differ from the client's within a few hundred ticks of it, or a slingshot to run on through the swing.
        private const float ShortRelease = 4f;
        // The rope tweak's places: its second tick on each of the last TuneTicks swing ticks before the release.
        private const int TuneTicks = 240;
        // ChainRushCircuit.unity's runner (2026-10-19): slingPullTime 0.4 s + slingCarryTime 2.6 s. A press while a
        // slingshot runs does nothing, so a press on the slingshot's last tick does nothing and the same press one tick later
        // fires a new one.
        private const int SlingTicks = 150;
        // The farthest a missed tick may be before the finish in FindClientOnlyFinish(missFirst): the client's predictor keeps
        // ClientPredictor.Window ticks, and the test delivers the state for the missed tick after the client has run past
        // the finish (a latency a little over this).
        private const int MaxMissLead = 250;
        // How hard SwingBotFrom's rope tweak steers. Since a slingshot speeds a swing up (2026-10-19 "C"), a full tweak in the
        // slingshot-shortened last swing moved the finish about 11 mm per tick of its place, more than the lever (the second
        // slingshot's first tick, 3 to 6 mm), so the search stepped over every fit; a gentler tweak steps finer.
        private const float SwingTweakSteer = 0.15f;

        // The circuit bot, plus the swing key: in an arc with more than releaseMargin of it ahead, a corner pole in reach is
        // hooked and held, with no steering while swinging (steering works the rope, so it keeps its length); with
        // releaseMargin or less of the arc left the key is let go and the circuit bot runs on. Reads only the game, so it can
        // go on from a restored snapshot.
        private sealed class PoleSwingBot : IInputSource
        {
            private readonly ChainRushGame game;
            private readonly RunnerMotor player;
            private readonly CircuitBot driver;
            private readonly float releaseMargin;

            public PoleSwingBot(ChainRushGame game, RunnerMotor player, GrappleController grapple, CircuitRace circuit, float releaseMargin)
            {
                this.game = game;
                this.player = player;
                this.releaseMargin = releaseMargin;
                driver = new CircuitBot(game, player, grapple, circuit);
            }

            public void Poll() { }
            public void Clear() { }

            public TickInput Consume()
            {
                TickInput driven = driver.Consume();
                double s = game.Track.Project(player.transform.position).S;
                bool inArc = game.Track.FrameAt(s).Curvature != 0f && game.Track.FrameAt(s + releaseMargin).Curvature != 0f;
                bool swinging = player.IsSwinging;
                bool press = !swinging && inArc && player.TryFindPole(out _);
                bool hold = swinging ? inArc : press;
                float steer = swinging || press ? 0f : driven.Steer;
                return new TickInput(steer, driven.PrimaryPressed, false, false, hold, false, press);
            }
        }

        // PoleSwingBot's run to the finish of the race, recorded; the game is left finished.
        private InputLog RecordSwingBotToFinish(int maxTicks, float releaseMargin)
        {
            var log = new InputLog();
            var recorder = new InputRecorder(new PoleSwingBot(game, player, grapple, circuit, releaseMargin), log);
            Time.timeScale = 0f;
            game.SetInputSource(recorder);
            game.StartRun();
            while (game.IsRunning && game.Tick < maxTicks) game.StepTick(recorder);
            Assert.That(game.HasFinished, Is.True, $"The swing bot's run did not finish within {maxTicks} ticks: tick {game.Tick}, failed {game.HasFailed}.");
            Assert.That(game.Racer.Gauge, Is.EqualTo(RacerState.MaxGauge), "The swings must have filled the gauge.");
            return log;
        }

        // A recorded run's states: [t] is the state after tick t (the one tick t + 1 starts from), for t = from .. finish - 1.
        private byte[][] StatesBefore(InputLog log, int finish, int from)
        {
            var states = new byte[finish][];
            var replay = new InputReplay(log);
            Time.timeScale = 0f;
            game.SetInputSource(replay);
            game.StartRun();
            while (game.IsRunning && game.Tick < finish)
            {
                if (game.Tick >= from) states[game.Tick] = SimSnapshotCodec.Encode(game.CaptureSnapshot());
                game.StepTick(replay);
            }
            Assert.That((game.HasFinished, game.Tick), Is.EqualTo((true, finish)), "Replaying the recorded log must finish where the recording did.");
            return states;
        }

        // The last tick of the recorded run's last swing before the finish (the tick it ended on), from its states.
        private static int LastSwingTick(byte[][] states, int from, int finish)
        {
            for (int t = finish - 1; t > from; t--)
                if (SimSnapshotCodec.Decode(states[t - 1]).Racer.SwingTicks > 0 && SimSnapshotCodec.Decode(states[t]).Racer.SwingTicks == 0) return t;
            Assert.Fail($"The recorded run has no swing that ends between ticks {from} and {finish}.");
            return -1;
        }

        // +1 when the pole of the swing in `state` is on the runner's right (steer +1 is the key toward it), -1 when on its left.
        private static float PoleSide(byte[] state)
        {
            SimSnapshot s = SimSnapshotCodec.Decode(state);
            Assert.That(s.Racer.SwingTicks, Is.GreaterThan(0), $"The runner is not swinging after tick {s.Tick}.");
            float radians = s.Racer.Heading * Mathf.Deg2Rad;
            var right = new Vector3(Mathf.Cos(radians), 0f, -Mathf.Sin(radians));
            return Vector3.Dot(s.Racer.SwingCenter - s.Position, right) >= 0f ? 1f : -1f;
        }

        private static TickInput WithSteer(in TickInput x, float steer) =>
            new TickInput(steer, x.PrimaryPressed, x.ReleasePressed, x.AttackPressed, x.SwingHeld, x.ChainActionPressed, x.SwingPressed);

        private static TickInput WithChainPress(in TickInput x) =>
            new TickInput(x.Steer, x.PrimaryPressed, x.ReleasePressed, x.AttackPressed, x.SwingHeld, true, x.SwingPressed);

        // `log` as a log of `length` ticks (idle past its end, as InputReplay gives), with tick t's input changed by edit(t, input).
        private static InputLog Edited(InputLog log, int length, Func<int, TickInput, TickInput> edit)
        {
            var copy = new InputLog();
            for (int t = 1; t <= length; t++) copy.Add(edit(t, t <= log.Count ? log[t - 1] : default));
            return copy;
        }

        // The log with the rope tweak on ticks tick - 1 and tick: steer toward the pole (side, PoleSide) on both, or with
        // `undo` toward it on tick - 1 and away from it on tick, which leaves the rope as it was.
        private static InputLog Tweaked(InputLog log, int length, int tick, float side, bool undo) =>
            Edited(log, length, (t, x) => t == tick - 1 ? WithSteer(x, side) : t == tick ? WithSteer(x, undo ? -side : side) : x);

        // From `state`, the run on `log` until it ends (or maxTicks): the tick it finished on, or -1 when it did not finish.
        private int FinishFrom(byte[] state, InputLog log, int maxTicks)
        {
            var source = new LogAtTick(game, log);
            game.RestoreSnapshot(SimSnapshotCodec.Decode(state));
            while (game.IsRunning && game.Tick < maxTicks) game.StepTick(source);
            return game.HasFinished ? game.Tick : -1;
        }

        // From `state`, the run on `log` up to tick `tick` (the state tick + 1 starts from); null if the run ended first.
        private byte[] StateAfter(byte[] state, InputLog log, int tick)
        {
            var source = new LogAtTick(game, log);
            game.RestoreSnapshot(SimSnapshotCodec.Decode(state));
            while (game.IsRunning && game.Tick < tick) game.StepTick(source);
            return game.IsRunning && game.Tick == tick ? SimSnapshotCodec.Encode(game.CaptureSnapshot()) : null;
        }

        // Whether a chain press in `state` fires the slingshot: a whole slot, no slingshot running, and, unless swinging is
        // allowed, not swinging (a slingshot on a swing speeds the swing up instead, 2026-10-19 "C").
        private static bool SlingReady(byte[] state, bool swingAllowed = false)
        {
            RacerState.Snapshot racer = SimSnapshotCodec.Decode(state).Racer;
            return racer.SlingTicks == 0 && racer.Gauge >= 1f && (swingAllowed || racer.SwingTicks == 0);
        }

        // The client's controls for FindFinishReplayCase: `tuned` up to P - 1, then PoleSwingBot (ShortRelease) run on from
        // `state` (the state before P) so that it hooks the last pole wherever the slingshot has taken the runner, with the
        // chain pressed on P and on F - 1 and the rope tweak (toward the pole, SwingTweakSteer) on the swing's ticks tweakIndex - 1 and
        // tweakIndex (counted from its hook, RacerState.SwingTicks; 0: none); recorded until 64 ticks past F. beforeF is the
        // state F starts from, or null when the run ended first.
        // The tweak goes by the swing's own ticks, not the recording's tick numbers: since a slingshot speeds a swing up
        // (2026-10-19 "C"), the slingshot fired on P shortens the last swing, and ticks taken from the recorded swing could
        // land after this one ended, where the tweak does nothing (gate 2026-10-19: every Q gave the same results).
        private InputLog SwingBotFrom(byte[] state, InputLog tuned, int p, int f, int tweakIndex, out byte[] beforeF)
        {
            var client = new InputLog();
            for (int t = 1; t < p; t++) client.Add(tuned[t - 1]);
            var bot = new PoleSwingBot(game, player, grapple, circuit, ShortRelease);
            game.RestoreSnapshot(SimSnapshotCodec.Decode(state));
            beforeF = null;
            while (game.IsRunning && game.Tick < f + 64)
            {
                int t = game.Tick + 1;
                if (t == f) beforeF = SimSnapshotCodec.Encode(game.CaptureSnapshot());
                TickInput x = bot.Consume();
                int swingTick = game.Racer.SwingTicks;
                if (tweakIndex > 0 && swingTick > 0 && (swingTick == tweakIndex - 1 || swingTick == tweakIndex))
                    x = WithSteer(x, SwingTweakSteer * PoleSide(SimSnapshotCodec.Encode(game.CaptureSnapshot())));
                if (t == p || t == f - 1) x = WithChainPress(x);
                client.Add(x);
                StepOn(x);
            }
            return client;
        }

        // A finish the client has to be corrected to: the server finishes on tick F on the controls of the tick before (it
        // missed F's), the client's own controls for F keep its run going. The lever: the client pressed the chain action on
        // P = F - SlingTicks, which fired a slingshot ending on F - 1, and again on F - 1, which did nothing (the slingshot was
        // still running); the server repeats that press on F, where it fires a second slingshot. A slingshot (SlingTicks)
        // lasts longer than the last swing (an R22 quarter turn), so P comes before the last corner and the slingshot moves
        // where the runner gets there: from P on, the client's controls are PoleSwingBot's run on from that state
        // (SwingBotFrom), which hooks the pole wherever the runner is. With the rope tweaked on Q - 1 and Q of the last
        // swing (none first, then Q = R - 1, R - 2, .. back to the hook, for R the release), and P tried from early to
        // late, the client's own F must keep its run going while the server's repeated press finishes it on F.
        // Deterministic: Q, then P in order; the first fit is taken. Returns the client's log; F and a description come out.
        // Fails with every case tried when none fits.
        private InputLog FindFinishReplayCase(InputLog log, int recordedFinish, out int endTick, out string found)
        {
            Time.timeScale = 0f;
            // Measured for the log line only; never part of the judgement.
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var tried = new System.Text.StringBuilder();
            int from = recordedFinish - TuneTicks - 600;
            byte[][] states = StatesBefore(log, recordedFinish, from);
            int release = LastSwingTick(states, from, recordedFinish);
            int hooked = release;
            while (hooked > from && SimSnapshotCodec.Decode(states[hooked - 1]).Racer.SwingTicks > 0) hooked--;
            int length = recordedFinish + 64;
            for (int q = 0; q <= TuneTicks && (q == 0 || release - q - 2 >= hooked); q++)
            {
                int tweak = q == 0 ? 0 : release - q;
                int startTick = q == 0 ? release : tweak - 2;
                byte[] tunedStart = states[startTick];
                InputLog tuned = q == 0 ? log : Tweaked(log, length, tweak, PoleSide(tunedStart), false);
                int tunedFinish = FinishFrom(tunedStart, tuned, RaceTicksLimit);
                if (tunedFinish < 0)
                {
                    tried.Append($" Q{tweak}:ran");
                    continue;
                }
                tried.Append($" Q{tweak}:fin@{tunedFinish}[");
                // The first slingshot gains the runner up to some 60 ticks when it fires before the last corner, nothing when it
                // runs out inside the last swing.
                int firstP = tunedFinish - SlingTicks - 60, lastP = tunedFinish - SlingTicks + 2;
                // The tuned run's state before each P in turn, one tick on from the last. Before the tweak it is the recording's.
                byte[] beforeP = firstP - 1 <= startTick ? states[firstP - 1] : StateAfter(tunedStart, tuned, firstP - 1);
                for (int p = firstP; p <= lastP && beforeP != null; p++)
                {
                    int f = p + SlingTicks;
                    int press = p;
                    byte[] here = beforeP;
                    game.RestoreSnapshot(SimSnapshotCodec.Decode(here));
                    StepOn(tuned[p - 1]);
                    beforeP = game.IsRunning ? SimSnapshotCodec.Encode(game.CaptureSnapshot()) : null;
                    if (!SlingReady(here, true))
                    {
                        tried.Append($" P{p}:not-ready");
                        continue;
                    }
                    // The tweak's place within the swing: as far from the hook as it is in the recorded swing.
                    InputLog client = SwingBotFrom(here, tuned, press, f, q == 0 ? 0 : tweak - hooked + 1, out byte[] beforeF);
                    if (beforeF == null)
                    {
                        tried.Append($" P{p}:ends<{f}");
                        continue;
                    }
                    if (!SlingReady(beforeF))
                    {
                        tried.Append($" P{p}:F-not-ready");
                        continue;
                    }
                    TickInput repeated = client[f - 2];
                    TickInput own = client[f - 1];
                    bool ownEnds = EndsOnNextTick(beforeF, own, out _, out double ownPast);
                    bool serverEnds = EndsOnNextTick(beforeF, repeated, out RaceWire.EndReason serverReason, out double serverPast);
                    if (ownEnds || !serverEnds || serverReason != RaceWire.EndReason.Finished)
                    {
                        tried.Append($" P{p}:C{ownPast:+0.000;-0.000},A{serverPast:+0.000;-0.000}");
                        continue;
                    }
                    endTick = f;
                    found = $"rope tweak {(q == 0 ? "none" : $"on the last swing's ticks {tweak - hooked}-{tweak - hooked + 1}")}, chain presses on ticks {p} and {f - 1}: on tick {f} the server's repeated press fires a second slingshot and finishes {serverPast:F5} m past the line, the client's own controls leave it {-ownPast:F5} m short (search {watch.Elapsed.TotalSeconds:F1} s)";
                    return client;
                }
                tried.Append(" ]");
            }
            Assert.Fail($"No case fits: with the rope tweak on none of the swing ticks from the hook on tick {hooked} to the release on tick {release}, and the chain presses tried, does the server's repeated press finish the race on a tick the client's own controls do not (recorded finish {recordedFinish}).\n" +
                        $"Cases (Q tick:finish[P tick: C/A = the client's own / the server's repeated controls on F, +/- metres past the line]; search {watch.Elapsed.TotalSeconds:F1} s):{tried}");
            endTick = -1;
            found = null;
            return null;
        }

        // A run whose finish only the client sees, found by FindClientOnlyFinish.
        private struct ClientOnlyFinish
        {
            // The client's controls from tick 1, long enough to run past the finish (idle past the recorded log).
            public InputLog ClientLog;
            // F: the client's run, from the server's state before F, finishes on F; the server's run does not.
            public int FinishTick;
            // F - D, for D the first tick the server missed (missFirst); 0 when it missed only F.
            public int MissLead;
            // The ticks whose controls the server misses (and runs on the controls before): F, and D when asked for.
            public int[] ServerMisses;
            public string Found;
        }

        // A finish only the client sees: the mirror of FindFinishReplayCase. The client pressed the chain action on F and the
        // slingshot carried it over the line on F; the server missed F's controls, ran F on the tick before's (no press) and
        // stays short of the line. Tuned by the rope tweak on two ticks of the last swing (none first, then ending on R - 1,
        // R - 2, ..): F is the tick before the one the tuned run finishes on, and the press must carry it over.
        // With missFirst, the server also missed a tick D of the last swing (ShortRelease: the swing lasts until shortly
        // before the finish). The client steered toward the pole on D - 1 and away from it on D, which leaves its rope as it
        // was; the server ran D on D - 1's controls, so its rope is 0.24 m shorter and its swing faster for the rest of it. The
        // client, corrected from the server's state for D with its own later controls, must finish on F (its press carries it
        // over, the server's repeated controls of F - 1 do not), while its own run (the rope as it was) must not finish on F.
        // The client's own run is the recorded one whatever D is, so F is tried one and two ticks before its finish. D is
        // tried from the release back (the longer the faster rope runs, the farther ahead the server's run is), at most
        // MaxMissLead ticks before F. Deterministic: in order; the first fit is taken. Fails with every case tried when none fits.
        private ClientOnlyFinish FindClientOnlyFinish(InputLog log, int recordedFinish, bool missFirst)
        {
            Time.timeScale = 0f;
            // Measured for the log line only; never part of the judgement.
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var tried = new System.Text.StringBuilder();
            int from = recordedFinish - TuneTicks - 600;
            byte[][] states = StatesBefore(log, recordedFinish, from);
            int release = LastSwingTick(states, from, recordedFinish);
            int length = recordedFinish + 64;

            for (int q = 0; q <= TuneTicks; q++)
            {
                // missFirst: q counts the missed tick D back from the release instead of placing the rope tweak.
                if (missFirst && q == 0) continue;
                int tick = q == 0 ? 0 : release - q;
                byte[] start = states[q == 0 ? release : tick - 2];
                InputLog tuned = q == 0 ? log : Tweaked(log, length, tick, PoleSide(start), missFirst);
                int tunedFinish = FinishFrom(start, tuned, RaceTicksLimit);
                if (tunedFinish < 0)
                {
                    tried.Append($" {tick}:ran");
                    continue;
                }
                if (missFirst && tunedFinish - 1 - tick > MaxMissLead)
                {
                    tried.Append($" {tick}:lead{tunedFinish - 1 - tick}");
                    break;
                }
                tried.Append($" {tick}:fin@{tunedFinish}[");
                for (int back = 1; back <= (missFirst ? 2 : 1); back++)
                {
                    int f = tunedFinish - back;
                    InputLog client = Edited(tuned, length, (t, x) => t == f ? WithChainPress(x) : x);
                    byte[] beforeF = StateAfter(start, client, f - 1);
                    if (beforeF == null || !SlingReady(beforeF))
                    {
                        tried.Append($" F{f}:not-ready");
                        continue;
                    }
                    TickInput repeated = client[f - 2];
                    TickInput own = client[f - 1];
                    if (!missFirst)
                    {
                        bool serverEnds = EndsOnNextTick(beforeF, repeated, out _, out double serverPast);
                        bool clientEnds = EndsOnNextTick(beforeF, own, out RaceWire.EndReason clientReason, out double clientPast);
                        if (serverEnds || !clientEnds || clientReason != RaceWire.EndReason.Finished)
                        {
                            tried.Append($" F{f}:A{serverPast:+0.000;-0.000},C{clientPast:+0.000;-0.000}");
                            continue;
                        }
                        return new ClientOnlyFinish
                        {
                            ClientLog = client,
                            FinishTick = f,
                            MissLead = 0,
                            ServerMisses = new[] { f },
                            Found = $"rope tweak {(q == 0 ? "none" : $"on ticks {tick - 1}-{tick}")}, chain press on tick {f}: the server's repeated controls leave it {-serverPast:F5} m short of the line and the client's own carry it {clientPast:F5} m past (search {watch.Elapsed.TotalSeconds:F1} s)",
                        };
                    }

                    // The client's own run (rope as it was) must not finish on F, press and all.
                    bool ownEnds = EndsOnNextTick(beforeF, own, out _, out double ownPast);
                    if (ownEnds)
                    {
                        tried.Append($" F{f}:own{ownPast:+0.000;-0.000}");
                        continue;
                    }
                    // The server's run: D on D - 1's controls (missed), then the client's up to F - 1.
                    byte[] clientAtD = StateAfter(start, client, tick);
                    game.RestoreSnapshot(SimSnapshotCodec.Decode(StateAfter(start, client, tick - 1)));
                    StepOn(client[tick - 2]);
                    byte[] serverAtD = SimSnapshotCodec.Encode(game.CaptureSnapshot());
                    if (SimSnapshotCodec.SameState(serverAtD, clientAtD))
                    {
                        tried.Append($" F{f}:same@D");
                        continue;
                    }
                    byte[] serverBeforeF = StateAfter(serverAtD, client, f - 1);
                    if (serverBeforeF == null || !SlingReady(serverBeforeF))
                    {
                        tried.Append($" F{f}:srv-not-ready");
                        continue;
                    }
                    bool serverEndsOnF = EndsOnNextTick(serverBeforeF, repeated, out _, out double srvPast);
                    bool replayEnds = EndsOnNextTick(serverBeforeF, own, out RaceWire.EndReason replayReason, out double replayPast);
                    if (serverEndsOnF || !replayEnds || replayReason != RaceWire.EndReason.Finished)
                    {
                        tried.Append($" F{f}:own{ownPast:+0.000},A{srvPast:+0.000;-0.000},C{replayPast:+0.000;-0.000}");
                        continue;
                    }
                    return new ClientOnlyFinish
                    {
                        ClientLog = client,
                        FinishTick = f,
                        MissLead = f - tick,
                        ServerMisses = new[] { tick, f },
                        Found = $"the server missed tick {tick} of the last swing (its rope 0.24 m shorter than the client's from there) and the chain press on tick {f}: the client's own run stays {-ownPast:F5} m short on F, the server's repeated controls {-srvPast:F5} m short, and the client corrected from the server's state is carried {replayPast:F5} m past (search {watch.Elapsed.TotalSeconds:F1} s)",
                    };
                }
                tried.Append(" ]");
            }
            Assert.Fail($"No case fits{(missFirst ? " (the server also missing a tick of the last swing)" : "")}: tried the last {TuneTicks} swing ticks before the release on tick {release} (recorded finish {recordedFinish}).\n" +
                        $"Cases (tick:finish[F tick: A = the server's repeated controls on F, C = the client's own (or corrected) on F, own = the client's own run, +/- metres past the line]; search {watch.Elapsed.TotalSeconds:F1} s):{tried}");
            return default;
        }

        // One tick on `input`.
        private void StepOn(in TickInput input)
        {
            var one = new InputLog();
            one.Add(input);
            game.StepTick(new InputReplay(one));
        }

        // Whether one tick on `input` from the encoded state ends the run on that tick, why, and how far the lap counter's
        // progress then is past the race length (negative: short of the finish line).
        private bool EndsOnNextTick(byte[] state, in TickInput input, out RaceWire.EndReason reason, out double pastLine)
        {
            game.RestoreSnapshot(SimSnapshotCodec.Decode(state));
            int tick = game.Tick + 1;
            var one = new InputLog();
            one.Add(input);
            game.StepTick(new InputReplay(one));
            Assert.That(game.Tick, Is.EqualTo(tick), "One step is one tick.");
            reason = game.HasFailed ? RaceWire.EndReason.Failed : RaceWire.EndReason.Finished;
            LapCounter laps = circuit.Laps;
            pastLine = laps.Progress - laps.LapLength * laps.LapCount;
            return !game.IsRunning;
        }

        private static bool SameInput(in TickInput a, in TickInput b) =>
            a.Steer == b.Steer && TickInputCodec.ToFlags(a) == TickInputCodec.ToFlags(b);

        // The server's run to its end: every input but dropTick arrives in time (0 = none dropped). Returns the states it
        // handed out (ticks 0 .. end - 1) and the end it raised.
        private byte[][] RunServerToEnd(InputLog log, int maxTicks, int dropTick, out ServerEnd end) =>
            RunServerToEnd(log, maxTicks, new[] { dropTick }, out end);

        // The same, with every input but those of dropTicks arriving in time.
        private byte[][] RunServerToEnd(InputLog log, int maxTicks, int[] dropTicks, out ServerEnd end)
        {
            activeBuffer?.Detach();
            var buffer = new ServerInputBuffer(game);
            activeBuffer = buffer;
            var states = new List<byte[]>(maxTicks + 1);
            int ends = 0;
            ServerEnd raised = default;
            buffer.StateReady += bytes => states.Add(bytes);
            buffer.RunEnded += (tick, reason, input) =>
            {
                ends++;
                raised = new ServerEnd { Tick = tick, Reason = reason, Input = input };
            };
            for (int t = 1; t <= log.Count; t++) if (Array.IndexOf(dropTicks, t) < 0) buffer.Receive(t, log[t - 1]);
            Time.timeScale = 0f;
            game.SetInputSource(buffer);
            game.StartRun();
            buffer.AfterTick();
            while (game.IsRunning && game.Tick < maxTicks) game.StepTick(buffer);
            Assert.That(game.IsRunning, Is.False, $"The server's run was still going at tick {game.Tick}.");
            // After the end nothing more is handed out: no tick runs, and the end is not raised again.
            game.StepTick(buffer);
            buffer.AfterTick();
            Assert.That(ends, Is.EqualTo(1), "The end is raised once.");
            Assert.That(raised.Tick, Is.EqualTo(game.Tick));
            Assert.That(states.Count, Is.EqualTo(raised.Tick), "One state for every tick before the end, none for the end.");
            // The client's run comes next on the same game; the buffer must not hear its ticks.
            buffer.Detach();
            activeBuffer = null;
            end = raised;
            return states.ToArray();
        }

        // The client's run with the server's states arriving `latency` ticks late, and the server's end right after the state
        // for the tick before it, as on the ordered channel. Steps until the end has been taken; once the client's own run has
        // ended a step runs no tick. Returns what OnServerEnd returned.
        private bool RunClientToEnd(InputLog log, byte[][] server, ServerEnd end, int latency, out ClientPredictor predictor)
        {
            predictor = StartClient(log);
            return StepClientToEnd(predictor, server, end, latency, null);
        }

        // A fresh client on `log` (played in order, one input per live tick) at tick 0, its state for tick 0 taken. `sent`, if
        // given, gets the tick of every control the client sends (InputConsumed), in order.
        private ClientPredictor StartClient(InputLog log, int catchUpLimit = int.MaxValue, List<int> sent = null)
        {
            activePredictor?.Detach();
            var predictor = new ClientPredictor(game, new InputReplay(log), catchUpLimit);
            activePredictor = predictor;
            if (sent != null) predictor.InputConsumed += (tick, _) => sent.Add(tick);
            Time.timeScale = 0f;
            game.SetInputSource(predictor);
            game.StartRun();
            predictor.AfterTick();
            return predictor;
        }

        // The rest of RunClientToEnd: one step is one live tick (none once the client's run has ended), then whatever arrives.
        // `onState`, if given, is called after each server state has been taken, with its tick.
        private bool StepClientToEnd(ClientPredictor predictor, byte[][] server, ServerEnd end, int latency, Action<int> onState)
        {
            bool corrected = false;
            for (int step = 1; !predictor.ServerEnded; step++)
            {
                Assert.That(step, Is.LessThanOrEqualTo(end.Tick + latency), "The end should have been delivered by now.");
                game.StepTick(predictor);
                int arrived = step - latency;
                if (arrived >= 0 && arrived < end.Tick)
                {
                    predictor.OnServerState(server[arrived]);
                    onState?.Invoke(arrived);
                }
                else if (arrived == end.Tick) corrected = predictor.OnServerEnd(end.Tick, end.Reason, end.Input);
            }
            return corrected;
        }

        // Where the run stands once it has ended: the tick, why, the runner's position and the lap counter's progress (a
        // finished run has no snapshot; these are what its last tick moved).
        private (int Tick, bool Failed, bool Finished, float X, float Y, float Z, double Progress) EndedAt()
        {
            Assert.That(game.IsRunning, Is.False, "The run must have ended.");
            Vector3 p = player.transform.position;
            return (game.Tick, game.HasFailed, game.HasFinished, p.x, p.y, p.z, circuit.Laps.Progress);
        }

        // The ticks from .. to, in order.
        private static List<int> TickRange(int from, int to)
        {
            var ticks = new List<int>(Math.Max(0, to - from + 1));
            for (int t = from; t <= to; t++) ticks.Add(t);
            return ticks;
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
