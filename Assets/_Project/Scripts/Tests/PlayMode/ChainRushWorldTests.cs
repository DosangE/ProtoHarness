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
    // Racers in isolated worlds (DESIGN.md P3 C1): two copies of the circuit scene, each on its own physics scene,
    // run side by side and each gives exactly the ticks it gives alone. The games are stepped by hand
    // (Time.timeScale = 0, ChainRushGame.StepTick) so both advance in lockstep. Same machine and editor only.
    public sealed class ChainRushWorldTests
    {
        private const string CircuitScene = "Assets/_Project/Scenes/ChainRushCircuit.unity";
        private const int BotTicks = 1700;

        private float timeScale;
        private RaceWorld first;
        private RaceWorld second;

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

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            timeScale = Time.timeScale;
            first = null;
            second = null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Both games are still running when a test ends; an idle source keeps FixedUpdate from stepping a finished test's trace.
            var idle = new InputReplay(new InputLog());
            if (first != null) first.Game.SetInputSource(idle);
            if (second != null) second.Game.SetInputSource(new InputReplay(new InputLog()));
            Time.timeScale = timeScale;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // ---- the worlds are separate ------------------------------------------------------------------

        [UnityTest, Timeout(180000)]
        public IEnumerator TwoIsolatedWorlds_RunSideBySide_EachMatchesItsSoloRun()
        {
            yield return LoadFirst();
            InputLog log = RecordBot(first, BotTicks);
            StateTrace solo = Replay(first, log, BotTicks);

            yield return LoadSecond(RaceWorld.IsolatedLoad);
            Assert.That(second.PhysicsWorld, Is.Not.EqualTo(first.PhysicsWorld), "An isolated copy must have a physics scene of its own.");
            StateTrace a = Trace(first, new InputReplay(log));
            StateTrace b = Trace(second, new InputReplay(log));
            RunTogether(a, b, BotTicks);

            Assert.That(StateTrace.Compare(solo, a, BotTicks), Is.Null, "World 1 with a neighbour must give its solo ticks: " + StateTrace.Compare(solo, a, BotTicks));
            Assert.That(StateTrace.Compare(solo, b, BotTicks), Is.Null, "World 2 must give the same ticks as world 1 alone: " + StateTrace.Compare(solo, b, BotTicks));
            Assert.That(a.Hash(BotTicks), Is.EqualTo(solo.Hash(BotTicks)));
            Debug.Log($"ChainRush worlds: two isolated copies ran {BotTicks} ticks side by side; both equal the solo run (hash 0x{solo.Hash(BotTicks):X16}).");
        }

        // Twins on the same spot cannot touch each other, so the test above shows the worlds run together, not that they
        // are apart. This one stands the second runner on the first one's path, 150 m down the track: the bot runs into it.
        // In an isolated copy the first run must still be its solo run.
        [UnityTest, Timeout(180000)]
        public IEnumerator IsolatedWorlds_ARunnerStandingOnThePath_DoesNotChangeTheOtherRun()
        {
            yield return LoadFirst();
            InputLog log = RecordBot(first, BotTicks);
            StateTrace solo = Replay(first, log, BotTicks);

            yield return LoadSecond(RaceWorld.IsolatedLoad);
            StateTrace a = Trace(first, new InputReplay(log));
            Vector3 stand = RunWithAStander(a, BotTicks);

            Assert.That(first.Game.Track.Project(first.Player.transform.position).S, Is.GreaterThan(200d), "The bot must have run past the spot where the other runner stands.");
            Assert.That(Vector3.Distance(second.Player.transform.position, stand), Is.LessThan(0.01f), "The other runner was not stepped and must still be where it was put.");
            Assert.That(StateTrace.Compare(solo, a, BotTicks), Is.Null, "A runner standing in another world changed this run: " + StateTrace.Compare(solo, a, BotTicks));
        }

        // The control: the same second copy without a physics scene of its own shares the first one's physics world, so the
        // standing runner is in the bot's way and the ticks change. If this did not differ, the test above could not tell a
        // shared world from a separate one.
        [UnityTest, Timeout(180000)]
        public IEnumerator SharedPhysicsWorld_ARunnerStandingOnThePath_ChangesTheOtherRun()
        {
            yield return LoadFirst();
            InputLog log = RecordBot(first, BotTicks);
            StateTrace solo = Replay(first, log, BotTicks);

            yield return LoadSecond(new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.That(second.PhysicsWorld, Is.EqualTo(first.PhysicsWorld), "Without LocalPhysicsMode both copies share the default physics scene.");
            StateTrace a = Trace(first, new InputReplay(log));
            RunWithAStander(a, BotTicks);

            string difference = StateTrace.Compare(solo, a, BotTicks);
            Assert.That(difference, Is.Not.Null, "A runner standing on the path in a shared physics world changed nothing: the control cannot tell the worlds apart.");
            Debug.Log("ChainRush worlds control (one shared physics world, a runner in the way): " + difference);
        }

        // ---- a world can be rolled back ----------------------------------------------------------------

        [UnityTest, Timeout(180000)]
        public IEnumerator Rollback_InAnIsolatedWorld_ReplaysEveryTickBitForBit()
        {
            yield return LoadFirst();
            yield return LoadSecond(RaceWorld.IsolatedLoad);
            RaceWorld world = second;
            var log = new InputLog();
            StateTrace original = new StateTrace(world.Game, world.Player, new InputRecorder(new CircuitBot(world.Game, world.Player, world.Grapple, world.Circuit), log), Extras(world));
            Time.timeScale = 0f;
            world.Game.SetInputSource(original);
            world.Game.StartRun();
            var marks = new List<(string Name, int Tick, SimSnapshot Snapshot)>();
            bool ground = false, air = false, hooked = false;
            while (world.Game.IsRunning && world.Game.Tick < BotTicks)
            {
                RacerState racer = world.Game.Racer;
                if (!ground && world.Game.Tick == 300) { ground = true; marks.Add(("ground", world.Game.Tick, world.Game.CaptureSnapshot())); }
                else if (!air && world.Game.Tick > 100 && !racer.Grounded && !world.Grapple.IsAttached) { air = true; marks.Add(("air", world.Game.Tick, world.Game.CaptureSnapshot())); }
                else if (!hooked && world.Grapple.IsAttached) { hooked = true; marks.Add(("grapple", world.Game.Tick, world.Game.CaptureSnapshot())); }
                world.Game.StepTick(original);
            }
            Assert.That(world.Game.IsRunning, Is.True, $"The bot's run ended at tick {world.Game.Tick}.");
            Assert.That(marks.Count, Is.EqualTo(3), "The bot's run must visit the ground, the air and the grapple.");

            foreach ((string name, int tick, SimSnapshot snapshot) in marks)
            {
                const int count = 60;
                world.Game.RestoreSnapshot(snapshot);
                var replay = new StateTrace(world.Game, world.Player, new OffsetReplay(log, tick), Extras(world));
                for (int i = 0; i < count && world.Game.IsRunning; i++) world.Game.StepTick(replay);
                string difference = StateTrace.Compare(original, tick, replay, 0, count);
                Assert.That(difference, Is.Null, $"snapshot '{name}' at tick {tick} in the isolated world: {difference}");
            }
            Debug.Log("ChainRush worlds: ground, air and grapple snapshots replay 60 ticks identically in an isolated world.");
        }

        // ---- the tick-completed event -----------------------------------------------------------------

        [UnityTest, Timeout(120000)]
        public IEnumerator TickCompleted_FiresOncePerTickThatRan_WithTheStateAfterIt()
        {
            yield return LoadFirst();
            InputLog log = RecordBot(first, 200);
            ChainRushGame game = first.Game;
            var seen = new List<int>();
            var positions = new List<Vector3>();
            game.TickCompleted += tick =>
            {
                seen.Add(tick);
                SimSnapshot inside = game.CaptureSnapshot();
                Assert.That(inside.Tick, Is.EqualTo(tick), "Inside the event the game is between ticks: its tick is the one that just ran.");
                positions.Add(inside.Position);
            };

            var replay = new InputReplay(log);
            game.SetInputSource(replay);
            game.StartRun();
            for (int i = 0; i < 5; i++) game.StepTick(replay);
            Assert.That(seen, Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
            Assert.That(game.CaptureSnapshot().Position, Is.EqualTo(positions[4]), "The state in the event is the state after the call returns.");

            game.TogglePause();
            game.StepTick(replay);
            Assert.That(seen.Count, Is.EqualTo(5), "A tick that did not run (the game is paused) raises nothing.");
            game.TogglePause();
            game.StepTick(replay);
            Assert.That(seen.Count, Is.EqualTo(6));
            Assert.Throws<ArgumentNullException>(() => game.StepTick(null));
        }

        // ---- helpers -------------------------------------------------------------------------------

        private IEnumerator LoadFirst()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(CircuitScene, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("World tests require the Unity Editor.");
            yield break;
#endif
            first = RaceWorld.FromScene(SceneManager.GetActiveScene());
            yield return null;
        }

        private IEnumerator LoadSecond(LoadSceneParameters parameters)
        {
            // Each copy of the scene brings its own camera and AudioListener, and Unity logs every frame that there are two.
            // The first copy's listener is switched off before the second loads (a race world has no use for a listener).
            foreach (GameObject root in first.Scene.GetRootGameObjects())
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(CircuitScene, parameters);
#else
            Assert.Fail("World tests require the Unity Editor.");
            yield break;
#endif
            second = RaceWorld.FromScene(SceneManager.GetSceneAt(SceneManager.sceneCount - 1));
            Assert.That(second.Scene, Is.Not.EqualTo(first.Scene));
            yield return null;
        }

        private static (string Name, Func<double> Read)[] Extras(RaceWorld world) => new (string, Func<double>)[]
        {
            ("lapProgress", () => world.Circuit.Laps.Progress),
            ("checkpoints", () => world.Circuit.Laps.CheckpointsPassed),
            ("laps", () => world.Circuit.Laps.CompletedLaps),
        };

        private static StateTrace Trace(RaceWorld world, IInputSource inner) => new StateTrace(world.Game, world.Player, inner, Extras(world));

        private InputLog RecordBot(RaceWorld world, int ticks)
        {
            var log = new InputLog();
            var recorder = new InputRecorder(new CircuitBot(world.Game, world.Player, world.Grapple, world.Circuit), log);
            Time.timeScale = 0f;
            world.Game.SetInputSource(recorder);
            world.Game.StartRun();
            while (world.Game.IsRunning && world.Game.Tick < ticks) world.Game.StepTick(recorder);
            Assert.That(world.Game.IsRunning, Is.True, $"The bot's run ended at tick {world.Game.Tick}.");
            return log;
        }

        // A run of the log on this world alone.
        private StateTrace Replay(RaceWorld world, InputLog log, int ticks)
        {
            StateTrace trace = Trace(world, new InputReplay(log));
            Time.timeScale = 0f;
            world.Game.SetInputSource(trace);
            world.Game.StartRun();
            for (int t = 0; t < ticks; t++) world.Game.StepTick(trace);
            Assert.That(world.Game.IsRunning, Is.True, $"The replay ended at tick {world.Game.Tick}.");
            return trace;
        }

        // Runs the first world on `a` while the second world's runner is put on the track 150 m from the start and not
        // stepped, so it stays there (a runner given idle controls would run on by itself). Returns where it was put.
        private Vector3 RunWithAStander(StateTrace a, int ticks)
        {
            Time.timeScale = 0f;
            first.Game.SetInputSource(a);
            first.Game.StartRun();
            second.Game.StartRun();
            Vector3 stand = second.Game.Track.FrameAt(150d).Position + Vector3.up * 0.5f;
            second.Player.SetPosition(stand);
            for (int t = 0; t < ticks; t++) first.Game.StepTick(a);
            return stand;
        }

        // Starts both worlds and steps them in lockstep, one tick each in turn.
        private void RunTogether(StateTrace a, StateTrace b, int ticks)
        {
            Time.timeScale = 0f;
            first.Game.SetInputSource(a);
            second.Game.SetInputSource(b);
            first.Game.StartRun();
            second.Game.StartRun();
            for (int t = 0; t < ticks; t++)
            {
                first.Game.StepTick(a);
                second.Game.StepTick(b);
            }
        }
    }
}
