using System;
using System.Collections;
using System.Text;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Combat;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Endless;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Track;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    // Determinism (DESIGN.md P2): the same seed and the same per-tick input give the same run, tick for tick and
    // bit for bit. A bot's run is recorded, then replayed from the log in the same session, again after the scene
    // is loaded afresh, and at other time scales; every sample of the state must match exactly. Same machine and
    // same editor build only: other platforms' floating point is not covered.
    public sealed class ChainRushReplayTests
    {
        private const string CircuitScene = "Assets/_Project/Scenes/ChainRushCircuit.unity";
        private const string ProceduralScene = "Assets/_Project/Scenes/ChainRushProcedural.unity";
        private const int CircuitTicks = 1700;
        private const int ProceduralTicks = 2500;
        private const ulong ProceduralSeed = 7UL;

        private ChainRushGame game;
        private RunnerMotor player;
        private GrappleController grapple;
        private CircuitRace circuit;
        private ProceduralCourse course;
        private EnemyDirector enemies;
        private float timeScale;

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

        // ---- circuit -------------------------------------------------------------------------------

        [UnityTest, Timeout(240000)]
        public IEnumerator Replay_CircuitBotRun_RepeatsEveryTickBitForBit()
        {
            yield return Load(CircuitScene);
            var log = new InputLog();
            StateTrace recorded = CircuitTrace(new InputRecorder(new CircuitBot(game, player, grapple, circuit), log));
            yield return Run(recorded, CircuitTicks, 3f, null);
            Assert.That(log.Count, Is.GreaterThanOrEqualTo(CircuitTicks));
            Assert.That(circuit.Laps.CheckpointsPassed, Is.GreaterThanOrEqualTo(2), "The recorded run should get past two checkpoints.");
            Assert.That(circuit.Laps.CompletedLaps, Is.Zero, "1700 ticks are short of a lap: the grapple gap is still ahead of the finish.");

            // The same session, six times as fast: ticks per frame differ, ticks do not.
            StateTrace sameSession = CircuitTrace(new InputReplay(log));
            yield return Run(sameSession, CircuitTicks, 6f, null);
            AssertSame("circuit, same session at 6x", recorded, sameSession, CircuitTicks);

            // A freshly loaded scene, slower: nothing the first run left behind may matter.
            yield return Load(CircuitScene);
            StateTrace reloaded = CircuitTrace(new InputReplay(log));
            yield return Run(reloaded, CircuitTicks, 2f, null);
            AssertSame("circuit, reloaded scene at 2x", recorded, reloaded, CircuitTicks);
            Debug.Log($"ChainRush replay circuit: {CircuitTicks} ticks identical, hash 0x{recorded.Hash(CircuitTicks):X16}, " +
                      $"{log.Count} recorded ticks, grapples {game.Grapples}.");
        }

        // ---- procedural course ---------------------------------------------------------------------

        [UnityTest, Timeout(300000)]
        public IEnumerator Replay_ProceduralCourseRun_RepeatsCourseAndEncountersBitForBit()
        {
            yield return Load(ProceduralScene);
            var log = new InputLog();
            StateTrace recorded = ProceduralTrace(new InputRecorder(new CourseBot(game, player, grapple, enemies, course), log));
            yield return Run(recorded, ProceduralTicks, 3f, () => course.SetSeed(ProceduralSeed));
            Assert.That(log.Count, Is.GreaterThanOrEqualTo(ProceduralTicks));
            Assert.That(course.Seed, Is.EqualTo(ProceduralSeed));
            Assert.That(game.Hits, Is.GreaterThan(3), "Encounters must really happen in the recorded run.");

            StateTrace sameSession = ProceduralTrace(new InputReplay(log));
            yield return Run(sameSession, ProceduralTicks, 6f, () => course.SetSeed(ProceduralSeed));
            AssertSame("procedural seed 7, same session at 6x", recorded, sameSession, ProceduralTicks);

            yield return Load(ProceduralScene);
            StateTrace reloaded = ProceduralTrace(new InputReplay(log));
            yield return Run(reloaded, ProceduralTicks, 2f, () => course.SetSeed(ProceduralSeed));
            AssertSame("procedural seed 7, reloaded scene at 2x", recorded, reloaded, ProceduralTicks);
            Debug.Log($"ChainRush replay procedural: {ProceduralTicks} ticks identical, hash 0x{recorded.Hash(ProceduralTicks):X16}, " +
                      $"hits {game.Hits}, rebases {course.RebaseCount}, modules {course.Modules.Count}.");
        }

        // ---- the comparison can tell ----------------------------------------------------------------

        [UnityTest, Timeout(120000)]
        public IEnumerator Replay_OneTickChanged_DivergesFromThatTickOnAndNotBefore()
        {
            const int ticks = 700;
            const int changed = 300;
            yield return Load(CircuitScene);
            var log = new InputLog();
            StateTrace recorded = CircuitTrace(new InputRecorder(new CircuitBot(game, player, grapple, circuit), log));
            yield return Run(recorded, ticks, 3f, null);

            // Same log with the steering of one tick forced to full right.
            TickInput original = log[changed];
            InputLog tampered = log.CopyWith(changed, new TickInput(1f, original.PrimaryPressed, original.ReleasePressed, original.AttackPressed, original.Drift, original.ChainActionPressed));
            Assert.That(original.Steer, Is.LessThan(1f), "The bot must not already steer full right on that tick.");
            StateTrace diverged = CircuitTrace(new InputReplay(tampered));
            yield return Run(diverged, ticks, 6f, null);

            // Input index 300 is consumed as tick 301; its effect shows in the sample after that tick, sample 301.
            int first = StateTrace.FirstDifferentSample(recorded, diverged, ticks);
            Assert.That(first, Is.EqualTo(changed + 1), "The runs must be identical up to the changed tick and differ right after it. " + StateTrace.Compare(recorded, diverged, ticks));

            // And the untouched log still replays identically (the control for the control).
            StateTrace untouched = CircuitTrace(new InputReplay(log));
            yield return Run(untouched, ticks, 6f, null);
            AssertSame("circuit, untouched log", recorded, untouched, ticks);
        }

        // ---- helpers -------------------------------------------------------------------------------

        private IEnumerator Load(string path)
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("Replay tests require the Unity Editor.");
            yield break;
#endif
            game = null;
            player = null;
            circuit = null;
            course = null;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame g)) game = g;
                if (root.TryGetComponent(out RunnerMotor p)) { player = p; grapple = p.GetComponent<GrappleController>(); }
                if (root.TryGetComponent(out CircuitRace c)) circuit = c;
                if (root.TryGetComponent(out ProceduralCourse pc)) course = pc;
            }
            Assert.That(game, Is.Not.Null);
            enemies = game.Enemies;
            yield return null;
        }

        private StateTrace CircuitTrace(IInputSource inner) => new StateTrace(game, player, inner,
            ("lapProgress", () => circuit.Laps.Progress),
            ("checkpoints", () => circuit.Laps.CheckpointsPassed),
            ("laps", () => circuit.Laps.CompletedLaps));

        private StateTrace ProceduralTrace(IInputSource inner) => new StateTrace(game, player, inner,
            ("modules", () => course.Modules.Count),
            ("rebases", () => course.RebaseCount),
            ("builtPieces", () => course.BuiltPieces),
            ("activeAnchors", () => course.ActiveAnchors),
            ("encounter", () => (double)enemies.State));

        // Starts a run on `source` and lets it go on until the game has taken at least `ticks` ticks.
        private IEnumerator Run(IInputSource source, int ticks, float scale, Action beforeStart)
        {
            game.SetInputSource(source);
            beforeStart?.Invoke();
            Time.timeScale = scale;
            game.StartRun();
            float deadline = Time.realtimeSinceStartup + 200f;
            while (game.IsRunning && game.Tick < ticks && Time.realtimeSinceStartup < deadline) yield return null;
            Time.timeScale = timeScale;
            Assert.That(game.IsRunning, Is.True, $"The run ended at tick {game.Tick} (health {game.Health}, failed {game.HasFailed}).");
            Assert.That(game.Tick, Is.GreaterThanOrEqualTo(ticks), "Timed out before the wanted number of ticks.");
        }

        // The traces are compared over exactly `ticks` samples: a run goes a tick or two past it before the
        // frame ends, and those extra ticks are not part of the comparison.
        private static void AssertSame(string what, StateTrace expected, StateTrace actual, int ticks)
        {
            string difference = StateTrace.Compare(expected, actual, ticks);
            Assert.That(difference, Is.Null, what + ": " + difference);
            Assert.That(actual.Hash(ticks), Is.EqualTo(expected.Hash(ticks)), what);
        }
    }
}
