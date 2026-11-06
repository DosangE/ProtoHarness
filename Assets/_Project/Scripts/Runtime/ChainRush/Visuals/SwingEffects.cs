using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProtoHarness.ChainRush.Visuals
{
    // Presentation only: how a pole swing shows beyond its chain (M6). Sparks fly off the pole's band, the chain snaps and
    // its halves spring back when the swing ends, and a thin amber tail follows the runner, apart from the booster's mint
    // one. Nothing here reads the runner's model, which is temporary (DECISIONS 2026-11-06). ControlFeedback notices the
    // moments and calls these; the simulation never reads them.
    public sealed class SwingEffects : IDisposable
    {
        private const int SnapPoints = 10;
        private const int MaxSparks = 64;
        private const float SnapWidth = 0.09f;
        private static readonly Color Spark = new Color(1f, 0.62f, 0.18f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private readonly GameObject root;
        private readonly GameObject tailObject;
        private readonly ParticleSystem sparks;
        private readonly ParticleSystemRenderer sparkRenderer;
        private readonly LineRenderer poleHalf;
        private readonly LineRenderer runnerHalf;
        private readonly TrailRenderer tail;
        private readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();
        private readonly Vector3[] points = new Vector3[SnapPoints];
        private bool snapped;
        private float snappedAt;
        private Vector3 snapBite;

        public bool SparksEmitting => sparks != null && sparks.isEmitting;
        public bool SnapShown => poleHalf != null && poleHalf.enabled;
        public bool TailEmitting => tail != null && tail.emitting;

        // material is ControlFeedback's, which owns it; the tint on each renderer gives these their colours.
        public SwingEffects(Transform parent, Transform runner, Material material)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (material == null) throw new ArgumentNullException(nameof(material));
            root = new GameObject("Swing effects");
            root.transform.SetParent(parent, false);
            sparks = MakeSparks(material, out sparkRenderer);
            SetTint(sparkRenderer, Spark, 4f);
            poleHalf = MakeHalf("Chain snap, pole half", material);
            runnerHalf = MakeHalf("Chain snap, runner half", material);
            tailObject = new GameObject("Swing trail");
            tailObject.transform.SetParent(runner, false);
            tailObject.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            tail = tailObject.AddComponent<TrailRenderer>();
            tail.sharedMaterial = material;
            tail.shadowCastingMode = ShadowCastingMode.Off;
            tail.receiveShadows = false;
            tail.time = FeedbackLook.SwingTailTime;
            tail.minVertexDistance = 0.15f;
            tail.startWidth = FeedbackLook.SwingTailWidth;
            tail.endWidth = 0f;
            tail.autodestruct = false;
            tail.emitting = false;
            tail.enabled = false;
            Color tailColor = Spark;
            tailColor.a = 0.35f;
            SetTint(tail, tailColor, 2f);
        }

        private ParticleSystem MakeSparks(Material material, out ParticleSystemRenderer renderer)
        {
            var holder = new GameObject("Swing sparks");
            // Inactive while it is set up: a new system plays on awake with the defaults otherwise.
            holder.SetActive(false);
            holder.transform.SetParent(root.transform, false);
            var system = holder.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = MaxSparks;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.gravityModifier = 1.5f;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = FeedbackLook.SparkRate;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            // The band's edge (CircuitScenery.PlacePoles: 1.6 times the pole's thickness across).
            shape.radius = SwingLook.PoleThickness * 0.8f;
            shape.radiusThickness = 0f;
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            holder.SetActive(true);
            return system;
        }

        private LineRenderer MakeHalf(string name, Material material)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(root.transform, false);
            var line = holder.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.useWorldSpace = true;
            line.positionCount = SnapPoints;
            line.widthMultiplier = SnapWidth;
            line.enabled = false;
            return line;
        }

        // The chain has bitten the band at bite.
        public void Hook(Vector3 bite)
        {
            sparks.transform.position = bite;
            sparks.Emit(FeedbackLook.SparkHookBurst);
        }

        // The swing has ended on its own: the chain parts where it was.
        public void Snap(Vector3 bite, float now)
        {
            snapped = true;
            snappedAt = now;
            snapBite = bite;
            sparks.transform.position = bite;
            sparks.Emit(FeedbackLook.SparkSnapBurst);
        }

        // Each frame of a run. body is where the runner's half of a snapped chain springs back to.
        public void Present(bool swinging, Vector3 bite, Vector3 body, float now)
        {
            if (swinging)
            {
                sparks.transform.position = bite;
                if (!sparks.isEmitting) sparks.Play();
            }
            else if (sparks.isEmitting) sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            tail.enabled = true;
            tail.emitting = swinging || (snapped && now - snappedAt < FeedbackLook.SwingTailAfter);
            float fade = snapped ? FeedbackLook.Fade(now - snappedAt, FeedbackLook.ChainSnapTime) : 0f;
            poleHalf.enabled = runnerHalf.enabled = fade > 0f;
            if (fade <= 0f) return;
            DrawHalf(poleHalf, snapBite, body, fade);
            DrawHalf(runnerHalf, body, snapBite, fade);
        }

        // From its fixed end to a tip that starts halfway to the other end and springs home, whipping aside as it goes.
        private void DrawHalf(LineRenderer half, Vector3 from, Vector3 toward, float fade)
        {
            Vector3 tip = Vector3.Lerp(from, toward, 0.5f * fade);
            Vector3 side = Vector3.Cross(toward - from, Vector3.up);
            side = side.sqrMagnitude > 1e-6f ? side.normalized : Vector3.right;
            float whip = FeedbackLook.ChainSnapWhip * (1f - fade);
            for (int i = 0; i < SnapPoints; i++)
            {
                float along = i / (float)(SnapPoints - 1);
                points[i] = Vector3.Lerp(from, tip, along) + side * (Mathf.Sin(along * Mathf.PI * 3f) * along * whip);
            }
            half.SetPositions(points);
            Color color = Color.Lerp(Spark, Color.white, fade);
            color.a = fade;
            SetTint(half, color, 2f);
        }

        // Out of a run, paused or on a respawn's jump: nothing shows, and the tail keeps no points to join across the jump.
        public void Hide()
        {
            snapped = false;
            if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (poleHalf != null) poleHalf.enabled = false;
            if (runnerHalf != null) runnerHalf.enabled = false;
            if (tail == null) return;
            tail.emitting = false;
            if (tail.enabled) tail.Clear();
            tail.enabled = false;
        }

        // SetPropertyBlock copies the values, so one block serves every renderer here.
        private void SetTint(Renderer renderer, Color color, float glow)
        {
            tint.SetColor(BaseColorId, color);
            tint.SetColor(EmissionColorId, new Color(color.r * glow, color.g * glow, color.b * glow, 1f));
            renderer.SetPropertyBlock(tint);
        }

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            if (tailObject != null) UnityEngine.Object.Destroy(tailObject);
        }
    }
}
