using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using ProtoHarness.ChainRush.Track;
using Debug = UnityEngine.Debug;

namespace ProtoHarness.ChainRush.Endless
{
    // Streams a generated course (COURSE.md T3c): CourseGenerator modules are appended to the game's
    // centerline up to AheadDistance in front of the runner, each module's road is laid with pooled
    // RoadPieces (a gap module is two spans, the gap itself has no road), and grapple anchors come from a
    // fixed pool that GrappleController already points at. Anything BehindDistance behind the runner goes
    // back to the pools. When the runner is far from the world origin everything is moved back by the
    // runner's horizontal position. Step runs once per simulation tick (ChainRushGame.FixedUpdate); at most
    // one road piece is rebuilt per tick, except while seeding a run.
    public sealed class ProceduralCourse : CourseStream
    {
        // The runner spawns 5 m along the first module, as it did on the straight course.
        private const double SpawnS = 5d;

        private struct Span
        {
            public double From;
            public double To;
            public RoadProfile Profile;
        }

        [SerializeField] private ChainRushGame game;
        [SerializeField] private RunnerMotor player;
        [SerializeField] private FollowCamera followCamera;
        [SerializeField] private CourseTuning tuning;
        [Tooltip("Pool of grapple anchors; the same array GrappleController.anchors uses.")]
        [SerializeField] private Transform[] anchors;
        [SerializeField] private Material roadMaterial;
        [SerializeField] private Material railMaterial;
        [SerializeField] private Material lightMaterial;
        [SerializeField] private int pieceCount = 24;
        [SerializeField] private float aheadDistance = 300f;
        [SerializeField] private float behindDistance = 60f;
        [SerializeField] private float anchorBehindDistance = 30f;
        [SerializeField] private float maxPieceLength = 50f;
        [SerializeField] private float shiftDistance = 400f;
        [Tooltip("Every run gets a new random seed unless a test calls SetSeed.")]
        [SerializeField] private bool randomSeed = true;
        [SerializeField] private int fixedSeed = 1;

        private readonly List<CourseModule> modules = new List<CourseModule>(16);
        private readonly Queue<Span> pending = new Queue<Span>(16);
        private readonly Stopwatch stepWatch = new Stopwatch();
        private RoadPiece[] pieces;
        private double[] anchorS;
        private int[] anchorOwner;
        private CourseGenerator generator;
        private Centerline track;
        private bool initialized;
        private bool hasPendingSeed;
        private ulong pendingSeed;

        // The seed of the current run.
        public ulong Seed { get; private set; }
        public int RebaseCount { get; private set; }
        // Slowest Step since the last seeding, in milliseconds.
        public double MaxStepMilliseconds { get; private set; }
        public int PoolSize => pieces == null ? 0 : pieces.Length;
        public int AnchorPoolSize => anchors == null ? 0 : anchors.Length;
        public int PendingBuilds => pending.Count;
        public int EscapeCount => generator == null ? 0 : generator.EscapeCount;
        public IReadOnlyList<CourseModule> Modules => modules;
        public override double Distance => Math.Max(0d, PlayerS - SpawnS);

        public int BuiltPieces
        {
            get
            {
                int built = 0;
                if (pieces != null)
                    for (int i = 0; i < pieces.Length; i++) if (pieces[i].IsBuilt) built++;
                return built;
            }
        }

        public int ActiveAnchors
        {
            get
            {
                int active = 0;
                if (anchorOwner != null)
                    for (int i = 0; i < anchorOwner.Length; i++) if (anchorOwner[i] >= 0) active++;
                return active;
            }
        }

        private double PlayerS => game.Track.Project(player.transform.position).S;

        private void Awake()
        {
            if (!Initialize()) enabled = false;
        }

        private void OnValidate()
        {
            if (pieceCount < 4 || aheadDistance <= 0f || behindDistance < 0f || anchorBehindDistance < 0f || maxPieceLength < 15f || shiftDistance <= 0f)
                Debug.LogError("ProceduralCourse: needs at least 4 pieces, positive ahead and shift distances, non-negative behind distances and a piece length of at least 15 m.", this);
            if (anchors != null && anchors.Length > 0 && anchors.Length < 8)
                Debug.LogError("ProceduralCourse: the anchor pool needs at least 8 entries (330 m window / 44 m shortest grapple module, rounded up).", this);
        }

        private void OnDestroy()
        {
            if (pieces == null) return;
            for (int i = 0; i < pieces.Length; i++) pieces[i].Destroy();
        }

        // The pool is made on the first call: ChainRushGame.Awake seeds the track before this Awake may have run.
        private bool Initialize()
        {
            if (initialized) return true;
            if (game == null || player == null || followCamera == null || tuning == null || anchors == null || anchors.Length == 0
                || roadMaterial == null || railMaterial == null || lightMaterial == null)
            {
                Debug.LogError("ProceduralCourse: game, player, camera, tuning, three materials and at least one anchor are required.", this);
                return false;
            }
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] != null) continue;
                Debug.LogError("ProceduralCourse: the anchor array contains a missing reference.", this);
                return false;
            }
            if (!tuning.TryValidate(out string error))
            {
                Debug.LogError(error, tuning);
                return false;
            }
            pieces = new RoadPiece[pieceCount];
            for (int i = 0; i < pieces.Length; i++) pieces[i] = new RoadPiece("Road piece " + i, transform, roadMaterial, railMaterial, lightMaterial);
            anchorS = new double[anchors.Length];
            anchorOwner = new int[anchors.Length];
            initialized = true;
            ClearState();
            return true;
        }

        // The next ResetCourse (the next run) uses this seed instead of a random one.
        public void SetSeed(ulong seed)
        {
            pendingSeed = seed;
            hasPendingSeed = true;
        }

        // One simulation tick, called only by ChainRushGame.FixedUpdate.
        public override void Step()
        {
            if (!game.IsRunning) return;
            stepWatch.Restart();
            double s = PlayerS;
            while (track.EndS < s + aheadDistance) AppendModule();
            Recycle(s);
            if (pending.Count > 0) BuildNext();
            track.TrimBefore(s - behindDistance - 20d);
            Rebase();
            stepWatch.Stop();
            MaxStepMilliseconds = Math.Max(MaxStepMilliseconds, stepWatch.Elapsed.TotalMilliseconds);
        }

        // Restarts the centerline at its origin and lays the road around the spawn for a fresh generator.
        public override void SeedTrack(Centerline track)
        {
            if (track == null) throw new ArgumentNullException(nameof(track));
            if (!Initialize()) throw new InvalidOperationException("ProceduralCourse is misconfigured; see the error logged before this.");
            this.track = track;
            track.Clear();
            ClearState();
            Seed = hasPendingSeed ? pendingSeed : (randomSeed ? unchecked((ulong)(uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue)) : (ulong)fixedSeed);
            hasPendingSeed = false;
            generator = new CourseGenerator(Seed, tuning);
            while (track.EndS < SpawnS + aheadDistance) AppendModule();
            while (pending.Count > 0) BuildNext();
            Physics.SyncTransforms();
        }

        public override void ResetCourse() => SeedTrack(game.Track);

        // Metres of solid road ahead of the runner before the next gap (or the end of the laid road), or -1
        // while the runner is over a gap.
        public float DistanceToEdge()
        {
            double s = PlayerS;
            if (TryGetNextGap(out double startS, out double endS, out _))
                return s >= startS ? -1f : (float)(startS - s);
            return (float)(track.EndS - s);
        }

        // The first gap the runner has not fully crossed, among the modules currently laid.
        public bool TryGetNextGap(out double startS, out double endS, out bool hasAnchor)
        {
            double s = PlayerS;
            for (int i = 0; i < modules.Count; i++)
            {
                CourseModule module = modules[i];
                if (!module.HasGap) continue;
                double gapEnd = module.GapStartS + module.GapLength;
                if (s > gapEnd) continue;
                startS = module.GapStartS;
                endS = gapEnd;
                hasAnchor = module.HasAnchor;
                return true;
            }
            startS = 0d;
            endS = 0d;
            hasAnchor = false;
            return false;
        }

        public Transform AnchorAt(int index)
        {
            if (anchors == null || index < 0 || index >= anchors.Length) throw new ArgumentOutOfRangeException(nameof(index), index, "No such anchor in the pool.");
            return anchors[index];
        }

        // True while the anchor belongs to a laid grapple module (the pool's other entries are switched off).
        public bool IsAnchorActive(int index)
        {
            if (anchorOwner == null || index < 0 || index >= anchorOwner.Length) throw new ArgumentOutOfRangeException(nameof(index), index, "No such anchor in the pool.");
            return anchorOwner[index] >= 0;
        }

        public override bool CanStartEncounter(float duration)
        {
            return player.IsGrounded && DistanceToEdge() > Mathf.Max(10f, player.Speed) * duration + 5f;
        }

        private void ClearState()
        {
            pending.Clear();
            modules.Clear();
            for (int i = 0; i < pieces.Length; i++) pieces[i].Hide();
            for (int i = 0; i < anchors.Length; i++)
            {
                anchors[i].gameObject.SetActive(false);
                anchorOwner[i] = -1;
            }
            RebaseCount = 0;
            MaxStepMilliseconds = 0d;
        }

        private void AppendModule()
        {
            CourseModule module = generator.Next();
            module.AppendTo(track);
            modules.Add(module);
            var profile = new RoadProfile(RoadProfile.DeckHalfWidth, RoadProfile.DeckThickness, !module.LeftOpen, !module.RightOpen);
            if (module.HasGap)
            {
                QueueRange(module.StartS, module.GapStartS, profile);
                QueueRange(module.GapStartS + module.GapLength, module.EndS, profile);
            }
            else
            {
                QueueRange(module.StartS, module.EndS, profile);
            }
            if (module.HasAnchor) PlaceAnchor(module);
        }

        // Splits [from, to] into equal spans of at most maxPieceLength.
        private void QueueRange(double from, double to, RoadProfile profile)
        {
            int count = Math.Max(1, (int)Math.Ceiling((to - from) / maxPieceLength - 1e-9));
            for (int i = 0; i < count; i++)
            {
                pending.Enqueue(new Span
                {
                    From = from + (to - from) * i / count,
                    To = i == count - 1 ? to : from + (to - from) * (i + 1) / count,
                    Profile = profile,
                });
            }
        }

        private void BuildNext()
        {
            RoadPiece free = null;
            for (int i = 0; i < pieces.Length && free == null; i++)
                if (!pieces[i].IsBuilt) free = pieces[i];
            if (free == null)
                throw new InvalidOperationException($"ProceduralCourse: all {pieces.Length} road pieces are in use and {pending.Count} spans are waiting. Raise pieceCount or shorten the ahead distance.");
            Span span = pending.Dequeue();
            free.Build(track, span.From, span.To, span.Profile);
        }

        private void PlaceAnchor(CourseModule module)
        {
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchorOwner[i] >= 0) continue;
                anchorOwner[i] = module.Index;
                anchorS[i] = module.AnchorS;
                anchors[i].position = track.FrameAt(module.AnchorS).TransformPoint(new Vector3(module.AnchorOffset, module.AnchorHeight, 0f));
                anchors[i].gameObject.SetActive(true);
                return;
            }
            throw new InvalidOperationException($"ProceduralCourse: all {anchors.Length} grapple anchors are in use when module {module.Index} needs one. Raise the anchor pool.");
        }

        // Pieces, modules and anchors that are far enough behind the runner go back to their pools. An anchor
        // the runner is still hanging from stays.
        private void Recycle(double s)
        {
            for (int i = 0; i < pieces.Length; i++)
                if (pieces[i].IsBuilt && pieces[i].ToS < s - behindDistance) pieces[i].Hide();
            int drop = 0;
            while (drop < modules.Count && modules[drop].EndS < s - behindDistance) drop++;
            if (drop > 0) modules.RemoveRange(0, drop);
            RacerState racer = game.Racer;
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchorOwner[i] < 0 || anchorS[i] >= s - anchorBehindDistance) continue;
                if (racer.HasAnchor && racer.AnchorIndex == i) continue;
                anchors[i].gameObject.SetActive(false);
                anchorOwner[i] = -1;
            }
        }

        // Far from the origin the whole scene moves back by the runner's horizontal position.
        private void Rebase()
        {
            Vector3 position = player.transform.position;
            var flat = new Vector3(position.x, 0f, position.z);
            if (flat.sqrMagnitude < shiftDistance * shiftDistance) return;
            Vector3 shift = -flat;
            player.ShiftOrigin(shift);
            followCamera.ShiftOrigin(shift);
            game.Enemies.ShiftOrigin(shift);
            track.ShiftOrigin(shift);
            for (int i = 0; i < pieces.Length; i++)
                if (pieces[i].IsBuilt) pieces[i].ShiftOrigin(shift);
            for (int i = 0; i < anchors.Length; i++) anchors[i].position += shift;
            RebaseCount++;
            Physics.SyncTransforms();
        }
    }
}
