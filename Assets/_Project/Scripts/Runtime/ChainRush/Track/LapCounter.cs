using System;
using System.Collections.Generic;

namespace ProtoHarness.ChainRush.Track
{
    // Counts laps on a loop from the runner's S (COURSE.md T4). S arrives folded into one lap, so the counter
    // adds up every tick's movement (each step folded into [-lap/2, lap/2], far above what one tick can
    // cover) into a running progress, and a lap is done when that progress reaches the next multiple of the
    // lap length after the lap's checkpoints were reached in order. Going backwards only lowers the progress:
    // a checkpoint or line that was reached stays reached, so no line is counted twice, and the start line
    // crossed backwards before the run begins is not a lap. Tick numbers are the caller's simulation ticks.
    public sealed class LapCounter
    {
        private readonly double lapLength;
        private readonly double[] checkpoints;
        private readonly int lapCount;
        private readonly int[] lapEndTicks;
        private readonly int[] checkpointTicks;
        private double progress;
        private double lastS;
        private bool started;
        private int completed;
        private int nextCheckpoint;
        private int startTick;

        // checkpoints are S values strictly inside (0, lapLength), ascending.
        public LapCounter(double lapLength, IReadOnlyList<double> checkpoints, int lapCount)
        {
            if (!(lapLength > 0d) || double.IsInfinity(lapLength))
                throw new ArgumentOutOfRangeException(nameof(lapLength), lapLength, "Lap length must be positive and finite.");
            if (lapCount < 1) throw new ArgumentOutOfRangeException(nameof(lapCount), lapCount, "A race needs at least one lap.");
            if (checkpoints == null) throw new ArgumentNullException(nameof(checkpoints));
            double previous = 0d;
            for (int i = 0; i < checkpoints.Count; i++)
            {
                if (!(checkpoints[i] > previous) || !(checkpoints[i] < lapLength))
                    throw new ArgumentException($"Checkpoint {i} ({checkpoints[i]}) must lie after the one before it and inside the lap (0, {lapLength}).", nameof(checkpoints));
                previous = checkpoints[i];
            }
            this.lapLength = lapLength;
            this.lapCount = lapCount;
            this.checkpoints = new double[checkpoints.Count];
            for (int i = 0; i < this.checkpoints.Length; i++) this.checkpoints[i] = checkpoints[i];
            lapEndTicks = new int[lapCount];
            checkpointTicks = new int[lapCount * this.checkpoints.Length];
        }

        public double LapLength => lapLength;
        public int LapCount => lapCount;
        public int CheckpointCount => checkpoints.Length;
        public bool HasStarted => started;
        public int CompletedLaps => completed;
        public bool IsFinished => completed >= lapCount;
        // The lap the runner is on, 1-based; it stays at the last lap once the race is finished.
        public int CurrentLap => Math.Min(completed + 1, lapCount);
        // Checkpoints reached in the current lap.
        public int CheckpointsPassed => nextCheckpoint;
        // Metres run along the track since the start, counting laps (negative behind the start line).
        public double Progress => progress;
        // 0..1 over the whole race.
        public double RaceFraction => Math.Max(0d, Math.Min(1d, progress / (lapLength * lapCount)));

        // Starts a race with the runner at s on tick `tick` (S 0 is the start line).
        public void Begin(double s, int tick)
        {
            if (double.IsNaN(s) || double.IsInfinity(s)) throw new ArgumentOutOfRangeException(nameof(s), s, "S must be finite.");
            progress = Fold(s);
            lastS = s;
            started = true;
            completed = 0;
            nextCheckpoint = 0;
            startTick = tick;
            Array.Clear(lapEndTicks, 0, lapEndTicks.Length);
            Array.Clear(checkpointTicks, 0, checkpointTicks.Length);
        }

        public void Update(double s, int tick)
        {
            if (!started) throw new InvalidOperationException("LapCounter.Begin must be called before Update.");
            if (double.IsNaN(s) || double.IsInfinity(s)) throw new ArgumentOutOfRangeException(nameof(s), s, "S must be finite.");
            if (IsFinished) return;
            progress += Fold(s - lastS);
            lastS = s;
            while (!IsFinished)
            {
                double lapStart = completed * lapLength;
                if (nextCheckpoint < checkpoints.Length)
                {
                    if (progress < lapStart + checkpoints[nextCheckpoint]) break;
                    checkpointTicks[completed * checkpoints.Length + nextCheckpoint] = tick;
                    nextCheckpoint++;
                }
                else
                {
                    if (progress < lapStart + lapLength) break;
                    lapEndTicks[completed] = tick;
                    completed++;
                    nextCheckpoint = 0;
                }
            }
        }

        // The tick on which the 1-based lap was completed.
        public int LapEndTick(int lap)
        {
            RequireCompleted(lap);
            return lapEndTicks[lap - 1];
        }

        // Ticks from the start of the lap (the race start for lap 1) to its end.
        public int LapTicks(int lap)
        {
            RequireCompleted(lap);
            return lapEndTicks[lap - 1] - (lap == 1 ? startTick : lapEndTicks[lap - 2]);
        }

        // Ticks of the whole race, once it is finished.
        public int TotalTicks
        {
            get
            {
                if (!IsFinished) throw new InvalidOperationException("The race is not finished.");
                return lapEndTicks[lapCount - 1] - startTick;
            }
        }

        // Ticks since the current lap began.
        public int CurrentLapTicks(int tick) => tick - (completed == 0 ? startTick : lapEndTicks[completed - 1]);

        // The tick on which a checkpoint (0-based) of a completed or current lap was reached, or -1.
        public int CheckpointTick(int lap, int checkpoint)
        {
            if (lap < 1 || lap > lapCount) throw new ArgumentOutOfRangeException(nameof(lap), lap, $"Laps run 1 to {lapCount}.");
            if (checkpoint < 0 || checkpoint >= checkpoints.Length) throw new ArgumentOutOfRangeException(nameof(checkpoint), checkpoint, $"Checkpoints run 0 to {checkpoints.Length - 1}.");
            bool reached = lap <= completed || (lap == completed + 1 && checkpoint < nextCheckpoint);
            return reached ? checkpointTicks[(lap - 1) * checkpoints.Length + checkpoint] : -1;
        }

        private void RequireCompleted(int lap)
        {
            if (lap < 1 || lap > lapCount) throw new ArgumentOutOfRangeException(nameof(lap), lap, $"Laps run 1 to {lapCount}.");
            if (lap > completed) throw new InvalidOperationException($"Lap {lap} is not finished yet.");
        }

        // A difference folded into [-lap/2, lap/2].
        private double Fold(double difference) => difference - Math.Round(difference / lapLength) * lapLength;
    }
}
