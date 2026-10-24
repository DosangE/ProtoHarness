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
        [SerializeField] private float maxRange = 20f;
        // The lowest the rope may let the runner (its center) hang over the track: standing height, so its feet stay above the
        // road. The rope never lets the runner further from the anchor than its length, so an anchor is offered only within
        // its height over the track less this. A longer rope hooked far behind the anchor is still slack while the runner
        // flies on at running speed, and it is let go at the bottom of the swing, in the gap (docs/DECISIONS.md 2026-10-19).
        [SerializeField] private float minHangHeight = 1.05f;
        [SerializeField] private float retractSpeed = 3f;
        // Reel speed once a chain action has empowered the current grapple.
        [SerializeField] private float empoweredRetractSpeed = 12f;
        private Transform candidate;
        private bool hasPoleCandidate;
        private Vector3 poleCandidate;
        private MetalChain steelChain;

        // Attachment, rope length and the miss deadline live in the racer's RacerState.
        public bool IsAttached => game.Racer.HasAnchor;
        public Vector3 AnchorPosition => IsAttached ? anchors[game.Racer.AnchorIndex].position : Vector3.zero;
        public Transform Candidate => candidate;
        // The corner pole a chain action would hook right now (its foot), for the HUD; refreshed every frame.
        public bool HasPoleCandidate => hasPoleCandidate;
        public Vector3 PoleCandidate => poleCandidate;
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
            rope.positionCount = 2;
            rope.enabled = false;
            steelChain = new MetalChain(rope.transform);
            // Keep the endpoint line as a presentation probe; the visible connection is the steel mesh.
            rope.widthMultiplier = 0.001f;
        }

        private void OnValidate()
        {
            if (maxRange <= 5f || retractSpeed < 0f || empoweredRetractSpeed < retractSpeed)
                Debug.LogError("GrappleController: range must exceed 5, retract speed cannot be negative, and the empowered reel cannot be slower.", this);
            if (minHangHeight < 0f)
                Debug.LogError("GrappleController: the lowest hanging height cannot be negative.", this);
        }

        // The chain shows for a grapple, a swing and the motor's slingshot pull.
        private void LateUpdate()
        {
            if (game.IsPaused) return;
            // A grapple already hooked in this time in the air leaves nothing to offer until the runner lands.
            candidate = game.Racer.AirGrappleUsed ? null : AnchorAt(SelectCandidate());
            bool swinging = game.IsRunning && !IsAttached && motor.IsSwinging;
            bool slinging = game.IsRunning && !IsAttached && !swinging && motor.IsSlingPulling;
            bool linked = IsAttached || swinging || slinging;
            hasPoleCandidate = game.IsRunning && !IsAttached && !swinging && motor.TryFindPole(out poleCandidate);
            // A corner swing's chain bites the band on the pole it circles (R5).
            Vector3 target = IsAttached ? AnchorPosition : swinging ? SwingLook.PoleBite(game.Racer.SwingCenter) : motor.SlingTarget;
            rope.enabled = linked;
            if (!linked) { steelChain.Hide(); return; }
            rope.SetPosition(0, ropeOrigin.position);
            rope.SetPosition(1, target);
            steelChain.Present(ropeOrigin.position, target);
        }

        private Transform AnchorAt(int index) => index == RacerState.NoAnchor ? null : anchors[index];

        // Returns the best anchor's index, or RacerState.NoAnchor.
        private int SelectCandidate()
        {
            int best = RacerState.NoAnchor;
            float bestScore = float.PositiveInfinity;
            Vector3 origin = transform.position + Vector3.up * 0.4f;
            TrackFrame frame = game.Track.Frame(transform.position);
            // Sight lines are checked in the physics world of this runner's scene (see RunnerMotor.SnapToGround).
            PhysicsScene world = gameObject.scene.GetPhysicsScene();
            for (int i = 0; i < anchors.Length; i++)
            {
                // A pooled anchor that is switched off is resting, not a hook point.
                if (!anchors[i].gameObject.activeInHierarchy) continue;
                Vector3 offset = anchors[i].position - origin;
                // "Ahead" and "off to the side" are measured along the track, not world z/x.
                Vector3 local = frame.InverseTransformDirection(offset);
                if (local.z < 1f || local.y < 0f || offset.sqrMagnitude > maxRange * maxRange) continue;
                // The rope is the distance from the runner (TryAttach), not from the sight line's origin.
                float hold = game.Track.Project(anchors[i].position).H - minHangHeight;
                if ((anchors[i].position - transform.position).sqrMagnitude > hold * hold || hold <= 0f) continue;
                // PhysicsScene has no Linecast; a line from origin to the anchor is a ray of that length along that offset.
                if (world.Raycast(origin, offset, offset.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                float score = offset.sqrMagnitude + local.x * local.x * 3f;
                if (score >= bestScore) continue;
                bestScore = score;
                best = i;
            }
            return best;
        }

        public bool TryAttach()
        {
            // One grapple per time in the air; a second press does nothing (a miss means no anchor in range).
            if (!game.IsRunning || motor.IsGrounded || IsAttached || game.Racer.AirGrappleUsed) return false;
            int index = SelectCandidate();
            candidate = AnchorAt(index);
            if (index == RacerState.NoAnchor)
            {
                game.Racer.MarkMiss(game.Tick + Ticks.FromSeconds(0.75f));
                return false;
            }
            game.Racer.Attach(index, Mathf.Max(5f, Vector3.Distance(transform.position, anchors[index].position)));
            rope.enabled = true;
            game.RegisterGrapple();
            game.PlayCue(1);
            return true;
        }

        public void ConstrainMotion(Vector3 position, ref Vector3 displacement, ref Vector3 velocity, float dt)
        {
            if (!IsAttached) return;
            ref float ropeLength = ref game.Racer.RopeLength;
            ropeLength = Mathf.Max(5f, ropeLength - (game.Racer.GrappleEmpowered ? empoweredRetractSpeed : retractSpeed) * dt);
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
            if (!IsAttached) return;
            // Detach clears the empowered flag, so read it first.
            bool empowered = game.Racer.GrappleEmpowered;
            game.Racer.Detach();
            rope.enabled = false;
            steelChain?.Hide();
            if (boost && game.IsRunning) motor.AddReleaseBoost(empowered);
        }

        // Ticks restart at zero with each run, so a stale miss deadline must not carry over.
        public void ClearMiss() => game.Racer.ClearMiss();

        private void OnDestroy() => steelChain?.Dispose();
    }
}
