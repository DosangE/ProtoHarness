using System;
using System.Collections;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Visuals;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    // Runner profiles (DECISIONS 2026-10-19 "M2-b 러너 프로필"): a profile scales the scene's runner values; ALPHA (all 1s)
    // runs bit for bit as the scene does, any other profile repeats bit for bit, and the stats show in how the runners go.
    public sealed class ChainRushProfileTests
    {
        private const string CircuitScene = "Assets/_Project/Scenes/ChainRushCircuit.unity";
        private const string DataFolder = "Assets/_Project/Data/";
        // The runner model the race scene's runner is built from (ChainRushSceneBuilder.RunnerModelPath).
        private const string RunnerModelPath = "Assets/_Project/Prefabs/ArmoredRunner.prefab";

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
            Assert.Fail("Profile tests require the Unity Editor.");
            yield break;
#endif
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame g)) game = g;
                if (root.TryGetComponent(out RunnerMotor p)) { player = p; grapple = p.GetComponent<GrappleController>(); }
                if (root.TryGetComponent(out CircuitRace c)) circuit = c;
            }
            Assert.That(game, Is.Not.Null);
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

        [Test]
        public void Profiles_TheFiveRunners_AreValidAndAlphaIsAllOnes()
        {
            string[] names = { "Alpha", "Beta", "Theta", "Delta", "Omega" };
            foreach (string name in names)
            {
                RunnerProfile profile = Load(name);
                Assert.That(profile.TryValidate(out string error), Is.True, error);
                Assert.That(profile.DisplayName, Is.EqualTo(name.ToUpperInvariant()));
            }
            RunnerProfile alpha = Load("Alpha");
            Assert.That(new[] { alpha.TopSpeedScale, alpha.AccelerationScale, alpha.GaugeChargeScale, alpha.Weight }, Is.All.EqualTo(1f),
                "ALPHA is the circuit as tuned: every scale exactly 1.");
        }

        // Each runner wears a look (Art/Models/Runner*.prefab) that fits the runner model: RunnerLook takes it on a copy of
        // the model, and the copy then shows the look's meshes.
        [Test]
        public void Profiles_TheFiveRunners_EachHaveALookThatFitsTheRunnerModel()
        {
#if UNITY_EDITOR
            var runnerModel = AssetDatabase.LoadAssetAtPath<GameObject>(RunnerModelPath);
            Assert.That(runnerModel, Is.Not.Null, RunnerModelPath);
            foreach (string name in new[] { "Alpha", "Beta", "Theta", "Delta", "Omega" })
            {
                RunnerProfile profile = Load(name);
                Assert.That(profile.Look, Is.Not.Null, profile.name + " has no look.");
                GameObject copy = UnityEngine.Object.Instantiate(runnerModel);
                try
                {
                    Assert.DoesNotThrow(() => RunnerLook.Apply(copy.transform, profile.Look), profile.name);
                    Assert.That(copy.GetComponentInChildren<MeshFilter>(true).sharedMesh,
                        Is.SameAs(profile.Look.GetComponentInChildren<MeshFilter>(true).sharedMesh), profile.name);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                }
            }
#else
            Assert.Fail("Profile tests require the Unity Editor.");
#endif
        }

        [Test]
        public void Configure_ScalesTheValuesTheRunRunsOn()
        {
            float run = player.RunSpeed, accel = player.RunAcceleration, top = player.SlingTopSpeed, carry = player.SlingCarrySpeed;
            float empowered = player.EmpoweredReleaseSpeed, meters = player.SwingMetersPerSlot;
            Assert.That(player.Profile, Is.Null, "A scene opened directly runs on its own values.");
            RunnerProfile delta = Load("Delta");
            player.Configure(delta);
            Assert.That(player.Profile, Is.SameAs(delta));
            Assert.That(player.RunSpeed, Is.EqualTo(run * delta.TopSpeedScale));
            Assert.That(player.SlingTopSpeed, Is.EqualTo(top * delta.TopSpeedScale));
            Assert.That(player.SlingCarrySpeed, Is.EqualTo(carry * delta.TopSpeedScale));
            Assert.That(player.EmpoweredReleaseSpeed, Is.EqualTo(empowered * delta.TopSpeedScale));
            Assert.That(player.RunAcceleration, Is.EqualTo(accel * delta.AccelerationScale));
            Assert.That(player.SwingMetersPerSlot, Is.EqualTo(meters / delta.GaugeChargeScale));
            player.Configure(Load("Alpha"));
            Assert.That(player.RunSpeed, Is.EqualTo(run), "Back to all 1s: the scene's values exactly.");
            Assert.That(player.SwingMetersPerSlot, Is.EqualTo(meters));
        }

        [Test]
        public void Configure_NullInvalidOrDuringARun_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => player.Configure(null));
            var broken = ScriptableObject.CreateInstance<RunnerProfile>();
            try
            {
#if UNITY_EDITOR
                // The profile says so itself, loudly, as it is edited (OnValidate).
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("must be positive"));
                var data = new SerializedObject(broken);
                data.FindProperty("topSpeedScale").floatValue = 0f;
                data.ApplyModifiedPropertiesWithoutUndo();
#endif
                Assert.Throws<ArgumentException>(() => player.Configure(broken));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(broken);
            }
            game.StartRun();
            Assert.Throws<InvalidOperationException>(() => player.Configure(Load("Beta")));
            game.TogglePause();
            Assert.Throws<InvalidOperationException>(() => player.Configure(Load("Beta")), "Paused is still during the run.");
            game.TogglePause();
            game.FailRun();
            Assert.DoesNotThrow(() => player.Configure(Load("Beta")), "After the run it may change.");
        }

        // ALPHA must not change a single bit: the same bot inputs on the scene's values and on ALPHA trace the same run.
        [UnityTest, Timeout(120000)]
        public IEnumerator Alpha_RacesBitForBitAsTheScene()
        {
            const int ticks = 700;
            var log = new InputLog();
            StateTrace scene = Trace(new InputRecorder(new CircuitBot(game, player, grapple, circuit), log));
            yield return Run(scene, ticks, 3f);
            player.Configure(Load("Alpha"));
            StateTrace alpha = Trace(new InputReplay(log));
            yield return Run(alpha, ticks, 6f);
            Assert.That(StateTrace.Compare(scene, alpha, ticks), Is.Null);
            Assert.That(alpha.Hash(ticks), Is.EqualTo(scene.Hash(ticks)));
        }

        // Another runner's run is as repeatable as ALPHA's, and differs from it.
        [UnityTest, Timeout(120000)]
        public IEnumerator OtherRunners_RepeatBitForBit_AndDifferFromAlpha()
        {
            const int ticks = 700;
            var log = new InputLog();
            StateTrace alpha = Trace(new InputRecorder(new CircuitBot(game, player, grapple, circuit), log));
            yield return Run(alpha, ticks, 3f);
            foreach (string name in new[] { "Beta", "Theta" })
            {
                player.Configure(Load(name));
                StateTrace first = Trace(new InputReplay(log));
                yield return Run(first, ticks, 6f);
                StateTrace second = Trace(new InputReplay(log));
                yield return Run(second, ticks, 6f);
                Assert.That(StateTrace.Compare(first, second, ticks), Is.Null, name + " twice");
                Assert.That(second.Hash(ticks), Is.EqualTo(first.Hash(ticks)), name + " twice");
                Assert.That(first.Hash(ticks), Is.Not.EqualTo(alpha.Hash(ticks)), name + " must race differently from ALPHA.");
                Debug.Log($"ChainRush profile {name}: {ticks} ticks repeat, hash 0x{first.Hash(ticks):X16}.");
            }
        }

        // From the start line, before the first hill (about 15 m), acceleration decides who is ahead:
        // BETA (x1.2) ahead of ALPHA ahead of THETA (x0.85).
        [UnityTest, Timeout(60000)]
        public IEnumerator Acceleration_FromTheLine_PutsBetaAheadOfAlphaAheadOfTheta()
        {
            const int ticks = 75;
            var distance = new double[3];
            string[] names = { "Beta", "Alpha", "Theta" };
            for (int i = 0; i < names.Length; i++)
            {
                player.Configure(Load(names[i]));
                // Read at exactly that tick: a frame at 3x runs a few ticks, so the run itself overshoots by a varying count.
                int runner = i;
                Action<int> sample = tick => { if (tick == ticks) distance[runner] = circuit.Laps.Progress; };
                game.TickCompleted += sample;
                yield return Run(new CircuitBot(game, player, grapple, circuit), ticks, 3f);
                game.TickCompleted -= sample;
            }
            Debug.Log($"ChainRush profile start: after {ticks} ticks BETA {distance[0]:0.00} m, ALPHA {distance[1]:0.00} m, THETA {distance[2]:0.00} m.");
            Assert.That(distance[0], Is.GreaterThan(distance[1]));
            Assert.That(distance[1], Is.GreaterThan(distance[2]));
        }

        // ---- helpers -------------------------------------------------------------------------------

        private static RunnerProfile Load(string name)
        {
#if UNITY_EDITOR
            var profile = AssetDatabase.LoadAssetAtPath<RunnerProfile>(DataFolder + "RunnerProfile_" + name + ".asset");
            Assert.That(profile, Is.Not.Null, "RunnerProfile_" + name);
            return profile;
#else
            Assert.Fail("Profile tests require the Unity Editor.");
            return null;
#endif
        }

        private StateTrace Trace(IInputSource inner) => new StateTrace(game, player, inner,
            ("lapProgress", () => circuit.Laps.Progress),
            ("gauge", () => player.Gauge));

        // Starts a run on `source`, lets it go until the game has taken at least `ticks` ticks, then ends it so the
        // runner may be configured again.
        private IEnumerator Run(IInputSource source, int ticks, float scale)
        {
            game.SetInputSource(source);
            Time.timeScale = scale;
            game.StartRun();
            float deadline = Time.realtimeSinceStartup + 60f;
            while (game.IsRunning && game.Tick < ticks && Time.realtimeSinceStartup < deadline) yield return null;
            Time.timeScale = timeScale;
            Assert.That(game.IsRunning, Is.True, $"The run ended at tick {game.Tick}.");
            Assert.That(game.Tick, Is.GreaterThanOrEqualTo(ticks), "Timed out before the wanted number of ticks.");
            game.FailRun();
        }
    }
}
