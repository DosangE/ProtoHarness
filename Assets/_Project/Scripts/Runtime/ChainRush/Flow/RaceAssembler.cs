using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Bots;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Tutorial;
using ProtoHarness.ChainRush.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProtoHarness.ChainRush.Flow
{
    // Puts the race scene together from the lobby's RaceSetup (RaceLaunch): swaps in the chosen track definition before
    // the circuit is built, gives the runner the chosen profile at the chosen speed class (SpeedScale) and its look, loads the track's environment scene additively, then counts the race in. A finish
    // goes to the best records (BestRecords) and, when it is the best for its track and speed, becomes the ghost there
    // (GhostFiles), which every race there shows running alongside, whichever runner (GhostRunner). After the race, ESC or the HUD's button goes back to the lobby. Without a request (the scene opened directly) it does nothing and
    // the scene runs as saved, Enter to start. A tutorial (RaceMode.Tutorial) lays the round floor instead of a road, has a
    // TutorialDirector run its stages, and keeps no record or ghost.
    // A race with opponents (RaceSetup.WithOpponents) loads one more copy of the race scene for each of them, on a physics
    // scene of its own, and hands them to a RaceField that steps every world on the same tick; only then does it count the
    // race in. In such a copy (RaceSetup.IsOpponentWorld) this assembles the simulation and the runner's model and
    // switches off everything else there is to see or hear: the player's world draws the road and plays the sound.
    // Runs before the game and the circuit so the definition is in place when either Awake builds the circuit.
    [DefaultExecutionOrder(-100)]
    public sealed class RaceAssembler : MonoBehaviour
    {
        [SerializeField] private ChainRushGame game;
        [SerializeField] private CircuitRace circuit;
        [SerializeField] private RunnerMotor player;
        [Tooltip("The race camera, which draws the track look's sky (UseSky).")]
        [SerializeField] private Camera viewCamera;
        private RaceSetup setup;
        private bool launched;
        private bool countedIn;
        private bool leaving;
        private bool wasFinished;
        private BestRecords records = BestRecords.Player;
        private GhostFiles ghosts;
        // The run's path so far, for the ghost of a new best (M4). Read after each tick, never by the simulation.
        private readonly GhostTrace trace = new GhostTrace();
        private bool traceBroken;
        private bool started;
        private GhostRunner ghost;
        // A finish in this race saved a new ghost; the next count-in shows it.
        private bool ghostReplaced;
        private TutorialDirector tutorial;
        private RaceField field;
        // A request for an opponent's world is waiting in RaceLaunch for the copy being loaded to take.
        private bool requestedWorld;
        private Scene loadedOpponent;
        // The opponents' copies of the race scene loaded so far, unloaded again if the race cannot be assembled.
        private readonly System.Collections.Generic.List<Scene> opponentScenes = new System.Collections.Generic.List<Scene>();

        // The opponents' worlds and their stepping, in a race with opponents once it is assembled; null otherwise.
        public RaceField Field => field;

        // Where a finish from the lobby is kept (the player's records; a test gives its own).
        public BestRecords Records
        {
            get => records;
            set => records = value ?? throw new System.ArgumentNullException(nameof(value));
        }
        // Where the ghost of each track and speed's best is kept (the player's ghosts; a test gives its own folder). Changing it once
        // the race is set up shows that store's ghost instead, as the lobby reads its records again when they change.
        public GhostFiles Ghosts
        {
            get => ghosts ??= GhostFiles.Player;
            set
            {
                ghosts = value ?? throw new System.ArgumentNullException(nameof(value));
                if (started) ShowGhost();
            }
        }
        // The best run here racing alongside (M4-3), or null when there is none (or its file could not be read).
        public GhostRunner Ghost => ghost;
        // The tutorial's stages in a tutorial; null in a race.
        public TutorialDirector Tutorial => tutorial;
        public bool HasGhost => ghost != null;
        // Whether the last finish beat the best record for its track and speed class, and so became the ghost there.
        public bool LastFinishWasBest { get; private set; }
        // The last finish against the records kept before it, for the result screen (M4-2). Valid once HasLastFinish.
        public FinishComparison LastFinish { get; private set; }
        public bool HasLastFinish { get; private set; }

        // Whether this race came from the lobby (and so goes back to it).
        public bool IsLaunched => launched;
        // Launched, and the environment is still loading: the countdown has not begun.
        public bool IsAssembling => launched && !countedIn;
        public RaceSetup Setup => setup;

        private void Awake()
        {
            if (game == null || circuit == null || player == null || viewCamera == null)
            {
                Debug.LogError("RaceAssembler: game, circuit, player and the view camera are required.", this);
                enabled = false;
                return;
            }
            if (!RaceLaunch.TryTake(out setup))
            {
                Debug.Log("RaceAssembler: no race was requested; the scene runs as saved (opened directly).", this);
                return;
            }
            if (setup.Mode == RaceMode.Bots && !setup.IsOpponentWorld && setup.OpponentCount == 0)
            {
                StopAssembling("a bot race needs opponents.");
                return;
            }
            // An opponent's world has nothing to show from its own camera or hear through its own listener, even when it
            // fails to assemble below: two AudioListeners log on every frame they are both on.
            if (setup.IsOpponentWorld)
                foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                {
                    foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                    foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                }
            // Only what the simulation can race today: multi is locked in the lobby.
            if (setup.Mode == RaceMode.Multi)
            {
                Debug.LogError($"RaceAssembler: {setup.Mode} at {setup.Speed} cannot be raced yet (multi: M7).", this);
                game.enabled = false;
                enabled = false;
                return;
            }
            // Records and ghosts are named by the track's definition asset (BestRecords, GhostFiles).
            if (string.IsNullOrEmpty(setup.Track.name))
            {
                Debug.LogError($"RaceAssembler: the track '{setup.Title}' has no asset name to keep its records under.", this);
                game.enabled = false;
                enabled = false;
                return;
            }
            circuit.UseDefinition(setup.Track);
            bool isTutorial = setup.Mode == RaceMode.Tutorial;
            if (isTutorial) circuit.UseArena(TutorialCourse.RingRadius, TutorialCourse.IslandRadius, TutorialCourse.FloorRadius);
            if (setup.Look != null)
            {
                circuit.UseLook(setup.Look);
                game.UseTrackSounds(setup.Look.Music, setup.Look.Ambience);
            }
            player.Configure(setup.Runner, SpeedScale.Of(setup.Speed));
            if (setup.Runner.Look != null && !TryDress(setup.Runner.Look)) return;
            launched = true;
            if (setup.IsOpponentWorld) return;
            if (isTutorial)
            {
                player.ReachPolesFrom(TutorialCourse.FloorRadius);
                tutorial = gameObject.AddComponent<TutorialDirector>();
                tutorial.Initialize(game, player, circuit.LightMaterial, setup.FirstStage);
                return;
            }
            if (setup.Mode != RaceMode.Bots) game.TickCompleted += RecordTick;
        }

        private void OnDestroy()
        {
            if (launched && game != null) game.TickCompleted -= RecordTick;
            SceneManager.sceneLoaded -= OpponentLoaded;
            // A copy that was being loaded when the race was left will not take its request; the next race must not find it.
            if (requestedWorld) RaceLaunch.Clear();
            // The ring was made for this tutorial (TutorialCourse.Setup), so it goes with it.
            if (tutorial != null) Destroy(setup.Track);
        }

        // Tick 1 is a new run (a restart starts over). Ticks arrive in order in a single race; if one does not, this run
        // has no ghost rather than a wrong one.
        private void RecordTick(int tick)
        {
            if (tick == 1)
            {
                trace.Clear();
                traceBroken = false;
            }
            if (traceBroken) return;
            if (tick != trace.Count + 1)
            {
                Debug.LogError($"RaceAssembler: tick {tick} came after {trace.Count}; this run will keep no ghost.", this);
                traceBroken = true;
                return;
            }
            trace.Add(player.transform.position, player.Heading);
        }

        // Puts the runner's look on the race's runner model (presentation only, RunnerLook). A look that does not fit
        // the model is refused loudly and the race is not run in it: there is no right look to fall back to.
        private bool TryDress(GameObject look)
        {
            Transform model = player.TryGetComponent(out RunnerTilt tilt) ? tilt.Body : null;
            if (model == null)
            {
                Debug.LogError($"RaceAssembler: the runner has no RunnerTilt with a body to wear the look '{look.name}'.", this);
                game.enabled = false;
                enabled = false;
                return false;
            }
            try
            {
                RunnerLook.Apply(model, look);
            }
            catch (System.ArgumentException e)
            {
                Debug.LogException(e, this);
                game.enabled = false;
                enabled = false;
                return false;
            }
            return true;
        }

        private void Start()
        {
            if (!launched) return;
            started = true;
            if (setup.IsOpponentWorld)
            {
                // Every Awake of the copy has run, so its road is laid and can be hidden. The field starts its run.
                HideAllButTheRunner();
                countedIn = true;
                return;
            }
            if (setup.Look != null && setup.Look.Sky != null) UseSky(setup.Look);
            if (tutorial == null) ShowGhost();
            if (setup.EnvironmentScene.Length == 0)
            {
                GatherTheField();
                return;
            }
            AsyncOperation load = SceneManager.LoadSceneAsync(setup.EnvironmentScene, LoadSceneMode.Additive);
            if (load == null)
            {
                Debug.LogError($"RaceAssembler: could not load the environment scene '{setup.EnvironmentScene}'. Is it in the build's scene list?", this);
                game.enabled = false;
                enabled = false;
                return;
            }
            load.completed += _ => GatherTheField();
        }

        // Counts the race in, after loading the opponents' worlds when the race has opponents.
        private void GatherTheField()
        {
            if (setup.OpponentCount == 0) CountIn();
            else StartCoroutine(LoadOpponents());
        }

        // One copy of the race scene for each opponent, one after another (RaceLaunch holds one request at a time), each
        // on a physics scene of its own; then the field that steps them with the player's game, and the count-in. A copy
        // that does not come up as a race world, or whose own assembler refused its setup (and switched its game off),
        // stops the race, loudly: there is no race to run without it.
        private System.Collections.IEnumerator LoadOpponents()
        {
            var worlds = new System.Collections.Generic.List<RaceWorld>(setup.OpponentCount);
            var inputs = new System.Collections.Generic.List<IInputSource>(setup.OpponentCount);
            for (int i = 0; i < setup.OpponentCount; i++)
            {
                if (leaving) yield break;
                RaceLaunch.Request(setup.ForOpponent(i));
                requestedWorld = true;
                loadedOpponent = default;
                SceneManager.sceneLoaded += OpponentLoaded;
                AsyncOperation load = SceneManager.LoadSceneAsync(RaceLaunch.RaceScene, RaceWorld.IsolatedLoad);
                if (load == null)
                {
                    StopAssembling($"could not load a copy of the scene '{RaceLaunch.RaceScene}' for opponent {i}. Is it in the build's scene list?");
                    yield break;
                }
                yield return load;
                SceneManager.sceneLoaded -= OpponentLoaded;
                Scene copy = loadedOpponent;
                if (!copy.IsValid() || !copy.isLoaded)
                {
                    StopAssembling($"the sceneLoaded event did not supply opponent {i}'s world.");
                    yield break;
                }
                opponentScenes.Add(copy);
                if (RaceLaunch.HasPending)
                {
                    StopAssembling($"the world loaded for opponent {i} did not take its race setup.");
                    yield break;
                }
                requestedWorld = false;
                RaceWorld world;
                try
                {
                    world = RaceWorld.FromScene(copy);
                }
                catch (System.Exception e) when (e is System.ArgumentException || e is System.InvalidOperationException)
                {
                    Debug.LogException(e, this);
                    StopAssembling($"the scene loaded for opponent {i} is not a race world.");
                    yield break;
                }
                RaceAssembler its = AssemblerOf(copy);
                if (its == null || !its.IsLaunched || !its.enabled || !world.Game.enabled)
                {
                    StopAssembling($"the world loaded for opponent {i} did not assemble itself (see its error before this).");
                    yield break;
                }
                worlds.Add(world);
                inputs.Add(new CircuitBot(world.Game, world.Player, world.Grapple, world.Circuit));
            }
            if (setup.Mode == RaceMode.Bots)
            {
                int count = worlds.Count + 1;
                int[] slots = StartGrid.Assign(count, setup.Seed);
                circuit.UseStartSlot(count, slots[0]);
                game.ReturnToStart();
                for (int i = 0; i < worlds.Count; i++) worlds[i].Circuit.UseStartSlot(count, slots[i + 1]);
            }
            field = gameObject.AddComponent<RaceField>();
            try
            {
                field.Initialize(game, worlds, inputs);
            }
            catch (System.ArgumentException e)
            {
                Debug.LogException(e, this);
                Destroy(field);
                field = null;
                StopAssembling("the opponents' worlds could not make a field.");
                yield break;
            }
            CountIn();
        }

        private void OpponentLoaded(Scene scene, LoadSceneMode mode)
        {
            if (requestedWorld && mode == LoadSceneMode.Additive && scene.name == RaceLaunch.RaceScene)
                loadedOpponent = scene;
        }

        private static RaceAssembler AssemblerOf(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                RaceAssembler found = root.GetComponentInChildren<RaceAssembler>(true);
                if (found != null) return found;
            }
            return null;
        }

        // Stops this race before it counts in and takes down the opponents' worlds loaded for it, so nothing of it runs on.
        private void StopAssembling(string why)
        {
            SceneManager.sceneLoaded -= OpponentLoaded;
            Debug.LogError("RaceAssembler: " + why + " The race is not run.", this);
            if (requestedWorld) RaceLaunch.Clear();
            requestedWorld = false;
            game.enabled = false;
            enabled = false;
            foreach (Scene copy in opponentScenes)
                if (copy.isLoaded) SceneManager.UnloadSceneAsync(copy);
            opponentScenes.Clear();
        }

        // In an opponent's world only the runner's model is left to see and nothing to hear: the road, the lights, the HUD
        // and the sound are the player's world's (the copies stand in the same place). Presentation only, so the copy's
        // run is unchanged: the simulation is stepped by calls (ChainRushGame.StepTick), not by these components.
        private void HideAllButTheRunner()
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (Renderer shown in root.GetComponentsInChildren<Renderer>(true))
                    if (!shown.transform.IsChildOf(player.transform)) shown.enabled = false;
                foreach (Light lamp in root.GetComponentsInChildren<Light>(true)) lamp.enabled = false;
                foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true)) source.mute = true;
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null || behaviour.transform.IsChildOf(player.transform)) continue;
                    if (behaviour is ChainRushGame || behaviour is CircuitRace || behaviour is RaceAssembler) continue;
                    behaviour.enabled = false;
                }
            }
        }

        // The track look's sky behind everything and its horizon colour as the fog (user 2026-11-02, "최외곽이 살짝
        // 비어보인다"), so the far land fades into the sky; the camera draws no further than the fog's end, where nothing
        // but fog is left. Set in Start, once the race scene is the active one: these render settings are the race
        // scene's and go with it when the lobby loads. Presentation only.
        private void UseSky(TrackLook look)
        {
            RenderSettings.skybox = look.Sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = look.Horizon;
            RenderSettings.fogStartDistance = look.FogStart;
            RenderSettings.fogEndDistance = look.FogEnd;
            viewCamera.clearFlags = CameraClearFlags.Skybox;
            viewCamera.farClipPlane = look.FogEnd;
        }

        // The best run on this track at this speed, whichever runner set it, loaded before the start and again when a race
        // that set a new best counts in once more (Update). A file that does not read is no reason to stop the race: it
        // runs without a ghost, and says why (the file stays until the next best replaces it).
        private void ShowGhost()
        {
            // An opponent's world shows no ghost, whichever store it is given.
            if (setup.IsOpponentWorld || setup.Mode == RaceMode.Bots) return;
            ghostReplaced = false;
            if (ghost != null)
            {
                Destroy(ghost.gameObject);
                ghost = null;
            }
            GhostTrace best;
            try
            {
                if (!Ghosts.TryLoad(setup.Track.name, setup.Speed, out best)) return;
            }
            catch (System.Exception e) when (e is System.IO.InvalidDataException || e is System.IO.IOException)
            {
                Debug.LogError($"RaceAssembler: the ghost on {setup.Track.name} at {setup.Speed} could not be read; racing without it. {e.Message}", this);
                return;
            }
            Transform model = player.TryGetComponent(out RunnerTilt tilt) ? tilt.Body : null;
            if (model == null)
            {
                Debug.LogError("RaceAssembler: the runner has no RunnerTilt with a body to copy for its ghost; racing without it.", this);
                return;
            }
            var root = new GameObject("Ghost runner");
            SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            ghost = root.AddComponent<GhostRunner>();
            ghost.Initialize(game, best, model);
        }

        private void CountIn()
        {
            if (leaving) return;
            countedIn = true;
            if (tutorial != null) game.ReturnToStart();
            else game.StartCountdown();
        }

        private void Update()
        {
            // An opponent's world keeps no record and does not leave for the lobby by itself.
            if (!launched || setup.IsOpponentWorld) return;
            // Each finish counts once; a restart (pause menu or RUN AGAIN) clears it and the next finish counts again.
            // Marked before it is kept, so a store that throws is not tried (and the flags not overwritten) every frame.
            bool finished = game.HasFinished;
            bool newFinish = finished && !wasFinished;
            wasFinished = finished;
            if (newFinish && tutorial == null && setup.Mode != RaceMode.Bots) KeepFinish();
            // RUN AGAIN or a restart after a new best: race the new ghost from this count-in.
            if (ghostReplaced && game.IsCountingDown) ShowGhost();
            if (!(finished || game.HasFailed)) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ReturnToLobby();
        }

        // The finish goes to the best for its track and speed class; a new best keeps this run's path as the ghost there.
        // The ghost is always the best's own run: a new best whose path is incomplete removes the old ghost, loudly.
        private void KeepFinish()
        {
            int ticks = circuit.Laps.TotalTicks;
            string track = setup.Track.name;
            // Read before the finish replaces it: the result screen compares with what was there.
            bool hadBest = records.TryGet(track, setup.Speed, out BestRecords.Record before);
            LastFinish = new FinishComparison(ticks, hadBest ? before.Ticks : 0, hadBest ? before.Runner : "");
            HasLastFinish = true;
            LastFinishWasBest = records.Submit(track, setup.Speed, ticks, setup.Runner.DisplayName);
            if (!LastFinishWasBest) return;
            if (traceBroken || trace.Count == 0)
            {
                Debug.LogError($"RaceAssembler: the best on {track} at {setup.Speed} is kept without a ghost (the run's path is incomplete); the old ghost is removed.", this);
                Ghosts.Delete(track, setup.Speed);
                ghostReplaced = true;
                return;
            }
            Ghosts.Save(track, setup.Speed, trace);
            ghostReplaced = true;
        }

        // Leaves the race (and its environment scene) for the lobby.
        public void ReturnToLobby()
        {
            if (!launched) throw new System.InvalidOperationException("RaceAssembler: a race opened directly has no lobby to go back to.");
            if (leaving) return;
            leaving = true;
            SceneManager.LoadScene(RaceLaunch.LobbyScene);
        }
    }
}
