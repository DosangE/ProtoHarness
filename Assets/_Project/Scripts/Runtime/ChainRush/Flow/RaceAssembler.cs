using ProtoHarness.ChainRush.Race;
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
    // the scene runs as saved, Enter to start.
    // Runs before the game and the circuit so the definition is in place when either Awake builds the circuit.
    [DefaultExecutionOrder(-100)]
    public sealed class RaceAssembler : MonoBehaviour
    {
        [SerializeField] private ChainRushGame game;
        [SerializeField] private CircuitRace circuit;
        [SerializeField] private RunnerMotor player;
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
            if (game == null || circuit == null || player == null)
            {
                Debug.LogError("RaceAssembler: game, circuit and player are required.", this);
                enabled = false;
                return;
            }
            if (!RaceLaunch.TryTake(out setup))
            {
                Debug.Log("RaceAssembler: no race was requested; the scene runs as saved (opened directly).", this);
                return;
            }
            // Only what the simulation can race today: multi is locked in the lobby.
            if (setup.Mode != RaceMode.Single)
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
            if (setup.Look != null) circuit.UseLook(setup.Look);
            player.Configure(setup.Runner, SpeedScale.Of(setup.Speed));
            if (setup.Runner.Look != null && !TryDress(setup.Runner.Look)) return;
            launched = true;
            game.TickCompleted += RecordTick;
        }

        private void OnDestroy()
        {
            if (launched && game != null) game.TickCompleted -= RecordTick;
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
            ShowGhost();
            if (setup.EnvironmentScene.Length == 0)
            {
                CountIn();
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
            load.completed += _ => CountIn();
        }

        // The best run on this track at this speed, whichever runner set it, loaded before the start and again when a race
        // that set a new best counts in once more (Update). A file that does not read is no reason to stop the race: it
        // runs without a ghost, and says why (the file stays until the next best replaces it).
        private void ShowGhost()
        {
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
            game.StartCountdown();
        }

        private void Update()
        {
            if (!launched) return;
            // Each finish counts once; a restart (pause menu or RUN AGAIN) clears it and the next finish counts again.
            // Marked before it is kept, so a store that throws is not tried (and the flags not overwritten) every frame.
            bool finished = game.HasFinished;
            bool newFinish = finished && !wasFinished;
            wasFinished = finished;
            if (newFinish) KeepFinish();
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
