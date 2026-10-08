using UnityEngine;
using ProtoHarness.ChainRush.Endless;
using ProtoHarness.ChainRush.Track;
using ProtoHarness.ChainRush.Visuals;

namespace ProtoHarness.ChainRush.Combat
{
    public sealed class EnemyDirector : MonoBehaviour
    {
        public enum Entrance { Above, Left, Right }
        public enum EncounterState { Idle, Warning, Entering, Vulnerable, Firing, Retracting, Striking }
        [SerializeField] private ChainRushGame game;
        [SerializeField] private RunnerMotor player;
        [SerializeField] private CourseStream course;
        [SerializeField] private Transform enemy;
        [SerializeField] private Transform warning;
        [SerializeField] private Transform impact;
        [SerializeField] private ChainVisual chain;
        [SerializeField] private EncounterTuning tuning;
        private int timer;
        private int nextEncounterTick;
        private int sequence;
        private Vector3 entranceOffset;
        private Vector3 targetOffset;
        public EncounterState State { get; private set; }
        public Entrance Direction { get; private set; }
        public bool CanAttack => State == EncounterState.Vulnerable;
        public bool HasEncounter => State != EncounterState.Idle;
        public float Remaining => CanAttack ? Ticks.ToSeconds(RemainingTicks) : 0f;
        public float WindowFraction => CanAttack ? (float)RemainingTicks / tuning.AttackWindowTicks : 0f;
        private int RemainingTicks => Mathf.Max(0, tuning.AttackWindowTicks - timer);
        public Transform Target => enemy;
        public float EncounterDuration => tuning.EncounterDuration;

        private void Awake()
        {
            if (game == null || player == null || course == null || tuning == null || enemy == null || warning == null || impact == null || chain == null)
            {
                Debug.LogError("EnemyDirector: all scene references are required.", this);
                enabled = false;
                return;
            }
            // ChainVisual initializes independently; visual cleanup starts after all Awake calls.
            enemy.gameObject.SetActive(false);
            warning.gameObject.SetActive(false);
            impact.gameObject.SetActive(false);
        }

        // One simulation tick, called only by ChainRushGame.FixedUpdate.
        public void Step()
        {
            if (!game.IsRunning) return;
            if (State == EncounterState.Idle)
            {
                if (game.Tick >= nextEncounterTick && course.CanStartEncounter(EncounterDuration))
                    BeginEncounter((Entrance)(sequence++ % 3));
                return;
            }
            // Landing/jump inputs must never turn an enemy into a required mid-gap attack.
            if ((State == EncounterState.Warning || State == EncounterState.Entering || CanAttack) && !player.IsGrounded)
            {
                ClearEncounter();
                return;
            }
            timer++;
            float seconds = Ticks.ToSeconds(timer);
            Vector3 playerPosition = player.transform.position;
            TrackFrame frame = game.Track.Frame(playerPosition);
            Vector3 target = playerPosition + frame.TransformDirection(targetOffset);
            warning.position = target;
            switch (State)
            {
                case EncounterState.Warning:
                    warning.localScale = Vector3.one * (1f + Mathf.Sin(seconds * 25f) * 0.15f);
                    if (timer >= tuning.WarningTicks) { SetState(EncounterState.Entering); enemy.gameObject.SetActive(true); }
                    break;
                case EncounterState.Entering:
                    enemy.position = Vector3.Lerp(playerPosition + frame.TransformDirection(entranceOffset), target, Mathf.SmoothStep(0f, 1f, (float)timer / tuning.EntranceTicks));
                    enemy.rotation = Quaternion.LookRotation(frame.Forward);
                    if (timer >= tuning.EntranceTicks) { SetState(EncounterState.Vulnerable); game.PlayCue(1); }
                    break;
                case EncounterState.Vulnerable:
                    enemy.position = target;
                    enemy.rotation = Quaternion.LookRotation(frame.Forward) * Quaternion.Euler(0f, 0f, Mathf.Sin(seconds * 7f) * 8f);
                    if (timer >= tuning.AttackWindowTicks) { SetState(EncounterState.Striking); warning.gameObject.SetActive(false); }
                    break;
                case EncounterState.Firing:
                    enemy.position = target;
                    chain.Present(enemy.position, (float)timer / tuning.FlightTicks);
                    if (timer >= tuning.FlightTicks)
                    {
                        game.RegisterEnemyHit();
                        enemy.gameObject.SetActive(false);
                        impact.gameObject.SetActive(true);
                        SetState(EncounterState.Retracting);
                        game.PlayPresentationCue(6);
                    }
                    break;
                case EncounterState.Retracting:
                    impact.position = target;
                    impact.localScale = Vector3.one * (1f + seconds * 6f);
                    chain.Present(target, 1f - (float)timer / tuning.RecoveryTicks);
                    if (timer >= tuning.RecoveryTicks) ClearEncounter();
                    break;
                case EncounterState.Striking:
                    enemy.position = Vector3.Lerp(target, playerPosition, (float)timer / tuning.RecoveryTicks);
                    if (timer >= tuning.RecoveryTicks)
                    {
                        ClearEncounter();
                        game.TakeDamage();
                    }
                    break;
            }
        }

        public bool BeginEncounter(Entrance direction)
        {
            if (direction < Entrance.Above || direction > Entrance.Right) throw new System.ArgumentOutOfRangeException(nameof(direction));
            if (!game.IsRunning || HasEncounter || !course.CanStartEncounter(EncounterDuration)) return false;
            Direction = direction;
            game.PlayPresentationCue(4);
            float side = direction == Entrance.Left ? -1f : direction == Entrance.Right ? 1f : 0f;
            // Offsets are in track terms (x = right, y = up, z = ahead) and turned into world space each tick.
            targetOffset = new Vector3(side * 2f, 1.6f, 8f);
            entranceOffset = direction == Entrance.Above ? new Vector3(0f, 16f, 8f) : new Vector3(side * 17f, 1.6f, 8f);
            Vector3 playerPosition = player.transform.position;
            enemy.position = playerPosition + game.Track.Frame(playerPosition).TransformDirection(entranceOffset);
            warning.gameObject.SetActive(true);
            SetState(EncounterState.Warning);
            return true;
        }

        public bool TryAttack()
        {
            if (!game.IsRunning || !CanAttack || timer >= tuning.AttackWindowTicks) return false;
            warning.gameObject.SetActive(false);
            SetState(EncounterState.Firing);
            game.PlayCue(2);
            return true;
        }

        private void SetState(EncounterState state) { State = state; timer = 0; }

        public void ClearEncounter()
        {
            SetState(EncounterState.Idle);
            enemy.gameObject.SetActive(false);
            warning.gameObject.SetActive(false);
            impact.gameObject.SetActive(false);
            chain.Hide();
            nextEncounterTick = game.Tick + tuning.NextGapTicks(course.Distance);
        }

        public void ResetEncounters()
        {
            ClearEncounter();
            sequence = 0;
            nextEncounterTick = Ticks.FromSeconds(0.2f);
        }

        public void ShiftOrigin(Vector3 offset)
        {
            enemy.position += offset;
            warning.position += offset;
            impact.position += offset;
            chain.ShiftOrigin(offset);
        }
    }
}
