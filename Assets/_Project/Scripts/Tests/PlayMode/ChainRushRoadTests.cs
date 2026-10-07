using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Track;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    // Procedural road pieces (COURSE.md T3a) laid along a centerline beside the prototype course: a bot
    // runs curves and a crest across piece seams, a procedural guard turns an unsteered runner, and an open
    // edge lets the runner fall.
    public sealed class ChainRushRoadTests
    {
        private const float RoadX = 300f;
        private const float PieceLength = 50f;
        private const float HalfWidth = RoadProfile.DeckHalfWidth;
        // Guard inner face minus the runner's 0.38 m capsule radius.
        private const float InnerEdge = HalfWidth - 0.38f;
        private static readonly double R30Exit = 30d + 30d * Mathf.PI / 2d;

        private ChainRushGame game;
        private RunnerMotor player;
        private Material material;
        private readonly List<RoadPiece> pieces = new List<RoadPiece>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
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
            }
            Assert.That(game, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null, "URP Lit shader is required for the visible test road.");
            material = new Material(shader) { name = "Test procedural road" };
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = 0; i < pieces.Count; i++) pieces[i].Destroy();
            pieces.Clear();
            if (material != null) Object.Destroy(material);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Road_BotRunsCurvesAndCrest_StaysGroundedAcrossPieces()
        {
            Centerline line = CourseRoad();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Lay(line, RoadProfile.Guarded);
            watch.Stop();
            Debug.Log("ChainRush road build: " + pieces.Count + " pieces over " + (line.EndS + 5d).ToString("0.0") + " m in "
                + watch.Elapsed.TotalMilliseconds.ToString("0.00") + " ms ("
                + (watch.Elapsed.TotalMilliseconds / pieces.Count).ToString("0.00") + " ms per piece, first build included).");
            var bot = new TrackFollower(line, player);
            yield return StartOn(line, bot);
            double goal = line.EndS - 10d;
            float maxOffset = 0f;
            int ticks = 0;
            int airborne = 0;
            float deadline = Time.time + 40f;
            while (game.IsRunning && S(line) < goal && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                maxOffset = Mathf.Max(maxOffset, Mathf.Abs(line.Project(player.transform.position).D));
                ticks++;
                if (!player.IsGrounded) airborne++;
            }
            Assert.That(game.IsRunning, Is.True, "Run ended on the procedural road: " + State(line) + ".");
            Assert.That(S(line), Is.GreaterThanOrEqualTo(goal), "Timed out on the procedural road.");
            Assert.That(maxOffset, Is.LessThan(InnerEdge - 0.3f), "The bot touched a guard (max offset " + maxOffset + ").");
            Assert.That(airborne, Is.EqualTo(0), "Lost ground on " + airborne + " of " + ticks + " ticks.");
        }

        [UnityTest]
        public IEnumerator Road_NoSteeringIntoR30_ProceduralGuardTurnsRunner()
        {
            Centerline line = CourseRoad();
            Lay(line, RoadProfile.Guarded);
            var still = new TrackFollower(line, player) { Steering = false };
            yield return StartOn(line, still);
            float minOffset = 0f;
            float deadline = Time.time + 20f;
            while (game.IsRunning && S(line) < R30Exit + 15d && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                minOffset = Mathf.Min(minOffset, line.Project(player.transform.position).D);
            }
            Assert.That(game.IsRunning, Is.True, "An unsteered runner must not fall on a guarded procedural curve: " + State(line) + ".");
            Assert.That(S(line), Is.GreaterThanOrEqualTo(R30Exit + 15d), "The guard should carry the runner through the curve.");
            Assert.That(minOffset, Is.LessThan(-InnerEdge + 0.3f), "Going straight into a right curve must reach the outer (left) guard.");
            Assert.That(minOffset, Is.GreaterThan(-InnerEdge - 0.1f), "The runner went through the outer guard.");
        }

        [UnityTest]
        public IEnumerator Road_OpenLeftEdge_SteeringOffFalls()
        {
            var line = new Centerline(new Vector3(RoadX, 0f, 0f), 0f);
            line.AppendStraight(100f);
            Lay(line, new RoadProfile(HalfWidth, RoadProfile.DeckThickness, false, true));
            var hold = new TrackFollower(line, player) { Steering = false };
            yield return StartOn(line, hold);
            // About 50 degrees to the left, then straight off the open edge.
            hold.Override = -1f;
            yield return new WaitForSeconds(0.4f);
            hold.Override = 0f;
            float minOffset = 0f;
            float deadline = Time.time + 5f;
            while (game.IsRunning && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                minOffset = Mathf.Min(minOffset, line.Project(player.transform.position).D);
            }
            Assert.That(game.HasFailed, Is.True, "Running off an open edge must end the run as a fall.");
            Assert.That(minOffset, Is.LessThan(-HalfWidth - 1f), "The runner should have left the road on the left: " + State(line) + ".");
        }

        // Straight 30 m, R30 right quarter turn, straight 20 m, a crest that eases up to 10% and back down to
        // -10% and flat over 90 m, an R50 left 60 degree turn, straight 30 m: about 270 m, short of the finish.
        private static Centerline CourseRoad()
        {
            var line = new Centerline(new Vector3(RoadX, 0f, 0f), 0f);
            line.AppendStraight(30f);
            line.AppendArc(30f, 90f);
            line.AppendStraight(20f);
            line.AppendStraight(30f, 0.1f);
            line.AppendStraight(30f, -0.1f);
            line.AppendStraight(30f, 0f);
            line.AppendArc(50f, -60f);
            line.AppendStraight(30f);
            return line;
        }

        // Covers [-5, EndS] with pieces of at most PieceLength that meet end to end.
        private void Lay(Centerline line, RoadProfile profile)
        {
            for (double from = -5d; from < line.EndS; from += PieceLength)
            {
                var piece = new RoadPiece("Test road piece " + pieces.Count, null, material, material, material);
                pieces.Add(piece);
                piece.Build(line, from, System.Math.Min(from + PieceLength, line.EndS), profile);
            }
            Physics.SyncTransforms();
        }

        private IEnumerator StartOn(Centerline line, IInputSource source)
        {
            game.SetTrack(line);
            game.SetInputSource(source);
            game.StartRun();
            Vector3 start = line.FrameAt(5d).TransformPoint(new Vector3(0f, 1.2f, 0f));
            player.ShiftOrigin(start - player.transform.position);
            Physics.SyncTransforms();
            // IsGrounded still reports the spawn deck until the controller moves once on the new road.
            yield return new WaitForFixedUpdate();
            float deadline = Time.time + 2f;
            while (!player.IsGrounded && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.True, "Runner never landed on the procedural road.");
        }

        private double S(Centerline line) => line.Project(player.transform.position).S;

        private string State(Centerline line)
        {
            TrackCoord coord = line.Project(player.transform.position);
            return "S " + coord.S.ToString("0.00") + ", D " + coord.D.ToString("0.00") + ", H " + coord.H.ToString("0.00")
                + ", position " + player.transform.position + ", velocity " + player.Velocity
                + ", grounded " + player.IsGrounded + ", failed " + game.HasFailed + ", finished " + game.HasFinished;
        }
    }
}
