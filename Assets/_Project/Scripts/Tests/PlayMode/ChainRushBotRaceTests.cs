using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Bots;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Flow;
using ProtoHarness.ChainRush.Flow.Lobby;
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
    // MP2 A: real isolated worlds, with snapshot fixtures just before a finish so the final-tick contract is exact.
    public sealed class ChainRushBotRaceTests
    {
        private const string RacePath = "Assets/_Project/Scenes/ChainRushCircuit.unity";
        private const string LobbyPath = "Assets/_Project/Scenes/ChainRushLobby.unity";
        private const string StadiumPath = "Assets/_Project/Data/Circuit_Stadium.asset";
        private float timeScale;
        private float listenerVolume;
        private BestRecords records;
        private GhostFiles ghosts;
        private RaceWorld player;
        private RaceAssembler assembler;
        private ChainRushHud hud;
        private RaceField Field => assembler.Field;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            timeScale = Time.timeScale;
            listenerVolume = AudioListener.volume;
            AudioListener.volume = 0f;
            RaceLaunch.Clear();
            records = new BestRecords("ChainRush.Test.BotRaceTests.");
            ghosts = new GhostFiles(Path.Combine(Path.GetTempPath(), "ProtoHarnessTests", "BotRaceGhosts"));
            foreach (SpeedClass speed in Enum.GetValues(typeof(SpeedClass))) records.Clear("Circuit_Stadium", speed);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (player != null) player.Game.SetInputSource(Idle());
            if (assembler != null && assembler.Field != null)
                foreach (RaceWorld world in Field.Opponents) world.Game.SetInputSource(Idle());
            Time.timeScale = timeScale;
            AudioListener.volume = listenerVolume;
            RaceLaunch.Clear();
            foreach (SpeedClass speed in Enum.GetValues(typeof(SpeedClass))) records.Clear("Circuit_Stadium", speed);
            if (Directory.Exists(ghosts.Folder)) Directory.Delete(ghosts.Folder, true);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(1, "1st"), TestCase(2, "2nd"), TestCase(3, "3rd"), TestCase(4, "4th")]
        [TestCase(11, "11th"), TestCase(12, "12th"), TestCase(13, "13th"), TestCase(21, "21st")]
        [TestCase(22, "22nd"), TestCase(23, "23rd"), TestCase(111, "111th"), TestCase(112, "112th"), TestCase(113, "113th")]
        public void Ordinal_APlace_UsesTheEnglishSuffix(int place, string expected)
        {
            Assert.That(ChainRushHud.Ordinal(place), Is.EqualTo(expected));
        }

        // User 2026-11-13: gold, silver, bronze, each shining less than the one above; 4th on plain white, not shining.
        [Test]
        public void PlaceLook_GoldSilverBronzeShineLessInTurn_FourthOnIsPlainWhite()
        {
            Assert.That(ChainRushHud.PlaceShine(1), Is.GreaterThan(ChainRushHud.PlaceShine(2)));
            Assert.That(ChainRushHud.PlaceShine(2), Is.GreaterThan(ChainRushHud.PlaceShine(3)));
            Assert.That(ChainRushHud.PlaceShine(3), Is.GreaterThan(0f));
            for (int place = 4; place <= 8; place++)
            {
                Assert.That(ChainRushHud.PlaceShine(place), Is.EqualTo(0f), $"place {place}");
                Assert.That(ChainRushHud.PlaceTint(place), Is.EqualTo(Color.white), $"place {place}");
            }
            Color gold = ChainRushHud.PlaceTint(1), silver = ChainRushHud.PlaceTint(2), bronze = ChainRushHud.PlaceTint(3);
            Assert.That(gold.r > gold.b && gold.g > gold.b, Is.True, "Gold is warm yellow.");
            Assert.That(Mathf.Abs(silver.r - silver.b) < 0.15f && silver.r < 0.95f, Is.True, "Silver is a light grey.");
            Assert.That(bronze.r > bronze.g && bronze.g > bronze.b && bronze.r < gold.r, Is.True, "Bronze is a darker orange-brown.");
            Assert.Throws<ArgumentOutOfRangeException>(() => ChainRushHud.PlaceTint(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChainRushHud.PlaceShine(0));
        }

        [Test]
        public void RaceSetup_OnlyBotsTakeNonemptyOpponents_AndKeepTheSeed()
        {
            RaceSetup setup = Setup();
            Assert.Throws<ArgumentException>(() => setup.WithOpponents());
            RaceSetup race = setup.WithOpponents(Profile("Beta"));
            Assert.That(race.Seed, Is.EqualTo(13579));
            Assert.That(race.ForOpponent(0).Seed, Is.EqualTo(race.Seed));
            var single = new RaceSetup(setup.Track, setup.Title, "", RaceMode.Single, setup.Speed, setup.Runner);
            Assert.Throws<ArgumentException>(() => single.WithOpponents(Profile("Beta")));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChainRushHud.Ordinal(0));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Lobby_BotsThroughSharedPages_LoadsThreeSeededBotsExcludingThePlayer()
        {
            yield return LoadScene(LobbyPath);
            LobbyMenu menu = Find<LobbyMenu>(SceneManager.GetActiveScene());
            // Single race, then bots on the mode page (user 2026-11-13: time attack and bots inside the single race).
            menu.Highlight(MainPage.SingleRow);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Mode));
            menu.Highlight(SingleModePage.BotsRow);
            Assert.That(menu.Description, Does.Contain("봇 3명"));
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Speed));
            menu.Back();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Mode));
            Assert.That(menu.Cursor, Is.EqualTo(SingleModePage.BotsRow));
            menu.Confirm();
            menu.Highlight(0);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Track));
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Runner));
            RaceSetup first = menu.BuildSetup(), repeated = menu.BuildSetup();
            Assert.That(first.Mode, Is.EqualTo(RaceMode.Bots));
            Assert.That(first.OpponentCount, Is.EqualTo(3));
            Assert.That(first.Seed, Is.EqualTo(repeated.Seed));
            var picked = new HashSet<RunnerProfile>();
            for (int i = 0; i < 3; i++)
            {
                Assert.That(first.Opponent(i), Is.Not.SameAs(first.Runner));
                Assert.That(first.Opponent(i), Is.SameAs(repeated.Opponent(i)));
                Assert.That(picked.Add(first.Opponent(i)), Is.True);
            }
            // Confirm the real lobby launch, then use the same setup for all checks.
            menu.Confirm();
            // The lobby loads the race scene on a later frame (LobbyMenu: SceneManager.LoadScene); wait for it, or the
            // lobby would be gathered as the race and the load would land in the next test.
            float loadDeadline = Time.realtimeSinceStartup + 30f;
            while (SceneManager.GetActiveScene().path != RacePath && Time.realtimeSinceStartup < loadDeadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(RacePath), "The lobby did not load the race scene.");
            yield return GatherRace();
            Assert.That(Field.RacerCount, Is.EqualTo(4));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(Field.OpponentInput(i), Is.TypeOf<CircuitBot>());
                Assert.That(Field.Opponents[i].Player.Profile, Is.SameAs(first.Opponent(i)));
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Race_BotsWithoutOpponents_RefusesAssemblyLoudly()
        {
            LogAssert.Expect(LogType.Error, "RaceAssembler: a bot race needs opponents. The race is not run.");
            RaceLaunch.Request(Setup());
            yield return LoadScene(RacePath);
            assembler = Find<RaceAssembler>(SceneManager.GetActiveScene());
            Assert.That(assembler.enabled, Is.False);
            Assert.That(assembler.Field, Is.Null);
            Assert.That(Find<ChainRushGame>(SceneManager.GetActiveScene()).enabled, Is.False);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Field_KeyboardOrMissingOpponentInput_ThrowsBeforeTakingTheWorlds()
        {
            yield return LoadRace();
            RaceField extra = player.Game.gameObject.AddComponent<RaceField>();
            try
            {
                IInputSource[] inputs = { Idle(), new KeyboardMouseInputSource(), Idle() };
                Assert.Throws<ArgumentException>(() => extra.Initialize(player.Game, Field.Opponents, inputs));
                inputs[1] = null;
                Assert.Throws<ArgumentException>(() => extra.Initialize(player.Game, Field.Opponents, inputs));
                Assert.Throws<ArgumentException>(() => extra.Initialize(player.Game, Field.Opponents, new IInputSource[0]));
                Assert.Throws<ArgumentNullException>(() => extra.Initialize(player.Game, Field.Opponents, null));
            }
            finally { UnityEngine.Object.DestroyImmediate(extra); }
            // Destroying an uninitialized field must leave the existing field in control.
            Assert.That(player.Game.SelfStepping, Is.False);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Field_BotFinishesFirst_PlayerDisplaysSecondAndResultFreezesEveryRunner()
        {
            yield return LoadRace();
            yield return StartField();
            ArmFinish(Field.Opponents[0], 2d);
            SetProgress(player, 500d);
            SetProgress(Field.Opponents[1], 300d);
            SetProgress(Field.Opponents[2], 200d);
            yield return StepField();
            Assert.That(Field.FinishTick(1), Is.GreaterThan(0));
            Assert.That(Field.HasResult, Is.False);
            Assert.That(Field.Place(0), Is.EqualTo(2));
            Assert.That(hud.BotPlaceText, Is.EqualTo("2nd"));
            int botFinish = Field.FinishTick(1);
            ArmFinish(player, 1d);
            yield return StepField();
            Assert.That(Field.HasResult, Is.True);
            Assert.That(Field.FinishTick(0), Is.GreaterThan(botFinish));
            Assert.That(new[] { Field.Place(0), Field.Place(1), Field.Place(2), Field.Place(3) }, Is.EqualTo(new[] { 2, 1, 3, 4 }));
            Assert.That(hud.BotResultCount, Is.EqualTo(4));
            Assert.That(hud.BotResultRow(0), Does.StartWith("1st").And.Contain(Field.RunnerName(1)));
            Assert.That(hud.BotResultRow(1), Does.StartWith("2nd").And.Contain(Field.RunnerName(0)));
            Assert.That(hud.BotResultRow(2), Does.Contain("진행 중").And.Contain(" m"));
            Assert.That(hud.BotResultRow(3), Does.Contain("진행 중").And.Contain(" m"));
            int finalTick = player.Game.Tick;
            double frozen = Field.Progress(2);
            yield return StepField();
            Assert.That(player.Game.Tick, Is.EqualTo(finalTick));
            Assert.That(Field.Progress(2), Is.EqualTo(frozen));
            Assert.That(Field.Opponents[1].Game.Tick, Is.EqualTo(finalTick));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Field_PlayerAndBotFinishOnTheSameTick_ShareFirstAfterEveryWorldSteps()
        {
            yield return LoadRace();
            yield return StartField();
            ArmFinish(player, 1d);
            ArmFinish(Field.Opponents[0], 10d);
            bool sawPlayerFinish = false;
            bool prematurelyFrozen = false;
            Action<int> observe = _ => { sawPlayerFinish = player.Game.HasFinished; prematurelyFrozen = Field.HasResult; };
            player.Game.TickCompleted += observe;
            try { yield return StepField(); }
            finally { player.Game.TickCompleted -= observe; }
            Assert.That(sawPlayerFinish, Is.True);
            Assert.That(prematurelyFrozen, Is.False);
            Assert.That(Field.HasResult, Is.True);
            Assert.That(Field.FinishTick(1), Is.EqualTo(Field.FinishTick(0)));
            Assert.That(Field.Place(0), Is.EqualTo(1));
            Assert.That(Field.Place(1), Is.EqualTo(1));
            Assert.That(Field.Place(2), Is.GreaterThanOrEqualTo(3));
            Assert.That(hud.BotResultRow(0), Does.StartWith("1st"));
            Assert.That(hud.BotResultRow(1), Does.StartWith("1st"));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Field_BotPassesThePlayerOnTheFinishTick_ResultUsesItsLastTickDistance()
        {
            yield return LoadRace();
            yield return StartField();
            SetProgress(Field.Opponents[1], 100d);
            SetProgress(Field.Opponents[2], 200d);
            ArmFinish(player, 1d);
            // This is inside the player's tick, before the opponent's final tick. The field must collect after it.
            Action<int> pass = _ => SetProgress(Field.Opponents[1], 400d);
            player.Game.TickCompleted += pass;
            try { yield return StepField(); }
            finally { player.Game.TickCompleted -= pass; }
            Assert.That(Field.Place(2), Is.LessThan(Field.Place(3)));
            Assert.That(Field.Progress(2), Is.GreaterThan(Field.Progress(3)));
            Assert.That(Field.Opponents[1].Game.Tick, Is.EqualTo(player.Game.Tick));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Race_BotFinishAndRestart_KeepNoRecordOrGhostAndResetStandingsWithTheSameLineup()
        {
            yield return LoadRace();
            int seed = assembler.Setup.Seed;
            var names = new[] { Field.RunnerName(1), Field.RunnerName(2), Field.RunnerName(3) };
            yield return StartField();
            ArmFinish(player, 1d);
            yield return StepField();
            Assert.That(Field.HasResult, Is.True);
            Assert.That(records.TryGet("Circuit_Stadium", SpeedClass.Normal, out _), Is.False);
            Assert.That(Directory.Exists(ghosts.Folder), Is.False, "No ghost directory may be created by a bot finish.");
            Assert.That(assembler.HasGhost || assembler.HasLastFinish || assembler.LastFinishWasBest, Is.False);
            player.Game.StartCountdown();
            yield return null;
            yield return null;
            Assert.That(Field.HasResult, Is.False);
            for (int i = 0; i < 4; i++)
            {
                Assert.That(Field.Place(i), Is.EqualTo(0));
                Assert.That(Field.FinishTick(i), Is.EqualTo(0));
                Assert.That(Field.Progress(i), Is.EqualTo(0d));
            }
            Assert.That(assembler.Setup.Seed, Is.EqualTo(seed));
            for (int i = 0; i < 3; i++) Assert.That(Field.RunnerName(i + 1), Is.EqualTo(names[i]));
            yield return StartField();
            Assert.That(Field.HasResult, Is.False);
            for (int i = 0; i < 4; i++) Assert.That(Field.FinishTick(i), Is.EqualTo(0));
        }

        private static IInputSource Idle() => new InputReplay(new InputLog());

        private static RunnerProfile Profile(string name)
        {
#if UNITY_EDITOR
            var profile = AssetDatabase.LoadAssetAtPath<RunnerProfile>("Assets/_Project/Data/RunnerProfile_" + name + ".asset");
            Assert.That(profile, Is.Not.Null);
            return profile;
#else
            Assert.Fail("Bot race tests require the Unity Editor.");
            return null;
#endif
        }

        private static RaceSetup Setup()
        {
#if UNITY_EDITOR
            var stadium = AssetDatabase.LoadAssetAtPath<TrackDefinition>(StadiumPath);
            Assert.That(stadium, Is.Not.Null);
            return new RaceSetup(stadium, "STADIUM", "", RaceMode.Bots, SpeedClass.Normal, Profile("Alpha"), seed: 13579);
#else
            Assert.Fail("Bot race tests require the Unity Editor.");
            return default;
#endif
        }

        private IEnumerator LoadRace()
        {
            RaceLaunch.Request(Setup().WithOpponents(Profile("Beta"), Profile("Theta"), Profile("Delta")));
            yield return LoadScene(RacePath);
            yield return GatherRace();
        }

        private IEnumerator GatherRace()
        {
            Scene scene = SceneManager.GetActiveScene();
            player = RaceWorld.FromScene(scene);
            assembler = Find<RaceAssembler>(scene);
            assembler.Records = records;
            assembler.Ghosts = ghosts;
            float deadline = Time.realtimeSinceStartup + 60f;
            while (assembler.IsAssembling && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(assembler.IsAssembling, Is.False);
            Assert.That(assembler.enabled, Is.True);
            Assert.That(Field, Is.Not.Null);
            hud = Find<ChainRushHud>(scene);
            Time.timeScale = 0f;
            yield return null;
        }

        private static IEnumerator LoadScene(string path)
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("Bot race tests require the Unity Editor.");
            yield break;
#endif
        }

        private IEnumerator StartField()
        {
            player.Game.SetInputSource(Idle());
            foreach (RaceWorld world in Field.Opponents) world.Game.SetInputSource(Idle());
            player.Game.StartRun();
            yield return StepField();
            Assert.That(player.Game.Tick, Is.GreaterThan(0));
        }

        private IEnumerator StepField()
        {
            Time.timeScale = 1f;
            yield return new WaitForFixedUpdate();
            Time.timeScale = 0f;
            yield return null;
        }

        private static void SetProgress(RaceWorld world, double metres)
        {
            SimSnapshot snapshot = world.Game.CaptureSnapshot();
            CircuitRace.Snapshot circuit = snapshot.Circuit;
            circuit.Laps.Progress = metres;
            world.Game.RestoreSnapshot(new SimSnapshot(snapshot.Tick, snapshot.Racer, snapshot.Position, circuit));
        }

        private static void ArmFinish(RaceWorld world, double beyond)
        {
            SimSnapshot snapshot = world.Game.CaptureSnapshot();
            CircuitRace.Snapshot circuit = snapshot.Circuit;
            LapCounter laps = world.Circuit.Laps;
            circuit.Laps.Progress = laps.LapLength * laps.LapCount + beyond;
            circuit.Laps.Completed = laps.LapCount - 1;
            circuit.Laps.NextCheckpoint = laps.CheckpointCount;
            for (int i = 0; i < laps.LapCount - 1; i++) circuit.Laps.LapEndTicks[i] = snapshot.Tick;
            world.Game.RestoreSnapshot(new SimSnapshot(snapshot.Tick, snapshot.Racer, snapshot.Position, circuit));
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            Assert.Fail($"The scene '{scene.name}' has no {typeof(T).Name}.");
            return null;
        }
    }
}
