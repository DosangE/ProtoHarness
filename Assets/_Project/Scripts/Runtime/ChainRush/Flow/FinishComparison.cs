using System;
using System.Globalization;

namespace ProtoHarness.ChainRush.Flow
{
    // A finish against the records that were kept before it (M4-2, DECISIONS 2026-10-29 "M4-2 결과 화면 비교"): the
    // runner's own best and the best for the track and speed class, with the lines the result screen shows. No earlier
    // record is 0 ticks. A tie is not a new record (BestRecords keeps the older).
    public readonly struct FinishComparison
    {
        public int Ticks { get; }
        public int RunnerBestBefore { get; }
        public int BestBefore { get; }
        public string BestBeforeRunner { get; }

        public FinishComparison(int ticks, int runnerBestBefore, int bestBefore, string bestBeforeRunner)
        {
            if (ticks <= 0) throw new ArgumentOutOfRangeException(nameof(ticks), ticks, "A finish takes at least one tick.");
            if (runnerBestBefore < 0) throw new ArgumentOutOfRangeException(nameof(runnerBestBefore), runnerBestBefore, "0 means no record.");
            if (bestBefore < 0) throw new ArgumentOutOfRangeException(nameof(bestBefore), bestBefore, "0 means no record.");
            if (bestBefore > 0 && string.IsNullOrEmpty(bestBeforeRunner)) throw new ArgumentException("A kept best has the runner that set it.", nameof(bestBeforeRunner));
            Ticks = ticks;
            RunnerBestBefore = runnerBestBefore;
            BestBefore = bestBefore;
            BestBeforeRunner = bestBefore > 0 ? bestBeforeRunner : "";
        }

        public bool IsNewRunnerBest => RunnerBestBefore == 0 || Ticks < RunnerBestBefore;
        public bool IsNewBest => BestBefore == 0 || Ticks < BestBefore;

        // "내 최고  01:19.4", "전체 최고  01:18.9 · BETA", or "첫 기록" when there was none.
        public string RunnerLine => "내 최고  " + (RunnerBestBefore == 0 ? "첫 기록" : ChainRushHud.FormatTime(ChainRush.Ticks.ToSeconds(RunnerBestBefore)));
        public string BestLine => "전체 최고  " + (BestBefore == 0 ? "첫 기록" : ChainRushHud.FormatTime(ChainRush.Ticks.ToSeconds(BestBefore)) + " · " + BestBeforeRunner);

        // This finish against the record, "NEW" when it beat it, empty when there was none.
        public string RunnerDelta => Delta(RunnerBestBefore);
        public string BestDelta => Delta(BestBefore);

        private string Delta(int before)
        {
            if (before == 0) return "";
            string delta = FormatDelta(Ticks - before);
            return Ticks < before ? delta + "  NEW" : delta;
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
