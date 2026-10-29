using System;
using NUnit.Framework;
using ProtoHarness.ChainRush.Flow;

namespace ProtoHarness.Tests.EditMode
{
    // A finish against the records kept before it, as the result screen shows it (M4-2, DECISIONS 2026-10-29
    // "M4-2 결과 화면 비교"). A tick is 0.02 s, so 3970 ticks is 01:19.4.
    public sealed class FinishComparisonTests
    {
        [Test]
        public void FormatDelta_AheadBehindAndLevel_InExactHundredths()
        {
            Assert.That(FinishComparison.FormatDelta(-30), Is.EqualTo("-0.60"));
            Assert.That(FinishComparison.FormatDelta(25), Is.EqualTo("+0.50"));
            Assert.That(FinishComparison.FormatDelta(1), Is.EqualTo("+0.02"));
            Assert.That(FinishComparison.FormatDelta(-3001), Is.EqualTo("-60.02"));
            Assert.That(FinishComparison.FormatDelta(0), Is.EqualTo("±0.00"));
        }

        [Test]
        public void NoEarlierRecords_IsNewEverywhere_AndSaysFirstRecord()
        {
            var finish = new FinishComparison(3970, 0, 0, "");
            Assert.That(finish.IsNewRunnerBest, Is.True);
            Assert.That(finish.IsNewBest, Is.True);
            Assert.That(finish.RunnerLine, Is.EqualTo("내 최고  첫 기록"));
            Assert.That(finish.BestLine, Is.EqualTo("전체 최고  첫 기록"));
            Assert.That(finish.RunnerDelta, Is.Empty);
            Assert.That(finish.BestDelta, Is.Empty);
        }

        [Test]
        public void FasterThanTheRunnersBest_SlowerThanTheTracks_ShowsBothDifferences()
        {
            var finish = new FinishComparison(3970, 4000, 3945, "BETA");
            Assert.That(finish.IsNewRunnerBest, Is.True);
            Assert.That(finish.IsNewBest, Is.False);
            Assert.That(finish.RunnerLine, Is.EqualTo("내 최고  01:20.0"));
            Assert.That(finish.RunnerDelta, Is.EqualTo("-0.60  NEW"));
            Assert.That(finish.BestLine, Is.EqualTo("전체 최고  01:18.9 · BETA"));
            Assert.That(finish.BestDelta, Is.EqualTo("+0.50"));
        }

        [Test]
        public void Tie_IsNotANewRecord()
        {
            var finish = new FinishComparison(3970, 3970, 3970, "BETA");
            Assert.That(finish.IsNewRunnerBest, Is.False);
            Assert.That(finish.IsNewBest, Is.False);
            Assert.That(finish.RunnerDelta, Is.EqualTo("±0.00"));
        }

        [Test]
        public void Arguments_Invalid_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FinishComparison(0, 0, 0, ""));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FinishComparison(3970, -1, 0, ""));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FinishComparison(3970, 0, -1, ""));
            Assert.Throws<ArgumentException>(() => new FinishComparison(3970, 0, 3945, ""), "a kept best without its runner");
        }
    }
}
