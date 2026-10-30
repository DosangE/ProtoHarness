using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Audio;
using ProtoHarness.ChainRush.Control;
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
        private const string SkywayPath = "Assets/_Project/Data/Circuit_Skyway.asset";
        private const string SkywayEnvironmentScene = "ChainRushTrackSkyway";
        private const string FingerLanesPath = "Assets/_Project/Data/Circuit_FingerLanes.asset";
        private const string FingerLanesEnvironmentScene = "ChainRushTrackFingerLanes";
        private const string PalmCoastPath = "Assets/_Project/Data/Circuit_PalmCoast.asset";

        private const string TestTitle = "TEST TRACK";
        // A made definition is named as an asset is: the race keeps its records and ghost under the name.
        private const string TestTrackAsset = "TestTrack";

        private const string AlphaPath = "Assets/_Project/Data/RunnerProfile_Alpha.asset";
        private const string BetaPath = "Assets/_Project/Data/RunnerProfile_Beta.asset";

        private TrackDefinition made;
        // A runner of all 1s: races exactly as the scene's values. Named, as a profile asset is.
        private RunnerProfile runner;
        // Ghosts in a folder of the tests' own, emptied after each test, so the player's ghosts are never touched.
        private GhostFiles ghosts;

        [SetUp]
        public void SetUp()
        {
            RaceLaunch.Clear();
            runner = ScriptableObject.CreateInstance<RunnerProfile>();
            runner.name = "TestRunner";
            ghosts = new GhostFiles(Path.Combine(Path.GetTempPath(), "ProtoHarnessTests", "FlowGhosts"));
            DeleteGhosts();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RaceLaunch.Clear();
            if (made != null) UnityEngine.Object.Destroy(made);
            if (runner != null) UnityEngine.Object.Destroy(runner);
            DeleteGhosts();
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // ---- the conditions ------------------------------------------------------------------------------

        [Test]
        public void RaceSetup_MissingTrackOrRunner_Throws()
        {
            made = ScriptableObject.CreateInstance<TrackDefinition>();
            made.name = TestTrackAsset;
            Assert.Throws<ArgumentNullException>(() => new RaceSetup(null, TestTitle, "", RaceMode.Single, SpeedClass.Fast, runner));
            Assert.Throws<ArgumentNullException>(() => new RaceSetup(made, TestTitle, null, RaceMode.Single, SpeedClass.Fast, runner));
            Assert.Throws<ArgumentNullException>(() => new RaceSetup(made, TestTitle, "", RaceMode.Single, SpeedClass.Fast, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RaceSetup(made, TestTitle, "", (RaceMode)7, SpeedClass.Fast, runner));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RaceSetup(made, TestTitle, "", RaceMode.Single, (SpeedClass)7, runner));
        }

        // The HUD names the track by this title (it was "STADIUM" on every circuit, SKYWAY included, until 2026-10-23).
        [Test]
        public void RaceSetup_MissingTitle_Throws()
        {
            made = ScriptableObject.CreateInstance<TrackDefinition>();
            made.name = TestTrackAsset;
            foreach (string title in new[] { null, "", "  " })
                Assert.Throws<ArgumentException>(() => new RaceSetup(made, title, "", RaceMode.Single, SpeedClass.Fast, runner), "title '" + title + "'");
            Assert.That(new RaceSetup(made, TestTitle, "", RaceMode.Single, SpeedClass.Fast, runner).Title, Is.EqualTo(TestTitle));
        }

        [Test]
        public void RaceLaunch_SecondRequestBeforeTaking_ThrowsAndTakingEmptiesIt()
        {
            made = ScriptableObject.CreateInstance<TrackDefinition>();
            made.name = TestTrackAsset;
            var setup = new RaceSetup(made, TestTitle, "", RaceMode.Single, SpeedClass.Fast, runner);
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
        // Speed first (user 2026-10-29), so the track page can show that speed's records.
        public IEnumerator Lobby_SingleThroughSpeedTrackAndRunner_BuildsTheChosenSetup()
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Main));
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Speed));
            Assert.That(menu.Cursor, Is.EqualTo(1), "The speed page opens on Fast.");
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Track));
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Runner));
            RaceSetup setup = menu.BuildSetup();
            Assert.That(setup.Track, Is.SameAs(Stadium()));
            Assert.That(setup.Title, Is.EqualTo("STADIUM"));
            Assert.That(setup.EnvironmentScene, Is.EqualTo(EnvironmentScene));
            Assert.That(setup.Mode, Is.EqualTo(RaceMode.Single));
            Assert.That(setup.Speed, Is.EqualTo(SpeedClass.Fast));
#if UNITY_EDITOR
            Assert.That(setup.Runner, Is.SameAs(AssetDatabase.LoadAssetAtPath<RunnerProfile>(AlphaPath)), "The runner page opens on the first runner, ALPHA.");
#endif
            Assert.That(RaceLaunch.HasPending, Is.False, "Nothing is requested before the runner is confirmed.");
        }

        // The runner page's preview follows the highlight and really draws the runner; it stops drawing off that page.
        [UnityTest]
        public IEnumerator Lobby_RunnerPage_PreviewShowsAndDrawsTheHighlightedRunner()
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            RunnerPreview preview = menu.Preview;
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.Rendering, Is.False, "The main menu draws no preview.");
            menu.Confirm();
            menu.Confirm();
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Runner));
            Assert.That(preview.Rendering, Is.True);
            Assert.That(preview.Shown, Is.EqualTo(0));
            for (int i = 0; i < 5; i++) Assert.That(preview.HasModel(i), Is.True, $"Runner {i} has a look.");
            Assert.That(preview.IsVisible(0), Is.True);

            menu.Move(1);
            Assert.That(preview.Shown, Is.EqualTo(1));
            Assert.That(preview.IsVisible(1), Is.True);
            Assert.That(preview.IsVisible(0), Is.False);
            for (int frame = 0; frame < 3; frame++) yield return null;
            yield return new WaitForEndOfFrame();
            Assert.That(DrawnPixels(preview.Texture), Is.GreaterThan(500), "The runner is drawn in the middle of the preview.");

            menu.Back();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Track));
            Assert.That(preview.Rendering, Is.False, "Leaving the runner page stops the preview.");
        }

        // On the runner page a click (Highlight) only picks the runner; the enter button or Enter (Confirm) starts the race.
        [UnityTest]
        public IEnumerator Lobby_RunnerPage_HighlightPicksWithoutStarting()
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            menu.Confirm();
            menu.Confirm();
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Runner));
            menu.Highlight(2);
            Assert.That(menu.Cursor, Is.EqualTo(2));
            Assert.That(menu.Preview.Shown, Is.EqualTo(2));
            Assert.That(menu.SetupRunner, Is.EqualTo(2), "RACE SETUP names the runner being picked (user 2026-10-20).");
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Runner));
            Assert.That(RaceLaunch.HasPending, Is.False, "Highlighting starts nothing.");
            menu.Highlight(0);
            Assert.That(menu.Cursor, Is.EqualTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => menu.Highlight(5));
            Assert.Throws<ArgumentOutOfRangeException>(() => menu.Highlight(-1));
            Assert.That(menu.Cursor, Is.EqualTo(0));
            menu.Highlight(3);
            menu.Back();
            Assert.That(menu.SetupRunner, Is.EqualTo(0), "Off the runner page it names the runner last entered with (none yet: the first).");
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
        }

        // The speed classes opened with M3 (DECISIONS 2026-10-25 "M3 속도 단계"): each one goes on to the track page and into the setup.
        [UnityTest]
        public IEnumerator Lobby_SpeedClasses_AreAllOpen_AndTheChosenOneGoesIntoTheSetup()
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Speed));
            Assert.That(menu.BuildSetup().Speed, Is.EqualTo(SpeedClass.Fast), "Fast until another is chosen.");
            menu.Move(-1);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Track), "Normal is open.");
            Assert.That(menu.Notice, Is.Empty);
            Assert.That(menu.BuildSetup().Speed, Is.EqualTo(SpeedClass.Normal));
            menu.Back();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Speed));
            menu.Move(2);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Track), "Very fast is open.");
            Assert.That(menu.BuildSetup().Speed, Is.EqualTo(SpeedClass.VeryFast));
        }

        // The tracks still to come are listed after the scene's tracks, locked (user 2026-10-21; the skyway took the
        // second placeholder's place the same day, DECISIONS "SKYWAY LOOP"; Finger Lanes came before the last one,
        // DECISIONS 2026-10-25 "FINGER LANES"; Palm Coast too, DECISIONS 2026-10-30 "PALM COAST").
        [UnityTest]
        public IEnumerator Lobby_PlaceholderTracks_RefuseWithANoticeAndStay()
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            menu.Confirm();
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Track));
            menu.Highlight(4);
            Assert.That(menu.TrackRecordText(4), Is.EqualTo("준비 중"));
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Track), "The placeholder track is locked.");
            Assert.That(menu.Notice, Is.Not.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => menu.Highlight(5), "The stadium, the skyway, Finger Lanes, Palm Coast and one placeholder.");
            menu.Move(1);
            Assert.That(menu.Cursor, Is.EqualTo(0), "The cursor wraps from the last placeholder to the stadium.");
            Assert.That(menu.BuildSetup().Track, Is.SameAs(Stadium()), "A refused track is not chosen.");
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Runner));
        }

        // The lobby tells UiSound each moment once (user 2026-10-21; the sounds come later): a move that moves the highlight,
        // a choice, a refusal and a step back. A move that lands where it was says nothing.
        [UnityTest]
        public IEnumerator Lobby_MovesChoicesRefusalsAndBack_EachPlayTheirCueOnce()
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            var heard = new System.Collections.Generic.List<UiSound.Cue>();
            Action<UiSound.Cue> listen = heard.Add;
            UiSound.Played += listen;
            try
            {
                menu.Move(1);
                Assert.That(heard, Is.EqualTo(new[] { UiSound.Cue.Move }));
                heard.Clear();
                menu.Confirm();
                Assert.That(heard, Is.EqualTo(new[] { UiSound.Cue.Denied }), "Multi is locked.");
                heard.Clear();
                menu.Highlight(1);
                Assert.That(heard, Is.Empty, "Highlighting the row already highlighted moves nothing.");
                menu.Highlight(0);
                menu.Confirm();
                Assert.That(heard, Is.EqualTo(new[] { UiSound.Cue.Move, UiSound.Cue.Confirm }));
                Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Speed));
                heard.Clear();
                menu.Back();
                Assert.That(heard, Is.EqualTo(new[] { UiSound.Cue.Back }));
                Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Main));
            }
            finally
            {
                UiSound.Played -= listen;
            }
        }

        // The main, speed and track pages describe every entry, each differently; settings has no description panel.
        [UnityTest]
        public IEnumerator Lobby_MainTrackAndSpeedEntries_EachHaveADescription()
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            foreach (LobbyMenu.Page expected in new[] { LobbyMenu.Page.Main, LobbyMenu.Page.Speed, LobbyMenu.Page.Track })
            {
                Assert.That(menu.CurrentPage, Is.EqualTo(expected));
                var seen = new System.Collections.Generic.HashSet<string>();
                for (int row = 0; row < 3; row++)
                {
                    menu.Highlight(row);
                    Assert.That(menu.Description, Is.Not.Empty, $"{expected} row {row}");
                    Assert.That(seen.Add(menu.Description), Is.True, $"{expected} row {row} repeats another row's description.");
                }
                // Main: single race (0); speed: fast (1); track: the stadium (0).
                menu.Highlight(expected == LobbyMenu.Page.Speed ? 1 : 0);
                menu.Confirm();
            }
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Runner));
            Assert.That(menu.Description, Is.Empty, "The runner page shows its profile, not a description.");
            menu.Back();
            menu.Back();
            menu.Back();
            menu.Highlight(2);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Settings));
            Assert.That(menu.Description, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Lobby_BackAndSettings_GoPageByPage()
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            menu.Confirm();
            menu.Confirm();
            menu.Back();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Speed));
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

        // The screen rows (2026-10-26) sit below the keys row; the editor does not apply them (ScreenOptions).
        [UnityTest]
        public IEnumerator Lobby_SettingsPage_ScreenRowsChangeOnlyTheirOwnSetting()
        {
            var store = TestSettings();
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            menu.Settings = store;
            try
            {
                menu.Move(2);
                menu.Confirm();
                Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Settings));
                menu.Move(5);
                Assert.That(menu.Cursor, Is.EqualTo(5), "The window mode is the row after the keys.");
                menu.Adjust(1);
                Assert.That(store.Windowed, Is.True, "Either direction switches the window mode.");
                menu.Confirm();
                Assert.That(store.Windowed, Is.False, "Enter switches it too.");
                Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Settings));
                menu.Move(1);
                menu.Adjust(-1);
                Assert.That(store.ResolutionIndex, Is.EqualTo(ScreenOptions.AutoResolution), "The resolution stops at Auto, it does not wrap.");
                menu.Adjust(1);
                Assert.That(store.ResolutionIndex, Is.EqualTo(1));
                for (int i = 0; i < ScreenOptions.ResolutionCount + 1; i++) menu.Adjust(1);
                Assert.That(store.ResolutionIndex, Is.EqualTo(ScreenOptions.ResolutionCount - 1), "The resolution stops at the largest.");
                menu.Confirm();
                Assert.That(store.ResolutionIndex, Is.EqualTo(ScreenOptions.ResolutionCount - 1), "Enter on the resolution does nothing.");
                menu.Move(1);
                menu.Adjust(-1);
                Assert.That(store.VSync, Is.True, "Either direction switches VSync.");
                menu.Confirm();
                Assert.That(store.VSync, Is.False, "Enter switches it too.");
                Assert.That(store.Windowed, Is.False);
                Assert.That(store.MasterLevel, Is.EqualTo(GameSettings.MaxLevel), "The volumes are untouched.");
                Assert.That(store.SpeedFieldOfView, Is.True);
                menu.Move(1);
                Assert.That(menu.Cursor, Is.EqualTo(0), "The list wraps from VSync to the first row.");
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
            made.name = TestTrackAsset;
            Assert.That(made.TryValidate(out string error), Is.True, error);
            RaceLaunch.Request(new RaceSetup(made, TestTitle, "", RaceMode.Single, SpeedClass.Fast, runner));
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
            Assert.That(Find<ChainRushHud>(assembler.gameObject.scene).Heading, Is.EqualTo(TestTitle), "The HUD names the lobby's track.");
        }

        // ESC's pause menu in a race from the lobby (user 2026-10-20): continue, restart and back to the lobby, the
        // highlight wrapping over all three and back on continue once the game runs again.
        [UnityTest]
        public IEnumerator Race_PauseMenuFromTheLobby_ContinuesRestartsAndGoesBackToTheLobby()
        {
            made = ScriptableObject.CreateInstance<TrackDefinition>();
            made.name = TestTrackAsset;
            Assert.That(made.TryValidate(out string error), Is.True, error);
            RaceLaunch.Request(new RaceSetup(made, TestTitle, "", RaceMode.Single, SpeedClass.Fast, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            ChainRushGame game = Find<ChainRushGame>(assembler.gameObject.scene);
            ChainRushHud hud = Find<ChainRushHud>(assembler.gameObject.scene);
            Assert.Throws<InvalidOperationException>(() => hud.ChoosePause(ChainRushHud.PauseChoice.Continue), "Not paused: no menu.");
            game.StartRun();
            game.TogglePause();
            Assert.That(game.IsPaused, Is.True);
            foreach (ChainRushHud.PauseChoice choice in Enum.GetValues(typeof(ChainRushHud.PauseChoice)))
                Assert.That(hud.Offers(choice), Is.True, choice.ToString());
            Assert.That(hud.PauseCursor, Is.EqualTo(ChainRushHud.PauseChoice.Continue));
            hud.MovePause(1);
            Assert.That(hud.PauseCursor, Is.EqualTo(ChainRushHud.PauseChoice.Restart));
            hud.MovePause(1);
            Assert.That(hud.PauseCursor, Is.EqualTo(ChainRushHud.PauseChoice.Lobby));
            hud.MovePause(1);
            Assert.That(hud.PauseCursor, Is.EqualTo(ChainRushHud.PauseChoice.Continue), "Wraps around.");
            hud.MovePause(-1);
            Assert.That(hud.PauseCursor, Is.EqualTo(ChainRushHud.PauseChoice.Lobby));

            hud.ChoosePause(ChainRushHud.PauseChoice.Continue);
            Assert.That(game.IsRunning, Is.True);
            yield return null;
            Assert.That(hud.PauseCursor, Is.EqualTo(ChainRushHud.PauseChoice.Continue), "The next pause opens on continue.");

            yield return new WaitForSeconds(0.2f);
            Assert.That(game.Tick, Is.GreaterThan(0));
            game.TogglePause();
            hud.ChoosePause(ChainRushHud.PauseChoice.Restart);
            Assert.That(game.IsCountingDown, Is.True, "Restart counts in a new run.");
            Assert.That(game.Tick, Is.Zero, "From the start.");

            game.StartRun();
            game.TogglePause();
            hud.ChoosePause(ChainRushHud.PauseChoice.Lobby);
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(RaceLaunch.LobbyScene));
        }

        // A race opened directly has no lobby to go back to; with ManualStart off (a network race) no restart either.
        [UnityTest]
        public IEnumerator Race_PauseMenuOpenedDirectly_OffersOnlyWhatTheRaceCanDo()
        {
            ChainRushGame game = null;
            yield return Load(RacePath, scene => game = Find<ChainRushGame>(scene));
            ChainRushHud hud = Find<ChainRushHud>(game.gameObject.scene);
            game.StartRun();
            game.TogglePause();
            Assert.That(hud.Offers(ChainRushHud.PauseChoice.Restart), Is.True);
            Assert.That(hud.Offers(ChainRushHud.PauseChoice.Lobby), Is.False);
            hud.MovePause(1);
            Assert.That(hud.PauseCursor, Is.EqualTo(ChainRushHud.PauseChoice.Restart));
            hud.MovePause(1);
            Assert.That(hud.PauseCursor, Is.EqualTo(ChainRushHud.PauseChoice.Continue), "Skips the lobby.");
            Assert.Throws<InvalidOperationException>(() => hud.ChoosePause(ChainRushHud.PauseChoice.Lobby));

            game.ManualStart = false;
            try
            {
                Assert.That(hud.Offers(ChainRushHud.PauseChoice.Restart), Is.False);
                hud.MovePause(1);
                Assert.That(hud.PauseCursor, Is.EqualTo(ChainRushHud.PauseChoice.Continue), "Continue is all there is.");
                Assert.Throws<InvalidOperationException>(() => hud.ChoosePause(ChainRushHud.PauseChoice.Restart));
                Assert.That(game.IsPaused, Is.True, "A refused entry changes nothing.");
            }
            finally
            {
                game.ManualStart = true;
            }
        }

        // The chosen runner's look is what the race shows (presentation only, RunnerLook): BETA from the lobby puts BETA's
        // look on the runner model, part by part in order.
        [UnityTest]
        public IEnumerator Race_FromTheLobby_WearsTheChosenRunnersLook()
        {
            RunnerProfile beta = Beta();
            Assert.That(beta.Look, Is.Not.Null, "BETA has no look.");
            RaceLaunch.Request(new RaceSetup(Stadium(), "STADIUM", "", RaceMode.Single, SpeedClass.Fast, beta));
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
            Assert.That(Find<ChainRushHud>(assembler.gameObject.scene).Heading, Is.EqualTo("CIRCUIT"), "No lobby: no track name to show.");
            Assert.Throws<InvalidOperationException>(assembler.ReturnToLobby, "A scene opened directly has no lobby.");
            made = ScriptableObject.CreateInstance<TrackDefinition>();
            made.name = TestTrackAsset;
            Assert.Throws<InvalidOperationException>(() => circuit.UseDefinition(made), "The circuit is already built.");
            Assert.That(circuit.Definition, Is.SameAs(Stadium()));
        }

        [UnityTest]
        public IEnumerator Race_Multi_IsRefusedLoudlyAndDoesNotRun()
        {
            RaceLaunch.Request(new RaceSetup(Stadium(), "STADIUM", "", RaceMode.Multi, SpeedClass.Fast, runner));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("cannot be raced yet"));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            ChainRushGame game = Find<ChainRushGame>(assembler.gameObject.scene);
            Assert.That(assembler.IsLaunched, Is.False);
            Assert.That(game.enabled, Is.False);
            Assert.That(game.IsCountingDown, Is.False);
        }

        // The lobby's speed class reaches the runner (SpeedScale, DECISIONS 2026-10-25 "M3 속도 단계").
        [UnityTest]
        public IEnumerator Race_VeryFastFromTheLobby_ScalesTheRunnersSpeeds()
        {
            RaceLaunch.Request(new RaceSetup(Stadium(), "STADIUM", "", RaceMode.Single, SpeedClass.VeryFast, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            Assert.That(assembler.IsLaunched, Is.True);
            RunnerMotor player = Find<RunnerMotor>(assembler.gameObject.scene);
            Assert.That(player.SpeedClassScale, Is.EqualTo(SpeedScale.VeryFast));
            // ChainRushCircuit.unity's runner runs at 20 m/s (M1-T, 2026-10-18); the test runner's profile is all 1s.
            Assert.That(player.RunSpeed, Is.EqualTo(20f * SpeedScale.VeryFast).Within(1e-4f));
        }

        // ---- best records (user 2026-10-19) ----------------------------------------------------------------

        [UnityTest]
        public IEnumerator Lobby_TrackPage_ShowsTheBestRecordOrNone()
        {
            var store = TestRecords();
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            menu.Records = store;
            Assert.That(menu.TrackRecordText(0), Is.EqualTo("빠름 기록 없음"));
            store.Submit(Stadium().name, SpeedClass.Fast, 3970, "BETA");
            menu.Confirm();
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Track));
            Assert.That(menu.TrackRecordText(0), Is.EqualTo("최고 기록 (빠름)  01:19.4  ·  BETA"), "Read again as the track page opens.");
            store.Clear(Stadium().name, SpeedClass.Fast);
        }

        // The report that moved the speed page first (user 2026-10-29): with a record at Fast only, choosing Normal shows
        // Normal's (none), and going back to choose Fast shows Fast's — the track page always shows the speed just chosen.
        [UnityTest]
        public IEnumerator Lobby_TrackPage_ShowsTheRecordsOfTheSpeedJustChosen()
        {
            var store = TestRecords();
            store.Clear(Stadium().name, SpeedClass.Normal);
            store.Submit(Stadium().name, SpeedClass.Fast, 3970, "BETA");
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            menu.Records = store;
            try
            {
                menu.Confirm();
                menu.Highlight(0);
                menu.Confirm();
                Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Track));
                Assert.That(menu.TrackRecordText(0), Is.EqualTo("보통 기록 없음"));
                menu.Back();
                Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Speed));
                menu.Highlight(1);
                menu.Confirm();
                Assert.That(menu.TrackRecordText(0), Is.EqualTo("최고 기록 (빠름)  01:19.4  ·  BETA"));
            }
            finally
            {
                store.Clear(Stadium().name, SpeedClass.Fast);
                store.Clear(Stadium().name, SpeedClass.Normal);
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Race_FinishFromTheLobby_KeepsTheBestAndItsGhost_AndRunAgainRacesIt()
        {
            var store = TestRecords();
            RaceLaunch.Request(new RaceSetup(Stadium(), "STADIUM", "", RaceMode.Single, SpeedClass.Fast, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = store;
            assembler.Ghosts = ghosts;
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
                // M4-2: compared with what was kept before it — nothing, so a first record.
                Assert.That(assembler.HasLastFinish, Is.True);
                Assert.That(assembler.LastFinish.Ticks, Is.EqualTo(circuit.Laps.TotalTicks));
                Assert.That(assembler.LastFinish.BestBefore, Is.Zero);
                Assert.That(assembler.LastFinish.BestLine, Is.EqualTo("최고 기록  첫 기록"));
                Assert.That(store.TryGet(Stadium().name, SpeedClass.Fast, out BestRecords.Record best), Is.True);
                Assert.That(best.Ticks, Is.EqualTo(circuit.Laps.TotalTicks));
                Assert.That(best.Runner, Is.EqualTo(runner.DisplayName));
                // The best's run as the ghost for the track and speed, ending where the runner finished.
                Assert.That(ghosts.TryLoad(Stadium().name, SpeedClass.Fast, out GhostTrace saved), Is.True);
                Assert.That(saved.Count, Is.GreaterThan(0));
                Assert.That(saved[saved.Count - 1].Position, Is.EqualTo(player.transform.position));
                Assert.That(saved[saved.Count - 1].Heading, Is.EqualTo(player.Heading));
                // There was no ghost to race in this run; RUN AGAIN counts in against the one just saved.
                Assert.That(assembler.HasGhost, Is.False);
                game.StartCountdown();
                yield return null;
                Assert.That(assembler.HasGhost, Is.True, "RUN AGAIN after a new best races its ghost.");
                Assert.That(assembler.Ghost.Count, Is.EqualTo(saved.Count));
            }
            finally
            {
                Time.timeScale = scale;
                store.Clear(Stadium().name, SpeedClass.Fast);
            }
        }

        // ---- the ghost (M4-3, DECISIONS 2026-10-29 "M4-3 고스트 재생", "고스트: 트랙 × 속도의 최고 기록 하나") ------

        // The ghost belongs to the track and speed, not to a runner: whoever races there races it.
        [UnityTest, Timeout(60000)]
        public IEnumerator Race_TheBestsGhost_FollowsItsTraceTickByTick_AndHidesAtItsEnd()
        {
            var trace = new GhostTrace();
            for (int i = 0; i < 40; i++) trace.Add(new Vector3(i * 0.5f, 1f, 3f), i * 2f);
            ghosts.Save(Stadium().name, SpeedClass.Fast, trace);
            RaceLaunch.Request(new RaceSetup(Stadium(), "STADIUM", "", RaceMode.Single, SpeedClass.Fast, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = TestRecords();
            assembler.Ghosts = ghosts;
            yield return null;
            Assert.That(assembler.HasGhost, Is.True);
            GhostRunner ghost = assembler.Ghost;
            Assert.That(ghost.Count, Is.EqualTo(40));
            Assert.That(ghost.Visible, Is.True, "Waiting on its first entry before the start.");
            Assert.That(ghost.Position, Is.EqualTo(trace[0].Position));
            ChainRushGame game = Find<ChainRushGame>(assembler.gameObject.scene);
            float scale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                game.StartRun();
                var idle = new InputReplay(new InputLog());
                foreach (int tick in new[] { 1, 2, 25, 40 })
                {
                    while (game.Tick < tick) game.StepTick(idle);
                    yield return null;
                    Assert.That(ghost.Visible, Is.True, "tick " + tick);
                    Assert.That(ghost.Position, Is.EqualTo(trace[tick - 1].Position), "tick " + tick);
                }
                game.StepTick(idle);
                yield return null;
                Assert.That(ghost.Visible, Is.False, "Past its end the ghost has finished.");
                game.StartRun();
                yield return null;
                Assert.That(ghost.Visible, Is.True, "A restart brings it back.");
                Assert.That(ghost.Position, Is.EqualTo(trace[0].Position));
            }
            finally
            {
                Time.timeScale = scale;
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Race_ABrokenGhostFile_RacesWithoutAGhostAndSaysSo()
        {
            var trace = new GhostTrace();
            trace.Add(Vector3.zero, 0f);
            ghosts.Save(Stadium().name, SpeedClass.Fast, trace);
            File.WriteAllBytes(Directory.GetFiles(ghosts.Folder)[0], new byte[] { 1, 2, 3 });
            RaceLaunch.Request(new RaceSetup(Stadium(), "STADIUM", "", RaceMode.Single, SpeedClass.Fast, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = TestRecords();
            LogAssert.Expect(LogType.Error, new Regex("could not be read; racing without it"));
            assembler.Ghosts = ghosts;
            yield return null;
            Assert.That(assembler.HasGhost, Is.False);
            Assert.That(Find<ChainRushGame>(assembler.gameObject.scene).IsCountingDown, Is.True, "The race goes on without its ghost.");
        }

        // ---- the skyway (DECISIONS 2026-10-21 "SKYWAY LOOP") -----------------------------------------------

        [UnityTest]
        public IEnumerator Lobby_SkywayTrack_IsChosenWithItsEnvironment()
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            menu.Confirm();
            menu.Confirm();
            menu.Highlight(1);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Runner), "The skyway can be raced.");
            RaceSetup setup = menu.BuildSetup();
            Assert.That(setup.Track, Is.SameAs(Skyway()));
            Assert.That(setup.Title, Is.EqualTo("SKYWAY LOOP"));
            Assert.That(setup.EnvironmentScene, Is.EqualTo(SkywayEnvironmentScene));
        }

        // The elevated stretch (user 2026-10-22): the road is 30 m across from the wide corner before the long straight to
        // the one after it, and seven swing poles stand along the straight every 40 m, left first, 11 m off the centerline.
        [Test]
        public void Skyway_ElevatedStretch_IsWideWithSevenAlternatingSwingPoles()
        {
            TrackDefinition skyway = Skyway();
            Assert.That(skyway.TryValidate(out string error), Is.True, error);
            System.Collections.Generic.IReadOnlyList<TrackDefinition.Segment> segments = skyway.Segments;
            const int straight = 10;
            Assert.That(segments[straight].IsArc, Is.False);
            Assert.That(segments[straight].Length, Is.EqualTo(280f));
            double start = 0d;
            for (int i = 0; i < straight; i++) start += segments[i].Length;
            double end = start + segments[straight].Length;
            double cornerBefore = start - segments[straight - 1].Length, cornerAfter = end + segments[straight + 1].Length;
            for (double s = cornerBefore; s <= cornerAfter; s += 10d) Assert.That(skyway.HalfWidthAt(s), Is.EqualTo(15f).Within(1e-4f), "at S " + s.ToString("0"));
            Assert.That(skyway.HalfWidthAt(0d), Is.EqualTo(RoadProfile.DeckHalfWidth), "The start line keeps the usual road.");
            Assert.That(skyway.WidestHalfWidth, Is.EqualTo(15f));
            Assert.That(skyway.Poles.Count, Is.EqualTo(7));
            for (int i = 0; i < skyway.Poles.Count; i++)
            {
                TrackDefinition.Pole pole = skyway.Poles[i];
                Assert.That(pole.S, Is.EqualTo(start + 20d + 40d * i).Within(1e-3d), "pole " + i);
                Assert.That(pole.Offset, Is.EqualTo(i % 2 == 0 ? -11f : 11f), "pole " + i + " alternates, left first");
                Assert.That(pole.Reach, Is.EqualTo(10f));
                Assert.That(pole.S, Is.InRange(start, end), "pole " + i + " is on the straight");
            }
        }

        // The real lobby setup loads the decoration-only skyway environment, then the bot runs both laps: no fall,
        // no guard touched, each lap close to the lap length at 20 m/s. Returning unloads the decoration too.
        [UnityTest, Timeout(120000)]
        public IEnumerator Race_SkywayFromTheLobby_BotFinishesTwoLaps()
        {
            var store = TestRecords();
            TrackDefinition skyway = Skyway();
            store.Clear(skyway.name, SpeedClass.Fast);
            Assert.That(skyway.TryValidate(out string error), Is.True, error);
            LobbyMenu menu = null;
            yield return Load(LobbyPath, loaded => menu = Find<LobbyMenu>(loaded));
            menu.Confirm();
            menu.Confirm();
            menu.Highlight(1);
            menu.Confirm();
            RaceSetup setup = menu.BuildSetup();
            Assert.That(setup.Track, Is.SameAs(skyway));
            Assert.That(setup.EnvironmentScene, Is.EqualTo(SkywayEnvironmentScene));
            RaceLaunch.Request(setup);
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = store;
            assembler.Ghosts = ghosts;
            Scene scene = assembler.gameObject.scene;
            Assert.That(Find<ChainRushHud>(scene).Heading, Is.EqualTo("SKYWAY LOOP"), "Not the stadium's name on the skyway.");
            ChainRushGame game = Find<ChainRushGame>(scene);
            RunnerMotor player = Find<RunnerMotor>(scene);
            CircuitRace circuit = Find<CircuitRace>(scene);
            Assert.That(circuit.Definition, Is.SameAs(skyway));
            float assemblyDeadline = Time.realtimeSinceStartup + 20f;
            while (assembler.IsAssembling && Time.realtimeSinceStartup < assemblyDeadline) yield return null;
            Assert.That(assembler.IsAssembling, Is.False, "The Skyway decoration did not finish loading.");
            Scene environment = SceneManager.GetSceneByName(SkywayEnvironmentScene);
            Assert.That(environment.isLoaded, Is.True);
            Assert.That(environment.rootCount, Is.EqualTo(1), "Only the exterior prefab belongs in the decoration scene.");
            GameObject exterior = environment.GetRootGameObjects()[0];
            Assert.That(exterior.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
            Assert.That(exterior.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(exterior.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(exterior.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
            float scale = Time.timeScale;
            try
            {
                game.SetInputSource(new CircuitBot(game, player, player.GetComponent<GrappleController>(), circuit));
                Time.timeScale = 6f;
                game.StartRun();
                float deadline = Time.realtimeSinceStartup + 90f;
                float maxOffset = 0f;
                double maxOffsetS = 0d;
                while (!game.HasFinished && !game.HasFailed && Time.realtimeSinceStartup < deadline)
                {
                    TrackCoord coord = game.Track.Project(player.transform.position);
                    if (Mathf.Abs(coord.D) > maxOffset)
                    {
                        maxOffset = Mathf.Abs(coord.D);
                        maxOffsetS = coord.S;
                    }
                    yield return null;
                }
                Time.timeScale = scale;
                Assert.That(game.HasFailed, Is.False, "The bot fell or died.");
                Assert.That(game.HasFinished, Is.True, "The bot did not finish two laps.");
                LapCounter laps = circuit.Laps;
                Assert.That(laps.CompletedLaps, Is.EqualTo(2));
                double lapSeconds = skyway.LapLength / 20d;
                for (int lap = 1; lap <= 2; lap++)
                {
                    float seconds = Ticks.ToSeconds(laps.LapTicks(lap));
                    Debug.Log($"ChainRush skyway lap {lap}: {seconds:F2} s");
                    Assert.That(seconds, Is.EqualTo(lapSeconds).Within(lapSeconds * 0.2d), $"lap {lap} time");
                }
                Assert.That(maxOffset, Is.LessThan(RoadProfile.DeckHalfWidth - 0.4f), $"The bot touched a guard at S {maxOffsetS:F1} (segment {SegmentAt(skyway, maxOffsetS)}).");
                Debug.Log($"ChainRush skyway race: {skyway.LapLength:F1} m a lap, total {Ticks.ToSeconds(laps.TotalTicks):F2} s, max offset {maxOffset:F2} m at S {maxOffsetS:F1}.");
                assembler.ReturnToLobby();
                yield return null;
                yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(RaceLaunch.LobbyScene));
                Assert.That(SceneManager.GetSceneByName(SkywayEnvironmentScene).isLoaded, Is.False, "The Skyway exterior must leave with the race.");
            }
            finally
            {
                Time.timeScale = scale;
                store.Clear(skyway.name, SpeedClass.Fast);
            }
        }

        // The other speed classes finish every track too (M3 완료 조건 "트랙마다 완주", DECISIONS 2026-10-25 "M3 속도 단계"):
        // the bot runs the whole race without a fall, each lap close to the lap length at 20 m/s times the class's scale.
        // At Normal it touches no guard either; at VeryFast it brushes the guard coming out of the R22 corners (the turn
        // radius of 25 m stays while the speed goes up; tight corners are for the pole swing), which is allowed and logged
        // (user 2026-10-25). No decoration: it does not touch the run.
        [UnityTest, Timeout(180000)]
        public IEnumerator Race_OtherSpeedClasses_BotFinishesEveryTrack([Values(SpeedClass.Normal, SpeedClass.VeryFast)] SpeedClass speed,
            [Values("STADIUM", "SKYWAY LOOP", "FINGER LANES", "PALM COAST")] string title)
        {
            var store = TestRecords();
            TrackDefinition track = title == "STADIUM" ? Stadium() : title == "SKYWAY LOOP" ? Skyway() : title == "FINGER LANES" ? FingerLanes() : PalmCoast();
            store.Clear(track.name, speed);
            RaceLaunch.Request(new RaceSetup(track, title, "", RaceMode.Single, speed, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = store;
            assembler.Ghosts = ghosts;
            Scene raceScene = assembler.gameObject.scene;
            ChainRushGame game = Find<ChainRushGame>(raceScene);
            RunnerMotor player = Find<RunnerMotor>(raceScene);
            CircuitRace circuit = Find<CircuitRace>(raceScene);
            Assert.That(circuit.Definition, Is.SameAs(track));
            Assert.That(player.SpeedClassScale, Is.EqualTo(SpeedScale.Of(speed)));
            float assemblyDeadline = Time.realtimeSinceStartup + 20f;
            while (assembler.IsAssembling && Time.realtimeSinceStartup < assemblyDeadline) yield return null;
            Assert.That(assembler.IsAssembling, Is.False, "The race did not finish assembling.");
            float scale = Time.timeScale;
            try
            {
                game.SetInputSource(new CircuitBot(game, player, player.GetComponent<GrappleController>(), circuit));
                Time.timeScale = 6f;
                game.StartRun();
                float deadline = Time.realtimeSinceStartup + 150f;
                float maxOffset = 0f;
                double maxOffsetS = 0d;
                while (!game.HasFinished && !game.HasFailed && Time.realtimeSinceStartup < deadline)
                {
                    TrackCoord coord = game.Track.Project(player.transform.position);
                    if (Mathf.Abs(coord.D) > maxOffset)
                    {
                        maxOffset = Mathf.Abs(coord.D);
                        maxOffsetS = coord.S;
                    }
                    yield return null;
                }
                Time.timeScale = scale;
                LapCounter laps = circuit.Laps;
                string where = $"{title} at {speed}: tick {game.Tick}, {laps.CompletedLaps} laps, progress {laps.Progress:F1} m, " +
                               $"max offset {maxOffset:F2} m at S {maxOffsetS:F1} (segment {SegmentAt(track, maxOffsetS)})";
                Assert.That(game.HasFailed, Is.False, "The bot fell or died. " + where);
                Assert.That(game.HasFinished, Is.True, "The bot did not finish. " + where);
                double lapSeconds = track.LapLength / (20d * SpeedScale.Of(speed));
                for (int lap = 1; lap <= laps.CompletedLaps; lap++)
                {
                    float seconds = Ticks.ToSeconds(laps.LapTicks(lap));
                    Debug.Log($"ChainRush {title} at {speed} lap {lap}: {seconds:F2} s (lap length at the class's run speed: {lapSeconds:F2} s)");
                    Assert.That(seconds, Is.EqualTo(lapSeconds).Within(lapSeconds * 0.2d), $"lap {lap} time. " + where);
                }
                // The guard stands at the track's own road half width (the usual 6 m but Finger Lanes' 7.2 m).
                if (speed != SpeedClass.VeryFast)
                    Assert.That(maxOffset, Is.LessThan(track.RoadHalfWidth - 0.4f), "The bot touched a guard. " + where);
                Debug.Log($"ChainRush {title} at {speed}: total {Ticks.ToSeconds(laps.TotalTicks):F2} s, {game.Grapples} grapples, max offset {maxOffset:F2} m at S {maxOffsetS:F1}.");
            }
            finally
            {
                Time.timeScale = scale;
                store.Clear(track.name, speed);
            }
        }

        // ---- Finger Lanes (DECISIONS 2026-10-25 "FINGER LANES") ------------------------------------------------

        // Village Finger's outline: seven fingers joined by half turns of R25 (user 2026-10-25: tighter than the first R28),
        // right at the tips and left at the turn column, then two right corners of R30 round the left edge, where the clock
        // tower narrows the road to 8 m and an S bend of two R30 45° arcs, left then right, steps the edge 17.6 m west (user
        // 2026-10-28: the edge's 290 m straight split in two). The road is 1.2 times the usual; two bumps 0.5 m high (user
        // 2026-10-25: a bump, not a climb) whose tops are kickers throwing by speed, both lip 0.3 on the full road before a
        // turn: the stone bridge's, then the one past the clock tower (2026-10-28, "like the one before the corner"). No gaps,
        // no anchors, no extra poles; two laps of about 1488 m.
        [Test]
        public void FingerLanes_Layout_IsSevenHalfTurnFingersAWideRoadAndTwoKickers()
        {
            TrackDefinition finger = FingerLanes();
            Assert.That(finger.TryValidate(out string error), Is.True, error);
            Assert.That(finger.LapCount, Is.EqualTo(2));
            // The S bend trades 2 R sin 45° of the edge's straight for its two arcs, and lengthens the top row by 2 R (1 - cos 45°).
            double jogNorth = 60d * Math.Sin(Math.PI / 4d), jogWest = 60d * (1d - Math.Cos(Math.PI / 4d));
            Assert.That(finger.LapLength, Is.EqualTo(822d - jogNorth + jogWest + 7d * Math.PI * 25d + Math.PI * 30d + Math.PI * 15d).Within(0.01d));
            var turns = new System.Collections.Generic.List<TrackDefinition.Segment>();
            float total = 0f;
            foreach (TrackDefinition.Segment segment in finger.Segments)
            {
                if (!segment.IsArc) continue;
                turns.Add(segment);
                total += segment.Degrees;
            }
            Assert.That(turns.Count, Is.EqualTo(11));
            for (int i = 0; i < 7; i++)
            {
                Assert.That(turns[i].Radius, Is.EqualTo(25f), "finger turn " + i);
                Assert.That(turns[i].Degrees, Is.EqualTo(i % 2 == 0 ? 180f : -180f), "finger turn " + i + " alternates, right first");
            }
            foreach (int i in new[] { 7, 10 })
            {
                Assert.That(turns[i].Radius, Is.EqualTo(30f), "corner " + i);
                Assert.That(turns[i].Degrees, Is.EqualTo(90f), "corner " + i);
            }
            Assert.That(turns[8].Radius, Is.EqualTo(30f), "the S bend's first arc");
            Assert.That(turns[8].Degrees, Is.EqualTo(-45f), "the S bend turns left (west) first");
            Assert.That(turns[9].Radius, Is.EqualTo(30f), "the S bend's second arc");
            Assert.That(turns[9].Degrees, Is.EqualTo(45f), "and back north");
            Assert.That(total, Is.EqualTo(360f));
            float narrowest = float.MaxValue;
            for (double s = 0d; s < finger.LapLength; s += 1d) narrowest = Mathf.Min(narrowest, finger.HalfWidthAt(s));
            Assert.That(narrowest, Is.EqualTo(4f).Within(1e-4f), "The clock tower's entrance.");
            Assert.That(finger.RoadHalfWidth, Is.EqualTo(RoadProfile.DeckHalfWidth * 1.2f).Within(1e-4f));
            Assert.That(finger.HalfWidthAt(0d), Is.EqualTo(finger.RoadHalfWidth), "The start line has the track's road.");
            var launches = new System.Collections.Generic.List<float>();
            foreach (TrackDefinition.Segment segment in finger.Segments)
                if (segment.Launch > 0f) launches.Add(segment.Launch);
            Assert.That(launches, Is.EqualTo(new[] { 0.3f, 0.3f }), "The bridge, then the bump past the clock tower.");
            // Both bumps sit on the full road, out of the tower's narrow passage.
            double lip = 0d;
            foreach (TrackDefinition.Segment segment in finger.Segments)
            {
                lip += segment.Length;
                if (segment.Launch > 0f) Assert.That(finger.HalfWidthAt(lip - 0.5d), Is.EqualTo(finger.RoadHalfWidth).Within(1e-4f), $"The kicker at S {lip:F1} is on the full road.");
            }
            Centerline line = finger.BuildCenterline(Vector3.zero, 0f);
            Assert.That(line.LaunchCount, Is.EqualTo(2));
            float highest = 0f;
            for (double s = 0d; s < finger.LapLength; s += 0.5d) highest = Mathf.Max(highest, line.FrameAt(s).Position.y);
            Assert.That(highest, Is.EqualTo(0.5f).Within(0.02f), "The bumps' tops, where they kick.");
            Assert.That(finger.Gaps(), Is.Empty);
            Assert.That(finger.Anchors, Is.Empty);
            Assert.That(finger.Poles, Is.Empty);
            // The nails of the two long fingers (user 2026-10-25): R15 quarter turns, 20 m across, a road half as wide.
            Assert.That(finger.Shortcuts.Count, Is.EqualTo(2));
            foreach (TrackDefinition.Shortcut shortcut in finger.Shortcuts)
            {
                Assert.That(shortcut.Radius, Is.EqualTo(15f));
                Assert.That(shortcut.Straight, Is.EqualTo(20f));
                Assert.That(shortcut.HalfWidth, Is.EqualTo(finger.RoadHalfWidth * 0.5f).Within(1e-4f));
                for (int i = 0; i < finger.CheckpointCount; i++)
                    Assert.That(finger.CheckpointS(i), Is.Not.InRange((double)shortcut.FromS, (double)shortcut.ToS), "No checkpoint where a shortcut skips.");
            }
            Assert.That(line.BranchCount, Is.EqualTo(2));
        }

        // A runner that swings round the nails' corner poles takes both shortcuts on each lap (user 2026-10-25): the laps
        // count across them and the race finishes. The bot hooks at the junction, lets go a quarter turn on, and again at
        // the straight's end (ShortcutBot); the R15 turns are tighter than the runner's own 25 m, so only a swing makes them.
        [UnityTest, Timeout(120000)]
        public IEnumerator Race_FingerLanesShortcuts_ASwingingRunnerTakesBothEachLap()
        {
            var store = TestRecords();
            TrackDefinition finger = FingerLanes();
            store.Clear(finger.name, SpeedClass.Fast);
            RaceLaunch.Request(new RaceSetup(finger, "FINGER LANES", "", RaceMode.Single, SpeedClass.Fast, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = store;
            assembler.Ghosts = ghosts;
            Scene raceScene = assembler.gameObject.scene;
            ChainRushGame game = Find<ChainRushGame>(raceScene);
            RunnerMotor player = Find<RunnerMotor>(raceScene);
            CircuitRace circuit = Find<CircuitRace>(raceScene);
            float assemblyDeadline = Time.realtimeSinceStartup + 20f;
            while (assembler.IsAssembling && Time.realtimeSinceStartup < assemblyDeadline) yield return null;
            Assert.That(assembler.IsAssembling, Is.False, "The race did not finish assembling.");
            Assert.That(game.Track.BranchCount, Is.EqualTo(2));
            float scale = Time.timeScale;
            var bot = new ShortcutBot(game, player, finger);
            try
            {
                game.SetInputSource(bot);
                Time.timeScale = 6f;
                game.StartRun();
                float deadline = Time.realtimeSinceStartup + 90f;
                while (!game.HasFinished && !game.HasFailed && Time.realtimeSinceStartup < deadline) yield return null;
                Time.timeScale = scale;
                LapCounter laps = circuit.Laps;
                string where = $"tick {game.Tick}, {laps.CompletedLaps} laps, progress {laps.Progress:F1} m, shortcuts taken {bot.Taken}, hooks missed {bot.Missed}";
                Assert.That(game.HasFailed, Is.False, "The runner fell or died. " + where);
                Assert.That(game.HasFinished, Is.True, "The runner did not finish two laps. " + where);
                Assert.That(bot.Missed, Is.EqualTo(0), "A hook found no pole. " + where);
                Assert.That(bot.Taken, Is.EqualTo(4), "Both shortcuts on both laps. " + where);
                for (int lap = 1; lap <= 2; lap++)
                    Debug.Log($"ChainRush Finger Lanes with shortcuts lap {lap}: {Ticks.ToSeconds(laps.LapTicks(lap)):F2} s");
            }
            finally
            {
                Time.timeScale = scale;
                store.Clear(finger.name, SpeedClass.Fast);
            }
        }

        // Holding the key toward the pole through each shortcut swing (user 2026-10-27, "steering in a shortcut swing speeds
        // the runner up far too much"; 2026-10-30 "꺾이는 정도에 따라 가속 수준이 차이나는 것 같다"): the rope adds no speed
        // at all now; only the throttle raises the swing, to 1.2 times the run speed, no faster than running (no booster
        // here). Before 2026-10-27 a quarter turn went from 19.9 to 30.3 m/s in 0.54 s, before 2026-10-30 to 27.4 m/s.
        [UnityTest, Timeout(120000)]
        public IEnumerator Race_FingerLanesShortcuts_InnerKeyHeld_TheRopeAddsNoSpeed()
        {
            var store = TestRecords();
            TrackDefinition finger = FingerLanes();
            store.Clear(finger.name, SpeedClass.Fast);
            RaceLaunch.Request(new RaceSetup(finger, "FINGER LANES", "", RaceMode.Single, SpeedClass.Fast, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = store;
            assembler.Ghosts = ghosts;
            Scene raceScene = assembler.gameObject.scene;
            ChainRushGame game = Find<ChainRushGame>(raceScene);
            RunnerMotor player = Find<RunnerMotor>(raceScene);
            CircuitRace circuit = Find<CircuitRace>(raceScene);
            float assemblyDeadline = Time.realtimeSinceStartup + 20f;
            while (assembler.IsAssembling && Time.realtimeSinceStartup < assemblyDeadline) yield return null;
            Assert.That(assembler.IsAssembling, Is.False, "The race did not finish assembling.");
            float scale = Time.timeScale;
            var bot = new ShortcutBot(game, player, finger) { HoldInnerKey = true };
            float limit = player.RunAcceleration * Ticks.Seconds;
            float last = 0f, steepest = float.NegativeInfinity, top = 0f;
            int steepestTick = -1, swingTicks = 0;
            bool wasSwinging = false, shortened = false;
            Action<int> sample = tick =>
            {
                float now = player.HorizontalSpeed;
                if (game.Racer.SwingTicks > 0)
                {
                    swingTicks++;
                    top = Mathf.Max(top, now);
                    if (game.Racer.SwingRadius < game.Racer.SwingHookRadius - 1f) shortened = true;
                    if (wasSwinging && now - last > steepest) { steepest = now - last; steepestTick = tick; }
                }
                wasSwinging = game.Racer.SwingTicks > 0;
                last = now;
            };
            try
            {
                game.SetInputSource(bot);
                game.TickCompleted += sample;
                Time.timeScale = 6f;
                game.StartRun();
                float deadline = Time.realtimeSinceStartup + 60f;
                while (circuit.Laps.CompletedLaps < 1 && !game.HasFailed && Time.realtimeSinceStartup < deadline) yield return null;
                Time.timeScale = scale;
                string where = $"tick {game.Tick}, {circuit.Laps.CompletedLaps} laps, shortcuts taken {bot.Taken}, hooks missed {bot.Missed}";
                Debug.Log($"ChainRush Finger Lanes inner-key swings: {swingTicks} swinging ticks, top {top:F2} m/s, steepest rise {steepest:F4} m/s a tick at tick {steepestTick} (limit {limit:F4}). {where}");
                Assert.That(game.HasFailed, Is.False, "The runner fell. " + where);
                Assert.That(circuit.Laps.CompletedLaps, Is.GreaterThanOrEqualTo(1), "The runner did not finish a lap. " + where);
                Assert.That(bot.Taken, Is.EqualTo(2), "Both shortcuts, the inner key held. " + where);
                Assert.That(swingTicks, Is.GreaterThan(0), "No swing was sampled. " + where);
                Assert.That(steepestTick, Is.GreaterThanOrEqualTo(0), "No two swinging ticks in a row were sampled. " + where);
                // The inner key really worked the rope, and the swing went no faster than the throttle's 1.2 times.
                Assert.That(shortened, Is.True, "The inner key never shortened the rope by a metre. " + where);
                Assert.That(top, Is.LessThanOrEqualTo(player.RunSpeed * 1.2f + 0.05f), "The shortened rope added speed. " + where);
                Assert.That(steepest, Is.LessThanOrEqualTo(limit + 1e-3f), $"The swing sped up faster than running at tick {steepestTick}. " + where);
            }
            finally
            {
                game.TickCompleted -= sample;
                Time.timeScale = scale;
                store.Clear(finger.name, SpeedClass.Fast);
            }
        }

        // The real lobby setup loads the village exterior, then the bot runs both laps at Fast: no fall, no guard
        // touched through the seven half turns, each lap close to the lap length at 20 m/s, and off the road for a moment
        // past each kicker's crest (user 2026-10-25): the bot only steers, so the kickers alone lift it.
        [UnityTest, Timeout(120000)]
        public IEnumerator Race_FingerLanesFromTheLobby_BotFinishesTwoLaps()
        {
            var store = TestRecords();
            TrackDefinition finger = FingerLanes();
            store.Clear(finger.name, SpeedClass.Fast);
            LobbyMenu menu = null;
            yield return Load(LobbyPath, loaded => menu = Find<LobbyMenu>(loaded));
            menu.Confirm();
            menu.Confirm();
            menu.Highlight(2);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Runner), "Finger Lanes can be raced.");
            RaceSetup setup = menu.BuildSetup();
            Assert.That(setup.Track, Is.SameAs(finger));
            Assert.That(setup.Title, Is.EqualTo("FINGER LANES"));
            Assert.That(setup.EnvironmentScene, Is.EqualTo(FingerLanesEnvironmentScene));
            RaceLaunch.Request(setup);
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = store;
            assembler.Ghosts = ghosts;
            Scene raceScene = assembler.gameObject.scene;
            Assert.That(Find<ChainRushHud>(raceScene).Heading, Is.EqualTo("FINGER LANES"));
            ChainRushGame game = Find<ChainRushGame>(raceScene);
            RunnerMotor player = Find<RunnerMotor>(raceScene);
            CircuitRace circuit = Find<CircuitRace>(raceScene);
            Assert.That(circuit.Definition, Is.SameAs(finger));
            float assemblyDeadline = Time.realtimeSinceStartup + 20f;
            while (assembler.IsAssembling && Time.realtimeSinceStartup < assemblyDeadline) yield return null;
            Assert.That(assembler.IsAssembling, Is.False, "The race did not finish assembling.");
            Scene environment = SceneManager.GetSceneByName(FingerLanesEnvironmentScene);
            Assert.That(environment.isLoaded, Is.True, "The village exterior must load through the saved lobby setup.");
            Assert.That(environment.rootCount, Is.EqualTo(1), "Only the exterior prefab belongs in the decoration scene.");
            GameObject exterior = environment.GetRootGameObjects()[0];
            Assert.That(exterior.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
            Assert.That(exterior.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(exterior.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(exterior.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
            // Each kicker's crest S, and the most the runner rose above its height on the flat start line within 25 m past it.
            var crests = new System.Collections.Generic.List<double>();
            double crestS = 0d;
            foreach (TrackDefinition.Segment segment in finger.Segments)
            {
                crestS += segment.Length;
                if (segment.Launch > 0f) crests.Add(crestS);
            }
            var lift = new float[crests.Count];
            float scale = Time.timeScale;
            try
            {
                game.SetInputSource(new CircuitBot(game, player, player.GetComponent<GrappleController>(), circuit));
                Time.timeScale = 6f;
                game.StartRun();
                float standing = game.Track.Project(player.transform.position).H;
                float deadline = Time.realtimeSinceStartup + 90f;
                float maxOffset = 0f;
                double maxOffsetS = 0d;
                while (!game.HasFinished && !game.HasFailed && Time.realtimeSinceStartup < deadline)
                {
                    TrackCoord coord = game.Track.Project(player.transform.position);
                    if (Mathf.Abs(coord.D) > maxOffset)
                    {
                        maxOffset = Mathf.Abs(coord.D);
                        maxOffsetS = coord.S;
                    }
                    for (int k = 0; k < crests.Count; k++)
                        if (coord.S > crests[k] && coord.S < crests[k] + 25d) lift[k] = Mathf.Max(lift[k], coord.H - standing);
                    yield return null;
                }
                Time.timeScale = scale;
                LapCounter laps = circuit.Laps;
                string where = $"tick {game.Tick}, {laps.CompletedLaps} laps, progress {laps.Progress:F1} m, " +
                               $"max offset {maxOffset:F2} m at S {maxOffsetS:F1} (segment {SegmentAt(finger, maxOffsetS)})";
                for (int k = 0; k < crests.Count; k++)
                {
                    Debug.Log($"ChainRush Finger Lanes kicker {k} at S {crests[k]:F1}: rose {lift[k]:F2} m above the road");
                    Assert.That(lift[k], Is.GreaterThan(0.5f), $"The bot was not thrown up past the kicker at S {crests[k]:F1}. " + where);
                }
                // The bump past the clock tower throws like the bridge's (user 2026-10-28).
                Assert.That(lift[1], Is.GreaterThanOrEqualTo(lift[0] * 0.9f), "The clock tower's bump throws much less than the bridge's. " + where);
                Assert.That(game.HasFailed, Is.False, "The bot fell or died. " + where);
                Assert.That(game.HasFinished, Is.True, "The bot did not finish two laps. " + where);
                Assert.That(laps.CompletedLaps, Is.EqualTo(2));
                double lapSeconds = finger.LapLength / 20d;
                for (int lap = 1; lap <= 2; lap++)
                {
                    float seconds = Ticks.ToSeconds(laps.LapTicks(lap));
                    Debug.Log($"ChainRush Finger Lanes lap {lap}: {seconds:F2} s");
                    Assert.That(seconds, Is.EqualTo(lapSeconds).Within(lapSeconds * 0.2d), $"lap {lap} time. " + where);
                }
                // The guard stands at the track's own road half width (1.2 times the usual here).
                Assert.That(maxOffset, Is.LessThan(finger.RoadHalfWidth - 0.4f), "The bot touched a guard. " + where);
                Debug.Log($"ChainRush Finger Lanes race: {finger.LapLength:F1} m a lap, total {Ticks.ToSeconds(laps.TotalTicks):F2} s, max offset {maxOffset:F2} m at S {maxOffsetS:F1}.");
                assembler.ReturnToLobby();
                yield return null;
                yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(RaceLaunch.LobbyScene));
                Assert.That(SceneManager.GetSceneByName(FingerLanesEnvironmentScene).isLoaded, Is.False, "The village exterior must leave with the race.");
            }
            finally
            {
                Time.timeScale = scale;
                store.Clear(finger.name, SpeedClass.Fast);
            }
        }

        // ---- Palm Coast (user 2026-10-30, Beach Drive outline, five boost pads and a jump over the sea) --------

        // The track as reshaped after play (2026-10-30): sixteen arcs, so sixteen corner poles, each at a real curve; a
        // steady 12% climb with three pads 30 m apart and the crest 10 m after the last (A, 2026-10-30: the pad's speed now
        // wears off fast, so the last pad sits just before the kicker); the sea 15 m past the crest, 45 m
        // wide; the climb given back over the beach.
        [Test]
        public void PalmCoast_Layout_IsTheCoastalLoopWithAClimbToAJumpOverTheSeaAndAGentleFinishHill()
        {
            TrackDefinition palm = PalmCoast();
            Assert.That(palm.TryValidate(out string error), Is.True, error);
            Assert.That(palm.LapCount, Is.EqualTo(2));
            Assert.That(palm.LapLength, Is.InRange(1995d, 2012d));
            Assert.That(palm.RoadHalfWidth, Is.EqualTo(7.2f).Within(1e-4f));
            Assert.That(palm.Gaps().Count, Is.EqualTo(1), "The sea under the jump.");
            Assert.That(palm.Anchors, Is.Empty);
            Assert.That(palm.Poles, Is.Empty);
            Assert.That(palm.Shortcuts, Is.Empty);
            var turns = new System.Collections.Generic.List<TrackDefinition.Segment>();
            float total = 0f;
            int kickers = 0;
            double s = 0d, lipS = 0d;
            foreach (TrackDefinition.Segment segment in palm.Segments)
            {
                s += segment.Length;
                if (segment.Launch > 0f)
                {
                    kickers++;
                    lipS = s;
                    Assert.That(segment.Launch, Is.EqualTo(0.3f).Within(1e-6f));
                }
                Assert.That(Mathf.Abs(segment.EndGrade), Is.LessThanOrEqualTo(TrackDefinition.MaxGrade));
                if (!segment.IsArc) continue;
                turns.Add(segment);
                total += segment.Degrees;
            }
            Assert.That(kickers, Is.EqualTo(1), "Only the jump's kicker throws.");
            Assert.That(turns.Count, Is.EqualTo(16), "Fourteen corners and the beach's two long bends: a pole at each.");
            Assert.That(total, Is.EqualTo(-360f).Within(0.001f), "Right is positive: this loop turns counterclockwise.");
            for (int i = 0; i < turns.Count; i++)
                Assert.That(turns[i].Radius, Is.GreaterThanOrEqualTo(TrackDefinition.MinRadius), "corner " + i);
            Assert.That(turns[0].Radius, Is.EqualTo(45f), "P1, met at Z1's speed");
            Assert.That(turns[1].Radius, Is.EqualTo(60f), "P2, met after the landing");
            Assert.That(turns[3].Radius, Is.EqualTo(220f), "the beach's first long bend");
            Assert.That(turns[3].Degrees, Is.EqualTo(-23.11f).Within(0.01f));
            Assert.That(turns[4].Radius, Is.EqualTo(220f), "and the second, back to west");
            Assert.That(turns[4].Degrees, Is.EqualTo(23.11f).Within(0.01f));
            Assert.That(turns[8].Degrees, Is.EqualTo(-96.3402f).Within(0.001f), "P7 from its two diagonal edges.");
            Assert.That(turns[9].Degrees, Is.EqualTo(-38.6598f).Within(0.001f), "P8 from the diagonal onto the southbound edge.");
            Assert.That(turns[15].Radius, Is.EqualTo(50f), "P14, met at Z4's carried speed");
            Centerline line = palm.BuildCenterline(Vector3.zero, 0f);
            Assert.That(line.IsLoop, Is.True);
            Assert.That(line.BoostZoneCount, Is.EqualTo(5), "One on the start straight, three on the climb, one before the finish.");
            Assert.That(line.LaunchCount, Is.EqualTo(1));
            var zones = new System.Collections.Generic.List<double>();
            line.GetBoostZones(zones);
            double[] expectedS = { 110d, 273.6858d, 303.6858d, 333.6858d, 1822.7102d };
            Vector2[] expectedPositions = { new Vector2(0f, 110f), new Vector2(83f, 210f), new Vector2(113f, 210f), new Vector2(143f, 210f), new Vector2(-132f, -68f) };
            (double start, double end) = palm.Gaps()[0];
            Assert.That(zones[2] - zones[1], Is.EqualTo(30d).Within(1e-3d), "The pads of the climb are 30 m apart.");
            Assert.That(zones[3] - zones[2], Is.EqualTo(30d).Within(1e-3d));
            Assert.That(lipS - zones[3], Is.EqualTo(10d).Within(1e-3d), "The last pad to the crest.");
            Assert.That(start - lipS, Is.EqualTo(15d).Within(1e-3d), "The crest to the sea: the flat run.");
            Assert.That(end - lipS, Is.EqualTo(60d).Within(1e-3d), "The crest to the far side of the sea.");
            for (int i = 0; i < zones.Count; i++)
            {
                Assert.That(zones[i], Is.EqualTo(expectedS[i]).Within(0.03d), "zone " + i);
                TrackFrame frame = line.FrameAt(zones[i]);
                Assert.That(frame.Curvature, Is.Zero, "Every zone is on a straight.");
                Assert.That(Vector2.Distance(new Vector2(frame.Position.x, frame.Position.z), expectedPositions[i]), Is.LessThan(0.03f));
            }
            for (int i = 1; i <= 3; i++)
                Assert.That(line.FrameAt(zones[i]).Grade, Is.EqualTo(0.12f).Within(1e-5f), "Every pad of the climb sits on the full 12%.");
            float low = 0f, high = 0f;
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            for (double at = 0d; at < line.EndS; at += 0.5d)
            {
                Vector3 position = line.FrameAt(at).Position;
                low = Mathf.Min(low, position.y);
                high = Mathf.Max(high, position.y);
                min = Vector2.Min(min, new Vector2(position.x, position.z));
                max = Vector2.Max(max, new Vector2(position.x, position.z));
            }
            Assert.That(Vector2.Distance(min, new Vector2(-360f, -68f)), Is.LessThan(0.1f));
            Assert.That(Vector2.Distance(max, new Vector2(325f, 330f)), Is.LessThan(0.1f));
            Assert.That(low, Is.EqualTo(-2f).Within(0.01f), "The start's descent and the beach's end meet at -2 m.");
            Assert.That(high - low, Is.EqualTo(11.88f).Within(0.02f), "The climb: 0.12 over 10/2 + 90 + 8/2 m.");
            Assert.That(line.FrameAt(lipS).Position.y, Is.EqualTo(high).Within(0.01f), "The crest is the top, flat from there.");
            Assert.That(line.FrameAt(end + 1d).Position.y, Is.EqualTo(high).Within(0.01f), "The landing is as high as the crest.");
            Assert.That(line.FrameAt(100d).Position.y, Is.EqualTo(-2f).Within(0.01f), "Descent just after the start.");
            Assert.That(line.FrameAt(zones[4]).Grade, Is.EqualTo(0.03f).Within(1e-5f), "The finish approach climbs gently.");
            Assert.That(line.FrameAt(line.EndS - 0.001d).Position.magnitude, Is.LessThan(0.01f), "The position and height meet the start.");
            Assert.That(palm.CheckpointCount, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
                Assert.That(palm.CheckpointS(i) / palm.LapLength, Is.EqualTo(new[] { 0.15d, 0.5d, 0.75d }[i]).Within(1e-6d));
        }

        // A track's own look (user 2026-10-30, Palm Coast's road and walls): the race lays the road and the guard walls with
        // the look's materials instead of the race scene's, and none with the scene's road material is left; with markings
        // it paints the lane markings too (CircuitScenery.PaintMarkings).
        [UnityTest]
        public IEnumerator Race_WithATrackLook_LaysTheRoadAndWallsWithItsMaterials()
        {
#if UNITY_EDITOR
            var store = TestRecords();
            TrackDefinition palm = PalmCoast();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var road = new Material(shader) { name = "Test look road" };
            var rail = new Material(shader) { name = "Test look rail" };
            var markings = new Material(shader) { name = "Test look markings" };
            TrackLook look = ScriptableObject.CreateInstance<TrackLook>();
            var data = new SerializedObject(look);
            data.FindProperty("road").objectReferenceValue = road;
            data.FindProperty("rail").objectReferenceValue = rail;
            data.FindProperty("markings").objectReferenceValue = markings;
            data.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                RaceLaunch.Request(new RaceSetup(palm, "PALM COAST", "", RaceMode.Single, SpeedClass.Fast, runner, look));
                RaceAssembler assembler = null;
                yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
                assembler.Records = store;
                assembler.Ghosts = ghosts;
                CircuitRace circuit = Find<CircuitRace>(assembler.gameObject.scene);
                Assert.That(circuit.PieceCount, Is.GreaterThan(0), "The road was laid.");
                int roads = 0, rails = 0, painted = 0;
                foreach (Renderer renderer in circuit.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.sharedMaterial == road) roads++;
                    if (renderer.sharedMaterial == rail) rails++;
                    if (renderer.sharedMaterial == markings) painted++;
                    Assert.That(renderer.sharedMaterial.name, Is.Not.EqualTo("M_Deck"), $"{renderer.name} kept the scene's road material.");
                }
                Assert.That(roads, Is.GreaterThan(0), "The road pieces use the look's road.");
                Assert.That(rails, Is.GreaterThan(0), "The guard walls use the look's rail.");
                // One mesh per 50 m stretch: 2001 m a lap is 41 stretches, the one over the sea and its neighbours aside.
                Assert.That(painted, Is.GreaterThan(30), "The main road's stretches have their markings.");
            }
            finally
            {
                store.Clear(palm.name, SpeedClass.Fast);
                UnityEngine.Object.Destroy(look);
                UnityEngine.Object.Destroy(road);
                UnityEngine.Object.Destroy(rail);
                UnityEngine.Object.Destroy(markings);
            }
#else
            Assert.Fail("This test requires the Unity Editor.");
            yield break;
#endif
        }

        // Requires Palm Coast as the lobby's fourth unlocked track. Its environment choice, if any, is the scene's.
        [UnityTest, Timeout(120000)]
        public IEnumerator Race_PalmCoastFromTheLobby_BotFinishesTwoLaps()
        {
            var store = TestRecords();
            TrackDefinition palm = PalmCoast();
            store.Clear(palm.name, SpeedClass.Fast);
            LobbyMenu menu = null;
            yield return Load(LobbyPath, loaded => menu = Find<LobbyMenu>(loaded));
            menu.Confirm();
            menu.Confirm();
            menu.Highlight(3);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Runner), "PALM COAST must be registered and unlocked in the lobby.");
            RaceSetup setup = menu.BuildSetup();
            Assert.That(setup.Track, Is.SameAs(palm));
            Assert.That(setup.Title, Is.EqualTo("PALM COAST"));
            RaceLaunch.Request(setup);
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = store;
            assembler.Ghosts = ghosts;
            Scene raceScene = assembler.gameObject.scene;
            ChainRushGame game = Find<ChainRushGame>(raceScene);
            RunnerMotor player = Find<RunnerMotor>(raceScene);
            CircuitRace circuit = Find<CircuitRace>(raceScene);
            Assert.That(Find<ChainRushHud>(raceScene).Heading, Is.EqualTo("PALM COAST"));
            Assert.That(circuit.Definition, Is.SameAs(palm));
            Assert.That(game.Track.BoostZoneCount, Is.EqualTo(5));
            float assemblyDeadline = Time.realtimeSinceStartup + 20f;
            while (assembler.IsAssembling && Time.realtimeSinceStartup < assemblyDeadline) yield return null;
            Assert.That(assembler.IsAssembling, Is.False, "The race did not finish assembling.");
            float scale = Time.timeScale;
            bool respawned = false;
            Action<int> onTick = tick => respawned |= player.IsRespawning;
            game.TickCompleted += onTick;
            try
            {
                game.SetInputSource(new CircuitBot(game, player, player.GetComponent<GrappleController>(), circuit));
                Time.timeScale = 6f;
                game.StartRun();
                float deadline = Time.realtimeSinceStartup + 90f;
                float maxOffset = 0f;
                double maxOffsetS = 0d;
                while (!game.HasFinished && !game.HasFailed && Time.realtimeSinceStartup < deadline)
                {
                    TrackCoord coord = game.Track.Project(player.transform.position);
                    if (Mathf.Abs(coord.D) > maxOffset)
                    {
                        maxOffset = Mathf.Abs(coord.D);
                        maxOffsetS = coord.S;
                    }
                    yield return null;
                }
                Time.timeScale = scale;
                LapCounter laps = circuit.Laps;
                string where = $"tick {game.Tick}, {laps.CompletedLaps} laps, progress {laps.Progress:F1} m, max offset {maxOffset:F2} m at S {maxOffsetS:F1} (segment {SegmentAt(palm, maxOffsetS)})";
                Assert.That(game.HasFailed, Is.False, where);
                Assert.That(respawned, Is.False, "The bot fell and respawned. " + where);
                Assert.That(game.HasFinished, Is.True, "The bot did not finish. " + where);
                Assert.That(laps.CompletedLaps, Is.EqualTo(2));
                double lapSeconds = palm.LapLength / 20d;
                for (int lap = 1; lap <= 2; lap++)
                    Assert.That(Ticks.ToSeconds(laps.LapTicks(lap)), Is.EqualTo(lapSeconds).Within(lapSeconds * 0.2d), $"lap {lap} time. " + where);
                Assert.That(maxOffset, Is.LessThan(palm.RoadHalfWidth - 0.4f), "The bot touched a guard. " + where);
                Debug.Log($"ChainRush Palm Coast race: {palm.LapLength:F1} m a lap, total {Ticks.ToSeconds(laps.TotalTicks):F2} s. {where}");
                assembler.ReturnToLobby();
                yield return null;
                yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(RaceLaunch.LobbyScene));
            }
            finally
            {
                game.TickCompleted -= onTick;
                Time.timeScale = scale;
                store.Clear(palm.name, SpeedClass.Fast);
            }
        }

        // Read every tick directly: no render frame may skip the crossing's immediate speed or the booster's cut.
        [UnityTest]
        public IEnumerator BoostZone_PalmCoastZ1_RaisesSpeedAtOnceAndCutsARunningBooster([Values(false, true)] bool booster,
            [Values(SpeedClass.Normal, SpeedClass.Fast, SpeedClass.VeryFast)] SpeedClass speed)
        {
            var store = TestRecords();
            TrackDefinition palm = PalmCoast();
            RaceLaunch.Request(new RaceSetup(palm, "PALM COAST", "", RaceMode.Single, speed, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = store;
            assembler.Ghosts = ghosts;
            Scene scene = assembler.gameObject.scene;
            ChainRushGame game = Find<ChainRushGame>(scene);
            RunnerMotor player = Find<RunnerMotor>(scene);
            CircuitRace circuit = Find<CircuitRace>(scene);
            var zones = new System.Collections.Generic.List<double>();
            game.Track.GetBoostZones(zones);
            Assert.That(zones.Count, Is.EqualTo(5));
            Assert.That(zones[0], Is.EqualTo(110d).Within(0.01d));
            Assert.That(player.BoostZoneSpeed, Is.EqualTo(55f), "The same pad speed in every class (user 2026-10-30).");
            Assert.That(player.BoostZoneSpeed, Is.GreaterThan(player.SlingTopSpeed));
            float scale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                game.StartRun();
                var bot = new CircuitBot(game, player, player.GetComponent<GrappleController>(), circuit);
                var log = new InputLog();
                var replay = new InputReplay(log);
                bool fired = false, crossed = false;
                for (int tick = 0; tick < 1000 && game.IsRunning; tick++)
                {
                    TrackFrame before = game.Track.Frame(player.transform.position);
                    Vector3 position = player.transform.position;
                    bool fire = booster && !fired && before.S >= zones[0] - 3d;
                    if (fire)
                    {
                        game.Racer.AddGauge(1f);
                        fired = true;
                    }
                    int slingBefore = game.Racer.SlingTicks;
                    bool grounded = player.IsGrounded;
                    TickInput input = bot.Consume();
                    log.Add(new TickInput(input.Steer, input.PrimaryPressed, input.ReleasePressed, input.AttackPressed, chainActionPressed: fire));
                    SimSnapshot snapshot = game.CaptureSnapshot();
                    game.StepTick(replay);
                    if (fire) Assert.That(game.Racer.SlingTicks, Is.GreaterThan(0), "The booster must really fire before the pad.");
                    double toS = before.S + Vector3.Dot(player.transform.position - position, before.Forward);
                    if (!(before.S < zones[0] && toS >= zones[0])) continue;
                    crossed = true;
                    Assert.That(grounded, Is.True, "The bot crosses on the ground.");
                    if (booster) Assert.That(slingBefore, Is.GreaterThan(0), "The booster must still be running when Z1 is crossed.");
                    Assert.That(player.ForwardSpeed, Is.GreaterThanOrEqualTo(player.BoostZoneSpeed), "Immediate state after the crossing tick.");
                    Assert.That(player.ForwardSpeed, Is.EqualTo(player.BoostZoneSpeed).Within(1e-4f), "A slower runner takes exactly the pad's speed.");
                    Assert.That(game.Racer.SlingTicks, Is.Zero, "The pad cuts the booster.");
                    // Restoring just before the pad must offer the same boost again, with no pad state to restore. The
                    // race's ghost recorder refuses a tick out of order and drops this run's ghost (RaceAssembler.RecordTick);
                    // the rewind here checks the simulation only.
                    SimSnapshot after = game.CaptureSnapshot();
                    LogAssert.Expect(LogType.Error, new Regex("came after \\d+; this run will keep no ghost"));
                    game.RestoreSnapshot(snapshot);
                    var crossing = new InputLog();
                    crossing.Add(log[log.Count - 1]);
                    game.StepTick(new InputReplay(crossing));
                    Assert.That(player.transform.position.Equals(after.Position), Is.True, "The restored crossing repeats its position exactly.");
                    Assert.That(game.Racer.Capture(), Is.EqualTo(after.Racer), "The restored crossing repeats every racer field exactly.");
                    Assert.That(circuit.Laps.Progress, Is.EqualTo(after.Circuit.Laps.Progress));
                    float boosted = player.ForwardSpeed;
                    game.StepTick(bot);
                    Assert.That(player.ForwardSpeed, Is.LessThan(boosted).And.GreaterThan(player.RunSpeed), "The pad's speed starts to wear off on the next tick.");
                    Assert.That(game.Racer.SlingTicks, Is.Zero);
                    Assert.That(game.Racer.BoostZoneCarrySpeed, Is.GreaterThan(player.RunSpeed), "The pad's speed is its carry.");
                    Assert.That(game.Racer.SwingCarrySpeed, Is.Zero, "A pad is not a swing's carry.");
                    // The part above running speed wears off in proportion to itself (user 2026-10-30, "순간에 폭발적인
                    // 부스트만"): the first 0.4 s takes off more than the next 0.4 s, and 0.8 s leaves well under the old
                    // falloffs (52 m/s at 3 m/s a second, 49 at 8).
                    int half = Ticks.FromSeconds(0.4f);
                    for (int hold = half; hold > 0; hold--) game.StepTick(bot);
                    float atHalf = player.ForwardSpeed;
                    for (int hold = half; hold > 0; hold--) game.StepTick(bot);
                    float atFull = player.ForwardSpeed;
                    Debug.Log($"ChainRush pad falloff at {speed}: {boosted:F2} -> {atHalf:F2} (0.4 s) -> {atFull:F2} (0.8 s), run {player.RunSpeed:F2}.");
                    Assert.That(boosted - atHalf, Is.GreaterThan(atHalf - atFull), "Harder at first, easing as it nears running speed.");
                    Assert.That(atFull, Is.LessThan(45f), "After 0.8 s most of the pad's speed is gone.");
                    Assert.That(atFull, Is.GreaterThan(player.RunSpeed), "but not all of it.");
                    break;
                }
                Assert.That(crossed, Is.True, "The bot did not reach Z1.");
                Assert.That(fired, Is.EqualTo(booster));
            }
            finally
            {
                Time.timeScale = scale;
                store.Clear(palm.name, speed);
            }
        }

        // The jump over the sea (user 2026-10-30): in every class the bot takes the climb's three pads, leaves the crest near the pad's
        // speed and lands past the sea without a fall. Then a fall into the sea (the runner put down in it, having last stood
        // just before it) comes back before the last pad of the climb, and from rest that pad carries the runner over.
        [UnityTest, Timeout(120000)]
        public IEnumerator Jump_PalmCoast_ThePadsCarryOverTheSea_AndAFallComesBackBeforeTheLastPad(
            [Values(SpeedClass.Normal, SpeedClass.Fast, SpeedClass.VeryFast)] SpeedClass speed)
        {
            var store = TestRecords();
            TrackDefinition palm = PalmCoast();
            RaceLaunch.Request(new RaceSetup(palm, "PALM COAST", "", RaceMode.Single, speed, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = store;
            assembler.Ghosts = ghosts;
            Scene scene = assembler.gameObject.scene;
            ChainRushGame game = Find<ChainRushGame>(scene);
            RunnerMotor player = Find<RunnerMotor>(scene);
            CircuitRace circuit = Find<CircuitRace>(scene);
            var zones = new System.Collections.Generic.List<double>();
            game.Track.GetBoostZones(zones);
            (double seaStart, double seaEnd) = palm.Gaps()[0];
            double lipS = 0d;
            foreach (TrackDefinition.Segment segment in palm.Segments)
            {
                lipS += segment.Length;
                if (segment.Launch > 0f) break;
            }
            float scale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                game.StartRun();
                var bot = new CircuitBot(game, player, player.GetComponent<GrappleController>(), circuit);
                float lipSpeed = 0f;
                int tick = 0;
                for (; tick < 2000 && game.IsRunning && circuit.Laps.Progress < seaEnd + 20d; tick++)
                {
                    double before = game.Track.Project(player.transform.position).S;
                    game.StepTick(bot);
                    Assert.That(player.IsRespawning, Is.False, $"The bot fell at tick {tick}, S {before:F1}.");
                    double s = game.Track.Project(player.transform.position).S;
                    if (before < lipS && s >= lipS) lipSpeed = player.ForwardSpeed;
                }
                Assert.That(circuit.Laps.Progress, Is.GreaterThanOrEqualTo(seaEnd + 20d), $"The bot did not get past the sea in {tick} ticks.");
                // The sea's far side is 60 m from the crest: a flight of 2 x 0.3 x v^2 / 22 needs at least 46.9 m/s.
                Assert.That(lipSpeed, Is.GreaterThan(46.9f), "The last pad leaves the crest fast enough to fly the sea.");
                Debug.Log($"ChainRush Palm Coast jump at {speed}: {lipSpeed:F2} m/s at the crest, past the sea by tick {tick}.");

                // A fall into the sea, having last stood on the flat run just before it.
                SimSnapshot now = game.CaptureSnapshot();
                RacerState.Snapshot racer = now.Racer;
                racer.LastGroundedS = seaStart - 1d;
                racer.Grounded = false;
                racer.Velocity = Vector3.zero;
                TrackFrame sea = game.Track.FrameAt((seaStart + seaEnd) * 0.5d);
                game.RestoreSnapshot(new SimSnapshot(now.Tick, racer, sea.Position + Vector3.down * 40f, now.Circuit));
                game.StepTick(bot);
                Assert.That(player.IsRespawning, Is.True, "The runner in the sea comes back.");
                double back = game.Track.Project(player.transform.position).S;
                Assert.That(back, Is.EqualTo(zones[3] - 4d).Within(0.5d), "Back before the last pad, not at rest past it.");
                Assert.That(player.ForwardSpeed, Is.Zero);
                bool crossed = false;
                for (int more = 0; more < 1000 && game.IsRunning; more++)
                {
                    bool held = player.IsRespawning;
                    game.StepTick(bot);
                    Assert.That(held || !player.IsRespawning, Is.True, $"It fell again, {more} ticks after coming back.");
                    if (game.Track.Project(player.transform.position).S >= seaEnd + 20d)
                    {
                        crossed = true;
                        break;
                    }
                }
                Assert.That(crossed, Is.True, "From rest before the last pad that pad carries the runner over the sea.");
            }
            finally
            {
                Time.timeScale = scale;
                store.Clear(palm.name, speed);
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
            RaceLaunch.Request(new RaceSetup(Stadium(), "STADIUM", EnvironmentScene, RaceMode.Single, SpeedClass.Fast, runner));
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

        private void DeleteGhosts()
        {
            if (Directory.Exists(ghosts.Folder)) Directory.Delete(ghosts.Folder, true);
        }

        // Settings under a test prefix, at their defaults, so the player's settings are never touched.
        private GameSettings TestSettings()
        {
            var store = new GameSettings("ChainRush.Test.ChainRushFlowTests.Settings.");
            store.Clear();
            return store;
        }

        // How many pixels in the middle third of a texture differ clearly from its corner (the cleared background).
        // Copied through a plain texture first: the preview's texture is multisampled.
        private static int DrawnPixels(Texture texture)
        {
            var plain = RenderTexture.GetTemporary(texture.width, texture.height, 0);
            var read = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            RenderTexture active = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, plain);
                RenderTexture.active = plain;
                read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                read.Apply();
            }
            finally
            {
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(plain);
            }
            Color corner = read.GetPixel(1, 1);
            int drawn = 0;
            for (int y = texture.height / 3; y < texture.height * 2 / 3; y++)
                for (int x = texture.width / 3; x < texture.width * 2 / 3; x++)
                {
                    Color pixel = read.GetPixel(x, y);
                    if (Mathf.Abs(pixel.r - corner.r) + Mathf.Abs(pixel.g - corner.g) + Mathf.Abs(pixel.b - corner.b) > 0.1f) drawn++;
                }
            UnityEngine.Object.Destroy(read);
            return drawn;
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

        // The index of the segment that holds s (0 .. count - 1), for messages.
        private static int SegmentAt(TrackDefinition definition, double s)
        {
            double end = 0d;
            for (int i = 0; i < definition.Segments.Count; i++)
            {
                end += definition.Segments[i].Length;
                if (s < end) return i;
            }
            return definition.Segments.Count - 1;
        }

        private static TrackDefinition Skyway()
        {
#if UNITY_EDITOR
            var skyway = AssetDatabase.LoadAssetAtPath<TrackDefinition>(SkywayPath);
            Assert.That(skyway, Is.Not.Null, SkywayPath + " (menu: Create Skyway Track)");
            return skyway;
#else
            Assert.Fail("Flow tests require the Unity Editor.");
            return null;
#endif
        }

        private static TrackDefinition FingerLanes()
        {
#if UNITY_EDITOR
            var finger = AssetDatabase.LoadAssetAtPath<TrackDefinition>(FingerLanesPath);
            Assert.That(finger, Is.Not.Null, FingerLanesPath + " (menu: Create Finger Lanes Track)");
            return finger;
#else
            Assert.Fail("Flow tests require the Unity Editor.");
            return null;
#endif
        }

        private static TrackDefinition PalmCoast()
        {
#if UNITY_EDITOR
            var palm = AssetDatabase.LoadAssetAtPath<TrackDefinition>(PalmCoastPath);
            Assert.That(palm, Is.Not.Null, PalmCoastPath + " (menu: Create Palm Coast Track)");
            return palm;
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
