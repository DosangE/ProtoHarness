using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProtoHarness.ChainRush.Flow
{
    // Puts the race scene together from the lobby's RaceSetup (RaceLaunch): swaps in the chosen track definition before
    // the circuit is built, gives the runner the chosen profile at the chosen speed class (SpeedScale) and its look, loads the track's environment scene additively, then counts the race in. A finish
    // goes to the best records (BestRecords). After the race, ESC or the HUD's button goes back to the lobby. Without a request (the scene opened directly) it does nothing and
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

        // Where a finish from the lobby is kept (the player's records; a test gives its own).
        public BestRecords Records
        {
            get => records;
            set => records = value ?? throw new System.ArgumentNullException(nameof(value));
        }
        // Whether the last finish beat the best record for its track and speed class.
        public bool LastFinishWasBest { get; private set; }

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
            circuit.UseDefinition(setup.Track);
            player.Configure(setup.Runner, SpeedScale.Of(setup.Speed));
            if (setup.Runner.Look != null && !TryDress(setup.Runner.Look)) return;
            launched = true;
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
            bool finished = game.HasFinished;
            if (finished && !wasFinished)
                LastFinishWasBest = records.Submit(setup.Track.name, setup.Speed, circuit.Laps.TotalTicks, setup.Runner.DisplayName);
            wasFinished = finished;
            if (!(finished || game.HasFailed)) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ReturnToLobby();
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
