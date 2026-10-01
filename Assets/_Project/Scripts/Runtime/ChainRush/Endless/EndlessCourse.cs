using UnityEngine;

namespace ProtoHarness.ChainRush.Endless
{
    public sealed class EndlessCourse : MonoBehaviour
    {
        [SerializeField] private ChainRushGame game;
        [SerializeField] private RunnerMotor player;
        [SerializeField] private FollowCamera followCamera;
        [SerializeField] private Transform[] chunks;
        [SerializeField] private float segmentLength = 56f;
        [SerializeField] private float deckLength = 40f;
        private Vector3[] initialPositions;
        private double originDistance;
        private int recycledCount;
        public double Distance => System.Math.Max(0d, originDistance + player.transform.position.z - 5d);
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
        public void Step()
        {
            if (!game.IsRunning) return;
            float z = player.transform.position.z;
            float span = chunks.Length * segmentLength;
            for (int i = 0; i < chunks.Length; i++)
                while (chunks[i].position.z + deckLength < z - segmentLength)
                {
                    chunks[i].position += Vector3.forward * span;
                    recycledCount++;
                }
            if (z < span) return;
            Vector3 shift = Vector3.back * span;
            for (int i = 0; i < chunks.Length; i++) chunks[i].position += shift;
            player.ShiftOrigin(shift);
            followCamera.ShiftOrigin(shift);
            game.Enemies.ShiftOrigin(shift);
            originDistance += span;
            RebaseCount++;
            Physics.SyncTransforms();
        }

        public float DistanceToEdge()
        {
            float z = player.transform.position.z;
            for (int i = 0; i < chunks.Length; i++)
            {
                float start = chunks[i].position.z;
                if (z >= start && z <= start + deckLength) return start + deckLength - z;
            }
            return -1f;
        }

        public bool CanStartEncounter(float duration)
        {
            return player.IsGrounded && DistanceToEdge() > Mathf.Max(10f, player.Speed) * duration + 5f;
        }

        public void ResetCourse()
        {
            for (int i = 0; i < chunks.Length; i++) chunks[i].position = initialPositions[i];
            originDistance = 0d;
            recycledCount = 0;
            RebaseCount = 0;
            Physics.SyncTransforms();
        }
    }
}
