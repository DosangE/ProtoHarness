using System;
using ProtoHarness.ChainRush.Audio;
using UnityEngine;

namespace ProtoHarness.ChainRush.Tutorial
{
    // Runs the tutorial's stages (TutorialCourse) on the race scene: judges the current goal after every tick, shows its
    // beacon, and between stages briefs the next one from the spawn. RaceAssembler adds it in a tutorial. Goal judging
    // only reads the simulation; a restart from the pause menu counts in the same stage again without briefing.
    // After the last stage the run completes and the HUD shows the end popup.
    public sealed class TutorialDirector : MonoBehaviour
    {
        // How long "STAGE CLEAR" stays up before the next stage's briefing.
        private const float ClearSeconds = 1.5f;
        private const float BeaconHeight = 30f;
        private const float BeaconThickness = 0.7f;
        private const float ZoneHeight = 0.04f;
        private static readonly Color BeaconColor = new Color(1f, 0.78f, 0.22f);

        private ChainRushGame game;
        private RunnerMotor player;
        private Material beaconMaterial;
        private Transform beacon;
        private Transform zone;
        private int stage;
        private int goal;
        private bool cleared;
        private float clearLeft;
        private float swung;
        private bool wasSwinging;
        private Vector3 lastRadial;

        public int StageIndex => stage;
        public int StageCount => TutorialCourse.Stages.Count;
        public int GoalIndex => goal;
        public TutorialCourse.Stage CurrentStage => TutorialCourse.Stages[stage];
        public TutorialCourse.Goal CurrentGoal => CurrentStage.Goals[goal];
        public string GoalText => TutorialCourse.GoalText(CurrentGoal, player.Gauge, game.Racer.SlingTicks > 0);
        public bool IsBriefing { get; private set; }
        public int PageIndex { get; private set; }
        public int PageCount => CurrentStage.Pages.Count;
        public TutorialCourse.Page CurrentPage => CurrentStage.Pages[PageIndex];
        // Every goal of the stage is done; the next stage has not begun.
        public bool IsStageCleared => cleared;
        // Every stage is done: the run has completed and the popup is up.
        public bool IsComplete { get; private set; }
        // 0..1 over the stage's goals.
        public float StageProgress => cleared ? 1f : (float)goal / CurrentStage.Goals.Count;

        public void Initialize(ChainRushGame raceGame, RunnerMotor runner, Material light, int firstStage)
        {
            if (raceGame == null) throw new ArgumentNullException(nameof(raceGame));
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (light == null) throw new ArgumentNullException(nameof(light));
            if (firstStage < 0 || firstStage >= StageCount)
                throw new ArgumentOutOfRangeException(nameof(firstStage), firstStage, $"TutorialDirector: stages run 0 to {StageCount - 1}.");
            game = raceGame;
            player = runner;
            stage = firstStage;
            IsBriefing = true;
            game.ManualStart = false;
            MakeBeacon(light);
            ShowGoal();
            game.TickCompleted += Judge;
        }

        private void OnDestroy()
        {
            if (game != null)
            {
                game.TickCompleted -= Judge;
            }
            if (beaconMaterial != null) Destroy(beaconMaterial);
        }

        public void NextPage()
        {
            if (IsBriefing && PageIndex + 1 < PageCount) PageIndex++;
        }

        public void PreviousPage()
        {
            if (IsBriefing && PageIndex > 0) PageIndex--;
        }

        public void CloseBriefing()
        {
            if (!IsBriefing) return;
            IsBriefing = false;
            game.ManualStart = true;
            game.StartRun();
            BeginStage();
        }

        // Restart the current stage immediately, without repeating the briefing or counting down.
        public void RestartStage()
        {
            goal = 0;
            cleared = false;
            IsComplete = false;
            IsBriefing = false;
            game.ManualStart = true;
            game.StartRun();
            BeginStage();
            ShowGoal();
        }

        // A run has just started from the spawn: forget the previous swing, including on a pause-menu restart.
        private void BeginStage()
        {
            swung = 0f;
            wasSwinging = false;
        }

        private void Update()
        {
            if (!cleared || !game.IsRunning) return;
            clearLeft -= Time.deltaTime;
            if (clearLeft > 0f) return;
            if (stage + 1 >= StageCount)
            {
                IsComplete = true;
                // Enter closes the popup (ChainRushHud), so it must not also start a run.
                game.ManualStart = false;
                game.CompleteRun();
                return;
            }
            stage++;
            goal = 0;
            cleared = false;
            ShowGoal();
            PageIndex = 0;
            IsBriefing = true;
            game.ManualStart = false;
            game.ReturnToStart();
        }

        // After a tick, so the goal is judged on where it left the runner.
        private void Judge(int tick)
        {
            TutorialCourse.Reading now = Read();
            if (cleared || !TutorialCourse.IsMet(CurrentGoal, now)) return;
            game.PlayCue(RaceCue.Finish);
            if (goal + 1 < CurrentStage.Goals.Count)
            {
                goal++;
                ShowGoal();
                return;
            }
            cleared = true;
            clearLeft = ClearSeconds;
            ShowGoal();
        }

        private TutorialCourse.Reading Read()
        {
            bool swinging = player.IsSwinging;
            bool released = wasSwinging && !swinging;
            Vector3 radial = player.transform.position - TutorialCourse.Pole;
            radial.y = 0f;
            // A new hook starts its own count; the swing's own centre is the only pole there is.
            if (swinging && !wasSwinging) swung = 0f;
            else if (swinging) swung += Mathf.Abs(Vector3.SignedAngle(lastRadial, radial, Vector3.up));
            else swung = 0f;
            lastRadial = radial;
            wasSwinging = swinging;
            RacerState racer = game.Racer;
            return new TutorialCourse.Reading
            {
                Position = player.transform.position,
                Speed = player.HorizontalSpeed,
                ForwardSpeed = player.ForwardSpeed,
                SwungDegrees = swung,
                RopeTaken = swinging ? racer.SwingHookRadius - racer.SwingRadius : 0f,
                Released = released,
                Gauge = player.Gauge,
                Boosting = racer.SlingTicks > 0,
            };
        }

        // A tall column to steer by and a flat disc the size of the zone, visual only like the corner poles.
        private void MakeBeacon(Material light)
        {
            beaconMaterial = new Material(light) { name = "Tutorial beacon", color = BeaconColor };
            if (beaconMaterial.HasProperty("_EmissionColor")) beaconMaterial.SetColor("_EmissionColor", BeaconColor);
            beacon = new GameObject("Tutorial beacon").transform;
            beacon.SetParent(transform, false);
            Transform column = Part("Column");
            column.localPosition = Vector3.up * (BeaconHeight * 0.5f);
            // The default cylinder is 2 m tall and 1 m across, centred on its origin.
            column.localScale = new Vector3(BeaconThickness, BeaconHeight * 0.5f, BeaconThickness);
            zone = Part("Zone");
            zone.localPosition = Vector3.up * ZoneHeight;
        }

        private Transform Part(string name)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = name;
            // Visual only: it must never collide, even in the frame before the collider is destroyed.
            Collider collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial = beaconMaterial;
            part.transform.SetParent(beacon, false);
            return part.transform;
        }

        private void ShowGoal()
        {
            TutorialCourse.Goal now = CurrentGoal;
            bool show = now.HasZone && !cleared;
            beacon.gameObject.SetActive(show);
            if (!show) return;
            beacon.position = now.Point;
            zone.localScale = new Vector3(now.Radius * 2f, ZoneHeight * 0.5f, now.Radius * 2f);
        }
    }
}
