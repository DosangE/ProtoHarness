using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Track;
using ProtoHarness.ChainRush.Visuals;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    // Corner pole swing (PROPOSAL_CORNER_POLE.md 2, COURSE.md 4-5): every arc has a pole at its center; the chain action
    // hooks it from the straight before the arc, the rope keeps its length, the runner circles on its own momentum while
    // the chain key is held. Runs on a visible guarded R30 test road beside the bench course, steered by TrackFollower.
    // On this road the pole is (RoadX + 30, 30) and can be hooked from s = 12 to 22: closer to the arc it is more than
    // 75 degrees aside, farther back more than R + 5 = 35 m away.
    public sealed class ChainRushCornerSwingTests
    {
        private const float RoadX = 300f;
        private const float Radius = 30f;
        private const float HalfWidth = 6f;
        private const float InnerEdge = HalfWidth - 0.38f;
        private const double ArcStart = 30d;
        private const double HookS = 16d;
        private static readonly double ArcEnd = ArcStart + Radius * Mathf.PI / 2d;
        private static readonly Vector3 Pole = new Vector3(RoadX + Radius, 0f, (float)ArcStart);

        private ChainRushGame game;
        private RunnerMotor player;
        private readonly List<Object> built = new List<Object>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Project/Scenes/ChainRushTestBench.unity", new LoadSceneParameters(LoadSceneMode.Single));
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
        public IEnumerator Swing_HookedBeforeTheArc_KeepsItsRopeAndItsSpeed()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot);
            yield return RunTo(line, HookS);
            game.Racer.AddGauge(1f);
            Vector3 hookedAt = player.transform.position;
            yield return Hook(bot);
            Assert.That(player.IsSwinging, Is.True, "A chain action with the pole in reach must hook it.");
            Assert.That(game.Racer.Gauge, Is.LessThan(0.2f), "The swing spends one slot.");
            Vector3 pole = game.Racer.SwingCenter;
            Assert.That(new Vector2(pole.x, pole.z), Is.EqualTo(new Vector2(Pole.x, Pole.z)).Using(Vector2EqualityComparer(1e-3f)), "The pole is the arc's center.");
            float rope = game.Racer.SwingRadius;
            Assert.That(rope, Is.EqualTo(Level(hookedAt - pole)).Within(0.25f), "The rope is the level distance to the pole when it hooked.");

            float hookSpeed = player.ForwardSpeed;
            float minSpeed = float.MaxValue, maxSpeed = 0f, worstRope = 0f, maxOffset = 0f;
            float deadline = Time.time + 10f;
            while (game.IsRunning && S(line) < ArcEnd - 2d && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(player.IsSwinging, Is.True, "The swing lasts while the chain key is held (s=" + S(line).ToString("0.0") + ").");
                worstRope = Mathf.Max(worstRope, Mathf.Abs(Level(player.transform.position - pole) - rope));
                minSpeed = Mathf.Min(minSpeed, player.ForwardSpeed);
                maxSpeed = Mathf.Max(maxSpeed, player.ForwardSpeed);
                maxOffset = Mathf.Max(maxOffset, Mathf.Abs(line.Project(player.transform.position).D));
            }
            Debug.Log("ChainRush pole swing: rope " + rope.ToString("0.00") + " m, hook speed " + hookSpeed.ToString("0.00") + " m/s, speed "
                + minSpeed.ToString("0.00") + ".." + maxSpeed.ToString("0.00") + " m/s, rope error " + worstRope.ToString("0.000") + " m, max offset "
                + maxOffset.ToString("0.00") + " m.");
            Assert.That(S(line), Is.GreaterThanOrEqualTo(ArcEnd - 2d), "Timed out in the arc.");
            Assert.That(worstRope, Is.LessThan(0.05f), "The rope keeps its length.");
            Assert.That(maxSpeed, Is.LessThan(hookSpeed + 0.1f), "Nothing speeds the swing up: it runs on momentum.");
            Assert.That(minSpeed, Is.GreaterThan(hookSpeed - 0.3f), "Nothing slows the swing down on flat ground.");
            Assert.That(maxOffset, Is.LessThan(InnerEdge - 0.3f), "Hooked from here, the circle stays on the road through the arc.");
        }

        [UnityTest]
        public IEnumerator Swing_InsideTheArc_ThePoleIsBesideSoTheChainFiresTheSlingshot()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot);
            yield return RunTo(line, ArcStart + 5d);
            Assert.That(player.TryFindPole(out _), Is.False, "Inside its arc the pole is about 90 degrees aside, past the 75 allowed.");
            game.Racer.AddGauge(1f);
            yield return Hook(bot);
            Assert.That(player.IsSwinging, Is.False, "The same corner cannot be hooked again from inside it.");
            Assert.That(player.IsSlingPulling, Is.True, "With no pole in reach the chain action is the slingshot.");
        }

        [UnityTest]
        public IEnumerator Swing_PoleBeyondReach_FiresTheSlingshot()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot);
            yield return RunTo(line, 6d);
            Assert.That(Level(player.transform.position - Pole), Is.GreaterThan(Radius + 5f), "From here the pole is past R + 5.");
            Assert.That(player.TryFindPole(out _), Is.False);
            game.Racer.AddGauge(1f);
            yield return Hook(bot);
            Assert.That(player.IsSwinging, Is.False);
            Assert.That(player.IsSlingPulling, Is.True);
        }

        [UnityTest]
        public IEnumerator Swing_LettingGoOfTheChain_EndsAndThrowsFast()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot);
            yield return RunTo(line, HookS);
            game.Racer.AddGauge(1f);
            yield return Hook(bot);
            yield return new WaitForSeconds(0.3f);
            Assert.That(player.IsSwinging, Is.True);
            bot.ChainHold = false;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(player.IsSwinging, Is.False, "Letting go of the chain key ends the swing.");
            Assert.That(player.ForwardSpeed, Is.GreaterThan(15.5f), "The exit throws at 17 m/s and the carry holds 16.");
        }

        // Held past the arc the runner keeps circling, crosses the straight after it and meets the inside guard there
        // (rope 33.1 m around a pole 30 m from the centerline: about 23 m into the straight). The guard ends the swing
        // without the throw.
        [UnityTest]
        public IEnumerator Swing_HeldPastTheArc_CirclesOnUntilAGuard_ThenStopsWithoutTheThrow()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot);
            yield return RunTo(line, HookS);
            game.Racer.AddGauge(1f);
            yield return Hook(bot);
            float hookSpeed = player.ForwardSpeed;
            yield return RunTo(line, ArcEnd + 2d);
            Assert.That(player.IsSwinging, Is.True, "Leaving the arc does not end a held swing.");
            float deadline = Time.time + 6f;
            while (game.IsRunning && player.IsSwinging && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(player.IsSwinging, Is.False, "The circle must reach a guard.");
            Assert.That(bot.ChainHold, Is.True, "The key was still held: the guard ended it.");
            TrackCoord at = line.Project(player.transform.position);
            Debug.Log("ChainRush pole swing: guard ended the swing at s=" + at.S.ToString("0.0") + " (arc end " + ArcEnd.ToString("0.0")
                + "), d=" + at.D.ToString("0.00") + ", speed " + player.ForwardSpeed.ToString("0.00") + " m/s.");
            Assert.That(at.S, Is.InRange(ArcEnd + 10d, ArcEnd + 40d));
            Assert.That(at.D, Is.GreaterThan(InnerEdge - 0.5f), "It meets the inside (right) guard.");
            Assert.That(player.ForwardSpeed, Is.LessThan(hookSpeed + 0.1f), "A guard ends the swing without the 17 m/s throw.");
        }

        // Presentation only (SwingLook): the body slews into the curve, the chain bites the inside rail, the root never turns.
        [UnityTest]
        public IEnumerator SwingLook_WhileSwinging_TurnsOnlyTheBodyInsideAndHooksTheInnerRail()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot);
            Transform body = player.GetComponent<RunnerTilt>().Body;
            LineRenderer chain = player.transform.Find("Chain Line").GetComponent<LineRenderer>();
            Quaternion root = player.transform.rotation;
            yield return RunTo(line, HookS);
            game.Racer.AddGauge(1f);
            yield return Hook(bot);
            // Into the arc (the rail is drawn there) and past the pose's blend time.
            yield return RunTo(line, ArcStart + 3d);
            yield return new WaitForSeconds(SwingLook.BlendTime + 0.05f);
            Assert.That(player.IsSwinging, Is.True);
            yield return new WaitForEndOfFrame();

            Quaternion upright = Quaternion.Euler(0f, player.Heading, 0f) * RunnerTilt.Evaluate(player.Velocity, player.Steer);
            Quaternion slewed = Quaternion.Euler(0f, player.Heading + SwingLook.SlipAngle, 0f) * RunnerTilt.Evaluate(player.Velocity, player.Steer);
            // The R30 arc turns right, so the inside is to the right: a positive yaw.
            Assert.That(Quaternion.Angle(body.rotation, slewed), Is.LessThan(3f), "The body must slew into the curve while swinging.");
            Assert.That(Quaternion.Angle(body.rotation, upright), Is.GreaterThan(SwingLook.SlipAngle - 3f));
            Assert.That(Quaternion.Angle(player.transform.rotation, root), Is.LessThan(1e-3f), "Only the body turns; the root carries physics.");

            Assert.That(chain.enabled, Is.True);
            Vector3 bite = chain.GetPosition(1);
            TrackCoord coord = line.Project(bite);
            Assert.That(coord.D, Is.EqualTo(HalfWidth).Within(0.3f), "The chain must bite the inside (right) rail, not the arc center.");
            Assert.That(coord.H, Is.EqualTo(RoadMeshBuilder.GuardRailHeight).Within(0.3f));

            bot.ChainHold = false;
            yield return new WaitForSeconds(SwingLook.BlendTime + 0.25f);
            Assert.That(player.IsSwinging, Is.False);
            yield return new WaitForEndOfFrame();
            upright = Quaternion.Euler(0f, player.Heading, 0f) * RunnerTilt.Evaluate(player.Velocity, player.Steer);
            Assert.That(Quaternion.Angle(body.rotation, upright), Is.LessThan(3f), "After the swing the body must face the heading again.");
            Assert.That(Quaternion.Angle(player.transform.rotation, root), Is.LessThan(1e-3f));
        }

        // Presses the chain key and keeps it held, then waits two ticks for the press to land.
        private static IEnumerator Hook(TrackFollower bot)
        {
            bot.ChainAction = true;
            bot.ChainHold = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        // Straight 30 m, an R30 right-hand quarter arc, straight 40 m.
        private static Centerline CurveRoad()
        {
            var line = new Centerline(new Vector3(RoadX, 0f, 0f), 0f);
            line.AppendStraight((float)ArcStart);
            line.AppendArc(Radius, 90f);
            line.AppendStraight(40f);
            return line;
        }

        private IEnumerator StartOn(Centerline line, TrackFollower bot)
        {
            TestRoad.Build(line, -5d, line.EndS, HalfWidth, true, built);
            bot.Drift = false;
            bot.ChainAction = false;
            bot.ChainHold = false;
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

        private static float Level(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        private static IEqualityComparer<Vector2> Vector2EqualityComparer(float tolerance) => new Vector2Close(tolerance);

        private sealed class Vector2Close : IEqualityComparer<Vector2>
        {
            private readonly float tolerance;
            public Vector2Close(float tolerance) => this.tolerance = tolerance;
            public bool Equals(Vector2 a, Vector2 b) => (a - b).magnitude <= tolerance;
            public int GetHashCode(Vector2 v) => 0;
        }
    }
}
