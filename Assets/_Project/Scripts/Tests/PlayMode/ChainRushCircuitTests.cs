using System.Collections;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Race;
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
    // The circuit race scene (COURSE.md T4): a bot runs three laps of the stadium and the lap counter is
    // exact; going backwards over the line is not a lap, a fall into a gap puts the runner back before it, a restart
    // starts over.
    public sealed class ChainRushCircuitTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/ChainRushCircuit.unity";
        private const float StandingHeight = 1.05f;

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
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("Circuit tests require the Unity Editor.");
            yield break;
#endif
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame g)) game = g;
                if (root.TryGetComponent(out RunnerMotor p)) { player = p; grapple = p.GetComponent<GrappleController>(); }
                if (root.TryGetComponent(out CircuitRace c)) circuit = c;
            }
            Assert.That(game, Is.Not.Null);
            Assert.That(circuit, Is.Not.Null, "The circuit scene needs a CircuitRace (menu: Create Circuit Scene).");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = timeScale;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // ---- the bot runs three laps --------------------------------------------------------------------

        [UnityTest, Timeout(180000)]
        public IEnumerator Race_BotRunsThreeLaps_CountsThemAndTimesThemExactly()
        {
            game.SetInputSource(new CircuitBot(game, player, grapple, circuit));
            Time.timeScale = 3f;
            game.StartRun();
            LapCounter laps = circuit.Laps;
            float started = Time.realtimeSinceStartup;
            float deadline = started + 150f;
            float maxOffset = 0f;
            double lastProgress = laps.Progress;
            while (game.IsRunning && !laps.IsFinished && Time.realtimeSinceStartup < deadline)
            {
                maxOffset = Mathf.Max(maxOffset, Mathf.Abs(game.Track.Project(player.transform.position).D));
                Assert.That(laps.Progress, Is.GreaterThanOrEqualTo(lastProgress - 1d), "The bot's progress must not run backwards. " + State());
                lastProgress = laps.Progress;
                yield return null;
            }
            Time.timeScale = timeScale;
            Assert.That(game.HasFailed, Is.False, "The bot fell or died. " + State());
            Assert.That(laps.IsFinished, Is.True, "Timed out after " + (Time.realtimeSinceStartup - started).ToString("F0") + " s. " + State());
            Assert.That(game.HasFinished, Is.True, State());
            Assert.That(laps.CompletedLaps, Is.EqualTo(3));
            // ChainRushCircuit.unity's runner runs at 20 m/s (M1-T, 2026-10-18).
            double lapSeconds = circuit.Definition.LapLength / 20d;
            int sum = 0;
            for (int lap = 1; lap <= 3; lap++)
            {
                float seconds = Ticks.ToSeconds(laps.LapTicks(lap));
                Debug.Log($"ChainRush circuit lap {lap}: {seconds:F2} s");
                Assert.That(seconds, Is.EqualTo(lapSeconds).Within(lapSeconds * 0.2d), $"lap {lap} time");
                sum += laps.LapTicks(lap);
            }
            Assert.That(laps.TotalTicks, Is.EqualTo(sum));
            Assert.That(game.Tick, Is.EqualTo(laps.LapEndTick(3)), "The game ends the race on the tick the third lap is completed.");
            Assert.That(game.Elapsed, Is.EqualTo(Ticks.ToSeconds(laps.TotalTicks)).Within(1e-3f));
            Assert.That(game.Health, Is.EqualTo(3));
            Assert.That(maxOffset, Is.LessThan(RoadProfile.DeckHalfWidth - 0.4f), "The bot touched a guard.");
            for (int cp = 0; cp < 3; cp++)
            {
                for (int lap = 1; lap <= 3; lap++) Assert.That(laps.CheckpointTick(lap, cp), Is.GreaterThan(0), $"lap {lap} checkpoint {cp}");
            }
            Debug.Log($"ChainRush circuit race: total {Ticks.ToSeconds(laps.TotalTicks):F2} s, {game.Grapples} grapples, slowest step {circuit.MaxStepMilliseconds:F2} ms, " +
                      $"{circuit.PieceCount} road pieces, real time {Time.realtimeSinceStartup - started:F0} s, max offset {maxOffset:F2} m.");
        }

        // ---- direction and gaps -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Race_RunningBackwardsOverTheStartLine_IsNotALap()
        {
            game.SetInputSource(new TrackFollower(game.Track, player) { Steering = false });
            game.StartRun();
            yield return LandOnRoad();
            game.Racer.Heading = Mathf.Repeat(game.Racer.Heading + 180f + 180f, 360f) - 180f;
            float deadline = Time.time + 1.5f;
            while (game.IsRunning && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(game.IsRunning, Is.True, State());
            Assert.That(circuit.Laps.Progress, Is.LessThan(-5d), "The runner went backwards over the line. " + State());
            Assert.That(circuit.Laps.CompletedLaps, Is.Zero);
            Assert.That(circuit.Laps.IsFinished, Is.False);
        }

        // A fall no longer ends a circuit run: the runner comes back on the road before the gap, at rest and facing along
        // the track, stands still for the hold (1 s = 50 ticks) while the clock runs, then runs on.
        [UnityTest]
        public IEnumerator Gap_RunningStraightIntoTheGrappleGap_FallsAndComesBackBeforeTheGapAndHoldsForOneSecond()
        {
            circuit.TryGetNextGap(150d, out double startS, out _, out bool hasAnchor);
            Assert.That(hasAnchor, Is.True, "The first gap after S 150 is the grapple gap on straight B.");
            game.SetInputSource(new TrackFollower(game.Track, player));
            game.StartRun();
            Teleport(startS - 12d);
            yield return LandOnRoad();
            // Every tick's state, read between ticks, so no tick is skipped however the frames fall.
            int respawnTick = -1, holdEndTick = -1, firstMoveTick = -1;
            double lowestH = 0d;
            Vector3 respawnPosition = default;
            float respawnSpeed = -1f, respawnHeading = 0f, trackHeading = 0f;
            bool respawnGrounded = false, movedDuringHold = false;
            System.Action<int> onTick = tick =>
            {
                TrackCoord coord = game.Track.Project(player.transform.position);
                if (respawnTick < 0)
                {
                    lowestH = System.Math.Min(lowestH, coord.H);
                    if (!player.IsRespawning) return;
                    respawnTick = tick;
                    respawnPosition = player.transform.position;
                    respawnSpeed = player.Velocity.magnitude;
                    respawnGrounded = player.IsGrounded;
                    respawnHeading = player.Heading;
                    Vector3 forward = game.Track.FrameAt(coord.S).Forward;
                    trackHeading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                    return;
                }
                if (holdEndTick < 0)
                {
                    if (player.transform.position != respawnPosition) movedDuringHold = true;
                    if (!player.IsRespawning) holdEndTick = tick;
                    return;
                }
                if (firstMoveTick < 0 && player.transform.position != respawnPosition) firstMoveTick = tick;
            };
            game.TickCompleted += onTick;
            try
            {
                float deadline = Time.time + 10f;
                while (game.IsRunning && firstMoveTick < 0 && Time.time < deadline) yield return new WaitForFixedUpdate();
            }
            finally
            {
                game.TickCompleted -= onTick;
            }
            Assert.That(respawnTick, Is.GreaterThan(0), "Running into the gap must put the runner back on the track. " + State());
            Assert.That(lowestH, Is.LessThan(-11d), "The runner fell into the gap before it came back.");
            Assert.That(game.HasFailed, Is.False, "A fall must not end a circuit run. " + State());
            Assert.That(game.IsRunning, Is.True, State());
            Assert.That(holdEndTick - respawnTick, Is.EqualTo(Ticks.FromSeconds(1f)), "The hold lasts one second of ticks.");
            Assert.That(movedDuringHold, Is.False, "The runner stands still during the hold.");
            Assert.That(firstMoveTick, Is.EqualTo(holdEndTick + 1), "The runner runs on the tick after the hold. " + State());
            TrackCoord back = game.Track.Project(respawnPosition);
            Assert.That(back.S, Is.LessThan(startS - 3d).And.GreaterThan(startS - 12d), $"The runner comes back about 4 m before the gap (starts at S {startS:F1}).");
            Assert.That(Mathf.Abs(back.D), Is.LessThan(0.01f), "on the centerline");
            Assert.That(back.H, Is.InRange(0.5d, 1.1d), "standing on the road");
            Assert.That(respawnGrounded, Is.True, "standing on the road");
            Assert.That(respawnSpeed, Is.Zero, "at rest");
            Assert.That(Mathf.DeltaAngle(respawnHeading, trackHeading), Is.EqualTo(0f).Within(1e-3f), "facing along the track");
            Assert.That(circuit.Laps.CompletedLaps, Is.Zero);
            Assert.That(game.HasFinished, Is.False);
        }

        // As the player told it (2026-10-19): "hooking the grapple too early makes the rope so long the runner nearly falls".
        // Measured: hooked far behind the anchor (18 m and more along the track) with little climb left, the long rope is
        // still slack while the runner flies on at running speed, so it follows its jump down into the gap and is let go at
        // the bottom. Now an anchor is offered only within its height over the track less standing height (here 10 - 1.05 m),
        // so the rope can never let the runner's feet below the road (docs/DECISIONS.md 2026-10-19, user "추천대로 ㄱㄱ").
        // The runner jumps further and further before the grapple gap and presses the grapple on every tick in the air until
        // it hooks. Up to 14 m before the edge it hooks in that jump, its feet stay above the road while hooked, and it
        // crosses. From 16 m (about 0.8 s early) the anchor never comes within the rope's reach in that jump: it cannot hook
        // and falls, and is put back before the gap — a jump made too early fails, as a missed jump does. (Before the air
        // sped the runner up, same day, the line was between 12 and 14 m.)
        [UnityTest, Timeout(180000)]
        public IEnumerator Grapple_JumpedBeforeTheGap_HooksAboveTheRoadOrFromTooEarlyCannotHookAndFalls()
        {
            // Every case runs and is logged; the failures are reported together at the end.
            var failures = new System.Collections.Generic.List<string>();
            foreach ((double jumpBefore, bool afterPeak, bool tooEarly) in new[]
            {
                (4.5d, false, false), (8d, false, false), (10d, false, false), (12d, false, false), (14d, false, false), (16d, false, true), (18d, false, true),
                (4.5d, true, false), (8d, true, false),
            })
            {
                string mode = $"jump {jumpBefore:0.0} m before the gap{(afterPeak ? ", pressing after the peak" : "")}";
                var bot = new EarlyHookBot(new CircuitBot(game, player, grapple, circuit), game, player, grapple, circuit, jumpBefore) { AfterPeak = afterPeak };
                bool wasAttached = false, respawned = false;
                // Counted in the bot's jump only: after a fall CircuitBot crosses on its own.
                int hooks = 0;
                float lowest = float.MaxValue;
                string hook = "none", release = "none";
                System.Action<int> onTick = tick =>
                {
                    TrackCoord here = game.Track.Project(player.transform.position);
                    respawned |= player.IsRespawning;
                    if (grapple.IsAttached && !respawned)
                    {
                        if (!wasAttached)
                        {
                            hooks++;
                            Vector3 offset = grapple.AnchorPosition - player.transform.position;
                            float along = new Vector2(offset.x, offset.z).magnitude;
                            hook = $"S {here.S:F1} H {here.H:F2} vy {player.Velocity.y:F2}, rope {game.Racer.RopeLength:F2} m, anchor {along:F1} m ahead at {Mathf.Atan2(offset.y, along) * Mathf.Rad2Deg:F1} deg";
                        }
                        lowest = Mathf.Min(lowest, here.H);
                    }
                    else if (wasAttached && !respawned) release = $"S {here.S:F1} H {here.H:F2}";
                    wasAttached = grapple.IsAttached;
                };
                game.TickCompleted += onTick;
                try
                {
                    game.SetInputSource(bot);
                    Time.timeScale = 3f;
                    game.StartRun();
                    float deadline = Time.realtimeSinceStartup + 40f;
                    while (game.IsRunning && (bot.GapEnd < 0d || circuit.Laps.Progress < bot.GapEnd + 5d) && Time.realtimeSinceStartup < deadline) yield return null;
                }
                finally
                {
                    Time.timeScale = timeScale;
                    game.TickCompleted -= onTick;
                }
                Debug.Log($"ChainRush circuit early grapple ({mode}): hooked at {hook}, lowest {(hooks > 0 ? lowest.ToString("F2") + " m" : "-")}, released at {release}, respawned {respawned}.");
                if (bot.GapEnd < 0d) failures.Add($"{mode}: the bot never reached the grapple gap. " + State());
                else if (circuit.Laps.Progress < bot.GapEnd) failures.Add($"{mode}: the runner never crossed the gap. " + State());
                if (tooEarly)
                {
                    if (hooks != 0) failures.Add($"{mode}: jumped too early, it must not hook in that jump, but hooked at {hook} (lowest {lowest:F2} m).");
                    if (!respawned) failures.Add($"{mode}: jumped too early without a hook, it must fall and be put back.");
                }
                else
                {
                    if (hooks != 1) failures.Add($"{mode}: pressing on every tick in the air must hook once in that jump, hooked {hooks} times.");
                    if (bot.LandedShort) failures.Add($"{mode}: the jump landed before the gap.");
                    if (respawned) failures.Add($"{mode}: the runner fell into the gap (hooked at {hook}).");
                    if (lowest < StandingHeight) failures.Add($"{mode}: hooked at {hook}, the runner hung {lowest:F2} m over the track, its feet below the road.");
                }
                game.FailRun();
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        // ---- restart and scene --------------------------------------------------------------------------

        [UnityTest, Timeout(60000)]
        public IEnumerator Race_Restart_StartsAgainOnTheLineWithNoLaps()
        {
            game.SetInputSource(new CircuitBot(game, player, grapple, circuit));
            Time.timeScale = 3f;
            game.StartRun();
            float deadline = Time.realtimeSinceStartup + 20f;
            while (game.IsRunning && circuit.Laps.CheckpointsPassed < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Time.timeScale = timeScale;
            Assert.That(circuit.Laps.CheckpointsPassed, Is.GreaterThanOrEqualTo(2), "The bot should reach the second checkpoint. " + State());
            game.StartRun();
            Assert.That(circuit.Laps.CompletedLaps, Is.Zero);
            Assert.That(circuit.Laps.CheckpointsPassed, Is.Zero);
            Assert.That(circuit.Laps.Progress, Is.EqualTo(0d).Within(1e-9));
            Assert.That(game.Tick, Is.Zero);
            yield return LandOnRoad();
            Assert.That(game.Track.Project(player.transform.position).S, Is.LessThan(2d).Or.GreaterThan(circuit.Definition.LapLength - 2d), "The runner starts on the line. " + State());
        }

        [UnityTest]
        public IEnumerator Scene_AnchorsAndRoad_MatchTheDefinition()
        {
            TrackDefinition definition = circuit.Definition;
            Assert.That(circuit.ActiveAnchors, Is.EqualTo(definition.Anchors.Count));
            TrackDefinition.Anchor anchor = definition.Anchors[0];
            Vector3 expected = game.Track.FrameAt(anchor.S).TransformPoint(new Vector3(anchor.Offset, anchor.Height, 0f));
            Assert.That(Vector3.Distance(circuit.AnchorAt(0).position, expected), Is.LessThan(1e-3f));
            Assert.That(game.Track.IsLoop, Is.True);
            Assert.That(game.Track.EndS, Is.EqualTo(definition.LapLength).Within(1e-9));
            Assert.That(circuit.PieceCount, Is.GreaterThanOrEqualTo(8));
            Assert.That(game.IsCircuit, Is.True);
            Assert.That(game.IsEndless, Is.False);
            Assert.That(game.Enemies, Is.Null);
            yield return null;
        }

        // R5: a pole model stands on every arc's center (the simulation's pole), tall and thick enough to see, its band
        // where the chain bites, with no collider; on the straight before an arc the HUD is offered that pole, mid-straight
        // none, and on the start line (the last arc's end) the last arc's.
        [UnityTest]
        public IEnumerator Scene_CornerPoles_StandOnEveryArcCenter_AndTheOneInReachIsOffered()
        {
            var feet = new System.Collections.Generic.List<Vector3>();
            game.Track.GetPoles(feet);
            int arcs = 0;
            foreach (TrackDefinition.Segment segment in circuit.Definition.Segments) if (segment.IsArc) arcs++;
            Assert.That(feet.Count, Is.EqualTo(arcs), "One pole per arc.");
            Assert.That(circuit.PoleCount, Is.EqualTo(arcs));
            yield return null;
            for (int i = 0; i < feet.Count; i++)
            {
                Transform pole = circuit.transform.Find("Corner pole " + i);
                Assert.That(pole, Is.Not.Null, $"Corner pole {i}");
                Assert.That(Vector3.Distance(pole.position, feet[i]), Is.LessThan(1e-3f), $"Pole {i} stands on its arc's center.");
                Assert.That(Vector3.Distance(pole.Find("Cap").position, SwingLook.PoleTop(feet[i])), Is.LessThan(1e-3f), $"Pole {i}'s cap is on its top.");
                Assert.That(Vector3.Distance(pole.Find("Bite band").position, SwingLook.PoleBite(feet[i])), Is.LessThan(1e-3f), $"Pole {i}'s band is where the chain bites.");
                // Big enough to see from down the straight: the first 8 m x 0.7 m post went unseen in play (2026-10-18).
                Bounds post = pole.Find("Post").GetComponent<Renderer>().bounds;
                Assert.That(post.size.y, Is.EqualTo(SwingLook.PoleHeight + SwingLook.PoleDepth).Within(1e-3f), $"Pole {i} runs from below the road to its top.");
                Assert.That(post.size.x, Is.GreaterThanOrEqualTo(1.5f), $"Pole {i} is thick enough to see.");
                Assert.That(post.max.y - feet[i].y, Is.EqualTo(SwingLook.PoleHeight).Within(1e-3f));
                Assert.That(pole.GetComponentsInChildren<Collider>(true), Is.Empty, $"Pole {i} is visual only.");
            }

            game.SetInputSource(new TrackFollower(game.Track, player));
            game.StartRun();
            yield return LandOnRoad();
            yield return null;
            // The start line is the last arc's end: its pole is 90 degrees aside and 40 m away, in reach since R4b (95 degrees,
            // 40 + 6 m; until R4b, 75 degrees, it was not). It stays in reach for about 3.5 m past the arc (R4b, 2026-10-18:
            // kept, to be judged in play at M1-T).
            Assert.That(grapple.HasPoleCandidate, Is.True, "On the start line the last arc's pole is still in reach. " + State());
            Assert.That(Vector3.Distance(grapple.PoleCandidate, feet[feet.Count - 1]), Is.LessThan(1e-3f), "The candidate is the pole of the arc just left.");

            // Mid-straight both poles are out of reach: the last arc's 72 m behind-aside, the first arc's 80 m ahead.
            Teleport(60d);
            yield return LandOnRoad();
            yield return null;
            Assert.That(grapple.HasPoleCandidate, Is.False, "Mid-straight both poles are out of reach. " + State());

            // The first arc (R40) starts at S 140, after the jump gap (S 119 to 125). From S 127, 13 m before the arc, the
            // pole is 72 degrees aside and 42.1 m away: in reach (95 degrees, 40 + 6 m) while the runner gathers speed.
            Teleport(127d);
            yield return LandOnRoad();
            yield return null;
            Assert.That(grapple.HasPoleCandidate, Is.True, "Before the first arc its pole must be offered. " + State());
            Assert.That(Vector3.Distance(grapple.PoleCandidate, feet[0]), Is.LessThan(1e-3f));
        }

        // ---- helpers ------------------------------------------------------------------------------------

        private void Teleport(double s)
        {
            TrackFrame frame = game.Track.FrameAt(s);
            // The track looks near the focus, so move the focus first, then let the circuit follow the runner.
            game.Track.SetFocus(s);
            player.ShiftOrigin(frame.TransformPoint(new Vector3(0f, StandingHeight, 0f)) - player.transform.position);
            player.FaceTrack();
            Physics.SyncTransforms();
            circuit.Step(game.Tick);
        }

        // IsGrounded still reports the spawn road until the controller has moved once on the new one.
        private IEnumerator LandOnRoad()
        {
            yield return new WaitForFixedUpdate();
            float deadline = Time.time + 2f;
            while (!player.IsGrounded && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.True, "Runner never landed on the circuit. " + State());
        }

        private string State()
        {
            TrackCoord coord = game.Track.Project(player.transform.position);
            return $"S {coord.S:F2}, D {coord.D:F2}, H {coord.H:F2}, position {player.transform.position}, velocity {player.Velocity}, grounded {player.IsGrounded}, " +
                   $"lap {circuit.Laps.CurrentLap}, laps done {circuit.Laps.CompletedLaps}, progress {circuit.Laps.Progress:F1}, failed {game.HasFailed}";
        }

        // CircuitBot, but at the first grapple gap it jumps jumpBefore metres before the edge and, in the air, presses the
        // grapple on every tick until it hooks (with AfterPeak, only once the runner is coming down). Landing again before
        // the gap (the jump fell short) is noted, and the bot goes on as CircuitBot.
        private sealed class EarlyHookBot : IInputSource
        {
            private readonly CircuitBot inner;
            private readonly ChainRushGame game;
            private readonly RunnerMotor player;
            private readonly GrappleController grapple;
            private readonly CircuitRace circuit;
            private readonly double jumpBefore;
            private bool jumped, done;
            public double GapStart = -1d, GapEnd = -1d;
            public bool LandedShort;
            public bool AfterPeak;

            public EarlyHookBot(CircuitBot inner, ChainRushGame game, RunnerMotor player, GrappleController grapple, CircuitRace circuit, double jumpBefore)
            {
                this.inner = inner;
                this.game = game;
                this.player = player;
                this.grapple = grapple;
                this.circuit = circuit;
                this.jumpBefore = jumpBefore;
            }

            public void Poll() { }
            public void Clear() { }

            public TickInput Consume()
            {
                TickInput input = inner.Consume();
                if (done) return input;
                double s = game.Track.Project(player.transform.position).S;
                bool primary = false;
                if (!jumped)
                {
                    // Jump gaps before the grapple gap are CircuitBot's.
                    if (!circuit.TryGetNextGap(s, out double startS, out double endS, out bool hasAnchor) || !hasAnchor) return input;
                    if (player.IsGrounded && startS - s <= jumpBefore)
                    {
                        GapStart = startS;
                        GapEnd = endS;
                        jumped = true;
                        primary = true;
                    }
                }
                else if (grapple.IsAttached) done = true;
                else if (player.IsGrounded && s > GapStart - jumpBefore + 1d)
                {
                    if (s < GapStart) LandedShort = true;
                    done = true;
                }
                else primary = !player.IsGrounded && (!AfterPeak || player.Velocity.y < 0f);
                return new TickInput(input.Steer, primary, false, false);
            }
        }
    }
}
