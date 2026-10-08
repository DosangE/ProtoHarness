using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Combat;
using ProtoHarness.ChainRush.Endless;
using ProtoHarness.ChainRush.Track;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    // The procedural course scene (COURSE.md T3c): a bot runs seeded courses of generated modules, the
    // same seed rebuilds the same course, pools do not grow, and gaps still kill.
    public sealed class ChainRushProceduralTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/ChainRushProcedural.unity";
        private const double RunLength = 1400d;
        private const float StandingHeight = 1.05f;

        private sealed class Report
        {
            public ulong Seed;
            public string Failure;
            public double Distance;
            public float Seconds;
            public int Rebases;
            public double MaxStepMilliseconds;
            public int Hits;
            public int Health;
            public int Modules;
            public int Gaps;
            public int Grapples;
            public int Escapes;
            public int BuiltPieces;

            public override string ToString() =>
                $"seed {Seed}: {(Failure == null ? "ran" : "FAILED")} {Distance:F0} m in {Seconds:F1} s real time, rebases {Rebases}, " +
                $"hits {Hits}, health {Health}, grapples {Grapples}, modules {Modules} (gaps {Gaps}, escapes {Escapes}), built pieces {BuiltPieces}, " +
                $"slowest step {MaxStepMilliseconds:F2} ms";
        }

        private ChainRushGame game;
        private RunnerMotor player;
        private GrappleController grapple;
        private ProceduralCourse course;
        private EnemyDirector enemies;
        private float timeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            timeScale = Time.timeScale;
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("Procedural course tests require the Unity Editor.");
            yield break;
#endif
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame g)) game = g;
                if (root.TryGetComponent(out RunnerMotor p)) { player = p; grapple = p.GetComponent<GrappleController>(); }
                if (root.TryGetComponent(out ProceduralCourse c)) course = c;
            }
            Assert.That(game, Is.Not.Null);
            Assert.That(course, Is.Not.Null, "The procedural scene needs a ProceduralCourse (menu: Create Procedural Scene).");
            enemies = game.Enemies;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = timeScale;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // ---- the bot runs seeded courses ----------------------------------------------------------------

        [UnityTest, Timeout(180000)]
        public IEnumerator Course_BotRunsFourteenHundredMetres_GateSeeds([Values(0, 1, 2)] int seed)
        {
            var report = new Report();
            yield return RunSeed((ulong)seed, RunLength, report);
            Debug.Log("ChainRush procedural run " + report);
            Assert.That(report.Failure, Is.Null, report.Failure);
            Assert.That(course.PoolSize, Is.EqualTo(24));
            Assert.That(course.AnchorPoolSize, Is.EqualTo(10));
        }

        // Completion criterion of T3c (COURSE.md 10): 20 seeds, 1400 m each. Not part of the merge gate.
        [UnityTest, Category("Sweep"), Timeout(2700000)]
        public IEnumerator Course_BotRunsFourteenHundredMetres_TwentySeedSweep()
        {
            var failures = new List<string>();
            int grapples = 0;
            for (ulong seed = 0; seed < 20; seed++)
            {
                var report = new Report();
                yield return RunSeed(seed, RunLength, report);
                Debug.Log("ChainRush procedural sweep " + report);
                grapples += report.Grapples;
                if (report.Failure != null) failures.Add(report.Failure);
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
            Assert.That(grapples, Is.GreaterThan(0), "No seed crossed a grapple gap, so the sweep did not exercise the grapple.");
        }

        private IEnumerator RunSeed(ulong seed, double length, Report report)
        {
            report.Seed = seed;
            course.SetSeed(seed);
            game.SetInputSource(new CourseBot(game, player, grapple, enemies, course));
            int objects = CountObjects();
            Time.timeScale = 3f;
            game.StartRun();
            if (course.Seed != seed) report.Failure = $"seed {seed}: the course used seed {course.Seed}.";
            float started = Time.realtimeSinceStartup;
            float deadline = started + 120f;
            double last = 0d;
            while (report.Failure == null && game.IsRunning && course.Distance < length && Time.realtimeSinceStartup < deadline)
            {
                if (course.Distance < last - 0.5d) report.Failure = $"seed {seed}: distance went back from {last:F1} to {course.Distance:F1} m. {State()}";
                last = course.Distance;
                yield return null;
            }
            Time.timeScale = timeScale;
            report.Seconds = Time.realtimeSinceStartup - started;
            report.Distance = course.Distance;
            report.Rebases = course.RebaseCount;
            report.MaxStepMilliseconds = course.MaxStepMilliseconds;
            report.Hits = game.Hits;
            report.Health = game.Health;
            report.Grapples = game.Grapples;
            report.Modules = course.Modules.Count;
            report.Escapes = course.EscapeCount;
            report.BuiltPieces = course.BuiltPieces;
            foreach (CourseModule module in course.Modules) if (module.HasGap) report.Gaps++;
            if (report.Failure != null) yield break;
            if (!game.IsRunning) report.Failure = $"seed {seed}: the run ended at {course.Distance:F0} m. {State()}";
            else if (course.Distance < length) report.Failure = $"seed {seed}: timed out at {course.Distance:F0} m after {report.Seconds:F0} s. {State()}";
            else if (report.Rebases < 1) report.Failure = $"seed {seed}: {length:F0} m without a single origin shift; the shift distance may be too large.";
            else if (CountObjects() != objects) report.Failure = $"seed {seed}: the scene grew from {objects} to {CountObjects()} objects.";
            else if (course.BuiltPieces > course.PoolSize) report.Failure = $"seed {seed}: more built pieces than the pool holds.";
        }

        // ---- the same seed rebuilds the same course -----------------------------------------------------

        [UnityTest]
        public IEnumerator Seed_RestartWithSameSeed_RebuildsSameCourseAndAnchors()
        {
            course.SetSeed(5UL);
            game.StartRun();
            string first = Describe();
            Assert.That(course.Modules.Count, Is.GreaterThan(3));
            course.SetSeed(5UL);
            game.StartRun();
            Assert.That(Describe(), Is.EqualTo(first), "Same seed must lay the same modules and anchors.");
            course.SetSeed(6UL);
            game.StartRun();
            Assert.That(Describe(), Is.Not.EqualTo(first), "A different seed must lay a different course.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Seed_NoSeedSet_EachRunDrawsANewSeed()
        {
            game.StartRun();
            ulong first = course.Seed;
            bool changed = false;
            for (int i = 0; i < 5 && !changed; i++)
            {
                game.StartRun();
                changed = course.Seed != first;
            }
            Assert.That(changed, Is.True, "Five restarts in a row drew the seed " + first + ".");
            yield return null;
        }

        private string Describe()
        {
            var text = new StringBuilder();
            text.Append("seed ").Append(course.Seed).Append(';');
            foreach (CourseModule module in course.Modules)
            {
                text.Append(module.Kind).Append(':').Append(module.Length.ToString("R")).Append(':').Append(module.GapLength.ToString("R"))
                    .Append(':').Append(module.AnchorS.ToString("R")).Append(';');
            }
            for (int i = 0; i < course.AnchorPoolSize; i++)
            {
                if (!course.IsAnchorActive(i)) continue;
                Vector3 position = course.AnchorAt(i).position;
                text.Append("anchor ").Append(position.x.ToString("R")).Append(',').Append(position.y.ToString("R")).Append(',').Append(position.z.ToString("R")).Append(';');
            }
            return text.ToString();
        }

        // ---- the start of a run -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Start_RunnerStandsOnTheFirstRoadWithRoadLaidAhead()
        {
            course.SetSeed(3UL);
            game.StartRun();
            yield return LandOnRoad();
            Assert.That(course.Distance, Is.EqualTo(0d).Within(0.2d));
            Assert.That(course.BuiltPieces, Is.GreaterThanOrEqualTo(6), "300 m of road should be laid at the start.");
            Assert.That(course.BuiltPieces, Is.LessThanOrEqualTo(course.PoolSize));
            Assert.That(course.PendingBuilds, Is.Zero);
            Assert.That(game.Track.EndS, Is.GreaterThanOrEqualTo(300d));
        }

        // ---- gaps still kill ----------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Gap_RunningStraightIntoAGrappleGap_EndsTheRunAsAFall()
        {
            double startS = FindGapSpot(true, out ulong seed);
            Assert.That(startS, Is.GreaterThan(0d), "No seed below 100 lays a grapple gap in its first 300 m.");
            game.SetInputSource(new TrackFollower(game.Track, player));
            Teleport(startS - 12d);
            yield return LandOnRoad();
            float deadline = Time.time + 8f;
            while (game.IsRunning && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(game.HasFailed, Is.True, $"Running into the gap of seed {seed} must end the run. {State()}");
            Assert.That(game.Track.Project(player.transform.position).H, Is.LessThan(-11f), State());
        }

        // ---- pooled anchors -----------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Anchor_RestingInThePool_IsNeverTheGrappleCandidate()
        {
            course.SetSeed(0UL);
            game.StartRun();
            yield return LandOnRoad();
            int resting = -1;
            for (int i = 0; i < course.AnchorPoolSize && resting < 0; i++)
                if (!course.IsAnchorActive(i)) resting = i;
            Assert.That(resting, Is.GreaterThanOrEqualTo(0), "Some anchor must be resting at the start.");
            Transform anchor = course.AnchorAt(resting);
            anchor.position = player.transform.position + Vector3.forward * 6f + Vector3.up * 4f;
            yield return null;
            yield return null;
            Assert.That(grapple.Candidate, Is.Not.SameAs(anchor), "A switched-off anchor must not be offered as a hook point.");
            anchor.gameObject.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(grapple.Candidate, Is.SameAs(anchor), "The same anchor, switched on, is the nearest hook point.");
            anchor.gameObject.SetActive(false);
        }

        // ---- enemies on curves --------------------------------------------------------------------------

        [UnityTest, Timeout(60000)]
        public IEnumerator Enemy_EncounterOnACurve_FacesTheTrackAndTheAttackConnects()
        {
            double spot = FindCurveSpot(out ulong seed);
            Assert.That(spot, Is.GreaterThan(0d), "No seed below 100 lays a curve with 100 m of solid road after it.");
            game.SetInputSource(new TrackFollower(game.Track, player));
            Teleport(spot);
            yield return LandOnRoad();
            Assert.That(enemies.BeginEncounter(EnemyDirector.Entrance.Right), Is.True, $"Seed {seed}: no encounter could start on the curve. {State()}");
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!enemies.CanAttack && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(enemies.CanAttack, Is.True, State());
            TrackFrame frame = game.Track.Frame(player.transform.position);
            Assert.That(Vector3.Dot(enemies.Target.forward, frame.Forward), Is.GreaterThan(0.98f), "The enemy must face along the track, not along world +z.");
            game.Attack();
            yield return new WaitForSeconds(0.7f);
            Assert.That(game.Hits, Is.EqualTo(1), State());
            Assert.That(game.Health, Is.EqualTo(3));
            Assert.That(enemies.HasEncounter, Is.False);
        }

        // ---- helpers ------------------------------------------------------------------------------------

        // Seeds 0..99 until a laid grapple (or any) gap shows up in the first 300 m; returns its start S.
        private double FindGapSpot(bool needAnchor, out ulong foundSeed)
        {
            for (ulong seed = 0; seed < 100; seed++)
            {
                course.SetSeed(seed);
                game.StartRun();
                if (course.TryGetNextGap(out double startS, out _, out bool hasAnchor) && (!needAnchor || hasAnchor) && startS > 40d)
                {
                    foundSeed = seed;
                    return startS;
                }
            }
            foundSeed = 0;
            return 0d;
        }

        // Seeds 0..99 until a curve module (not the spawn module) is followed by at least 100 m without a gap;
        // returns the S in its middle.
        private double FindCurveSpot(out ulong foundSeed)
        {
            for (ulong seed = 0; seed < 100; seed++)
            {
                course.SetSeed(seed);
                game.StartRun();
                IReadOnlyList<CourseModule> modules = course.Modules;
                for (int i = 1; i < modules.Count; i++)
                {
                    CourseModule module = modules[i];
                    if (module.Kind != ModuleKind.GentleCurve && module.Kind != ModuleKind.SharpCurve && module.Kind != ModuleKind.SCurve) continue;
                    double middle = module.StartS + module.Length * 0.5d;
                    bool gapSoon = false;
                    for (int j = i; j < modules.Count; j++)
                        if (modules[j].HasGap && modules[j].GapStartS < middle + 100d) gapSoon = true;
                    // Only modules wholly inside the laid window count, so that a gap beyond it cannot be missed.
                    if (gapSoon || module.EndS + 100d > game.Track.EndS) continue;
                    foundSeed = seed;
                    return middle;
                }
            }
            foundSeed = 0;
            return 0d;
        }

        private void Teleport(double s)
        {
            TrackFrame frame = game.Track.FrameAt(s);
            player.ShiftOrigin(frame.TransformPoint(new Vector3(0f, StandingHeight, 0f)) - player.transform.position);
            player.FaceTrack();
            Physics.SyncTransforms();
        }

        // IsGrounded still reports the spawn road until the controller has moved once on the new one.
        private IEnumerator LandOnRoad()
        {
            yield return new WaitForFixedUpdate();
            float deadline = Time.time + 2f;
            while (!player.IsGrounded && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.True, "Runner never landed on the procedural road. " + State());
        }

        private string State()
        {
            TrackCoord coord = game.Track.Project(player.transform.position);
            return $"S {coord.S:F2}, D {coord.D:F2}, H {coord.H:F2}, position {player.transform.position}, velocity {player.Velocity}, " +
                   $"grounded {player.IsGrounded}, health {game.Health}, hits {game.Hits}, failed {game.HasFailed}";
        }

        private static int CountObjects()
        {
            int count = 0;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) count += root.GetComponentsInChildren<Transform>(true).Length;
            return count;
        }
    }
}
