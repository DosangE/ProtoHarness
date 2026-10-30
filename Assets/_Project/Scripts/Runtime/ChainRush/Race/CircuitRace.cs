using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using ProtoHarness.ChainRush.Track;
using Debug = UnityEngine.Debug;

namespace ProtoHarness.ChainRush.Race
{
    // A hand-built circuit race for one runner (COURSE.md T4): builds the closed centerline from a
    // TrackDefinition, has CircuitScenery lay the road, the start and checkpoint bands and the poles once, hangs the
    // grapple anchors over the gaps, and counts laps from the runner's S every tick.
    // ChainRushGame takes the centerline from BuildTrack, calls PrepareTick before a tick's input is read
    // (the track then looks for the runner near where it was) and Step after the runner moved. The race
    // starts on tick 0 with the runner on the start line (S 0); lap 1 ends the next time it crosses the line.
    public sealed class CircuitRace : MonoBehaviour
    {
        [SerializeField] private ChainRushGame game;
        [SerializeField] private RunnerMotor player;
        [SerializeField] private TrackDefinition definition;
        [Tooltip("Pool of grapple anchors; the same array GrappleController.anchors uses. Extra ones are switched off.")]
        [SerializeField] private Transform[] anchors;
        [SerializeField] private Material roadMaterial;
        [SerializeField] private Material railMaterial;
        [SerializeField] private Material lightMaterial;
        [SerializeField] private float maxPieceLength = 50f;

        private readonly Stopwatch stepWatch = new Stopwatch();
        private Centerline track;
        private LapCounter counter;
        private CircuitScenery scenery;
        private List<(double Start, double End)> gaps;
        private bool initialized;
        private double lastS;

        // The race's state between ticks: the lap counter and where the runner was last seen.
        public struct Snapshot
        {
            public LapCounter.Snapshot Laps;
            public double LastS;
        }

        public LapCounter Laps => counter;
        public TrackDefinition Definition => definition;
        public Centerline Track => track;
        public int LapCount => definition.LapCount;
        public bool IsFinished => counter.IsFinished;
        // 0..1 over the whole race.
        public float RaceFraction => (float)counter.RaceFraction;
        // The runner's S (folded into one lap) at the last Step.
        public double LastS => lastS;
        public int PieceCount => scenery == null ? 0 : scenery.PieceCount;
        public int PoleCount => scenery == null ? 0 : scenery.PoleCount;
        public int AnchorPoolSize => anchors == null ? 0 : anchors.Length;
        public double MaxStepMilliseconds { get; private set; }

        public int ActiveAnchors
        {
            get
            {
                int active = 0;
                for (int i = 0; anchors != null && i < anchors.Length; i++) if (anchors[i].gameObject.activeSelf) active++;
                return active;
            }
        }

        private void Awake()
        {
            if (!Initialize()) enabled = false;
        }

        private void OnValidate()
        {
            if (maxPieceLength < 15f) Debug.LogError("CircuitRace: the longest road piece must be at least 15 m.", this);
        }

        private void OnDestroy() => scenery?.Destroy();

        // Races a different track than the one saved in the scene (the lobby's choice, RaceAssembler). The circuit is
        // built from the definition once, in the first Awake of this or the game, so it must be swapped before that.
        public void UseDefinition(TrackDefinition replacement)
        {
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            if (initialized) throw new InvalidOperationException("CircuitRace: the circuit is already built; set the definition before the scene's Awake.");
            definition = replacement;
        }

        // The game asks for the centerline in its Awake, which can run before this Awake, so the circuit is
        // made on the first call of either.
        public Centerline BuildTrack()
        {
            if (!Initialize()) throw new InvalidOperationException("CircuitRace is misconfigured; see the error logged before this.");
            return track;
        }

        // Called at the start of a tick, before the runner's input is read.
        public void PrepareTick() => track.SetFocus(lastS);

        // Called after the runner moved on this tick.
        public void Step(int tick)
        {
            stepWatch.Restart();
            lastS = track.Project(player.transform.position).S;
            counter.Update(lastS, tick);
            stepWatch.Stop();
            MaxStepMilliseconds = Math.Max(MaxStepMilliseconds, stepWatch.Elapsed.TotalMilliseconds);
        }

        // A new race: the clock is at tick 0 and the runner is on the start slot.
        public void ResetRace()
        {
            lastS = -definition.StartSlot(0);
            counter.Begin(lastS, 0);
            track.SetFocus(lastS);
            MaxStepMilliseconds = 0d;
        }

        public Snapshot Capture() => new Snapshot { Laps = counter.Capture(), LastS = lastS };

        public void Restore(in Snapshot snapshot)
        {
            counter.Restore(snapshot.Laps);
            lastS = snapshot.LastS;
            track.SetFocus(lastS);
        }

        public Transform AnchorAt(int index)
        {
            if (anchors == null || index < 0 || index >= anchors.Length) throw new ArgumentOutOfRangeException(nameof(index), index, "No such anchor in the pool.");
            return anchors[index];
        }

        // The first gap the runner has not fully crossed on this lap, or the first one of the next lap. S is
        // folded into the lap, so a gap after the seam comes back as S + lap length.
        public bool TryGetNextGap(double s, out double startS, out double endS, out bool hasAnchor)
        {
            startS = 0d;
            endS = 0d;
            hasAnchor = false;
            if (gaps.Count == 0) return false;
            double lap = definition.LapLength;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < gaps.Count; i++)
                {
                    double start = gaps[i].Start + pass * lap;
                    double end = gaps[i].End + pass * lap;
                    if (s > end) continue;
                    startS = start;
                    endS = end;
                    hasAnchor = HasAnchorIn(gaps[i]);
                    return true;
                }
            }
            return false;
        }

        private bool HasAnchorIn((double Start, double End) gap)
        {
            IReadOnlyList<TrackDefinition.Anchor> list = definition.Anchors;
            for (int i = 0; i < list.Count; i++)
                if (list[i].S >= gap.Start && list[i].S <= gap.End) return true;
            return false;
        }

        private bool Initialize()
        {
            if (initialized) return true;
            if (game == null || player == null || definition == null || anchors == null || anchors.Length == 0
                || roadMaterial == null || railMaterial == null || lightMaterial == null)
            {
                Debug.LogError("CircuitRace: game, player, definition, three materials and an anchor pool are required.", this);
                return false;
            }
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] != null) continue;
                Debug.LogError("CircuitRace: the anchor array contains a missing reference.", this);
                return false;
            }
            if (!definition.TryValidate(out string error))
            {
                Debug.LogError(error, definition);
                return false;
            }
            if (anchors.Length < definition.Anchors.Count)
            {
                Debug.LogError($"CircuitRace: the definition has {definition.Anchors.Count} anchors but the pool holds {anchors.Length}.", this);
                return false;
            }
            track = definition.BuildCenterline(Vector3.zero, 0f);
            var checkpoints = new double[definition.CheckpointCount];
            for (int i = 0; i < checkpoints.Length; i++) checkpoints[i] = definition.CheckpointS(i);
            counter = new LapCounter(definition.LapLength, checkpoints, definition.LapCount);
            gaps = definition.Gaps();
            scenery = new CircuitScenery(definition, track, transform, roadMaterial, railMaterial, lightMaterial, maxPieceLength);
            scenery.LayRoad();
            PlaceAnchors();
            scenery.PlaceMarkers(checkpoints);
            scenery.PlacePoles();
            scenery.PlaceBoostZones();
            Physics.SyncTransforms();
            initialized = true;
            ResetRace();
            return true;
        }

        private void PlaceAnchors()
        {
            IReadOnlyList<TrackDefinition.Anchor> list = definition.Anchors;
            for (int i = 0; i < anchors.Length; i++)
            {
                if (i >= list.Count)
                {
                    anchors[i].gameObject.SetActive(false);
                    continue;
                }
                anchors[i].position = track.FrameAt(list[i].S).TransformPoint(new Vector3(list[i].Offset, list[i].Height, 0f));
                anchors[i].gameObject.SetActive(true);
            }
        }
    }
}
