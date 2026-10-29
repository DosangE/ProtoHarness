using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Track;
using ProtoHarness.ChainRush.Visuals;
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
        // Holds the throttle and nothing else, for the tests that need a running runner but not the keys: running needs
        // the throttle since 2026-10-19, and the default keyboard source reads it as let go when no key is down.
        private sealed class FullThrottleInputSource : IInputSource
        {
            public void Poll() { }
            public TickInput Consume() => new TickInput(0f, false, false, false);
            public void Clear() { }
        }

        // Scripted edges are consumed once on the next tick, while the throttle stays held like FullThrottleInputSource.
        private sealed class FeedbackInputSource : IInputSource
        {
            private bool primary;
            private bool boost;

            public void PressPrimary() => primary = true;
            public void PressBoost() => boost = true;
            public void Poll() { }
            public TickInput Consume()
            {
                var input = new TickInput(0f, primary, false, false, chainActionPressed: boost);
                Clear();
                return input;
            }
            public void Clear() { primary = false; boost = false; }
        }

        private ChainRushGame game;
        private RunnerMotor player;
        private GrappleController grapple;
        private ChainRushHud hud;
        private ControlFeedback feedback;
        private float previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
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
                if (root.TryGetComponent(out ChainRushHud foundHud)) hud = foundHud;
            }
            Assert.That(game, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(grapple, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.gameObject, Is.EqualTo(game.gameObject), "The HUD belongs to the game root.");
            feedback = hud.GetComponent<ControlFeedback>();
            Assert.That(feedback, Is.Not.Null);
            Assert.That(feedback.enabled, Is.True);
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
            game.SetInputSource(new FullThrottleInputSource());
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
            // Running toward the anchor needs the throttle; jumping in place no longer reaches it (GrappleJumpZ).
            game.SetInputSource(new FullThrottleInputSource());
            game.StartRun();
            MovePlayer(new Vector3(0f, 1f, GrappleJumpZ));
            yield return new WaitForFixedUpdate();
            player.PrimaryAction();
            yield return WaitForAnchorInReach();
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
        public IEnumerator ChainRushHud_BoosterSlotSpent_FlashesThenFades()
        {
            var input = new FeedbackInputSource();
            game.SetInputSource(input);
            game.StartRun();
            game.Racer.AddGauge(1f);
            input.PressBoost();
            float deadline = Time.time + 0.5f;
            while (hud.BoostFlash <= 0f && Time.time < deadline) yield return null;
            Assert.That(player.Gauge, Is.EqualTo(0f).Within(0.001f), "The chain action must spend the prepared slot.");
            Assert.That(hud.BoostFlash, Is.GreaterThan(0f));
            yield return new WaitForSeconds(FeedbackLook.BoostFlashTime + 0.05f);
            Assert.That(hud.BoostFlash, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ChainRushHud_GrappleAttached_ShowsHookFlash()
        {
            var input = new FeedbackInputSource();
            game.SetInputSource(input);
            game.StartRun();
            MovePlayer(new Vector3(0f, 1f, GrappleJumpZ));
            yield return new WaitForFixedUpdate();
            input.PressPrimary();
            yield return WaitForAnchorInReach();
            Assert.That(player.IsGrounded, Is.False);
            input.PressPrimary();
            float deadline = Time.time + 0.5f;
            while (hud.HookFlash <= 0f && Time.time < deadline) yield return null;
            Assert.That(grapple.IsAttached, Is.True);
            Assert.That(hud.HookFlash, Is.GreaterThan(0f));
        }

        // The bench has no corner poles. Build the same R30 road as its Shift/Ctrl test here, without device events.
        [UnityTest]
        public IEnumerator ControlFeedback_PoleWithinReach_ShowsReadyRingAndHidesWhenPaused()
        {
            var built = new List<Object>();
            try
            {
                var line = new Centerline(new Vector3(300f, 0f, 0f), 0f);
                line.AppendStraight(30f);
                line.AppendArc(30f, 90f);
                line.AppendStraight(40f);
                TestRoad.Build(line, -5d, line.EndS, 6f, true, built);
                game.SetTrack(line);
                game.SetInputSource(new FullThrottleInputSource());
                game.StartRun();
                MovePlayer(line.FrameAt(16d).TransformPoint(new Vector3(0f, 1.2f, 0f)));
                player.FaceTrack();
                Physics.SyncTransforms();
                Assert.That(player.TryFindPole(out _), Is.True, "The R30 road must offer its corner pole here.");
                float deadline = Time.time + 0.5f;
                while (!feedback.PoleReadyShown && Time.time < deadline) yield return null;
                Assert.That(grapple.HasPoleCandidate, Is.True);
                Assert.That(feedback.PoleReadyShown, Is.True);
                game.TogglePause();
                yield return null;
                Assert.That(feedback.PoleReadyShown, Is.False);
                Assert.That(feedback.TrailEmitting, Is.False);
            }
            finally
            {
                if (game.IsPaused) game.TogglePause();
                for (int i = 0; i < built.Count; i++) if (built[i] != null) Object.Destroy(built[i]);
            }
        }

        // A second miss while the first still counts (JustMissed lasts 0.75 s and nothing stops another press) shakes
        // again (Codex review 2026-10-29).
        [UnityTest]
        public IEnumerator ChainRushHud_SecondMissWhileTheFirstStillCounts_ShakesAgain()
        {
            game.SetInputSource(new FullThrottleInputSource());
            game.StartRun();
            MovePlayer(new Vector3(50f, 5f, 12f));
            yield return new WaitForFixedUpdate();
            Assert.That(grapple.TryAttach(), Is.False);
            yield return null;
            Assert.That(hud.MissShake, Is.GreaterThan(0f));
            yield return new WaitForSeconds(FeedbackLook.MissShakeTime + 0.05f);
            Assert.That(hud.MissShake, Is.Zero);
            Assert.That(grapple.JustMissed, Is.True, "precondition: the first miss still counts.");
            Assert.That(player.IsGrounded, Is.False, "precondition: still in the air.");
            Assert.That(grapple.TryAttach(), Is.False);
            yield return null;
            Assert.That(hud.MissShake, Is.GreaterThan(0f));
        }

        // A booster spent while another still runs (the slingshot pulling as the grapple it led into is empowered with a
        // second slot) shows again: each spend is its own moment (Codex review 2026-10-29).
        [UnityTest]
        public IEnumerator ChainRushHud_GrappleEmpoweredWhileTheSlingshotPulls_FlashesAgain()
        {
            var input = new FeedbackInputSource();
            game.SetInputSource(input);
            game.StartRun();
            MovePlayer(new Vector3(0f, 1f, GrappleJumpZ));
            yield return new WaitForFixedUpdate();
            input.PressPrimary();
            yield return WaitForAnchorInReach();
            game.Racer.AddGauge(2f);
            input.PressBoost();
            float deadline = Time.time + 0.5f;
            while (!player.IsSlingPulling && Time.time < deadline) yield return null;
            Assert.That(player.IsSlingPulling, Is.True, "precondition: the slingshot fired in the air.");
            yield return null;
            Assert.That(feedback.SinceBoost(Time.time), Is.GreaterThanOrEqualTo(0f));
            float firstAt = Time.time - feedback.SinceBoost(Time.time);
            Assert.That(grapple.TryAttach(), Is.True);
            input.PressBoost();
            deadline = Time.time + 0.5f;
            while (!game.Racer.GrappleEmpowered && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(game.Racer.GrappleEmpowered, Is.True, "precondition: the second slot empowered the grapple.");
            Assert.That(player.IsSlingPulling, Is.True, "precondition: the slingshot was still pulling.");
            yield return null;
            float secondAt = Time.time - feedback.SinceBoost(Time.time);
            Assert.That(secondAt, Is.GreaterThan(firstAt), "The second spend must start the booster effect again.");
            Assert.That(hud.BoostFlash, Is.GreaterThan(0f));
            Assert.That(player.Gauge, Is.EqualTo(0f).Within(0.001f));
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
        public IEnumerator Grapple_SecondPressInTheSameAir_IsRefusedWithoutAMissUntilLanding()
        {
            game.SetInputSource(new FullThrottleInputSource());
            game.StartRun();
            MovePlayer(new Vector3(0f, 1f, GrappleJumpZ));
            yield return new WaitForFixedUpdate();
            player.PrimaryAction();
            yield return WaitForAnchorInReach();
            Assert.That(grapple.TryAttach(), Is.True);
            grapple.Release(false);
            Assert.That(player.IsGrounded, Is.False);
            Assert.That(grapple.TryAttach(), Is.False, "The anchor is still in range, but this time in the air already had its grapple.");
            Assert.That(grapple.JustMissed, Is.False);
            Assert.That(game.Grapples, Is.EqualTo(1));
            yield return null;
            Assert.That(grapple.Candidate, Is.Null);
            // Put the runner back over solid road at rest, so the jump's leftover climb does not keep it in the air.
            MovePlayer(new Vector3(0f, 1f, 10f));
            game.Racer.Velocity = Vector3.zero;
            float deadline = Time.time + 1f;
            while (!player.IsGrounded && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.True,
                $"Never landed: position {player.transform.position}, velocity {game.Racer.Velocity}.");
            Assert.That(game.Racer.AirGrappleUsed, Is.False);
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

        // Device: depends on virtual keyboard event delivery, which failed repeatedly in the editor
        // (docs/DECISIONS.md 2026-10-04). Excluded from the merge gate run; run it with the Device menu.
        // KartRider-style keys with no mouse (2026-10-18): arrows steer, left Alt jumps and grapples, letting go of it releases.
        // The up arrow is the throttle (2026-10-19): held through every step that needs the runner moving.
        [UnityTest]
        [Category("Device")]
        public IEnumerator Input_ArrowKeys_StartsSteersJumpsGrapplesAndRestarts()
        {
            Keyboard originalKeyboard = Keyboard.current;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                // Enter counts down 3, 2, 1, then the run starts.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return new WaitForSeconds(0.15f);
                Assert.That(game.IsCountingDown, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return WaitOutCountdown();
                Assert.That(game.IsRunning, Is.True);
                float startZ = player.transform.position.z;
                yield return new WaitForSeconds(0.3f);
                Assert.That(player.transform.position.z - startZ, Is.LessThan(0.1f), "Without the up arrow the runner stays on the start line.");
                // The bench runs circuit physics (2026-10-19): the turn rate is held to speed / 22 m, so a runner at rest
                // barely turns. Get up to speed on the up arrow first.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
                yield return new WaitForSeconds(0.7f);
                Assert.That(player.transform.position.z - startZ, Is.GreaterThan(2f), "The up arrow is the throttle.");
                // Free steering (2026-10-05, T2a): the left and right arrows turn the facing.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow, Key.LeftArrow));
                yield return new WaitForSeconds(0.3f);
                float leftHeading = player.Heading;
                Assert.That(leftHeading, Is.LessThan(-10f));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow, Key.RightArrow));
                yield return new WaitForSeconds(0.4f);
                Assert.That(player.Heading, Is.GreaterThan(leftHeading + 10f));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
                MovePlayer(new Vector3(0f, 1f, GrappleJumpZ));
                player.FaceTrack();
                yield return new WaitForFixedUpdate();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow, Key.LeftAlt));
                yield return new WaitForSeconds(0.15f);
                Assert.That(player.IsGrounded, Is.False);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
                // Left Alt again once the anchor is in reach (about 9 m since 2026-10-19, see GrappleJumpZ).
                yield return WaitForAnchorInReach();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow, Key.LeftAlt));
                yield return new WaitForSeconds(0.1f);
                Assert.That(grapple.IsAttached, Is.True);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChainRush-grapple.png"));
                yield return new WaitForSeconds(0.05f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
                yield return new WaitForSeconds(0.05f);
                Assert.That(grapple.IsAttached, Is.False);
                // R no longer restarts (user 2026-10-20): the run goes on.
                int tickBeforeR = game.Tick;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow, Key.R));
                yield return new WaitForSeconds(0.15f);
                Assert.That(game.IsRunning, Is.True, "R must not restart the run.");
                Assert.That(game.Tick, Is.GreaterThan(tickBeforeR));
                Assert.That(game.Grapples, Is.GreaterThan(0));
                // ESC opens the pause menu; ↓ then Enter picks 재시작, which puts the runner back on the start line and
                // counts down again.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return new WaitForSeconds(0.05f);
                Assert.That(game.IsPaused, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.DownArrow));
                yield return new WaitForSeconds(0.05f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
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
                if (originalKeyboard != null) originalKeyboard.MakeCurrent();
            }
        }

        // Device: left Shift pressed with a corner pole in reach hooks it, the right arrow held with it shortens the rope of a right-hand
        // corner, letting go of Shift ends the swing; left Ctrl spends a slot (NEXT_REQUESTS.md R4b). On an R30 test road
        // beside the bench course (as ChainRushCornerSwingTests): straight 30 m, then the arc, its pole in reach from s = 10.1.
        [UnityTest]
        [Category("Device")]
        public IEnumerator Input_ShiftAndCtrl_SwingsOnThePoleAndFiresChainAction()
        {
            Keyboard originalKeyboard = Keyboard.current;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var built = new List<Object>();
            try
            {
                var line = new Centerline(new Vector3(300f, 0f, 0f), 0f);
                line.AppendStraight(30f);
                line.AppendArc(30f, 90f);
                line.AppendStraight(40f);
                TestRoad.Build(line, -5d, line.EndS, 6f, true, built);
                game.SetTrack(line);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return new WaitForSeconds(0.15f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return WaitOutCountdown();
                Assert.That(game.IsRunning, Is.True);
                // The up arrow is the throttle (2026-10-19): held while the runner has to get to the pole.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
                player.ShiftOrigin(line.FrameAt(5d).TransformPoint(new Vector3(0f, 1.2f, 0f)) - player.transform.position);
                Physics.SyncTransforms();
                float deadline = Time.time + 4f;
                while (game.IsRunning && line.Project(player.transform.position).S < 16d && Time.time < deadline) yield return new WaitForFixedUpdate();
                Assert.That(line.Project(player.transform.position).S, Is.GreaterThanOrEqualTo(16d), "The runner never reached the hook point on the test road.");
                Assert.That(player.TryFindPole(out _), Is.True, "precondition: the pole is in reach here.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow, Key.LeftShift));
                yield return new WaitForSeconds(0.1f);
                Assert.That(player.IsSwinging, Is.True, "Pressing left Shift with a pole in reach must hook it.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow, Key.LeftShift, Key.RightArrow));
                yield return new WaitForSeconds(0.2f);
                Assert.That(player.IsSwinging, Is.True, "The swing lasts while left Shift is held.");
                Assert.That(game.Racer.SwingRadius, Is.LessThan(game.Racer.SwingHookRadius - 0.5f), "The right arrow is the key toward the pole of a right-hand corner: it shortens the rope.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
                yield return new WaitForSeconds(0.1f);
                Assert.That(player.IsSwinging, Is.False, "Letting go of left Shift ends the swing.");
                game.Racer.AddGauge(1f);
                float before = game.Racer.Gauge;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow, Key.LeftCtrl));
                yield return new WaitForSeconds(0.1f);
                Assert.That(game.Racer.Gauge, Is.EqualTo(before - 1f).Within(0.05f), "Left Ctrl must spend one slot.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (originalKeyboard != null) originalKeyboard.MakeCurrent();
                for (int i = 0; i < built.Count; i++) if (built[i] != null) Object.Destroy(built[i]);
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

        [UnityTest, Category("Enemy")]
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
            game.SetInputSource(new FullThrottleInputSource());
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

        // The bench deck ends at z 48 and its anchor hangs at z 56, 10 m up. Since 2026-10-19 an anchor is in reach only
        // within its height over the track less standing height (GrappleController.minHangHeight): about 9 m, where it was
        // 20 m. A jump from rest 1 m before the edge brings it in reach in the air.
        private const float GrappleJumpZ = 47f;

        // Waits (up to 1 s) for the grapple to offer an anchor, then the test presses. Pressing earlier would be a miss.
        private IEnumerator WaitForAnchorInReach()
        {
            float deadline = Time.time + 1f;
            while (grapple.Candidate == null && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(grapple.Candidate, Is.Not.Null, $"No anchor came within the rope's reach: runner at {player.transform.position}.");
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
