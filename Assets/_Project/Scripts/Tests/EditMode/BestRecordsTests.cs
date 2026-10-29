using System;
using NUnit.Framework;
using ProtoHarness.ChainRush.Flow;

namespace ProtoHarness.Tests.EditMode
{
    // Best records per track and speed class (user 2026-10-19). A test prefix keeps the player's records untouched.
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
            foreach (SpeedClass speed in Enum.GetValues(typeof(SpeedClass))) records.Clear(Track, speed);
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

        [Test]
        public void Arguments_Invalid_Throw()
        {
            Assert.Throws<ArgumentException>(() => new BestRecords(""));
            Assert.Throws<ArgumentException>(() => records.TryGet("", SpeedClass.Fast, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => records.TryGet(Track, (SpeedClass)7, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => records.Submit(Track, SpeedClass.Fast, 0, "BETA"));
            Assert.Throws<ArgumentException>(() => records.Submit(Track, SpeedClass.Fast, 4000, ""));
        }
    }
}
