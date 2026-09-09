using UnityEngine;

namespace ProtoHarness.ChainRush.Visuals
{
    [DefaultExecutionOrder(200)]
    public sealed class ChainVisual : MonoBehaviour
    {
        [SerializeField] private ChainRushGame game;
        [SerializeField] private Transform origin;
        [SerializeField] private Transform hook;
        [SerializeField] private Transform[] links;
        [SerializeField] private LineRenderer core;
        private Vector3 endpoint;
        private bool visible;
        public bool IsVisible => visible;

        private void Awake()
        {
            if (game == null || origin == null || hook == null || core == null || links == null || links.Length == 0)
            {
                Debug.LogError("ChainVisual: game, origin, hook, core and pooled links are required.", this);
                enabled = false;
                return;
            }
            for (int i = 0; i < links.Length; i++)
                if (links[i] == null)
                {
                    Debug.LogError("ChainVisual: missing pooled link reference.", this);
                    enabled = false;
                    return;
                }
            core.positionCount = 2;
            for (int i = 0; i < links.Length; i++) links[i].localScale = Vector3.one * 1.35f;
            Hide();
        }

        public void Present(Vector3 target, float extension)
        {
            endpoint = Vector3.Lerp(origin.position, target, Mathf.Clamp01(extension));
            visible = true;
        }

        private void LateUpdate()
        {
            if (!visible || game.IsPaused) return;
            Vector3 delta = endpoint - origin.position;
            float length = delta.magnitude;
            if (length < 0.01f) { Hide(); return; }
            core.enabled = true;
            core.SetPosition(0, origin.position);
            core.SetPosition(1, endpoint);
            hook.gameObject.SetActive(true);
            hook.position = endpoint;
            Quaternion facing = Quaternion.LookRotation(delta);
            hook.rotation = facing;
            int count = Mathf.Clamp(Mathf.CeilToInt(length / 0.35f), 1, links.Length);
            for (int i = 0; i < links.Length; i++)
            {
                links[i].gameObject.SetActive(i < count);
                if (i >= count) continue;
                links[i].position = Vector3.Lerp(origin.position, endpoint, (i + 0.5f) / count);
                links[i].rotation = facing * Quaternion.AngleAxis(i % 2 * 90f, Vector3.forward);
            }
        }

        public void ShiftOrigin(Vector3 offset) => endpoint += offset;

        public void Hide()
        {
            visible = false;
            core.enabled = false;
            hook.gameObject.SetActive(false);
            for (int i = 0; i < links.Length; i++) links[i].gameObject.SetActive(false);
        }
    }
}
