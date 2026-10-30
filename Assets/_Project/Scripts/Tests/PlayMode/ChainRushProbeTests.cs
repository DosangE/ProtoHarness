using System;
using System.Collections;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Flow;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Track;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    // Measurements for play reports that have no cause yet (user 2026-10-19): they log numbers and assert only that the
    // run went as planned. Explicit: run by name, not in the gate. A finding becomes a real test with its fix.
    //   Grapple dip: "a grapple gets released by itself at some point; if the runner sped up on the way it nearly fell".
    //   Swing hook jump: "entering a corner with a swing, the runner jumps to the rope's length, not where it was".
    //   Pole reach: "halfway round the corner the pole never hooks" (2026-10-25: the angle, DECISIONS "측정: 코너 중간에 …").
    //   Shortcut swing speed: "steering in a shortcut swing speeds the runner up far too much" (2026-10-27).
    [Explicit]
    public sealed class ChainRushProbeTests
    {
        private const string CircuitScene = "Assets/_Project/Scenes/ChainRushCircuit.unity";
        private const string FingerLanesPath = "Assets/_Project/Data/Circuit_FingerLanes.asset";

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

        // "Halfway round the corner the pole never hooks" (Probe_SwingHook_EveryCombination, 2026-10-19; HANDOFF 2절 3번).
        // The bot drives the first corner without swinging, on the inside line and on the centerline. Per tick from 20 m
        // before the arc to its end: what Centerline.TryFindPole weighs for the corner's pole (level distance against the
        // arc radius plus poleReach, the angle off the heading against poleMaxAngle) and whether the runner is grounded
        // (TryHookPole needs the coyote time), next to what TryFindPole answers. Logs every 10th tick and every change.
        [UnityTest, Timeout(180000)]
        public IEnumerator Probe_PoleReach_RoundTheFirstCorner()
        {
            // The circuit scene's runner (ChainRushCircuit.unity, grep 2026-10-25): poleMaxAngle 95, poleReach 6.
            const float MaxAngle = 95f, Reach = 6f;
            double arcStart = -1d;
            for (double s = 100d; s < 250d && arcStart < 0d; s += 0.5d)
                if (game.Track.FrameAt(s).Curvature != 0f) arcStart = s;
            Assert.That(arcStart, Is.GreaterThan(0d), "No corner found.");
            double arcEnd = arcStart;
            while (game.Track.FrameAt(arcEnd).Curvature != 0f) arcEnd += 0.5d;
            TrackFrame mid = game.Track.FrameAt((arcStart + arcEnd) / 2d);
            float radius = 1f / Mathf.Abs(mid.Curvature);
            Vector3 center = mid.Position + mid.Right * (Mathf.Sign(mid.Curvature) * radius);
            var poles = new System.Collections.Generic.List<Vector3>();
            game.Track.GetPoles(poles);
            Vector3 pole = poles[0];
            for (int i = 1; i < poles.Count; i++)
                if (Level(poles[i] - center) < Level(pole - center)) pole = poles[i];
            Debug.Log($"ChainRush probe reach: first corner s {arcStart:0.0}..{arcEnd:0.0}, radius {radius:0.0} m, its pole {Level(pole - center):0.00} m from the arc center; limits: distance {radius + Reach:0.0} m, angle {MaxAngle:0} deg.");
            foreach (bool inside in new[] { true, false })
            {
                string line = inside ? "inside line" : "centerline";
                IInputSource bot = inside
                    ? new ProbeBot(new CircuitBot(game, player, grapple, circuit), game, player, circuit) { InsideLine = true }
                    : (IInputSource)new CircuitBot(game, player, grapple, circuit);
                string lastKey = null;
                int offered = 0, sampled = 0, firstOffered = -1, lastOffered = -1;
                double firstOfferedS = 0d, lastOfferedS = 0d;
                Action<int> sample = tick =>
                {
                    Vector3 now = player.transform.position;
                    TrackCoord here = game.Track.Project(now);
                    if (here.S < arcStart - 20d || here.S > arcEnd) return;
                    Vector3 toward = pole - now;
                    float distance = Level(toward);
                    float angle = Mathf.Abs(Mathf.DeltaAngle(player.Heading, Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg));
                    TrackFrame frame = game.Track.FrameAt(here.S);
                    float headingOff = Mathf.DeltaAngle(Mathf.Atan2(frame.Forward.x, frame.Forward.z) * Mathf.Rad2Deg, player.Heading);
                    bool grounded = game.Racer.CoyoteTime > 0f;
                    bool found = player.TryFindPole(out Vector3 got);
                    bool isThis = found && Level(got - pole) < 0.01f;
                    sampled++;
                    if (isThis && grounded)
                    {
                        offered++;
                        if (firstOffered < 0) { firstOffered = tick; firstOfferedS = here.S; }
                        lastOffered = tick;
                        lastOfferedS = here.S;
                    }
                    string why = !found ? (distance > radius + Reach ? "too far" : angle > MaxAngle ? "angle" : "other") : isThis ? "this pole" : "another pole";
                    string key = why + (grounded ? "" : " airborne");
                    if (key != lastKey || sampled % 10 == 1)
                        Debug.Log($"ChainRush probe reach [{line}] tick {tick} s {here.S:0.0} ({(here.S - arcStart) / (arcEnd - arcStart) * 100d:0}% of the arc) D {here.D:+0.00;-0.00}: " +
                                  $"distance {distance:0.00} m, angle {angle:0.0} deg, heading off the track {headingOff:+0.0;-0.0} deg, speed {player.HorizontalSpeed:0.0}, turn limit {player.TurnLimit:0.0} deg/s, " +
                                  $"grounded {grounded} -> TryFindPole: {why}.");
                    lastKey = key;
                };
                game.TickCompleted += sample;
                yield return RunUntil(bot, () => circuit.Laps.Progress > arcEnd + 2d || game.HasFailed, 60f, () => { });
                game.TickCompleted -= sample;
                Debug.Log($"ChainRush probe reach [{line}]: {sampled} ticks sampled, this pole offered on the ground for {offered}" +
                          (offered > 0 ? $", from tick {firstOffered} s {firstOfferedS:0.0} ({(firstOfferedS - arcStart) / (arcEnd - arcStart) * 100d:0}%) to tick {lastOffered} s {lastOfferedS:0.0} ({(lastOfferedS - arcStart) / (arcEnd - arcStart) * 100d:0}%)." : "."));
                Assert.That(sampled, Is.GreaterThan(0), "The bot did not reach the corner.");
                game.FailRun();
            }
        }

        // Finger Lanes' nails at Fast and Very Fast, first with the bot steering straight through each swing, then holding
        // the key toward the pole the whole swing (ShortcutBot.HoldInnerKey): for each swing the speed before the hook, the
        // hook's speed at the end (the throttle raises it), the rope from hook to letting go, the top speed and the speed
        // it lets go at. "Steering in a shortcut swing speeds the runner up far too much" (user 2026-10-27). Since 2026-10-30
        // ("손가락 맵에서 지름길쪽으로 갈 시 … 가속이 너무 심하다 … 겹쳐서 가속") also with a booster fired on each swing's
        // first tick (BoosterOnHook), with and without the inner key, and the carried speed each swing leaves.
        [UnityTest, Timeout(600000)]
        public IEnumerator Probe_ShortcutSwing_SpeedWithTheInnerKeyHeld()
        {
#if UNITY_EDITOR
            var finger = AssetDatabase.LoadAssetAtPath<TrackDefinition>(FingerLanesPath);
#else
            TrackDefinition finger = null;
#endif
            Assert.That(finger, Is.Not.Null, FingerLanesPath);
            RunnerProfile neutral = ScriptableObject.CreateInstance<RunnerProfile>();
            neutral.name = "Neutral";
            var records = new BestRecords("ChainRush.Test.ProbeTests.");
            var ghosts = new GhostFiles(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ProtoHarnessTests", "ProbeGhosts"));
            try
            {
                foreach (SpeedClass speed in new[] { SpeedClass.Fast, SpeedClass.VeryFast })
                foreach (bool inner in new[] { false, true })
                foreach (bool booster in new[] { false, true })
                {
                    string run = speed + (inner ? ", inner key held" : ", no steering") + (booster ? ", booster on hook" : "");
                    records.Clear(finger.name, speed);
                    RaceLaunch.Clear();
                    RaceLaunch.Request(new RaceSetup(finger, "FINGER LANES", "", RaceMode.Single, speed, neutral));
                    RaceAssembler assembler = null;
#if UNITY_EDITOR
                    yield return EditorSceneManager.LoadSceneAsyncInPlayMode(CircuitScene, new LoadSceneParameters(LoadSceneMode.Single));
#endif
                    foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                    {
                        if (root.TryGetComponent(out RaceAssembler a)) assembler = a;
                        if (root.TryGetComponent(out ChainRushGame g)) game = g;
                        if (root.TryGetComponent(out RunnerMotor p)) player = p;
                        if (root.TryGetComponent(out CircuitRace c)) circuit = c;
                    }
                    Assert.That(assembler, Is.Not.Null);
                    assembler.Records = records;
                    assembler.Ghosts = ghosts;
                    float assemblyDeadline = Time.realtimeSinceStartup + 20f;
                    while (assembler.IsAssembling && Time.realtimeSinceStartup < assemblyDeadline) yield return null;
                    Assert.That(assembler.IsAssembling, Is.False, "The race did not finish assembling.");

                    var shortcutBot = new ShortcutBot(game, player, finger) { HoldInnerKey = inner };
                    IInputSource bot = booster ? new BoosterOnHook(game, shortcutBot) : shortcutBot;
                    int swings = 0, hookTick = -1;
                    float before = 0f, last = 0f, hookSpeed = 0f, hookRope = 0f, minRope = 0f, top = 0f, lapTop = 0f;
                    double hookS = 0d;
                    Action<int> sample = tick =>
                    {
                        float now = player.HorizontalSpeed;
                        lapTop = Mathf.Max(lapTop, now);
                        if (game.Racer.SwingTicks > 0)
                        {
                            if (hookTick < 0)
                            {
                                hookTick = tick;
                                before = last;
                                hookRope = game.Racer.SwingHookRadius;
                                minRope = hookRope;
                                top = now;
                                hookS = game.Track.Project(player.transform.position).S;
                            }
                            top = Mathf.Max(top, now);
                            minRope = Mathf.Min(minRope, game.Racer.SwingRadius);
                            hookSpeed = game.Racer.SwingHookSpeed;
                        }
                        else if (hookTick >= 0)
                        {
                            swings++;
                            Debug.Log($"ChainRush probe shortcut swing [{run}] swing {swings}: hooked at s {hookS:0.0} tick {hookTick} from {before:0.00} m/s, " +
                                      $"hook speed at the end {hookSpeed:0.00} m/s, rope {hookRope:0.00} -> {minRope:0.00} m, top {top:0.00} m/s, " +
                                      $"let go at {now:0.00} m/s after {(tick - hookTick) * Ticks.Seconds:0.00} s, carrying {game.Racer.SwingCarrySpeed:0.00} m/s.");
                            hookTick = -1;
                        }
                        last = now;
                    };
                    game.TickCompleted += sample;
                    yield return RunUntil(bot, () => circuit.Laps.CompletedLaps >= 1 || game.HasFailed, 60f, () => { });
                    game.TickCompleted -= sample;
                    Debug.Log($"ChainRush probe shortcut swing [{run}]: lap 1 {(circuit.Laps.CompletedLaps >= 1 ? Ticks.ToSeconds(circuit.Laps.LapTicks(1)).ToString("0.00") + " s" : "not finished")}, " +
                              $"shortcuts taken {shortcutBot.Taken}, hooks missed {shortcutBot.Missed}, failed {game.HasFailed}, top speed {lapTop:0.00} m/s, run speed {player.RunSpeed:0.00} m/s.");
                    if (!inner && !booster)
                    {
                        Assert.That(game.HasFailed, Is.False, "The plain shortcut run fell. " + run);
                        Assert.That(shortcutBot.Missed, Is.EqualTo(0), "The plain shortcut run missed a hook. " + run);
                    }
                    Assert.That(swings, Is.GreaterThan(0), "No swing was measured. " + run);
                    game.FailRun();
                }
            }
            finally
            {
                RaceLaunch.Clear();
                records.Clear(finger.name, SpeedClass.Fast);
                records.Clear(finger.name, SpeedClass.VeryFast);
                if (System.IO.Directory.Exists(ghosts.Folder)) System.IO.Directory.Delete(ghosts.Folder, true);
                UnityEngine.Object.Destroy(neutral);
            }
        }

        private static float Level(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        // Another bot's input with the chain action pressed on the first tick of each swing, a full gauge slot given for it,
        // as a player firing a booster in a shortcut's swing would.
        private sealed class BoosterOnHook : IInputSource
        {
            private readonly ChainRushGame game;
            private readonly IInputSource inner;
            private bool wasSwinging;

            public BoosterOnHook(ChainRushGame game, IInputSource inner)
            {
                this.game = game;
                this.inner = inner;
            }

            public void Poll() => inner.Poll();
            public void Clear() => inner.Clear();

            public TickInput Consume()
            {
                TickInput input = inner.Consume();
                bool swinging = game.Racer.SwingTicks > 0;
                bool fire = swinging && !wasSwinging;
                wasSwinging = swinging;
                if (!fire) return input;
                game.Racer.AddGauge(1f);
                return new TickInput(input.Steer, input.PrimaryPressed, input.ReleasePressed, input.AttackPressed, input.SwingHeld,
                    true, input.SwingPressed, input.Coasting, input.Reversing);
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
