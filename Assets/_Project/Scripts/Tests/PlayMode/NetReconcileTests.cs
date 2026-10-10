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
            Assert.That((back.PrimaryPressed, back.ReleasePressed, back.AttackPressed, back.Drift, back.ChainActionPressed), Is.EqualTo((true, false, true, true, false)));
            bytes[bytes.Length - 1] = 0xFF;
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeInput(bytes, out _), "unknown flag bits");
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
                Assert.That((back.PrimaryPressed, back.ReleasePressed, back.AttackPressed, back.Drift, back.ChainActionPressed), Is.EqualTo((false, true, false, true, true)));
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

        // ---- the end of the server's run ----------------------------------------------------------------

        // The server missed a press (a jump or a grapple), so its run falls on a tick where the client's run, on the controls
        // the client really typed, goes on. The client's prediction is corrected once by the server's states, and then the
        // server's end arrives: the client's run must have ended on the same tick for the same reason. The correction comes
        // from the states, not from the end: by the time the end arrives the state for the tick before it has been compared,
        // so the client already plays the server's run, which falls on its own (OnServerEnd returns false).
        [UnityTest, Timeout(300000)]
        public IEnumerator ClientEnd_WhenTheServersRunFellAfterAMissedInput_CorrectsOnceAndFailsOnTheSameTick()
        {
            yield return Load();
            InputLog log = RecordBot(BotTicks);
            byte[][] truth = RunTruth(log, BotTicks);
            int dropTick = FindFallingDrop(log, truth, out int fallTick);
            byte[][] server = RunServerToEnd(log, BotTicks, dropTick, out ServerEnd end);
            Assert.That(end.Tick, Is.EqualTo(fallTick), "The buffer's run must fall where repeating the input by hand did.");
            Assert.That(end.Reason, Is.EqualTo(RaceWire.EndReason.Failed));
            Assert.That(end.Input.Steer, Is.EqualTo(log[end.Tick - 1].Steer), "The end carries the controls the server ran its last tick on.");
            Assert.That(TickInputCodec.ToFlags(end.Input), Is.EqualTo(TickInputCodec.ToFlags(log[end.Tick - 1])));
            // The server's states end before its fall, so they go first: FirstDifference walks the first one.
            int diverge = FirstDifference(server, truth);
            Assert.That(diverge, Is.EqualTo(dropTick), "The missed press must change the server's state on that very tick.");

            foreach (int latency in new[] { 5, 15, 40 })
            {
                bool corrected = RunClientToEnd(log, server, end, latency, out ClientPredictor predictor);
                Assert.That(predictor.Corrections, Is.EqualTo(1), $"latency {latency}: one missed input is one correction.");
                Assert.That(predictor.FirstMismatchTick, Is.EqualTo(diverge), $"latency {latency}");
                Assert.That(corrected, Is.False, $"latency {latency}: the states already put the client on the server's run, which falls by itself.");
                Assert.That(predictor.ServerEnded, Is.True, $"latency {latency}");
                Assert.That((predictor.EndTick, predictor.EndReason), Is.EqualTo((end.Tick, RaceWire.EndReason.Failed)), $"latency {latency}");
                Assert.That(game.HasFailed, Is.True, $"latency {latency}: the client's run must have fallen.");
                Assert.That(game.Tick, Is.EqualTo(end.Tick), $"latency {latency}: the client's run must end on the server's tick.");
                Assert.That(predictor.StatesCompared, Is.EqualTo(end.Tick), $"latency {latency}: every state the server sent (ticks 0..{end.Tick - 1}) is compared.");
                Debug.Log($"Net end: missed press at tick {dropTick}, server fell at tick {end.Tick}; latency {latency} ticks: client corrected at tick {diverge + latency}, failed at tick {game.Tick}.");
            }
        }

        // The server missed the input of the very tick its run ended on, and ran that tick on the tick before's controls,
        // while the client's own controls for it keep the client's run going. Every state the server sent agrees with the
        // prediction (the inputs before were all the same), so it is the end that corrects: the client goes back to the
        // state before that tick, runs it on the controls the end carries, and ends on the same tick for the same reason
        // (OnServerEnd returns true). The end must carry the repeated controls, not the ones the client typed.
        // The finish, not a fall: a missed press makes the run fall only long after (ticks into the gap, RunnerMotor's fall
        // check at H < -12), where no input of that tick holds the runner up (a PlayMode run on 2026-10-10 found every one of
        // the 96 inputs still falling, for each of the three falling presses). A finish is the lap counter's progress reaching
        // the race length on the tick, so a run that crosses the line by less than one tick's controls can change stays short
        // of it on the client's controls; FindFinishReplayCase looks for such a run.
        [UnityTest, Timeout(300000)]
        public IEnumerator ClientEnd_WhenTheServerMissedTheInputOfItsLastTick_ReplaysThatTickOnTheServersControlsAndEndsTheSame()
        {
            yield return Load();
            InputLog log = RecordBotToFinish(RaceTicksLimit);
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
            Assert.That(predictor.Faulted, Is.True, "Only Clear lifts the fault.");
            Assert.That(predictor.ServerEnded, Is.False);

            predictor.Clear();
            Assert.That(predictor.Faulted, Is.False, "Clear lifts the fault.");
            Assert.That(predictor.FaultReason, Is.Null);
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

        // A tick whose input, when the server misses it, makes the server's run fall: a press of the primary control (a jump
        // or a grapple) that the repeated input of the tick before loses. Each press is tried from the truth's state before it.
        private int FindFallingDrop(InputLog log, byte[][] truth, out int fallTick)
        {
            Time.timeScale = 0f;
            var tried = new System.Text.StringBuilder();
            for (int d = 3; d < BotTicks; d++)
            {
                if (!log[d - 1].PrimaryPressed || log[d - 2].PrimaryPressed) continue;
                var source = new TickReplay(game, log, d);
                game.RestoreSnapshot(SimSnapshotCodec.Decode(truth[d - 1]));
                while (game.IsRunning && game.Tick < BotTicks) game.StepTick(source);
                tried.Append($" {d}:{(game.HasFailed ? "fell" : "ran")}@{game.Tick}");
                if (game.HasFailed)
                {
                    fallTick = game.Tick;
                    return d;
                }
            }
            Assert.Fail($"No single missed press makes the run fall within {BotTicks} ticks. Presses tried (tick:outcome@last tick):{tried}");
            fallTick = -1;
            return -1;
        }

        // Where the variants start, in ticks before the recorded finish, and the most ticks a variant flips drift on. The search
        // is bounded by this fixed set of variants, not by the clock, so its outcome does not depend on the machine; the
        // test's Timeout guards the wall clock. Each variant replays only the last few hundred ticks of the race.
        private static readonly int[] FinishVariantLeads = { 150, 300, 450, 600 };
        private const int FinishVariantMaxDrift = 40;
        // The recorded run itself (j = 0, the same for every lead) once, and j = 1 .. FinishVariantMaxDrift for each lead: 161.
        private static int FinishVariantCount => 1 + FinishVariantLeads.Length * FinishVariantMaxDrift;

        // A finish the client has to be corrected to. The run finishes on the tick the lap counter's progress reaches the
        // race length (LapCounter.Update, CircuitRace.Step, ChainRushGame.RunTick), by however far it passed the line on that
        // tick. One tick's controls change how far the runner moves on that tick only a little (drift on the ground lowers
        // the run target by a tenth, and the speed moves toward it by at most 0.6 m/s in a tick: about 0.012 m; steer turns
        // the facing by under half a degree), so the client stays short of the line only when that margin is smaller.
        // Variants of the recorded run move the margin: drift flipped on the j ticks after tick (finish - lead), for j = 0 ..
        // FinishVariantMaxDrift and each lead of FinishVariantLeads, slows (or speeds) the runner for a moment and so shifts
        // where it is on every later tick. For a variant that finishes on tick T, from its state after T - 1: one tick on its
        // own input for T - 1 (what a server that missed T's input repeats) must finish, and one tick on some input of
        // EndCandidates must not end the run. Deterministic: leads, then j, then candidates in order; the first fit is taken.
        // Returns the client's log (the variant, idle input past its end as InputReplay gives, that input on T); T and a
        // description come out. Fails with every variant tried (finish tick, the server's margin past the line, the least
        // margin any candidate left) when none of the FinishVariantCount variants fits.
        private InputLog FindFinishReplayCase(InputLog log, int recordedFinish, out int endTick, out string found)
        {
            Time.timeScale = 0f;
            // Measured for the log line only; never part of the judgement.
            var watch = System.Diagnostics.Stopwatch.StartNew();
            TickInput[] candidates = EndCandidates();
            var tried = new System.Text.StringBuilder();

            // The recorded run's state on each lead's start tick.
            var starts = new byte[FinishVariantLeads.Length][];
            var replay = new InputReplay(log);
            game.SetInputSource(replay);
            game.StartRun();
            while (game.IsRunning && game.Tick < RaceTicksLimit)
            {
                for (int i = 0; i < FinishVariantLeads.Length; i++)
                    if (game.Tick == recordedFinish - FinishVariantLeads[i]) starts[i] = SimSnapshotCodec.Encode(game.CaptureSnapshot());
                game.StepTick(replay);
            }
            Assert.That((game.HasFinished, game.Tick), Is.EqualTo((true, recordedFinish)), "Replaying the recorded log must finish where the recording did.");

            int variants = 0;
            for (int i = 0; i < FinishVariantLeads.Length; i++)
            {
                int lead = FinishVariantLeads[i];
                int startTick = recordedFinish - lead;
                Assert.That(starts[i], Is.Not.Null, $"The recorded run has no tick {startTick} ({lead} before its finish at {recordedFinish}).");
                // j = 0 is the recorded run itself, the same for every lead.
                for (int j = i == 0 ? 0 : 1; j <= FinishVariantMaxDrift; j++)
                {
                    variants++;
                    string name = $"{lead}+{j}";
                    var source = new LogAtTick(game, j == 0 ? log : WithDriftFlipped(log, startTick + 1, j));
                    game.RestoreSnapshot(SimSnapshotCodec.Decode(starts[i]));
                    while (game.IsRunning && game.Tick < RaceTicksLimit) game.StepTick(source);
                    if (!game.HasFinished)
                    {
                        tried.Append($" {name}:{(game.HasFailed ? "fell" : "ran")}@{game.Tick}");
                        continue;
                    }
                    int finish = game.Tick;
                    // The state the finishing tick starts from: the same variant again, stopped one tick short.
                    game.RestoreSnapshot(SimSnapshotCodec.Decode(starts[i]));
                    while (game.IsRunning && game.Tick < finish - 1) game.StepTick(source);
                    Assert.That((game.IsRunning, game.Tick), Is.EqualTo((true, finish - 1)), $"variant {name}: running it again did not reach tick {finish - 1}.");
                    byte[] before = SimSnapshotCodec.Encode(game.CaptureSnapshot());
                    TickInput repeated = source.InputFor(finish - 1);
                    bool serverEnds = EndsOnNextTick(before, repeated, out RaceWire.EndReason serverReason, out double serverPast);
                    if (!serverEnds || serverReason != RaceWire.EndReason.Finished)
                    {
                        tried.Append($" {name}:fin@{finish},repeat-{(serverEnds ? serverReason.ToString() : "runs-on")}");
                        continue;
                    }
                    double least = double.PositiveInfinity;
                    for (int c = 0; c < candidates.Length; c++)
                    {
                        if (SameInput(candidates[c], repeated)) continue;
                        bool ends = EndsOnNextTick(before, candidates[c], out _, out double past);
                        if (past < least) least = past;
                        if (ends) continue;
                        endTick = finish;
                        found = $"variant {name} (drift flipped on {j} ticks from tick {startTick + 1}) finishes on tick {finish}, {serverPast:F5} m past the line on the repeated input; candidate {c} leaves the client {-past:F5} m short of it (search {watch.Elapsed.TotalSeconds:F1} s)";
                        return WithInputAt(source, finish, candidates[c]);
                    }
                    tried.Append($" {name}:fin@{finish},A+{serverPast:F4},C+{least:F4}");
                }
            }
            Assert.That(variants, Is.EqualTo(FinishVariantCount), "The search must try exactly the fixed set of variants.");
            Assert.Fail($"All {variants} variants of the recorded run (finish at tick {recordedFinish}) were tried and none fits: in none does the server's repeated input of the tick before finish the race on the tick while some single client input of that tick keeps the run going.\n" +
                        $"Variants (lead+drift ticks: outcome; fin@T, A+ = metres past the line on the repeated input, C+ = least metres past it any candidate left; search {watch.Elapsed.TotalSeconds:F1} s):{tried}");
            endTick = -1;
            found = null;
            return null;
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

        // The log with drift flipped on `count` ticks from `firstTick` (1-based), everything else kept.
        private static InputLog WithDriftFlipped(InputLog log, int firstTick, int count)
        {
            if (firstTick < 1 || firstTick + count - 1 > log.Count) throw new ArgumentOutOfRangeException(nameof(firstTick), firstTick, $"Ticks {firstTick}..{firstTick + count - 1} are not all in a log of {log.Count}.");
            var copy = new InputLog();
            for (int t = 1; t <= log.Count; t++)
            {
                TickInput x = log[t - 1];
                copy.Add(t >= firstTick && t < firstTick + count
                    ? new TickInput(x.Steer, x.PrimaryPressed, x.ReleasePressed, x.AttackPressed, !x.Drift, x.ChainActionPressed)
                    : x);
            }
            return copy;
        }

        // What `source` plays, as a log from tick 1, long enough for the client to run past `tick` (idle past the source's
        // log, as InputReplay gives), with `input` on `tick`.
        private static InputLog WithInputAt(LogAtTick source, int tick, in TickInput input)
        {
            var copy = new InputLog();
            int length = tick + 64;
            for (int t = 1; t <= length; t++) copy.Add(t == tick ? input : source.InputFor(t));
            return copy;
        }

        // Every steer of -1, 0, 1 with every set of the five buttons, presses of the primary control (jump or grapple) first.
        private static TickInput[] EndCandidates()
        {
            var all = new List<TickInput>(96);
            foreach (bool primary in new[] { true, false })
            foreach (bool release in new[] { false, true })
            foreach (bool attack in new[] { false, true })
            foreach (bool drift in new[] { false, true })
            foreach (bool chain in new[] { false, true })
            foreach (float steer in new[] { 0f, -1f, 1f })
                all.Add(new TickInput(steer, primary, release, attack, drift, chain));
            return all.ToArray();
        }

        private static bool SameInput(in TickInput a, in TickInput b) =>
            a.Steer == b.Steer && TickInputCodec.ToFlags(a) == TickInputCodec.ToFlags(b);

        // The server's run to its end: every input but dropTick arrives in time (0 = none dropped). Returns the states it
        // handed out (ticks 0 .. end - 1) and the end it raised.
        private byte[][] RunServerToEnd(InputLog log, int maxTicks, int dropTick, out ServerEnd end)
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
            for (int t = 1; t <= log.Count; t++) if (t != dropTick) buffer.Receive(t, log[t - 1]);
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
            activePredictor?.Detach();
            predictor = new ClientPredictor(game, new InputReplay(log));
            activePredictor = predictor;
            Time.timeScale = 0f;
            game.SetInputSource(predictor);
            game.StartRun();
            predictor.AfterTick();
            bool corrected = false;
            for (int step = 1; !predictor.ServerEnded; step++)
            {
                Assert.That(step, Is.LessThanOrEqualTo(end.Tick + latency), "The end should have been delivered by now.");
                game.StepTick(predictor);
                int arrived = step - latency;
                if (arrived >= 0 && arrived < end.Tick) predictor.OnServerState(server[arrived]);
                else if (arrived == end.Tick) corrected = predictor.OnServerEnd(end.Tick, end.Reason, end.Input);
            }
            return corrected;
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
