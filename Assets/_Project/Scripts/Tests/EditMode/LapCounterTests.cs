using System;
using NUnit.Framework;
using ProtoHarness.ChainRush.Track;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class LapCounterTests
    {
        private const double Lap = 531.3;
        private const double Step = 0.2;
        private static readonly double[] Checkpoints = { 132.8, 265.6, 398.5 };

        private static LapCounter Counter(int laps = 3) => new LapCounter(Lap, Checkpoints, laps);

        // Moves the runner `metres` forward in 0.2 m steps from the current S, folding S into one lap.
        private static double Drive(LapCounter counter, double s, double metres, ref int tick)
        {
            int steps = (int)Math.Round(Math.Abs(metres) / Step);
            double direction = metres < 0d ? -1d : 1d;
            for (int i = 0; i < steps; i++)
            {
                s += direction * Step;
                tick++;
                counter.Update(Fold(s), tick);
            }
            return s;
        }

        private static double Fold(double s) => s - Math.Floor(s / Lap) * Lap;

        [Test]
        public void ThreeLaps_RunStraightThrough_FinishAfterThreeLapLengths()
        {
            LapCounter counter = Counter();
            int tick = 0;
            counter.Begin(0d, tick);
            double s = 0d;
            s = Drive(counter, s, Lap * 3d - 1d, ref tick);
            Assert.That(counter.IsFinished, Is.False);
            Assert.That(counter.CompletedLaps, Is.EqualTo(2));
            Assert.That(counter.CurrentLap, Is.EqualTo(3));
            Drive(counter, s, 2d, ref tick);
            Assert.That(counter.IsFinished, Is.True);
            Assert.That(counter.CompletedLaps, Is.EqualTo(3));
            Assert.That(counter.CurrentLap, Is.EqualTo(3), "Stays on the last lap after the finish.");
            Assert.That(counter.RaceFraction, Is.EqualTo(1d).Within(1e-3));
        }

        [Test]
        public void LapTimes_ConstantSpeed_EqualLapLengthOverStepAndSumToTheTotal()
        {
            LapCounter counter = Counter();
            int tick = 0;
            counter.Begin(0d, tick);
            Drive(counter, 0d, Lap * 3d + 1d, ref tick);
            int expected = (int)Math.Round(Lap / Step);
            int sum = 0;
            for (int lap = 1; lap <= 3; lap++)
            {
                Assert.That(counter.LapTicks(lap), Is.EqualTo(expected).Within(1), $"lap {lap}");
                sum += counter.LapTicks(lap);
            }
            Assert.That(counter.TotalTicks, Is.EqualTo(sum));
            Assert.That(counter.LapEndTick(3), Is.EqualTo(counter.TotalTicks));
        }

        [Test]
        public void StartTick_NotZero_LapOneCountsFromIt()
        {
            LapCounter counter = Counter(1);
            counter.Begin(0d, 100);
            int tick = 100;
            Drive(counter, 0d, Lap + 1d, ref tick);
            Assert.That(counter.LapTicks(1), Is.EqualTo((int)Math.Round(Lap / Step)).Within(1));
            Assert.That(counter.TotalTicks, Is.EqualTo(counter.LapTicks(1)));
        }

        [Test]
        public void Checkpoints_ReachedInOrderEachLap_RecordTheirTicks()
        {
            LapCounter counter = Counter(2);
            int tick = 0;
            counter.Begin(0d, tick);
            double s = Drive(counter, 0d, 140d, ref tick);
            Assert.That(counter.CheckpointsPassed, Is.EqualTo(1));
            Assert.That(counter.CheckpointTick(1, 0), Is.GreaterThan(0));
            Assert.That(counter.CheckpointTick(1, 1), Is.EqualTo(-1));
            Drive(counter, s, Lap - 140d + 140d, ref tick);
            Assert.That(counter.CompletedLaps, Is.EqualTo(1));
            Assert.That(counter.CheckpointsPassed, Is.EqualTo(1), "Lap 2 has its first checkpoint behind it.");
            Assert.That(counter.CheckpointTick(1, 2), Is.GreaterThan(counter.CheckpointTick(1, 1)));
            Assert.That(counter.CheckpointTick(2, 0), Is.GreaterThan(counter.LapEndTick(1)));
        }

        [Test]
        public void StartLine_JitterBackAndForthAfterALap_CountsItOnce()
        {
            LapCounter counter = Counter();
            int tick = 0;
            counter.Begin(0d, tick);
            double s = Drive(counter, 0d, Lap + 0.4d, ref tick);
            Assert.That(counter.CompletedLaps, Is.EqualTo(1));
            for (int i = 0; i < 20; i++)
            {
                s = Drive(counter, s, -1.2d, ref tick);
                s = Drive(counter, s, 1.2d, ref tick);
            }
            Assert.That(counter.CompletedLaps, Is.EqualTo(1), "Jitter across the seam must not add laps.");
            Assert.That(counter.CurrentLap, Is.EqualTo(2));
        }

        [Test]
        public void BackwardsBehindTheStartLine_ThenForward_IsNotALap()
        {
            LapCounter counter = Counter();
            int tick = 0;
            counter.Begin(0d, tick);
            double s = Drive(counter, 0d, -50d, ref tick);
            Assert.That(counter.Progress, Is.EqualTo(-50d).Within(0.3));
            s = Drive(counter, s, 52d, ref tick);
            Assert.That(counter.CompletedLaps, Is.Zero);
            Assert.That(counter.Progress, Is.EqualTo(2d).Within(0.3));
        }

        [Test]
        public void BackwardsPastACheckpoint_ThenForwardAgain_DoesNotCountItTwice()
        {
            LapCounter counter = Counter(1);
            int tick = 0;
            counter.Begin(0d, tick);
            double s = Drive(counter, 0d, 140d, ref tick);
            int first = counter.CheckpointTick(1, 0);
            s = Drive(counter, s, -30d, ref tick);
            s = Drive(counter, s, 30d, ref tick);
            Assert.That(counter.CheckpointsPassed, Is.EqualTo(1));
            Assert.That(counter.CheckpointTick(1, 0), Is.EqualTo(first), "The first pass stays the checkpoint time.");
            Drive(counter, s, Lap, ref tick);
            Assert.That(counter.IsFinished, Is.True);
        }

        [Test]
        public void Update_JumpOverHalfALap_FoldsTheShortWay()
        {
            LapCounter counter = Counter();
            counter.Begin(10d, 0);
            counter.Update(Lap - 10d, 1);
            Assert.That(counter.Progress, Is.EqualTo(-10d).Within(1e-9), "From S 10 to S lap-10 is 20 m backwards, not a lap forward.");
        }

        [Test]
        public void Begin_AfterARace_ResetsEverything()
        {
            LapCounter counter = Counter(1);
            int tick = 0;
            counter.Begin(0d, tick);
            Drive(counter, 0d, Lap + 1d, ref tick);
            Assert.That(counter.IsFinished, Is.True);
            counter.Begin(0d, 0);
            Assert.That(counter.IsFinished, Is.False);
            Assert.That(counter.CompletedLaps, Is.Zero);
            Assert.That(counter.CheckpointsPassed, Is.Zero);
            Assert.That(counter.Progress, Is.Zero);
        }

        [Test]
        public void Update_AfterTheFinish_IsIgnored()
        {
            LapCounter counter = Counter(1);
            int tick = 0;
            counter.Begin(0d, tick);
            Drive(counter, 0d, Lap + 1d, ref tick);
            int total = counter.TotalTicks;
            counter.Update(50d, tick + 100);
            Assert.That(counter.TotalTicks, Is.EqualTo(total));
            Assert.That(counter.CompletedLaps, Is.EqualTo(1));
        }

        [Test]
        public void CurrentLapTicks_CountsFromTheLastLine()
        {
            LapCounter counter = Counter();
            int tick = 0;
            counter.Begin(0d, tick);
            Drive(counter, 0d, Lap + 20d, ref tick);
            Assert.That(counter.CurrentLapTicks(tick), Is.EqualTo((int)Math.Round(20d / Step)).Within(2));
        }

        [Test]
        public void Queries_BeforeTheLapIsFinished_Throw()
        {
            LapCounter counter = Counter();
            counter.Begin(0d, 0);
            Assert.Throws<InvalidOperationException>(() => counter.LapTicks(1));
            Assert.Throws<InvalidOperationException>(() => counter.LapEndTick(1));
            Assert.Throws<InvalidOperationException>(() => { int total = counter.TotalTicks; });
            Assert.Throws<ArgumentOutOfRangeException>(() => counter.LapTicks(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => counter.LapTicks(4));
        }

        [Test]
        public void Update_BeforeBegin_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => Counter().Update(1d, 1));
        }

        [Test]
        public void Constructor_BadValues_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LapCounter(0d, Checkpoints, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LapCounter(Lap, Checkpoints, 0));
            Assert.Throws<ArgumentNullException>(() => new LapCounter(Lap, null, 3));
            Assert.Throws<ArgumentException>(() => new LapCounter(Lap, new[] { 200d, 100d }, 3));
            Assert.Throws<ArgumentException>(() => new LapCounter(Lap, new[] { 0d, 100d }, 3));
            Assert.Throws<ArgumentException>(() => new LapCounter(Lap, new[] { 100d, Lap }, 3));
        }

        [Test]
        public void NoCheckpoints_LapsStillCount()
        {
            var counter = new LapCounter(Lap, Array.Empty<double>(), 2);
            int tick = 0;
            counter.Begin(0d, tick);
            Drive(counter, 0d, Lap * 2d + 1d, ref tick);
            Assert.That(counter.IsFinished, Is.True);
        }
    }
}
