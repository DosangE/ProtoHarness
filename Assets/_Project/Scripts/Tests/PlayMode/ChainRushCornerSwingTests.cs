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
    // Corner pole swing, the drift of this game (NEXT_REQUESTS.md R4b, PROPOSAL_CORNER_POLE.md 2, COURSE.md 4-5): every arc has
    // a pole at its center; a swing key press hooks it, the runner circles it while the key is held, steering shortens
    // (toward the pole) or lets out the rope by 6 m/s, the speed is the hook's speed times the hooked rope over the rope now
    // (0.8 .. 1.5 times), every 35 m swung fills a gauge slot, and letting go carries the runner straight on, the part
    // above running speed falling off by 3 m/s a second. Runs on visible R30 test roads beside the bench course, steered by
    // TrackFollower until the hook; while swinging the bot sends a fixed steer (Override), since steering works the rope.
    // On these roads the pole is (RoadX + 30, 30) and can be hooked from s = 10.1 (closer than R + 6 = 36 m) through the
    // arc (90 degrees aside, within the 95 allowed) to about 2.6 m past it.
    public sealed class ChainRushCornerSwingTests
    {
        private const float RoadX = 300f;
        private const float Radius = 30f;
        private const float HalfWidth = 6f;
        // The open road for rope changes: wide enough for a rope from 22 m (1.5 times faster) to 41 m (0.8 times), no guards.
        private const float OpenHalfWidth = 20f;
        private const float InnerEdge = HalfWidth - 0.38f;
        private const double ArcStart = 30d;
        private const double HookS = 16d;
        private const double WideArcStart = 40d;
        private const float RopeRate = 6f;
        private const float MetersPerSlot = 35f;
        private const float CarryFalloff = 3f;
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
        public IEnumerator Swing_HookedBeforeTheArc_KeepsItsRopeAndSpeedAndFillsTheGaugeBySwungDistance()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot, HalfWidth, true);
            yield return RunTo(line, HookS);
            Vector3 hookedAt = player.transform.position;
            yield return Hook(bot, 0f);
            Assert.That(player.IsSwinging, Is.True, "A swing key press with the pole in reach must hook it.");
            Assert.That(game.Racer.Gauge, Is.LessThan(0.05f), "A hook costs nothing (the gauge only gained the first ticks' swing).");
            Vector3 pole = game.Racer.SwingCenter;
            Assert.That(new Vector2(pole.x, pole.z), Is.EqualTo(new Vector2(Pole.x, Pole.z)).Using(Vector2EqualityComparer(1e-3f)), "The pole is the arc's center.");
            float rope = game.Racer.SwingRadius;
            Assert.That(rope, Is.EqualTo(Level(hookedAt - pole)).Within(0.25f), "The rope is the level distance to the pole when it hooked.");
            Assert.That(game.Racer.SwingHookRadius, Is.EqualTo(rope), "With no steering the rope stays as it hooked.");

            float hookSpeed = game.Racer.SwingHookSpeed;
            float gaugeFrom = game.Racer.Gauge;
            float swung = 0f, minSpeed = float.MaxValue, maxSpeed = 0f, worstRope = 0f, maxOffset = 0f;
            Vector3 last = player.transform.position;
            float deadline = Time.time + 10f;
            while (game.IsRunning && S(line) < ArcEnd - 2d && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(player.IsSwinging, Is.True, "The swing lasts while the swing key is held (s=" + S(line).ToString("0.0") + ").");
                swung += Level(player.transform.position - last);
                last = player.transform.position;
                worstRope = Mathf.Max(worstRope, Mathf.Abs(Level(player.transform.position - pole) - rope));
                minSpeed = Mathf.Min(minSpeed, player.ForwardSpeed);
                maxSpeed = Mathf.Max(maxSpeed, player.ForwardSpeed);
                maxOffset = Mathf.Max(maxOffset, Mathf.Abs(line.Project(player.transform.position).D));
            }
            float gained = game.Racer.Gauge - gaugeFrom;
            Debug.Log("ChainRush pole swing: rope " + rope.ToString("0.00") + " m, hook speed " + hookSpeed.ToString("0.00") + " m/s, speed "
                + minSpeed.ToString("0.00") + ".." + maxSpeed.ToString("0.00") + " m/s, rope error " + worstRope.ToString("0.000") + " m, max offset "
                + maxOffset.ToString("0.00") + " m, swung " + swung.ToString("0.0") + " m, gauge +" + gained.ToString("0.000") + ".");
            Assert.That(S(line), Is.GreaterThanOrEqualTo(ArcEnd - 2d), "Timed out in the arc.");
            Assert.That(worstRope, Is.LessThan(0.05f), "The rope keeps its length while no key works it.");
            Assert.That(maxSpeed, Is.LessThan(hookSpeed + 0.05f), "With the rope as it hooked, the swing keeps the hook's speed.");
            Assert.That(minSpeed, Is.GreaterThan(hookSpeed - 0.05f));
            Assert.That(maxOffset, Is.LessThan(InnerEdge - 0.3f), "Hooked from here, the circle stays on the road through the arc.");
            Assert.That(gained, Is.EqualTo(swung / MetersPerSlot).Within(0.03f), "One gauge slot per 35 m swung.");
        }

        // The R30 arc turns right, so D (steer +1) is the key toward the pole: it shortens the rope and speeds the swing
        // up, A lets the rope out and slows it, and the speed always follows hooked rope / rope now within 0.8 .. 1.5.
        [UnityTest]
        public IEnumerator Swing_InsideKeyShortensTheRopeAndSpeedsUp_OutsideKeyLetsItOut_WithinTheSpeedLimits()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot, OpenHalfWidth, false);
            yield return RunTo(line, HookS);
            yield return Hook(bot, 0f);
            float hookRope = game.Racer.SwingHookRadius;
            float hookSpeed = game.Racer.SwingHookSpeed;
            Assert.That(player.IsSwinging, Is.True);

            bot.Override = 1f;
            float rope = game.Racer.SwingRadius;
            for (int i = 0; i < 25; i++) yield return new WaitForFixedUpdate();
            Assert.That(rope - game.Racer.SwingRadius, Is.EqualTo(25 * RopeRate * Ticks.Seconds).Within(0.01f), "The inside key shortens the rope by 6 m/s.");
            AssertSwingSpeed(hookSpeed, hookRope, "shortened");
            Assert.That(player.ForwardSpeed, Is.GreaterThan(hookSpeed + 0.5f), "A shorter rope is faster.");
            Assert.That(Level(player.transform.position - game.Racer.SwingCenter), Is.EqualTo(game.Racer.SwingRadius).Within(0.05f), "The runner is pulled in with the rope.");

            bot.Override = -1f;
            rope = game.Racer.SwingRadius;
            for (int i = 0; i < 50; i++) yield return new WaitForFixedUpdate();
            Assert.That(game.Racer.SwingRadius - rope, Is.EqualTo(50 * RopeRate * Ticks.Seconds).Within(0.01f), "The outside key lets it out by 6 m/s.");
            AssertSwingSpeed(hookSpeed, hookRope, "let out");
            Assert.That(player.ForwardSpeed, Is.LessThan(hookSpeed - 0.3f), "A longer rope than the hooked one is slower.");

            // Out to 0.8 times the hook's speed (a rope of hookRope / 0.8) and past it: the speed stops there.
            while (game.IsRunning && player.IsSwinging && game.Racer.SwingRadius < hookRope / 0.8f + 1f) yield return new WaitForFixedUpdate();
            Assert.That(player.IsSwinging, Is.True, "The swing must last until the rope is past the slow limit.");
            Assert.That(player.ForwardSpeed, Is.EqualTo(hookSpeed * 0.8f).Within(0.05f), "The speed is not lower than 0.8 times the hook's.");

            bot.Override = 1f;
            while (game.IsRunning && player.IsSwinging && game.Racer.SwingRadius > hookRope / 1.5f - 1f) yield return new WaitForFixedUpdate();
            Assert.That(player.IsSwinging, Is.True, "The swing must last until the rope is past the fast limit.");
            Assert.That(player.ForwardSpeed, Is.EqualTo(hookSpeed * 1.5f).Within(0.05f), "The speed is not higher than 1.5 times the hook's.");
        }

        [UnityTest]
        public IEnumerator Swing_InsideTheArc_ThePoleBesideIsInReach()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot, HalfWidth, true);
            yield return RunTo(line, ArcStart + 5d);
            Assert.That(player.TryFindPole(out _), Is.True, "Inside its arc the pole is about 90 degrees aside, within the 95 allowed.");
            yield return Hook(bot, 0f);
            Assert.That(player.IsSwinging, Is.True);
            Assert.That(player.IsSlingPulling, Is.False, "The swing key never fires the slingshot.");
        }

        [UnityTest]
        public IEnumerator Swing_PoleBeyondReach_ThePressDoesNothing()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot, HalfWidth, true);
            yield return RunTo(line, 6d);
            Assert.That(Level(player.transform.position - Pole), Is.GreaterThan(Radius + 6f), "From here the pole is past R + 6.");
            Assert.That(player.TryFindPole(out _), Is.False);
            game.Racer.AddGauge(1f);
            yield return Hook(bot, 0f);
            Assert.That(player.IsSwinging, Is.False);
            Assert.That(player.IsSlingPulling, Is.False, "Out of reach the press does nothing at all.");
            Assert.That(game.Racer.Gauge, Is.EqualTo(1f).Within(1e-4f), "Nothing is spent.");
        }

        [UnityTest]
        public IEnumerator Swing_KeyHeldIntoReach_DoesNotHookUntilPressed()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot, HalfWidth, true);
            yield return RunTo(line, 6d);
            bot.SwingPress = true;
            bot.SwingHold = true;
            yield return RunTo(line, HookS);
            Assert.That(player.TryFindPole(out _), Is.True);
            Assert.That(player.IsSwinging, Is.False, "Holding the key into reach does not hook: only a press does.");
            bot.SwingHold = false;
            yield return new WaitForFixedUpdate();
            yield return Hook(bot, 0f);
            Assert.That(player.IsSwinging, Is.True);
        }

        // A swing hooks only from the ground (2026-10-19, M1-T play): a press in the middle of a jump does nothing, and the
        // key held into the landing does not hook either. A fresh press on the ground does. At the bench's 20 m/s a jump
        // flies about 21 m, longer than the R30 road's reach before its arc (about 20 m), so this runs on an R60 road: its
        // pole is in reach from about 27.5 m before the arc (60 + 6 m), and the jump lands on the straight.
        [UnityTest]
        public IEnumerator Swing_PressedInTheAir_DoesNotHook_NorWhenHeldIntoTheLanding()
        {
            Centerline line = WideCurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot, HalfWidth, true);
            yield return RunTo(line, WideArcStart - 26d);
            player.PrimaryAction();
            float deadline = Time.time + 1f;
            while (game.IsRunning && player.IsGrounded && Time.time < deadline) yield return new WaitForFixedUpdate();
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.False, "precondition: the runner is in the middle of its jump.");
            Assert.That(player.TryFindPole(out _), Is.True, "precondition: the pole is in reach.");
            yield return Hook(bot, 0f);
            Assert.That(player.IsGrounded, Is.False);
            Assert.That(player.IsSwinging, Is.False, "A press in the air must not hook.");

            deadline = Time.time + 2f;
            while (game.IsRunning && !player.IsGrounded && Time.time < deadline) yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.True, "The jump must land before the arc.");
            Assert.That(S(line), Is.LessThan(WideArcStart), "precondition: landed before the arc.");
            Assert.That(player.TryFindPole(out _), Is.True);
            Assert.That(player.IsSwinging, Is.False, "The key held into the landing does not hook: only a press does.");
            bot.SwingHold = false;
            yield return new WaitForFixedUpdate();
            yield return Hook(bot, 0f);
            Assert.That(player.IsSwinging, Is.True, "A press on the ground hooks.");
        }

        [UnityTest]
        public IEnumerator Swing_LettingGo_GoesStraightOnAtTheSwingSpeed_ThenTheExtraFallsOffByThreeASecond()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot, OpenHalfWidth, false);
            yield return RunTo(line, HookS);
            yield return Hook(bot, 1f);
            // A rope about 9 m shorter: about 1.4 times the hook's speed, so the carry is still above run speed a second later.
            for (int i = 0; i < 75; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.IsSwinging, Is.True);
            float swingSpeed = player.ForwardSpeed;
            Assert.That(swingSpeed, Is.GreaterThan(game.Racer.SwingHookSpeed + 0.5f), "precondition: the shortened rope swings faster than the hook.");
            bot.Override = 0f;
            bot.SwingHold = false;
            yield return new WaitForFixedUpdate();
            Assert.That(player.IsSwinging, Is.False, "Letting go of the swing key ends the swing.");
            float heading = player.Heading;
            float carried = player.ForwardSpeed;
            Assert.That(carried, Is.EqualTo(swingSpeed - CarryFalloff * Ticks.Seconds).Within(0.02f), "It goes on at the swing's speed, less one tick's falloff.");
            for (int i = 0; i < 50; i++) yield return new WaitForFixedUpdate();
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(player.Heading, heading)), Is.LessThan(0.5f), "With no steering it goes straight on.");
            Assert.That(player.ForwardSpeed, Is.EqualTo(carried - 50 * CarryFalloff * Ticks.Seconds).Within(0.02f), "The extra speed falls off by 3 m/s a second.");
            Assert.That(game.Racer.SwingCarrySpeed, Is.GreaterThan(10f), "Still above the run speed after a second.");
        }

        // A booster fired while swinging speeds the swing up like a booster on the ground (user 2026-10-19 "C"): the hook's
        // speed rises toward the pull's 28 m/s, the rope's ratio still applies on top. Until then the swing ignored it and the
        // booster's time ran out unused while the key was held.
        [UnityTest]
        public IEnumerator Swing_BoosterWhileSwinging_SpeedsTheSwingUp()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot, OpenHalfWidth, false);
            yield return RunTo(line, HookS);
            yield return Hook(bot, 0f);
            Assert.That(player.IsSwinging, Is.True);
            float hookSpeed = game.Racer.SwingHookSpeed;
            Assert.That(hookSpeed, Is.LessThan(player.SlingTopSpeed - 4f), "precondition: room below the booster's speed.");
            game.Racer.AddGauge(1f);
            float gaugeBefore = game.Racer.Gauge;
            bot.ChainAction = true;
            for (int i = 0; i < 25; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.IsSwinging, Is.True, "A booster does not end the swing.");
            // The swing goes on filling the gauge (about 0.3 of a slot in these 0.5 s), so the slot shows as a drop of over half.
            Assert.That(game.Racer.Gauge, Is.LessThan(gaugeBefore - 0.5f), "The booster spent a slot.");
            Debug.Log("ChainRush pole swing booster: hook speed " + hookSpeed.ToString("0.00") + " -> " + game.Racer.SwingHookSpeed.ToString("0.00")
                + " m/s after 0.5 s, swing speed " + player.ForwardSpeed.ToString("0.00") + " m/s.");
            Assert.That(game.Racer.SwingHookSpeed, Is.GreaterThan(hookSpeed + 2f), "Half a second of booster speeds the swing up.");
            Assert.That(game.Racer.SwingHookSpeed, Is.LessThanOrEqualTo(player.SlingTopSpeed), "No faster than the booster's own speed.");
            AssertSwingSpeed(game.Racer.SwingHookSpeed, game.Racer.SwingHookRadius, "boosted");
        }

        // Held past the arc the runner keeps circling, crosses the straight after it and meets the inside guard there
        // (rope 33.1 m around a pole 30 m from the centerline: about 23 m into the straight). The guard ends the swing
        // without the carry.
        [UnityTest]
        public IEnumerator Swing_HeldPastTheArc_CirclesOnUntilAGuard_ThenStopsWithoutTheCarry()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot, HalfWidth, true);
            yield return RunTo(line, HookS);
            yield return Hook(bot, 0f);
            float hookSpeed = game.Racer.SwingHookSpeed;
            yield return RunTo(line, ArcEnd + 2d);
            Assert.That(player.IsSwinging, Is.True, "Leaving the arc does not end a held swing.");
            float deadline = Time.time + 6f;
            while (game.IsRunning && player.IsSwinging && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(player.IsSwinging, Is.False, "The circle must reach a guard.");
            Assert.That(bot.SwingHold, Is.True, "The key was still held: the guard ended it.");
            TrackCoord at = line.Project(player.transform.position);
            Debug.Log("ChainRush pole swing: guard ended the swing at s=" + at.S.ToString("0.0") + " (arc end " + ArcEnd.ToString("0.0")
                + "), d=" + at.D.ToString("0.00") + ", speed " + player.ForwardSpeed.ToString("0.00") + " m/s.");
            Assert.That(at.S, Is.InRange(ArcEnd + 10d, ArcEnd + 40d));
            Assert.That(at.D, Is.GreaterThan(InnerEdge - 0.5f), "It meets the inside (right) guard.");
            Assert.That(player.ForwardSpeed, Is.LessThan(hookSpeed + 0.1f), "A guard ends the swing without speed to carry.");
            Assert.That(game.Racer.SwingCarrySpeed, Is.EqualTo(0f));
        }

        // Presentation only (SwingLook): the body slews into the curve, the chain bites the pole's band, the root never turns.
        // Until R5 (2026-10-18) the chain bit the inside rail beside the runner; R5 moved it to the pole by design.
        [UnityTest]
        public IEnumerator SwingLook_WhileSwinging_TurnsOnlyTheBodyInsideAndHooksThePoleTop()
        {
            Centerline line = CurveRoad();
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot, HalfWidth, true);
            Transform body = player.GetComponent<RunnerTilt>().Body;
            LineRenderer chain = player.transform.Find("Chain Line").GetComponent<LineRenderer>();
            Quaternion root = player.transform.rotation;
            yield return RunTo(line, HookS);
            yield return Hook(bot, 0f);
            // Into the arc and past the pose's blend time.
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
            Assert.That(new Vector2(bite.x, bite.z), Is.EqualTo(new Vector2(Pole.x, Pole.z)).Using(Vector2EqualityComparer(1e-3f)), "The chain must bite the pole at the arc's center.");
            Assert.That(bite.y - game.Racer.SwingCenter.y, Is.EqualTo(SwingLook.BiteHeight).Within(1e-3f), "It bites the pole's band near hand height.");

            bot.SwingHold = false;
            yield return new WaitForSeconds(SwingLook.BlendTime + 0.25f);
            Assert.That(player.IsSwinging, Is.False);
            yield return new WaitForEndOfFrame();
            upright = Quaternion.Euler(0f, player.Heading, 0f) * RunnerTilt.Evaluate(player.Velocity, player.Steer);
            Assert.That(Quaternion.Angle(body.rotation, upright), Is.LessThan(3f), "After the swing the body must face the heading again.");
            Assert.That(Quaternion.Angle(player.transform.rotation, root), Is.LessThan(1e-3f));
        }

        // A pole beside a straight (Centerline.AddPole, user 2026-10-22, the Skyway's wide straight) hooks like a corner pole:
        // on a road 30 m across, abeam of a pole 11 m to the left, the runner circles it to the left, stays on the road and
        // fills the gauge by the distance swung; letting go ends the swing.
        [UnityTest]
        public IEnumerator Swing_PoleBesideAStraight_HooksAbeamCirclesTowardItAndFillsTheGauge()
        {
            const float sideHalfWidth = 15f, poleOffset = -11f, poleReach = 10f;
            const double poleS = 50d;
            var line = new Centerline(new Vector3(RoadX, 0f, 0f), 0f);
            line.AppendStraight(120f);
            Vector3 sidePole = line.FrameAt(poleS).TransformPoint(new Vector3(poleOffset, 0f, 0f));
            line.AddPole(sidePole, poleReach);
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot, sideHalfWidth, true);
            yield return RunTo(line, poleS - 25d);
            Assert.That(player.TryFindPole(out _), Is.False, "27 m away the pole is past 10 + 6 m.");
            yield return RunTo(line, poleS);
            Assert.That(player.TryFindPole(out Vector3 offered), Is.True, "Abeam, 11 m aside, the pole is in reach.");
            Assert.That(Level(offered - sidePole), Is.LessThan(1e-3f));

            float headingFrom = player.Heading;
            Vector3 hookedAt = player.transform.position;
            yield return Hook(bot, 0f);
            Assert.That(player.IsSwinging, Is.True, "A swing key press with the side pole in reach must hook it.");
            Assert.That(Level(game.Racer.SwingCenter - sidePole), Is.LessThan(1e-3f), "It hooks the side pole.");
            Assert.That(game.Racer.SwingRadius, Is.EqualTo(Level(hookedAt - sidePole)).Within(0.5f), "The rope is the distance when it hooked.");

            // Counted from here, as Swing_HookedBeforeTheArc does: the hook's own ticks have swung already.
            float gaugeFrom = game.Racer.Gauge;
            float swung = 0f, maxOffset = 0f;
            Vector3 last = player.transform.position;
            for (int i = 0; i < 30 && game.IsRunning; i++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(player.IsSwinging, Is.True, "The swing lasts while the key is held.");
                swung += Level(player.transform.position - last);
                last = player.transform.position;
                maxOffset = Mathf.Max(maxOffset, Mathf.Abs(line.Project(player.transform.position).D));
            }
            float turned = Mathf.DeltaAngle(headingFrom, player.Heading);
            float gained = game.Racer.Gauge - gaugeFrom;
            Debug.Log("ChainRush side pole swing: rope " + game.Racer.SwingRadius.ToString("0.00") + " m, turned " + turned.ToString("0.0")
                + " deg, swung " + swung.ToString("0.0") + " m, gauge +" + gained.ToString("0.000") + ", max offset " + maxOffset.ToString("0.00") + " m.");
            Assert.That(turned, Is.LessThan(-30f), "Circling a pole on the left turns the runner left.");
            Assert.That(maxOffset, Is.LessThan(sideHalfWidth - 1f), "The circle stays on the wide road.");
            Assert.That(gained, Is.EqualTo(swung / MetersPerSlot).Within(0.03f), "One gauge slot per 35 m swung.");

            bot.SwingHold = false;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(player.IsSwinging, Is.False, "Letting go of the key ends the swing.");
        }

        // Presses the swing key and keeps it held with a fixed steer (it works the rope while swinging), then waits two
        // ticks for the press to land.
        private static IEnumerator Hook(TrackFollower bot, float steer)
        {
            bot.Steering = false;
            bot.Override = steer;
            bot.SwingPress = true;
            bot.SwingHold = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        // The swing's speed is the hook's speed times the hooked rope over the rope now, within 0.8 .. 1.5 times.
        private void AssertSwingSpeed(float hookSpeed, float hookRope, string when)
        {
            float expected = hookSpeed * Mathf.Clamp(hookRope / game.Racer.SwingRadius, 0.8f, 1.5f);
            Assert.That(player.ForwardSpeed, Is.EqualTo(expected).Within(0.02f), "Swing speed after the rope was " + when + ".");
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

        // Straight 40 m, an R60 right-hand quarter arc, straight 40 m: a longer reach before the arc than CurveRoad's.
        private static Centerline WideCurveRoad()
        {
            var line = new Centerline(new Vector3(RoadX, 0f, 0f), 0f);
            line.AppendStraight((float)WideArcStart);
            line.AppendArc(60f, 90f);
            line.AppendStraight(40f);
            return line;
        }

        private IEnumerator StartOn(Centerline line, TrackFollower bot, float halfWidth, bool guards)
        {
            TestRoad.Build(line, -5d, line.EndS, halfWidth, guards, built);
            bot.ChainAction = false;
            bot.SwingPress = false;
            bot.SwingHold = false;
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
