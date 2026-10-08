using System.Collections;
using NUnit.Framework;
using ProtoHarness.ChainRush;
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
    // The circuit race scene (COURSE.md T4): a bot runs three laps of the stadium and the lap counter is
    // exact; going backwards over the line is not a lap, a gap still kills, a restart starts over.
    public sealed class ChainRushCircuitTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/ChainRushCircuit.unity";
        private const float StandingHeight = 1.05f;

        private ChainRushGame game;
        private RunnerMotor player;
        private GrappleController grapple;
        private CircuitRace circuit;
        private float timeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            timeScale = Time.timeScale;
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("Circuit tests require the Unity Editor.");
            yield break;
#endif
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame g)) game = g;
                if (root.TryGetComponent(out RunnerMotor p)) { player = p; grapple = p.GetComponent<GrappleController>(); }
                if (root.TryGetComponent(out CircuitRace c)) circuit = c;
            }
            Assert.That(game, Is.Not.Null);
            Assert.That(circuit, Is.Not.Null, "The circuit scene needs a CircuitRace (menu: Create Circuit Scene).");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = timeScale;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // ---- the bot runs three laps --------------------------------------------------------------------

        [UnityTest, Timeout(180000)]
        public IEnumerator Race_BotRunsThreeLaps_CountsThemAndTimesThemExactly()
        {
            game.SetInputSource(new CircuitBot(game, player, grapple, circuit));
            Time.timeScale = 3f;
            game.StartRun();
            LapCounter laps = circuit.Laps;
            float started = Time.realtimeSinceStartup;
            float deadline = started + 150f;
            float maxOffset = 0f;
            double lastProgress = laps.Progress;
            while (game.IsRunning && !laps.IsFinished && Time.realtimeSinceStartup < deadline)
            {
                maxOffset = Mathf.Max(maxOffset, Mathf.Abs(game.Track.Project(player.transform.position).D));
                Assert.That(laps.Progress, Is.GreaterThanOrEqualTo(lastProgress - 1d), "The bot's progress must not run backwards. " + State());
                lastProgress = laps.Progress;
                yield return null;
            }
            Time.timeScale = timeScale;
            Assert.That(game.HasFailed, Is.False, "The bot fell or died. " + State());
            Assert.That(laps.IsFinished, Is.True, "Timed out after " + (Time.realtimeSinceStartup - started).ToString("F0") + " s. " + State());
            Assert.That(game.HasFinished, Is.True, State());
            Assert.That(laps.CompletedLaps, Is.EqualTo(3));
            double lapSeconds = circuit.Definition.LapLength / 10d;
            int sum = 0;
            for (int lap = 1; lap <= 3; lap++)
            {
                float seconds = Ticks.ToSeconds(laps.LapTicks(lap));
                Debug.Log($"ChainRush circuit lap {lap}: {seconds:F2} s");
                Assert.That(seconds, Is.EqualTo(lapSeconds).Within(lapSeconds * 0.2d), $"lap {lap} time");
                sum += laps.LapTicks(lap);
            }
            Assert.That(laps.TotalTicks, Is.EqualTo(sum));
            Assert.That(game.Tick, Is.EqualTo(laps.LapEndTick(3)), "The game ends the race on the tick the third lap is completed.");
            Assert.That(game.Elapsed, Is.EqualTo(Ticks.ToSeconds(laps.TotalTicks)).Within(1e-3f));
            Assert.That(game.Health, Is.EqualTo(3));
            Assert.That(maxOffset, Is.LessThan(RoadProfile.DeckHalfWidth - 0.4f), "The bot touched a guard.");
            for (int cp = 0; cp < 3; cp++)
            {
                for (int lap = 1; lap <= 3; lap++) Assert.That(laps.CheckpointTick(lap, cp), Is.GreaterThan(0), $"lap {lap} checkpoint {cp}");
            }
            Debug.Log($"ChainRush circuit race: total {Ticks.ToSeconds(laps.TotalTicks):F2} s, {game.Grapples} grapples, slowest step {circuit.MaxStepMilliseconds:F2} ms, " +
                      $"{circuit.PieceCount} road pieces, real time {Time.realtimeSinceStartup - started:F0} s, max offset {maxOffset:F2} m.");
        }

        // ---- direction and gaps -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Race_RunningBackwardsOverTheStartLine_IsNotALap()
        {
            game.SetInputSource(new TrackFollower(game.Track, player) { Steering = false });
            game.StartRun();
            yield return LandOnRoad();
            game.Racer.Heading = Mathf.Repeat(game.Racer.Heading + 180f + 180f, 360f) - 180f;
            float deadline = Time.time + 1.5f;
            while (game.IsRunning && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(game.IsRunning, Is.True, State());
            Assert.That(circuit.Laps.Progress, Is.LessThan(-5d), "The runner went backwards over the line. " + State());
            Assert.That(circuit.Laps.CompletedLaps, Is.Zero);
            Assert.That(circuit.Laps.IsFinished, Is.False);
        }

        [UnityTest]
        public IEnumerator Gap_RunningStraightIntoTheGrappleGap_EndsTheRunAsAFallWithoutALap()
        {
            circuit.TryGetNextGap(150d, out double startS, out _, out bool hasAnchor);
            Assert.That(hasAnchor, Is.True, "The first gap after S 150 is the grapple gap on straight B.");
            game.SetInputSource(new TrackFollower(game.Track, player));
            game.StartRun();
            Teleport(startS - 12d);
            yield return LandOnRoad();
            float deadline = Time.time + 8f;
            while (game.IsRunning && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(game.HasFailed, Is.True, "Running into the gap must end the run. " + State());
            Assert.That(game.Track.Project(player.transform.position).H, Is.LessThan(-11f), State());
            Assert.That(circuit.Laps.CompletedLaps, Is.Zero);
            Assert.That(game.HasFinished, Is.False);
        }

        // ---- restart and scene --------------------------------------------------------------------------

        [UnityTest, Timeout(60000)]
        public IEnumerator Race_Restart_StartsAgainOnTheLineWithNoLaps()
        {
            game.SetInputSource(new CircuitBot(game, player, grapple, circuit));
            Time.timeScale = 3f;
            game.StartRun();
            float deadline = Time.realtimeSinceStartup + 20f;
            while (game.IsRunning && circuit.Laps.CheckpointsPassed < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Time.timeScale = timeScale;
            Assert.That(circuit.Laps.CheckpointsPassed, Is.GreaterThanOrEqualTo(2), "The bot should reach the second checkpoint. " + State());
            game.StartRun();
            Assert.That(circuit.Laps.CompletedLaps, Is.Zero);
            Assert.That(circuit.Laps.CheckpointsPassed, Is.Zero);
            Assert.That(circuit.Laps.Progress, Is.EqualTo(0d).Within(1e-9));
            Assert.That(game.Tick, Is.Zero);
            yield return LandOnRoad();
            Assert.That(game.Track.Project(player.transform.position).S, Is.LessThan(2d).Or.GreaterThan(circuit.Definition.LapLength - 2d), "The runner starts on the line. " + State());
        }

        [UnityTest]
        public IEnumerator Scene_AnchorsAndRoad_MatchTheDefinition()
        {
            TrackDefinition definition = circuit.Definition;
            Assert.That(circuit.ActiveAnchors, Is.EqualTo(definition.Anchors.Count));
            TrackDefinition.Anchor anchor = definition.Anchors[0];
            Vector3 expected = game.Track.FrameAt(anchor.S).TransformPoint(new Vector3(anchor.Offset, anchor.Height, 0f));
            Assert.That(Vector3.Distance(circuit.AnchorAt(0).position, expected), Is.LessThan(1e-3f));
            Assert.That(game.Track.IsLoop, Is.True);
            Assert.That(game.Track.EndS, Is.EqualTo(definition.LapLength).Within(1e-9));
            Assert.That(circuit.PieceCount, Is.GreaterThanOrEqualTo(8));
            Assert.That(game.IsCircuit, Is.True);
            Assert.That(game.IsEndless, Is.False);
            Assert.That(game.Enemies, Is.Null);
            yield return null;
        }

        // ---- helpers ------------------------------------------------------------------------------------

        private void Teleport(double s)
        {
            TrackFrame frame = game.Track.FrameAt(s);
            // The track looks near the focus, so move the focus first, then let the circuit follow the runner.
            game.Track.SetFocus(s);
            player.ShiftOrigin(frame.TransformPoint(new Vector3(0f, StandingHeight, 0f)) - player.transform.position);
            player.FaceTrack();
            Physics.SyncTransforms();
            circuit.Step(game.Tick);
        }

        // IsGrounded still reports the spawn road until the controller has moved once on the new one.
        private IEnumerator LandOnRoad()
        {
            yield return new WaitForFixedUpdate();
            float deadline = Time.time + 2f;
            while (!player.IsGrounded && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.True, "Runner never landed on the circuit. " + State());
        }

        private string State()
        {
            TrackCoord coord = game.Track.Project(player.transform.position);
            return $"S {coord.S:F2}, D {coord.D:F2}, H {coord.H:F2}, position {player.transform.position}, velocity {player.Velocity}, grounded {player.IsGrounded}, " +
                   $"lap {circuit.Laps.CurrentLap}, laps done {circuit.Laps.CompletedLaps}, progress {circuit.Laps.Progress:F1}, failed {game.HasFailed}";
        }
    }
}
