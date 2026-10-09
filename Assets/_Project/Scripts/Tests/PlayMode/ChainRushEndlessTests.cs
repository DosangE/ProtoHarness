using System.Collections;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Combat;
using ProtoHarness.ChainRush.Control;
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
    public sealed class ChainRushEndlessTests
    {
        // Attack-only scripted source, so combat does not depend on virtual device event delivery.
        // The keyboard mapping itself is covered by the Device-category test in ChainRushTests.
        private sealed class AttackInputSource : IInputSource
        {
            public bool Attack;

            public void Poll() { }

            public TickInput Consume()
            {
                var input = new TickInput(0f, false, false, Attack);
                Attack = false;
                return input;
            }

            public void Clear() => Attack = false;
        }

        private ChainRushGame game;
        private RunnerMotor player;
        private GrappleController grapple;
        private EndlessCourse course;
        private EnemyDirector enemies;
        private float timeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            timeScale = Time.timeScale;
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Project/Scenes/ChainRushEndless.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("Endless prototype tests require the Unity Editor.");
            yield break;
#endif
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame g)) game = g;
                if (root.TryGetComponent(out RunnerMotor p)) { player = p; grapple = p.GetComponent<GrappleController>(); }
                if (root.TryGetComponent(out EndlessCourse c)) course = c;
            }
            Assert.That(game, Is.Not.Null);
            Assert.That(course, Is.Not.Null);
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

        private void MovePlayer(float z)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = new Vector3(0f, 1f, z);
            controller.enabled = true;
            controller.Move(Vector3.down * 0.2f);
        }

        // Places the runner on the centerline at s (1 m up, settled onto the road) facing along the track.
        private void MovePlayerToS(double s)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = game.Track.FrameAt(s).Position + Vector3.up;
            controller.enabled = true;
            controller.Move(Vector3.down * 0.2f);
            player.FaceTrack();
        }

        // Restarts with the first seed from 1 whose streamed course has a grapple gap; returns its start S.
        private double StartOnSeedWithGrappleGap()
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                course.Seed = seed;
                game.StartRun();
                CourseStream stream = course.Stream;
                for (int i = 0; i < stream.ModuleCount; i++)
                {
                    CourseModule module = stream.Module(i);
                    if (module.Kind == ModuleKind.GrappleGap) return module.StartS + module.GapStart;
                }
            }
            Assert.Fail("No seed in 1..50 streams a grapple gap within the first stretch.");
            return 0d;
        }

        private IEnumerator Prepare(EnemyDirector.Entrance direction)
        {
            game.StartRun();
            MovePlayer(-5f);
            yield return new WaitForFixedUpdate();
            Assert.That(enemies.BeginEncounter(direction), Is.True);
            Assert.That(enemies.TryAttack(), Is.False, "Warnings are not attackable yet.");
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!enemies.CanAttack && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(enemies.CanAttack, Is.True);
            Assert.That(enemies.Direction, Is.EqualTo(direction));
        }

        [UnityTest]
        public IEnumerator Combat_EachDirection_AttackFiresConnectsAndRetracts()
        {
            var source = new AttackInputSource();
            game.SetInputSource(source);
            foreach (EnemyDirector.Entrance direction in System.Enum.GetValues(typeof(EnemyDirector.Entrance)))
            {
                yield return Prepare(direction);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChainRush-enemy-" + direction + ".png"));
                yield return null;
                source.Attack = true;
                yield return new WaitForSeconds(0.07f);
                Assert.That(enemies.State, Is.EqualTo(EnemyDirector.EncounterState.Firing));
                Assert.That(game.Hits, Is.Zero, "Hit must occur after chain flight, not on attack input.");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChainRush-chain-" + direction + ".png"));
                yield return new WaitForSeconds(0.5f);
                Assert.That(game.Hits, Is.EqualTo(1));
                Assert.That(game.Health, Is.EqualTo(3));
                Assert.That(enemies.HasEncounter, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator Combat_EachDirection_TimeoutDamagesExactlyOnce()
        {
            foreach (EnemyDirector.Entrance direction in System.Enum.GetValues(typeof(EnemyDirector.Entrance)))
            {
                yield return Prepare(direction);
                yield return new WaitForSeconds(1.7f);
                Assert.That(game.Health, Is.EqualTo(2));
                Assert.That(game.Hits, Is.Zero);
                Assert.That(enemies.HasEncounter, Is.False);
                yield return new WaitForSeconds(0.15f);
                Assert.That(game.Health, Is.EqualTo(2));
            }
        }

        [UnityTest]
        public IEnumerator Combat_PauseAndRestart_FreezesWindowAndClearsFlight()
        {
            yield return Prepare(EnemyDirector.Entrance.Left);
            game.TogglePause();
            float remaining = enemies.Remaining;
            Vector3 position = enemies.Target.position;
            yield return new WaitForSeconds(0.25f);
            Assert.That(enemies.Remaining, Is.EqualTo(remaining));
            Assert.That(enemies.Target.position, Is.EqualTo(position));
            Assert.That(enemies.TryAttack(), Is.False);
            game.TogglePause();
            game.Attack();
            yield return new WaitForSeconds(0.05f);
            game.StartRun();
            Assert.That(enemies.HasEncounter, Is.False);
            Assert.That(game.Health, Is.EqualTo(3));
            Assert.That(game.Hits, Is.Zero);
            Assert.That(course.Distance, Is.EqualTo(0d).Within(0.1d));
        }

        [UnityTest]
        public IEnumerator Combat_GapAndJump_DoesNotRequireAttack()
        {
            game.StartRun();
            // 20 m of spawn rest left: too little to finish an encounter on it (T3c, rests only).
            MovePlayer(40f);
            yield return new WaitForFixedUpdate();
            Assert.That(enemies.BeginEncounter(EnemyDirector.Entrance.Above), Is.False);
            yield return Prepare(EnemyDirector.Entrance.Right);
            player.PrimaryAction();
            yield return new WaitForSeconds(0.2f);
            Assert.That(player.IsGrounded, Is.False);
            Assert.That(enemies.HasEncounter, Is.False);
            Assert.That(game.Health, Is.EqualTo(3));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Grapple_RestartWhileAttached_ClearsOldChainImmediately()
        {
            double gapStart = StartOnSeedWithGrappleGap();
            MovePlayerToS(gapStart - 4d);
            yield return new WaitForFixedUpdate();
            player.PrimaryAction();
            yield return new WaitForSeconds(0.25f);
            Assert.That(grapple.TryAttach(), Is.True);
            yield return new WaitForSeconds(0.08f);
            ProtoHarness.ChainRush.Visuals.ChainVisual visual = null;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == "Grapple Chain") visual = root.GetComponent<ProtoHarness.ChainRush.Visuals.ChainVisual>();
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.IsVisible, Is.True);
            game.StartRun();
            yield return null;
            Assert.That(visual.IsVisible, Is.False);
            Assert.That(grapple.IsAttached, Is.False);
        }

        // A steering bot (TrackFollower) on seeds 1 and 2: jumps just before jump gaps, jumps early and grapples
        // over grapple gaps, and attacks every enemy. COURSE.md T3c-1 completion: 1400 m on both seeds.
        [UnityTest, Timeout(300000)]
        public IEnumerator Endless_LongRun_TwoSeedsReach1400mAndRestartWithoutGrowingPool()
        {
            int objects = CountObjects();
            int pool = course.PoolSize;
            Time.timeScale = 3f;
            foreach (int seed in new[] { 1, 2 })
            {
                course.Seed = seed;
                game.StartRun();
                var bot = new TrackFollower(game.Track, player);
                game.SetInputSource(bot);
                float deadline = Time.realtimeSinceStartup + 100f;
                double lastDistance = 0d;
                while (game.IsRunning && course.Distance < 1400d && Time.realtimeSinceStartup < deadline)
                {
                    bool gapAhead = course.Stream.TryNextGap(game.Track.Project(player.transform.position).S, out _, out _, out ModuleKind gap);
                    float edge = course.DistanceToEdge();
                    float lead = gap == ModuleKind.GrappleGap ? 4.5f : 2f;
                    if (gapAhead && player.IsGrounded && edge <= lead) player.PrimaryAction();
                    if (gapAhead && gap == ModuleKind.GrappleGap && !player.IsGrounded && !grapple.IsAttached
                        && game.Track.Project(player.transform.position).H > 2.6f) grapple.TryAttach();
                    if (enemies.CanAttack) game.Attack();
                    Assert.That(course.Distance, Is.GreaterThanOrEqualTo(lastDistance - 0.01d), $"seed {seed}: distance went back");
                    lastDistance = course.Distance;
                    yield return null;
                }
                Assert.That(game.IsRunning, Is.True, $"seed {seed}: failed at {course.Distance:0.0} m; player={player.transform.position}; health={game.Health}");
                Assert.That(course.Distance, Is.GreaterThanOrEqualTo(1400d), $"seed {seed}: timed out");
                Assert.That(course.RebaseCount, Is.GreaterThanOrEqualTo(3), $"seed {seed}: rebases");
                Assert.That(course.RecycledCount, Is.GreaterThan(16), $"seed {seed}: road pieces reused");
                Assert.That(game.Hits, Is.GreaterThan(3), $"seed {seed}: rest encounters must actually spawn");
                Assert.That(course.PoolSize, Is.EqualTo(pool));
                Assert.That(CountObjects(), Is.EqualTo(objects), $"seed {seed}: object count");
                Debug.Log($"ChainRush endless seed {seed}: 1400 m, rebases {course.RebaseCount}, road reuses {course.RecycledCount}, hits {game.Hits}, health {game.Health}.");
            }
            game.StartRun();
            Assert.That(course.RebaseCount, Is.Zero);
            Assert.That(course.RecycledCount, Is.Zero);
            Assert.That(course.Distance, Is.EqualTo(0d).Within(0.1d));
            Assert.That(player.IsGrounded, Is.True);
        }

        private static int CountObjects()
        {
            int count = 0;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) count += root.GetComponentsInChildren<Transform>(true).Length;
            return count;
        }
    }
}
