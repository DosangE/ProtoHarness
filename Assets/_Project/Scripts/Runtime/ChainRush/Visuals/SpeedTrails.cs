using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProtoHarness.ChainRush.Visuals
{
    // Presentation only: what follows the runner at speed (user 2026-11-06, "스윙꼬리 따로 부스터 단일꼬리 따로 스윙+부스터 꼬리
    // 따로, 부스터존에 의한 부스터 꼬리 따로"). One tail at a time: thin amber for a swing, mint for a booster, wide gold for
    // both, green for a boost pad's carry, fading with it. The swing and the booster tails drawn one on the other did not
    // read as a third tail (user 2026-11-06, "부스터 + 스윙 의 꼬리가 안보인다"), so both has its own. Under any booster, white
    // wind lines cut back past the runner (user 2026-11-06, "날카롭게 바람을 가르는 듯한 흰색 뾰족한 선들"): thin spindles,
    // pointed at both ends, shot in mirrored pairs from all round the runner and fanning back ("여러 방향으로"), read from
    // the user's reference clip. Loose particle streaks were too irregular and sonic rings too round and too many. All of
    // it hangs on the runner's root, so it does not depend on the runner's temporary model (DECISIONS 2026-11-06).
    public sealed class SpeedTrails : IDisposable
    {
        public enum Style { None, Swing, Booster, SwingBooster, BoostZone }

        private const int LinePoints = 7;
        // Below this share of a pad's speed no new line starts; the tail fades on.
        private const float WeakestLine = 0.05f;
        // Each line's direction round the runner, in degrees from its right over the top to its left, none into the road,
        // and how far it fans out from straight back. Line i mirrors line Length - 1 - i. Fixed, so the lines stay regular.
        private static readonly float[] LineAround = { -10f, 20f, 50f, 75f, 105f, 130f, 160f, 190f };
        private static readonly float[] LineFan = { 30f, 20f, 30f, 20f, 20f, 30f, 20f, 30f };
        // The order the mirrored pairs fire in, each named by its first line, so the wave jumps about the runner.
        private static readonly int[] PairOrder = { 0, 3, 1, 2 };
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private readonly Transform runner;
        private readonly TrailRenderer boostTail;
        private readonly TrailRenderer swingTail;
        private readonly TrailRenderer swingBoostTail;
        private readonly LineRenderer[] lines = new LineRenderer[LineAround.Length];
        private readonly float[] lineBornAt = new float[LineAround.Length];
        private readonly Vector3[] points = new Vector3[LinePoints];
        private readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();
        private int nextPair;
        private float nextLineAt;
        private float lineStrength;

        public Style Current { get; private set; }
        public bool BoostTailEmitting => boostTail != null && boostTail.emitting;
        public bool SwingTailEmitting => swingTail != null && swingTail.emitting;
        public bool SwingBoostTailEmitting => swingBoostTail != null && swingBoostTail.emitting;

        public bool WindLinesShown
        {
            get
            {
                for (int i = 0; i < lines.Length; i++)
                    if (lines[i] != null && lines[i].enabled) return true;
                return false;
            }
        }

        // A booster outranks a boost pad: the pad's carry ends when a booster fires, and a swing hooked on a carry ends it.
        public static Style StyleOf(bool swinging, bool booster, bool boostZone)
        {
            if (swinging) return booster ? Style.SwingBooster : Style.Swing;
            if (booster) return Style.Booster;
            return boostZone ? Style.BoostZone : Style.None;
        }

        // material is ControlFeedback's, which owns it; the tint on each renderer gives these their colours.
        public SpeedTrails(Transform runner, Material material)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (material == null) throw new ArgumentNullException(nameof(material));
            this.runner = runner;
            boostTail = MakeTail("Boost trail", material, FeedbackLook.BoostTailWidth, FeedbackLook.BoostTailTime);
            swingTail = MakeTail("Swing trail", material, FeedbackLook.SwingTailWidth, FeedbackLook.SwingTailTime);
            swingBoostTail = MakeTail("Swing boost trail", material, FeedbackLook.SwingBoostTailWidth, FeedbackLook.BoostTailTime);
            Color swingColor = FeedbackLook.SwingAmber;
            swingColor.a = 0.6f;
            SetTint(swingTail, swingColor, FeedbackLook.TailGlow);
            Color bothColor = FeedbackLook.SwingBoostGold;
            bothColor.a = 0.8f;
            SetTint(swingBoostTail, bothColor, FeedbackLook.TailGlow);
            // Pointed at both ends, fullest a little behind the leading tip.
            var spindle = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f));
            for (int i = 0; i < lines.Length; i++) lines[i] = MakeLine(i, material, spindle);
        }

        private TrailRenderer MakeTail(string name, Material material, float width, float time)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(runner, false);
            holder.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            var tail = holder.AddComponent<TrailRenderer>();
            tail.sharedMaterial = material;
            tail.shadowCastingMode = ShadowCastingMode.Off;
            tail.receiveShadows = false;
            tail.time = time;
            tail.minVertexDistance = 0.15f;
            tail.startWidth = width;
            tail.endWidth = 0f;
            tail.autodestruct = false;
            tail.emitting = false;
            tail.enabled = false;
            return tail;
        }

        private LineRenderer MakeLine(int index, Material material, AnimationCurve spindle)
        {
            var holder = new GameObject("Wind line " + index);
            holder.transform.SetParent(runner, false);
            var line = holder.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.useWorldSpace = true;
            line.positionCount = LinePoints;
            line.widthCurve = spindle;
            line.widthMultiplier = FeedbackLook.WindLineWidth;
            line.enabled = false;
            return line;
        }

        // Each frame of a run. padShare is how much of a boost pad's speed its carry still holds (FeedbackLook.PadShare);
        // forward is where the runner faces, since its root does not turn.
        public void Present(Style style, float padShare, Vector3 forward, float now)
        {
            Current = style;
            bool zone = style == Style.BoostZone;
            bool both = style == Style.SwingBooster;
            bool boost = zone || style == Style.Booster;
            float strength = zone ? padShare : 1f;
            boostTail.enabled = swingTail.enabled = swingBoostTail.enabled = true;
            boostTail.emitting = boost;
            swingTail.emitting = style == Style.Swing;
            swingBoostTail.emitting = both;
            if (boost)
            {
                Color tailColor = zone ? FeedbackLook.BoostZoneGreen : NightUi.Accent;
                boostTail.time = FeedbackLook.BoostTailTime * Mathf.Lerp(0.5f, 1f, strength);
                tailColor.a = Mathf.Lerp(0.25f, 0.6f, strength);
                SetTint(boostTail, tailColor, FeedbackLook.TailGlow);
            }
            if (boost || both)
            {
                lineStrength = strength;
                if (strength > WeakestLine && now >= nextLineAt) StartPair(now);
            }
            UpdateLines(forward, now);
        }

        private void StartPair(float now)
        {
            int first = PairOrder[nextPair];
            StartLine(first, now);
            StartLine(lines.Length - 1 - first, now);
            nextPair = (nextPair + 1) % PairOrder.Length;
            nextLineAt = now + FeedbackLook.WindLineEvery;
        }

        private void StartLine(int index, float now)
        {
            lineBornAt[index] = now;
            lines[index].enabled = true;
        }

        private void UpdateLines(Vector3 forward, float now)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            for (int i = 0; i < lines.Length; i++)
                DrawLine(i, (now - lineBornAt[i]) / FeedbackLook.WindLineTime, forward, right);
        }

        // The leading tip shoots out fast and slows; the trailing tip catches it up, so the line stretches, then closes to
        // nothing at its end.
        private void DrawLine(int index, float age, Vector3 forward, Vector3 right)
        {
            LineRenderer line = lines[index];
            if (!line.enabled) return;
            if (age >= 1f)
            {
                line.enabled = false;
                return;
            }
            float around = LineAround[index] * Mathf.Deg2Rad;
            float fan = LineFan[index] * Mathf.Deg2Rad;
            Vector3 outward = right * Mathf.Cos(around) + Vector3.up * Mathf.Sin(around);
            Vector3 way = outward * Mathf.Sin(fan) - forward * Mathf.Cos(fan);
            Vector3 origin = runner.position + Vector3.up * FeedbackLook.WindLineHeight + forward * FeedbackLook.WindLineAhead
                + outward * FeedbackLook.WindLineRadius;
            float lead = FeedbackLook.WindLineReach * (1f - (1f - age) * (1f - age));
            float trail = FeedbackLook.WindLineReach * age * age;
            for (int i = 0; i < LinePoints; i++)
                points[i] = origin + way * Mathf.Lerp(lead, trail, i / (float)(LinePoints - 1));
            line.SetPositions(points);
            Color color = Color.white;
            color.a = FeedbackLook.WindLineAlpha * lineStrength * (1f - age * age);
            SetTint(line, color, 1.5f);
        }

        // Out of a run, paused or on a respawn's jump: nothing shows, and no tail keeps points to join across the jump.
        public void Hide()
        {
            Current = Style.None;
            nextLineAt = 0f;
            for (int i = 0; i < lines.Length; i++)
                if (lines[i] != null) lines[i].enabled = false;
            HideTail(boostTail);
            HideTail(swingTail);
            HideTail(swingBoostTail);
        }

        private static void HideTail(TrailRenderer tail)
        {
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
            if (boostTail != null) UnityEngine.Object.Destroy(boostTail.gameObject);
            if (swingTail != null) UnityEngine.Object.Destroy(swingTail.gameObject);
            if (swingBoostTail != null) UnityEngine.Object.Destroy(swingBoostTail.gameObject);
            for (int i = 0; i < lines.Length; i++)
                if (lines[i] != null) UnityEngine.Object.Destroy(lines[i].gameObject);
        }
    }
}
