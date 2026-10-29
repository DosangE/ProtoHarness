using System;
using System.Globalization;

namespace ProtoHarness.ChainRush.Flow
{
    // A finish against the best that was kept before it for its track and speed class (M4-2, DECISIONS 2026-10-29
    // "M4-2 결과 화면 비교"; one best per track and speed since "고스트: 트랙 × 속도의 최고 기록 하나"), with the line the
    // result screen shows. No earlier record is 0 ticks. A tie is not a new record (BestRecords keeps the older).
    public readonly struct FinishComparison
    {
        public int Ticks { get; }
        public int BestBefore { get; }
        public string BestBeforeRunner { get; }

        public FinishComparison(int ticks, int bestBefore, string bestBeforeRunner)
        {
            if (ticks <= 0) throw new ArgumentOutOfRangeException(nameof(ticks), ticks, "A finish takes at least one tick.");
            if (bestBefore < 0) throw new ArgumentOutOfRangeException(nameof(bestBefore), bestBefore, "0 means no record.");
            if (bestBefore > 0 && string.IsNullOrEmpty(bestBeforeRunner)) throw new ArgumentException("A kept best has the runner that set it.", nameof(bestBeforeRunner));
            Ticks = ticks;
            BestBefore = bestBefore;
            BestBeforeRunner = bestBefore > 0 ? bestBeforeRunner : "";
        }

        public bool IsNewBest => BestBefore == 0 || Ticks < BestBefore;

        // "최고 기록  01:18.9 · BETA", or "최고 기록  첫 기록" when there was none.
        public string BestLine => "최고 기록  " + (BestBefore == 0 ? "첫 기록" : ChainRushHud.FormatTime(ChainRush.Ticks.ToSeconds(BestBefore)) + " · " + BestBeforeRunner);

        // This finish against the record, "NEW" when it beat it, empty when there was none.
        public string BestDelta
        {
            get
            {
                if (BestBefore == 0) return "";
                string delta = FormatDelta(Ticks - BestBefore);
                return Ticks < BestBefore ? delta + "  NEW" : delta;
            }
        }

        // Seconds to two places, from ticks so it is exact (a tick is 0.02 s): "-0.60" ahead, "+0.50" behind, "±0.00" level.
        public static string FormatDelta(int ticks)
        {
            if (ticks == 0) return "±0.00";
            int hundredths = Math.Abs(ticks) * (int)Math.Round(ChainRush.Ticks.Seconds * 100f);
            return (ticks < 0 ? "-" : "+") + (hundredths / 100).ToString(CultureInfo.InvariantCulture) + "." + (hundredths % 100).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
