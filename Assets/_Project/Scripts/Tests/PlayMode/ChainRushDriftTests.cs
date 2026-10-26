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
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    // The chain gauge and the chain actions (COURSE.md T2c), driven through IInputSource only. Drift lived here too until
    // R4b took it out (the swing is the drift now, ChainRushCornerSwingTests); the file kept its name.
    // Open-road checks use a wide visible test road beside the prototype course; the grapple check uses
    // the prototype course's first gap like ChainRushTests.
    public sealed class ChainRushDriftTests
    {
        private sealed class ScriptedInputSource : IInputSource
        {
            public bool ChainAction;
            public bool Coasting;

            public void Poll() { }

            public TickInput Consume()
            {
                var input = new TickInput(0f, false, false, false, false, ChainAction, coasting: Coasting);
                ChainAction = false;
                return input;
            }

            public void Clear() { }
        }

        private const float RoadX = 300f;
        // ChainRushTestBench.unity runner: circuit physics since 2026-10-19 (run 20, slingshot to 28, carry 26 for 2.6 s with
        // a push of 30 m/s a second at rest easing off toward the boost speed, overspeed falls off by 8 m/s a second).
        private const float RunSpeed = 20f;

        private ChainRushGame game;
        private RunnerMotor player;
        private GrappleController grapple;
        private ScriptedInputSource source;
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
                if (root.TryGetComponent(out GrappleController foundGrapple)) grapple = foundGrapple;
            }
            Assert.That(game, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(grapple, Is.Not.Null);
            source = new ScriptedInputSource();
            game.SetInputSource(source);
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
        public IEnumerator Sling_WithSlot_PullsWellPastRunSpeedThenSettles()
        {
            Centerline line = OpenRoad();
            yield return StartOn(line);
            yield return RunTo(line, 15d);
            game.Racer.AddGauge(1f);
            source.ChainAction = true;
            float peak = 0f;
            // The boost eases off as it nears its speed (2026-10-19): from 20 it is about 24.4 after 1 s and 25.8 at the end
            // of the 3 s boost (pull 0.4 s + carry 2.6 s), so the peak is read over the whole boost.
            float until = Time.time + 3f;
            while (Time.time < until)
            {
                yield return new WaitForFixedUpdate();
                peak = Mathf.Max(peak, player.ForwardSpeed);
            }
            Assert.That(game.Racer.Gauge, Is.EqualTo(0f).Within(1e-4f), "The slingshot spends one slot.");
            Assert.That(peak, Is.GreaterThan(RunSpeed + 5f), "The chain pull must accelerate the runner.");
            // Then about 25.8 -> 20 at 8 m/s a second (0.73 s): settled about 3.7 s after the press.
            yield return new WaitForSeconds(1f);
            Assert.That(game.IsRunning, Is.True);
            Assert.That(player.ForwardSpeed, Is.EqualTo(RunSpeed).Within(0.5f), "Speed returns to the run speed after the carry.");
        }

        // KartRider style (2026-10-19): letting go of the throttle cuts a booster at once, the slingshot and a swing's carry,
        // and the runner then coasts down instead of riding the 2 s carry at 26.
        [UnityTest]
        public IEnumerator Sling_ThrottleLetGo_CutsTheBoostAtOnce()
        {
            Centerline line = OpenRoad();
            yield return StartOn(line);
            yield return RunTo(line, 15d);
            game.Racer.AddGauge(1f);
            source.ChainAction = true;
            yield return new WaitForSeconds(0.3f);
            Assert.That(player.IsSlingPulling, Is.True, "precondition: the slingshot is pulling.");
            game.Racer.SwingCarrySpeed = 25f;
            source.Coasting = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(game.Racer.SlingTicks, Is.Zero, "Letting go of the throttle ends the slingshot.");
            Assert.That(game.Racer.SwingCarrySpeed, Is.Zero, "Letting go of the throttle ends a swing's carry.");
            float cut = player.ForwardSpeed;
            yield return new WaitForSeconds(0.5f);
            Assert.That(player.ForwardSpeed, Is.LessThan(cut - 2f), "After the cut the runner coasts down.");
        }

        // The slingshot is a booster, and a booster needs the throttle (2026-10-19): without it the chain action keeps the slot.
        [UnityTest]
        public IEnumerator Sling_WithoutThrottle_DoesNotFireAndKeepsTheSlot()
        {
            Centerline line = OpenRoad();
            yield return StartOn(line);
            yield return RunTo(line, 15d);
            game.Racer.AddGauge(1f);
            source.Coasting = true;
            source.ChainAction = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(source.ChainAction, Is.False, "precondition: the press was consumed.");
            Assert.That(game.Racer.SlingTicks, Is.Zero, "No slingshot without the throttle.");
            Assert.That(game.Racer.Gauge, Is.EqualTo(1f).Within(1e-4f), "The slot is kept.");
        }

        [UnityTest]
        public IEnumerator Sling_WithoutWholeSlot_DoesNothing()
        {
            Centerline line = OpenRoad();
            yield return StartOn(line);
            yield return RunTo(line, 15d);
            game.Racer.AddGauge(0.9f);
            source.ChainAction = true;
            float peak = 0f;
            float until = Time.time + 0.5f;
            while (Time.time < until)
            {
                yield return new WaitForFixedUpdate();
                peak = Mathf.Max(peak, player.ForwardSpeed);
            }
            Assert.That(game.Racer.Gauge, Is.EqualTo(0.9f).Within(1e-4f));
            Assert.That(peak, Is.LessThan(RunSpeed + 0.5f));
        }

        [UnityTest]
        public IEnumerator Grapple_ChainActionWhileAttached_ReelsFasterAndThrowsHarder()
        {
            game.StartRun();
            // 1 m before the bench deck's edge (z 48): a jump from rest brings its anchor (z 56, 10 m up) within the rope's
            // reach in the air, about 9 m since 2026-10-19 (GrappleController.minHangHeight). Press once it is offered.
            MovePlayer(new Vector3(0f, 1f, 47f));
            yield return new WaitForFixedUpdate();
            player.PrimaryAction();
            float deadline = Time.time + 1f;
            while (grapple.Candidate == null && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(grapple.Candidate, Is.Not.Null, $"No anchor came within the rope's reach: runner at {player.transform.position}.");
            Assert.That(grapple.TryAttach(), Is.True);
            game.Racer.AddGauge(1f);
            source.ChainAction = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(game.Racer.GrappleEmpowered, Is.True, "A chain action while attached empowers the grapple.");
            Assert.That(game.Racer.Gauge, Is.EqualTo(0f).Within(1e-4f));
            float before = grapple.RopeLength;
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.That(before - grapple.RopeLength, Is.GreaterThan(1.5f), "Empowered reel is 12 m/s (normal 3).");
            grapple.Release(true);
            Assert.That(player.ForwardSpeed, Is.GreaterThanOrEqualTo(18f - 1e-3f), "Empowered release throws at 18 m/s or more.");
        }

        // A wide straight road (no guards) so pulls never touch an edge.
        private Centerline OpenRoad()
        {
            var line = new Centerline(new Vector3(RoadX, 0f, 0f), 0f);
            line.AppendStraight(200f);
            TestRoad.Build(line, -5d, 120d, 40f, false, built);
            return line;
        }

        private IEnumerator StartOn(Centerline line)
        {
            game.SetTrack(line);
            game.StartRun();
            Vector3 start = line.FrameAt(5d).TransformPoint(new Vector3(0f, 1.2f, 0f));
            player.ShiftOrigin(start - player.transform.position);
            Physics.SyncTransforms();
            // IsGrounded still reports the spawn deck until the controller moves once on the new road.
            yield return new WaitForFixedUpdate();
            float deadline = Time.time + 2f;
            while (!player.IsGrounded && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.True, "Runner never landed on the test road.");
        }

        private IEnumerator RunTo(Centerline line, double s)
        {
            float deadline = Time.time + 10f;
            while (game.IsRunning && line.Project(player.transform.position).S < s && Time.time < deadline)
                yield return new WaitForFixedUpdate();
            Assert.That(line.Project(player.transform.position).S, Is.GreaterThanOrEqualTo(s), "Timed out before s=" + s + ".");
        }

        private void MovePlayer(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
            controller.Move(Vector3.down * 0.2f);
        }
    }
}
