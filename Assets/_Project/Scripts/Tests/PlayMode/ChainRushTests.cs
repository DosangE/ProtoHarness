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
                // Enter counts down 3, 2, 1, then the run starts.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return new WaitForSeconds(0.15f);
                Assert.That(game.IsCountingDown, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return WaitOutCountdown();
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
                // R puts the runner back on the start line at once and counts down again.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                yield return new WaitForSeconds(0.05f);
                Assert.That(game.IsCountingDown, Is.True);
                Assert.That(game.Grapples, Is.Zero);
                Assert.That(player.transform.position.z, Is.LessThan(7f));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return WaitOutCountdown();
                Assert.That(game.IsRunning, Is.True);
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
                yield return new WaitForSeconds(0.15f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return WaitOutCountdown();
                yield return new WaitForSeconds(0.35f);
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

        // Device: with ManualStart off (a network race), Enter and R neither start nor restart a run.
        [UnityTest]
        [Category("Device")]
        public IEnumerator Input_ManualStartOff_EnterAndRDoNothing()
        {
            Keyboard originalKeyboard = Keyboard.current;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            game.ManualStart = false;
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return new WaitForSeconds(0.15f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(0.05f);
                Assert.That(game.IsReady, Is.True, "Enter must not start a run while ManualStart is off.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                yield return new WaitForSeconds(0.15f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(game.IsReady, Is.True, "R must not start a run while ManualStart is off.");

                game.StartRun();
                yield return new WaitForSeconds(0.3f);
                int tick = game.Tick;
                Assert.That(tick, Is.GreaterThan(0));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                yield return new WaitForSeconds(0.15f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(game.IsRunning, Is.True, "R must not restart a run while ManualStart is off.");
                Assert.That(game.Tick, Is.GreaterThan(tick), "The run must go on, not start over.");
            }
            finally
            {
                game.ManualStart = true;
                InputSystem.RemoveDevice(keyboard);
                if (originalKeyboard != null) originalKeyboard.MakeCurrent();
            }
        }

        // The countdown before a run: 3, 2, 1 (game time), then one CountdownFinished and the run starts at tick 0.
        [UnityTest]
        public IEnumerator StartCountdown_CountsThreeTwoOne_ThenStartsTheRunOnce()
        {
            Assert.That(game.ManualStart, Is.True, "Enter and R start runs unless something turns that off.");
            int finished = 0;
            int tickAtFinish = -1;
            System.Action onFinished = () => { finished++; tickAtFinish = game.Tick; };
            game.CountdownFinished += onFinished;
            try
            {
                float startedAt = Time.time;
                game.StartCountdown();
                Assert.That(game.IsCountingDown, Is.True);
                Assert.That(game.IsRunning, Is.False);
                Assert.That(game.CountdownNumber, Is.EqualTo(3));
                var seen = new System.Collections.Generic.List<int> { game.CountdownNumber };
                float deadline = Time.realtimeSinceStartup + ChainRushGame.CountdownSeconds + 2f;
                while (game.IsCountingDown && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    int number = game.CountdownNumber;
                    if (number > 0 && number != seen[seen.Count - 1]) seen.Add(number);
                    Assert.That(game.Tick, Is.Zero, "No tick may run during the countdown.");
                }
                Assert.That(game.IsRunning, Is.True, "The run must start when the countdown ends.");
                Assert.That(seen, Is.EqualTo(new[] { 3, 2, 1 }));
                Assert.That(Time.time - startedAt, Is.GreaterThanOrEqualTo(ChainRushGame.CountdownSeconds - 0.05f));
                Assert.That(finished, Is.EqualTo(1));
                Assert.That(tickAtFinish, Is.Zero, "CountdownFinished comes before the run's first tick.");
                Assert.That(game.CountdownNumber, Is.Zero);
                yield return new WaitForSeconds(0.3f);
                Assert.That(finished, Is.EqualTo(1));
                Assert.That(game.Tick, Is.GreaterThan(0));
            }
            finally
            {
                game.CountdownFinished -= onFinished;
            }
        }

        // A run a countdown starts begins exactly as one started at once, wherever the runner was before: on the start line,
        // facing the track. Counting down again starts the count over.
        [UnityTest]
        public IEnumerator StartCountdown_TheRunBeginsAsOneStartedAtOnce_AndAgainStartsTheCountOver()
        {
            game.StartRun();
            Vector3 atOnce = player.transform.position;
            float headingAtOnce = player.Heading;
            yield return new WaitForSeconds(0.3f);
            MovePlayer(new Vector3(3f, 1f, 30f));
            game.StartCountdown();
            Assert.That(player.transform.position, Is.EqualTo(atOnce), "The runner waits on the start line.");
            yield return new WaitForSeconds(1.5f);
            Assert.That(game.CountdownNumber, Is.EqualTo(2).Or.EqualTo(1));
            game.StartCountdown();
            Assert.That(game.CountdownNumber, Is.EqualTo(3), "Counting down again starts from 3.");
            Vector3 atStart = default;
            float headingAtStart = float.NaN;
            System.Action onFinished = () => { atStart = player.transform.position; headingAtStart = player.Heading; };
            game.CountdownFinished += onFinished;
            try
            {
                yield return WaitOutCountdown();
                Assert.That(game.IsRunning, Is.True);
                Assert.That(BitsOf(atStart), Is.EqualTo(BitsOf(atOnce)), "The run must begin where one started at once begins.");
                Assert.That(System.BitConverter.SingleToInt32Bits(headingAtStart), Is.EqualTo(System.BitConverter.SingleToInt32Bits(headingAtOnce)));
            }
            finally
            {
                game.CountdownFinished -= onFinished;
            }
        }

        private static (int, int, int) BitsOf(Vector3 v) =>
            (System.BitConverter.SingleToInt32Bits(v.x), System.BitConverter.SingleToInt32Bits(v.y), System.BitConverter.SingleToInt32Bits(v.z));

        // Until the countdown is over (with a second to spare; a countdown that never ends fails the caller's next check).
        private IEnumerator WaitOutCountdown()
        {
            float deadline = Time.realtimeSinceStartup + ChainRushGame.CountdownSeconds + 1f;
            while (game.IsCountingDown && Time.realtimeSinceStartup < deadline) yield return null;
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
