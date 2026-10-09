using UnityEngine;

namespace ProtoHarness.ChainRush.Track
{
    // Weights, parameter ranges and rule constants for CourseGenerator. Distance-dependent values come in pairs
    // (at 0m, at fullDistance) and change linearly in between, then hold; ranges are Vector2 (min, max).
    // Defaults are the T3b starting values (docs/HANDOFF.md 2-4), chosen by calculation, not yet play-tuned.
    [CreateAssetMenu(menuName = "ProtoHarness/ChainRush/Course Tuning")]
    public sealed class CourseTuning : ScriptableObject
    {
        // Physical limits the tuning may not cross (docs/COURSE.md 6-1, 6-3, 6-4, 6-5).
        public const float MinRadius = 30f;
        public const float JumpGapLimit = 7.7f;
        public const float GradeLimit = 0.2f;
        public const float MinGapApproach = 15f;
        public const float MinRestLength = 40f;

        [SerializeField] private float fullDistance = 1400f;

        [Header("Weights (at 0m, at full distance)")]
        [SerializeField] private Vector2 straightWeight = new Vector2(3f, 2f);
        [SerializeField] private Vector2 gentleCurveWeight = new Vector2(3f, 2f);
        [SerializeField] private Vector2 sharpCurveWeight = new Vector2(0f, 2f);
        [SerializeField] private Vector2 sCurveWeight = new Vector2(1f, 2f);
        [SerializeField] private Vector2 uphillWeight = new Vector2(2f, 2f);
        [SerializeField] private Vector2 downhillWeight = new Vector2(2f, 2f);
        [SerializeField] private Vector2 hillWeight = new Vector2(1f, 2f);
        [SerializeField] private Vector2 jumpGapWeight = new Vector2(1f, 2f);
        [SerializeField] private Vector2 grappleGapWeight = new Vector2(2f, 2f);

        [Header("Ranges (min, max) at 0m and at full distance")]
        [SerializeField] private Vector2 straightLengthStart = new Vector2(30f, 80f);
        [SerializeField] private Vector2 straightLengthFull = new Vector2(30f, 80f);
        [SerializeField] private Vector2 restLengthStart = new Vector2(40f, 60f);
        [SerializeField] private Vector2 restLengthFull = new Vector2(40f, 60f);
        [SerializeField] private Vector2 gentleRadiusStart = new Vector2(60f, 120f);
        [SerializeField] private Vector2 gentleRadiusFull = new Vector2(60f, 120f);
        [SerializeField] private Vector2 gentleDegreesStart = new Vector2(30f, 90f);
        [SerializeField] private Vector2 gentleDegreesFull = new Vector2(30f, 90f);
        [SerializeField] private Vector2 sharpRadiusStart = new Vector2(50f, 60f);
        [SerializeField] private Vector2 sharpRadiusFull = new Vector2(30f, 50f);
        [SerializeField] private Vector2 sharpDegreesStart = new Vector2(60f, 90f);
        [SerializeField] private Vector2 sharpDegreesFull = new Vector2(60f, 120f);
        [SerializeField] private Vector2 sCurveRadiusStart = new Vector2(80f, 80f);
        [SerializeField] private Vector2 sCurveRadiusFull = new Vector2(50f, 80f);
        [SerializeField] private Vector2 sCurveDegreesStart = new Vector2(30f, 45f);
        [SerializeField] private Vector2 sCurveDegreesFull = new Vector2(30f, 60f);
        [SerializeField] private Vector2 uphillGradeStart = new Vector2(0.04f, 0.06f);
        [SerializeField] private Vector2 uphillGradeFull = new Vector2(0.04f, 0.10f);
        [SerializeField] private Vector2 downhillGradeStart = new Vector2(0.04f, 0.08f);
        [SerializeField] private Vector2 downhillGradeFull = new Vector2(0.04f, 0.12f);
        [Tooltip("Length held at full grade between the two ramps of an uphill or downhill.")]
        [SerializeField] private Vector2 slopeLengthStart = new Vector2(40f, 80f);
        [SerializeField] private Vector2 slopeLengthFull = new Vector2(40f, 80f);
        [Tooltip("Length over which an uphill or downhill eases into and out of its grade.")]
        [SerializeField] private float slopeRampLength = 20f;
        [Tooltip("A hill is three equal pieces (0 to +g, +g to -g, -g to 0); its top is 0.75 x grade x piece length above its ends.")]
        [SerializeField] private Vector2 hillGradeStart = new Vector2(0.06f, 0.08f);
        [SerializeField] private Vector2 hillGradeFull = new Vector2(0.06f, 0.10f);
        [SerializeField] private Vector2 hillPieceLengthStart = new Vector2(40f, 53f);
        [SerializeField] private Vector2 hillPieceLengthFull = new Vector2(40f, 53f);
        [SerializeField] private Vector2 jumpGapStart = new Vector2(5f, 6f);
        [SerializeField] private Vector2 jumpGapFull = new Vector2(5f, 7.7f);
        [SerializeField] private Vector2 grappleGapStart = new Vector2(16f, 16f);
        [SerializeField] private Vector2 grappleGapFull = new Vector2(14f, 18f);
        [SerializeField] private Vector2 grappleAnchorSideStart = new Vector2(0f, 0f);
        [SerializeField] private Vector2 grappleAnchorSideFull = new Vector2(-2f, 2f);

        [Header("Fixed shapes")]
        [Tooltip("Straight run-up before and landing after every gap (rule R1).")]
        [SerializeField] private float gapApproachLength = 15f;
        [Tooltip("Anchor height above the road, at the middle of a grapple gap.")]
        [SerializeField] private float grappleAnchorHeight = 10f;
        [SerializeField] private float spawnRestLength = 60f;
        [SerializeField] private float escapeRestLength = 40f;

        [Header("Rules")]
        [Tooltip("R3: a rest starts within this distance of the previous rest's end.")]
        [SerializeField] private float restSpacing = 200f;
        [Tooltip("R5: window for the net turn limit.")]
        [SerializeField] private float turnWindow = 600f;
        [SerializeField] private float maxNetTurn = 180f;
        [Tooltip("R6: the road stays within this height of the start.")]
        [SerializeField] private float maxHeightOffset = 40f;
        [Tooltip("R7: how far back new modules are checked for overlap.")]
        [SerializeField] private float overlapWindow = 1200f;
        [Tooltip("R7: centerline sample spacing.")]
        [SerializeField] private float overlapSampleStep = 5f;
        [Tooltip("R7: centerline samples of two modules stay at least twice this apart. Half width 6 + guard 0.3 + clearance 4.")]
        [SerializeField] private float overlapMargin = 10.3f;
        [Tooltip("Every drawn module must leave this much straight road ahead clear of the course (R7 distance), so escape rests have room.")]
        [SerializeField] private float lookaheadLength = 300f;
        [Tooltip("Candidates tried per module before the escape rest.")]
        [SerializeField] private int candidateAttempts = 8;

        [Header("Open edges (no guard, falling allowed)")]
        [SerializeField] private float openEdgeStart = 600f;
        [Tooltip("Chance that an eligible straight or gentle curve drops one guard.")]
        [SerializeField] private float openEdgeChance = 0.2f;
        [Tooltip("Open-edge modules never exceed this share of all modules so far.")]
        [SerializeField] private float openEdgeMaxShare = 0.1f;

        public float FullDistance => fullDistance;
        public float SlopeRampLength => slopeRampLength;
        public float GapApproachLength => gapApproachLength;
        public float GrappleAnchorHeight => grappleAnchorHeight;
        public float SpawnRestLength => spawnRestLength;
        public float EscapeRestLength => escapeRestLength;
        public float RestSpacing => restSpacing;
        public float TurnWindow => turnWindow;
        public float MaxNetTurn => maxNetTurn;
        public float MaxHeightOffset => maxHeightOffset;
        public float OverlapWindow => overlapWindow;
        public float OverlapSampleStep => overlapSampleStep;
        public float OverlapMargin => overlapMargin;
        public float LookaheadLength => lookaheadLength;
        public int CandidateAttempts => candidateAttempts;
        public float OpenEdgeStart => openEdgeStart;
        public float OpenEdgeChance => openEdgeChance;
        public float OpenEdgeMaxShare => openEdgeMaxShare;

        // 0 at the start, 1 from fullDistance on.
        public float Progress(double s) => Mathf.Clamp01((float)(s / fullDistance));

        // Draw weight of a kind at s. Rests are never drawn; rule R3 forces them.
        public float Weight(ModuleKind kind, double s)
        {
            switch (kind)
            {
                case ModuleKind.Straight: return At(straightWeight, s);
                case ModuleKind.Rest: return 0f;
                case ModuleKind.GentleCurve: return At(gentleCurveWeight, s);
                case ModuleKind.SharpCurve: return At(sharpCurveWeight, s);
                case ModuleKind.SCurve: return At(sCurveWeight, s);
                case ModuleKind.Uphill: return At(uphillWeight, s);
                case ModuleKind.Downhill: return At(downhillWeight, s);
                case ModuleKind.Hill: return At(hillWeight, s);
                case ModuleKind.JumpGap: return At(jumpGapWeight, s);
                case ModuleKind.GrappleGap: return At(grappleGapWeight, s);
                default: throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Unknown module kind.");
            }
        }

        public Vector2 StraightLength(double s) => At(straightLengthStart, straightLengthFull, s);
        public Vector2 RestLength(double s) => At(restLengthStart, restLengthFull, s);
        public Vector2 GentleRadius(double s) => At(gentleRadiusStart, gentleRadiusFull, s);
        public Vector2 GentleDegrees(double s) => At(gentleDegreesStart, gentleDegreesFull, s);
        public Vector2 SharpRadius(double s) => At(sharpRadiusStart, sharpRadiusFull, s);
        public Vector2 SharpDegrees(double s) => At(sharpDegreesStart, sharpDegreesFull, s);
        public Vector2 SCurveRadius(double s) => At(sCurveRadiusStart, sCurveRadiusFull, s);
        public Vector2 SCurveDegrees(double s) => At(sCurveDegreesStart, sCurveDegreesFull, s);
        public Vector2 UphillGrade(double s) => At(uphillGradeStart, uphillGradeFull, s);
        public Vector2 DownhillGrade(double s) => At(downhillGradeStart, downhillGradeFull, s);
        public Vector2 SlopeLength(double s) => At(slopeLengthStart, slopeLengthFull, s);
        public Vector2 HillGrade(double s) => At(hillGradeStart, hillGradeFull, s);
        public Vector2 HillPieceLength(double s) => At(hillPieceLengthStart, hillPieceLengthFull, s);
        public Vector2 JumpGap(double s) => At(jumpGapStart, jumpGapFull, s);
        public Vector2 GrappleGap(double s) => At(grappleGapStart, grappleGapFull, s);
        public Vector2 GrappleAnchorSide(double s) => At(grappleAnchorSideStart, grappleAnchorSideFull, s);

        // The first problem found, or null when the tuning is usable. CourseGenerator throws on a problem.
        public string FindProblem()
        {
            if (!(fullDistance > 0f)) return "fullDistance must be positive.";
            if (!(straightWeight.x >= 0f && straightWeight.y >= 0f && gentleCurveWeight.x >= 0f && gentleCurveWeight.y >= 0f
                  && sharpCurveWeight.x >= 0f && sharpCurveWeight.y >= 0f && sCurveWeight.x >= 0f && sCurveWeight.y >= 0f
                  && uphillWeight.x >= 0f && uphillWeight.y >= 0f && downhillWeight.x >= 0f && downhillWeight.y >= 0f
                  && hillWeight.x >= 0f && hillWeight.y >= 0f && jumpGapWeight.x >= 0f && jumpGapWeight.y >= 0f
                  && grappleGapWeight.x >= 0f && grappleGapWeight.y >= 0f))
                return "Weights cannot be negative.";
            if (!(TotalWeight(0d) > 0f) || !(TotalWeight(fullDistance) > 0f))
                return "Weights must not all be zero at 0m or at full distance.";

            string problem = Range("straightLength", straightLengthStart, straightLengthFull, 0f, float.MaxValue)
                ?? Range("restLength", restLengthStart, restLengthFull, MinRestLength, float.MaxValue)
                ?? Range("gentleRadius", gentleRadiusStart, gentleRadiusFull, MinRadius, float.MaxValue)
                ?? Range("gentleDegrees", gentleDegreesStart, gentleDegreesFull, 0f, 180f)
                ?? Range("sharpRadius", sharpRadiusStart, sharpRadiusFull, MinRadius, float.MaxValue)
                ?? Range("sharpDegrees", sharpDegreesStart, sharpDegreesFull, 0f, 180f)
                ?? Range("sCurveRadius", sCurveRadiusStart, sCurveRadiusFull, MinRadius, float.MaxValue)
                ?? Range("sCurveDegrees", sCurveDegreesStart, sCurveDegreesFull, 0f, 180f)
                ?? Range("uphillGrade", uphillGradeStart, uphillGradeFull, 0f, GradeLimit)
                ?? Range("downhillGrade", downhillGradeStart, downhillGradeFull, 0f, GradeLimit)
                ?? Range("slopeLength", slopeLengthStart, slopeLengthFull, 0f, float.MaxValue)
                ?? Range("hillGrade", hillGradeStart, hillGradeFull, 0f, GradeLimit)
                ?? Range("hillPieceLength", hillPieceLengthStart, hillPieceLengthFull, 0f, float.MaxValue)
                ?? Range("jumpGap", jumpGapStart, jumpGapFull, 0f, JumpGapLimit)
                ?? Range("grappleGap", grappleGapStart, grappleGapFull, 0f, float.MaxValue);
            if (problem != null) return problem;
            if (grappleAnchorSideStart.x > grappleAnchorSideStart.y || grappleAnchorSideFull.x > grappleAnchorSideFull.y)
                return "grappleAnchorSide ranges need min <= max.";

            if (!(slopeRampLength > 0f)) return "slopeRampLength must be positive.";
            if (!(gapApproachLength >= MinGapApproach)) return $"gapApproachLength must be at least {MinGapApproach} (rule R1).";
            if (!(grappleAnchorHeight > 0f)) return "grappleAnchorHeight must be positive.";
            if (!(spawnRestLength >= MinRestLength) || !(escapeRestLength >= MinRestLength))
                return $"spawnRestLength and escapeRestLength must be at least {MinRestLength}.";
            if (!(turnWindow > 0f) || !(maxNetTurn > 0f) || !(maxHeightOffset > 0f))
                return "turnWindow, maxNetTurn and maxHeightOffset must be positive.";
            if (!(overlapWindow > 0f) || !(overlapSampleStep > 0f) || !(overlapMargin >= 0f))
                return "overlapWindow and overlapSampleStep must be positive and overlapMargin non-negative.";
            if (!(lookaheadLength >= escapeRestLength)) return "lookaheadLength must be at least escapeRestLength.";
            if (candidateAttempts < 1) return "candidateAttempts must be at least 1.";
            if (!(openEdgeStart >= 0f) || !(openEdgeChance >= 0f && openEdgeChance <= 1f) || !(openEdgeMaxShare >= 0f && openEdgeMaxShare <= 1f))
                return "openEdgeStart must be non-negative and openEdgeChance, openEdgeMaxShare within [0, 1].";

            // A kind longer than the rest spacing could never be placed: R3 would always force a rest instead.
            if (!(restSpacing >= spawnRestLength)) return "restSpacing must be at least spawnRestLength.";
            for (int step = 0; step <= 10; step++)
            {
                double s = fullDistance * step / 10.0;
                float longest = LongestModule(s);
                if (longest > restSpacing)
                    return $"At {s:F0}m a module can be {longest:F1}m long, more than restSpacing {restSpacing}; it could never be placed.";
            }
            return null;
        }

        private float TotalWeight(double s)
        {
            float total = 0f;
            for (var kind = ModuleKind.Straight; kind <= ModuleKind.GrappleGap; kind++) total += Weight(kind, s);
            return total;
        }

        private float LongestModule(double s)
        {
            float gentle = GentleRadius(s).y * GentleDegrees(s).y * Mathf.Deg2Rad;
            float sharp = SharpRadius(s).y * SharpDegrees(s).y * Mathf.Deg2Rad;
            float sCurve = 2f * SCurveRadius(s).y * SCurveDegrees(s).y * Mathf.Deg2Rad;
            float slope = 2f * slopeRampLength + SlopeLength(s).y;
            float hill = 3f * HillPieceLength(s).y;
            float gap = 2f * gapApproachLength + Mathf.Max(JumpGap(s).y, GrappleGap(s).y);
            return Mathf.Max(Mathf.Max(Mathf.Max(StraightLength(s).y, RestLength(s).y), Mathf.Max(gentle, sharp)),
                Mathf.Max(Mathf.Max(sCurve, slope), Mathf.Max(hill, gap)));
        }

        private static string Range(string name, Vector2 start, Vector2 full, float floor, float ceiling)
        {
            if (!(start.x > 0f && start.x >= floor && full.x > 0f && full.x >= floor))
                return $"{name} minimum must be positive and at least {floor}.";
            if (start.x > start.y || full.x > full.y) return $"{name} ranges need min <= max.";
            if (start.y > ceiling || full.y > ceiling) return $"{name} cannot exceed {ceiling}.";
            return null;
        }

        private float At(Vector2 pair, double s) => Mathf.Lerp(pair.x, pair.y, Progress(s));

        private Vector2 At(Vector2 start, Vector2 full, double s) => Vector2.Lerp(start, full, Progress(s));

        private void OnValidate()
        {
            string problem = FindProblem();
            if (problem != null) Debug.LogError($"CourseTuning: {problem}", this);
        }
    }
}
