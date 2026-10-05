using System.Collections;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    public sealed class ChainRushTests
    {
        private ChainRushGame game;
        private RunnerMotor player;
        private GrappleController grapple;
        private float previousTimeScale;

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
                if (root.TryGetComponent(out GrappleController foundGrapple)) grapple = foundGrapple;
            }
            Assert.That(game, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(grapple, Is.Not.Null);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator StartRun_Ready_RunsAndMovesForward()
        {
            Assert.That(game.IsReady, Is.True);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChainRush-ready.png"));
            yield return new WaitForSeconds(0.15f);
            game.StartRun();
            float initialZ = player.transform.position.z;
            yield return new WaitForSeconds(0.5f);
            Assert.That(game.IsRunning, Is.True);
            Assert.That(player.transform.position.z, Is.GreaterThan(initialZ + 2f));
            Assert.That(player.IsGrounded, Is.True);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChainRush-running.png"));
            yield return new WaitForSeconds(0.15f);
        }

        [UnityTest]
        public IEnumerator Grapple_JumpAtGap_AttachesConstrainsAndReleases()
        {
            game.StartRun();
            MovePlayer(new Vector3(0f, 1f, 44f));
            yield return new WaitForFixedUpdate();
            player.PrimaryAction();
            yield return new WaitForSeconds(0.25f);
            Assert.That(player.IsGrounded, Is.False);
            Assert.That(grapple.TryAttach(), Is.True);
            Assert.That(game.Grapples, Is.EqualTo(1));
            Vector3 displacement = Vector3.down * 50f;
            Vector3 velocity = Vector3.down * 25f;
            grapple.ConstrainMotion(player.transform.position, ref displacement, ref velocity, 0.02f);
            Assert.That(Vector3.Distance(player.transform.position + displacement, grapple.AnchorPosition),
                Is.LessThanOrEqualTo(grapple.RopeLength + 0.001f));
            grapple.Release(true);
            Assert.That(grapple.IsAttached, Is.False);
            Assert.That(player.Velocity.y, Is.GreaterThanOrEqualTo(7f));
        }

        [UnityTest]
        public IEnumerator Grapple_NoAnchorInRange_DoesNotAttach()
        {
            game.StartRun();
            MovePlayer(new Vector3(50f, 5f, 12f));
            yield return new WaitForFixedUpdate();
            Assert.That(grapple.TryAttach(), Is.False);
            Assert.That(grapple.IsAttached, Is.False);
        }

        [UnityTest]
        public IEnumerator Damage_RepeatedContact_RespectsInvulnerability()
        {
            game.StartRun();
            game.TakeDamage();
            game.TakeDamage();
            Assert.That(game.Health, Is.EqualTo(2));
            yield return new WaitForSeconds(1.3f);
            game.TakeDamage();
            Assert.That(game.Health, Is.EqualTo(1));
        }

        // Device: depends on virtual Keyboard/Mouse event delivery, which failed repeatedly in the editor
        // (docs/DECISIONS.md 2026-10-04). Excluded from the merge gate run; run it with the Device menu.
        [UnityTest]
        [Category("Device")]
        public IEnumerator Input_KeyboardAndMouse_StartsSteersJumpsGrapplesAndRestarts()
        {
            Keyboard originalKeyboard = Keyboard.current;
            Mouse originalMouse = Mouse.current;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return new WaitForSeconds(0.15f);
                Assert.That(game.IsRunning, Is.True);
                // Free steering (2026-10-05, T2a): A/D turn the facing.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
                yield return new WaitForSeconds(0.3f);
                float leftHeading = player.Heading;
                Assert.That(leftHeading, Is.LessThan(-10f));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                yield return new WaitForSeconds(0.4f);
                Assert.That(player.Heading, Is.GreaterThan(leftHeading + 10f));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                MovePlayer(new Vector3(0f, 1f, 44f));
                player.FaceTrack();
                yield return new WaitForFixedUpdate();
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                yield return new WaitForSeconds(0.15f);
                Assert.That(player.IsGrounded, Is.False);
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return new WaitForSeconds(0.05f);
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                yield return new WaitForSeconds(0.1f);
                Assert.That(grapple.IsAttached, Is.True);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChainRush-grapple.png"));
                yield return new WaitForSeconds(0.05f);
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return new WaitForSeconds(0.05f);
                Assert.That(grapple.IsAttached, Is.False);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                yield return new WaitForSeconds(0.05f);
                Assert.That(game.Grapples, Is.Zero);
                Assert.That(player.transform.position.z, Is.LessThan(7f));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                MovePlayer(new Vector3(0f, 1f, 15f));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return new WaitForSeconds(0.05f);
                Assert.That(game.Hits, Is.EqualTo(1));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
                if (originalKeyboard != null) originalKeyboard.MakeCurrent();
                if (originalMouse != null) originalMouse.MakeCurrent();
            }
        }

        // Device: left Shift held drifts and fills the gauge; left Ctrl spends a slot (T2c, KartRider keys).
        [UnityTest]
        [Category("Device")]
        public IEnumerator Input_ShiftAndCtrl_DriftsAndFiresChainAction()
        {
            Keyboard originalKeyboard = Keyboard.current;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return new WaitForSeconds(0.5f);
                Assert.That(game.IsRunning, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift, Key.D));
                yield return new WaitForSeconds(0.4f);
                Assert.That(player.IsDrifting, Is.True, "Holding left Shift on the ground must drift.");
                Assert.That(game.Racer.Gauge, Is.GreaterThan(0f), "Drifting must fill the chain gauge.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(0.1f);
                Assert.That(player.IsDrifting, Is.False);
                game.Racer.AddGauge(1f);
                float before = game.Racer.Gauge;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
                yield return new WaitForSeconds(0.1f);
                Assert.That(game.Racer.Gauge, Is.EqualTo(before - 1f).Within(0.05f), "Left Ctrl must spend one slot.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (originalKeyboard != null) originalKeyboard.MakeCurrent();
            }
        }

        [UnityTest]
        public IEnumerator Restart_AfterAttackAndFall_ResetsCourseAndState()
        {
            game.StartRun();
            MovePlayer(new Vector3(0f, 1f, 15f));
            game.Attack();
            Assert.That(game.Hits, Is.EqualTo(1));
            MovePlayer(new Vector3(0f, -13f, 15f));
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(game.HasFailed, Is.True);
            game.StartRun();
            Assert.That(game.Health, Is.EqualTo(3));
            Assert.That(game.Hits, Is.Zero);
            Assert.That(game.Grapples, Is.Zero);
            Assert.That(player.transform.position.z, Is.EqualTo(5f).Within(0.1f));
            MovePlayer(new Vector3(0f, 1f, 15f));
            game.Attack();
            Assert.That(game.Hits, Is.EqualTo(1), "Destroyed targets must be restored on restart.");
        }

        [UnityTest]
        public IEnumerator Pause_Running_FreezesMotionAndTimer()
        {
            game.StartRun();
            yield return new WaitForSeconds(0.2f);
            game.TogglePause();
            Vector3 position = player.transform.position;
            float elapsed = game.Elapsed;
            yield return new WaitForSeconds(0.3f);
            Assert.That(player.transform.position, Is.EqualTo(position));
            Assert.That(game.Elapsed, Is.EqualTo(elapsed));
            game.TogglePause();
            yield return new WaitForSeconds(0.2f);
            Assert.That(player.transform.position.z, Is.GreaterThan(position.z));
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator Course_TimedJumpsAndHeldGrapple_ReachesFinish()
        {
            Time.timeScale = 3f;
            game.StartRun();
            float deadline = Time.realtimeSinceStartup + 65f;
            float nextTraceZ = 36f;
            string trajectory = "";
            while (game.IsRunning && Time.realtimeSinceStartup < deadline)
            {
                float z = player.transform.position.z;
                if (z >= nextTraceZ)
                {
                    trajectory += "\nPosition=" + player.transform.position + ", velocity=" + player.Velocity +
                        ", ground=" + player.IsGrounded + ", linked=" + grapple.IsAttached + ", length=" + grapple.RopeLength;
                    nextTraceZ += 2f;
                }
                for (int gap = 0; gap < 8; gap++)
                {
                    float edge = 48f + gap * 56f;
                    if (player.IsGrounded && z >= edge - 4.5f && z < edge) player.PrimaryAction();
                }
                if (!player.IsGrounded && player.transform.position.y > 2.6f && !grapple.IsAttached)
                    grapple.TryAttach();
                game.Attack();
                yield return null;
            }
            Assert.That(game.HasFinished, Is.True,
                "Course did not finish: position=" + player.transform.position + ", links=" + game.Grapples + ", health=" + game.Health + trajectory);
            Assert.That(game.Grapples, Is.GreaterThanOrEqualTo(8));
            Assert.That(game.Elapsed, Is.LessThan(80f));
        }

        private void MovePlayer(Vector3 position)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
            controller.Move(Vector3.down * 0.2f);
        }
    }
}
