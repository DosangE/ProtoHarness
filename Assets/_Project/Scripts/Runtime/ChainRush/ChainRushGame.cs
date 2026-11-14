using UnityEngine;
using UnityEngine.InputSystem;
using ProtoHarness.ChainRush.Audio;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Track;

namespace ProtoHarness.ChainRush
{
    public sealed class ChainRushGame : MonoBehaviour
    {
        private enum Phase { Ready, Countdown, Running, Paused, Failed, Complete }
        // How long the 3, 2, 1 before a run lasts (StartCountdown).
        public const float CountdownSeconds = 3f;
        [SerializeField] private RunnerMotor player;
        [SerializeField] private GrappleController grapple;
        [SerializeField] private FollowCamera followCamera;
        [SerializeField] private CourseTarget[] targets;
        [SerializeField] private Transform attackVisual;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private float finishZ = 496f;
        [SerializeField] private CircuitRace circuit;
        [SerializeField] private RunRules rules;
        [SerializeField] private bool enhancedPresentation;
        [SerializeField] private Audio.ChainRushAudio presentationAudio;
        [SerializeField] private Visuals.RunnerAnimation presentationAnimation;
        private Phase phase;
        private RacerState racer;
        private Centerline track;
        private int tick;
        private IInputSource inputSource;
        private AudioClip[] cues;
        private float countdownLeft;
        private enum TickStage { Input, Movement, Judgment }
        private TickStage tickStage;
        private TickInput pendingInput;

        public bool IsRunning => phase == Phase.Running;
        public bool IsPaused => phase == Phase.Paused;
        public bool IsReady => phase == Phase.Ready;
        public bool IsCountingDown => phase == Phase.Countdown;
        // 3, 2, 1 while counting down; 0 otherwise.
        public int CountdownNumber => IsCountingDown ? Mathf.CeilToInt(countdownLeft) : 0;
        // How long the number now showing has been up, in seconds; 0 when not counting down.
        public float CountdownNumberAge => IsCountingDown ? CountdownNumber - countdownLeft : 0f;
        // Whether Enter and R (and the HUD's buttons) may start a run. A network race starts runs itself and turns this off,
        // so the keyboard cannot restart a run the server is simulating.
        public bool ManualStart { get; set; } = true;
        // Whether FixedUpdate advances the simulation. A race of several worlds (Race/RaceField) turns this off in every
        // one of them and steps them itself, all on the same tick; a tick is the same tick either way.
        public bool SelfStepping { get; set; } = true;
        public bool HasFailed => phase == Phase.Failed;
        public bool HasFinished => phase == Phase.Complete;
        public int Health => racer.Health;
        public int Hits => racer.Hits;
        public int Grapples => racer.Grapples;
        public int Tick => tick;
        public float Elapsed => Ticks.ToSeconds(tick);
        public float Progress => circuit != null ? circuit.RaceFraction : Mathf.Clamp01((float)(PlayerS / finishZ));
        public float FinishZ => finishZ;
        public bool DamageFlash => racer.IsInvulnerable(tick) && IsRunning;
        public bool AttackActive => racer.IsAttackShown(tick);
        public bool IsCircuit => circuit != null;
        public CircuitRace Circuit => circuit;
        public RunnerMotor Player => player;
        public bool HasPresentation => enhancedPresentation;
        // Presentation only; RaceAssembler may supply the track's clips before the audio's Awake.
        public void UseTrackSounds(AudioClip music, AudioClip ambience)
        {
            if (!enhancedPresentation) return;
            if (presentationAudio == null) throw new System.InvalidOperationException("ChainRushGame: enhanced presentation requires audio.");
            presentationAudio.UseTrackSounds(music, ambience);
        }
        public RacerState Racer => racer;
        public Centerline Track => track;
        public double Distance => System.Math.Max(0d, PlayerS - 5d);
        private double PlayerS => track.Project(player.transform.position).S;

        private void Awake()
        {
            if (player == null || grapple == null || followCamera == null || targets == null || attackVisual == null || audioSource == null || rules == null)
            {
                Debug.LogError("ChainRushGame: all scene references must be assigned.", this);
                enabled = false;
                return;
            }
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] == null)
                {
                    Debug.LogError("ChainRushGame: targets contains a missing reference.", this);
                    enabled = false;
                    return;
                }
            if (!Mathf.Approximately(Time.fixedDeltaTime, Ticks.Seconds))
            {
                Debug.LogError($"ChainRushGame: Time.fixedDeltaTime ({Time.fixedDeltaTime}) must equal Ticks.Seconds ({Ticks.Seconds}). The simulation advances one tick per FixedUpdate.", this);
                enabled = false;
                return;
            }
            if (enhancedPresentation && (presentationAudio == null || presentationAnimation == null))
            {
                Debug.LogError("ChainRushGame: enhanced presentation requires audio and animation references.", this);
                enabled = false;
                return;
            }
            cues = new AudioClip[4];
            for (int i = 0; i < cues.Length; i++)
            {
                const int SampleRate = 22050;
                float[] samples = new float[4410];
                for (int sample = 0; sample < samples.Length; sample++)
                {
                    float t = (float)sample / SampleRate;
                    float envelope = Mathf.Sin(Mathf.PI * sample / samples.Length) * Mathf.Exp(-t * 18f);
                    float frequency = i == 3 ? 150f : 380f + i * 160f + t * 1000f;
                    samples[sample] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.16f;
                }
                cues[i] = AudioClip.Create("ChainRush cue " + i, samples.Length, 1, SampleRate, false);
                cues[i].SetData(samples, 0);
            }
            racer = new RacerState(rules);
            // T0: the straight course is laid out along +z from the world origin. A track definition asset replaces this (COURSE.md T4).
            if (circuit != null)
            {
                // A circuit race runs on the closed centerline of its definition (COURSE.md T4).
                track = circuit.BuildTrack();
            }
            else
            {
                track = new Centerline(Vector3.zero, 0f);
                track.AppendStraight(finishZ);
            }
            inputSource = new KeyboardMouseInputSource();
            attackVisual.gameObject.SetActive(false);
        }

        // Replaces where gameplay controls come from (touch, replay, network). Menu keys are not part of it.
        public void SetInputSource(IInputSource source)
        {
            if (source == null) throw new System.ArgumentNullException(nameof(source));
            source.Clear();
            inputSource = source;
        }

        // The source StepTick() reads, as SetInputSource left it (the keyboard until then). Read-only, for checks.
        public IInputSource InputSource => inputSource;

        // Replaces the course centerline (test roads now, hand-built circuits later). The finish line stays
        // at finishZ along it. A circuit race builds its own track, so it refuses a replacement.
        public void SetTrack(Centerline replacement)
        {
            if (replacement == null) throw new System.ArgumentNullException(nameof(replacement));
            if (circuit != null) throw new System.InvalidOperationException("ChainRushGame: a circuit race builds its own track; SetTrack is for finite straight courses.");
            if (replacement.PieceCount == 0) throw new System.ArgumentException("Track needs at least one piece.", nameof(replacement));
            track = replacement;
        }

        private void OnValidate()
        {
            if (finishZ <= 0f) Debug.LogError("ChainRushGame: finishZ must be positive.", this);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                // A run in progress restarts from the pause menu (ChainRushHud), not from a key (user 2026-10-20: R removed).
                if (ManualStart && keyboard.enterKey.wasPressedThisFrame && !IsRunning && !IsPaused && !IsCountingDown) StartCountdown();
                if (keyboard.escapeKey.wasPressedThisFrame) TogglePause();
            }
            if (IsCountingDown)
            {
                countdownLeft -= Time.deltaTime;
                if (countdownLeft > 0f) return;
                StartRun();
                CountdownFinished?.Invoke();
            }
            if (!IsRunning) return;
            inputSource.Poll();
            float elapsed = Elapsed;
            for (int i = 0; i < targets.Length; i++) targets[i].Animate(elapsed);
            attackVisual.gameObject.SetActive(AttackActive);
            if (AttackActive) attackVisual.localScale = Vector3.one * (1f + Ticks.ToSeconds(racer.AttackTicksLeft(tick)) * 4f);
        }

        private void FixedUpdate()
        {
            if (SelfStepping) StepTick(inputSource);
        }

        // One tick on the game's own input source (SetInputSource), for whoever steps the game in FixedUpdate's place
        // (SelfStepping off).
        public void StepTick() => StepTick(inputSource);

        // The complete-tick simulation entry point. One call is one tick, and the order below is the
        // contract: consumed input, runner, hazards, finish. FixedUpdate calls it
        // with the game's input source; a test or a rollback can call it directly, as often as it likes, with
        // another source (the phase must be Running, so the caller stops FixedUpdate with Time.timeScale = 0).
        public void StepTick(IInputSource source)
        {
            if (source == null) throw new System.ArgumentNullException(nameof(source));
            RequireTickStage(TickStage.Input);
            if (!IsRunning) return;
            try
            {
                if (!InputTick(source)) return;
                MoveTick();
                JudgeTick();
            }
            finally
            {
                // An input source may refuse a tick (network fault). Preserve that exception without leaving the
                // stage machine stuck: the original complete-tick entry point could be called again afterward.
                tickStage = TickStage.Input;
            }
        }

        // Raised at the end of every tick that ran, with that tick's number, whether it came from FixedUpdate or a direct
        // StepTick. The state is then between ticks: a snapshot taken here is the state after that tick, which a snapshot
        // taken while the input is being read (inside IInputSource.Consume) is not. The run may have ended during the tick.
        public event System.Action<int> TickCompleted;

        // The field performs each stage for every runner before beginning the next stage.
        public bool InputTick() => InputTick(inputSource);

        public bool InputTick(IInputSource source)
        {
            RequireTickStage(TickStage.Input);
            if (source == null) throw new System.ArgumentNullException(nameof(source));
            if (!IsRunning) return false;
            tickStage = TickStage.Movement;
            tick++;
            // The closed track looks for the runner near where it was, so a return leg is not mistaken for the start.
            if (circuit != null) circuit.PrepareTick();
            pendingInput = source.Consume();
            if (pendingInput.AttackPressed) Attack();
            return true;
        }

        public void MoveTick()
        {
            RequireTickStage(TickStage.Movement);
            tickStage = TickStage.Judgment;
            player.Step(pendingInput);
        }

        public void JudgeTick()
        {
            RequireTickStage(TickStage.Judgment);
            JudgeRunTick();
            tickStage = TickStage.Input;
            TickCompleted?.Invoke(tick);
        }

        private void RequireTickStage(TickStage expected)
        {
            if (tickStage != expected) throw new System.InvalidOperationException("ChainRushGame: tick stages must run in input, movement, judgment order.");
        }

        private void JudgeRunTick()
        {
            if (!IsRunning) return;
            Vector3 position = player.transform.position;
            for (int i = 0; i < targets.Length; i++)
                if (targets[i].Touches(position)) TakeDamage();
            if (!IsRunning) return;
            if (circuit != null)
            {
                circuit.Step(tick);
                if (circuit.IsFinished) CompleteRun();
            }
            else if (track.Project(position).S >= finishZ) CompleteRun();
        }

        // Everything the simulation carries from one tick to the next, as of now (between ticks). Only a circuit
        // race is rolled back (a network race runs on it), so other modes throw (DESIGN.md P3, DECISIONS "롤백 안전성").
        public SimSnapshot CaptureSnapshot()
        {
            RequireSnapshotSupport();
            if (phase != Phase.Running) throw new System.InvalidOperationException("ChainRushGame: take a snapshot while the run is going.");
            return new SimSnapshot(tick, racer.Capture(), player.transform.position, circuit.Capture());
        }

        // Puts the simulation back to a snapshot. A fall or a finish after the snapshot is undone too: the run is going again.
        public void RestoreSnapshot(in SimSnapshot snapshot)
        {
            RequireSnapshotSupport();
            player.SetPosition(snapshot.Position);
            racer.Restore(snapshot.Racer);
            circuit.Restore(snapshot.Circuit);
            tick = snapshot.Tick;
            phase = Phase.Running;
        }

        private void RequireSnapshotSupport()
        {
            if (circuit == null)
                throw new System.NotSupportedException("ChainRushGame: snapshots are only supported in a circuit race.");
        }

        // Resets the runner and camera to their start pose, ready without advancing simulation or counting down.
        public void ReturnToStart() => ResetRun();

        // Starts a run at once. Servers, rollbacks and tests start here; a player at the keyboard goes through StartCountdown.
        public void StartRun()
        {
            ResetRun();
            phase = Phase.Running;
        }

        // Puts the runner on the start line and counts 3, 2, 1 (CountdownSeconds, game time); then starts the run (StartRun,
        // which resets everything again, so the run begins exactly as one started at once) and raises CountdownFinished.
        // Calling it again while counting down starts the count over.
        public void StartCountdown()
        {
            ResetRun();
            countdownLeft = CountdownSeconds;
            phase = Phase.Countdown;
        }

        // Raised right after a countdown has started the run, before its first tick.
        public event System.Action CountdownFinished;

        private void ResetRun()
        {
            tickStage = TickStage.Input;
            phase = Phase.Ready;
            if (circuit != null) circuit.ResetRace();
            if (enhancedPresentation) { presentationAudio.ResetAudio(); presentationAnimation.ResetPose(); }
            player.ResetAtSpawn();
            followCamera.Snap();
            for (int i = 0; i < targets.Length; i++) targets[i].Restore();
            racer.Reset();
            player.FaceTrack();
            tick = 0;
            inputSource.Clear();
            attackVisual.gameObject.SetActive(false);
        }

        public void TogglePause()
        {
            if (phase == Phase.Running) phase = Phase.Paused;
            else if (phase == Phase.Paused) phase = Phase.Running;
        }

        public void RegisterGrapple() => racer.AddGrapple();

        public void Attack()
        {
            if (!IsRunning || !racer.CanAttack(tick)) return;
            racer.ShowAttack(tick);
            racer.BeginAttackCooldown(tick);
            PlayCue(RaceCue.Attack);
            for (int i = 0; i < targets.Length; i++)
                if (targets[i].TryHit(player.transform.position)) racer.AddHit();
        }

        public void TakeDamage()
        {
            if (!IsRunning || !racer.TryTakeDamage(tick)) return;
            PlayCue(RaceCue.Damage);
            if (racer.IsDown) FailRun();
        }

        public void FailRun()
        {
            if (!IsRunning) return;
            phase = Phase.Failed;
            grapple.Release(false);
            attackVisual.gameObject.SetActive(false);
        }

        // Ends the run as finished: a race when its last lap is done, the tutorial when its last stage is.
        public void CompleteRun()
        {
            if (!IsRunning) return;
            phase = Phase.Complete;
            grapple.Release(false);
            attackVisual.gameObject.SetActive(false);
            PlayCue(RaceCue.Finish);
        }

        // Every cue as it plays, whichever clips play it (a test listens here).
        public event System.Action<RaceCue> CuePlayed;

        // Only the first four cues (Jump..Damage) have a plain fallback clip; without the presentation any later one
        // sounds like Finish, as every move did before they had their own sounds (2026-11-05).
        public void PlayCue(RaceCue cue)
        {
            if (cue < RaceCue.Jump || cue > RaceCue.Respawn) throw new System.ArgumentOutOfRangeException(nameof(cue), cue, "ChainRushGame: no such cue.");
            CuePlayed?.Invoke(cue);
            if (enhancedPresentation) { presentationAudio.PlayCue(cue); return; }
            audioSource.PlayOneShot(cues[(int)cue < cues.Length ? (int)cue : (int)RaceCue.Finish]);
        }

        private void OnDestroy()
        {
            if (cues == null) return;
            for (int i = 0; i < cues.Length; i++) if (cues[i] != null) Destroy(cues[i]);
        }
    }
}
