using System;
using ProtoHarness.ChainRush.Race;
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
        // The track's name as the lobby lists it, shown on the race's HUD.
        public string Title { get; }
        // Scene name, loaded additively; empty for none.
        public string EnvironmentScene { get; }
        public RaceMode Mode { get; }
        public int Seed { get; }
        public SpeedClass Speed { get; }
        public RunnerProfile Runner { get; }
        // The track's own road and wall materials; null keeps the race scene's.
        public TrackLook Look { get; }
        // Where a tutorial starts (a row of TutorialCourse.Stages); 0 for a race.
        public int FirstStage { get; }
        // The most runners that race alongside the player's: eight racers in all (DESIGN.md §6).
        public const int MaxOpponents = 7;
        // The runners racing alongside the player's, each in a world of its own (Race/RaceField); none for a race alone.
        private readonly RunnerProfile[] opponents;
        public int OpponentCount => opponents == null ? 0 : opponents.Length;
        // Whether this is the setup of one of those worlds (ForOpponent): the race scene then assembles only the
        // simulation and the runner's model, with no view, sound, record or ghost of its own (RaceAssembler).
        public bool IsOpponentWorld { get; }

        public RunnerProfile Opponent(int index)
        {
            if (index < 0 || index >= OpponentCount)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"This race has {OpponentCount} opponents.");
            return opponents[index];
        }

        // The same bot race with these runners alongside. An empty lineup cannot race.
        public RaceSetup WithOpponents(params RunnerProfile[] runners)
        {
            if (runners == null) throw new ArgumentNullException(nameof(runners));
            if (Track == null) throw new InvalidOperationException("The setup is empty; build it with its constructor.");
            if (Mode != RaceMode.Bots) throw new ArgumentException($"Only a bot race takes opponents, not {Mode}.");
            if (IsOpponentWorld) throw new InvalidOperationException("An opponent's world has no opponents of its own.");
            if (runners.Length == 0) throw new ArgumentException("A bot race needs opponents.", nameof(runners));
            if (runners.Length > MaxOpponents)
                throw new ArgumentOutOfRangeException(nameof(runners), runners.Length, $"A race takes at most {MaxOpponents} opponents.");
            for (int i = 0; i < runners.Length; i++)
                if (runners[i] == null) throw new ArgumentException($"Opponent {i} is missing.", nameof(runners));
            return new RaceSetup(this, Runner, EnvironmentScene, Look, (RunnerProfile[])runners.Clone(), false);
        }

        // The setup of one opponent's world: the same track and speed with that opponent as its runner, and nothing
        // that is only for looking at (no environment scene, no track look).
        public RaceSetup ForOpponent(int index) => new RaceSetup(this, Opponent(index), "", null, null, true);

        private RaceSetup(in RaceSetup from, RunnerProfile runner, string environmentScene, TrackLook look, RunnerProfile[] opponents, bool opponentWorld)
        {
            Track = from.Track;
            Title = from.Title;
            EnvironmentScene = environmentScene;
            Mode = from.Mode;
            Seed = from.Seed;
            Speed = from.Speed;
            Runner = runner;
            Look = look;
            FirstStage = from.FirstStage;
            this.opponents = opponents;
            IsOpponentWorld = opponentWorld;
        }

        public RaceSetup(TrackDefinition track, string title, string environmentScene, RaceMode mode, SpeedClass speed, RunnerProfile runner,
            TrackLook look = null, int firstStage = 0, int seed = 0)
        {
            if (track == null) throw new ArgumentNullException(nameof(track));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A race needs the track's title for the HUD.", nameof(title));
            if (environmentScene == null) throw new ArgumentNullException(nameof(environmentScene), "Pass an empty string for no environment scene.");
            if (!Enum.IsDefined(typeof(RaceMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown race mode.");
            if (!Enum.IsDefined(typeof(SpeedClass), speed)) throw new ArgumentOutOfRangeException(nameof(speed), speed, "Unknown speed class.");
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (firstStage < 0) throw new ArgumentOutOfRangeException(nameof(firstStage), firstStage, "A first stage cannot be negative.");
            Track = track;
            Title = title;
            EnvironmentScene = environmentScene;
            Mode = mode;
            Seed = seed;
            Speed = speed;
            Runner = runner;
            Look = look;
            FirstStage = firstStage;
            opponents = null;
            IsOpponentWorld = false;
        }
    }
}
