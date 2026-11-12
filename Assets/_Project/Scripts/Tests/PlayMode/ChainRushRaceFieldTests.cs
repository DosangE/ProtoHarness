using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Flow;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Track;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    // A race with opponents (MP1, DECISIONS 2026-11-12 "MP1 구조"): every opponent gets an isolated copy of the race
    // scene, a RaceField steps all the worlds on the same tick, and the player's run is bit for bit the run it is alone.
    // The copies show only their runner. Same machine and editor only.
    public sealed class ChainRushRaceFieldTests
    {
        private const string CircuitScene = "Assets/_Project/Scenes/ChainRushCircuit.unity";
        private const string StadiumPath = "Assets/_Project/Data/Circuit_Stadium.asset";
        private const string ProfilePath = "Assets/_Project/Data/RunnerProfile_{0}.asset";
        private const int BotTicks = 1700;

        private float timeScale;
        private float listenerVolume;
        private BestRecords records;
        private GhostFiles ghosts;
        private RaceAssembler assembler;
        private RaceWorld player;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            timeScale = Time.timeScale;
            listenerVolume = AudioListener.volume;
            AudioListener.volume = 0f;
            RaceLaunch.Clear();
            records = new BestRecords("ChainRush.Test.RaceFieldTests.");
            ghosts = new GhostFiles(Path.Combine(Path.GetTempPath(), "ProtoHarnessTests", "RaceFieldGhosts"));
            assembler = null;
            player = null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // The games may still be running when a test ends; idle sources keep a bot from running on.
            if (player != null) player.Game.SetInputSource(new InputReplay(new InputLog()));
            if (assembler != null && assembler.Field != null)
                foreach (RaceWorld world in assembler.Field.Opponents) world.Game.SetInputSource(new InputReplay(new InputLog()));
            Time.timeScale = timeScale;
            AudioListener.volume = listenerVolume;
            RaceLaunch.Clear();
            if (Directory.Exists(ghosts.Folder)) Directory.Delete(ghosts.Folder, true);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // ---- the setup ---------------------------------------------------------------------------------

        [Test]
        public void RaceSetup_WithOpponents_KeepsThemAndRefusesWhatCannotRace()
        {
            RaceSetup alone = Setup();
            Assert.That(alone.OpponentCount, Is.EqualTo(0));
            Assert.That(alone.IsOpponentWorld, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => alone.Opponent(0));

            RunnerProfile beta = Profile("Beta");
            RunnerProfile theta = Profile("Theta");
            var given = new[] { beta, theta };
            RaceSetup race = alone.WithOpponents(given);
            given[0] = null;
            Assert.That(race.OpponentCount, Is.EqualTo(2));
            Assert.That(race.Opponent(0), Is.SameAs(beta), "The setup keeps its own copy of the list.");
            Assert.That(race.Opponent(1), Is.SameAs(theta));
            Assert.That(race.Runner, Is.SameAs(alone.Runner));
            Assert.That(race.IsOpponentWorld, Is.False);

            RaceSetup world = race.ForOpponent(1);
            Assert.That(world.Runner, Is.SameAs(theta));
            Assert.That(world.Track, Is.SameAs(race.Track));
            Assert.That(world.Speed, Is.EqualTo(race.Speed));
            Assert.That(world.IsOpponentWorld, Is.True);
            Assert.That(world.OpponentCount, Is.EqualTo(0));
            Assert.That(world.EnvironmentScene, Is.Empty);
            Assert.That(world.Look, Is.Null);

            Assert.Throws<ArgumentNullException>(() => alone.WithOpponents(null));
            Assert.Throws<ArgumentException>(() => alone.WithOpponents(beta, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => alone.WithOpponents(new RunnerProfile[RaceSetup.MaxOpponents + 1]));
            Assert.Throws<InvalidOperationException>(() => world.WithOpponents(beta));
            Assert.Throws<InvalidOperationException>(() => default(RaceSetup).WithOpponents(beta));
            var tutorial = new RaceSetup(alone.Track, "TUTORIAL", "", RaceMode.Tutorial, SpeedClass.Fast, alone.Runner);
            Assert.Throws<InvalidOperationException>(() => tutorial.WithOpponents(beta));
        }

        // ---- the worlds --------------------------------------------------------------------------------

        [UnityTest, Timeout(120000)]
        public IEnumerator Race_WithThreeOpponents_LoadsAnIsolatedWorldForEach_ShowingOnlyItsRunner()
        {
            RunnerProfile[] runners = { Profile("Beta"), Profile("Theta"), Profile("Delta") };
            yield return LoadRace(Setup().WithOpponents(runners));
            RaceField field = assembler.Field;
            Assert.That(field, Is.Not.Null, "A race with opponents has a field.");
            Assert.That(field.RacerCount, Is.EqualTo(4));
            Assert.That(field.Opponents.Count, Is.EqualTo(3));
            Assert.That(player.Game.SelfStepping, Is.False, "The field steps the player's game.");
            Assert.That(player.Game.ManualStart, Is.True, "The player still starts the race.");
            Assert.That(player.Game.IsCountingDown, Is.True, "The race counts in once the field is gathered.");
            Assert.That(player.Player.Profile, Is.SameAs(Profile("Alpha")));

            var physics = new List<PhysicsScene> { player.PhysicsWorld };
            for (int i = 0; i < field.Opponents.Count; i++)
            {
                RaceWorld world = field.Opponents[i];
                string which = "opponent " + i;
                Assert.That(world.Scene, Is.Not.EqualTo(player.Scene), which);
                Assert.That(physics, Has.No.Member(world.PhysicsWorld), which + " must have a physics scene of its own.");
                physics.Add(world.PhysicsWorld);
                Assert.That(world.Player.Profile, Is.SameAs(runners[i]), which);
                Assert.That(world.Player.SpeedClassScale, Is.EqualTo(player.Player.SpeedClassScale), which);
                Assert.That(world.Circuit.Definition, Is.SameAs(player.Circuit.Definition), which);
                Assert.That(world.Game.SelfStepping, Is.False, which);
                Assert.That(world.Game.ManualStart, Is.False, which);
                Assert.That(world.Game.IsReady, Is.True, which + " waits on the start line.");
                Assert.That(world.Player.transform.position, Is.EqualTo(player.Player.transform.position), which + " stands where a race starts.");

                RaceAssembler its = Find<RaceAssembler>(world.Scene);
                Assert.That(its.Setup.IsOpponentWorld, Is.True, which);
                Assert.That(its.IsAssembling, Is.False, which);
                Assert.That(its.Field, Is.Null, which);
                Assert.That(its.HasGhost, Is.False, which);
                foreach (Camera camera in All<Camera>(world.Scene)) Assert.That(camera.enabled, Is.False, which + ": " + camera.name);
                foreach (AudioListener listener in All<AudioListener>(world.Scene)) Assert.That(listener.enabled, Is.False, which);
                foreach (Light light in All<Light>(world.Scene)) Assert.That(light.enabled, Is.False, which + ": " + light.name);
                foreach (AudioSource source in All<AudioSource>(world.Scene)) Assert.That(source.mute, Is.True, which + ": " + source.name);
                foreach (ChainRushHud hud in All<ChainRushHud>(world.Scene)) Assert.That(hud.enabled, Is.False, which);
                int shown = 0;
                foreach (Renderer renderer in All<Renderer>(world.Scene))
                {
                    bool runnerPart = renderer.transform.IsChildOf(world.Player.transform);
                    if (!runnerPart) Assert.That(renderer.enabled, Is.False, which + " still draws " + renderer.name);
                    else if (renderer.enabled) shown++;
                }
                Assert.That(shown, Is.GreaterThan(0), which + " must still show its runner.");
                Assert.That(world.Circuit.PieceCount, Is.GreaterThan(0), which + " still has its road to run on.");
            }

            int listeners = 0;
            foreach (AudioListener listener in All<AudioListener>(player.Scene)) if (listener.enabled) listeners++;
            Assert.That(listeners, Is.EqualTo(1), "The player's world keeps the one listener.");
            int roads = 0;
            foreach (Renderer renderer in All<Renderer>(player.Scene))
                if (renderer.enabled && !renderer.transform.IsChildOf(player.Player.transform)) roads++;
            Assert.That(roads, Is.GreaterThan(0), "The player's world draws the road.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Race_Alone_HasNoField_AndTheGameStepsItself()
        {
            yield return LoadRace(Setup());
            Assert.That(assembler.Field, Is.Null);
            Assert.That(player.Game.SelfStepping, Is.True);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
        }

        // ---- the run -----------------------------------------------------------------------------------

        // The opponents are other runners driven by their own bots, in real time, while the player's world replays a
        // recorded run: with them alongside it must give exactly the ticks it gave alone.
        [UnityTest, Timeout(240000)]
        public IEnumerator Race_WithThreeOpponents_ThePlayersRunIsBitForBitItsSoloRun_AndEveryWorldIsOnTheSameTick()
        {
            yield return LoadRace(Setup());
            Time.timeScale = 0f;
            var log = new InputLog();
            var recorder = new InputRecorder(Bot(player), log);
            player.Game.SetInputSource(recorder);
            player.Game.StartRun();
            while (player.Game.IsRunning && player.Game.Tick < BotTicks) player.Game.StepTick(recorder);
            Assert.That(player.Game.IsRunning, Is.True, $"The bot's run ended at tick {player.Game.Tick}.");
            StateTrace solo = Trace(player, new InputReplay(log));
            player.Game.SetInputSource(solo);
            player.Game.StartRun();
            for (int t = 0; t < BotTicks; t++) player.Game.StepTick(solo);
            Assert.That(player.Game.IsRunning, Is.True, $"The replay ended at tick {player.Game.Tick}.");
            Time.timeScale = timeScale;

            yield return LoadRace(Setup().WithOpponents(Profile("Beta"), Profile("Theta"), Profile("Delta")));
            RaceField field = assembler.Field;
            foreach (RaceWorld world in field.Opponents) world.Game.SetInputSource(Bot(world));
            StateTrace together = Trace(player, new InputReplay(log));
            player.Game.SetInputSource(together);
            int outOfStep = 0;
            int checkedTicks = 0;
            Action<int> onTick = tick =>
            {
                // Raised inside the field's step, after the player's tick and before the opponents': they are one behind.
                checkedTicks++;
                foreach (RaceWorld world in field.Opponents)
                    if (world.Game.IsRunning && world.Game.Tick != tick - 1) outOfStep++;
            };
            player.Game.TickCompleted += onTick;
            try
            {
                Time.timeScale = 6f;
                player.Game.StartRun();
                float deadline = Time.realtimeSinceStartup + 90f;
                while (player.Game.IsRunning && player.Game.Tick <= BotTicks && Time.realtimeSinceStartup < deadline) yield return null;
                Time.timeScale = 0f;
            }
            finally
            {
                player.Game.TickCompleted -= onTick;
            }

            Assert.That(player.Game.Tick, Is.GreaterThan(BotTicks), "The race did not run long enough.");
            Assert.That(field.enabled, Is.True, "The field stopped the race.");
            Assert.That(checkedTicks, Is.GreaterThan(BotTicks));
            Assert.That(outOfStep, Is.EqualTo(0), "An opponent was not one tick behind the player inside the step.");
            for (int i = 0; i < field.Opponents.Count; i++)
            {
                RaceWorld world = field.Opponents[i];
                Assert.That(world.Game.IsRunning, Is.True, $"Opponent {i}'s run ended at tick {world.Game.Tick}.");
                Assert.That(world.Game.Tick, Is.EqualTo(player.Game.Tick), $"Opponent {i} is not on the player's tick between steps.");
                Assert.That(world.Circuit.Laps.Progress, Is.GreaterThan(200d), $"Opponent {i}'s bot must have run.");
            }
            string difference = StateTrace.Compare(solo, together, BotTicks);
            Assert.That(difference, Is.Null, "Three opponents alongside changed the player's run: " + difference);
            Assert.That(together.Hash(BotTicks), Is.EqualTo(solo.Hash(BotTicks)));
            Debug.Log($"ChainRush race field: the player's {BotTicks} ticks with three opponents alongside equal its solo run (hash 0x{solo.Hash(BotTicks):X16}); " +
                      $"opponents' progress {field.Opponents[0].Circuit.Laps.Progress:F1} / {field.Opponents[1].Circuit.Laps.Progress:F1} / {field.Opponents[2].Circuit.Laps.Progress:F1} m, the player's {player.Circuit.Laps.Progress:F1} m.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Field_PauseAndRestart_TheOpponentsFollowThePlayersGame()
        {
            yield return LoadRace(Setup().WithOpponents(Profile("Beta"), Profile("Omega")));
            RaceField field = assembler.Field;
            ChainRushGame game = player.Game;
            var idle = new InputReplay(new InputLog());
            game.SetInputSource(idle);
            foreach (RaceWorld world in field.Opponents) world.Game.SetInputSource(new InputReplay(new InputLog()));

            game.StartRun();
            yield return WaitForTick(game, 10);
            AssertInStep(field, game, "running");

            game.TogglePause();
            yield return null;
            yield return null;
            int pausedAt = game.Tick;
            foreach (RaceWorld world in field.Opponents) Assert.That(world.Game.IsPaused, Is.True, "An opponent runs on while the player's game is paused.");
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(game.Tick, Is.EqualTo(pausedAt));
            AssertTicks(field, pausedAt, "paused");

            game.TogglePause();
            yield return WaitForTick(game, pausedAt + 10);
            AssertInStep(field, game, "resumed");

            game.StartCountdown();
            yield return null;
            yield return null;
            foreach (RaceWorld world in field.Opponents)
            {
                Assert.That(world.Game.IsReady, Is.True, "An opponent must wait on the start line while the player's game counts down.");
                Assert.That(world.Game.Tick, Is.EqualTo(0));
            }

            game.StartRun();
            yield return WaitForTick(game, 5);
            AssertInStep(field, game, "restarted");
            Assert.That(field.enabled, Is.True);
        }

        // ---- helpers -------------------------------------------------------------------------------

        private static RunnerProfile Profile(string name)
        {
#if UNITY_EDITOR
            var profile = AssetDatabase.LoadAssetAtPath<RunnerProfile>(string.Format(ProfilePath, name));
            Assert.That(profile, Is.Not.Null, "No runner profile " + name);
            return profile;
#else
            Assert.Fail("Race field tests require the Unity Editor.");
            return null;
#endif
        }

        private static RaceSetup Setup()
        {
#if UNITY_EDITOR
            var stadium = AssetDatabase.LoadAssetAtPath<TrackDefinition>(StadiumPath);
            Assert.That(stadium, Is.Not.Null);
            return new RaceSetup(stadium, "STADIUM", "", RaceMode.Single, SpeedClass.Fast, Profile("Alpha"));
#else
            Assert.Fail("Race field tests require the Unity Editor.");
            return default;
#endif
        }

        // Loads the race scene with the setup, as the lobby does, and waits until it is assembled (opponents included).
        private IEnumerator LoadRace(RaceSetup setup)
        {
#if UNITY_EDITOR
            RaceLaunch.Request(setup);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(CircuitScene, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("Race field tests require the Unity Editor.");
            yield break;
#endif
            Scene scene = SceneManager.GetActiveScene();
            player = RaceWorld.FromScene(scene);
            assembler = Find<RaceAssembler>(scene);
            assembler.Records = records;
            assembler.Ghosts = ghosts;
            float deadline = Time.realtimeSinceStartup + 60f;
            while (assembler.IsAssembling && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(assembler.IsAssembling, Is.False, "The race was never assembled.");
            Assert.That(assembler.IsLaunched, Is.True);
            yield return null;
        }

        private static IEnumerator WaitForTick(ChainRushGame game, int tick)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (game.Tick < tick && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.Tick, Is.GreaterThanOrEqualTo(tick), "The race did not reach tick " + tick);
        }

        private static void AssertInStep(RaceField field, ChainRushGame game, string when)
        {
            Assert.That(game.IsRunning, Is.True, when);
            AssertTicks(field, game.Tick, when);
            foreach (RaceWorld world in field.Opponents) Assert.That(world.Game.IsRunning, Is.True, when);
        }

        private static void AssertTicks(RaceField field, int tick, string when)
        {
            for (int i = 0; i < field.Opponents.Count; i++)
                Assert.That(field.Opponents[i].Game.Tick, Is.EqualTo(tick), $"Opponent {i} while {when}.");
        }

        private static CircuitBot Bot(RaceWorld world) => new CircuitBot(world.Game, world.Player, world.Grapple, world.Circuit);

        private static StateTrace Trace(RaceWorld world, IInputSource inner) => new StateTrace(world.Game, world.Player, inner,
            ("lapProgress", () => world.Circuit.Laps.Progress),
            ("checkpoints", () => world.Circuit.Laps.CheckpointsPassed),
            ("laps", () => world.Circuit.Laps.CompletedLaps));

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            Assert.Fail($"The scene '{scene.name}' has no {typeof(T).Name}.");
            return null;
        }

        private static List<T> All<T>(Scene scene) where T : Component
        {
            var found = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<T>(true));
            return found;
        }
    }
}
