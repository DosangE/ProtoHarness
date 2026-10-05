using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
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
    // Curves with free steering (COURSE.md T2b) on a visible test road beside the prototype course: a bot
    // steers around R30, an unsteered runner is turned by the outer guard, and side grip is measured.
    public sealed class ChainRushCurveTests
    {
        private const float RoadX = 300f;
        private const float HalfWidth = 6f;
        // Guard inner face minus the runner's 0.38 m capsule radius.
        private const float InnerEdge = HalfWidth - 0.38f;
        private static readonly float QuarterR30 = 30f * Mathf.PI / 2f;

        private ChainRushGame game;
        private RunnerMotor player;
        private float previousTimeScale;
        private readonly List<Object> built = new List<Object>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Project/Scenes/ChainRushPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("These prototype tests require the Unity Editor.");
            yield break;
#endif
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame foundGame)) game = foundGame;
                if (root.TryGetComponent(out RunnerMotor foundPlayer)) player = foundPlayer;
            }
            Assert.That(game, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale;
            for (int i = 0; i < built.Count; i++) if (built[i] != null) Object.Destroy(built[i]);
            built.Clear();
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Curve_BotSteersR30_StaysClearOfGuards()
        {
            Centerline line = CurveRoad(90f);
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot);
            double exitS = 30d + QuarterR30;
            float maxOffset = 0f;
            float minSpeed = float.PositiveInfinity;
            float deadline = Time.time + 15f;
            while (game.IsRunning && S(line) < exitS + 15d && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                maxOffset = Mathf.Max(maxOffset, Mathf.Abs(line.Project(player.transform.position).D));
                if (S(line) > 15d) minSpeed = Mathf.Min(minSpeed, player.ForwardSpeed);
            }
            Assert.That(game.IsRunning, Is.True, "Run ended in the curve.");
            Assert.That(S(line), Is.GreaterThanOrEqualTo(exitS + 15d), "Timed out in the curve.");
            Assert.That(maxOffset, Is.LessThan(InnerEdge - 0.3f), "The bot touched a guard (max offset " + maxOffset + ").");
            Assert.That(Mathf.DeltaAngle(player.Heading, 90f), Is.EqualTo(0f).Within(5f), "The runner should leave the curve facing +x.");
            Assert.That(minSpeed, Is.GreaterThan(8f), "Clean steering should keep speed through R30.");
        }

        [UnityTest]
        public IEnumerator Curve_NoSteering_OuterGuardTurnsRunnerWithoutFall()
        {
            Centerline line = CurveRoad(90f);
            var still = new TrackFollower(line, player) { Steering = false };
            yield return StartOn(line, still);
            double exitS = 30d + QuarterR30;
            float minOffset = 0f;
            float deadline = Time.time + 20f;
            while (game.IsRunning && S(line) < exitS + 15d && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                minOffset = Mathf.Min(minOffset, line.Project(player.transform.position).D);
            }
            Assert.That(game.IsRunning, Is.True, "An unsteered runner must not fall in a guarded curve.");
            Assert.That(S(line), Is.GreaterThanOrEqualTo(exitS + 15d), "The guard should carry the runner through the curve.");
            Assert.That(minOffset, Is.LessThan(-InnerEdge + 0.3f), "Going straight into a right curve must reach the outer (left) guard.");
        }

        // Measurement for the KartRider-style grip decision (DECISIONS 2026-10-05 T2b): full steer for
        // 0.5 s at run speed on a wide straight road, per side grip. Logs the peak slide; the turn needs
        // speed x 120 deg/s, about 21 m/s^2 at 10 m/s, so grips under that should slide.
        [UnityTest]
        public IEnumerator Grip_FullSteerAtRunSpeed_LogsSlidePerGrip()
        {
            var line = new Centerline(new Vector3(RoadX, 0f, 0f), 0f);
            line.AppendStraight(200f);
            TestRoad.Build(line, -5d, 80d, 40f, false, built);
            float[] grips = { 60f, 30f, 15f };
            var table = new System.Text.StringBuilder("ChainRush grip sweep (full steer 0.5 s from 10 m/s):");
            float slideAtLowest = 0f;
            float slideAtHighest = 0f;
            foreach (float grip in grips)
            {
                SetSideGrip(grip);
                var hold = new TrackFollower(line, player) { Steering = false };
                yield return StartOn(line, hold, false);
                yield return RunTo(line, 15d);
                hold.Override = 1f;
                float peakSlide = 0f;
                float peakSlip = 0f;
                float until = Time.time + 0.5f;
                while (Time.time < until)
                {
                    yield return new WaitForFixedUpdate();
                    peakSlide = Mathf.Max(peakSlide, Mathf.Abs(player.SideSpeed));
                    var flat = new Vector3(player.Velocity.x, 0f, player.Velocity.z);
                    if (flat.sqrMagnitude > 1f) peakSlip = Mathf.Max(peakSlip, Vector3.Angle(flat, player.Facing));
                }
                Assert.That(game.IsRunning, Is.True);
                table.Append(" grip ").Append(grip.ToString("0")).Append(": slide ").Append(peakSlide.ToString("0.00"))
                    .Append(" m/s, slip ").Append(peakSlip.ToString("0.0")).Append(" deg;");
                if (grip == grips[0]) slideAtHighest = peakSlide;
                if (grip == grips[grips.Length - 1]) slideAtLowest = peakSlide;
            }
            Debug.Log(table.ToString());
            Assert.That(slideAtLowest, Is.GreaterThan(slideAtHighest), "Lower grip must slide more: " + table);
        }

        // Straight 30 m, an R30 arc, straight 40 m.
        private static Centerline CurveRoad(float degrees)
        {
            var line = new Centerline(new Vector3(RoadX, 0f, 0f), 0f);
            line.AppendStraight(30f);
            line.AppendArc(30f, degrees);
            line.AppendStraight(40f);
            return line;
        }

        private IEnumerator StartOn(Centerline line, IInputSource source, bool buildRoad = true)
        {
            if (buildRoad) TestRoad.Build(line, -5d, line.EndS, HalfWidth, true, built);
            game.SetTrack(line);
            game.SetInputSource(source);
            game.StartRun();
            Vector3 start = line.FrameAt(5d).TransformPoint(new Vector3(0f, 1.2f, 0f));
            player.ShiftOrigin(start - player.transform.position);
            Physics.SyncTransforms();
            float deadline = Time.time + 2f;
            while (!player.IsGrounded && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.True, "Runner never landed on the test road.");
        }

        private IEnumerator RunTo(Centerline line, double s)
        {
            float deadline = Time.time + 10f;
            while (game.IsRunning && S(line) < s && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(S(line), Is.GreaterThanOrEqualTo(s), "Timed out before s=" + s + ".");
        }

        private double S(Centerline line) => line.Project(player.transform.position).S;

        // Changes the runner's side grip on this play-mode instance only; the scene asset is not touched.
        private void SetSideGrip(float grip)
        {
#if UNITY_EDITOR
            var motor = new SerializedObject(player);
            SerializedProperty property = motor.FindProperty("sideGrip");
            Assert.That(property, Is.Not.Null, "RunnerMotor.sideGrip is missing.");
            property.floatValue = grip;
            motor.ApplyModifiedPropertiesWithoutUndo();
#else
            Assert.Fail("Grip sweep needs the Unity Editor.");
#endif
        }
    }
}
