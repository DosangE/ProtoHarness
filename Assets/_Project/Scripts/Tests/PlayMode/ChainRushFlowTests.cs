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
    // saved when opened directly. Lap runs also check the build's scene list and load the race by name.
    public sealed class ChainRushFlowTests
    {
        private const string LobbyPath = "Assets/_Project/Scenes/ChainRushLobby.unity";
        private const string RacePath = "Assets/_Project/Scenes/ChainRushCircuit.unity";
        private const string TitlePath = "Assets/_Project/Scenes/ChainRushTitle.unity";
        private const string EnvironmentScene = "ChainRushTrackStadium";
        private const string StadiumPath = "Assets/_Project/Data/Circuit_Stadium.asset";
        private const string SkywayPath = "Assets/_Project/Data/Circuit_Skyway.asset";
        private const string FingerLanesPath = "Assets/_Project/Data/Circuit_FingerLanes.asset";
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
            menu.Highlight(5);
            Assert.That(menu.TrackRecordText(5), Is.EqualTo("준비 중"));
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Track), "The placeholder track is locked.");
            Assert.That(menu.Notice, Is.Not.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => menu.Highlight(6), "The stadium, the skyway, Finger Lanes, Palm Coast, Summit Drop and one placeholder.");
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
            menu.Highlight(ProtoHarness.ChainRush.Flow.Lobby.MainPage.SettingsRow);
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
            menu.Move(ProtoHarness.ChainRush.Flow.Lobby.MainPage.SettingsRow);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Settings));
            menu.Back();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Main));
            Assert.That(menu.Cursor, Is.EqualTo(ProtoHarness.ChainRush.Flow.Lobby.MainPage.SettingsRow), "Back on the settings entry.");
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
                menu.Move(ProtoHarness.ChainRush.Flow.Lobby.MainPage.SettingsRow);
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
                Assert.That(menu.Cursor, Is.EqualTo(ProtoHarness.ChainRush.Flow.Lobby.MainPage.SettingsRow), "Back on the settings entry.");
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
                menu.Move(ProtoHarness.ChainRush.Flow.Lobby.MainPage.SettingsRow);
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
            RaceSetup setup = default;
            yield return Load(LobbyPath, loaded => setup = LobbyLapSetup(Find<LobbyMenu>(loaded), "STADIUM", SpeedClass.Fast, runner));
            RaceAssembler assembler = null;
            yield return LoadLapRace(setup, store, ghosts, loaded => assembler = loaded);
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
                // User 2026-11-02: keep the old CircuitTests' exact finish accounting here, in the run that must finish
                // to save a record and ghost. The Lap matrix stops at lap one and never repeats these three laps.
                LapCounter laps = circuit.Laps;
                Assert.That(game.HasFailed, Is.False, "The bot fell or died.");
                Assert.That(laps.IsFinished, Is.True);
                Assert.That(game.HasFinished, Is.True, "The bot did not finish three laps.");
                Assert.That(laps.CompletedLaps, Is.EqualTo(3));
                double lapSeconds = circuit.Definition.LapLength / 20d;
                int sum = 0;
                for (int lap = 1; lap <= 3; lap++)
                {
                    Assert.That(Ticks.ToSeconds(laps.LapTicks(lap)), Is.EqualTo(lapSeconds).Within(lapSeconds * 0.2d), $"lap {lap} time");
                    sum += laps.LapTicks(lap);
                    for (int cp = 0; cp < 3; cp++)
                        Assert.That(laps.CheckpointTick(lap, cp), Is.GreaterThan(0), $"lap {lap} checkpoint {cp}");
                }
                Assert.That(laps.TotalTicks, Is.EqualTo(sum));
                Assert.That(game.Tick, Is.EqualTo(laps.LapEndTick(3)), "The game ends on the third lap's last tick.");
                Assert.That(game.Elapsed, Is.EqualTo(Ticks.ToSeconds(laps.TotalTicks)).Within(1e-3f));
                Assert.That(game.Health, Is.EqualTo(3));
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
                yield return LeaveLapRace(assembler, setup.EnvironmentScene);
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
        public IEnumerator Lobby_TrackChoices_AreChosenWithTheirSavedSetup(
            [Values("STADIUM", "SKYWAY LOOP", "FINGER LANES", "PALM COAST", "SUMMIT DROP")] string title)
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            menu.Confirm();
            menu.Confirm();
            int index = title == "STADIUM" ? 0 : title == "SKYWAY LOOP" ? 1 : title == "FINGER LANES" ? 2 : title == "PALM COAST" ? 3 : 4;
            menu.Highlight(index);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Runner), title + " can be raced.");
            RaceSetup setup = menu.BuildSetup();
            RaceSetup saved = LobbyLapSetup(menu, title, SpeedClass.Fast, runner);
            Assert.That(setup.Track, Is.SameAs(saved.Track));
            Assert.That(setup.Title, Is.EqualTo(title));
            Assert.That(setup.EnvironmentScene, Is.EqualTo(saved.EnvironmentScene));
            Assert.That(setup.Look, Is.SameAs(saved.Look));
        }

        // Specification changed, user 2026-11-02: a pre-existing 30 m highway, with its middle 9 m left of the
        // 12 m racing lanes; the corners are separate 12 m ramps. Seven poles stay 40 m apart, +/-11 m from
        // the HIGHWAY middle, left first (the right pole is now +2 m from the racing line).
        [Test]
        public void Skyway_ElevatedStretch_IsWideWithSevenAlternatingSwingPoles()
        {
            TrackDefinition skyway = Skyway();
            Assert.That(skyway.TryValidate(out string error), Is.True, error);
            System.Collections.Generic.IReadOnlyList<TrackDefinition.Segment> segments = skyway.Segments;
            const int straight = 11;
            Assert.That(segments[straight].IsArc, Is.False);
            Assert.That(segments[straight].Length, Is.EqualTo(330f));
            double start = 0d;
            for (int i = 0; i < straight; i++) start += segments[i].Length;
            double end = start + segments[straight].Length;
            TrackDefinition.Bay highway = skyway.Bays[1];
            Assert.That(highway.StraightDeck, Is.True);
            Assert.That(highway.FromS, Is.EqualTo(start).Within(0.001d));
            Assert.That(highway.ToS, Is.EqualTo(end).Within(0.001d));
            Assert.That(highway.Width + 2f * skyway.HalfWidthAt(start), Is.EqualTo(30f));
            Assert.That(highway.Extension, Is.EqualTo(50f));
            double cornerBefore = start - segments[straight - 1].Length, cornerAfter = end + segments[straight + 1].Length;
            for (double s = cornerBefore; s <= cornerAfter; s += 10d) Assert.That(skyway.HalfWidthAt(s), Is.EqualTo(6f).Within(1e-4f), "12 m ramps/racing lanes at S " + s.ToString("0"));
            Assert.That(skyway.HalfWidthAt(0d), Is.EqualTo(RoadProfile.DeckHalfWidth), "The start line keeps the usual road.");
            Assert.That(skyway.WidestHalfWidth, Is.EqualTo(6f), "The separate highway no longer widens the racing road.");
            Assert.That(skyway.Poles.Count, Is.EqualTo(7));
            for (int i = 0; i < skyway.Poles.Count; i++)
            {
                TrackDefinition.Pole pole = skyway.Poles[i];
                Assert.That(pole.S, Is.EqualTo(start + 20d + 40d * i).Within(1e-3d), "pole " + i);
                Assert.That(pole.Offset + 9f, Is.EqualTo(i % 2 == 0 ? -11f : 11f), "pole " + i + " alternates about the highway middle, left first");
                Assert.That(pole.Reach, Is.EqualTo(10f));
                Assert.That(pole.S, Is.InRange(start, end), "pole " + i + " is on the straight");
            }
        }

        // The on-ramp (user 2026-11-02, "넓은 지상도로에서 고가를 향해 오르막으로 가는게 정식 루트이고 넓은 지상도로의 정면은
        // 가드로 막혀"): beside the flat approach the bay is road at the road's height with no guard between it and the
        // race's lanes, it ends square against a guard a runner going straight on meets, and the race's lanes go on.
        [UnityTest]
        public IEnumerator Race_SkywayBay_IsOneRoadWithTheLanesAndEndsAgainstAGuard()
        {
            TrackDefinition skyway = Skyway();
            Assert.That(skyway.Bays.Count, Is.EqualTo(2), "User 2026-11-02: the original ground bay plus the independent highway.");
            TrackDefinition.Bay bay = skyway.Bays[0];
            Assert.That(bay.Left, Is.True);
            RaceLaunch.Request(new RaceSetup(skyway, "SKYWAY LOOP", "", RaceMode.Single, SpeedClass.Fast, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = TestRecords();
            assembler.Ghosts = ghosts;
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            PhysicsScene physics = assembler.gameObject.scene.GetPhysicsScene();
            Centerline line = skyway.BuildCenterline(Vector3.zero, 0f);
            float main = skyway.HalfWidthAt(bay.FromS);
            TrackFrame frame = line.FrameAt(bay.ToS - 8d);
            Vector3 left = -frame.Right;
            Vector3 overBay = frame.Position + left * (main + bay.Width * 0.5f);
            Assert.That(physics.Raycast(overBay + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 10f), Is.True, "Road under the bay.");
            Assert.That(ground.point.y, Is.EqualTo(frame.Position.y).Within(0.01f), "The bay is level with the road.");
            Vector3 lanes = frame.Position + Vector3.up;
            Assert.That(physics.Raycast(lanes, left, main + bay.Width - 0.5f), Is.False, "No guard between the race's lanes and the bay.");
            Assert.That(physics.Raycast(overBay + Vector3.up, frame.Forward, out RaycastHit wall, 30f), Is.True, "A runner going straight on in the bay meets the end guard.");
            Assert.That(Vector3.Dot(wall.point - line.FrameAt(bay.ToS).Position, frame.Forward), Is.EqualTo(-RoadMeshBuilder.GuardThickness).Within(0.05f),
                "The guard stands square across the bay's end.");
            Assert.That(physics.Raycast(lanes, frame.Forward, 14f), Is.False, "The race's own lanes go on up the ramp.");
        }

        // User 2026-11-02: the highway runs straight past both junctions, closed across its full width. Its
        // racing lanes share the slab with the other lanes; the right ramps have no guard across their join.
        [UnityTest]
        public IEnumerator Race_SkywayHighway_ClosesBothEndsAndJoinsRampsWithoutInteriorGuards()
        {
            TrackDefinition skyway = Skyway();
            TrackDefinition.Bay bay = skyway.Bays[1];
            Assert.That(bay.StraightDeck, Is.True);
            TrackLook look = null;
#if UNITY_EDITOR
            look = AssetDatabase.LoadAssetAtPath<TrackLook>("Assets/_Project/Data/TrackLook_Skyway.asset");
#endif
            Assert.That(look, Is.Not.Null);
            RaceLaunch.Request(new RaceSetup(skyway, "SKYWAY LOOP", "", RaceMode.Single, SpeedClass.Fast, runner, look));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = TestRecords();
            assembler.Ghosts = ghosts;
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            PhysicsScene physics = assembler.gameObject.scene.GetPhysicsScene();
            Centerline line = skyway.BuildCenterline(Vector3.zero, 0f);
            TrackFrame join = line.FrameAt(bay.FromS);
            float length = bay.ToS - bay.FromS;
            foreach (float x in new[] { -23f, -9f, 0f, 5f })
            {
                Vector3 behind = join.TransformPoint(new Vector3(x, 1f, -20f));
                Assert.That(physics.Raycast(behind, -join.Forward, out RaycastHit start, 40f), Is.True, "The whole highway is blocked behind the join, x=" + x);
                Assert.That(join.InverseTransformPoint(start.point).z, Is.EqualTo(-50f + RoadMeshBuilder.GuardThickness).Within(0.05f));
                Vector3 beyond = join.TransformPoint(new Vector3(x, 1f, length + 20f));
                Assert.That(physics.Raycast(beyond, join.Forward, out RaycastHit end, 40f), Is.True, "The whole highway is blocked beyond the exit, x=" + x);
                Assert.That(join.InverseTransformPoint(end.point).z, Is.EqualTo(length + 50f - RoadMeshBuilder.GuardThickness).Within(0.05f));
                Vector3 above = join.TransformPoint(new Vector3(x, 3f, 100f));
                Assert.That(physics.Raycast(above, Vector3.down, out RaycastHit road, 6f), Is.True);
                Assert.That(road.point.y, Is.EqualTo(join.Position.y).Within(0.01f));
            }
            foreach (double s in new[] { bay.FromS - 10d, bay.FromS + 10d, bay.ToS - 10d, bay.ToS + 10d })
            {
                TrackFrame frame = line.FrameAt(s);
                Assert.That(physics.Raycast(frame.Position + Vector3.up, -join.Right, 20f), Is.False, "No interior guard at S " + s);
            }
            for (double s = bay.FromS - 50d; s < bay.ToS + 50d; s += 0.5d)
            {
                TrackFrame frame = line.FrameAt(s);
                Assert.That(physics.Raycast(frame.Position + Vector3.up * 2f, Vector3.down, out RaycastHit road, 4f), Is.True, "Continuous ramp/deck surface at S " + s);
                Assert.That(road.point.y, Is.EqualTo(frame.Position.y).Within(0.015f));
            }
            Assert.That(Vector3.Dot(line.FrameAt(bay.FromS - 50d).Position - join.Position, join.Right), Is.GreaterThan(6f), "The join ramp approaches from the north/right.");
            Assert.That(Vector3.Dot(line.FrameAt(bay.ToS + 50d).Position - line.FrameAt(bay.ToS).Position, join.Right), Is.GreaterThan(6f), "The exit ramp goes north/right.");
            CircuitRace circuit = Find<CircuitRace>(assembler.gameObject.scene);
            int centreVertices = 0, joinHatches = 0, exitHatches = 0;
            foreach (MeshFilter filter in circuit.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.name != "Highway accent") continue;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 local = join.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    if (Mathf.Abs(local.y - 0.025f) < 0.001f)
                    {
                        Assert.That(Mathf.Abs(local.x + 9f), Is.EqualTo(0.08f).Within(0.001f).Or.EqualTo(0.2f).Within(0.001f), "Yellow double lines use the highway middle.");
                        centreVertices++;
                    }
                    if (Mathf.Abs(local.y - 0.03f) < 0.001f && local.x > 2f)
                    {
                        if (local.z < 0f) joinHatches++;
                        if (local.z > length) exitHatches++;
                    }
                }
            }
            Assert.That(centreVertices, Is.GreaterThan(100));
            Assert.That(joinHatches, Is.GreaterThan(10), "Yellow hatching at the join gore.");
            Assert.That(exitHatches, Is.GreaterThan(10), "Yellow hatching at the exit gore.");
        }

        // The altered collision path needs a rollback through each ramp/deck seam, as well as the unchanged
        // STADIUM fingerprint. Record and restore with the existing StateTrace/InputLog tools, tick by tick.
        [UnityTest, Timeout(120000)]
        public IEnumerator Race_SkywayHighway_RollbackAcrossJoinAndExitRepeatsEveryTick()
        {
            TrackDefinition skyway = Skyway();
            TrackDefinition.Bay bay = skyway.Bays[1];
            RaceLaunch.Request(new RaceSetup(skyway, "SKYWAY LOOP", "", RaceMode.Single, SpeedClass.Fast, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = TestRecords();
            assembler.Ghosts = ghosts;
            Scene scene = assembler.gameObject.scene;
            ChainRushGame game = Find<ChainRushGame>(scene);
            RunnerMotor player = Find<RunnerMotor>(scene);
            CircuitRace circuit = Find<CircuitRace>(scene);
            var bot = new CircuitBot(game, player, player.GetComponent<GrappleController>(), circuit);
            float scale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                game.StartRun();
                // The race's ghost recorder refuses the first tick out of order and drops this run's ghost
                // (RaceAssembler.RecordTick), once; the rewinds here check the simulation only.
                LogAssert.Expect(LogType.Error, new Regex("came after \\d+; this run will keep no ghost"));
                foreach (double junction in new[] { (double)bay.FromS, bay.ToS })
                {
                    int limit = game.Tick + 10000;
                    while (game.IsRunning && game.Track.Project(player.transform.position).S < junction - 50d && game.Tick < limit) game.StepTick(bot);
                    Assert.That(game.IsRunning, Is.True);
                    Assert.That(game.Tick, Is.LessThan(limit));
                    SimSnapshot snapshot = game.CaptureSnapshot();
                    var log = new InputLog();
                    var original = Trace(new InputRecorder(bot, log));
                    while (game.IsRunning && game.Track.Project(player.transform.position).S < junction + 55d && game.Tick < limit) game.StepTick(original);
                    Assert.That(game.IsRunning, Is.True);
                    Assert.That(game.Tick, Is.LessThan(limit));
                    Assert.That(log.Count, Is.GreaterThan(100), "The recorded window crosses the whole junction.");
                    game.RestoreSnapshot(snapshot);
                    var replay = Trace(new InputReplay(log));
                    for (int i = 0; i < log.Count && game.IsRunning; i++) game.StepTick(replay);
                    Assert.That(StateTrace.Compare(original, replay, log.Count), Is.Null, "Junction S " + junction);
                }
            }
            finally
            {
                Time.timeScale = scale;
            }

            StateTrace Trace(IInputSource input) => new StateTrace(game, player, input,
                ("lapProgress", () => circuit.Laps.Progress), ("checkpoints", () => circuit.Laps.CheckpointsPassed),
                ("lastGroundedS", () => game.Racer.LastGroundedS));
        }

        // User 2026-11-02: "장식 테스트도 봇완주에 포함 ... 한바퀴만". Every track at every speed keeps its coverage,
        // using the saved lobby entry, decoration and look in the same run. The first counted lap is enough here;
        // Race_FinishFromTheLobby keeps the finish/records/ghost contract. VeryFast may brush a guard (2026-10-25), and Fast
        // too since turning costs less speed (user 2026-11-04): corners are for the pole swing, only Normal must keep clear.
        [UnityTest, Timeout(90000), Category("Lap")]
        public IEnumerator Race_BotLap_WithLobbyEnvironmentAndLook_CompletesOneLap(
            [Values(SpeedClass.Normal, SpeedClass.Fast, SpeedClass.VeryFast)] SpeedClass speed,
            [Values("STADIUM", "SKYWAY LOOP", "FINGER LANES", "PALM COAST", "SUMMIT DROP")] string title)
        {
            RaceSetup setup = default;
            yield return Load(LobbyPath, scene => setup = LobbyLapSetup(Find<LobbyMenu>(scene), title, speed, runner));
            TrackDefinition track = setup.Track;
            var store = TestRecords();
            store.Clear(track.name, speed);
            RaceAssembler assembler = null;
            yield return LoadLapRace(setup, store, ghosts, loaded => assembler = loaded);
            Scene raceScene = assembler.gameObject.scene;
            ChainRushGame game = Find<ChainRushGame>(raceScene);
            RunnerMotor player = Find<RunnerMotor>(raceScene);
            CircuitRace circuit = Find<CircuitRace>(raceScene);
            if (title == "PALM COAST") Assert.That(game.Track.BoostZoneCount, Is.EqualTo(5));

            // Fast's two FINGER kickers keep the original height and clock-tower/bridge comparison.
            var crests = new System.Collections.Generic.List<double>();
            double crestS = 0d;
            if (title == "FINGER LANES" && speed == SpeedClass.Fast)
                foreach (TrackDefinition.Segment segment in track.Segments)
                {
                    crestS += segment.Length;
                    if (segment.Launch > 0f) crests.Add(crestS);
                }
            var lift = new float[crests.Count];
            float scale = Time.timeScale;
            bool respawned = false;
            Action<int> onTick = tick => respawned |= player.IsRespawning;
            game.TickCompleted += onTick;
            try
            {
                game.SetInputSource(new CircuitBot(game, player, player.GetComponent<GrappleController>(), circuit));
                Time.timeScale = 6f;
                game.StartRun();
                float standing = game.Track.Project(player.transform.position).H;
                float deadline = Time.realtimeSinceStartup + 60f;
                float maxOffset = 0f;
                double maxOffsetS = 0d;
                double lastProgress = circuit.Laps.Progress;
                while (circuit.Laps.CompletedLaps < 1 && !game.HasFailed && Time.realtimeSinceStartup < deadline)
                {
                    TrackCoord coord = game.Track.Project(player.transform.position);
                    if (Mathf.Abs(coord.D) > maxOffset)
                    {
                        maxOffset = Mathf.Abs(coord.D);
                        maxOffsetS = coord.S;
                    }
                    Assert.That(circuit.Laps.Progress, Is.GreaterThanOrEqualTo(lastProgress - 1d), "The bot's progress must not run backwards.");
                    lastProgress = circuit.Laps.Progress;
                    for (int k = 0; k < crests.Count; k++)
                        if (coord.S > crests[k] && coord.S < crests[k] + 25d) lift[k] = Mathf.Max(lift[k], coord.H - standing);
                    yield return null;
                }
                Time.timeScale = scale;
                LapCounter laps = circuit.Laps;
                string where = $"{title} at {speed}: tick {game.Tick}, {laps.CompletedLaps} laps, progress {laps.Progress:F1} m, " +
                               $"max offset {maxOffset:F2} m at S {maxOffsetS:F1} (segment {SegmentAt(track, maxOffsetS)})";
                Assert.That(game.HasFailed, Is.False, "The bot fell or died. " + where);
                Assert.That(respawned, Is.False, "The bot fell and respawned. " + where);
                Assert.That(laps.CompletedLaps, Is.EqualTo(1), "The bot did not complete one lap. " + where);
                double lapSeconds = track.LapLength / (20d * SpeedScale.Of(speed));
                float seconds = Ticks.ToSeconds(laps.LapTicks(1));
                Assert.That(seconds, Is.EqualTo(lapSeconds).Within(lapSeconds * 0.2d), "lap 1 time. " + where);
                if (speed == SpeedClass.Normal)
                    Assert.That(maxOffset, Is.LessThan(track.RoadHalfWidth - 0.4f), "The bot touched a guard. " + where);
                for (int k = 0; k < crests.Count; k++)
                {
                    Debug.Log($"ChainRush Finger Lanes kicker {k} at S {crests[k]:F1}: rose {lift[k]:F2} m above the road");
                    Assert.That(lift[k], Is.GreaterThan(0.5f), $"The bot was not thrown up past the kicker at S {crests[k]:F1}. " + where);
                }
                if (crests.Count > 0)
                    Assert.That(lift[1], Is.GreaterThanOrEqualTo(lift[0] * 0.9f), "The clock tower's bump throws much less than the bridge's. " + where);
                if (title == "STADIUM" && speed == SpeedClass.Fast) Assert.That(game.Health, Is.EqualTo(3));
                for (int cp = 0; cp < track.CheckpointCount; cp++)
                    Assert.That(laps.CheckpointTick(1, cp), Is.GreaterThan(0), $"lap 1 checkpoint {cp}");
                Debug.Log($"ChainRush {title} at {speed}: lap 1 {seconds:F2} s (expected {lapSeconds:F2} s), {game.Grapples} grapples. {where}");
                yield return LeaveLapRace(assembler, setup.EnvironmentScene);
            }
            finally
            {
                game.TickCompleted -= onTick;
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

        // A runner that swings round the nails' corner poles takes both shortcuts in one lap (user 2026-11-02): the laps
        // count across them. The bot hooks at the junction, lets go a quarter turn on, and again at
        // the straight's end (ShortcutBot); the R15 turns are tighter than the runner's own 25 m, so only a swing makes them.
        [UnityTest, Timeout(90000), Category("Lap")]
        public IEnumerator Race_FingerLanesShortcuts_ASwingingRunnerTakesBothInOneLap()
        {
            var store = TestRecords();
            TrackDefinition finger = FingerLanes();
            store.Clear(finger.name, SpeedClass.Fast);
            RaceSetup setup = default;
            yield return Load(LobbyPath, scene => setup = LobbyLapSetup(Find<LobbyMenu>(scene), "FINGER LANES", SpeedClass.Fast, runner));
            RaceAssembler assembler = null;
            yield return LoadLapRace(setup, store, ghosts, loaded => assembler = loaded);
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
                float deadline = Time.realtimeSinceStartup + 60f;
                while (circuit.Laps.CompletedLaps < 1 && !game.HasFailed && Time.realtimeSinceStartup < deadline) yield return null;
                Time.timeScale = scale;
                LapCounter laps = circuit.Laps;
                string where = $"tick {game.Tick}, {laps.CompletedLaps} laps, progress {laps.Progress:F1} m, shortcuts taken {bot.Taken}, hooks missed {bot.Missed}";
                Assert.That(game.HasFailed, Is.False, "The runner fell or died. " + where);
                Assert.That(laps.CompletedLaps, Is.EqualTo(1), "The runner did not complete one lap. " + where);
                Assert.That(bot.Missed, Is.EqualTo(0), "A hook found no pole. " + where);
                Assert.That(bot.Taken, Is.EqualTo(2), "Both shortcuts in one lap. " + where);
                Debug.Log($"ChainRush Finger Lanes with shortcuts lap 1: {Ticks.ToSeconds(laps.LapTicks(1)):F2} s");
                yield return LeaveLapRace(assembler, setup.EnvironmentScene);
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
        [UnityTest, Timeout(90000), Category("Lap")]
        public IEnumerator Race_FingerLanesShortcuts_InnerKeyHeld_TheRopeAddsNoSpeed()
        {
            var store = TestRecords();
            TrackDefinition finger = FingerLanes();
            store.Clear(finger.name, SpeedClass.Fast);
            RaceSetup setup = default;
            yield return Load(LobbyPath, scene => setup = LobbyLapSetup(Find<LobbyMenu>(scene), "FINGER LANES", SpeedClass.Fast, runner));
            RaceAssembler assembler = null;
            yield return LoadLapRace(setup, store, ghosts, loaded => assembler = loaded);
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
                string where = $"tick {game.Tick}, {circuit.Laps.CompletedLaps} laps, shortcuts taken {bot.Taken}, hooks missed {bot.Missed}, "
                    + $"progress {circuit.Laps.Progress:F1} m, heading {player.Heading:F1}, speed {player.HorizontalSpeed:F2} m/s, swinging {player.IsSwinging}";
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
                Assert.That(bot.Missed, Is.Zero, "A hook found no pole. " + where);
                yield return LeaveLapRace(assembler, setup.EnvironmentScene);
            }
            finally
            {
                game.TickCompleted -= sample;
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

        // The race laid its road, walls and lane markings with the look the lobby entry carries (user 2026-11-02, every
        // track's own road and walls).
        private static void AssertLaysTheLook(CircuitRace circuit, TrackLook look)
        {
            int roads = 0, rails = 0, painted = 0;
            foreach (Renderer renderer in circuit.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.sharedMaterial == look.Road) roads++;
                if (renderer.sharedMaterial == look.Rail) rails++;
                if (renderer.sharedMaterial == look.Markings) painted++;
            }
            Assert.That(roads, Is.GreaterThan(0), $"The road pieces use {look.name}'s road.");
            Assert.That(rails, Is.GreaterThan(0), $"The guard walls use {look.name}'s rail.");
            Assert.That(painted, Is.GreaterThan(0), $"The road has {look.name}'s lane markings.");
            if (look.Sky == null) return;
            Assert.That(RenderSettings.skybox, Is.SameAs(look.Sky), $"The race draws {look.name}'s sky.");
            Assert.That(RenderSettings.fogColor, Is.EqualTo(look.Horizon), $"The fog is {look.name}'s horizon.");
        }

        // The race plays the look's own sounds: Finger Lanes is the track that has them (user 2026-11-05).
        [UnityTest]
        public IEnumerator Race_FingerLanesLook_PlaysTheTracksMusicAndAmbience()
        {
#if UNITY_EDITOR
            var store = TestRecords();
            var definition = AssetDatabase.LoadAssetAtPath<TrackDefinition>(FingerLanesPath);
            var look = AssetDatabase.LoadAssetAtPath<TrackLook>("Assets/_Project/Data/TrackLook_FingerLanes.asset");
            Assert.That(definition, Is.Not.Null);
            Assert.That(look, Is.Not.Null);
            Assert.That(look.Music, Is.Not.Null, "Rebuild Track Sounds links the music.");
            Assert.That(look.Ambience, Is.Not.Null, "Rebuild Track Sounds links the ambience.");
            RaceLaunch.Request(new RaceSetup(definition, "FINGER LANES", "", RaceMode.Single, SpeedClass.Fast, runner, look));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = store;
            assembler.Ghosts = ghosts;
            ChainRushAudio sound = Find<ChainRushAudio>(assembler.gameObject.scene);
            Assert.That(sound.MusicClip, Is.SameAs(look.Music));
            Assert.That(sound.TrackAmbienceClip, Is.SameAs(look.Ambience));
#else
            Assert.Fail("Flow tests require the Unity Editor.");
            yield break;
#endif
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

        // A track look's sky (user 2026-11-02, "하늘 … 서킷 공통적으로"): the race camera draws it behind everything instead of
        // the scene's flat colour, the fog thickens linearly to its horizon colour between its distances, and the camera
        // draws no further than the fog's end.
        [UnityTest]
        public IEnumerator Race_WithATrackLookSky_DrawsTheSkyAndFogsToItsHorizon()
        {
#if UNITY_EDITOR
            var store = TestRecords();
            TrackDefinition palm = PalmCoast();
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader panorama = Shader.Find("Skybox/Panoramic");
            Assert.That(lit, Is.Not.Null);
            Assert.That(panorama, Is.Not.Null);
            var road = new Material(lit) { name = "Test look road" };
            var rail = new Material(lit) { name = "Test look rail" };
            var sky = new Material(panorama) { name = "Test look sky" };
            var horizon = new Color(0.7f, 0.8f, 0.9f, 1f);
            TrackLook look = ScriptableObject.CreateInstance<TrackLook>();
            var data = new SerializedObject(look);
            data.FindProperty("road").objectReferenceValue = road;
            data.FindProperty("rail").objectReferenceValue = rail;
            data.FindProperty("sky").objectReferenceValue = sky;
            data.FindProperty("horizon").colorValue = horizon;
            data.FindProperty("fogStart").floatValue = 120f;
            data.FindProperty("fogEnd").floatValue = 430f;
            data.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                RaceLaunch.Request(new RaceSetup(palm, "PALM COAST", "", RaceMode.Single, SpeedClass.Fast, runner, look));
                RaceAssembler assembler = null;
                yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
                assembler.Records = store;
                assembler.Ghosts = ghosts;
                Camera view = Find<FollowCamera>(assembler.gameObject.scene).GetComponent<Camera>();
                Assert.That(RenderSettings.skybox, Is.SameAs(sky), "The race draws the look's sky.");
                Assert.That(view.clearFlags, Is.EqualTo(CameraClearFlags.Skybox), "The camera clears to the sky, not the scene's flat colour.");
                Assert.That(RenderSettings.fog, Is.True);
                Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.Linear));
                Assert.That(RenderSettings.fogColor, Is.EqualTo(horizon), "The far land fades into the horizon's colour.");
                Assert.That(RenderSettings.fogStartDistance, Is.EqualTo(120f));
                Assert.That(RenderSettings.fogEndDistance, Is.EqualTo(430f));
                Assert.That(view.farClipPlane, Is.EqualTo(430f), "Nothing past the fog's end is drawn.");
            }
            finally
            {
                store.Clear(palm.name, SpeedClass.Fast);
                UnityEngine.Object.Destroy(look);
                UnityEngine.Object.Destroy(road);
                UnityEngine.Object.Destroy(rail);
                UnityEngine.Object.Destroy(sky);
            }
#else
            Assert.Fail("This test requires the Unity Editor.");
            yield break;
#endif
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

        [Test]
        public void SummitDrop_Layout_HasEightHeightStagesOneLapAndTheDesignedFeatures()
        {
            TrackDefinition summit = SummitDrop();
            Assert.That(summit.TryValidate(out string error), Is.True, error);
            Assert.That(summit.IsOpenCourse, Is.True);
            Assert.That(summit.LapCount, Is.EqualTo(1));
            Assert.That(summit.LapLength, Is.EqualTo(2279.956d).Within(0.02d));
            Centerline line = summit.BuildCenterline(Vector3.zero, 0f);
            Assert.That(line.IsLoop, Is.False);
            var indices = new[] { 0, 8, 16, 23, 28, 31, 38, 44, 48 };
            var heights = new[] { 0f, -70f, -99.6f, -114.6f, -114.6f, -114.6f, -129.1f, -179.1f, -167.1f };
            double s = 0d;
            int stage = 0;
            for (int i = 0; i <= summit.Segments.Count; i++)
            {
                if (i == indices[stage])
                {
                    Assert.That(line.FrameAt(s).Position.y, Is.EqualTo(heights[stage]).Within(0.02f), $"stage {stage} S {s:F2}");
                    if (stage > 0) Debug.Log($"SUMMIT DROP boundary {stage}: S={s:F3}, height={line.FrameAt(s).Position.y:F3}");
                    stage++;
                }
                if (i == summit.Segments.Count) break;
                TrackDefinition.Segment segment = summit.Segments[i];
                Assert.That(Mathf.Abs(segment.EndGrade), Is.LessThanOrEqualTo(0.2f));
                if (segment.IsArc) Assert.That(segment.Radius, Is.GreaterThanOrEqualTo(28f));
                else if (i > 0 && segment.EndGrade != summit.Segments[i - 1].EndGrade)
                    Assert.That(segment.Length, Is.EqualTo(10f), "All grade changes use short easing straights.");
                s += segment.Length;
            }
            Assert.That(line.FrameAt(s).Grade, Is.Zero);
            Assert.That(line.FrameAt(s + summit.FinishRunout).Position.y, Is.EqualTo(-167.1f).Within(0.02f));
            Assert.That(summit.OpenEdges.Count, Is.EqualTo(1));
            TrackDefinition.OpenEdge edge = summit.OpenEdges[0];
            Assert.That(edge.Left, Is.True);
            Assert.That(edge.ToS - edge.FromS, Is.EqualTo(150f));
            Assert.That(summit.HalfWidthAt(900d), Is.EqualTo(4.5f));
            Assert.That(summit.Gaps().Count, Is.EqualTo(1));
            Assert.That(summit.Gaps()[0].End - summit.Gaps()[0].Start, Is.EqualTo(30d));
            Assert.That(line.BoostZoneCount, Is.EqualTo(3));
            Assert.That(line.LaunchCount, Is.EqualTo(1));
            Assert.That(line.BranchCount, Is.EqualTo(1));
            Assert.That(summit.Poles.Count, Is.EqualTo(3));
            Assert.That(summit.Poles[1].S - summit.Poles[0].S, Is.EqualTo(40f).Within(0.001f));
            Assert.That(summit.Poles[2].S - summit.Poles[1].S, Is.EqualTo(40f).Within(0.001f));
            Assert.That(summit.CheckpointCount, Is.InRange(3, 4));
            for (int i = 0; i < summit.CheckpointCount; i++)
                Assert.That(summit.CheckpointS(i), Is.Not.InRange((double)summit.Shortcuts[0].FromS, summit.Shortcuts[0].ToS));
            var points = new System.Collections.Generic.List<Vector3>();
            line.GetPoles(points);
            Assert.That(points.Count, Is.EqualTo(18), "13 main turns, 2 shortcut turns and 3 slalom poles.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SummitDrop_BotAtEachSpeed_FinishesAndReportsTimeOffsetAndFalls(
            [Values(SpeedClass.Normal, SpeedClass.Fast, SpeedClass.VeryFast)] SpeedClass speed)
        {
            TrackDefinition summit = SummitDrop();
            var store = TestRecords();
            RaceLaunch.Request(new RaceSetup(summit, "SUMMIT DROP", "", RaceMode.Single, speed, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = store;
            assembler.Ghosts = ghosts;
            Scene scene = assembler.gameObject.scene;
            ChainRushGame game = Find<ChainRushGame>(scene);
            RunnerMotor player = Find<RunnerMotor>(scene);
            CircuitRace circuit = Find<CircuitRace>(scene);
            var bot = new CircuitBot(game, player, player.GetComponent<GrappleController>(), circuit);
            float scale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                game.StartRun();
                float offset = 0f, lipSpeed = 0f;
                double offsetS = 0d;
                int falls = 0;
                bool held = false;
                double lip = summit.Gaps()[0].Start - 15d;
                for (int tick = 0; tick < 16000 && game.IsRunning; tick++)
                {
                    double before = game.Track.Project(player.transform.position).S;
                    game.StepTick(bot);
                    TrackCoord here = game.Track.Project(player.transform.position);
                    if (Mathf.Abs(here.D) > offset) { offset = Mathf.Abs(here.D); offsetS = here.S; }
                    if (player.IsRespawning && !held) falls++;
                    held = player.IsRespawning;
                    if (before < lip && here.S >= lip) lipSpeed = player.ForwardSpeed;
                    if (tick % 400 == 0) yield return null;
                }
                string metrics = $"SUMMIT DROP {speed}: finish={game.HasFinished}, seconds={Ticks.ToSeconds(circuit.Laps.TotalTicks):F3}, " +
                    $"maxOffset={offset:F3} at S={offsetS:F3}, respawns={falls}, lipSpeed={lipSpeed:F3}";
                Debug.Log(metrics);
                Assert.That(game.HasFinished, Is.True, metrics);
                Assert.That(circuit.Laps.CompletedLaps, Is.EqualTo(1), metrics);
                Assert.That(lipSpeed, Is.GreaterThan(Mathf.Sqrt(45f * 22f / 0.48f)), "The pad carries even Normal over the 30 m gap, 45 m from the lip.");
            }
            finally { Time.timeScale = scale; store.Clear(summit.name, speed); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SummitDrop_OpenLeftEdge_FallsTwelveMetresThenRespawnsOnRoad()
        {
            TrackDefinition summit = SummitDrop();
            RaceLaunch.Request(new RaceSetup(summit, "SUMMIT DROP", "", RaceMode.Single, SpeedClass.Fast, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = TestRecords();
            assembler.Ghosts = ghosts;
            Scene scene = assembler.gameObject.scene;
            ChainRushGame game = Find<ChainRushGame>(scene);
            RunnerMotor player = Find<RunnerMotor>(scene);
            float scale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                game.StartRun();
                // Start ON the narrow straight. Drive physically through its left edge, never teleport below it.
                TrackFrame frame = game.Track.FrameAt(900d);
                SimSnapshot now = game.CaptureSnapshot();
                RacerState.Snapshot racer = now.Racer;
                racer.LastGroundedS = 900d;
                racer.Heading = Mathf.Atan2(-frame.Right.x, -frame.Right.z) * Mathf.Rad2Deg;
                racer.Velocity = -frame.Right * player.RunSpeed;
                racer.Grounded = true;
                CircuitRace.Snapshot atEdge = now.Circuit;
                atEdge.LastS = frame.S;
                game.RestoreSnapshot(new SimSnapshot(now.Tick, racer, frame.Position + Vector3.up, atEdge));
                var input = new InputLog();
                for (int i = 0; i < 300; i++) input.Add(new TickInput(0f, false, false, false));
                var drive = new InputReplay(input);
                float lowest = 0f;
                bool outside = false;
                for (int tick = 0; tick < 300 && !player.IsRespawning; tick++)
                {
                    game.StepTick(drive);
                    TrackCoord here = game.Track.Project(player.transform.position);
                    outside |= here.D < -summit.HalfWidthAt(here.S) - 1f;
                    lowest = Mathf.Min(lowest, here.H);
                }
                Assert.That(outside, Is.True, "The capsule actually crossed the open left edge.");
                Assert.That(lowest, Is.LessThan(-10f), "A real fall before the existing -12 m recovery.");
                Assert.That(player.IsRespawning, Is.True);
                TrackCoord back = game.Track.Project(player.transform.position);
                Assert.That(Mathf.Abs(back.D), Is.LessThan(0.1f));
                Assert.That(back.H, Is.InRange(0.5f, 1.5f));
                Assert.That(player.IsGrounded, Is.True);
            }
            finally { Time.timeScale = scale; }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SummitDrop_SwingShortcut_ReachesFinishSoonerThanMainRoad()
        {
            TrackDefinition summit = SummitDrop();
            var times = new int[2];
            for (int run = 0; run < 2; run++)
            {
                RaceLaunch.Request(new RaceSetup(summit, "SUMMIT DROP", "", RaceMode.Single, SpeedClass.Fast, runner));
                RaceAssembler assembler = null;
                yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
                assembler.Records = TestRecords();
                assembler.Ghosts = ghosts;
                Scene scene = assembler.gameObject.scene;
                ChainRushGame game = Find<ChainRushGame>(scene);
                RunnerMotor player = Find<RunnerMotor>(scene);
                CircuitRace circuit = Find<CircuitRace>(scene);
                var shortcut = new ShortcutBot(game, player, summit);
                IInputSource input = run == 0 ? (IInputSource)new CircuitBot(game, player, player.GetComponent<GrappleController>(), circuit) : shortcut;
                float scale = Time.timeScale;
                try
                {
                    Time.timeScale = 0f;
                    game.StartRun();
                    for (int tick = 0; tick < 14000 && game.IsRunning; tick++)
                    {
                        game.StepTick(input);
                        if (tick % 400 == 0) yield return null;
                    }
                    Assert.That(game.HasFinished, Is.True, $"run {run}, progress {circuit.Laps.Progress:F1}");
                    times[run] = circuit.Laps.TotalTicks;
                    if (run == 1)
                    {
                        Assert.That(shortcut.Taken, Is.EqualTo(1));
                        Assert.That(shortcut.Missed, Is.Zero);
                    }
                    Debug.Log($"SUMMIT DROP shortcut={run == 1}: finish {Ticks.ToSeconds(times[run]):F3} s");
                }
                finally { Time.timeScale = scale; TestRecords().Clear(summit.name, SpeedClass.Fast); }
            }
            Assert.That(times[1], Is.LessThan(times[0]), "The swing must save time over the entire run.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SummitDrop_JumpWithoutPad_FallsAndRespawnsBeforePad(
            [Values(SpeedClass.Normal, SpeedClass.Fast, SpeedClass.VeryFast)] SpeedClass speed)
        {
            TrackDefinition summit = SummitDrop();
            RaceLaunch.Request(new RaceSetup(summit, "SUMMIT DROP", "", RaceMode.Single, speed, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = TestRecords();
            assembler.Ghosts = ghosts;
            Scene scene = assembler.gameObject.scene;
            ChainRushGame game = Find<ChainRushGame>(scene);
            RunnerMotor player = Find<RunnerMotor>(scene);
            CircuitRace circuit = Find<CircuitRace>(scene);
            float scale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                game.StartRun();
                var gap = summit.Gaps()[0];
                TrackFrame frame = game.Track.FrameAt(gap.Start - 20d);
                SimSnapshot now = game.CaptureSnapshot();
                RacerState.Snapshot racer = now.Racer;
                racer.LastGroundedS = frame.S;
                racer.Heading = Mathf.Atan2(frame.Forward.x, frame.Forward.z) * Mathf.Rad2Deg;
                racer.Velocity = frame.Forward * player.SlingTopSpeed;
                racer.BoostZoneCarrySpeed = 0f;
                racer.Grounded = true;
                CircuitRace.Snapshot atKicker = now.Circuit;
                atKicker.LastS = frame.S;
                game.RestoreSnapshot(new SimSnapshot(now.Tick, racer, frame.Position + Vector3.up, atKicker));
                var bot = new CircuitBot(game, player, player.GetComponent<GrappleController>(), circuit);
                bool landed = false;
                for (int tick = 0; tick < 700 && !player.IsRespawning; tick++)
                {
                    game.StepTick(bot);
                    TrackCoord here = game.Track.Project(player.transform.position);
                    landed |= here.S >= gap.End && player.IsGrounded;
                }
                Assert.That(landed, Is.False, $"A {speed} slingshot without a pad must not clear the gap.");
                Assert.That(player.IsRespawning, Is.True);
                var pads = new System.Collections.Generic.List<double>();
                game.Track.GetBoostZones(pads);
                Assert.That(game.Track.Project(player.transform.position).S, Is.LessThan(pads[0]), "Recovery must offer the pad again.");
            }
            finally { Time.timeScale = scale; }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SummitDrop_RollbackAcrossShortcut_RepeatsEveryTickBitForBit()
        {
            TrackDefinition summit = SummitDrop();
            RaceLaunch.Request(new RaceSetup(summit, "SUMMIT DROP", "", RaceMode.Single, SpeedClass.Fast, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = TestRecords();
            assembler.Ghosts = ghosts;
            Scene scene = assembler.gameObject.scene;
            ChainRushGame game = Find<ChainRushGame>(scene);
            RunnerMotor player = Find<RunnerMotor>(scene);
            CircuitRace circuit = Find<CircuitRace>(scene);
            float scale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                game.StartRun();
                var bot = new ShortcutBot(game, player, summit);
                TrackDefinition.Shortcut shortcut = summit.Shortcuts[0];
                while (game.IsRunning && game.Tick < 10000 && circuit.LastS < shortcut.FromS - 20d) game.StepTick(bot);
                Assert.That(game.IsRunning, Is.True);
                Assert.That(game.Tick, Is.LessThan(10000));
                SimSnapshot snapshot = game.CaptureSnapshot();
                var log = new InputLog();
                StateTrace original = Trace(new InputRecorder(bot, log));
                int limit = game.Tick + 3000;
                while (game.IsRunning && game.Tick < limit && circuit.LastS < shortcut.ToS + 20d) game.StepTick(original);
                Assert.That(circuit.LastS, Is.GreaterThanOrEqualTo(shortcut.ToS + 20d));
                Assert.That(bot.Taken, Is.EqualTo(1));
                game.RestoreSnapshot(snapshot);
                LogAssert.Expect(LogType.Error, new Regex("came after \\d+; this run will keep no ghost"));
                StateTrace replay = Trace(new InputReplay(log));
                for (int i = 0; i < log.Count; i++) game.StepTick(replay);
                Assert.That(StateTrace.Compare(original, replay, log.Count), Is.Null);
                Assert.That(replay.Hash(log.Count), Is.EqualTo(original.Hash(log.Count)));
                Debug.Log($"SUMMIT DROP shortcut rollback: {log.Count} identical ticks, hash 0x{original.Hash(log.Count):X16}");
            }
            finally { Time.timeScale = scale; }

            StateTrace Trace(IInputSource input) => new StateTrace(game, player, input,
                ("lapProgress", () => circuit.Laps.Progress), ("checkpoints", () => circuit.Laps.CheckpointsPassed),
                ("lastGroundedS", () => game.Racer.LastGroundedS));
        }

        private static TrackDefinition SummitDrop()
        {
#if UNITY_EDITOR
            const string path = "Assets/_Project/Data/Circuit_SummitDrop.asset";
            var summit = AssetDatabase.LoadAssetAtPath<TrackDefinition>(path);
            Assert.That(summit, Is.Not.Null, path + " (menu: Create Summit Drop Track)");
            return summit;
#else
            Assert.Fail("Flow tests require the Unity Editor.");
            return null;
#endif
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator OpenCourse_DownhillBot_FinishesOnRunoutAndKeepsItsRecordAndGhost()
        {
            made = OpenDownhill();
            var store = TestRecords();
            store.Clear(made.name, SpeedClass.Fast);
            RaceLaunch.Request(new RaceSetup(made, "OPEN DOWNHILL", "", RaceMode.Single, SpeedClass.Fast, runner));
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
                Time.timeScale = 0f;
                Transform finish = circuit.transform.Find("Finish line"), start = circuit.transform.Find("Start line");
                Assert.That(finish, Is.Not.Null);
                Assert.That(start, Is.Not.Null);
                Assert.That(game.Track.Project(finish.position).S, Is.EqualTo(made.LapLength).Within(1e-3));
                Assert.That(game.Track.Project(start.position).S, Is.EqualTo(0d).Within(1e-3));
                game.StartRun();
                var bot = new CircuitBot(game, player, player.GetComponent<GrappleController>(), circuit);
                for (int tick = 0; tick < 3000 && game.IsRunning; tick++)
                {
                    game.StepTick(bot);
                    Assert.That(player.IsRespawning, Is.False, "The bot must drive the descent and jump without a fall.");
                }
                Assert.That(game.HasFinished, Is.True);
                Assert.That(circuit.Laps.CompletedLaps, Is.EqualTo(1));
                Assert.That(game.Progress, Is.EqualTo(1f));
                TrackCoord end = game.Track.Project(player.transform.position);
                Assert.That(end.S, Is.GreaterThanOrEqualTo(made.LapLength).And.LessThan(game.Track.EndS));
                Assert.That(player.IsGrounded, Is.True);
                Assert.That(player.transform.position.y, Is.LessThan(0f));
                Assert.That(circuit.TryGetNextGap(end.S, out _, out _, out _), Is.False, "There is no next lap's gap.");
                yield return null;
                Assert.That(assembler.HasLastFinish, Is.True);
                Assert.That(store.TryGet(made.name, SpeedClass.Fast, out BestRecords.Record best), Is.True);
                Assert.That(best.Ticks, Is.EqualTo(circuit.Laps.TotalTicks));
                Assert.That(ghosts.TryLoad(made.name, SpeedClass.Fast, out GhostTrace ghost), Is.True);
                Assert.That(ghost.Count, Is.EqualTo(best.Ticks));
                Assert.That(ghost[ghost.Count - 1].Position, Is.EqualTo(player.transform.position));

                // The game stops at the finish; moving the capsule farther proves the runout's physical end guard.
                CharacterController capsule = player.GetComponent<CharacterController>();
                for (int step = 0; step < 150; step++) capsule.Move(Vector3.forward * 0.5f + Vector3.down * 0.1f);
                end = game.Track.Project(player.transform.position);
                Assert.That(end.S, Is.GreaterThan(game.Track.EndS - 2d).And.LessThan(game.Track.EndS));
                Assert.That(end.H, Is.InRange(0.5f, 1.5f));
                Assert.That(capsule.isGrounded, Is.True);
            }
            finally
            {
                Time.timeScale = scale;
                store.Clear(made.name, SpeedClass.Fast);
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator OpenCourse_StartGuardAndFalls_StayOnRoadAndRespawnFacingForward()
        {
            made = OpenDownhill();
            RaceLaunch.Request(new RaceSetup(made, "OPEN DOWNHILL", "", RaceMode.Single, SpeedClass.Fast, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = TestRecords();
            assembler.Ghosts = ghosts;
            Scene scene = assembler.gameObject.scene;
            ChainRushGame game = Find<ChainRushGame>(scene);
            RunnerMotor player = Find<RunnerMotor>(scene);
            float scale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                game.StartRun();
                var reverseLog = new InputLog();
                for (int i = 0; i < 500; i++) reverseLog.Add(new TickInput(0f, false, false, false, coasting: true, reversing: true));
                var reverse = new InputReplay(reverseLog);
                for (int i = 0; i < reverseLog.Count; i++)
                {
                    game.StepTick(reverse);
                    Assert.That(player.IsRespawning, Is.False);
                }
                TrackCoord behind = game.Track.Project(player.transform.position);
                Assert.That(behind.S, Is.GreaterThan(-made.StartRunway).And.LessThan(-made.StartRunway + 2d));
                Assert.That(player.IsGrounded, Is.True);
                Assert.That(game.HasFinished, Is.False);
                Assert.That(game.Progress, Is.Zero);

                foreach ((double lastGrounded, double expected) in new[] { (1d, 0d), (37d, 26d), (110d, 82d) })
                {
                    SimSnapshot now = game.CaptureSnapshot();
                    RacerState.Snapshot racer = now.Racer;
                    racer.LastGroundedS = lastGrounded;
                    racer.Grounded = false;
                    racer.Velocity = Vector3.zero;
                    racer.RespawnHoldTicks = 0;
                    TrackFrame frame = game.Track.FrameAt(lastGrounded);
                    game.RestoreSnapshot(new SimSnapshot(now.Tick, racer, frame.Position + Vector3.down * 40f, now.Circuit));
                    game.StepTick(new InputReplay(new InputLog()));
                    Assert.That(player.IsRespawning, Is.True);
                    double back = game.Track.Project(player.transform.position).S;
                    Assert.That(back, Is.EqualTo(expected).Within(0.05d));
                    Assert.That(player.Heading, Is.EqualTo(0f).Within(1e-4f));
                    Assert.That(player.IsGrounded, Is.True);
                    Assert.That(game.Track.Project(player.transform.position).H, Is.InRange(0.5f, 1.5f));
                }
            }
            finally { Time.timeScale = scale; }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator OpenCourse_RollbackAcrossDownhillAndFinish_RepeatsEveryTickBitForBit()
        {
            made = OpenDownhill();
            RaceLaunch.Request(new RaceSetup(made, "OPEN DOWNHILL", "", RaceMode.Single, SpeedClass.Fast, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = TestRecords();
            assembler.Ghosts = ghosts;
            Scene scene = assembler.gameObject.scene;
            ChainRushGame game = Find<ChainRushGame>(scene);
            RunnerMotor player = Find<RunnerMotor>(scene);
            CircuitRace circuit = Find<CircuitRace>(scene);
            float scale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                game.StartRun();
                var bot = new CircuitBot(game, player, player.GetComponent<GrappleController>(), circuit);
                // Include a grounded downhill pose, a pad and the finish in the restored window.
                while (game.IsRunning && game.Tick < 3000 && circuit.LastS < 80d) game.StepTick(bot);
                Assert.That(game.IsRunning, Is.True);
                SimSnapshot snapshot = game.CaptureSnapshot();
                var log = new InputLog();
                StateTrace original = Trace(new InputRecorder(bot, log));
                for (int i = 0; i < 2000 && game.IsRunning; i++) game.StepTick(original);
                Assert.That(game.HasFinished, Is.True);
                Vector3 finishPosition = player.transform.position, finishVelocity = player.Velocity;
                int finishTick = game.Tick, totalTicks = circuit.Laps.TotalTicks;
                double progress = circuit.Laps.Progress;
                Assert.That(log.Count, Is.GreaterThan(30));
                game.RestoreSnapshot(snapshot);
                LogAssert.Expect(LogType.Error, new Regex("came after \\d+; this run will keep no ghost"));
                StateTrace replay = Trace(new InputReplay(log));
                for (int i = 0; i < log.Count; i++) game.StepTick(replay);
                Assert.That(StateTrace.Compare(original, replay, log.Count), Is.Null);
                Assert.That(replay.Hash(log.Count), Is.EqualTo(original.Hash(log.Count)));
                Assert.That(game.HasFinished, Is.True);
                Assert.That(game.Tick, Is.EqualTo(finishTick));
                Assert.That(circuit.Laps.TotalTicks, Is.EqualTo(totalTicks));
                Assert.That(circuit.Laps.Progress, Is.EqualTo(progress));
                Assert.That(player.transform.position, Is.EqualTo(finishPosition));
                Assert.That(player.Velocity, Is.EqualTo(finishVelocity));
                Debug.Log($"ChainRush open rollback: {log.Count} ticks identical through finish, hash 0x{original.Hash(log.Count):X16}.");
                game.ReturnToStart();
            }
            finally { Time.timeScale = scale; }

            StateTrace Trace(IInputSource input) => new StateTrace(game, player, input,
                ("lapProgress", () => circuit.Laps.Progress), ("checkpoints", () => circuit.Laps.CheckpointsPassed),
                ("lastGroundedS", () => game.Racer.LastGroundedS));
        }

        private static TrackDefinition OpenDownhill() => TrackDefinition.OpenCourse("TestOpenDownhill", new[]
        {
            TrackDefinition.Segment.Straight(30f), TrackDefinition.Segment.Gap(6f), TrackDefinition.Segment.Straight(30f),
            TrackDefinition.Segment.BoostStraight(30f, 20f, -0.15f), TrackDefinition.Segment.Straight(30f, 0f)
        });

        // ---- helpers -------------------------------------------------------------------------------------

        // Read the saved menu's very same tracks array as LobbyMenu.BuildSetup, without navigating its pages on every
        // lap (user 2026-11-02). A neutral runner keeps the existing 20 m/s baseline; track/title/environment/look are
        // the real entry's values. Lobby_TrackChoices_AreChosenWithTheirSavedSetup covers the menu path.
        internal static RaceSetup LobbyLapSetup(LobbyMenu menu, string title, SpeedClass speed, RunnerProfile runner)
        {
#if UNITY_EDITOR
            var data = new SerializedObject(menu);
            SerializedProperty entries = data.FindProperty("tracks");
            Assert.That(entries, Is.Not.Null, "LobbyMenu.tracks is the source of lap setups.");
            RaceSetup setup = default;
            int matches = 0;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("title").stringValue != title) continue;
                matches++;
                var track = (TrackDefinition)entry.FindPropertyRelative("definition").objectReferenceValue;
                string environment = entry.FindPropertyRelative("environmentScene").stringValue;
                var look = (TrackLook)entry.FindPropertyRelative("look").objectReferenceValue;
                Assert.That(track, Is.Not.Null, title);
                Assert.That(look, Is.Not.Null, title + " needs its road and walls.");
                Assert.That(track.TryValidate(out string error), Is.True, error);
                string suffix = title == "STADIUM" ? "Stadium" : title == "SKYWAY LOOP" ? "Skyway" :
                    title == "FINGER LANES" ? "FingerLanes" : title == "PALM COAST" ? "PalmCoast" : title == "SUMMIT DROP" ? "SummitDrop" : null;
                Assert.That(suffix, Is.Not.Null, "No expected lobby entry for " + title);
                Assert.That(track, Is.SameAs(AssetDatabase.LoadAssetAtPath<TrackDefinition>("Assets/_Project/Data/Circuit_" + suffix + ".asset")));
                Assert.That(environment, Is.EqualTo("ChainRushTrack" + suffix));
                Assert.That(look.name, Is.EqualTo("TrackLook_" + suffix));
                setup = new RaceSetup(track, entry.FindPropertyRelative("title").stringValue, environment,
                    RaceMode.Single, speed, runner, look);
            }
            Assert.That(matches, Is.EqualTo(1), title + " must be registered exactly once in the saved lobby.");
            return setup;
#else
            Assert.Fail("Lap tests require the Unity Editor.");
            return default;
#endif
        }

        internal static IEnumerator LoadLapRace(RaceSetup setup, BestRecords records, GhostFiles ghosts, Action<RaceAssembler> found)
        {
            // Keep the old Loop_ByName contract: the shipped scene list and name-based race load are exercised here.
            string environmentPath = "Assets/_Project/Scenes/" + setup.EnvironmentScene + ".unity";
            foreach (string path in new[] { TitlePath, LobbyPath, RacePath, environmentPath })
                Assert.That(SceneUtility.GetBuildIndexByScenePath(path), Is.GreaterThanOrEqualTo(0), "Not in the build's scene list: " + path);
            RaceLaunch.Request(setup);
            SceneManager.LoadScene(RaceLaunch.RaceScene);
            yield return null;
            Scene scene = SceneManager.GetActiveScene();
            RaceAssembler assembler = Find<RaceAssembler>(scene);
            assembler.Records = records;
            assembler.Ghosts = ghosts;
            float deadline = Time.realtimeSinceStartup + 20f;
            while (assembler.IsAssembling && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(assembler.IsAssembling, Is.False, "The decoration never finished loading: " + setup.Title);
            Assert.That(assembler.IsLaunched, Is.True);
            Assert.That(Find<ChainRushGame>(scene).IsCountingDown, Is.True);
            Assert.That(Find<ChainRushHud>(scene).Heading, Is.EqualTo(setup.Title));
            Assert.That(Find<RunnerMotor>(scene).SpeedClassScale, Is.EqualTo(SpeedScale.Of(setup.Speed)));
            CircuitRace circuit = Find<CircuitRace>(scene);
            Assert.That(circuit.Definition, Is.SameAs(setup.Track));
            AssertLaysTheLook(circuit, setup.Look);
            Scene environment = SceneManager.GetSceneByName(setup.EnvironmentScene);
            Assert.That(environment.isLoaded, Is.True);
            Assert.That(environment.rootCount, Is.EqualTo(1), "Only the exterior prefab belongs in the decoration scene.");
            GameObject exterior = environment.GetRootGameObjects()[0];
            if (setup.Title == "STADIUM") Assert.That(exterior.name, Is.EqualTo("StadiumExterior"));
            Assert.That(exterior.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
            Assert.That(exterior.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(exterior.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(exterior.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
            found(assembler);
        }

        internal static IEnumerator LeaveLapRace(RaceAssembler assembler, string environment)
        {
            assembler.ReturnToLobby();
            float deadline = Time.realtimeSinceStartup + 10f;
            while ((SceneManager.GetActiveScene().name != RaceLaunch.LobbyScene || SceneManager.GetSceneByName(environment).isLoaded) &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(RaceLaunch.LobbyScene));
            Assert.That(SceneManager.GetSceneByName(environment).isLoaded, Is.False, "The exterior must leave with the race.");
        }

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
