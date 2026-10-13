using System;
using UnityEngine;

namespace ProtoHarness.ChainRush.Track
{
    // Weights, parameter ranges and rule constants for CourseGenerator (COURSE.md 5-1, 6-5). Every
    // range is a Vector2 (x = min, y = max) given at distance 0 and at "full" difficulty; values in
    // between blend linearly over rampDistance, like EncounterTuning.NextGap. Defaults are calculated
    // starting values, not play-tested (HANDOFF.md 2-4).
    [CreateAssetMenu(menuName = "ProtoHarness/ChainRush/Course Tuning")]
    public sealed class CourseTuning : ScriptableObject
    {
        // Hard physical limits (COURSE.md 6): validation rejects tunings beyond them.
        public const float MinRadius = 30f;
        public const float MaxJumpGap = 7.7f;
        public const float MaxGrade = 0.2f;

        public static readonly int KindCount = Enum.GetValues(typeof(ModuleKind)).Length;

        [Header("Difficulty ramp")]
        [Tooltip("Distance in metres at which every range reaches its full value.")]
        [SerializeField] private float rampDistance = 1400f;

        [Header("Module weights (index = ModuleKind: Straight, Rest, GentleCurve, SharpCurve, SCurve, Uphill, Downhill, Hill, JumpGap, GrappleGap)")]
        [SerializeField] private float[] weightsAtStart = { 3f, 0f, 3f, 0f, 1f, 2f, 2f, 1f, 1f, 2f };
        [SerializeField] private float[] weightsAtFull = { 2f, 0f, 2f, 2f, 2f, 2f, 2f, 2f, 2f, 2f };

        [Header("Ranges (x = min, y = max)")]
        [SerializeField] private Vector2 straightLengthStart = new Vector2(30f, 80f);
        [SerializeField] private Vector2 straightLengthFull = new Vector2(30f, 80f);
        [SerializeField] private Vector2 restLengthStart = new Vector2(40f, 60f);
        [SerializeField] private Vector2 restLengthFull = new Vector2(40f, 60f);
        [SerializeField] private Vector2 gentleRadiusStart = new Vector2(60f, 120f);
        [SerializeField] private Vector2 gentleRadiusFull = new Vector2(60f, 120f);
        [SerializeField] private Vector2 gentleAngleStart = new Vector2(30f, 90f);
        [SerializeField] private Vector2 gentleAngleFull = new Vector2(30f, 90f);
        [SerializeField] private Vector2 sharpRadiusStart = new Vector2(50f, 60f);
        [SerializeField] private Vector2 sharpRadiusFull = new Vector2(30f, 50f);
        [SerializeField] private Vector2 sharpAngleStart = new Vector2(60f, 90f);
        [SerializeField] private Vector2 sharpAngleFull = new Vector2(60f, 120f);
        [SerializeField] private Vector2 sCurveRadiusStart = new Vector2(80f, 80f);
        [SerializeField] private Vector2 sCurveRadiusFull = new Vector2(50f, 80f);
        [SerializeField] private Vector2 sCurveAngleStart = new Vector2(30f, 45f);
        [SerializeField] private Vector2 sCurveAngleFull = new Vector2(30f, 60f);
        [SerializeField] private Vector2 uphillGradeStart = new Vector2(0.04f, 0.06f);
        [SerializeField] private Vector2 uphillGradeFull = new Vector2(0.04f, 0.10f);
        [SerializeField] private Vector2 uphillLengthStart = new Vector2(40f, 80f);
        [SerializeField] private Vector2 uphillLengthFull = new Vector2(40f, 80f);
        [SerializeField] private Vector2 downhillGradeStart = new Vector2(0.04f, 0.08f);
        [SerializeField] private Vector2 downhillGradeFull = new Vector2(0.04f, 0.12f);
        [SerializeField] private Vector2 downhillLengthStart = new Vector2(40f, 80f);
        [SerializeField] private Vector2 downhillLengthFull = new Vector2(40f, 80f);
        // A hill is three pieces (up to +g, crest +g to -g, back to 0): its peak is grade * length / 4.
        [SerializeField] private Vector2 hillGradeStart = new Vector2(0.10f, 0.12f);
        [SerializeField] private Vector2 hillGradeFull = new Vector2(0.10f, 0.12f);
        [SerializeField] private Vector2 hillLengthStart = new Vector2(120f, 160f);
        [SerializeField] private Vector2 hillLengthFull = new Vector2(120f, 160f);
        [SerializeField] private Vector2 jumpGapLengthStart = new Vector2(5f, 6f);
        [SerializeField] private Vector2 jumpGapLengthFull = new Vector2(5f, 7.7f);
        [SerializeField] private Vector2 grappleGapLengthStart = new Vector2(16f, 16f);
        [SerializeField] private Vector2 grappleGapLengthFull = new Vector2(14f, 18f);
        [Tooltip("Anchor lateral offset from the centerline (positive = right).")]
        [SerializeField] private Vector2 anchorOffsetStart = new Vector2(0f, 0f);
        [SerializeField] private Vector2 anchorOffsetFull = new Vector2(-2f, 2f);
        [Tooltip("Anchor height above the road (the scene's passing value is 10).")]
        [SerializeField] private Vector2 anchorHeightStart = new Vector2(10f, 10f);
        [SerializeField] private Vector2 anchorHeightFull = new Vector2(10f, 10f);

        [Header("Connection rules (COURSE.md 6-5)")]
        [Tooltip("R1: flat straight before and after every gap, inside the gap module.")]
        [SerializeField] private float gapRunway = 15f;
        [Tooltip("R3: the first module is a rest of this length.")]
        [SerializeField] private float spawnRestLength = 60f;
        [Tooltip("R3: a flat straight at least this long counts as a rest.")]
        [SerializeField] private float restMinLength = 40f;
        [Tooltip("R3: no more than this many metres of non-rest modules in a row.")]
        [SerializeField] private float restSpacing = 200f;
        [Tooltip("R4: most consecutive modules of one kind.")]
        [SerializeField] private int maxSameKindRun = 2;
        [Tooltip("R5: window (metres, counted in whole modules) and limit (degrees) of the net turn.")]
        [SerializeField] private float turnWindow = 600f;
        [SerializeField] private float turnLimit = 180f;
        [Tooltip("R6: height stays within this many metres of the start height.")]
        [SerializeField] private float heightLimit = 40f;
        [Tooltip("R7: a module may not come closer than footprintMargin (a box half-size) to modules in this window, except the one right before it.")]
        [SerializeField] private float intersectionWindow = 1200f;
        [SerializeField] private float footprintSpacing = 5f;
        [Tooltip("Half road width 6 + guard 0.3 + 4 spare.")]
        [SerializeField] private float footprintMargin = 10.3f;

        [Header("Generation")]
        [SerializeField] private int candidatesPerModule = 8;
        [Tooltip("Length of the fallback rest emitted when every candidate broke a rule.")]
        [SerializeField] private float escapeRestLength = 40f;

        [Header("Open edges (no guard, COURSE.md 4-4)")]
        [SerializeField] private float openEdgeStartDistance = 600f;
        [Tooltip("Open edges never exceed this fraction of all modules generated so far.")]
        [SerializeField] private float openEdgeMaxFraction = 0.10f;
        [Tooltip("Chance that an eligible module (straight or gentle curve) opens one edge.")]
        [SerializeField] private float openEdgeChance = 0.25f;

        public float RampDistance => rampDistance;
        public float GapRunway => gapRunway;
        public float SpawnRestLength => spawnRestLength;
        public float RestMinLength => restMinLength;
        public float RestSpacing => restSpacing;
        public int MaxSameKindRun => maxSameKindRun;
        public float TurnWindow => turnWindow;
        public float TurnLimit => turnLimit;
        public float HeightLimit => heightLimit;
        public float IntersectionWindow => intersectionWindow;
        public float FootprintSpacing => footprintSpacing;
        public float FootprintMargin => footprintMargin;
        public int CandidatesPerModule => candidatesPerModule;
        public float EscapeRestLength => escapeRestLength;
        public float OpenEdgeStartDistance => openEdgeStartDistance;
        public float OpenEdgeMaxFraction => openEdgeMaxFraction;
        public float OpenEdgeChance => openEdgeChance;

        public Vector2 StraightLength(double distance) => Blend(straightLengthStart, straightLengthFull, distance);
        public Vector2 RestLength(double distance) => Blend(restLengthStart, restLengthFull, distance);
        public Vector2 GentleRadius(double distance) => Blend(gentleRadiusStart, gentleRadiusFull, distance);
        public Vector2 GentleAngle(double distance) => Blend(gentleAngleStart, gentleAngleFull, distance);
        public Vector2 SharpRadius(double distance) => Blend(sharpRadiusStart, sharpRadiusFull, distance);
        public Vector2 SharpAngle(double distance) => Blend(sharpAngleStart, sharpAngleFull, distance);
        public Vector2 SCurveRadius(double distance) => Blend(sCurveRadiusStart, sCurveRadiusFull, distance);
        public Vector2 SCurveAngle(double distance) => Blend(sCurveAngleStart, sCurveAngleFull, distance);
        public Vector2 UphillGrade(double distance) => Blend(uphillGradeStart, uphillGradeFull, distance);
        public Vector2 UphillLength(double distance) => Blend(uphillLengthStart, uphillLengthFull, distance);
        public Vector2 DownhillGrade(double distance) => Blend(downhillGradeStart, downhillGradeFull, distance);
        public Vector2 DownhillLength(double distance) => Blend(downhillLengthStart, downhillLengthFull, distance);
        public Vector2 HillGrade(double distance) => Blend(hillGradeStart, hillGradeFull, distance);
        public Vector2 HillLength(double distance) => Blend(hillLengthStart, hillLengthFull, distance);
        public Vector2 JumpGapLength(double distance) => Blend(jumpGapLengthStart, jumpGapLengthFull, distance);
        public Vector2 GrappleGapLength(double distance) => Blend(grappleGapLengthStart, grappleGapLengthFull, distance);
        public Vector2 AnchorOffset(double distance) => Blend(anchorOffsetStart, anchorOffsetFull, distance);
        public Vector2 AnchorHeight(double distance) => Blend(anchorHeightStart, anchorHeightFull, distance);

        // Weight of a kind at a distance, blended like the ranges.
        public float Weight(ModuleKind kind, double distance)
        {
            int i = (int)kind;
            return Mathf.Lerp(weightsAtStart[i], weightsAtFull[i], Progress(distance));
        }

        // 0 at distance 0 up to 1 at rampDistance and beyond.
        public float Progress(double distance) => (float)Math.Min(1d, Math.Max(0d, distance / rampDistance));

        private Vector2 Blend(Vector2 start, Vector2 full, double distance) => Vector2.Lerp(start, full, Progress(distance));

        // The same checks OnValidate logs; CourseGenerator throws ArgumentException with this message.
        public bool TryValidate(out string error)
        {
            if (!(rampDistance > 0f)) return Fail("rampDistance must be positive.", out error);
            if (!ValidWeights(weightsAtStart, out error, nameof(weightsAtStart))) return false;
            if (!ValidWeights(weightsAtFull, out error, nameof(weightsAtFull))) return false;
            if (!ValidRange(straightLengthStart, straightLengthFull, 1f, float.MaxValue, nameof(straightLengthStart), out error)) return false;
            if (!ValidRange(restLengthStart, restLengthFull, 1f, float.MaxValue, nameof(restLengthStart), out error)) return false;
            if (!ValidRange(gentleRadiusStart, gentleRadiusFull, MinRadius, float.MaxValue, nameof(gentleRadiusStart), out error)) return false;
            if (!ValidRange(gentleAngleStart, gentleAngleFull, 1f, 180f, nameof(gentleAngleStart), out error)) return false;
            if (!ValidRange(sharpRadiusStart, sharpRadiusFull, MinRadius, float.MaxValue, nameof(sharpRadiusStart), out error)) return false;
            if (!ValidRange(sharpAngleStart, sharpAngleFull, 1f, 180f, nameof(sharpAngleStart), out error)) return false;
            if (!ValidRange(sCurveRadiusStart, sCurveRadiusFull, MinRadius, float.MaxValue, nameof(sCurveRadiusStart), out error)) return false;
            if (!ValidRange(sCurveAngleStart, sCurveAngleFull, 1f, 180f, nameof(sCurveAngleStart), out error)) return false;
            if (!ValidRange(uphillGradeStart, uphillGradeFull, 0.001f, MaxGrade, nameof(uphillGradeStart), out error)) return false;
            if (!ValidRange(uphillLengthStart, uphillLengthFull, 4f, float.MaxValue, nameof(uphillLengthStart), out error)) return false;
            if (!ValidRange(downhillGradeStart, downhillGradeFull, 0.001f, MaxGrade, nameof(downhillGradeStart), out error)) return false;
            if (!ValidRange(downhillLengthStart, downhillLengthFull, 4f, float.MaxValue, nameof(downhillLengthStart), out error)) return false;
            if (!ValidRange(hillGradeStart, hillGradeFull, 0.001f, MaxGrade, nameof(hillGradeStart), out error)) return false;
            if (!ValidRange(hillLengthStart, hillLengthFull, 4f, float.MaxValue, nameof(hillLengthStart), out error)) return false;
            if (!ValidRange(jumpGapLengthStart, jumpGapLengthFull, 0.5f, MaxJumpGap, nameof(jumpGapLengthStart), out error)) return false;
            if (!ValidRange(grappleGapLengthStart, grappleGapLengthFull, 0.5f, float.MaxValue, nameof(grappleGapLengthStart), out error)) return false;
            if (!ValidRange(anchorOffsetStart, anchorOffsetFull, -float.MaxValue, float.MaxValue, nameof(anchorOffsetStart), out error)) return false;
            if (!ValidRange(anchorHeightStart, anchorHeightFull, 0f, float.MaxValue, nameof(anchorHeightStart), out error)) return false;
            if (!(gapRunway > 0f)) return Fail("gapRunway must be positive.", out error);
            if (!(spawnRestLength > 0f) || !(escapeRestLength > 0f)) return Fail("spawnRestLength and escapeRestLength must be positive.", out error);
            if (!(restMinLength > 0f) || escapeRestLength < restMinLength || spawnRestLength < restMinLength || restLengthStart.x < restMinLength || restLengthFull.x < restMinLength)
                return Fail("Every rest length (spawn, escape, rest ranges) must be at least restMinLength, or it would not count as a rest.", out error);
            if (!(restSpacing > restMinLength)) return Fail("restSpacing must exceed restMinLength.", out error);
            if (maxSameKindRun < 1) return Fail("maxSameKindRun must be at least 1.", out error);
            if (!(turnWindow > 0f) || !(turnLimit > 0f)) return Fail("turnWindow and turnLimit must be positive.", out error);
            if (!(heightLimit > 0f)) return Fail("heightLimit must be positive.", out error);
            if (!(intersectionWindow > 0f) || !(footprintSpacing > 0f) || !(footprintMargin > 0f))
                return Fail("intersectionWindow, footprintSpacing and footprintMargin must be positive.", out error);
            if (candidatesPerModule < 1 || candidatesPerModule > 31) return Fail("candidatesPerModule must be within [1, 31].", out error);
            if (openEdgeStartDistance < 0f || openEdgeMaxFraction < 0f || openEdgeMaxFraction > 1f || openEdgeChance < 0f || openEdgeChance > 1f)
                return Fail("Open edge values must be non-negative and the fraction and chance within [0, 1].", out error);
            error = null;
            return true;
        }

        private static bool Fail(string message, out string error)
        {
            error = "CourseTuning: " + message;
            return false;
        }

        private static bool ValidWeights(float[] weights, out string error, string name)
        {
            if (weights == null || weights.Length != KindCount) return Fail($"{name} needs exactly {KindCount} entries (one per ModuleKind).", out error);
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (!(weights[i] >= 0f) || float.IsInfinity(weights[i])) return Fail($"{name}[{i}] must be finite and not negative.", out error);
                sum += weights[i];
            }
            if (!(sum > 0f)) return Fail($"{name} must have at least one positive weight.", out error);
            error = null;
            return true;
        }

        private static bool ValidRange(Vector2 start, Vector2 full, float lowest, float highest, string name, out string error)
        {
            foreach (Vector2 range in new[] { start, full })
            {
                if (!(range.x <= range.y) || range.x < lowest || range.y > highest)
                    return Fail($"{name}/Full range [{range.x}, {range.y}] must satisfy min <= max within [{lowest}, {highest}].", out error);
            }
            error = null;
            return true;
        }

        private void OnValidate()
        {
            if (!TryValidate(out string error)) Debug.LogError(error, this);
        }
    }
}
