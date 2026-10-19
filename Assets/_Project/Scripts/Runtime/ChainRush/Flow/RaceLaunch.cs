using System;
using UnityEngine;

namespace ProtoHarness.ChainRush.Flow
{
    // The hand-off between scenes: the lobby leaves a RaceSetup here and loads the race scene, whose RaceAssembler takes
    // it once in its Awake. A race scene opened directly (tests, the editor) finds nothing and runs as it was saved.
    // Scenes are loaded by name, so all of them must be in the build's scene list.
    public static class RaceLaunch
    {
        public const string TitleScene = "ChainRushTitle";
        public const string LobbyScene = "ChainRushLobby";
        // The scene a race runs in: runner, camera, HUD and the circuit built from the chosen definition.
        public const string RaceScene = "ChainRushCircuit";

        private static RaceSetup pending;
        private static bool hasPending;

        public static bool HasPending => hasPending;

        // A race scene must take a request before the next one is made; a second request would silently replace the first.
        public static void Request(in RaceSetup setup)
        {
            if (setup.Track == null) throw new ArgumentException("The setup is empty; build it with its constructor.", nameof(setup));
            if (hasPending) throw new InvalidOperationException("RaceLaunch: a race was requested and has not started yet.");
            pending = setup;
            hasPending = true;
        }

        public static bool TryTake(out RaceSetup setup)
        {
            setup = pending;
            if (!hasPending) return false;
            pending = default;
            hasPending = false;
            return true;
        }

        // Drops a request no race scene will take (a load that was abandoned, a test's leftovers).
        public static void Clear()
        {
            pending = default;
            hasPending = false;
        }

        // Play mode can be entered without a domain reload, which would keep a request from the last session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Clear();
    }
}
