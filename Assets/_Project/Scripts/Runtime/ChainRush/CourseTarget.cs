using UnityEngine;

namespace ProtoHarness.ChainRush
{
    public sealed class CourseTarget : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private bool destructible;
        [SerializeField] private Vector3 contactHalfSize = Vector3.one;
        private bool destroyed;
        private float phase;

        public bool IsDestroyed => destroyed;
        public bool IsDestructible => destructible;

        private void Awake()
        {
            if (visual == null)
            {
                Debug.LogError("CourseTarget: visual is required.", this);
                enabled = false;
                return;
            }
            phase = transform.position.z;
        }

        private void OnValidate()
        {
            if (contactHalfSize.x <= 0f || contactHalfSize.y <= 0f || contactHalfSize.z <= 0f)
                Debug.LogError("CourseTarget: contact half sizes must be positive.", this);
        }

        public void Animate(float elapsed)
        {
            if (destroyed || !destructible) return;
            visual.localRotation = Quaternion.Euler(0f, elapsed * 70f + phase, 45f);
            Vector3 position = visual.localPosition;
            position.y = Mathf.Sin(elapsed * 3f + phase) * 0.18f;
            visual.localPosition = position;
        }

        public bool Touches(Vector3 playerPosition)
        {
            if (destroyed) return false;
            Vector3 delta = playerPosition - transform.position;
            return Mathf.Abs(delta.x) < contactHalfSize.x + 0.4f &&
                Mathf.Abs(delta.y) < contactHalfSize.y + 0.7f && Mathf.Abs(delta.z) < contactHalfSize.z + 0.4f;
        }

        public bool TryHit(Vector3 playerPosition)
        {
            if (!destructible || destroyed) return false;
            Vector3 delta = transform.position - playerPosition;
            if (delta.z < -0.5f || delta.z > 5f || Mathf.Abs(delta.x) > 2.5f || Mathf.Abs(delta.y) > 2.5f) return false;
            destroyed = true;
            visual.gameObject.SetActive(false);
            return true;
        }

        public void Restore()
        {
            destroyed = false;
            visual.gameObject.SetActive(true);
        }
    }
}
