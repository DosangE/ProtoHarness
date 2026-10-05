using UnityEngine;
using ProtoHarness.ChainRush.Track;
using ProtoHarness.ChainRush.Visuals;

namespace ProtoHarness.ChainRush
{
    [DefaultExecutionOrder(150)]
    public sealed class GrappleController : MonoBehaviour
    {
        [SerializeField] private ChainRushGame game;
        [SerializeField] private RunnerMotor motor;
        [SerializeField] private LineRenderer rope;
        [SerializeField] private Transform ropeOrigin;
        [SerializeField] private Transform[] anchors;
        [SerializeField] private float maxRange = 32f;
        [SerializeField] private float retractSpeed = 3f;
        [SerializeField] private ChainVisual chainVisual;
        private float visualExtension;
        private Vector3 lastAnchor;
        private Transform candidate;

        // Attachment, rope length and the miss deadline live in the racer's RacerState.
        public bool IsAttached => game.Racer.HasAnchor;
        public Vector3 AnchorPosition => IsAttached ? anchors[game.Racer.AnchorIndex].position : Vector3.zero;
        public Transform Candidate => candidate;
        public float RopeLength => game.Racer.RopeLength;
        public bool JustMissed => game.Tick < game.Racer.MissUntilTick;

        private void Awake()
        {
            if (game == null || motor == null || rope == null || ropeOrigin == null || anchors == null || anchors.Length == 0)
            {
                Debug.LogError("GrappleController: all references and at least one anchor are required.", this);
                enabled = false;
                return;
            }
            for (int i = 0; i < anchors.Length; i++)
                if (anchors[i] == null)
                {
                    Debug.LogError("GrappleController: anchor array contains a missing reference.", this);
                    enabled = false;
                    return;
                }
            if (game.IsEndless && chainVisual == null)
            {
                Debug.LogError("GrappleController: endless mode requires a chain visual.", this);
                enabled = false;
                return;
            }
            rope.positionCount = 2;
            rope.enabled = false;
        }

        private void OnValidate()
        {
            if (maxRange <= 5f || retractSpeed < 0f)
                Debug.LogError("GrappleController: range must exceed 5 and retract speed cannot be negative.", this);
        }

        private void LateUpdate()
        {
            if (game.IsPaused) return;
            candidate = AnchorAt(SelectCandidate());
            if (game.IsEndless)
            {
                if (IsAttached) lastAnchor = AnchorPosition;
                visualExtension = Mathf.MoveTowards(visualExtension, IsAttached ? 1f : 0f, Time.deltaTime * 8f);
                if (visualExtension > 0f) chainVisual.Present(lastAnchor, visualExtension);
                else chainVisual.Hide();
            }
            if (!IsAttached) return;
            rope.SetPosition(0, ropeOrigin.position);
            rope.SetPosition(1, AnchorPosition);
        }

        private Transform AnchorAt(int index) => index == RacerState.NoAnchor ? null : anchors[index];

        // Returns the best anchor's index, or RacerState.NoAnchor.
        private int SelectCandidate()
        {
            int best = RacerState.NoAnchor;
            float bestScore = float.PositiveInfinity;
            Vector3 origin = transform.position + Vector3.up * 0.4f;
            TrackFrame frame = game.Track.Frame(transform.position);
            for (int i = 0; i < anchors.Length; i++)
            {
                Vector3 offset = anchors[i].position - origin;
                // "Ahead" and "off to the side" are measured along the track, not world z/x.
                Vector3 local = frame.InverseTransformDirection(offset);
                if (local.z < 1f || local.y < 0f || offset.sqrMagnitude > maxRange * maxRange) continue;
                if (Physics.Linecast(origin, anchors[i].position, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                float score = offset.sqrMagnitude + local.x * local.x * 3f;
                if (score >= bestScore) continue;
                bestScore = score;
                best = i;
            }
            return best;
        }

        public bool TryAttach()
        {
            if (!game.IsRunning || motor.IsGrounded || IsAttached) return false;
            int index = SelectCandidate();
            candidate = AnchorAt(index);
            if (index == RacerState.NoAnchor)
            {
                game.Racer.MarkMiss(game.Tick + Ticks.FromSeconds(0.75f));
                return false;
            }
            game.Racer.Attach(index, Mathf.Max(5f, Vector3.Distance(transform.position, anchors[index].position)));
            rope.enabled = !game.IsEndless;
            game.RegisterGrapple();
            game.PlayCue(1);
            return true;
        }

        public void ConstrainMotion(Vector3 position, ref Vector3 displacement, ref Vector3 velocity, float dt)
        {
            if (!IsAttached) return;
            ref float ropeLength = ref game.Racer.RopeLength;
            ropeLength = Mathf.Max(5f, ropeLength - retractSpeed * dt);
            Vector3 anchor = AnchorPosition;
            Vector3 radial = position + displacement - anchor;
            if (radial.sqrMagnitude <= ropeLength * ropeLength) return;
            Vector3 normal = radial.normalized;
            displacement = anchor + normal * ropeLength - position;
            float outwardSpeed = Vector3.Dot(velocity, normal);
            if (outwardSpeed > 0f) velocity -= normal * outwardSpeed;
        }

        public void Release(bool boost)
        {
            if (!boost && game.IsEndless)
            {
                visualExtension = 0f;
                chainVisual.Hide();
            }
            if (!IsAttached) return;
            game.Racer.Detach();
            rope.enabled = false;
            if (boost && game.IsRunning) motor.AddReleaseBoost();
        }

        // Ticks restart at zero with each run, so a stale miss deadline must not carry over.
        public void ClearMiss() => game.Racer.ClearMiss();

        public void ShiftOrigin(Vector3 offset)
        {
            lastAnchor += offset;
            if (game.IsEndless) chainVisual.ShiftOrigin(offset);
        }
    }
}
