using System;
using UnityEngine;
using ProtoHarness.ChainRush.Track;

namespace ProtoHarness.ChainRush.Endless
{
    // The endless run's course (COURSE.md T3c): a CourseStream generates modules from the seed onto the game's
    // centerline, a fixed pool of RoadPiece objects is rebuilt to cover its road spans, and a fixed pool of
    // scene anchors is placed at its grapple anchors (unused anchors are switched off, which GrappleController
    // skips). When the runner gets rebaseDistance away from the world origin horizontally, everything moves
    // back by the runner's horizontal position. Enemy encounters open only on rest modules.
    public sealed class EndlessCourse : MonoBehaviour
    {
        [SerializeField] private ChainRushGame game;
        [SerializeField] private RunnerMotor player;
        [SerializeField] private FollowCamera followCamera;
        [SerializeField] private CourseTuning tuning;
        [Tooltip("Course seed for the next run. The same seed always gives the same course.")]
        [SerializeField] private int seed = 1;
        [SerializeField] private Material roadMaterial;
        [SerializeField] private Material railMaterial;
        [SerializeField] private Material lightMaterial;
        [Tooltip("Scene objects moved onto grapple gaps; also listed in GrappleController.anchors.")]
        [SerializeField] private Transform[] anchors;
        [SerializeField] private int roadPieceCount = 32;
        [SerializeField] private float aheadDistance = 300f;
        [SerializeField] private float behindDistance = 60f;
        [SerializeField] private float rebaseDistance = 448f;

        private CourseStream stream;
        private RoadPiece[] pieces;
        private bool[] pieceUsed;
        // S of the anchor each scene anchor shows, NaN when free.
        private double[] anchorKeys;
        private int syncedRevision = -1;
        private bool checkedSetup;
        private bool setupValid;
        private int recycledCount;

        // The track's S counts from the run start and survives origin shifts, so it is the distance.
        public double Distance => Math.Max(0d, PlayerS - 5d);
        // Road pieces rebuilt for a new span after they had already shown another one.
        public int RecycledCount => recycledCount;
        public int RebaseCount { get; private set; }
        public int PoolSize => pieces != null ? pieces.Length : 0;
        public CourseStream Stream => stream;

        // Takes effect on the next ResetCourse (StartRun).
        public int Seed
        {
            get => seed;
            set => seed = value;
        }

        private void Awake() => Setup();

        private void OnValidate()
        {
            if (roadPieceCount < 1 || !(aheadDistance > 0f) || behindDistance < (float)CourseStream.LeadIn || !(rebaseDistance > 0f))
                Debug.LogError($"EndlessCourse: roadPieceCount must be positive, aheadDistance and rebaseDistance positive, behindDistance at least {CourseStream.LeadIn}.", this);
        }

        // Validates the references once and builds the road pool. ChainRushGame.Awake may call SeedTrack before
        // this component's own Awake, so both paths come here.
        private bool Setup()
        {
            if (checkedSetup) return setupValid;
            checkedSetup = true;
            if (game == null || player == null || followCamera == null || tuning == null || roadMaterial == null
                || railMaterial == null || lightMaterial == null || anchors == null || anchors.Length == 0)
            {
                Debug.LogError("EndlessCourse: game, player, camera, tuning, the three road materials and at least one anchor are required.", this);
                enabled = false;
                return false;
            }
            for (int i = 0; i < anchors.Length; i++)
                if (anchors[i] == null)
                {
                    Debug.LogError("EndlessCourse: anchor array contains a missing reference.", this);
                    enabled = false;
                    return false;
                }
            string problem = tuning.FindProblem();
            if (problem != null)
            {
                Debug.LogError($"EndlessCourse: course tuning '{tuning.name}' is invalid: {problem}", this);
                enabled = false;
                return false;
            }
            pieces = new RoadPiece[roadPieceCount];
            pieceUsed = new bool[roadPieceCount];
            for (int i = 0; i < pieces.Length; i++)
                pieces[i] = new RoadPiece("Road piece " + i, transform, roadMaterial, railMaterial, lightMaterial);
            anchorKeys = new double[anchors.Length];
            setupValid = true;
            return true;
        }

        // Starts the generated course on the game's centerline (called once from ChainRushGame.Awake).
        public void SeedTrack(Centerline track)
        {
            if (track == null) throw new ArgumentNullException(nameof(track));
            if (!Setup()) throw new InvalidOperationException("EndlessCourse is not set up; see the error logged above.");
            stream = new CourseStream(track, tuning, aheadDistance, behindDistance);
            Restart();
        }

        public void ResetCourse()
        {
            if (stream == null) throw new InvalidOperationException("EndlessCourse: SeedTrack must run before ResetCourse.");
            Restart();
            recycledCount = 0;
            RebaseCount = 0;
        }

        // One simulation tick, called only by ChainRushGame.FixedUpdate.
        public void Step()
        {
            if (!game.IsRunning) return;
            stream.Advance(PlayerS);
            Sync();
            Vector3 position = player.transform.position;
            var horizontal = new Vector3(position.x, 0f, position.z);
            if (horizontal.sqrMagnitude < rebaseDistance * rebaseDistance) return;
            Vector3 shift = -horizontal;
            for (int i = 0; i < pieces.Length; i++) pieces[i].ShiftOrigin(shift);
            for (int i = 0; i < anchors.Length; i++) anchors[i].position += shift;
            player.ShiftOrigin(shift);
            followCamera.ShiftOrigin(shift);
            game.Enemies.ShiftOrigin(shift);
            game.Track.ShiftOrigin(shift);
            RebaseCount++;
            Physics.SyncTransforms();
        }

        // Road left before the next gap: the runner's edge for jumps. Infinity when no gap is generated yet.
        public float DistanceToEdge() => (float)stream.DistanceToGap(PlayerS);

        // Encounters need a grounded runner on a rest with enough rest left to finish the encounter on it.
        public bool CanStartEncounter(float duration)
        {
            return player.IsGrounded && stream.RestRemaining(PlayerS) > Mathf.Max(10f, player.Speed) * duration + 5f;
        }

        private double PlayerS => game.Track.Project(player.transform.position).S;

        private void Restart()
        {
            stream.Reset((ulong)(uint)seed);
            for (int i = 0; i < pieces.Length; i++) pieces[i].Hide();
            for (int i = 0; i < anchors.Length; i++) Free(i);
            syncedRevision = -1;
            Sync();
            Physics.SyncTransforms();
        }

        // Matches pieces and anchors to the stream's spans and anchors by S: frees what the stream dropped, then
        // builds or places what is new. The pools never grow; running out is a setup error.
        private void Sync()
        {
            if (stream.Revision == syncedRevision) return;
            syncedRevision = stream.Revision;
            for (int i = 0; i < pieces.Length; i++)
                if (pieces[i].IsBuilt && FindSpan(pieces[i].FromS) < 0) pieces[i].Hide();
            for (int span = 0; span < stream.SpanCount; span++)
            {
                double from = stream.SpanFrom(span);
                if (FindPiece(from) >= 0) continue;
                int free = FindPiece(double.NaN);
                if (free < 0)
                    throw new InvalidOperationException($"EndlessCourse: all {pieces.Length} road pieces are in use for {stream.SpanCount} spans; raise roadPieceCount.");
                if (pieceUsed[free]) recycledCount++;
                pieceUsed[free] = true;
                pieces[free].Build(stream.Line, from, stream.SpanTo(span), stream.SpanProfile(span));
            }
            for (int i = 0; i < anchors.Length; i++)
                if (!double.IsNaN(anchorKeys[i]) && FindAnchor(anchorKeys[i]) < 0) Free(i);
            for (int anchor = 0; anchor < stream.AnchorCount; anchor++)
            {
                double key = stream.AnchorS(anchor);
                if (FindAnchorObject(key) >= 0) continue;
                int free = FindAnchorObject(double.NaN);
                if (free < 0)
                    throw new InvalidOperationException($"EndlessCourse: all {anchors.Length} anchors are in use for {stream.AnchorCount} grapple gaps; add anchors.");
                anchorKeys[free] = key;
                anchors[free].position = stream.AnchorPosition(anchor);
                anchors[free].gameObject.SetActive(true);
            }
        }

        private void Free(int anchor)
        {
            anchorKeys[anchor] = double.NaN;
            anchors[anchor].gameObject.SetActive(false);
        }

        private int FindSpan(double from)
        {
            for (int i = 0; i < stream.SpanCount; i++)
                if (stream.SpanFrom(i) == from) return i;
            return -1;
        }

        // The built piece showing the span starting at from, or with NaN the first hidden piece.
        private int FindPiece(double from)
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                if (double.IsNaN(from) ? !pieces[i].IsBuilt : pieces[i].IsBuilt && pieces[i].FromS == from) return i;
            }
            return -1;
        }

        private int FindAnchor(double key)
        {
            for (int i = 0; i < stream.AnchorCount; i++)
                if (stream.AnchorS(i) == key) return i;
            return -1;
        }

        // The scene anchor showing key, or with NaN the first free one.
        private int FindAnchorObject(double key)
        {
            for (int i = 0; i < anchorKeys.Length; i++)
                if (double.IsNaN(key) ? double.IsNaN(anchorKeys[i]) : anchorKeys[i] == key) return i;
            return -1;
        }
    }
}
