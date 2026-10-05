using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Track;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    // Corner swing (COURSE.md T2d): the chain action while drifting into a curve hooks the arc's center.
    // Runs on a visible guarded R30 test road beside the prototype course, steered by TrackFollower.
    public sealed class ChainRushCornerSwingTests
    {
        private const float RoadX = 300f;
        private const float HalfWidth = 6f;
        private const float InnerEdge = HalfWidth - 0.38f;
        private const double ArcStart = 30d;
        private static readonly double ArcEnd = ArcStart + 30d * Mathf.PI / 2d;

        private ChainRushGame game;
        private RunnerMotor player;
        private readonly List<Object> built = new List<Object>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
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
            for (int i = 0; i < built.Count; i++) if (built[i] != null) Object.Destroy(built[i]);
            built.Clear();
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Swing_DriftingIntoCurve_HooksClearsGuardsAndBeatsPlainRun()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot);
            float plainTime = 0f;
            yield return TimeArc(line, t => plainTime = t);

            yield return StartOn(line, bot, false);
            yield return RunTo(line, ArcStart + 1d);
            // Timed from the same point as the plain run.
            float start = Time.time;
            game.Racer.AddGauge(1f);
            bot.Drift = true;
            yield return new WaitForFixedUpdate();
            bot.ChainAction = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(player.IsSwinging, Is.True, "Drift + chain action inside a curve must start a corner swing.");
            Assert.That(game.Racer.Gauge, Is.LessThan(0.2f), "The swing spends one slot.");
            float maxOffset = 0f;
            float peakSwingSpeed = 0f;
            float deadline = Time.time + 10f;
            bool exited = false;
            while (game.IsRunning && S(line) < ArcEnd && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                maxOffset = Mathf.Max(maxOffset, Mathf.Abs(line.Project(player.transform.position).D));
                if (player.IsSwinging) peakSwingSpeed = Mathf.Max(peakSwingSpeed, player.ForwardSpeed);
                if (!exited && !player.IsSwinging)
                {
                    exited = true;
                    bot.Drift = false;
                }
            }
            float swingTime = Time.time - start;
            bot.Drift = false;
            Debug.Log("ChainRush corner swing: R30 quarter arc plain " + plainTime.ToString("0.00") + " s, with swing from s="
                + (ArcStart + 1d).ToString("0") + " " + swingTime.ToString("0.00") + " s, peak swing speed "
                + peakSwingSpeed.ToString("0.0") + " m/s, max offset " + maxOffset.ToString("0.00") + " m.");
            Assert.That(game.IsRunning, Is.True);
            Assert.That(maxOffset, Is.LessThan(InnerEdge - 0.3f), "The swing must keep the runner clear of the guards.");
            Assert.That(peakSwingSpeed, Is.GreaterThan(13f), "The swing must speed up toward 14 m/s.");
            // 2 s near 14 m/s then a 17 m/s exit should take well over 15 % off a 10 m/s run.
            Assert.That(swingTime, Is.LessThan(plainTime * 0.85f), "Swinging through the curve must clearly beat running it.");
        }

        [UnityTest]
        public IEnumerator Swing_ReleaseDrift_EndsAndThrowsFast()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot);
            yield return RunTo(line, ArcStart + 1d);
            game.Racer.AddGauge(1f);
            bot.Drift = true;
            yield return new WaitForFixedUpdate();
            bot.ChainAction = true;
            yield return new WaitForSeconds(0.3f);
            Assert.That(player.IsSwinging, Is.True);
            bot.Drift = false;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(player.IsSwinging, Is.False, "Releasing drift must end the swing.");
            Assert.That(player.ForwardSpeed, Is.GreaterThan(15.5f), "The exit throws at 17 m/s and the carry holds 16.");
        }

        [UnityTest]
        public IEnumerator ChainAction_DriftingOnStraight_FiresSlingshotNotSwing()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player) { Steering = false, Override = 1f };
            yield return StartOn(line, bot);
            yield return RunTo(line, 12d);
            game.Racer.AddGauge(1f);
            bot.Drift = true;
            yield return new WaitForFixedUpdate();
            bot.ChainAction = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(player.IsSwinging, Is.False, "No curve here, so no swing.");
            Assert.That(player.IsSlingPulling, Is.True, "Off a curve the chain action is the slingshot.");
        }

        // Time for the bot to run the arc from its start to its end without chain actions.
        private IEnumerator TimeArc(Centerline line, System.Action<float> report)
        {
            yield return RunTo(line, ArcStart + 1d);
            float start = Time.time;
            yield return RunTo(line, ArcEnd);
            report(Time.time - start);
        }

        // Straight 30 m, an R30 right-hand quarter arc, straight 40 m.
        private static Centerline CurveRoad()
        {
            var line = new Centerline(new Vector3(RoadX, 0f, 0f), 0f);
            line.AppendStraight(30f);
            line.AppendArc(30f, 90f);
            line.AppendStraight(40f);
            return line;
        }

        private IEnumerator StartOn(Centerline line, TrackFollower bot, bool buildRoad = true)
        {
            if (buildRoad) TestRoad.Build(line, -5d, line.EndS, HalfWidth, true, built);
            bot.Drift = false;
            bot.ChainAction = false;
            game.SetTrack(line);
            game.SetInputSource(bot);
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
            float deadline = Time.time + 15f;
            while (game.IsRunning && S(line) < s && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(S(line), Is.GreaterThanOrEqualTo(s), "Timed out before s=" + s + ".");
        }

        private double S(Centerline line) => line.Project(player.transform.position).S;
    }
}
