using System;
using System.Collections;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Audio;
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

        private const string TestTitle = "TEST TRACK";

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
            foreach (string title in new[] { null, "", "  " })
                Assert.Throws<ArgumentException>(() => new RaceSetup(made, title, "", RaceMode.Single, SpeedClass.Fast, runner), "title '" + title + "'");
            Assert.That(new RaceSetup(made, TestTitle, "", RaceMode.Single, SpeedClass.Fast, runner).Title, Is.EqualTo(TestTitle));
        }

        [Test]
        public void RaceLaunch_SecondRequestBeforeTaking_ThrowsAndTakingEmptiesIt()
        {
            made = ScriptableObject.CreateInstance<TrackDefinition>();
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
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Speed));
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

        // The speed classes opened with M3 (DECISIONS 2026-10-25 "M3 속도 단계"): each one goes on to the runner page and into the setup.
        [UnityTest]
        public IEnumerator Lobby_SpeedClasses_AreAllOpen_AndTheChosenOneGoesIntoTheSetup()
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            menu.Confirm();
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Speed));
            Assert.That(menu.BuildSetup().Speed, Is.EqualTo(SpeedClass.Fast), "Fast until another is chosen.");
            menu.Move(-1);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Runner), "Normal is open.");
            Assert.That(menu.Notice, Is.Empty);
            Assert.That(menu.BuildSetup().Speed, Is.EqualTo(SpeedClass.Normal));
            menu.Back();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Speed));
            menu.Move(2);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Runner), "Very fast is open.");
            Assert.That(menu.BuildSetup().Speed, Is.EqualTo(SpeedClass.VeryFast));
        }

        // The tracks still to come are listed after the scene's tracks, locked (user 2026-10-21; the skyway took the
        // second placeholder's place the same day, DECISIONS "SKYWAY LOOP"; Finger Lanes came before the last one,
        // DECISIONS 2026-10-25 "FINGER LANES").
        [UnityTest]
        public IEnumerator Lobby_PlaceholderTracks_RefuseWithANoticeAndStay()
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Track));
            menu.Highlight(3);
            Assert.That(menu.TrackRecordText(3), Is.EqualTo("준비 중"));
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Track), "The placeholder track is locked.");
            Assert.That(menu.Notice, Is.Not.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => menu.Highlight(4), "The stadium, the skyway, Finger Lanes and one placeholder.");
            menu.Move(1);
            Assert.That(menu.Cursor, Is.EqualTo(0), "The cursor wraps from the last placeholder to the stadium.");
            Assert.That(menu.BuildSetup().Track, Is.SameAs(Stadium()), "A refused track is not chosen.");
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Speed));
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
                Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Track));
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

        // The main, track and speed pages describe every entry, each differently; settings has no description panel.
        [UnityTest]
        public IEnumerator Lobby_MainTrackAndSpeedEntries_EachHaveADescription()
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            foreach (LobbyMenu.Page expected in new[] { LobbyMenu.Page.Main, LobbyMenu.Page.Track, LobbyMenu.Page.Speed })
            {
                Assert.That(menu.CurrentPage, Is.EqualTo(expected));
                var seen = new System.Collections.Generic.HashSet<string>();
                for (int row = 0; row < 3; row++)
                {
                    menu.Highlight(row);
                    Assert.That(menu.Description, Is.Not.Empty, $"{expected} row {row}");
                    Assert.That(seen.Add(menu.Description), Is.True, $"{expected} row {row} repeats another row's description.");
                }
                // Main: single race (0); track: the stadium (0); speed: fast (1), the open ones.
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
            RaceLaunch.Request(new RaceSetup(Stadium(), "STADIUM", "", RaceMode.Single, SpeedClass.Fast, runner));
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

        // ---- the skyway (DECISIONS 2026-10-21 "SKYWAY LOOP") -----------------------------------------------

        [UnityTest]
        public IEnumerator Lobby_SkywayTrack_IsChosenWithItsEnvironment()
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            menu.Confirm();
            menu.Highlight(1);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Speed), "The skyway can be raced.");
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
            menu.Highlight(1);
            menu.Confirm();
            RaceSetup setup = menu.BuildSetup();
            Assert.That(setup.Track, Is.SameAs(skyway));
            Assert.That(setup.EnvironmentScene, Is.EqualTo(SkywayEnvironmentScene));
            RaceLaunch.Request(setup);
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = store;
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
            [Values("STADIUM", "SKYWAY LOOP", "FINGER LANES")] string title)
        {
            var store = TestRecords();
            TrackDefinition track = title == "STADIUM" ? Stadium() : title == "SKYWAY LOOP" ? Skyway() : FingerLanes();
            store.Clear(track.name, speed);
            RaceLaunch.Request(new RaceSetup(track, title, "", RaceMode.Single, speed, runner));
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = store;
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
        // tower narrows the road to 8 m. The road is 1.2 times the usual; two bumps 0.5 m high (user 2026-10-25: a bump, not
        // a climb) whose tops are kickers throwing by speed, the stone bridge's (lip 0.3) then the clock tower exit's (lip
        // 0.35). No gaps, no anchors, no extra poles; two laps of 1466 m (plan A, user 2026-10-25).
        [Test]
        public void FingerLanes_Layout_IsSevenHalfTurnFingersAWideRoadAndTwoKickers()
        {
            TrackDefinition finger = FingerLanes();
            Assert.That(finger.TryValidate(out string error), Is.True, error);
            Assert.That(finger.LapCount, Is.EqualTo(2));
            Assert.That(finger.LapLength, Is.EqualTo(822d + 7d * Math.PI * 25d + Math.PI * 30d).Within(0.01d));
            var turns = new System.Collections.Generic.List<TrackDefinition.Segment>();
            float total = 0f;
            foreach (TrackDefinition.Segment segment in finger.Segments)
            {
                if (!segment.IsArc) continue;
                turns.Add(segment);
                total += segment.Degrees;
            }
            Assert.That(turns.Count, Is.EqualTo(9));
            for (int i = 0; i < 7; i++)
            {
                Assert.That(turns[i].Radius, Is.EqualTo(25f), "finger turn " + i);
                Assert.That(turns[i].Degrees, Is.EqualTo(i % 2 == 0 ? 180f : -180f), "finger turn " + i + " alternates, right first");
            }
            for (int i = 7; i < 9; i++)
            {
                Assert.That(turns[i].Radius, Is.EqualTo(30f), "corner " + i);
                Assert.That(turns[i].Degrees, Is.EqualTo(90f), "corner " + i);
            }
            Assert.That(total, Is.EqualTo(360f));
            float narrowest = float.MaxValue;
            for (double s = 0d; s < finger.LapLength; s += 1d) narrowest = Mathf.Min(narrowest, finger.HalfWidthAt(s));
            Assert.That(narrowest, Is.EqualTo(4f).Within(1e-4f), "The clock tower's entrance.");
            Assert.That(finger.RoadHalfWidth, Is.EqualTo(RoadProfile.DeckHalfWidth * 1.2f).Within(1e-4f));
            Assert.That(finger.HalfWidthAt(0d), Is.EqualTo(finger.RoadHalfWidth), "The start line has the track's road.");
            var launches = new System.Collections.Generic.List<float>();
            foreach (TrackDefinition.Segment segment in finger.Segments)
                if (segment.Launch > 0f) launches.Add(segment.Launch);
            Assert.That(launches, Is.EqualTo(new[] { 0.3f, 0.35f }), "The bridge, then the clock tower.");
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
            menu.Highlight(2);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Speed), "Finger Lanes can be raced.");
            RaceSetup setup = menu.BuildSetup();
            Assert.That(setup.Track, Is.SameAs(finger));
            Assert.That(setup.Title, Is.EqualTo("FINGER LANES"));
            Assert.That(setup.EnvironmentScene, Is.EqualTo(FingerLanesEnvironmentScene));
            RaceLaunch.Request(setup);
            RaceAssembler assembler = null;
            yield return Load(RacePath, scene => assembler = Find<RaceAssembler>(scene));
            assembler.Records = store;
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
