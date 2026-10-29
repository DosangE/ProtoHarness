using System;
using System.IO;
using UnityEngine;

namespace ProtoHarness.ChainRush.Flow
{
    // The ghost of the best finish per track and speed class (M4; one per track and speed, whichever runner set it — user
    // 2026-10-29 "트랙별 최고기록만 남기고 싶다"), one GhostTrace file each, kept beside that best in BestRecords. Files,
    // not PlayerPrefs: a three-lap trace is tens of kilobytes. The game uses Player (under Application.persistentDataPath),
    // tests their own folder so they never touch the player's ghosts. A missing file is no ghost; a file that does not
    // decode throws (GhostTrace.Decode), it is not passed off as none.
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

        public bool TryLoad(string track, SpeedClass speed, out GhostTrace trace)
        {
            string path = PathOf(track, speed);
            trace = null;
            if (!File.Exists(path)) return false;
            trace = GhostTrace.Decode(File.ReadAllBytes(path));
            return true;
        }

        // Replaces the ghost kept for this track and speed class.
        public void Save(string track, SpeedClass speed, GhostTrace trace)
        {
            if (trace == null) throw new ArgumentNullException(nameof(trace));
            if (trace.Count == 0) throw new ArgumentException("A ghost needs at least one tick.", nameof(trace));
            string path = PathOf(track, speed);
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(path, GhostTrace.Encode(trace));
        }

        public void Delete(string track, SpeedClass speed)
        {
            string path = PathOf(track, speed);
            if (File.Exists(path)) File.Delete(path);
        }

        // Named like the record keys, by the track's definition asset (Circuit_Stadium.Fast.ghost).
        private string PathOf(string track, SpeedClass speed)
        {
            if (string.IsNullOrEmpty(track)) throw new ArgumentException("A track is required.", nameof(track));
            if (track.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException($"'{track}' cannot be part of a file name.", nameof(track));
            if (!Enum.IsDefined(typeof(SpeedClass), speed)) throw new ArgumentOutOfRangeException(nameof(speed), speed, "Unknown speed class.");
            return Path.Combine(folder, track + "." + speed + ".ghost");
        }
    }
}
