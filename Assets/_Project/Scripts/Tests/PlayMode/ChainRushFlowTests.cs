using System;
using System.Collections;
using NUnit.Framework;
using ProtoHarness.ChainRush;
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

        private TrackDefinition made;

        [SetUp]
        public void SetUp() => RaceLaunch.Clear();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RaceLaunch.Clear();
            if (made != null) UnityEngine.Object.Destroy(made);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // ---- the conditions ------------------------------------------------------------------------------

        [Test]
        public void RaceSetup_MissingTrackOrRunner_Throws()
        {
            made = ScriptableObject.CreateInstance<TrackDefinition>();
            Assert.Throws<ArgumentNullException>(() => new RaceSetup(null, "", RaceMode.Single, SpeedClass.Fast, "R"));
            Assert.Throws<ArgumentNullException>(() => new RaceSetup(made, null, RaceMode.Single, SpeedClass.Fast, "R"));
            Assert.Throws<ArgumentException>(() => new RaceSetup(made, "", RaceMode.Single, SpeedClass.Fast, ""));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RaceSetup(made, "", (RaceMode)7, SpeedClass.Fast, "R"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RaceSetup(made, "", RaceMode.Single, (SpeedClass)7, "R"));
        }

        [Test]
        public void RaceLaunch_SecondRequestBeforeTaking_ThrowsAndTakingEmptiesIt()
        {
            made = ScriptableObject.CreateInstance<TrackDefinition>();
            var setup = new RaceSetup(made, "", RaceMode.Single, SpeedClass.Fast, "R");
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
            Assert.That(setup.Runner, Is.EqualTo(LobbyMenu.DefaultRunner));
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
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Main));
            Assert.That(menu.Cursor, Is.EqualTo(2), "Back on the settings entry.");
        }

        // ---- the race scene ------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Race_FromTheLobby_RacesTheChosenDefinitionAndCountsIn()
        {
            made = ScriptableObject.CreateInstance<TrackDefinition>();
            Assert.That(made.TryValidate(out string error), Is.True, error);
            RaceLaunch.Request(new RaceSetup(made, "", RaceMode.Single, SpeedClass.Fast, LobbyMenu.DefaultRunner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            ChainRushGame game = Find<ChainRushGame>(assembler.gameObject.scene);
            CircuitRace circuit = Find<CircuitRace>(assembler.gameObject.scene);
            Assert.That(RaceLaunch.HasPending, Is.False, "The race scene took the request.");
            Assert.That(assembler.IsLaunched, Is.True);
            Assert.That(circuit.Definition, Is.SameAs(made), "The circuit is built from the lobby's definition, not the saved one.");
            Assert.That(assembler.IsAssembling, Is.False, "No environment scene: it counts in at once.");
            Assert.That(game.IsCountingDown, Is.True);
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
            RaceLaunch.Request(new RaceSetup(Stadium(), "", RaceMode.Single, SpeedClass.VeryFast, LobbyMenu.DefaultRunner));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("cannot be raced yet"));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            ChainRushGame game = Find<ChainRushGame>(assembler.gameObject.scene);
            Assert.That(assembler.IsLaunched, Is.False);
            Assert.That(game.enabled, Is.False);
            Assert.That(game.IsCountingDown, Is.False);
        }

        // ---- the whole loop, by scene name ---------------------------------------------------------------

        // Needs the four scenes in the build's scene list (M2-a2); until then it is reported as ignored, not passed.
        [UnityTest, Timeout(60000)]
        public IEnumerator Loop_ByName_RaceAssemblesWithItsEnvironmentAndGoesBackToTheLobby()
        {
            foreach (string path in new[] { TitlePath, LobbyPath, RacePath, EnvironmentPath })
                if (SceneUtility.GetBuildIndexByScenePath(path) < 0)
                    Assert.Ignore("Not in the build's scene list yet (M2-a2): " + path);
            RaceLaunch.Request(new RaceSetup(Stadium(), EnvironmentScene, RaceMode.Single, SpeedClass.Fast, LobbyMenu.DefaultRunner));
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

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.TryGetComponent(out T component)) return component;
            Assert.Fail($"Scene '{scene.name}' has no {typeof(T).Name} at its roots.");
            return null;
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
