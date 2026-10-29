using System;
using System.IO;
using UnityEngine;

namespace ProtoHarness.ChainRush.Flow
{
    // The ghost of each runner's best finish per track and speed class (M4), one GhostTrace file each, kept beside the
    // runner's record in BestRecords. Files, not PlayerPrefs: a three-lap trace is tens of kilobytes. The game uses Player
    // (under Application.persistentDataPath), tests their own folder so they never touch the player's ghosts. A missing
    // file is no ghost; a file that does not decode throws (GhostTrace.Decode), it is not passed off as none.
    public sealed class GhostFiles
    {
        private static GhostFiles player;

        // Made on first use: Application.persistentDataPath cannot be read from a MonoBehaviour's field initializer.
        public static GhostFiles Player => player ??= new GhostFiles(Path.Combine(Application.persistentDataPath, "Ghosts"));

        private readonly string folder;

        public GhostFiles(string folder)
        {
            if (string.IsNullOrEmpty(folder)) throw new ArgumentException("A folder is required.", nameof(folder));
            this.folder = folder;
        }

        public string Folder => folder;

        public bool TryLoad(string track, SpeedClass speed, string runner, out GhostTrace trace)
        {
            string path = PathOf(track, speed, runner);
            trace = null;
            if (!File.Exists(path)) return false;
            trace = GhostTrace.Decode(File.ReadAllBytes(path));
            return true;
        }

        // Replaces the ghost kept for this track, speed class and runner.
        public void Save(string track, SpeedClass speed, string runner, GhostTrace trace)
        {
            if (trace == null) throw new ArgumentNullException(nameof(trace));
            if (trace.Count == 0) throw new ArgumentException("A ghost needs at least one tick.", nameof(trace));
            string path = PathOf(track, speed, runner);
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(path, GhostTrace.Encode(trace));
        }

        public void Delete(string track, SpeedClass speed, string runner)
        {
            string path = PathOf(track, speed, runner);
            if (File.Exists(path)) File.Delete(path);
        }

        // Named like the record keys: the track's definition asset and the runner's profile asset (Circuit_Stadium.Fast.RunnerProfile_Alpha.ghost).
        private string PathOf(string track, SpeedClass speed, string runner)
        {
            RequireName(track, nameof(track));
            RequireName(runner, nameof(runner));
            if (!Enum.IsDefined(typeof(SpeedClass), speed)) throw new ArgumentOutOfRangeException(nameof(speed), speed, "Unknown speed class.");
            return Path.Combine(folder, track + "." + speed + "." + runner + ".ghost");
        }

        private static void RequireName(string name, string parameter)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A name is required.", parameter);
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException($"'{name}' cannot be part of a file name.", parameter);
        }
    }
}
