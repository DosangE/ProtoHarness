using System;
using System.Collections.Generic;

namespace ProtoHarness.ChainRush.Flow.Lobby
{
    // What the lobby's pages share: the scene's tracks and runners (checked by LobbyMenu before this is made), the
    // runner preview, where settings and best records are kept, and the choices made so far.
    public sealed class LobbyContext
    {
        private GameSettings settings;
        private BestRecords records;

        public LobbyContext(LobbyMenu.TrackChoice[] tracks, RunnerProfile[] runners, RunnerPreview preview, GameSettings settings, BestRecords records)
        {
            Tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
            Runners = runners ?? throw new ArgumentNullException(nameof(runners));
            Preview = preview ?? throw new ArgumentNullException(nameof(preview));
            Settings = settings;
            Records = records;
        }

        public IReadOnlyList<LobbyMenu.TrackChoice> Tracks { get; }
        public IReadOnlyList<RunnerProfile> Runners { get; }
        public RunnerPreview Preview { get; }

        public GameSettings Settings
        {
            get => settings;
            set => settings = value ?? throw new ArgumentNullException(nameof(value));
        }

        public BestRecords Records
        {
            get => records;
            set => records = value ?? throw new ArgumentNullException(nameof(value));
        }

        // The track, speed class (SpeedPage order) and runner as last confirmed. Speed starts on Fast, the only class
        // unlocked.
        public int Track { get; set; }
        public int Speed { get; set; } = 1;
        public int Runner { get; set; }
    }
}
