using System.Collections;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    // Free steering and deck guards (COURSE.md T2a), driven through IInputSource only.
    public sealed class ChainRushSteeringTests
    {
        private sealed class ScriptedInputSource : IInputSource
        {
            public float Steer;

            public void Poll() { }
            public TickInput Consume() => new TickInput(Steer, false, false, false);
            public void Clear() { }
        }

        private const string TestBenchScene = "Assets/_Project/Scenes/ChainRushTestBench.unity";
        // Deck half width 6 m (ChainRushSceneBuilder) minus the runner's 0.38 m capsule radius.
        private const float InnerEdge = 6f - 0.38f;

        private ChainRushGame game;
        private RunnerMotor player;
        private ScriptedInputSource source;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return Load(TestBenchScene);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Guard_SteerHardLeft_StaysOnDeckAndKeepsRunning()
        {
            game.StartRun();
            source.Steer = -1f;
            float startZ = player.transform.position.z;
            float minX = 0f;
            float deadline = Time.time + 2.5f;
            while (Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                minX = Mathf.Min(minX, player.transform.position.x);
            }
            Assert.That(game.IsRunning, Is.True, "Run ended while steering into the guard.");
            Assert.That(player.IsGrounded, Is.True);
            Assert.That(minX, Is.LessThan(-InnerEdge + 0.3f), "The runner never reached the left guard.");
            Assert.That(minX, Is.GreaterThan(-InnerEdge - 0.1f), "The runner went past the left guard.");
            Assert.That(player.transform.position.z, Is.GreaterThan(startZ + 5f), "The guard must turn the runner along the road.");
        }

        [UnityTest]
        public IEnumerator Guard_AngledHit_SlidesAlongRailKeepingTheFacing()
        {
            // In ticks: a long frame in the steering wait turns the runner further, and it can reach the guard
            // before the "still clear" check.
            game.StartRun();
            yield return WaitTicks(Ticks.FromSeconds(0.5f));
            source.Steer = -1f;
            // The bench runs circuit physics (2026-10-19): the 22 m turn radius turns about 45 degrees a second here,
            // so 0.6 s of full steer (0.3 s before) is what passes -20 degrees.
            yield return WaitTicks(Ticks.FromSeconds(0.6f));
            source.Steer = 0f;
            // The turn rate eases to zero over 0.1 s at the default turn acceleration; sample after it settles.
            yield return WaitTicks(Ticks.FromSeconds(0.15f));
            float released = player.Heading;
            Assert.That(released, Is.LessThan(-20f));
            yield return WaitTicks(Ticks.FromSeconds(0.1f));
            Assert.That(player.transform.position.x, Is.GreaterThan(-InnerEdge + 0.3f), "Still clear of the guard here.");
            Assert.That(player.Heading, Is.EqualTo(released).Within(0.01f), "Free steering keeps the facing when input is released.");
            yield return WaitTicks(Ticks.FromSeconds(2f));
            Assert.That(game.IsRunning, Is.True);
            Assert.That(player.transform.position.x, Is.LessThan(-InnerEdge + 0.3f), "The runner should be running along the left guard.");
            Assert.That(player.Heading, Is.EqualTo(released).Within(0.01f), "A guard never turns the facing (user 2026-11-04).");
            Debug.Log("ChainRush angled guard hit: heading " + released.ToString("0.00") + " deg, speed along the rail 2 s later "
                + player.Velocity.z.ToString("0.00") + " m/s.");
            // The run target's share along the rail: run speed 20 by the cosine of the angle to it.
            float along = 20f * Mathf.Cos(released * Mathf.Deg2Rad);
            Assert.That(player.Velocity.z, Is.EqualTo(along).Within(0.5f), "The run must carry on along the rail.");
        }

        // Running the wrong way the guard keeps the facing too, and the run goes on along it the way the runner faces.
        [UnityTest]
        public IEnumerator Guard_FacingBackward_SlidesBackwardKeepingTheFacing()
        {
            const float heading = -150f;
            game.StartRun();
            // Leave room behind on the first deck. Drive keeps the old velocity when the facing changes, then
            // accelerates along the new facing and removes sideways speed before the runner reaches the left guard.
            yield return WaitTicks(Ticks.FromSeconds(2f));
            Assert.That(player.transform.position.z, Is.GreaterThan(25f), "precondition: room to run backward on the deck.");
            game.Racer.Heading = heading;
            yield return WaitTicks(Ticks.FromSeconds(2f));
            Assert.That(game.IsRunning, Is.True);
            Assert.That(player.transform.position.x, Is.LessThan(-InnerEdge + 0.3f), "The runner should reach the left guard.");
            Assert.That(player.transform.position.x, Is.GreaterThan(-InnerEdge - 0.1f), "The runner should stay inside the guard.");
            Debug.Log("ChainRush guard contact: heading " + heading.ToString("0") + ", speed along the guard "
                + player.Velocity.z.ToString("0.00") + " m/s.");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(player.Heading, heading)), Is.LessThanOrEqualTo(0.01f), "The guard must keep the facing.");
            Assert.That(player.Velocity.z, Is.LessThan(-5f), "The run must carry on backward along the guard.");
        }

        // The angle to a guard sets the speed along it (user 2026-11-04): square gives nothing, a slight turn creeps that way
        // and more turn goes faster. Read from how far the runner really moved, not from its velocity.
        [UnityTest]
        public IEnumerator Guard_NearSquare_MovesAlongTheRailByTheAngle([Values(-85f, -75f, -60f)] float heading)
        {
            game.StartRun();
            yield return WaitTicks(Ticks.FromSeconds(1f));
            game.Racer.Heading = heading;
            yield return WaitTicks(Ticks.FromSeconds(2f));
            Assert.That(player.transform.position.x, Is.LessThan(-InnerEdge + 0.3f), "The runner should be against the left guard.");
            float z = player.transform.position.z;
            yield return WaitTicks(Ticks.FromSeconds(1f));
            float moved = player.transform.position.z - z;
            float along = 20f * Mathf.Cos(heading * Mathf.Deg2Rad);
            Debug.Log("ChainRush near square: heading " + heading.ToString("0") + ", moved " + moved.ToString("0.00") + " m in 1 s, velocity "
                + player.Velocity.z.ToString("0.00") + " m/s, wanted " + along.ToString("0.00") + " m/s.");
            Assert.That(moved, Is.EqualTo(along).Within(along * 0.25f), "The runner must really move along the guard by the angle.");
        }

        // Stopped square against a guard the speed that sets the turn limit is gone: steering must still get the runner out.
        [UnityTest]
        public IEnumerator Guard_SquareOnHit_SteeringTurnsTheRunnerAway()
        {
            game.StartRun();
            yield return WaitTicks(Ticks.FromSeconds(1f));
            game.Racer.Heading = -90f;
            yield return WaitTicks(Ticks.FromSeconds(1.5f));
            Assert.That(player.transform.position.x, Is.LessThan(-InnerEdge + 0.3f), "The runner should be against the left guard.");
            float z = player.transform.position.z;
            Debug.Log("ChainRush square hit: speed " + player.Velocity.magnitude.ToString("0.00") + " m/s, turn limit "
                + player.TurnLimit.ToString("0.00") + " deg/s.");
            source.Steer = 1f;
            yield return WaitTicks(Ticks.FromSeconds(2f));
            Debug.Log("ChainRush square hit: after 2 s of full steer heading " + player.Heading.ToString("0.0") + ", moved "
                + (player.transform.position.z - z).ToString("0.00") + " m along the guard.");
            Assert.That(Mathf.DeltaAngle(-90f, player.Heading), Is.GreaterThan(30f), "Steering must turn the runner off the guard.");
            Assert.That(player.transform.position.z - z, Is.GreaterThan(1f), "Turned out, the runner must get going again.");
        }

        [UnityTest]
        public IEnumerator Guards_PrototypeDecks_AreGuarded()
        {
            AssertDecksGuarded();
            yield return null;
        }

        private static void AssertDecksGuarded()
        {
            int decks = 0;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>())
                {
                    if (item.name != "Deck" || item.GetComponent<BoxCollider>() == null) continue;
                    decks++;
                    int rails = 0;
                    foreach (Transform sibling in item.parent)
                        if (sibling.name == "Guard rail" && sibling.TryGetComponent(out BoxCollider rail) && rail.enabled) rails++;
                    Assert.That(rails, Is.EqualTo(2), "Deck under " + item.parent.name + " needs a guard on each side.");
                }
            Assert.That(decks, Is.GreaterThan(0), "No decks found in " + SceneManager.GetActiveScene().path);
        }

        private IEnumerator Load(string path)
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("These tests require the Unity Editor.");
            yield break;
#endif
            game = null;
            player = null;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame foundGame)) game = foundGame;
                if (root.TryGetComponent(out RunnerMotor foundPlayer)) player = foundPlayer;
            }
            Assert.That(game, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            source = new ScriptedInputSource();
            game.SetInputSource(source);
            yield return null;
        }

        // Until the game has run exactly `ticks` more ticks. A wait in game seconds lets one long frame run several
        // ticks before the coroutine resumes (up to the 0.33 s maximum timestep), so a check of something that lasts
        // only a few ticks waits in ticks. The final check fails if a resume ever skipped a tick.
        private IEnumerator WaitTicks(int ticks)
        {
            int target = game.Tick + ticks;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (game.Tick < target && Time.realtimeSinceStartup < deadline) yield return new WaitForFixedUpdate();
            Assert.That(game.Tick, Is.EqualTo(target), $"Waited for {ticks} ticks.");
        }
    }
}
