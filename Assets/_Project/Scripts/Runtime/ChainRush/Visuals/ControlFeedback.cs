using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProtoHarness.ChainRush.Visuals
{
    // Presentation only: reads the current motor and grapple on each frame and never writes simulation state.
    // A rollback repeating a tick is harmless: these local edges and renderers are never read by the simulation.
    // GrappleController (150) refreshes its offered pole before this presentation reads it. This is the one place that
    // notices the control moments; ChainRushHud reads them from here (Since*) for its screen effects.
    [DefaultExecutionOrder(200)]
    public sealed class ControlFeedback : MonoBehaviour
    {
        private const int RingCount = 4;
        private const int CirclePoints = 48;
        private const int ReadyRing = 0, BoostRing = 1, AnchorRing = 2, SwingRing = 3;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private ChainRushGame game;
        private RunnerMotor player;
        private GrappleController grapple;
        private Transform runner;
        private CharacterController capsule;
        private GameObject effectsRoot;
        private Material material;
        private MaterialPropertyBlock tint;
        private LineRenderer[] rings;
        private float[] burstAt;
        private TrailRenderer trail;
        // A booster is either action a gauge slot buys, watched apart: the slingshot can still be pulling when the grapple
        // it leads into is empowered with a second slot, and that second spend must show too (review 2026-10-29).
        private StateEdge slingPull;
        private StateEdge empower;
        private StateEdge hook;
        private StateEdge swing;
        private StateEdge poleReady;
        private bool boosted;
        private float boostedAt;
        private int spentBoostSlot = -1;
        // A miss is noticed by its mark moving later, not by JustMissed turning on: a second miss while the first still
        // shows (it lasts 0.75 s and nothing stops another press) must shake again (review 2026-10-29).
        private int lastMissUntil;
        private bool missed;
        private float missedAt;
        // A boost pad is noticed by its carry jumping up, not turning on: the climb's pads come 30 m apart, before the
        // last one's carry is gone (Palm Coast).
        private float lastPadCarry;
        private bool padHit;
        private float padAt;
        private bool initialized;

        public bool PoleReadyShown => rings != null && rings[ReadyRing].enabled;
        public bool TrailEmitting => trail != null && trail.emitting;

        // Seconds since each moment on Time.time, negative when it has not happened in this run.
        public float SinceBoost(float now) => boosted ? now - boostedAt : -1f;
        public float SinceHook(float now) => hook.Since(now);
        public float SinceSwing(float now) => swing.Since(now);
        public float SinceMiss(float now) => missed ? now - missedAt : -1f;
        public float SincePoleReady(float now) => poleReady.Since(now);
        public float SincePad(float now) => padHit ? now - padAt : -1f;
        // The gauge slot the last booster emptied (the whole slots left after spending it), -1 before any.
        public int SpentBoostSlot => spentBoostSlot;

        // Allocate every renderer and its geometry once; the HUD adds this component after validating its references.
        public void Initialize(ChainRushGame game, RunnerMotor player, GrappleController grapple)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (grapple == null) throw new ArgumentNullException(nameof(grapple));
            if (this.game != null) throw new InvalidOperationException("ControlFeedback: already initialized.");
            this.game = game;
            this.player = player;
            this.grapple = grapple;
            runner = player.transform;
            if (!player.TryGetComponent(out capsule))
            {
                Debug.LogError("ControlFeedback: the runner requires a CharacterController.", this);
                enabled = false;
                return;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("ControlFeedback: the URP Lit shader is required.", this);
                enabled = false;
                return;
            }
            // The same transparent URP Lit setup as GhostRunner, with emission for the mint light.
            material = new Material(shader) { name = "Control feedback (runtime)" };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_Smoothness", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_EMISSION");
            material.renderQueue = (int)RenderQueue.Transparent;
            tint = new MaterialPropertyBlock();
            effectsRoot = new GameObject("Control feedback");
            effectsRoot.transform.SetParent(transform, false);
            rings = new LineRenderer[RingCount];
            burstAt = new float[RingCount];
            var circle = new Vector3[CirclePoints];
            for (int i = 0; i < CirclePoints; i++)
            {
                float angle = i * Mathf.PI * 2f / CirclePoints;
                circle[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
            for (int i = 0; i < RingCount; i++)
            {
                rings[i] = MakeLine("Feedback ring " + i, CirclePoints, true);
                rings[i].SetPositions(circle);
            }
            var tail = new GameObject("Boost trail");
            tail.transform.SetParent(runner, false);
            tail.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            trail = tail.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.time = 0.25f;
            trail.minVertexDistance = 0.15f;
            trail.startWidth = 0.25f;
            trail.endWidth = 0f;
            trail.autodestruct = false;
            trail.emitting = false;
            trail.enabled = false;
            Color trailColor = NightUi.Accent;
            trailColor.a = 0.35f;
            SetTint(trail, trailColor);
            initialized = true;
        }

        private LineRenderer MakeLine(string name, int points, bool loop)
        {
            var root = new GameObject(name);
            root.transform.SetParent(effectsRoot.transform, false);
            var line = root.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = points;
            line.widthMultiplier = 0.075f;
            line.enabled = false;
            return line;
        }

        private void Start()
        {
            if (initialized) return;
            Debug.LogError("ControlFeedback: Initialize was not called, or it failed, before the first frame.", this);
            enabled = false;
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                // Turned back on after a failed Initialize: there is nothing to draw with.
                Debug.LogError("ControlFeedback: enabled without a successful Initialize.", this);
                enabled = false;
                return;
            }
            if (!game.IsRunning || game.IsPaused)
            {
                HideAll();
                // A pause keeps the watched states, so resuming cannot fire a second impact for the same action.
                if (!game.IsPaused) ForgetRun();
                return;
            }
            float now = Time.time;
            bool fired = slingPull.Update(player.IsSlingPulling, now);
            bool empowered = empower.Update(game.Racer.GrappleEmpowered, now);
            if (fired || empowered)
            {
                boosted = true;
                boostedAt = now;
                spentBoostSlot = Mathf.FloorToInt(player.Gauge);
                Vector3 feet = runner.TransformPoint(capsule.center - Vector3.up * (capsule.height * 0.5f));
                StartRing(BoostRing, feet + Vector3.up * 0.08f, Quaternion.identity, now);
            }
            int missUntil = game.Racer.MissUntilTick;
            if (missUntil > lastMissUntil)
            {
                missed = true;
                missedAt = now;
            }
            lastMissUntil = missUntil;
            float padCarry = game.Racer.BoostZoneCarrySpeed;
            if (padCarry > lastPadCarry + 0.5f)
            {
                padHit = true;
                padAt = now;
            }
            lastPadCarry = padCarry;
            if (hook.Update(grapple.IsAttached, now))
                StartRing(AnchorRing, grapple.AnchorPosition, Quaternion.FromToRotation(Vector3.up, -player.Facing), now);
            if (swing.Update(player.IsSwinging, now))
                StartRing(SwingRing, SwingLook.PoleBite(game.Racer.SwingCenter), Quaternion.identity, now);
            bool ready = grapple.HasPoleCandidate && !grapple.IsAttached && !player.IsSwinging;
            poleReady.Update(ready, now);
            rings[ReadyRing].enabled = ready;
            if (ready)
            {
                Vector3 bite = SwingLook.PoleBite(grapple.PoleCandidate);
                float since = poleReady.Since(now);
                rings[ReadyRing].transform.SetPositionAndRotation(bite, Quaternion.identity);
                rings[ReadyRing].transform.localScale = Vector3.one *
                    ((SwingLook.PoleThickness * 0.5f + 0.18f) * FeedbackLook.PoleScale(since));
                Color color = Color.Lerp(NightUi.Accent, Color.white, FeedbackLook.Fade(since, FeedbackLook.PoleEnterTime));
                color.a = 0.8f;
                SetTint(rings[ReadyRing], color);
            }
            UpdateRing(BoostRing, now, FeedbackLook.BoostRingTime, 0.5f, 3.5f, false);
            UpdateRing(AnchorRing, now, FeedbackLook.HookRingTime, 0.5f, 2f, true);
            UpdateRing(SwingRing, now, FeedbackLook.HookRingTime, SwingLook.PoleThickness * 0.5f, 2.6f, true);
            trail.enabled = true;
            // Only while a booster is on: the slingshot pulling or carrying, or the empowered grapple. Speed left over
            // after it (user 2026-10-29: the trail only for the booster) leaves no trail.
            trail.emitting = game.Racer.SlingTicks > 0 || game.Racer.GrappleEmpowered;
        }

        // Between runs: a new run starts with no moments behind it. The miss mark is taken as it stands, so a mark left
        // from before is not mistaken for a new miss.
        private void ForgetRun()
        {
            slingPull = empower = hook = swing = poleReady = default;
            boosted = missed = padHit = false;
            spentBoostSlot = -1;
            lastMissUntil = game.Racer.MissUntilTick;
            lastPadCarry = game.Racer.BoostZoneCarrySpeed;
        }

        private void StartRing(int index, Vector3 center, Quaternion rotation, float now)
        {
            burstAt[index] = now;
            rings[index].transform.SetPositionAndRotation(center, rotation);
            rings[index].enabled = true;
        }

        private void UpdateRing(int index, float now, float duration, float startRadius, float endRadius, bool whiteStart)
        {
            LineRenderer ring = rings[index];
            if (!ring.enabled) return;
            float fade = FeedbackLook.Fade(now - burstAt[index], duration);
            ring.enabled = fade > 0f;
            if (!ring.enabled) return;
            ring.transform.localScale = Vector3.one * Mathf.Lerp(endRadius, startRadius, fade);
            Color color = whiteStart ? Color.Lerp(NightUi.Accent, Color.white, fade) : NightUi.Accent;
            color.a = fade;
            SetTint(ring, color);
        }

        // SetPropertyBlock copies the values, so all renderers can reuse this one cached block and shared material.
        private void SetTint(Renderer renderer, Color color)
        {
            tint.SetColor(BaseColorId, color);
            tint.SetColor(EmissionColorId, new Color(color.r * 2f, color.g * 2f, color.b * 2f, 1f));
            renderer.SetPropertyBlock(tint);
        }

        private void HideAll()
        {
            if (rings != null)
                for (int i = 0; i < rings.Length; i++) rings[i].enabled = false;
            if (trail == null) return;
            trail.emitting = false;
            if (trail.enabled) trail.Clear();
            trail.enabled = false;
        }

        private void OnDisable() => HideAll();

        private void OnDestroy()
        {
            if (effectsRoot != null) Destroy(effectsRoot);
            if (trail != null) Destroy(trail.gameObject);
            if (material != null) Destroy(material);
        }
    }
}
