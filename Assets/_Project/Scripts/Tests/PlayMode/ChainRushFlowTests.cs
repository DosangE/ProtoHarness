using System;
using System.Collections;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Flow;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Track;
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
    // The game's skeleton (DESIGN.md §5-1 M2, DECISIONS 2026-10-19 "M2 씬 구성"): the lobby builds a RaceSetup page by
    // page and refuses locked entries; the race scene takes the setup (RaceLaunch) and assembles itself from it, or runs as
    // saved when opened directly. Scenes are loaded by path here, so only the last test needs the build's scene list.
    public sealed class ChainRushFlowTests
    {
        private const string LobbyPath = "Assets/_Project/Scenes/ChainRushLobby.unity";
        private const string RacePath = "Assets/_Project/Scenes/ChainRushCircuit.unity";
        private const string TitlePath = "Assets/_Project/Scenes/ChainRushTitle.unity";
        private const string EnvironmentPath = "Assets/_Project/Scenes/ChainRushTrackStadium.unity";
        private const string EnvironmentScene = "ChainRushTrackStadium";
        private const string StadiumPath = "Assets/_Project/Data/Circuit_Stadium.asset";

        private const string AlphaPath = "Assets/_Project/Data/RunnerProfile_Alpha.asset";
        private const string BetaPath = "Assets/_Project/Data/RunnerProfile_Beta.asset";

        private TrackDefinition made;
        // A runner of all 1s: races exactly as the scene's values.
        private RunnerProfile runner;

        [SetUp]
        public void SetUp()
        {
            RaceLaunch.Clear();
            runner = ScriptableObject.CreateInstance<RunnerProfile>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RaceLaunch.Clear();
            if (made != null) UnityEngine.Object.Destroy(made);
            if (runner != null) UnityEngine.Object.Destroy(runner);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // ---- the conditions ------------------------------------------------------------------------------

        [Test]
        public void RaceSetup_MissingTrackOrRunner_Throws()
        {
            made = ScriptableObject.CreateInstance<TrackDefinition>();
            Assert.Throws<ArgumentNullException>(() => new RaceSetup(null, "", RaceMode.Single, SpeedClass.Fast, runner));
            Assert.Throws<ArgumentNullException>(() => new RaceSetup(made, null, RaceMode.Single, SpeedClass.Fast, runner));
            Assert.Throws<ArgumentNullException>(() => new RaceSetup(made, "", RaceMode.Single, SpeedClass.Fast, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RaceSetup(made, "", (RaceMode)7, SpeedClass.Fast, runner));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RaceSetup(made, "", RaceMode.Single, (SpeedClass)7, runner));
        }

        [Test]
        public void RaceLaunch_SecondRequestBeforeTaking_ThrowsAndTakingEmptiesIt()
        {
            made = ScriptableObject.CreateInstance<TrackDefinition>();
            var setup = new RaceSetup(made, "", RaceMode.Single, SpeedClass.Fast, runner);
            RaceLaunch.Request(setup);
            Assert.Throws<InvalidOperationException>(() => RaceLaunch.Request(setup));
            Assert.That(RaceLaunch.TryTake(out RaceSetup taken), Is.True);
            Assert.That(taken.Track, Is.SameAs(made));
            Assert.That(RaceLaunch.HasPending, Is.False);
            Assert.That(RaceLaunch.TryTake(out _), Is.False);
            Assert.Throws<ArgumentException>(() => RaceLaunch.Request(default));
        }

        // ---- the lobby -----------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Lobby_SingleThroughTrackSpeedAndRunner_BuildsTheChosenSetup()
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Main));
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Track));
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Speed));
            Assert.That(menu.Cursor, Is.EqualTo(1), "The speed page opens on Fast, the only class unlocked.");
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Runner));
            RaceSetup setup = menu.BuildSetup();
            Assert.That(setup.Track, Is.SameAs(Stadium()));
            Assert.That(setup.EnvironmentScene, Is.EqualTo(EnvironmentScene));
            Assert.That(setup.Mode, Is.EqualTo(RaceMode.Single));
            Assert.That(setup.Speed, Is.EqualTo(SpeedClass.Fast));
#if UNITY_EDITOR
            Assert.That(setup.Runner, Is.SameAs(AssetDatabase.LoadAssetAtPath<RunnerProfile>(AlphaPath)), "The runner page opens on the first runner, ALPHA.");
#endif
            Assert.That(RaceLaunch.HasPending, Is.False, "Nothing is requested before the runner is confirmed.");
        }

        [UnityTest]
        public IEnumerator Lobby_LockedEntries_RefuseWithANoticeAndStay()
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            menu.Move(1);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Main), "Multi is locked.");
            Assert.That(menu.Notice, Is.Not.Empty);
            menu.Move(-1);
            Assert.That(menu.Notice, Is.Empty, "Moving clears the notice.");
            menu.Confirm();
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Speed));
            menu.Move(-1);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Speed), "Normal is locked.");
            Assert.That(menu.Notice, Is.Not.Empty);
            menu.Move(2);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Speed), "Very fast is locked (the cursor wrapped to it).");
            Assert.That(menu.BuildSetup().Speed, Is.EqualTo(SpeedClass.Fast), "A refused entry is not chosen.");
        }

        [UnityTest]
        public IEnumerator Lobby_BackAndSettings_GoPageByPage()
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            menu.Confirm();
            menu.Confirm();
            menu.Back();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Track));
            menu.Back();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Main));
            menu.Move(2);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Settings));
            menu.Back();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Main));
            Assert.That(menu.Cursor, Is.EqualTo(2), "Back on the settings entry.");
        }

        // ---- settings (M2-c, user 2026-10-20) --------------------------------------------------------------

        [UnityTest]
        public IEnumerator Lobby_SettingsPage_AdjustsOnlyTheHighlightedSettingAndOpensTheKeys()
        {
            var store = TestSettings();
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            menu.Settings = store;
            try
            {
                menu.Adjust(1);
                Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Main));
                Assert.That(menu.Cursor, Is.EqualTo(0), "Adjusting does nothing off the settings page.");
                menu.Move(2);
                menu.Confirm();
                Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Settings));
                Assert.That(menu.Cursor, Is.EqualTo(0));
                menu.Adjust(1);
                Assert.That(store.MasterLevel, Is.EqualTo(GameSettings.MaxLevel), "A volume stops at the top.");
                for (int i = 0; i < 11; i++) menu.Adjust(-1);
                Assert.That(store.MasterLevel, Is.EqualTo(0), "A volume stops at zero, it does not wrap.");
                menu.Move(1);
                menu.Adjust(-1);
                Assert.That(store.MusicLevel, Is.EqualTo(GameSettings.MaxLevel - 1));
                Assert.That(store.MasterLevel, Is.EqualTo(0));
                Assert.That(store.EffectsLevel, Is.EqualTo(GameSettings.MaxLevel));
                menu.Move(1);
                menu.Adjust(-1);
                Assert.That(store.EffectsLevel, Is.EqualTo(GameSettings.MaxLevel - 1));
                Assert.That(store.MusicLevel, Is.EqualTo(GameSettings.MaxLevel - 1));
                Assert.That(store.SpeedFieldOfView, Is.True);
                menu.Move(1);
                menu.Adjust(1);
                Assert.That(store.SpeedFieldOfView, Is.False, "Either direction switches the speed view.");
                menu.Confirm();
                Assert.That(store.SpeedFieldOfView, Is.True, "Enter switches it too.");
                Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Settings));
                menu.Move(-3);
                menu.Confirm();
                Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Settings), "Enter on a volume row does nothing.");
                Assert.That(store.MasterLevel, Is.EqualTo(0));
                menu.Move(4);
                menu.Adjust(-1);
                menu.Adjust(1);
                Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Settings), "The keys row has nothing to adjust.");
                Assert.That(store.MasterLevel, Is.EqualTo(0));
                Assert.That(store.MusicLevel, Is.EqualTo(GameSettings.MaxLevel - 1));
                Assert.That(store.EffectsLevel, Is.EqualTo(GameSettings.MaxLevel - 1));
                Assert.That(store.SpeedFieldOfView, Is.True);
                menu.Confirm();
                Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Keys));
                menu.Back();
                Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Settings));
                Assert.That(menu.Cursor, Is.EqualTo(4), "Back on the keys row.");
                menu.Back();
                Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Main));
                Assert.That(menu.Cursor, Is.EqualTo(2), "Back on the settings entry.");
            }
            finally
            {
                store.Clear();
            }
        }

        // ---- the race scene ------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Race_FromTheLobby_RacesTheChosenDefinitionAndCountsIn()
        {
            made = ScriptableObject.CreateInstance<TrackDefinition>();
            Assert.That(made.TryValidate(out string error), Is.True, error);
            RaceLaunch.Request(new RaceSetup(made, "", RaceMode.Single, SpeedClass.Fast, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            ChainRushGame game = Find<ChainRushGame>(assembler.gameObject.scene);
            CircuitRace circuit = Find<CircuitRace>(assembler.gameObject.scene);
            Assert.That(RaceLaunch.HasPending, Is.False, "The race scene took the request.");
            Assert.That(assembler.IsLaunched, Is.True);
            Assert.That(circuit.Definition, Is.SameAs(made), "The circuit is built from the lobby's definition, not the saved one.");
            Assert.That(Find<RunnerMotor>(assembler.gameObject.scene).Profile, Is.SameAs(runner), "The runner races the lobby's profile.");
            Assert.That(assembler.IsAssembling, Is.False, "No environment scene: it counts in at once.");
            Assert.That(game.IsCountingDown, Is.True);
        }

        // The chosen runner's look is what the race shows (presentation only, RunnerLook): BETA from the lobby puts BETA's
        // look on the runner model, part by part in order.
        [UnityTest]
        public IEnumerator Race_FromTheLobby_WearsTheChosenRunnersLook()
        {
            RunnerProfile beta = Beta();
            Assert.That(beta.Look, Is.Not.Null, "BETA has no look.");
            RaceLaunch.Request(new RaceSetup(Stadium(), "", RaceMode.Single, SpeedClass.Fast, beta));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            Assert.That(assembler.IsLaunched, Is.True);
            Transform body = Find<RunnerTilt>(assembler.gameObject.scene).Body;
            MeshFilter[] worn = body.GetComponentsInChildren<MeshFilter>(true);
            MeshFilter[] look = beta.Look.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(worn.Length, Is.EqualTo(look.Length));
            Assert.That(worn.Length, Is.GreaterThan(0));
            for (int i = 0; i < worn.Length; i++)
            {
                Assert.That(worn[i].sharedMesh, Is.SameAs(look[i].sharedMesh), worn[i].name);
                Assert.That(worn[i].GetComponent<MeshRenderer>().sharedMaterials,
                    Is.EqualTo(look[i].GetComponent<MeshRenderer>().sharedMaterials), worn[i].name);
            }
        }

        [UnityTest]
        public IEnumerator Race_OpenedDirectly_RunsAsSavedAndWaitsForEnter()
        {
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            ChainRushGame game = Find<ChainRushGame>(assembler.gameObject.scene);
            CircuitRace circuit = Find<CircuitRace>(assembler.gameObject.scene);
            Assert.That(assembler.IsLaunched, Is.False);
            Assert.That(circuit.Definition, Is.SameAs(Stadium()));
            Assert.That(game.IsReady, Is.True);
            Assert.Throws<InvalidOperationException>(assembler.ReturnToLobby, "A scene opened directly has no lobby.");
            made = ScriptableObject.CreateInstance<TrackDefinition>();
            Assert.Throws<InvalidOperationException>(() => circuit.UseDefinition(made), "The circuit is already built.");
            Assert.That(circuit.Definition, Is.SameAs(Stadium()));
        }

        [UnityTest]
        public IEnumerator Race_LockedSpeedClass_IsRefusedLoudlyAndDoesNotRun()
        {
            RaceLaunch.Request(new RaceSetup(Stadium(), "", RaceMode.Single, SpeedClass.VeryFast, runner));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("cannot be raced yet"));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            ChainRushGame game = Find<ChainRushGame>(assembler.gameObject.scene);
            Assert.That(assembler.IsLaunched, Is.False);
            Assert.That(game.enabled, Is.False);
            Assert.That(game.IsCountingDown, Is.False);
        }

        // ---- best records (user 2026-10-19) ----------------------------------------------------------------

        [UnityTest]
        public IEnumerator Lobby_TrackPage_ShowsTheBestRecordOrNone()
        {
            var store = TestRecords();
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            menu.Records = store;
            Assert.That(menu.TrackRecordText(0), Is.EqualTo("기록 없음"));
            store.Submit(Stadium().name, SpeedClass.Fast, 3970, "BETA");
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Track));
            Assert.That(menu.TrackRecordText(0), Is.EqualTo("최고 기록  01:19.4  ·  BETA"), "Read again as the track page opens.");
            store.Clear(Stadium().name, SpeedClass.Fast);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Race_FinishFromTheLobby_KeepsItAsTheBestRecord()
        {
            var store = TestRecords();
            RaceLaunch.Request(new RaceSetup(Stadium(), "", RaceMode.Single, SpeedClass.Fast, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = store;
            Scene scene = assembler.gameObject.scene;
            ChainRushGame game = Find<ChainRushGame>(scene);
            RunnerMotor player = Find<RunnerMotor>(scene);
            CircuitRace circuit = Find<CircuitRace>(scene);
            float scale = Time.timeScale;
            try
            {
                game.SetInputSource(new CircuitBot(game, player, player.GetComponent<GrappleController>(), circuit));
                Time.timeScale = 6f;
                game.StartRun();
                float deadline = Time.realtimeSinceStartup + 90f;
                while (!game.HasFinished && !game.HasFailed && Time.realtimeSinceStartup < deadline) yield return null;
                Time.timeScale = scale;
                Assert.That(game.HasFinished, Is.True, "The bot did not finish three laps.");
                yield return null;
                Assert.That(assembler.LastFinishWasBest, Is.True);
                Assert.That(store.TryGet(Stadium().name, SpeedClass.Fast, out BestRecords.Record best), Is.True);
                Assert.That(best.Ticks, Is.EqualTo(circuit.Laps.TotalTicks));
                Assert.That(best.Runner, Is.EqualTo(runner.DisplayName));
            }
            finally
            {
                Time.timeScale = scale;
                store.Clear(Stadium().name, SpeedClass.Fast);
            }
        }

        // ---- the whole loop, by scene name ---------------------------------------------------------------

        // Needs the four scenes in the build's scene list (M2-a2); until then it is reported as ignored, not passed.
        [UnityTest, Timeout(60000)]
        public IEnumerator Loop_ByName_RaceAssemblesWithItsEnvironmentAndGoesBackToTheLobby()
        {
            foreach (string path in new[] { TitlePath, LobbyPath, RacePath, EnvironmentPath })
                if (SceneUtility.GetBuildIndexByScenePath(path) < 0)
                    Assert.Ignore("Not in the build's scene list yet (M2-a2): " + path);
            RaceLaunch.Request(new RaceSetup(Stadium(), EnvironmentScene, RaceMode.Single, SpeedClass.Fast, runner));
            SceneManager.LoadScene(RaceLaunch.RaceScene);
            yield return null;
            RaceAssembler assembler = Find<RaceAssembler>(SceneManager.GetActiveScene());
            ChainRushGame game = Find<ChainRushGame>(SceneManager.GetActiveScene());
            float deadline = Time.realtimeSinceStartup + 20f;
            while (assembler.IsAssembling && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(assembler.IsAssembling, Is.False, "The environment never finished loading.");
            Assert.That(SceneManager.GetSceneByName(EnvironmentScene).isLoaded, Is.True);
            Assert.That(game.IsCountingDown, Is.True);
            game.StartRun();
            game.FailRun();
            assembler.ReturnToLobby();
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(RaceLaunch.LobbyScene));
            Assert.That(SceneManager.GetSceneByName(EnvironmentScene).isLoaded, Is.False, "The environment leaves with the race.");
        }

        // ---- helpers -------------------------------------------------------------------------------------

        private static IEnumerator Load(string path, Action<Scene> found)
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("Flow tests require the Unity Editor.");
            yield break;
#endif
            found(SceneManager.GetActiveScene());
            yield return null;
        }

        // Records under a test prefix, empty for the stadium at Fast, so the player's records are never touched.
        private BestRecords TestRecords()
        {
            var store = new BestRecords("ChainRush.Test.FlowTests.");
            store.Clear(Stadium().name, SpeedClass.Fast);
            return store;
        }

        // Settings under a test prefix, at their defaults, so the player's settings are never touched.
        private GameSettings TestSettings()
        {
            var store = new GameSettings("ChainRush.Test.ChainRushFlowTests.Settings.");
            store.Clear();
            return store;
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.TryGetComponent(out T component)) return component;
            Assert.Fail($"Scene '{scene.name}' has no {typeof(T).Name} at its roots.");
            return null;
        }

        private static RunnerProfile Beta()
        {
#if UNITY_EDITOR
            var beta = AssetDatabase.LoadAssetAtPath<RunnerProfile>(BetaPath);
            Assert.That(beta, Is.Not.Null, BetaPath);
            return beta;
#else
            Assert.Fail("Flow tests require the Unity Editor.");
            return null;
#endif
        }

        private static TrackDefinition Stadium()
        {
#if UNITY_EDITOR
            var stadium = AssetDatabase.LoadAssetAtPath<TrackDefinition>(StadiumPath);
            Assert.That(stadium, Is.Not.Null, StadiumPath);
            return stadium;
#else
            Assert.Fail("Flow tests require the Unity Editor.");
            return null;
#endif
        }
    }
}
