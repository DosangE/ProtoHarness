using System;
using System.Collections;
using System.Diagnostics;
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
using Debug = UnityEngine.Debug;

namespace ProtoHarness.Tests.PlayMode
{
    // What the per-tick controller normalization (RunnerMotor.NormalizeController: CharacterController off, then on)
    // costs. The numbers go to the Console and to DECISIONS; the one assertion is a loose ceiling that only
    // catches something gone badly wrong, not a benchmark. Editor numbers only: a built player was not measured.
    public sealed class ChainRushControllerCostTests
    {
        private const string CircuitScene = "Assets/_Project/Scenes/ChainRushCircuit.unity";
        private const double TickBudgetMicroseconds = 20000.0;
        private const double CeilingMicroseconds = TickBudgetMicroseconds * 0.1;

        private ChainRushGame game;
        private RunnerMotor player;
        private GrappleController grapple;
        private CircuitRace circuit;
        private float timeScale;

        // Plays a log back starting at an arbitrary tick.
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
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = timeScale;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private static double Microseconds(long ticks) => ticks * 1000000.0 / Stopwatch.Frequency;

        [UnityTest, Timeout(120000)]
        public IEnumerator ControllerOffAndOn_PerCall_IsFarBelowTheTickBudget()
        {
            yield return Load();
            Time.timeScale = 0f;
            game.StartRun();
            var controller = player.GetComponent<CharacterController>();
            Assert.That(controller, Is.Not.Null);

            // The same two operations as RunnerMotor.NormalizeController, which is private.
            const int batches = 5;
            const int perBatch = 400;
            var perCall = new double[batches];
            for (int b = 0; b < batches; b++)
            {
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < perBatch; i++)
                {
                    controller.enabled = false;
                    controller.enabled = true;
                }
                perCall[b] = Microseconds(Stopwatch.GetTimestamp() - start) / perBatch;
            }
            Array.Sort(perCall);
            double median = perCall[batches / 2];
            Debug.Log($"ChainRush controller cost: off+on = {median:F2} us per call (median of {batches} batches of {perBatch}; min {perCall[0]:F2}, max {perCall[batches - 1]:F2}); tick budget {TickBudgetMicroseconds:F0} us, so {median / TickBudgetMicroseconds * 100.0:F3}% of a tick.");
            Assert.That(median, Is.LessThan(CeilingMicroseconds), "Off+on costs more than 10% of a tick; look at the numbers in the log.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator WholeTick_AndAReplay_CostWithTheNormalizationIncluded()
        {
            yield return Load();
            var log = new InputLog();
            var bot = new InputRecorder(new CircuitBot(game, player, grapple, circuit), log);
            Time.timeScale = 0f;
            game.StartRun();

            const int ticks = 700;
            const int snapshotTick = 300;
            SimSnapshot snapshot = default;
            long start = Stopwatch.GetTimestamp();
            long excluded = 0;
            while (game.IsRunning && game.Tick < ticks)
            {
                if (game.Tick == snapshotTick)
                {
                    long before = Stopwatch.GetTimestamp();
                    snapshot = game.CaptureSnapshot();
                    excluded += Stopwatch.GetTimestamp() - before;
                }
                game.StepTick(bot);
            }
            double wholeRun = Microseconds(Stopwatch.GetTimestamp() - start - excluded);
            Assert.That(game.IsRunning, Is.True, $"The bot's run ended at tick {game.Tick}.");
            Assert.That(log.Count, Is.GreaterThanOrEqualTo(ticks));
            double perTick = wholeRun / ticks;

            // 20 rollbacks of 40 ticks each, replaying the recorded input (no bot work in the loop).
            const int rollbacks = 20;
            const int span = 40;
            long replayStart = Stopwatch.GetTimestamp();
            for (int r = 0; r < rollbacks; r++)
            {
                game.RestoreSnapshot(snapshot);
                var replay = new OffsetReplay(log, snapshotTick);
                for (int i = 0; i < span; i++) game.StepTick(replay);
            }
            double replayTotal = Microseconds(Stopwatch.GetTimestamp() - replayStart);
            double perReplayTick = replayTotal / (rollbacks * span);

            Debug.Log($"ChainRush tick cost: {perTick:F1} us per tick over {ticks} ticks with the bot and the recorder in the loop = {perTick / TickBudgetMicroseconds * 100.0:F2}% of the {TickBudgetMicroseconds:F0} us budget.");
            Debug.Log($"ChainRush replay cost: {perReplayTick:F1} us per replayed tick ({rollbacks} rollbacks x {span} ticks = {replayTotal / 1000.0:F2} ms, restore included); 8 resimulated ticks per frame would cost {perReplayTick * 8.0 / 1000.0:F3} ms of a 20 ms tick.");
            Assert.That(perReplayTick, Is.LessThan(CeilingMicroseconds), "A replayed tick costs more than 10% of a tick; look at the numbers in the log.");
        }

        private IEnumerator Load()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(CircuitScene, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("Controller cost tests require the Unity Editor.");
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
    }
}
