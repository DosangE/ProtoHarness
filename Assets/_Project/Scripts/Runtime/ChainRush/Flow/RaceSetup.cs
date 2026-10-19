using System;
using ProtoHarness.ChainRush.Track;

namespace ProtoHarness.ChainRush.Flow
{
    // What the lobby chose for one race, handed to the race scene (RaceLaunch) and assembled there (RaceAssembler):
    // the track's simulation (its definition), the scene loaded additively around it for looks, and the conditions.
    // The environment scene is decoration only and holds no collider, so the race scene alone still decides the run
    // (DECISIONS 2026-10-19 "M2 씬 구성").
    public readonly struct RaceSetup
    {
        public TrackDefinition Track { get; }
        // Scene name, loaded additively; empty for none.
        public string EnvironmentScene { get; }
        public RaceMode Mode { get; }
        public SpeedClass Speed { get; }
        public RunnerProfile Runner { get; }

        public RaceSetup(TrackDefinition track, string environmentScene, RaceMode mode, SpeedClass speed, RunnerProfile runner)
        {
            if (track == null) throw new ArgumentNullException(nameof(track));
            if (environmentScene == null) throw new ArgumentNullException(nameof(environmentScene), "Pass an empty string for no environment scene.");
            if (!Enum.IsDefined(typeof(RaceMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown race mode.");
            if (!Enum.IsDefined(typeof(SpeedClass), speed)) throw new ArgumentOutOfRangeException(nameof(speed), speed, "Unknown speed class.");
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            Track = track;
            EnvironmentScene = environmentScene;
            Mode = mode;
            Speed = speed;
            Runner = runner;
        }
    }
}
