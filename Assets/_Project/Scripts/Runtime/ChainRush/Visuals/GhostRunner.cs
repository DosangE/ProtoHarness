using System;
using ProtoHarness.ChainRush.Flow;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProtoHarness.ChainRush.Visuals
{
    // The runner's best run as a see-through copy of its model (M4-3, DECISIONS 2026-10-29 "M4-3 고스트 재생"): on the
    // game's tick N it stands where that run was after tick N (GhostTrace entry N - 1), facing its heading, so it moves
    // exactly as the runner does (both step on ticks, neither is interpolated). Before the start it waits on the first
    // entry; past the trace's end (the ghost has finished) it is hidden, and a restart brings it back from tick 1.
    // Presentation only: the simulation never reads it, and the copy has no colliders. Its pose stays as copied
    // (RunnerAnimation does not drive it), as RemoteGhost's does.
    public sealed class GhostRunner : MonoBehaviour
    {
        // Mint (NightUi.Accent) at a third: the runner's shape, plainly not the runner.
        private static readonly Color Tint = new Color(NightUi.Accent.r, NightUi.Accent.g, NightUi.Accent.b, 0.35f);
        private ChainRushGame game;
        private GhostTrace trace;
        private Transform body;
        private Material material;

        public bool Visible => body != null && body.gameObject.activeSelf;
        public Vector3 Position => body != null ? body.position : default;
        public int Count => trace != null ? trace.Count : 0;

        // model: the runner's visual (RunnerTilt.Body), already dressed in its look. The copy sits under the ghost's root
        // where the model sits under the runner, so the root stands where the run's position was.
        public void Initialize(ChainRushGame game, GhostTrace trace, Transform model)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (trace == null) throw new ArgumentNullException(nameof(trace));
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (trace.Count == 0) throw new ArgumentException("A ghost needs at least one tick.", nameof(trace));
            if (body != null) throw new InvalidOperationException("GhostRunner: already initialized.");
            this.game = game;
            this.trace = trace;
            material = SeeThrough();
            body = new GameObject("Ghost body").transform;
            body.SetParent(transform, false);
            GameObject copy = Instantiate(model.gameObject, body, false);
            copy.name = model.name;
            copy.transform.localPosition = model.localPosition;
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = model.localScale;
            copy.SetActive(true);
            // Visual only: it must never collide, even in the frame before the colliders are destroyed.
            foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Destroy(collider);
            }
            foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            Follow();
        }

        // URP Lit set up as its inspector does for Surface Type Transparent, Blend Alpha.
        private static Material SeeThrough()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("GhostRunner: the URP Lit shader is required.");
            var see = new Material(shader) { name = "Ghost runner (runtime)" };
            see.SetFloat("_Surface", 1f);
            see.SetFloat("_Blend", 0f);
            see.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            see.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            see.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            see.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            see.SetFloat("_ZWrite", 0f);
            see.SetOverrideTag("RenderType", "Transparent");
            see.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            see.renderQueue = (int)RenderQueue.Transparent;
            see.SetColor("_BaseColor", Tint);
            see.SetFloat("_Smoothness", 0.2f);
            return see;
        }

        private void LateUpdate()
        {
            if (body != null) Follow();
        }

        private void Follow()
        {
            int tick = game.Tick;
            bool show = tick <= trace.Count;
            if (body.gameObject.activeSelf != show) body.gameObject.SetActive(show);
            if (!show) return;
            GhostTrace.Sample sample = trace[Mathf.Max(tick, 1) - 1];
            body.SetPositionAndRotation(sample.Position, Quaternion.Euler(0f, sample.Heading, 0f));
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
