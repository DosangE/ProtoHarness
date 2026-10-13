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
    // Drift, the chain gauge and the chain actions (COURSE.md T2c), driven through IInputSource only.
    // Open-road checks use a wide visible test road beside the prototype course; the grapple check uses
    // the prototype course's first gap like ChainRushTests.
    public sealed class ChainRushDriftTests
    {
        private sealed class ScriptedInputSource : IInputSource
        {
            public float Steer;
            public bool Drift;
            public bool ChainAction;

            public void Poll() { }

            public TickInput Consume()
            {
                var input = new TickInput(Steer, false, false, false, Drift, ChainAction);
                ChainAction = false;
                return input;
            }

            public void Clear() { }
        }

        private const float RoadX = 300f;
        private const float RunSpeed = 10f;

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
                "Assets/_Project/Scenes/ChainRushPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
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
        public IEnumerator Drift_FullSteer_SlidesFillsGaugeAndRegainsGrip()
        {
            Centerline line = OpenRoad();
            yield return StartOn(line);
            yield return RunTo(line, 15d);
            source.Drift = true;
            source.Steer = 1f;
            float peakSlip = 0f;
            float until = Time.time + 1f;
            while (Time.time < until)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(player.IsDrifting, Is.True);
                var flat = new Vector3(player.Velocity.x, 0f, player.Velocity.z);
                if (flat.sqrMagnitude > 1f) peakSlip = Mathf.Max(peakSlip, Vector3.Angle(flat, player.Facing));
            }
            float gauge = game.Racer.Gauge;
            Debug.Log("ChainRush drift: 1 s full steer, peak slip " + peakSlip.ToString("0.0") + " deg, gauge " + gauge.ToString("0.00") + " slots.");
            Assert.That(peakSlip, Is.GreaterThan(10f), "Drifting at full steer must slide.");
            Assert.That(gauge, Is.GreaterThan(0.2f), "Sliding must fill the gauge.");
            source.Drift = false;
            source.Steer = 0f;
            yield return new WaitForSeconds(0.3f);
            Assert.That(Mathf.Abs(player.SideSpeed), Is.LessThan(0.5f), "Releasing drift must restore grip.");
            Assert.That(game.Racer.Gauge, Is.EqualTo(gauge).Within(1e-4f), "Only drifting fills the gauge.");
        }

        [UnityTest]
        public IEnumerator Sling_WithSlot_PullsPastFifteenThenSettles()
        {
            Centerline line = OpenRoad();
            yield return StartOn(line);
            yield return RunTo(line, 15d);
            game.Racer.AddGauge(1f);
            source.ChainAction = true;
            float peak = 0f;
            float until = Time.time + 0.5f;
            while (Time.time < until)
            {
                yield return new WaitForFixedUpdate();
                peak = Mathf.Max(peak, player.ForwardSpeed);
            }
            Assert.That(game.Racer.Gauge, Is.EqualTo(0f).Within(1e-4f), "The slingshot spends one slot.");
            Assert.That(peak, Is.GreaterThan(15f), "The chain pull must accelerate the runner.");
            yield return new WaitForSeconds(1.5f);
            Assert.That(game.IsRunning, Is.True);
            Assert.That(player.ForwardSpeed, Is.EqualTo(RunSpeed).Within(0.5f), "Speed returns to the run speed after the carry.");
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
            MovePlayer(new Vector3(0f, 1f, 44f));
            yield return new WaitForFixedUpdate();
            player.PrimaryAction();
            yield return new WaitForSeconds(0.25f);
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

        // A wide straight road (no guards) so slides and pulls never touch an edge.
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
