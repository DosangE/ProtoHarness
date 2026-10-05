using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Track;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    // Slopes run on a test road built beside the prototype course: a mesh strip that follows a
    // centerline handed to the game with SetTrack. The scene itself has no slopes yet.
    public sealed class ChainRushSlopeTests
    {
        private const float RoadX = 300f;
        private const float RoadHalfWidth = 4f;
        // ChainRushPrototype.unity runner values (COURSE.md section 2): runSpeed 10; slope factor 1.5, scale 0.7..1.3.
        private const float RunSpeed = 10f;

        private ChainRushGame game;
        private RunnerMotor player;
        private float previousTimeScale;
        private readonly List<Object> built = new List<Object>();

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
            }
            Assert.That(game, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale;
            for (int i = 0; i < built.Count; i++) if (built[i] != null) Object.Destroy(built[i]);
            built.Clear();
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Downhill_TwentyPercent_StaysGrounded()
        {
            Centerline line = SlopeRoad(-0.2f);
            yield return StartOn(line, 110d);
            yield return RunTo(line, 32d);
            int ticks = 0;
            int airborne = 0;
            float deadline = Time.time + 10f;
            while (game.IsRunning && S(line) < 68d && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                ticks++;
                if (!player.IsGrounded) airborne++;
            }
            Assert.That(game.IsRunning, Is.True, "Run ended on the downhill at s=" + S(line));
            Assert.That(ticks, Is.GreaterThan(100), "Too few samples on the slope.");
            Assert.That(airborne, Is.Zero, "Lost ground on " + airborne + " of " + ticks + " ticks.");
        }

        [UnityTest]
        public IEnumerator Slope_ConstantGrade_ScalesRunSpeed([Values(-0.2f, 0.2f)] float grade)
        {
            Centerline line = SlopeRoad(grade);
            yield return StartOn(line, 110d);
            yield return RunTo(line, 40d);
            double fromS = S(line);
            int fromTick = game.Tick;
            yield return RunTo(line, 65d);
            float measured = (float)((S(line) - fromS) / Ticks.ToSeconds(game.Tick - fromTick));
            float expected = RunSpeed * Mathf.Clamp(1f - 1.5f * grade, 0.7f, 1.3f);
            Assert.That(measured, Is.EqualTo(expected).Within(0.5f), "Grade " + grade + ": ds/dt " + measured);
        }

        // Measures the flat-takeoff jump against landing heights (COURSE.md 6-1) and logs the table.
        // The road ends just past takeoff so the arc continues below the takeoff height.
        [UnityTest]
        public IEnumerator Jump_FlatTakeoff_ReachesPastGapLimit()
        {
            var line = new Centerline(new Vector3(RoadX, 0f, 0f), 0f);
            line.AppendStraight(200f);
            yield return StartOn(line, 20d);
            yield return RunTo(line, 17d);
            Assert.That(player.IsGrounded, Is.True);
            double takeoffS = S(line);
            float takeoffY = player.transform.position.y;
            float takeoffSpeed = player.Velocity.z;
            player.PrimaryAction();
            var samples = new List<Vector2> { Vector2.zero };
            float deadline = Time.time + 4f;
            while (game.IsRunning && player.transform.position.y - takeoffY > -6.5f && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                samples.Add(new Vector2((float)(S(line) - takeoffS), player.transform.position.y - takeoffY));
            }
            Assert.That(game.IsRunning, Is.True);
            float apex = float.NegativeInfinity;
            for (int i = 0; i < samples.Count; i++) apex = Mathf.Max(apex, samples[i].y);
            var table = new System.Text.StringBuilder();
            table.Append("ChainRush jump reach (takeoff speed ").Append(takeoffSpeed.ToString("0.00"))
                .Append(" m/s, apex ").Append(apex.ToString("0.00")).Append(" m):");
            float[] heights = { 3f, 2f, 1f, 0f, -1f, -3f, -6f };
            float flatReach = float.NaN;
            for (int h = 0; h < heights.Length; h++)
            {
                float reach = ReachAt(samples, heights[h]);
                if (heights[h] == 0f) flatReach = reach;
                table.Append(" dh ").Append(heights[h].ToString("+0;-0;0")).Append(" = ")
                    .Append(float.IsNaN(reach) ? "not reached" : reach.ToString("0.00") + " m").Append(';');
            }
            Debug.Log(table.ToString());
            // COURSE.md 6-1: gaps without a grapple are planned at <= 8 m on flat ground.
            Assert.That(flatReach, Is.GreaterThanOrEqualTo(8f), table.ToString());
        }

        // Horizontal distance where the falling branch of the arc first crosses height dh.
        private static float ReachAt(List<Vector2> samples, float dh)
        {
            for (int i = 1; i < samples.Count; i++)
            {
                Vector2 a = samples[i - 1];
                Vector2 b = samples[i];
                if (b.y >= a.y || a.y < dh || b.y > dh) continue;
                return Mathf.Lerp(a.x, b.x, (a.y - dh) / (a.y - b.y));
            }
            return float.NaN;
        }

        // Flat 20 m, ease into the grade over 10 m, hold it for 40 m, ease out over 10 m, flat 30 m.
        private static Centerline SlopeRoad(float grade)
        {
            var line = new Centerline(new Vector3(RoadX, 0f, 0f), 0f);
            line.AppendStraight(20f);
            line.AppendStraight(10f, grade);
            line.AppendStraight(40f);
            line.AppendStraight(10f, 0f);
            line.AppendStraight(30f);
            return line;
        }

        private IEnumerator StartOn(Centerline line, double roadEndS)
        {
            BuildRoad(line, -5d, roadEndS);
            game.SetTrack(line);
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
            float deadline = Time.time + 10f;
            while (game.IsRunning && S(line) < s && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(game.IsRunning, Is.True, "Run ended before s=" + s + " (at " + S(line) + ").");
            Assert.That(S(line), Is.GreaterThanOrEqualTo(s), "Timed out before s=" + s + ".");
        }

        private double S(Centerline line) => line.Project(player.transform.position).S;

        // A road strip along the centerline, sampled every 0.5 m, faces up (clockwise seen from above).
        private void BuildRoad(Centerline line, double fromS, double toS)
        {
            int segments = Mathf.CeilToInt((float)((toS - fromS) / 0.5d));
            var vertices = new Vector3[(segments + 1) * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                TrackFrame frame = line.FrameAt(fromS + (toS - fromS) * i / segments);
                vertices[i * 2] = frame.TransformPoint(new Vector3(-RoadHalfWidth, 0f, 0f));
                vertices[i * 2 + 1] = frame.TransformPoint(new Vector3(RoadHalfWidth, 0f, 0f));
            }
            for (int i = 0; i < segments; i++)
            {
                int left = i * 2;
                int t = i * 6;
                triangles[t] = left;
                triangles[t + 1] = left + 2;
                triangles[t + 2] = left + 1;
                triangles[t + 3] = left + 1;
                triangles[t + 4] = left + 2;
                triangles[t + 5] = left + 3;
            }
            var mesh = new Mesh { name = "Slope test road" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            var road = new GameObject("Slope Test Road");
            road.AddComponent<MeshCollider>().sharedMesh = mesh;
            built.Add(road);
            built.Add(mesh);
            Physics.SyncTransforms();
        }
    }
}
