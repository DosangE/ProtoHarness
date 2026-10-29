using System;
using NUnit.Framework;
using ProtoHarness.ChainRush.Flow;

namespace ProtoHarness.Tests.EditMode
{
    // Best records per track and speed class (user 2026-10-19), and each runner's own (M4). A test prefix keeps the player's records untouched.
    public sealed class BestRecordsTests
    {
        private const string Track = "TestTrack";
        private BestRecords records;

        [SetUp]
        public void SetUp()
        {
            records = new BestRecords("ChainRush.Test.BestRecordsTests.");
            Clear();
        }

        [TearDown]
        public void TearDown() => Clear();

        private void Clear()
        {
            foreach (SpeedClass speed in Enum.GetValues(typeof(SpeedClass)))
            {
                records.Clear(Track, speed);
                records.ClearForRunner(Track, speed, "RunnerProfile_Beta");
                records.ClearForRunner(Track, speed, "RunnerProfile_Theta");
            }
        }

        [Test]
        public void TryGet_NoFinishYet_ReturnsFalse()
        {
            Assert.That(records.TryGet(Track, SpeedClass.Fast, out _), Is.False);
        }

        [Test]
        public void Submit_FirstFinish_IsKeptWithItsRunner()
        {
            Assert.That(records.Submit(Track, SpeedClass.Fast, 4000, "BETA"), Is.True);
            Assert.That(records.TryGet(Track, SpeedClass.Fast, out BestRecords.Record best), Is.True);
            Assert.That(best.Ticks, Is.EqualTo(4000));
            Assert.That(best.Runner, Is.EqualTo("BETA"));
        }

        [Test]
        public void Submit_SlowerOrTied_KeepsTheOlder_FasterReplacesIt()
        {
            records.Submit(Track, SpeedClass.Fast, 4000, "BETA");
            Assert.That(records.Submit(Track, SpeedClass.Fast, 4100, "THETA"), Is.False);
            Assert.That(records.Submit(Track, SpeedClass.Fast, 4000, "THETA"), Is.False, "A tie keeps the older record.");
            records.TryGet(Track, SpeedClass.Fast, out BestRecords.Record kept);
            Assert.That(kept.Runner, Is.EqualTo("BETA"));
            Assert.That(records.Submit(Track, SpeedClass.Fast, 3900, "OMEGA"), Is.True);
            records.TryGet(Track, SpeedClass.Fast, out BestRecords.Record faster);
            Assert.That(faster.Ticks, Is.EqualTo(3900));
            Assert.That(faster.Runner, Is.EqualTo("OMEGA"));
        }

        [Test]
        public void Submit_SpeedClasses_AreKeptApart()
        {
            records.Submit(Track, SpeedClass.Fast, 4000, "BETA");
            Assert.That(records.TryGet(Track, SpeedClass.Normal, out _), Is.False);
            Assert.That(records.TryGet(Track, SpeedClass.VeryFast, out _), Is.False);
        }

        // ---- each runner's own best (M4) ----

        [Test]
        public void SubmitForRunner_RunnersAreKeptApart_AndTheBestAboveIsUntouched()
        {
            Assert.That(records.SubmitForRunner(Track, SpeedClass.Fast, "RunnerProfile_Beta", 4000), Is.True);
            Assert.That(records.SubmitForRunner(Track, SpeedClass.Fast, "RunnerProfile_Theta", 4200), Is.True, "A slower runner keeps its own best.");
            Assert.That(records.TryGetForRunner(Track, SpeedClass.Fast, "RunnerProfile_Beta", out int beta), Is.True);
            Assert.That(records.TryGetForRunner(Track, SpeedClass.Fast, "RunnerProfile_Theta", out int theta), Is.True);
            Assert.That(beta, Is.EqualTo(4000));
            Assert.That(theta, Is.EqualTo(4200));
            Assert.That(records.TryGetForRunner(Track, SpeedClass.Normal, "RunnerProfile_Beta", out _), Is.False);
            Assert.That(records.TryGet(Track, SpeedClass.Fast, out _), Is.False, "A runner's best is not the best for the track.");
        }

        [Test]
        public void SubmitForRunner_SlowerOrTied_KeepsTheOlder_FasterReplacesIt()
        {
            records.SubmitForRunner(Track, SpeedClass.Fast, "RunnerProfile_Beta", 4000);
            Assert.That(records.SubmitForRunner(Track, SpeedClass.Fast, "RunnerProfile_Beta", 4100), Is.False);
            Assert.That(records.SubmitForRunner(Track, SpeedClass.Fast, "RunnerProfile_Beta", 4000), Is.False, "A tie keeps the older record.");
            Assert.That(records.SubmitForRunner(Track, SpeedClass.Fast, "RunnerProfile_Beta", 3900), Is.True);
            records.TryGetForRunner(Track, SpeedClass.Fast, "RunnerProfile_Beta", out int best);
            Assert.That(best, Is.EqualTo(3900));
        }

        [Test]
        public void Arguments_Invalid_Throw()
        {
            Assert.Throws<ArgumentException>(() => records.TryGetForRunner(Track, SpeedClass.Fast, "", out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => records.SubmitForRunner(Track, SpeedClass.Fast, "RunnerProfile_Beta", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => records.SubmitForRunner(Track, (SpeedClass)7, "RunnerProfile_Beta", 4000));
            Assert.Throws<ArgumentException>(() => new BestRecords(""));
            Assert.Throws<ArgumentException>(() => records.TryGet("", SpeedClass.Fast, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => records.TryGet(Track, (SpeedClass)7, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => records.Submit(Track, SpeedClass.Fast, 0, "BETA"));
            Assert.Throws<ArgumentException>(() => records.Submit(Track, SpeedClass.Fast, 4000, ""));
        }
    }
}
