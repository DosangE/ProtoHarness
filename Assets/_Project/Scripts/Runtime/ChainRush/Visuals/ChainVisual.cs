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
        private MetalChain steelChain;
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
            steelChain = new MetalChain(transform);
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
            core.enabled = false;
            core.SetPosition(0, origin.position);
            core.SetPosition(1, endpoint);
            hook.gameObject.SetActive(true);
            hook.position = endpoint;
            Quaternion facing = Quaternion.LookRotation(delta);
            hook.rotation = facing;
            steelChain.Present(origin.position, endpoint);
        }

        public void ShiftOrigin(Vector3 offset) => endpoint += offset;

        public void Hide()
        {
            visible = false;
            core.enabled = false;
            steelChain?.Hide();
            hook.gameObject.SetActive(false);
            for (int i = 0; i < links.Length; i++) links[i].gameObject.SetActive(false);
        }

        private void OnDestroy() => steelChain?.Dispose();
    }
}
