using UnityEngine;
using UnityEngine.InputSystem;
using ProtoHarness.ChainRush.Endless;
using ProtoHarness.ChainRush.Combat;
using ProtoHarness.ChainRush.Control;

namespace ProtoHarness.ChainRush
{
    public sealed class ChainRushGame : MonoBehaviour
    {
        private enum Phase { Ready, Running, Paused, Failed, Complete }
        [SerializeField] private RunnerMotor player;
        [SerializeField] private GrappleController grapple;
        [SerializeField] private FollowCamera followCamera;
        [SerializeField] private CourseTarget[] targets;
        [SerializeField] private Transform attackVisual;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private float finishZ = 496f;
        [SerializeField] private bool endlessMode;
        [SerializeField] private EndlessCourse endlessCourse;
        [SerializeField] private EnemyDirector enemies;
        [SerializeField] private RunRules rules;
        [SerializeField] private bool enhancedPresentation;
        [SerializeField] private Audio.ChainRushAudio presentationAudio;
        [SerializeField] private Visuals.RunnerAnimation presentationAnimation;
        private Phase phase;
        private int health;
        private int hits;
        private int grapples;
        private int tick;
        private int damageUntilTick;
        private int attackUntilTick;
        private int nextAttackTick;
        private IInputSource inputSource;
        private AudioClip[] cues;

        public bool IsRunning => phase == Phase.Running;
        public bool IsPaused => phase == Phase.Paused;
        public bool IsReady => phase == Phase.Ready;
        public bool HasFailed => phase == Phase.Failed;
        public bool HasFinished => phase == Phase.Complete;
        public int Health => health;
        public int Hits => hits;
        public int Grapples => grapples;
        public int Tick => tick;
        public float Elapsed => Ticks.ToSeconds(tick);
        public float Progress => Mathf.Clamp01(player.transform.position.z / finishZ);
        public float FinishZ => finishZ;
        public bool DamageFlash => tick < damageUntilTick && IsRunning;
        public bool AttackActive => tick < attackUntilTick;
        public bool IsEndless => endlessMode;
        public bool HasPresentation => enhancedPresentation;
        public EnemyDirector Enemies => enemies;
        public double Distance => endlessMode ? endlessCourse.Distance : System.Math.Max(0d, player.transform.position.z - 5d);

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
            if (endlessMode && (endlessCourse == null || enemies == null))
            {
                Debug.LogError("ChainRushGame: endless mode requires course and enemy director.", this);
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
            health = rules.MaxHealth;
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

        private void OnValidate()
        {
            if (finishZ <= 0f) Debug.LogError("ChainRushGame: finishZ must be positive.", this);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) StartRun();
                else if (keyboard.enterKey.wasPressedThisFrame && !IsRunning && !IsPaused) StartRun();
                if (keyboard.escapeKey.wasPressedThisFrame) TogglePause();
            }
            if (!IsRunning) return;
            inputSource.Poll();
            float elapsed = Elapsed;
            for (int i = 0; i < targets.Length; i++) targets[i].Animate(elapsed);
            attackVisual.gameObject.SetActive(AttackActive);
            if (AttackActive) attackVisual.localScale = Vector3.one * (1f + Ticks.ToSeconds(attackUntilTick - tick) * 4f);
        }

        // The only simulation entry point. One call is one tick, and the order below is the
        // contract: consumed input, runner, hazards, finish, enemies, course recycling.
        private void FixedUpdate()
        {
            if (!IsRunning) return;
            tick++;
            TickInput input = inputSource.Consume();
            if (input.AttackPressed) Attack();
            player.Step(input);
            if (!IsRunning) return;
            Vector3 position = player.transform.position;
            for (int i = 0; i < targets.Length; i++)
                if (targets[i].Touches(position)) TakeDamage();
            if (!IsRunning) return;
            if (!endlessMode && position.z >= finishZ) CompleteRun();
            if (!IsRunning || !endlessMode) return;
            enemies.Step();
            if (!IsRunning) return;
            endlessCourse.Step();
        }

        public void StartRun()
        {
            phase = Phase.Ready;
            if (enhancedPresentation) { presentationAudio.ResetAudio(); presentationAnimation.ResetPose(); }
            if (endlessMode) endlessCourse.ResetCourse();
            player.ResetAtSpawn();
            followCamera.Snap();
            for (int i = 0; i < targets.Length; i++) targets[i].Restore();
            health = rules.MaxHealth;
            hits = 0;
            grapples = 0;
            tick = 0;
            damageUntilTick = 0;
            attackUntilTick = 0;
            nextAttackTick = 0;
            inputSource.Clear();
            if (endlessMode) enemies.ResetEncounters();
            attackVisual.gameObject.SetActive(false);
            phase = Phase.Running;
        }

        public void TogglePause()
        {
            if (phase == Phase.Running) phase = Phase.Paused;
            else if (phase == Phase.Paused) phase = Phase.Running;
        }

        public void RegisterGrapple() => grapples++;
        public void RegisterEnemyHit() { if (IsRunning) { hits++; PlayPresentationCue(5); } }

        public void Attack()
        {
            if (!IsRunning || tick < nextAttackTick) return;
            if (endlessMode)
            {
                if (enemies.TryAttack()) nextAttackTick = tick + rules.AttackCooldownTicks;
                return;
            }
            attackUntilTick = tick + rules.AttackVisualTicks;
            nextAttackTick = tick + rules.AttackCooldownTicks;
            PlayCue(2);
            for (int i = 0; i < targets.Length; i++)
                if (targets[i].TryHit(player.transform.position)) hits++;
        }

        public void TakeDamage()
        {
            if (!IsRunning || tick < damageUntilTick) return;
            health--;
            damageUntilTick = tick + rules.DamageInvulnerabilityTicks;
            PlayCue(3);
            if (health <= 0) FailRun();
        }

        public void FailRun()
        {
            if (!IsRunning) return;
            phase = Phase.Failed;
            grapple.Release(false);
            attackVisual.gameObject.SetActive(false);
            if (endlessMode) enemies.ClearEncounter();
        }

        private void CompleteRun()
        {
            if (!IsRunning) return;
            phase = Phase.Complete;
            grapple.Release(false);
            attackVisual.gameObject.SetActive(false);
            PlayCue(1);
        }

        public void PlayCue(int index)
        {
            if (index < 0 || index >= cues.Length) throw new System.ArgumentOutOfRangeException(nameof(index));
            if (enhancedPresentation) { presentationAudio.PlayCue(index); return; }
            audioSource.PlayOneShot(cues[index]);
        }

        public void PlayPresentationCue(int index)
        {
            if (enhancedPresentation) presentationAudio.PlayCue(index);
        }

        private void OnDestroy()
        {
            if (cues == null) return;
            for (int i = 0; i < cues.Length; i++) if (cues[i] != null) Destroy(cues[i]);
        }
    }
}
