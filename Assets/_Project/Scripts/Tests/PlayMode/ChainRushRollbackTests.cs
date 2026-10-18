using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Race;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    // Rollback safety (DESIGN.md P3): a snapshot of one tick, restored and run again from the same input, gives
    // the same ticks bit for bit, and it can be done many times inside one frame. The game is stepped by hand
    // (Time.timeScale = 0, ChainRushGame.StepTick), so a snapshot is taken at an exact tick and a rollback does
    // not wait for the engine. Same machine and same editor build only, like the determinism tests.
    public sealed class ChainRushRollbackTests
    {
        private const string CircuitScene = "Assets/_Project/Scenes/ChainRushCircuit.unity";
        private const string TestBenchScene = "Assets/_Project/Scenes/ChainRushTestBench.unity";
        private const int BotTicks = 1700;

        private ChainRushGame game;
        private RunnerMotor player;
        private GrappleController grapple;
        private CircuitRace circuit;
        private float timeScale;

        private struct Mark
        {
            public string Name;
            public int Tick;
            public SimSnapshot Snapshot;
        }

        // Plays a log back starting at an arbitrary tick: the input a restored game needs next.
        private sealed class OffsetReplay : IInputSource
        {
            private readonly InputLog log;
            private int position;

            public OffsetReplay(InputLog log, int start)
            {
                this.log = log;
                position = start;
            }

            public void Poll() { }
            public TickInput Consume() => position < log.Count ? log[position++] : default;
            public void Clear() { }
        }

        // Fires the slingshot on the first straight, then hooks the next corner pole from inside its arc (the rope then
        // runs along the centerline), shortens the rope for its first SwingRopeTicks, holds through the arc and lets go
        // back on the straight, so the sling, swing, rope and carry states are all visited.
        private sealed class SwingBot : IInputSource
        {
            private const int SwingRopeTicks = 20;
            private readonly ChainRushGame game;
            private readonly RunnerMotor player;
            private readonly CircuitBot driver;
            private int ticks;
            private bool slung;
            private bool swung;
            private bool sawArc;

            // The circuit bot does the steering and the jumps and grapples over the gaps; this adds the chain action and
            // the swing key, and works the rope while swinging.
            public SwingBot(ChainRushGame game, RunnerMotor player, GrappleController grapple, CircuitRace circuit)
            {
                this.game = game;
                this.player = player;
                driver = new CircuitBot(game, player, grapple, circuit);
            }

            public void Poll() { }
            public void Clear() { }

            public TickInput Consume()
            {
                ticks++;
                TickInput driven = driver.Consume();
                float steer = driven.Steer;
                float curvature = game.Track.Frame(player.transform.position).Curvature;
                RacerState racer = game.Racer;
                if (racer.SlingTicks > 0) slung = true;
                bool swinging = player.IsSwinging;
                if (swinging) swung = true;
                if (swinging && curvature != 0f) sawArc = true;
                // First the slingshot: on a straight, every few ticks until it fires. Then the pole swing: press inside the
                // first arc whose pole is in reach, hold through it and let go back on the straight.
                bool chain = !slung && curvature == 0f && ticks % 10 == 0;
                bool press = slung && !swung && curvature != 0f && player.TryFindPole(out _);
                bool hold = swinging ? !(sawArc && curvature == 0f) : press;
                if (swinging)
                {
                    // While swinging steering works the rope: the key toward the pole shortens it, for a while, then none.
                    float h = player.Heading * Mathf.Deg2Rad;
                    var right = new Vector3(Mathf.Cos(h), 0f, -Mathf.Sin(h));
                    float poleSide = Vector3.Dot(racer.SwingCenter - player.transform.position, right) >= 0f ? 1f : -1f;
                    steer = racer.SwingTicks <= SwingRopeTicks ? poleSide : 0f;
                }
                return new TickInput(steer, driven.PrimaryPressed, false, false, hold, chain, press);
            }
        }

        // The circuit bot's steering without its jumps and grapples, so the runner falls into the first gap it meets.
        private sealed class NoPressBot : IInputSource
        {
            private readonly CircuitBot driver;

            public NoPressBot(ChainRushGame game, RunnerMotor player, GrappleController grapple, CircuitRace circuit)
            {
                driver = new CircuitBot(game, player, grapple, circuit);
            }

            public void Poll() { }
            public void Clear() { }
            public TickInput Consume() => new TickInput(driver.Consume().Steer, false, false, false);
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
            Time.timeScale = timeScale;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // ---- the proof -----------------------------------------------------------------------------

        [UnityTest, Timeout(120000)]
        public IEnumerator Rollback_FromGroundAirAndGrapple_ReplaysEveryTickBitForBit()
        {
            yield return Load(CircuitScene);
            var log = new InputLog();
            var marks = new List<Mark>();
            StateTrace original = RecordBot(log, BotTicks, marks);

            Assert.That(marks.Count, Is.EqualTo(3), "The bot's run must visit the ground, the air and the grapple.");
            foreach (Mark mark in marks)
            {
                const int count = 60;
                string difference = Replay(original, log, mark, count);
                Assert.That(difference, Is.Null, $"snapshot '{mark.Name}' at tick {mark.Tick}: {difference}");
                Assert.That(game.Tick, Is.EqualTo(mark.Tick + count));
            }

            // From the very first ground snapshot through everything the bot does after it: the jump gap, the grapple.
            Mark ground = marks[0];
            int rest = original.Samples - ground.Tick;
            string longRun = Replay(original, log, ground, rest);
            Assert.That(longRun, Is.Null, $"{rest} ticks from the ground snapshot at tick {ground.Tick}: {longRun}");
            Debug.Log($"ChainRush rollback: ground@{marks[0].Tick}, air@{marks[1].Tick}, grapple@{marks[2].Tick} each replay 60 ticks identical; {rest} ticks from the ground snapshot identical.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Rollback_ManyTimesInOneFrame_AlwaysGivesTheSameTicks()
        {
            yield return Load(CircuitScene);
            var log = new InputLog();
            var marks = new List<Mark>();
            StateTrace original = RecordBot(log, BotTicks, marks);

            int frame = Time.frameCount;
            int rollbacks = 0;
            for (int i = 0; i < 20; i++)
            {
                Mark mark = marks[i % marks.Count];
                string difference = Replay(original, log, mark, 40);
                Assert.That(difference, Is.Null, $"rollback {i} to '{mark.Name}' (tick {mark.Tick}): {difference}");
                rollbacks++;
            }
            Assert.That(Time.frameCount, Is.EqualTo(frame), "All the rollbacks must happen inside one frame.");
            Debug.Log($"ChainRush rollback: {rollbacks} rollbacks of 40 ticks in frame {frame}, all identical.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Rollback_FromALaterStateToAnEarlierSnapshot_ReplaysTheOriginalRun()
        {
            yield return Load(CircuitScene);
            var log = new InputLog();
            var marks = new List<Mark>();
            StateTrace original = RecordBot(log, BotTicks, marks);
            Mark late = marks[2];
            Assert.That(late.Tick, Is.GreaterThan(marks[0].Tick));

            // The game is now at the end of the recording; go back to the late snapshot, then to the early one.
            string toLate = Replay(original, log, late, 100);
            Assert.That(toLate, Is.Null, toLate);
            string toEarly = Replay(original, log, marks[0], 100);
            Assert.That(toEarly, Is.Null, toEarly);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Rollback_AcrossTheLapSeam_ReplaysEveryTickAndTheLapTimes()
        {
            yield return Load(CircuitScene);
            var log = new InputLog();
            StateTrace original = CircuitTrace(new InputRecorder(new CircuitBot(game, player, grapple, circuit), log));
            Time.timeScale = 0f;
            game.StartRun();
            var recent = new Queue<Mark>();
            int seamTick = -1;
            while (game.IsRunning && game.Tick < 12000)
            {
                if (seamTick < 0)
                {
                    recent.Enqueue(Take("before the seam"));
                    if (recent.Count > 80) recent.Dequeue();
                }
                game.StepTick(original);
                if (seamTick < 0 && circuit.Laps.CompletedLaps >= 1) seamTick = game.Tick;
                if (seamTick >= 0 && game.Tick >= seamTick + 100) break;
            }
            Assert.That(seamTick, Is.GreaterThan(0), $"The bot did not complete a lap in {game.Tick} ticks (running {game.IsRunning}, lap progress {circuit.Laps.Progress:F1} m).");
            int firstLapTicks = circuit.Laps.LapTicks(1);
            int checkpointTick = circuit.Laps.CheckpointTick(1, 0);
            Mark mark = recent.Peek();
            Assert.That(seamTick - mark.Tick, Is.InRange(60, 80), "The snapshot should be about 80 ticks before the seam.");

            int count = Math.Min(180, original.Samples - mark.Tick);
            Assert.That(count, Is.GreaterThan(150));
            string difference = Replay(original, log, mark, count);
            Assert.That(difference, Is.Null, difference);
            Assert.That(circuit.Laps.CompletedLaps, Is.GreaterThanOrEqualTo(1), "The replay must cross the seam too.");
            Assert.That(circuit.Laps.LapTicks(1), Is.EqualTo(firstLapTicks));
            Assert.That(circuit.Laps.CheckpointTick(1, 0), Is.EqualTo(checkpointTick));
            Debug.Log($"ChainRush rollback: seam at tick {seamTick}, snapshot at tick {mark.Tick}, {count} ticks identical, lap 1 took {firstLapTicks} ticks.");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Rollback_DuringSlingCornerSwingRopeAndCarry_ReplaysEveryTickBitForBit()
        {
            yield return Load(CircuitScene);
            var log = new InputLog();
            var marks = new List<Mark>();
            StateTrace original = CircuitTrace(new InputRecorder(new SwingBot(game, player, grapple, circuit), log));
            Time.timeScale = 0f;
            game.StartRun();
            // Two chain slots up front (as the chain tests do): the slingshot comes before the first swing fills the gauge.
            // The snapshots are all taken after this, so the replays carry the gauge in the snapshot.
            game.Racer.AddGauge(2f);
            bool sling = false, swing = false, rope = false, carry = false;
            while (game.IsRunning && game.Tick < 5000 && !(sling && swing && rope && carry))
            {
                RacerState racer = game.Racer;
                if (!sling && racer.SlingTicks > 0 && racer.SwingTicks == 0) { sling = true; marks.Add(Take("sling")); }
                else if (!swing && racer.SwingTicks > 0) { swing = true; marks.Add(Take("swing")); }
                else if (!rope && racer.SwingTicks > 0 && racer.SwingRadius < racer.SwingHookRadius - 1f) { rope = true; marks.Add(Take("rope")); }
                else if (!carry && racer.SwingTicks == 0 && racer.SwingCarrySpeed > 0f) { carry = true; marks.Add(Take("carry")); }
                game.StepTick(original);
            }
            // Run a little on so every snapshot has ticks after it to replay.
            for (int i = 0; i < 120 && game.IsRunning; i++) game.StepTick(original);
            string where = $"tick {game.Tick}, running {game.IsRunning}, failed {game.HasFailed}, S {game.Track.Project(player.transform.position).S:F1}, " +
                           $"curvature {game.Track.Frame(player.transform.position).Curvature:F4}, gauge {game.Racer.Gauge:F2}, rope {game.Racer.SwingRadius:F2}/{game.Racer.SwingHookRadius:F2}";
            Assert.That(sling, Is.True, $"The swing bot never fired the slingshot ({where}).");
            Assert.That(swing, Is.True, $"The swing bot never hooked a corner swing ({where}).");
            Assert.That(rope, Is.True, $"The swing bot never shortened the rope by a metre ({where}).");
            Assert.That(carry, Is.True, $"The swing bot never let go with speed to carry ({where}).");

            foreach (Mark mark in marks)
            {
                int count = Math.Min(80, original.Samples - mark.Tick);
                Assert.That(count, Is.GreaterThan(30), $"'{mark.Name}' at tick {mark.Tick} leaves too few ticks to replay.");
                string difference = Replay(original, log, mark, count);
                Assert.That(difference, Is.Null, $"snapshot '{mark.Name}' at tick {mark.Tick}: {difference}");
            }
            Debug.Log("ChainRush rollback: sling, corner swing, rope and carry snapshots " + string.Join(", ", marks.ConvertAll(m => m.Name + "@" + m.Tick)) + " replay identically.");
        }

        // A fall in a circuit race puts the runner back on the track and holds it (RunnerMotor.RespawnOnTrack). Snapshots
        // taken while falling and in the middle of the hold replay every tick bit for bit, through the respawn, the rest of
        // the hold and the running on after it (where the bot, still pressing nothing, may fall again).
        [UnityTest, Timeout(120000)]
        public IEnumerator Rollback_AroundAFallAndTheRespawnHold_ReplaysEveryTickBitForBit()
        {
            yield return Load(CircuitScene);
            var log = new InputLog();
            var marks = new List<Mark>();
            StateTrace original = CircuitTrace(new InputRecorder(new NoPressBot(game, player, grapple, circuit), log));
            Time.timeScale = 0f;
            game.StartRun();
            const int heldFor = 20;
            const int after = 200;
            int respawnTick = -1;
            while (game.IsRunning && game.Tick < 3000)
            {
                if (respawnTick < 0 && marks.Count == 0 && game.Tick > 50 && game.Track.Project(player.transform.position).H < -3f) marks.Add(Take("falling"));
                game.StepTick(original);
                if (respawnTick < 0 && player.IsRespawning) respawnTick = game.Tick;
                if (respawnTick > 0 && game.Tick == respawnTick + heldFor) marks.Add(Take("held"));
                if (respawnTick > 0 && game.Tick >= respawnTick + after) break;
            }
            Assert.That(respawnTick, Is.GreaterThan(0), $"The bot pressing nothing never fell (tick {game.Tick}, running {game.IsRunning}, failed {game.HasFailed}).");
            Assert.That(game.IsRunning, Is.True, "A fall must not end a circuit run.");
            Assert.That(marks.Count, Is.EqualTo(2), "The run must be snapshotted while falling and during the hold.");
            Assert.That(marks[0].Tick, Is.LessThan(respawnTick));
            Assert.That(marks[1].Snapshot.Racer.RespawnHoldTicks, Is.EqualTo(Ticks.FromSeconds(1f) - heldFor), "The held snapshot is in the middle of the hold.");

            foreach (Mark mark in marks)
            {
                int count = original.Samples - mark.Tick;
                string difference = Replay(original, log, mark, count);
                Assert.That(difference, Is.Null, $"snapshot '{mark.Name}' at tick {mark.Tick}: {difference}");
                Assert.That(game.Tick, Is.EqualTo(mark.Tick + count));
            }
            Debug.Log($"ChainRush rollback: respawn at tick {respawnTick}; falling@{marks[0].Tick} and held@{marks[1].Tick} replay to tick {respawnTick + after} identically.");
        }

        // ---- the manual step is the same simulation --------------------------------------------------

        [UnityTest, Timeout(240000)]
        public IEnumerator StepTick_ByHand_IsTheSameRunAsFixedUpdate()
        {
            yield return Load(CircuitScene);
            var log = new InputLog();
            StateTrace byHand = RecordBot(log, BotTicks, new List<Mark>());

            StateTrace live = CircuitTrace(new InputReplay(log));
            game.SetInputSource(live);
            Time.timeScale = 3f;
            game.StartRun();
            float deadline = Time.realtimeSinceStartup + 120f;
            while (game.IsRunning && game.Tick < BotTicks && Time.realtimeSinceStartup < deadline) yield return null;
            Time.timeScale = timeScale;
            Assert.That(game.Tick, Is.GreaterThanOrEqualTo(BotTicks));
            string difference = StateTrace.Compare(byHand, live, BotTicks);
            Assert.That(difference, Is.Null, "Stepping by hand and FixedUpdate must give the same ticks: " + difference);
        }

        // ---- the comparison can tell ----------------------------------------------------------------

        [UnityTest, Timeout(120000)]
        public IEnumerator Rollback_WithTheGroundedAnswerLeftWrong_Diverges()
        {
            yield return Load(CircuitScene);
            var log = new InputLog();
            var marks = new List<Mark>();
            StateTrace original = RecordBot(log, BotTicks, marks);
            Mark air = marks[1];
            Assert.That(air.Snapshot.Racer.Grounded, Is.False, "The air snapshot must have been taken in the air.");

            // The same rollback with the grounded answer flipped after the restore: the replay must go wrong,
            // or the grounded answer would not need to be part of the snapshot.
            string difference = Replay(original, log, air, 30, () => game.Racer.Grounded = true);
            Assert.That(difference, Is.Not.Null, "Flipping Grounded after the restore changed nothing: the comparison cannot tell.");
            Debug.Log("ChainRush rollback control (Grounded flipped after restore): " + difference);

            // And the honest rollback is still fine.
            Assert.That(Replay(original, log, air, 30), Is.Null);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Snapshot_OutsideACircuitRaceOrRun_ThrowsLoudly()
        {
            yield return Load(CircuitScene);
            Assert.Throws<InvalidOperationException>(() => game.CaptureSnapshot(), "A snapshot before the run starts has no tick to hold.");
            // Outside a circuit race: the test bench (the endless course is on hold, 2026-10-18).
            yield return Load(TestBenchScene);
            Time.timeScale = 0f;
            game.StartRun();
            Assert.Throws<NotSupportedException>(() => game.CaptureSnapshot());
            Assert.Throws<NotSupportedException>(() => game.RestoreSnapshot(default));
        }

        // ---- helpers -------------------------------------------------------------------------------

        private IEnumerator Load(string path)
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("Rollback tests require the Unity Editor.");
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

        private StateTrace CircuitTrace(IInputSource inner) => new StateTrace(game, player, inner,
            ("lapProgress", () => circuit.Laps.Progress),
            ("checkpoints", () => circuit.Laps.CheckpointsPassed),
            ("laps", () => circuit.Laps.CompletedLaps),
            ("lastGroundedS", () => game.Racer.LastGroundedS),
            ("respawnHold", () => game.Racer.RespawnHoldTicks));

        private Mark Take(string name) => new Mark { Name = name, Tick = game.Tick, Snapshot = game.CaptureSnapshot() };

        // Runs the circuit bot by hand for `ticks` ticks, recording its input in `log`, and takes a snapshot on the
        // ground (tick 300), in the air (the first tick the runner is airborne and not hooked) and hooked to a grapple anchor.
        private StateTrace RecordBot(InputLog log, int ticks, List<Mark> marks)
        {
            StateTrace trace = CircuitTrace(new InputRecorder(new CircuitBot(game, player, grapple, circuit), log));
            Time.timeScale = 0f;
            game.StartRun();
            bool ground = false, air = false, hooked = false;
            while (game.IsRunning && game.Tick < ticks)
            {
                RacerState racer = game.Racer;
                if (!ground && game.Tick == 300)
                {
                    ground = true;
                    Assert.That(racer.Grounded, Is.True, "Tick 300 should be on the ground.");
                    marks.Add(Take("ground"));
                }
                else if (!air && game.Tick > 100 && !racer.Grounded && !grapple.IsAttached)
                {
                    air = true;
                    marks.Add(Take("air"));
                }
                else if (!hooked && grapple.IsAttached)
                {
                    hooked = true;
                    marks.Add(Take("grapple"));
                }
                game.StepTick(trace);
            }
            Assert.That(game.IsRunning, Is.True, $"The bot's run ended at tick {game.Tick} (health {game.Health}, failed {game.HasFailed}).");
            Assert.That(trace.Samples, Is.GreaterThanOrEqualTo(ticks));
            return trace;
        }

        // Restores the snapshot and plays the log on from its tick for `count` ticks. Returns null when every
        // sample matches the original run's samples from that tick, else what differs first.
        private string Replay(StateTrace original, InputLog log, Mark mark, int count, Action afterRestore = null)
        {
            Assert.That(mark.Tick + count, Is.LessThanOrEqualTo(original.Samples), "Not enough recorded ticks after the snapshot.");
            game.RestoreSnapshot(mark.Snapshot);
            Assert.That(game.Tick, Is.EqualTo(mark.Tick));
            afterRestore?.Invoke();
            StateTrace replay = CircuitTrace(new OffsetReplay(log, mark.Tick));
            for (int i = 0; i < count && game.IsRunning; i++) game.StepTick(replay);
            return StateTrace.Compare(original, mark.Tick, replay, 0, count);
        }
    }
}
