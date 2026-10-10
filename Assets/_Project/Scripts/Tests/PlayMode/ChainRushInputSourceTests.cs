using System;
using System.Collections;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Visuals;
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
        // Free steering (2026-10-05, T2a): steering turns the facing; the runner moves the way it faces.
        public IEnumerator Steer_ScriptedLeftThenRight_TurnsFacingWithoutDevices()
        {
            game.StartRun();
            source.Steer = -1f;
            yield return new WaitForSeconds(0.3f);
            float leftHeading = player.Heading;
            Assert.That(leftHeading, Is.LessThan(-10f), "Steering left must turn the facing left.");
            Assert.That(player.transform.position.x, Is.LessThan(0f), "Facing left must carry the runner left.");
            source.Steer = 1f;
            yield return new WaitForSeconds(0.4f);
            Assert.That(player.Heading, Is.GreaterThan(leftHeading + 10f), "Steering right must turn the facing back right.");
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

        [UnityTest]
        public IEnumerator Tilt_ScriptedSteerLeft_LeansBodyFromMotorState()
        {
            Assert.That(player.GetComponent<RunnerTilt>(), Is.Not.Null, "RunnerTilt must sit on the runner.");
            Transform body = player.transform.Find("Runner Visual");
            Assert.That(body, Is.Not.Null);
            game.StartRun();
            source.Steer = -1f;
            yield return new WaitForSeconds(0.3f);
            yield return new WaitForEndOfFrame();
            Assert.That(player.Steer, Is.EqualTo(-1f));
            // The body faces the heading, then leans on top of it.
            Quaternion expected = Quaternion.LookRotation(player.Facing) * RunnerTilt.Evaluate(player.Velocity, player.Steer);
            Assert.That(Quaternion.Angle(body.rotation, expected), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(body.localRotation, Quaternion.identity), Is.GreaterThan(10f));
            source.Steer = 0f;
            game.StartRun();
            // Whether a tick lands before the next end of frame depends on frame timing; force one so the
            // check below sees the same state every run.
            yield return new WaitForFixedUpdate();
            yield return new WaitForEndOfFrame();
            // That tick adds gravity (velocity.y -0.44, a 0.4 degree pitch), so "upright" means the steering
            // lean is gone: no steer, and no yaw or roll beyond what velocity alone gives.
            Assert.That(player.Steer, Is.Zero);
            Assert.That(Quaternion.Angle(body.rotation, Quaternion.LookRotation(player.Facing) * RunnerTilt.Evaluate(player.Velocity, 0f)),
                Is.LessThan(0.01f), "Restart must clear the steering lean.");
        }

        [UnityTest]
        // Only the body turns: the root carries the physics capsule, and turning it per frame breaks bit-for-bit replay.
        public IEnumerator Tilt_ScriptedSteerLeft_TurnsBodyToFaceHeading()
        {
            Transform body = player.transform.Find("Runner Visual");
            Assert.That(body, Is.Not.Null);
            Quaternion rootRotation = player.transform.rotation;
            game.StartRun();
            source.Steer = -1f;
            yield return new WaitForSeconds(0.3f);
            yield return new WaitForEndOfFrame();
            Assert.That(player.Heading, Is.LessThan(-10f), "The runner must have turned for this check to mean anything.");
            Assert.That(Quaternion.Angle(body.rotation, Quaternion.LookRotation(player.Facing) * RunnerTilt.Evaluate(player.Velocity, player.Steer)),
                Is.LessThan(0.5f), "The body must face its heading after turning.");
            // Heading wraps from -180 to 180; the body must follow across the seam.
            game.Racer.Heading = -170f;
            yield return new WaitForSeconds(0.2f);
            yield return new WaitForEndOfFrame();
            Assert.That(player.Heading, Is.GreaterThan(90f), "Steering left from -170 must have wrapped past -180.");
            Assert.That(Quaternion.Angle(body.rotation, Quaternion.LookRotation(player.Facing) * RunnerTilt.Evaluate(player.Velocity, player.Steer)),
                Is.LessThan(0.5f), "The body must face its heading after it wraps.");
            Assert.That(player.transform.rotation, Is.EqualTo(rootRotation), "The root must not turn.");
        }
    }
}
