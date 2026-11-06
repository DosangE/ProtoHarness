using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProtoHarness.ChainRush.Visuals
{
    // Presentation only: how a pole swing shows beyond its chain (M6). Sparks fly off the pole's band, and off the chain
    // just by the runner, where the camera can see them (user 2026-11-06: the band's alone were too far off to tell); the
    // chain snaps and its halves spring back when the swing ends. Nothing here reads the runner's model, which is
    // temporary (DECISIONS 2026-11-06). ControlFeedback notices the moments and calls these; the simulation never reads
    // them.
    public sealed class SwingEffects : IDisposable
    {
        private const int SnapPoints = 10;
        private const int MaxSparks = 64;
        private const float SnapWidth = 0.09f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private readonly GameObject root;
        private readonly ParticleSystem bandSparks;
        private readonly ParticleSystem handSparks;
        private readonly LineRenderer poleHalf;
        private readonly LineRenderer runnerHalf;
        private readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();
        private readonly Vector3[] points = new Vector3[SnapPoints];
        private bool snapped;
        private float snappedAt;
        private Vector3 snapBite;

        public bool SparksEmitting => bandSparks != null && bandSparks.isEmitting;
        public bool HandSparksEmitting => handSparks != null && handSparks.isEmitting;
        public bool SnapShown => poleHalf != null && poleHalf.enabled;

        // material is ControlFeedback's, which owns it; the tint on each renderer gives these their colours.
        public SwingEffects(Transform parent, Material material)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (material == null) throw new ArgumentNullException(nameof(material));
            root = new GameObject("Swing effects");
            root.transform.SetParent(parent, false);
            // The band's edge (CircuitScenery.PlacePoles: 1.6 times the pole's thickness across).
            bandSparks = MakeSparks("Band sparks", material, SwingLook.PoleThickness * 0.8f, FeedbackLook.SparkRate, 4f, 10f, 0.12f, 0.3f);
            handSparks = MakeSparks("Hand sparks", material, 0.15f, FeedbackLook.HandSparkRate, 2f, 6f, 0.05f, 0.13f);
            poleHalf = MakeHalf("Chain snap, pole half", material);
            runnerHalf = MakeHalf("Chain snap, runner half", material);
        }

        private ParticleSystem MakeSparks(string name, Material material, float radius, float rate, float slowest, float fastest, float smallest, float largest)
        {
            var holder = new GameObject(name);
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
            main.startSpeed = new ParticleSystem.MinMaxCurve(slowest, fastest);
            main.startSize = new ParticleSystem.MinMaxCurve(smallest, largest);
            main.gravityModifier = 1.5f;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = 0f;
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SetTint(renderer, FeedbackLook.SwingAmber, 4f);
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

        // Where the chain leaves the runner: a little way up it from the body, toward the band.
        private static Vector3 HandEnd(Vector3 bite, Vector3 body) => body + (bite - body).normalized * 0.8f;

        // The chain has bitten the band at bite.
        public void Hook(Vector3 bite, Vector3 body)
        {
            bandSparks.transform.position = bite;
            bandSparks.Emit(FeedbackLook.SparkHookBurst);
            handSparks.transform.position = HandEnd(bite, body);
            handSparks.Emit(FeedbackLook.SparkHookBurst);
        }

        // The swing has ended on its own: the chain parts where it was.
        public void Snap(Vector3 bite, Vector3 body, float now)
        {
            snapped = true;
            snappedAt = now;
            snapBite = bite;
            bandSparks.transform.position = bite;
            bandSparks.Emit(FeedbackLook.SparkSnapBurst);
            handSparks.transform.position = HandEnd(bite, body);
            handSparks.Emit(FeedbackLook.SparkSnapBurst);
        }

        // Each frame of a run. body is where the runner's half of a snapped chain springs back to.
        public void Present(bool swinging, Vector3 bite, Vector3 body, float now)
        {
            if (swinging)
            {
                bandSparks.transform.position = bite;
                handSparks.transform.position = HandEnd(bite, body);
                if (!bandSparks.isEmitting) bandSparks.Play();
                if (!handSparks.isEmitting) handSparks.Play();
            }
            else
            {
                if (bandSparks.isEmitting) bandSparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (handSparks.isEmitting) handSparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
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
            Color color = Color.Lerp(FeedbackLook.SwingAmber, Color.white, fade);
            color.a = fade;
            SetTint(half, color, 2f);
        }

        // Out of a run, paused or on a respawn's jump: nothing shows.
        public void Hide()
        {
            snapped = false;
            if (bandSparks != null) bandSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (handSparks != null) handSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (poleHalf != null) poleHalf.enabled = false;
            if (runnerHalf != null) runnerHalf.enabled = false;
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
        }
    }
}
