using System;
using System.Collections.Generic;

namespace ProtoHarness.ChainRush.Control
{
    // The controls of one run, one TickInput per simulation tick in order: entry i is what the simulation
    // consumed on tick i + 1 (ticks count from 1 after StartRun). Recording and replaying it with
    // InputRecorder and InputReplay, with the same seed, should repeat the run exactly (DESIGN.md P2).
    public sealed class InputLog
    {
        private readonly List<TickInput> inputs;

        public InputLog() => inputs = new List<TickInput>(1024);

        private InputLog(List<TickInput> inputs) => this.inputs = inputs;

        public int Count => inputs.Count;

        public TickInput this[int index]
        {
            get
            {
                if (index < 0 || index >= inputs.Count) throw new ArgumentOutOfRangeException(nameof(index), index, $"The log holds {inputs.Count} ticks.");
                return inputs[index];
            }
        }

        public void Add(in TickInput input) => inputs.Add(input);

        public void Clear() => inputs.Clear();

        // A copy with one entry replaced, for checking that a changed input changes the run.
        public InputLog CopyWith(int index, in TickInput replacement)
        {
            if (index < 0 || index >= inputs.Count) throw new ArgumentOutOfRangeException(nameof(index), index, $"The log holds {inputs.Count} ticks.");
            var copy = new List<TickInput>(inputs);
            copy[index] = replacement;
            return new InputLog(copy);
        }
    }
}
