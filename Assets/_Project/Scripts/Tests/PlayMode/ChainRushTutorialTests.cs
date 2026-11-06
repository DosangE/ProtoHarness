using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Flow;
using ProtoHarness.ChainRush.Flow.Lobby;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    // The tutorial on the real race scene (DECISIONS 2026-11-06 "튜토리얼"): the lobby opens it at the chosen stage, a bot
    // clears every stage with real physics, the last one ends in the popup that goes back to the lobby, ESC's menu leaves
    // and restarts it, and the map has nothing of a race: no start line, no laps, no finish.
    public sealed class ChainRushTutorialTests
    {
        private const string LobbyPath = "Assets/_Project/Scenes/ChainRushLobby.unity";
        private const string RacePath = "Assets/_Project/Scenes/ChainRushCircuit.unity";
        private const float RunnerHeight = 1.05f;

        private RunnerProfile runner;
        private float timeScale;

        [SetUp]
        public void SetUp()
        {
            RaceLaunch.Clear();
            timeScale = Time.timeScale;
            runner = ScriptableObject.CreateInstance<RunnerProfile>();
            runner.name = "TestRunner";
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = timeScale;
            RaceLaunch.Clear();
            if (runner != null) UnityEngine.Object.Destroy(runner);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // ---- the way in ----------------------------------------------------------------------------------

        // Through the lobby's real Confirm, so the ring made there has to live through the scene load.
        [UnityTest]
        public IEnumerator Lobby_TutorialRow_OpensItsStagesAndStartsTheChosenOne()
        {
            LobbyMenu menu = null;
            yield return Load(LobbyPath, scene => menu = Find<LobbyMenu>(scene));
            menu.Highlight(MainPage.TutorialRow);
            Assert.That(menu.Description, Is.Not.Empty);
            menu.Confirm();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Tutorial));
            for (int row = 0; row < TutorialCourse.Stages.Count; row++)
            {
                menu.Highlight(row);
                Assert.That(menu.Description, Is.EqualTo(TutorialCourse.Stages[row].Summary));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => menu.Highlight(TutorialCourse.Stages.Count));
            menu.Back();
            Assert.That(menu.CurrentPage, Is.EqualTo(LobbyMenu.Page.Main));
            Assert.That(menu.Cursor, Is.EqualTo(MainPage.TutorialRow), "Back lands on the row it came from.");
            menu.Confirm();
            menu.Highlight(3);
            menu.Confirm();
            float deadline = Time.realtimeSinceStartup + 20f;
            while (SceneManager.GetActiveScene().name != RaceLaunch.RaceScene && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Scene race = SceneManager.GetActiveScene();
            Assert.That(race.name, Is.EqualTo(RaceLaunch.RaceScene));
            RaceAssembler assembler = Find<RaceAssembler>(race);
            CircuitRace circuit = Find<CircuitRace>(race);
            Assert.That(circuit.Definition, Is.Not.Null, "The ring lived through the scene load.");
            Assert.That(circuit.IsArena, Is.True);
            Assert.That(assembler.Tutorial, Is.Not.Null);
            Assert.That(assembler.Tutorial.StageIndex, Is.EqualTo(3), "It starts at the chosen stage.");
            Assert.That(Find<RunnerMotor>(race).SpeedClassScale, Is.EqualTo(SpeedScale.Of(TutorialCourse.Speed)));
            Assert.That(Find<ChainRushHud>(race).Heading, Is.EqualTo(TutorialCourse.Title));
            Assert.That(assembler.Tutorial.IsBriefing, Is.True);
            Assert.That(Find<ChainRushGame>(race).IsReady, Is.True);
            Assert.That(Find<ChainRushGame>(race).IsCountingDown, Is.False);
            Assert.That(assembler.HasGhost, Is.False, "A tutorial has no ghost.");
        }

        // ---- the stages ----------------------------------------------------------------------------------

        // Real physics from the first stage to the popup: every goal can be done, on a floor of many pieces and with the
        // turn radius the circuit has. Then the popup's way out.
        [UnityTest]
        public IEnumerator Stages_ABotClearsThemAll_ThenThePopupGoesBackToTheLobby()
        {
            Race race = null;
            yield return Start(0, made => race = made);
            race.Game.SetInputSource(new TutorialBot(race.Game, race.Player, race.Director));
            Time.timeScale = 6f;
            float deadline = Time.realtimeSinceStartup + 120f;
            while (!race.Director.IsComplete && Time.realtimeSinceStartup < deadline)
            {
                if (race.Director.IsBriefing) race.Director.CloseBriefing();
                yield return null;
            }
            Assert.That(race.Director.IsComplete, Is.True,
                $"Stuck on stage {race.Director.StageIndex} goal {race.Director.GoalIndex}: {race.Director.CurrentGoal.Text} at {race.Player.transform.position}");
            Assert.That(race.Game.HasFinished, Is.True, "The run stops for the popup.");
            Assert.That(race.Game.ManualStart, Is.False, "Enter closes the popup; it must not start a run.");
            race.Assembler.ReturnToLobby();
            yield return WaitForScene(RaceLaunch.LobbyScene);
        }

        [UnityTest]
        public IEnumerator Stages_TheBoosterStageStartsWithZeroGauge()
        {
            Race race = null;
            yield return Start(4, made => race = made);
            Assert.That(race.Player.Gauge, Is.Zero);
            yield return StartStage(race);
            Assert.That(race.Player.Gauge, Is.Zero);
            Assert.That(race.Director.CurrentGoal.Kind, Is.EqualTo(TutorialCourse.GoalKind.Gauge));
        }

        [UnityTest]
        public IEnumerator Booster_EmptyGaugeShowsRefillInstructions_UntilRefilled()
        {
            Race race = null;
            yield return Start(4, made => race = made);
            race.Game.SetInputSource(new TutorialBot(race.Game, race.Player, race.Director, useBoost: false));
            yield return StartStage(race);
            float deadline = Time.realtimeSinceStartup + 30f;
            while (race.Director.CurrentGoal.Kind != TutorialCourse.GoalKind.Boost && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(race.Director.CurrentGoal.Kind, Is.EqualTo(TutorialCourse.GoalKind.Boost), "The bot filled the gauge and released the rope.");
            Time.timeScale = 0f;
            Assert.That(race.Player.IsSwinging, Is.False);
            Assert.That(race.Game.Racer.SlingTicks, Is.Zero);
            // Fill to the cap before spending to remove any fractional gauge earned during the release.
            race.Game.Racer.AddGauge(RacerState.MaxGauge);
            for (int slot = 0; slot < (int)RacerState.MaxGauge; slot++)
                Assert.That(race.Game.Racer.TrySpendGaugeSlot(), Is.True);
            Assert.That(race.Player.Gauge, Is.Zero);
            Assert.That(race.Director.GoalText, Is.EqualTo("게이지가 없습니다.\n봉에서 스윙해 다시 채우세요."));
            race.Game.Racer.AddGauge(1f);
            Assert.That(race.Director.GoalText, Is.EqualTo(race.Director.CurrentGoal.Text));
            Assert.That(race.Director.CurrentGoal.Kind, Is.EqualTo(TutorialCourse.GoalKind.Boost), "Guidance does not change goal order.");
        }

        [UnityTest]
        public IEnumerator Briefing_WaitsAtTheStartWithoutTicks_ThenRunsImmediatelyFromTheFirstGoal()
        {
            Race race = null;
            yield return Start(0, made => race = made);
            Assert.That(race.Director.IsBriefing, Is.True);
            Assert.That(race.Game.IsReady, Is.True);
            Assert.That(race.Game.ManualStart, Is.False);
            Assert.That(race.Game.Tick, Is.Zero);
            Vector3 spawn = race.Player.transform.position;
            Assert.That(new Vector2(spawn.x, spawn.z).magnitude, Is.LessThan(0.05f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(race.Player.Heading, 0f)), Is.LessThan(0.1f));
            race.Game.SetInputSource(new Throttle());
            // A supplied driving input cannot advance simulation while the popup is open.
            race.Game.StepTick(new Throttle());
            yield return new WaitForSeconds(ChainRushGame.CountdownSeconds + 0.2f);
            Assert.That(race.Director.IsBriefing, Is.True);
            Assert.That(race.Game.IsReady, Is.True);
            Assert.That(race.Game.Tick, Is.Zero);
            Assert.That(Vector3.Distance(race.Player.transform.position, spawn), Is.LessThan(0.01f));
            race.Director.CloseBriefing();
            Assert.That(race.Game.IsCountingDown, Is.False);
            Assert.That(race.Game.IsRunning, Is.True);
            Assert.That(race.Game.ManualStart, Is.True);
            Assert.That(race.Director.IsBriefing, Is.False);
            race.Director.CloseBriefing();
            Assert.That(race.Game.IsRunning, Is.True, "Closing twice keeps the run going.");
            yield return StartStage(race);
            Assert.That(race.Director.GoalIndex, Is.Zero);
            yield return new WaitForFixedUpdate();
            Assert.That(race.Game.Tick, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator Briefing_PageNavigationStopsAtBothEnds_AndAnyPageCanStart()
        {
            Race race = null;
            yield return Start(3, made => race = made);
            Assert.That(race.Director.PageIndex, Is.Zero);
            Assert.That(race.Director.PageCount, Is.GreaterThan(1));
            race.Director.PreviousPage();
            Assert.That(race.Director.PageIndex, Is.Zero);
            for (int i = 1; i < race.Director.PageCount; i++)
            {
                race.Director.NextPage();
                Assert.That(race.Director.PageIndex, Is.EqualTo(i));
                Assert.That(race.Director.CurrentPage.Title, Is.EqualTo(race.Director.CurrentStage.Pages[i].Title));
            }
            race.Director.NextPage();
            Assert.That(race.Director.PageIndex, Is.EqualTo(race.Director.PageCount - 1));
            for (int i = race.Director.PageCount - 2; i >= 0; i--)
            {
                race.Director.PreviousPage();
                Assert.That(race.Director.PageIndex, Is.EqualTo(i));
            }
            race.Director.PreviousPage();
            Assert.That(race.Director.PageIndex, Is.Zero);
            race.Director.CloseBriefing();
            Assert.That(race.Game.IsCountingDown, Is.False);
            Assert.That(race.Game.IsRunning, Is.True, "Experienced players can skip from the first page.");
            Assert.That(race.Director.IsBriefing, Is.False);
        }

        [UnityTest]
        public IEnumerator Stages_ClearedStageOpensTheNextBriefingBackAtTheStart()
        {
            Race race = null;
            yield return Start(0, made => race = made);
            Vector3 spawn = race.Player.transform.position;
            race.Game.SetInputSource(new TutorialBot(race.Game, race.Player, race.Director));
            yield return StartStage(race);
            float deadline = Time.realtimeSinceStartup + 30f;
            while (race.Director.StageIndex == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(race.Director.StageIndex, Is.EqualTo(1));
            Assert.That(race.Director.IsBriefing, Is.True);
            Assert.That(race.Director.PageIndex, Is.Zero);
            Assert.That(race.Director.GoalIndex, Is.Zero);
            Assert.That(race.Game.IsReady, Is.True);
            Assert.That(race.Game.ManualStart, Is.False);
            Assert.That(race.Game.Tick, Is.Zero);
            Assert.That(Vector3.Distance(race.Player.transform.position, spawn), Is.LessThan(0.05f));
            yield return new WaitForSeconds(ChainRushGame.CountdownSeconds + 0.2f);
            Assert.That(race.Director.IsBriefing, Is.True);
            Assert.That(race.Game.Tick, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Briefing_LobbyLeavesBeforeTheStageStarts()
        {
            Race race = null;
            yield return Start(0, made => race = made);
            Assert.That(race.Director.IsBriefing, Is.True);
            // Same destination the HUD's briefing ESC handler calls.
            race.Assembler.ReturnToLobby();
            yield return WaitForScene(RaceLaunch.LobbyScene);
        }

        // ---- ESC -----------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Pause_RestartRunsImmediatelyFromTheStagesFirstGoal()
        {
            Race race = null;
            yield return Start(1, made => race = made);
            yield return StartStage(race);
            race.Player.SetPosition(race.Director.CurrentGoal.Point + Vector3.up * RunnerHeight);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (race.Director.GoalIndex == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(race.Director.GoalIndex, Is.EqualTo(1), "The zone was reached.");
            race.Game.TogglePause();
            Assert.That(race.Hud.Offers(ChainRushHud.PauseChoice.Restart), Is.True);
            race.Hud.ChoosePause(ChainRushHud.PauseChoice.Restart);
            yield return null;
            yield return null;
            Assert.That(race.Game.IsCountingDown, Is.False);
            Assert.That(race.Game.IsRunning, Is.True);
            Assert.That(race.Director.IsBriefing, Is.False, "Restart skips the already-read briefing.");
            Assert.That(race.Game.ManualStart, Is.True);
            Assert.That(race.Director.StageIndex, Is.EqualTo(1), "The same stage.");
            Assert.That(race.Director.GoalIndex, Is.EqualTo(0), "From its first goal.");
        }

        [UnityTest]
        public IEnumerator Pause_LobbyLeavesTheTutorialHalfway()
        {
            Race race = null;
            yield return Start(0, made => race = made);
            yield return StartStage(race);
            race.Game.TogglePause();
            Assert.That(race.Hud.Offers(ChainRushHud.PauseChoice.Lobby), Is.True, "Out by ESC, as in a single race.");
            race.Hud.ChoosePause(ChainRushHud.PauseChoice.Lobby);
            yield return WaitForScene(RaceLaunch.LobbyScene);
        }

        // ---- the map -------------------------------------------------------------------------------------

        // User 2026-11-06: a mission map, with no finish. Round the ring more than a lap, nothing is counted.
        [UnityTest]
        public IEnumerator Map_HasNoStartLine_AndGoingRoundIsNoLap()
        {
            Race race = null;
            yield return Start(0, made => race = made);
            yield return StartStage(race);
            Assert.That(race.Circuit.transform.Find("Start line"), Is.Null);
            for (int degrees = 0; degrees <= 540; degrees += 30)
            {
                Vector3 radial = Quaternion.Euler(0f, -90f + degrees, 0f) * Vector3.forward;
                race.Player.SetPosition(TutorialCourse.Pole + radial * TutorialCourse.RingRadius + Vector3.up * RunnerHeight);
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
            }
            Assert.That(race.Circuit.Laps.CompletedLaps, Is.EqualTo(0));
            Assert.That(race.Game.IsRunning, Is.True, "Nothing ended the run.");
        }

        [UnityTest]
        public IEnumerator Map_TheWallKeepsTheRunnerOnTheFloor()
        {
            Race race = null;
            yield return Start(0, made => race = made);
            yield return StartStage(race);
            race.Game.SetInputSource(new Throttle());
            Vector3 outward = Vector3.left;
            race.Player.SetPosition(TutorialCourse.Pole + outward * (TutorialCourse.FloorRadius - 20f) + Vector3.up * RunnerHeight);
            race.Game.Racer.Heading = -90f;
            Time.timeScale = 6f;
            float deadline = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < deadline) yield return null;
            Vector3 fromPole = race.Player.transform.position - TutorialCourse.Pole;
            fromPole.y = 0f;
            Assert.That(fromPole.magnitude, Is.GreaterThan(TutorialCourse.FloorRadius - 3f), "It ran up to the wall.");
            Assert.That(fromPole.magnitude, Is.LessThan(TutorialCourse.FloorRadius), "The wall held it.");
            Assert.That(race.Player.IsGrounded, Is.True, "It stands on the floor.");
        }

        // ---- helpers -------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Map_PlayerFindsThePoleAcrossTheWholeFloor()
        {
            Race race = null;
            yield return Start(0, made => race = made);
            foreach (float radius in new[] { TutorialCourse.IslandRadius + 1f, 15f, 30f, 60f, 100f, TutorialCourse.FloorRadius - 1f })
                for (int degrees = 0; degrees < 360; degrees += 15)
                {
                    Vector3 radial = Quaternion.Euler(0f, degrees, 0f) * Vector3.forward;
                    race.Player.SetPosition(TutorialCourse.Pole + radial * radius + Vector3.up * RunnerHeight);
                    Assert.That(race.Player.TryFindPole(out Vector3 pole), Is.True, $"radius {radius}, angle {degrees}");
                    Assert.That(Vector3.Distance(pole, TutorialCourse.Pole), Is.LessThan(0.01f));
                }
        }

        [UnityTest]
        public IEnumerator Map_PlayerHooksAndKeepsSwingingNearTheWall()
        {
            Race race = null;
            yield return Start(0, made => race = made);
            Time.timeScale = 0f;
            race.Director.CloseBriefing();
            race.Player.SetPosition(TutorialCourse.Pole + Vector3.right * (TutorialCourse.FloorRadius - 3f) + Vector3.up * RunnerHeight);
            for (int tick = 0; tick < 20; tick++) race.Game.StepTick(new Throttle());
            Assert.That(race.Player.IsGrounded, Is.True);
            Vector3 forward = race.Game.Track.Frame(race.Player.transform.position).Forward;
            race.Game.Racer.Heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            race.Game.Racer.Velocity = forward * 12f;
            race.Game.StepTick(new FixedInput(new TickInput(0f, false, false, false, swingHeld: true, swingPressed: true)));
            Assert.That(race.Player.IsSwinging, Is.True, "A real grounded press near the wall hooks the pole.");
            Assert.That(race.Game.Racer.SwingHookRadius, Is.GreaterThan(TutorialCourse.FloorRadius - 5f));
            Vector3 hookedAt = race.Player.transform.position;
            var hold = new FixedInput(new TickInput(0f, false, false, false, swingHeld: true));
            for (int tick = 0; tick < 120; tick++) race.Game.StepTick(hold);
            Assert.That(race.Player.IsSwinging, Is.True);
            Assert.That(Vector3.Distance(race.Player.transform.position, hookedAt), Is.GreaterThan(10f));
        }

        [UnityTest]
        public IEnumerator Map_FloorRaycastsAcrossRadiiAndDirectionsAreLevelWithinOneMillimetre()
        {
            Race race = null;
            yield return Start(0, made => race = made);
            // Keep the runner's collider out of every downward ray, including the ray through its spawn.
            race.Player.SetPosition(TutorialCourse.Pole + Vector3.up * 50f);
            PhysicsScene world = race.Player.gameObject.scene.GetPhysicsScene();
            float lowest = float.PositiveInfinity, highest = float.NegativeInfinity;
            foreach (float radius in new[] { TutorialCourse.IslandRadius + 1f, 15f, 30f, 60f, 100f, TutorialCourse.FloorRadius - 1f })
                for (int degrees = 0; degrees < 360; degrees += 15)
                {
                    Vector3 radial = Quaternion.Euler(0f, degrees, 0f) * Vector3.forward;
                    Vector3 from = TutorialCourse.Pole + radial * radius + Vector3.up * 10f;
                    Assert.That(world.Raycast(from, Vector3.down, out RaycastHit hit, 20f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True, $"radius {radius}, angle {degrees}");
                    lowest = Mathf.Min(lowest, hit.point.y);
                    highest = Mathf.Max(highest, hit.point.y);
                }
            Assert.That(highest - lowest, Is.LessThanOrEqualTo(0.001f));
        }

        [UnityTest]
        public IEnumerator Poles_DefaultMotorKeepsTheOriginalDistanceWithoutExtraReach()
        {
            RunnerMotor player = null;
            ChainRushGame game = null;
            yield return Load(RacePath, scene => { player = Find<RunnerMotor>(scene); game = Find<ChainRushGame>(scene); });
            var poles = new List<Vector3>();
            game.Track.GetPoles(poles);
            Assert.That(poles, Is.Not.Empty);
            int found = 0, missed = 0;
            foreach (Vector3 pole in poles)
                foreach (float radius in new[] { 4f, 20f, 35.5f, 36.5f, 80f, 200f })
                    for (int degrees = 0; degrees < 360; degrees += 30)
                    {
                        player.SetPosition(pole + Quaternion.Euler(0f, degrees, 0f) * Vector3.forward * radius + Vector3.up * RunnerHeight);
                        Vector3 forward = game.Track.Frame(player.transform.position).Forward;
                        bool expected = game.Track.TryFindPole(player.transform.position, Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg,
                            95f, 6f, out Vector3 original, 0.5f);
                        Assert.That(player.TryFindPole(out Vector3 actual), Is.EqualTo(expected));
                        if (expected) { found++; Assert.That(actual, Is.EqualTo(original)); }
                        else missed++;
                    }
            Assert.That(found, Is.GreaterThan(0));
            Assert.That(missed, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator Poles_AdditionalReachRejectsInvalidValuesAndChangesDuringARun()
        {
            Race race = null;
            yield return Start(0, made => race = made);
            foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => race.Player.ReachPolesFrom(invalid));
            race.Player.ReachPolesFrom(0f);
            race.Player.ReachPolesFrom(TutorialCourse.FloorRadius);
            race.Director.CloseBriefing();
            Assert.Throws<InvalidOperationException>(() => race.Player.ReachPolesFrom(1f));
            race.Game.TogglePause();
            Assert.Throws<InvalidOperationException>(() => race.Player.ReachPolesFrom(1f));
            race.Game.TogglePause();
        }

        private sealed class FixedInput : IInputSource
        {
            private readonly TickInput input;
            public FixedInput(TickInput input) => this.input = input;
            public void Poll() { }
            public void Clear() { }
            public TickInput Consume() => input;
        }

        private sealed class Race
        {
            public RaceAssembler Assembler;
            public ChainRushGame Game;
            public RunnerMotor Player;
            public CircuitRace Circuit;
            public ChainRushHud Hud;
            public TutorialDirector Director => Assembler.Tutorial;
        }

        private IEnumerator Start(int stage, Action<Race> made)
        {
            RaceLaunch.Request(TutorialCourse.Setup(runner, null, stage));
            yield return Load(RacePath, scene => made(new Race
            {
                Assembler = Find<RaceAssembler>(scene),
                Game = Find<ChainRushGame>(scene),
                Player = Find<RunnerMotor>(scene),
                Circuit = Find<CircuitRace>(scene),
                Hud = Find<ChainRushHud>(scene),
            }));
        }

        private static IEnumerator StartStage(Race race)
        {
            if (race.Director.IsBriefing) race.Director.CloseBriefing();
            Assert.That(race.Game.IsCountingDown, Is.False);
            Assert.That(race.Game.IsRunning, Is.True, "Closing the briefing starts immediately.");
            Time.timeScale = 6f;
            yield break;
        }

        private static IEnumerator WaitForScene(string name)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(name));
        }

        private static IEnumerator Load(string path, Action<Scene> found)
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("Tutorial tests require the Unity Editor.");
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

        // Full throttle, straight on.
        private sealed class Throttle : IInputSource
        {
            public void Poll() { }
            public void Clear() { }
            public TickInput Consume() => new TickInput(0f, false, false, false);
        }

        // Plays the current goal as the panel tells a player to.
        private sealed class TutorialBot : IInputSource
        {
            // Steering is full this many degrees off the mark.
            private const float FullSteerDegrees = 15f;
            // The scene's brake (RunnerMotor.brakeDeceleration) in m/s a second, and a little early for the stop goal.
            private const float Brake = 20f;
            private const float BrakeMargin = 1f;

            private readonly ChainRushGame game;
            private readonly RunnerMotor player;
            private readonly TutorialDirector director;
            // A guidance test holds at Boost to inspect the panel before the next action consumes a slot.
            private readonly bool useBoost;

            public TutorialBot(ChainRushGame game, RunnerMotor player, TutorialDirector director, bool useBoost = true)
            {
                this.game = game;
                this.player = player;
                this.director = director;
                this.useBoost = useBoost;
            }

            public void Poll() { }
            public void Clear() { }

            public TickInput Consume()
            {
                if (director.IsStageCleared) return new TickInput(0f, false, false, false, coasting: true);
                TutorialCourse.Goal goal = director.CurrentGoal;
                switch (goal.Kind)
                {
                    case TutorialCourse.GoalKind.Reach: return new TickInput(SteerAt(goal.Point, player.Heading), false, false, false);
                    case TutorialCourse.GoalKind.Stop: return Stop(goal);
                    case TutorialCourse.GoalKind.Reverse:
                        // Backing up, the rear goes the way the facing's opposite does, and the keys are the other way round.
                        return new TickInput(-SteerAt(goal.Point, player.Heading + 180f), false, false, false, coasting: true, reversing: true);
                    case TutorialCourse.GoalKind.Swing:
                    case TutorialCourse.GoalKind.Gauge: return Swing(0f);
                    case TutorialCourse.GoalKind.Rope: return Swing(TowardThePole());
                    case TutorialCourse.GoalKind.Release: return new TickInput(0f, false, false, false);
                    case TutorialCourse.GoalKind.Boost: return new TickInput(0f, false, false, false, chainActionPressed: useBoost);
                    default: throw new ArgumentOutOfRangeException(nameof(goal), goal.Kind, "TutorialBot: unknown goal.");
                }
            }

            private TickInput Stop(in TutorialCourse.Goal goal)
            {
                float steer = SteerAt(goal.Point, player.Heading);
                float speed = player.HorizontalSpeed;
                float brakingDistance = speed * speed / (2f * Brake) + BrakeMargin;
                bool brake = Distance(goal.Point) <= brakingDistance;
                return new TickInput(steer, false, false, false, coasting: brake, reversing: brake);
            }

            // The key goes down while no pole is hooked, which hooks on the first tick the pole is in reach.
            private TickInput Swing(float steer)
            {
                if (!player.IsSwinging && !player.TryFindPole(out _))
                    return new TickInput(SteerAt(TutorialCourse.Pole, player.Heading), false, false, false);
                return new TickInput(steer, false, false, false, swingHeld: true, swingPressed: !player.IsSwinging);
            }

            private float TowardThePole()
            {
                Vector3 right = Quaternion.Euler(0f, player.Heading + 90f, 0f) * Vector3.forward;
                return Vector3.Dot(game.Racer.SwingCenter - player.transform.position, right) >= 0f ? 1f : -1f;
            }

            private float SteerAt(Vector3 point, float heading)
            {
                Vector3 to = point - player.transform.position;
                float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                return Mathf.Clamp(Mathf.DeltaAngle(heading, yaw) / FullSteerDegrees, -1f, 1f);
            }

            private float Distance(Vector3 point)
            {
                Vector3 to = point - player.transform.position;
                to.y = 0f;
                return to.magnitude;
            }
        }
    }
}
