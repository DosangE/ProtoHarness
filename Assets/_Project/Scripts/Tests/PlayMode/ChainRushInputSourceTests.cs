using System;
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
    // Drives the game through IInputSource only: no Keyboard or Mouse device is created here.
    public sealed class ChainRushInputSourceTests
    {
        private sealed class ScriptedInputSource : IInputSource
        {
            public float Steer;
            public bool Primary;
            public bool Release;
            public bool Attack;
            public int PollCount;
            public int ClearCount;

            public void Poll() => PollCount++;

            public TickInput Consume()
            {
                var input = new TickInput(Steer, Primary, Release, Attack);
                Primary = false;
                Release = false;
                Attack = false;
                return input;
            }

            public void Clear() => ClearCount++;
        }

        private ChainRushGame game;
        private RunnerMotor player;
        private ScriptedInputSource source;

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
            source = new ScriptedInputSource();
            game.SetInputSource(source);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private void MovePlayer(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
            controller.Move(Vector3.down * 0.2f);
        }

        [UnityTest]
        public IEnumerator SetInputSource_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => game.SetInputSource(null));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetInputSource_Replaced_ClearsNewSourceOnce()
        {
            Assert.That(source.ClearCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Poll_NotRunning_IsNotCalledUntilRunStarts()
        {
            yield return new WaitForSeconds(0.1f);
            Assert.That(game.IsRunning, Is.False);
            Assert.That(source.PollCount, Is.Zero);
            game.StartRun();
            yield return new WaitForSeconds(0.1f);
            Assert.That(source.PollCount, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator StartRun_Called_ClearsInputSource()
        {
            int before = source.ClearCount;
            game.StartRun();
            Assert.That(source.ClearCount, Is.EqualTo(before + 1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Steer_ScriptedLeftThenRight_MovesPlayerWithoutDevices()
        {
            game.StartRun();
            source.Steer = -1f;
            yield return new WaitForSeconds(0.3f);
            float leftX = player.transform.position.x;
            Assert.That(leftX, Is.LessThan(-0.5f));
            source.Steer = 1f;
            yield return new WaitForSeconds(0.4f);
            Assert.That(player.transform.position.x, Is.GreaterThan(leftX + 0.5f));
        }

        [UnityTest]
        public IEnumerator Primary_ScriptedPressOnGround_JumpsOnce()
        {
            game.StartRun();
            yield return new WaitForSeconds(0.15f);
            Assert.That(player.IsGrounded, Is.True);
            source.Primary = true;
            yield return new WaitForSeconds(0.15f);
            Assert.That(player.IsGrounded, Is.False);
            Assert.That(player.Velocity.y, Is.GreaterThan(0f), "The jump impulse must have been applied exactly by this press.");
        }

        [UnityTest]
        public IEnumerator Attack_ScriptedPressNearTarget_HitsExactlyOnce()
        {
            game.StartRun();
            MovePlayer(new Vector3(0f, 1f, 15f));
            yield return null;
            source.Attack = true;
            yield return new WaitForSeconds(0.05f);
            Assert.That(game.Hits, Is.EqualTo(1));
        }
    }
}
