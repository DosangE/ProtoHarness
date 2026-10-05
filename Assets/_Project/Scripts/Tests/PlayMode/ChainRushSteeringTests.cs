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

        private const string PrototypeScene = "Assets/_Project/Scenes/ChainRushPrototype.unity";
        private const string EndlessScene = "Assets/_Project/Scenes/ChainRushEndless.unity";
        // Deck half width 6 m (ChainRushSceneBuilder) minus the runner's 0.38 m capsule radius.
        private const float InnerEdge = 6f - 0.38f;

        private ChainRushGame game;
        private RunnerMotor player;
        private ScriptedInputSource source;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return Load(PrototypeScene);
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
        public IEnumerator Guard_AngledHit_TurnsAlongRailAndRecoversSpeed()
        {
            game.StartRun();
            yield return new WaitForSeconds(0.5f);
            source.Steer = -1f;
            yield return new WaitForSeconds(0.3f);
            source.Steer = 0f;
            // The turn rate eases to zero over 0.1 s at the default turn acceleration; sample after it settles.
            yield return new WaitForSeconds(0.15f);
            float released = player.Heading;
            Assert.That(released, Is.LessThan(-20f));
            yield return new WaitForSeconds(0.1f);
            Assert.That(player.transform.position.x, Is.GreaterThan(-InnerEdge + 0.3f), "Still clear of the guard here.");
            Assert.That(player.Heading, Is.EqualTo(released).Within(0.01f), "Free steering keeps the facing when input is released.");
            yield return new WaitForSeconds(2f);
            Assert.That(game.IsRunning, Is.True);
            Assert.That(player.transform.position.x, Is.LessThan(-InnerEdge + 0.3f), "The runner should be running along the left guard.");
            Assert.That(player.Heading, Is.EqualTo(0f).Within(1f), "The guard must turn the facing along the rail.");
            Assert.That(player.ForwardSpeed, Is.GreaterThan(9.5f), "Speed must recover after the hit.");
        }

        [UnityTest]
        public IEnumerator Guards_BothScenes_LineEveryDeck()
        {
            AssertDecksGuarded();
            yield return Load(EndlessScene);
            AssertDecksGuarded();
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
    }
}
