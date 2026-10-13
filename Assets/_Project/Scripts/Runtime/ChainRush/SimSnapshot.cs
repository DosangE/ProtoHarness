using ProtoHarness.ChainRush.Race;
using UnityEngine;

namespace ProtoHarness.ChainRush
{
    // The whole simulation of a circuit race between two ticks, by value: the tick count, the racer's state, the
    // runner's position and the race's lap state. ChainRushGame.CaptureSnapshot makes one and RestoreSnapshot puts
    // the game back to it, so a prediction can be thrown away and run again from the same input (DESIGN.md P3).
    // The world is the fixed circuit; nothing else carries over from a tick to the next.
    public readonly struct SimSnapshot
    {
        public readonly int Tick;
        public readonly RacerState.Snapshot Racer;
        public readonly Vector3 Position;
        public readonly CircuitRace.Snapshot Circuit;

        public SimSnapshot(int tick, RacerState.Snapshot racer, Vector3 position, CircuitRace.Snapshot circuit)
        {
            Tick = tick;
            Racer = racer;
            Position = position;
            Circuit = circuit;
        }
    }
}
