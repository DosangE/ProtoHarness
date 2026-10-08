using UnityEngine;
using ProtoHarness.ChainRush.Track;

namespace ProtoHarness.ChainRush.Endless
{
    public sealed class EndlessCourse : CourseStream
    {
        [SerializeField] private ChainRushGame game;
        [SerializeField] private RunnerMotor player;
        [SerializeField] private FollowCamera followCamera;
        [SerializeField] private Transform[] chunks;
        [SerializeField] private float segmentLength = 56f;
        [SerializeField] private float deckLength = 40f;
        private Vector3[] initialPositions;
        private int recycledCount;
        // The track's S counts from the run start and survives origin shifts, so it is the distance.
        public override double Distance => System.Math.Max(0d, PlayerS - 5d);
        public int RecycledCount => recycledCount;
        public int RebaseCount { get; private set; }
        public int PoolSize => chunks.Length;

        private void Awake()
        {
            if (game == null || player == null || followCamera == null || chunks == null || chunks.Length < 4)
            {
                Debug.LogError("EndlessCourse: game, player, camera and at least four chunks are required.", this);
                enabled = false;
                return;
            }
            initialPositions = new Vector3[chunks.Length];
            for (int i = 0; i < chunks.Length; i++)
            {
                if (chunks[i] == null)
                {
                    Debug.LogError("EndlessCourse: missing chunk reference.", this);
                    enabled = false;
                    return;
                }
                initialPositions[i] = chunks[i].position;
            }
        }

        private void OnValidate()
        {
            if (deckLength <= 0f || segmentLength <= deckLength)
                Debug.LogError("EndlessCourse: segment length must exceed positive deck length.", this);
        }

        // One simulation tick, called only by ChainRushGame.FixedUpdate.
        public override void Step()
        {
            if (!game.IsRunning) return;
            Centerline track = game.Track;
            double s = PlayerS;
            float span = chunks.Length * segmentLength;
            // Keep the centerline one pool span ahead and a little behind; chunks sit on it.
            while (track.EndS < s + span) track.AppendStraight(segmentLength);
            track.TrimBefore(s - 2d * segmentLength);
            for (int i = 0; i < chunks.Length; i++)
                while (track.Project(chunks[i].position).S + deckLength < s - segmentLength)
                {
                    chunks[i].position += track.Frame(chunks[i].position).Forward * span;
                    recycledCount++;
                }
            Vector3 position = player.transform.position;
            Vector3 forward = track.Frame(position).Forward;
            if (Vector3.Dot(position, forward) < span) return;
            Vector3 shift = forward * -span;
            for (int i = 0; i < chunks.Length; i++) chunks[i].position += shift;
            player.ShiftOrigin(shift);
            followCamera.ShiftOrigin(shift);
            game.Enemies.ShiftOrigin(shift);
            track.ShiftOrigin(shift);
            RebaseCount++;
            Physics.SyncTransforms();
        }

        // Restarts the centerline at its origin with one straight piece; Step extends it as the runner advances.
        public override void SeedTrack(Centerline track)
        {
            if (track == null) throw new System.ArgumentNullException(nameof(track));
            track.Clear();
            track.AppendStraight(segmentLength);
        }

        public float DistanceToEdge()
        {
            Centerline track = game.Track;
            double s = PlayerS;
            for (int i = 0; i < chunks.Length; i++)
            {
                double start = track.Project(chunks[i].position).S;
                if (s >= start && s <= start + deckLength) return (float)(start + deckLength - s);
            }
            return -1f;
        }

        private double PlayerS => game.Track.Project(player.transform.position).S;

        public override bool CanStartEncounter(float duration)
        {
            return player.IsGrounded && DistanceToEdge() > Mathf.Max(10f, player.Speed) * duration + 5f;
        }

        public override void ResetCourse()
        {
            for (int i = 0; i < chunks.Length; i++) chunks[i].position = initialPositions[i];
            SeedTrack(game.Track);
            recycledCount = 0;
            RebaseCount = 0;
            Physics.SyncTransforms();
        }
    }
}
