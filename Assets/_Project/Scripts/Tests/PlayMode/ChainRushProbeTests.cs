using System;
using System.Collections;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Track;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    // Measurements for play reports that have no cause yet (user 2026-10-19): they log numbers and assert only that the
    // run went as planned. Explicit: run by name, not in the gate. A finding becomes a real test with its fix.
    //   Grapple dip: "a grapple gets released by itself at some point; if the runner sped up on the way it nearly fell".
    //   Swing hook jump: "entering a corner with a swing, the runner jumps to the rope's length, not where it was".
    [Explicit]
    public sealed class ChainRushProbeTests
    {
        private const string CircuitScene = "Assets/_Project/Scenes/ChainRushCircuit.unity";

        private ChainRushGame game;
        private RunnerMotor player;
        private GrappleController grapple;
        private CircuitRace circuit;
        private float timeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            timeScale = Time.timeScale;
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(CircuitScene, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("Probes require the Unity Editor.");
            yield break;
#endif
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame g)) game = g;
                if (root.TryGetComponent(out RunnerMotor p)) { player = p; grapple = p.GetComponent<GrappleController>(); }
                if (root.TryGetComponent(out CircuitRace c)) circuit = c;
            }
            Assert.That(circuit, Is.Not.Null);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = timeScale;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // The bot over the grapple gap: plain, with a booster fired before it, and with the grapple empowered.
        [UnityTest, Timeout(180000)]
        public IEnumerator Probe_GrappleGap_HowLowTheRunnerHangsAndWhereItLetsGo()
        {
            foreach (string mode in new[] { "plain", "booster 40 m before", "booster 15 m before", "empowered grapple" })
            {
                var bot = new ProbeBot(new CircuitBot(game, player, grapple, circuit), game, player, circuit);
                if (mode == "booster 40 m before") bot.BoostBeforeGap = 40d;
                if (mode == "booster 15 m before") bot.BoostBeforeGap = 15d;
                if (mode == "empowered grapple") bot.EmpowerGrapple = true;
                bool wasAttached = false;
                float attachRope = 0f, minH = float.MaxValue, attachSpeed = 0f;
                string release = "none";
                Action<int> sample = tick =>
                {
                    TrackCoord here = game.Track.Project(player.transform.position);
                    if (grapple.IsAttached)
                    {
                        if (!wasAttached) { attachRope = game.Racer.RopeLength; attachSpeed = player.HorizontalSpeed; }
                        minH = Mathf.Min(minH, here.H);
                    }
                    else if (wasAttached)
                    {
                        release = $"tick {tick} s {here.S:0.0} h {here.H:0.00} speed {player.HorizontalSpeed:0.00} vy {player.Velocity.y:0.00}";
                    }
                    wasAttached = grapple.IsAttached;
                };
                game.TickCompleted += sample;
                // Two booster slots from the start, so the booster and the empowered grapple have gauge to spend.
                yield return RunUntil(bot, () => circuit.Laps.Progress > 345d || game.HasFailed, 60f, () => game.Racer.AddGauge(2f));
                game.TickCompleted -= sample;
                Debug.Log($"ChainRush probe grapple ({mode}): attach rope {attachRope:0.00} m at {attachSpeed:0.00} m/s, lowest {minH:0.00} m (track-relative), released {release}, respawned {player.IsRespawning}.");
                game.FailRun();
            }
        }

        // The bot round one lap, hooking each corner's pole as soon as it comes in reach and holding the swing 0.6 s.
        // Per tick: how far the runner moved beyond what its speed allows, and the rope against the real distance.
        [UnityTest, Timeout(180000)]
        public IEnumerator Probe_SwingHook_PositionJumpAtEachCorner()
        {
            var bot = new ProbeBot(new CircuitBot(game, player, grapple, circuit), game, player, circuit) { SwingCorners = true };
            Vector3 last = Vector3.zero;
            float lastSpeed = 0f;
            int hooks = 0;
            float worstJump = 0f;
            string worstAt = "none";
            bool wasSwinging = false;
            Camera view = Camera.main;
            Vector3 lastCameraOffset = Vector3.zero;
            float worstCamera = 0f;
            Action<int> sample = tick =>
            {
                Vector3 now = player.transform.position;
                float moved = (now - last).magnitude;
                float allowed = Mathf.Max(lastSpeed, player.Velocity.magnitude) * Ticks.Seconds + 0.05f;
                if (tick > 1 && moved - allowed > worstJump)
                {
                    worstJump = moved - allowed;
                    worstAt = $"tick {tick} s {game.Track.Project(now).S:0.0} moved {moved:0.00} m (speed allows {allowed:0.00}), swinging {player.IsSwinging}, just hooked {player.IsSwinging && !wasSwinging}";
                }
                if (player.IsSwinging && !wasSwinging)
                {
                    hooks++;
                    Vector3 pole = game.Racer.SwingCenter;
                    float distance = new Vector2(now.x - pole.x, now.z - pole.z).magnitude;
                    Debug.Log($"ChainRush probe swing hook {hooks}: tick {tick} s {game.Track.Project(now).S:0.0}, rope {game.Racer.SwingRadius:0.00} m, distance now {distance:0.00} m, moved this tick {moved:0.00} m.");
                }
                wasSwinging = player.IsSwinging;
                last = now;
                lastSpeed = player.Velocity.magnitude;
            };
            game.TickCompleted += sample;
            game.SetInputSource(bot);
            Time.timeScale = 2f;
            game.StartRun();
            last = player.transform.position;
            float deadline = Time.realtimeSinceStartup + 120f;
            bool first = true;
            while (game.IsRunning && circuit.Laps.CompletedLaps < 1 && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForEndOfFrame();
                Vector3 offset = view.transform.position - player.transform.position;
                if (!first) worstCamera = Mathf.Max(worstCamera, (offset - lastCameraOffset).magnitude);
                lastCameraOffset = offset;
                first = false;
            }
            Time.timeScale = timeScale;
            game.TickCompleted -= sample;
            Debug.Log($"ChainRush probe swing: {hooks} hooks, worst jump beyond speed {worstJump:0.00} m at {worstAt}; camera offset change per frame at most {worstCamera:0.00} m; respawned {player.IsRespawning}, laps {circuit.Laps.CompletedLaps}.");
            Assert.That(hooks, Is.GreaterThan(0), "The bot must hook at least one corner.");
        }

        // As the player told it (2026-10-19): "on the inside line, pressing Shift jumped me to the outside". The bot keeps
        // 4 m inside of the centerline through each corner and hooks once it is in the arc. Logs, per hook, the lateral
        // offset D (positive = right) before and after and the rope against the real distance.
        [UnityTest, Timeout(180000)]
        public IEnumerator Probe_SwingHook_FromTheInsideLine()
        {
            var bot = new ProbeBot(new CircuitBot(game, player, grapple, circuit), game, player, circuit) { SwingCorners = true, InsideLine = true };
            Vector3 last = Vector3.zero;
            float lastSpeed = 0f, worstJump = 0f;
            int hooks = 0, report = -1;
            float dBefore = 0f;
            string worstAt = "none", pending = null;
            bool wasSwinging = false;
            Action<int> sample = tick =>
            {
                Vector3 now = player.transform.position;
                TrackCoord here = game.Track.Project(now);
                float moved = (now - last).magnitude;
                float allowed = Mathf.Max(lastSpeed, player.Velocity.magnitude) * Ticks.Seconds + 0.05f;
                if (tick > 1 && moved - allowed > worstJump)
                {
                    worstJump = moved - allowed;
                    worstAt = $"tick {tick} s {here.S:0.0} moved {moved:0.00} m (speed allows {allowed:0.00}), swinging {player.IsSwinging}";
                }
                if (player.IsSwinging && !wasSwinging)
                {
                    hooks++;
                    Vector3 pole = game.Racer.SwingCenter;
                    float distance = new Vector2(now.x - pole.x, now.z - pole.z).magnitude;
                    pending = $"ChainRush probe inside hook {hooks}: tick {tick} s {here.S:0.0}, D before {dBefore:+0.00;-0.00} -> on the hook tick {here.D:+0.00;-0.00}, rope {game.Racer.SwingRadius:0.00} m, distance {distance:0.00} m, moved {moved:0.00} m";
                    report = tick + 5;
                }
                if (tick == report && pending != null)
                {
                    Debug.Log($"{pending}, D 5 ticks later {here.D:+0.00;-0.00}, swinging {player.IsSwinging}.");
                    pending = null;
                }
                dBefore = here.D;
                wasSwinging = player.IsSwinging;
                last = now;
                lastSpeed = player.Velocity.magnitude;
            };
            game.TickCompleted += sample;
            game.SetInputSource(bot);
            Time.timeScale = 2f;
            game.StartRun();
            last = player.transform.position;
            float deadline = Time.realtimeSinceStartup + 120f;
            while (game.IsRunning && circuit.Laps.CompletedLaps < 1 && Time.realtimeSinceStartup < deadline) yield return null;
            Time.timeScale = timeScale;
            game.TickCompleted -= sample;
            Debug.Log($"ChainRush probe inside: {hooks} hooks, worst jump beyond speed {worstJump:0.00} m at {worstAt}; laps {circuit.Laps.CompletedLaps}.");
            Assert.That(hooks, Is.GreaterThan(0), "The bot must hook at least one corner.");
        }

        // The first corner, on the inside line, in every combination the player could not recall (2026-10-19): when the swing
        // key is pressed (as soon as the pole is in reach / at the arc's start / halfway round), with or without a booster
        // running, and steering none / toward the pole / away from it for the first 10 swing ticks. Per case: D (positive =
        // right) the tick before the hook, on it and 10 ticks on, the rope against the real distance, and the worst
        // movement beyond what the speed allows over those ticks.
        [UnityTest, Timeout(300000)]
        public IEnumerator Probe_SwingHook_EveryCombination()
        {
            double arcStart = -1d;
            for (double s = 100d; s < 250d && arcStart < 0d; s += 0.5d)
                if (game.Track.FrameAt(s).Curvature != 0f) arcStart = s;
            Assert.That(arcStart, Is.GreaterThan(0d), "No corner found.");
            double arcEnd = arcStart;
            while (game.Track.FrameAt(arcEnd).Curvature != 0f) arcEnd += 0.5d;
            float towardPole = Mathf.Sign(game.Track.FrameAt(arcStart + 1d).Curvature);
            Debug.Log($"ChainRush probe combos: first corner s {arcStart:0.0}..{arcEnd:0.0}, the pole is to the {(towardPole > 0f ? "right" : "left")}.");
            string[] timings = { "early", "arc start", "halfway" };
            string[] steers = { "none", "toward pole", "away" };
            foreach (string timing in timings)
            foreach (bool booster in new[] { false, true })
            foreach (string steerName in steers)
            {
                float steer = steerName == "none" ? 0f : steerName == "toward pole" ? towardPole : -towardPole;
                // Early: the first tick the first corner's pole is in reach, looked for from 40 m before its arc (from the
                // start line the last corner's pole is in reach too).
                double hookAt = timing == "early" ? arcStart - 40d : timing == "arc start" ? arcStart : (arcStart + arcEnd) / 2d;
                var bot = new ComboBot(new ProbeBot(new CircuitBot(game, player, grapple, circuit), game, player, circuit) { InsideLine = true },
                    game, player, hookAt, booster ? arcStart - 30d : -1d, steer);
                int hook = -1;
                float dBefore = 0f, dHook = 0f, dAfter = 0f, rope = 0f, distance = 0f, worst = 0f, lastSpeed = 0f, previousD = 0f;
                double hookS = 0d;
                bool stillSwinging = false;
                Vector3 last = Vector3.zero;
                Action<int> sample = tick =>
                {
                    Vector3 now = player.transform.position;
                    TrackCoord here = game.Track.Project(now);
                    if (hook < 0 && player.IsSwinging)
                    {
                        hook = tick;
                        hookS = here.S;
                        dBefore = previousD;
                        dHook = here.D;
                        rope = game.Racer.SwingRadius;
                        Vector3 pole = game.Racer.SwingCenter;
                        distance = new Vector2(now.x - pole.x, now.z - pole.z).magnitude;
                    }
                    if (hook > 0 && tick <= hook + 10)
                        worst = Mathf.Max(worst, (now - last).magnitude - (Mathf.Max(lastSpeed, player.Velocity.magnitude) * Ticks.Seconds + 0.05f));
                    if (hook > 0 && tick == hook + 10)
                    {
                        dAfter = here.D;
                        stillSwinging = player.IsSwinging;
                    }
                    previousD = here.D;
                    last = now;
                    lastSpeed = player.Velocity.magnitude;
                };
                game.TickCompleted += sample;
                yield return RunUntil(bot, () => (hook > 0 && game.Tick > hook + 12) || circuit.Laps.Progress > arcEnd + 40d, 60f, () => game.Racer.AddGauge(2f));
                game.TickCompleted -= sample;
                Debug.Log(hook < 0
                    ? $"ChainRush probe combo [{timing}, booster {(booster ? "on" : "off")}, steer {steerName}]: never hooked."
                    : $"ChainRush probe combo [{timing}, booster {(booster ? "on" : "off")}, steer {steerName}]: hook s {hookS:0.0} tick {hook}, D {dBefore:+0.00;-0.00} -> {dHook:+0.00;-0.00} -> 10 ticks {dAfter:+0.00;-0.00} (swinging {stillSwinging}), rope {rope:0.00} vs distance {distance:0.00}, worst jump beyond speed {Mathf.Max(0f, worst):0.00} m.");
                game.FailRun();
            }
        }

        // ProbeBot's inside line, with one swing: pressed at the first tick past hookAt (0: as soon as a pole is in reach)
        // with a pole in reach, held 10 ticks with a fixed steer, then let go; a booster fired at boostAt (negative: none).
        private sealed class ComboBot : IInputSource
        {
            private readonly ProbeBot driver;
            private readonly ChainRushGame game;
            private readonly RunnerMotor player;
            private readonly double hookAt, boostAt;
            private readonly float steer;
            private bool boosted, hooked;
            private int holdLeft;

            public ComboBot(ProbeBot driver, ChainRushGame game, RunnerMotor player, double hookAt, double boostAt, float steer)
            {
                this.driver = driver;
                this.game = game;
                this.player = player;
                this.hookAt = hookAt;
                this.boostAt = boostAt;
                this.steer = steer;
            }

            public void Poll() { }
            public void Clear() { }

            public TickInput Consume()
            {
                TickInput driven = driver.Consume();
                double s = game.Track.Project(player.transform.position).S;
                bool chain = false, press = false, held = false;
                float x = driven.Steer;
                if (boostAt >= 0d && !boosted && s >= boostAt)
                {
                    chain = true;
                    boosted = true;
                }
                if (!hooked && s >= hookAt && s < hookAt + 200d && player.IsGrounded && player.TryFindPole(out _))
                {
                    hooked = true;
                    press = true;
                    held = true;
                    holdLeft = 10;
                    x = steer;
                }
                else if (holdLeft > 0)
                {
                    holdLeft--;
                    held = true;
                    x = steer;
                }
                return new TickInput(x, driven.PrimaryPressed, false, false, held, chain, press);
            }
        }

        private IEnumerator RunUntil(IInputSource source, Func<bool> done, float realSeconds, Action afterStart)
        {
            game.SetInputSource(source);
            Time.timeScale = 3f;
            game.StartRun();
            afterStart();
            float deadline = Time.realtimeSinceStartup + realSeconds;
            while (game.IsRunning && !done() && Time.realtimeSinceStartup < deadline) yield return null;
            Time.timeScale = timeScale;
        }

        // CircuitBot with extras: a booster a set distance before the grapple gap, Ctrl while grappling, a swing at each
        // corner as soon as its pole is in reach (held 0.6 s, steering straight).
        private sealed class ProbeBot : IInputSource
        {
            private readonly CircuitBot inner;
            private readonly ChainRushGame game;
            private readonly RunnerMotor player;
            private readonly CircuitRace circuit;
            public double BoostBeforeGap = -1d;
            public bool EmpowerGrapple;
            public bool SwingCorners;
            // Keep 4 m inside of the centerline through corners, and hook only once in the arc.
            public bool InsideLine;
            private bool boosted;
            private bool empowered;
            private int swingLeft;
            private int swingCooldown;

            public ProbeBot(CircuitBot inner, ChainRushGame game, RunnerMotor player, CircuitRace circuit)
            {
                this.inner = inner;
                this.game = game;
                this.player = player;
                this.circuit = circuit;
            }

            public void Poll() { }
            public void Clear() { }

            public TickInput Consume()
            {
                TickInput input = inner.Consume();
                float steer = InsideLine ? SteerOnInsideLine() : input.Steer;
                bool chain = false, swingPress = false, swingHeld = false;
                double s = game.Track.Project(player.transform.position).S;
                bool inArc = game.Track.FrameAt(s).Curvature != 0f;
                if (BoostBeforeGap > 0d && !boosted && circuit.TryGetNextGap(s, out double startS, out _, out bool hasAnchor) && hasAnchor && startS - s <= BoostBeforeGap)
                {
                    chain = true;
                    boosted = true;
                }
                if (EmpowerGrapple && !empowered && player.GetComponent<GrappleController>().IsAttached)
                {
                    chain = true;
                    empowered = true;
                }
                if (SwingCorners)
                {
                    if (swingLeft > 0)
                    {
                        swingLeft--;
                        swingHeld = swingLeft > 0;
                        steer = 0f;
                        if (swingLeft == 0) swingCooldown = 100;
                    }
                    else if (swingCooldown > 0) swingCooldown--;
                    else if (player.IsGrounded && (!InsideLine || inArc) && player.TryFindPole(out _))
                    {
                        swingPress = true;
                        swingHeld = true;
                        swingLeft = 30;
                        steer = 0f;
                    }
                }
                return new TickInput(steer, input.PrimaryPressed, input.ReleasePressed, input.AttackPressed, swingHeld, chain, swingPress, input.Coasting);
            }

            // TrackFollower's steering toward a line 4 m inside of the centerline wherever a corner is here or within 30 m
            // (positive curvature turns right, and D is positive to the right, so inside is D of the curvature's sign).
            private float SteerOnInsideLine()
            {
                Vector3 position = player.transform.position;
                TrackCoord here = game.Track.Project(position);
                float curve = game.Track.FrameAt(here.S).Curvature;
                if (curve == 0f) curve = game.Track.FrameAt(here.S + 30d).Curvature;
                float line = curve == 0f ? 0f : Mathf.Sign(curve) * 4f;
                TrackFrame frame = game.Track.Frame(position);
                float trackYaw = Mathf.Atan2(frame.Forward.x, frame.Forward.z) * Mathf.Rad2Deg;
                float wanted = trackYaw - Mathf.Clamp((here.D - line) * 4f, -20f, 20f);
                float turnLimit = player.TurnLimit;
                float feedForward = turnLimit > 0f ? player.ForwardSpeed * frame.Curvature * Mathf.Rad2Deg / turnLimit : 0f;
                return Mathf.Clamp(feedForward + Mathf.DeltaAngle(player.Heading, wanted) / 20f, -1f, 1f);
            }
        }
    }
}
