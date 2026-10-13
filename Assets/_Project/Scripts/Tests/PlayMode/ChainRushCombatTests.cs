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
    // Encounters, the chain attack and the grapple on the procedural course. These are the combat tests of
    // the old straight endless course, moved here with the same assertions: only where the runner starts
    // changed (the spawn rest instead of a z value on the old decks, a laid gap instead of its fixed place).
    public sealed class ChainRushCombatTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/ChainRushProcedural.unity";
        // The first module is a 60 m rest and no gap comes before S 75, so an encounter can start at the spawn.
        private const ulong SpawnSeed = 3UL;
        private const float StandingHeight = 1.05f;

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
        private ProceduralCourse course;
        private EnemyDirector enemies;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("Combat tests require the Unity Editor.");
            yield break;
#endif
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame g)) game = g;
                if (root.TryGetComponent(out RunnerMotor p)) { player = p; grapple = p.GetComponent<GrappleController>(); }
                if (root.TryGetComponent(out ProceduralCourse c)) course = c;
            }
            Assert.That(game, Is.Not.Null);
            Assert.That(course, Is.Not.Null);
            enemies = game.Enemies;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator Prepare(EnemyDirector.Entrance direction)
        {
            course.SetSeed(SpawnSeed);
            game.StartRun();
            yield return LandOnRoad();
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
            double gapStart = FindGapSpot(false);
            Assert.That(gapStart, Is.GreaterThan(0d), "No seed below 100 lays a gap in its first 300 m.");
            // Ten metres before a gap there is no room for an encounter.
            Teleport(gapStart - 10d);
            yield return LandOnRoad();
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
            double gapStart = FindGapSpot(true);
            Assert.That(gapStart, Is.GreaterThan(0d), "No seed below 100 lays a grapple gap in its first 300 m.");
            // Four metres before the edge, like the old deck test: jump, then grab the anchor over the gap.
            Teleport(gapStart - 4d);
            yield return LandOnRoad();
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

        // ---- helpers (the same shapes as ChainRushProceduralTests) --------------------------------------

        // Seeds 0..99 until a gap (with an anchor when asked) shows up in the first 300 m, past S 40; returns
        // its start S. The course is left laid for that seed with the run started.
        private double FindGapSpot(bool needAnchor)
        {
            for (ulong seed = 0; seed < 100; seed++)
            {
                course.SetSeed(seed);
                game.StartRun();
                if (course.TryGetNextGap(out double startS, out _, out bool hasAnchor) && (!needAnchor || hasAnchor) && startS > 40d) return startS;
            }
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
            Assert.That(player.IsGrounded, Is.True, "Runner never landed on the procedural road.");
        }
    }
}
