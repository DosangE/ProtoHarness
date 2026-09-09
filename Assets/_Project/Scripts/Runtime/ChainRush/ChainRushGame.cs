using UnityEngine;
using UnityEngine.InputSystem;
using ProtoHarness.ChainRush.Endless;
using ProtoHarness.ChainRush.Combat;

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
        [SerializeField] private bool enhancedPresentation;
        [SerializeField] private Audio.ChainRushAudio presentationAudio;
        [SerializeField] private Visuals.RunnerAnimation presentationAnimation;
        private Phase phase;
        private int health = 3;
        private int hits;
        private int grapples;
        private float elapsed;
        private float damageUntil;
        private float attackUntil;
        private float nextAttackTime;
        private AudioClip[] cues;

        public bool IsRunning => phase == Phase.Running;
        public bool IsPaused => phase == Phase.Paused;
        public bool IsReady => phase == Phase.Ready;
        public bool HasFailed => phase == Phase.Failed;
        public bool HasFinished => phase == Phase.Complete;
        public int Health => health;
        public int Hits => hits;
        public int Grapples => grapples;
        public float Elapsed => elapsed;
        public float Progress => Mathf.Clamp01(player.transform.position.z / finishZ);
        public float FinishZ => finishZ;
        public bool DamageFlash => elapsed < damageUntil && IsRunning;
        public bool AttackActive => elapsed < attackUntil;
        public bool IsEndless => endlessMode;
        public bool HasPresentation => enhancedPresentation;
        public EnemyDirector Enemies => enemies;
        public double Distance => endlessMode ? endlessCourse.Distance : System.Math.Max(0d, player.transform.position.z - 5d);

        private void Awake()
        {
            if (player == null || grapple == null || followCamera == null || targets == null || attackVisual == null || audioSource == null)
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
            attackVisual.gameObject.SetActive(false);
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
                if (keyboard.spaceKey.wasPressedThisFrame && IsRunning) Attack();
            }
            if (!IsRunning) return;
            elapsed += Time.deltaTime;
            Vector3 position = player.transform.position;
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i].Animate(elapsed);
                if (targets[i].Touches(position)) TakeDamage();
            }
            attackVisual.gameObject.SetActive(AttackActive);
            if (AttackActive) attackVisual.localScale = Vector3.one * (1f + (attackUntil - elapsed) * 4f);
            if (!endlessMode && position.z >= finishZ) CompleteRun();
        }

        public void StartRun()
        {
            phase = Phase.Ready;
            if (enhancedPresentation) { presentationAudio.ResetAudio(); presentationAnimation.ResetPose(); }
            if (endlessMode) endlessCourse.ResetCourse();
            player.ResetAtSpawn();
            followCamera.Snap();
            for (int i = 0; i < targets.Length; i++) targets[i].Restore();
            health = 3;
            hits = 0;
            grapples = 0;
            elapsed = 0f;
            damageUntil = 0f;
            attackUntil = 0f;
            nextAttackTime = 0f;
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
            if (!IsRunning || elapsed < nextAttackTime) return;
            if (endlessMode)
            {
                if (enemies.TryAttack()) nextAttackTime = elapsed + 0.35f;
                return;
            }
            attackUntil = elapsed + 0.18f;
            nextAttackTime = elapsed + 0.35f;
            PlayCue(2);
            for (int i = 0; i < targets.Length; i++)
                if (targets[i].TryHit(player.transform.position)) hits++;
        }

        public void TakeDamage()
        {
            if (!IsRunning || elapsed < damageUntil) return;
            health--;
            damageUntil = elapsed + 1.25f;
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
