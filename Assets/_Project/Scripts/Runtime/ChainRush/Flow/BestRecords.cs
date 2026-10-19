using System;
using UnityEngine;

namespace ProtoHarness.ChainRush.Flow
{
    // The best finish time per track and speed class, with the runner that set it (user 2026-10-19: shown on the lobby's
    // track page, "기록 없음" until there is one). Kept in PlayerPrefs under a key prefix; the game uses Player, tests
    // their own prefix so they never touch the player's records.
    public sealed class BestRecords
    {
        public readonly struct Record
        {
            public int Ticks { get; }
            public string Runner { get; }

            public Record(int ticks, string runner)
            {
                Ticks = ticks;
                Runner = runner;
            }
        }

        public static BestRecords Player { get; } = new BestRecords("ChainRush.Best.");

        private readonly string prefix;

        public BestRecords(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) throw new ArgumentException("A key prefix is required.", nameof(prefix));
            this.prefix = prefix;
        }

        public bool TryGet(string track, SpeedClass speed, out Record record)
        {
            string key = Key(track, speed);
            record = default;
            int ticks = PlayerPrefs.GetInt(key + ".ticks", 0);
            if (ticks <= 0) return false;
            record = new Record(ticks, PlayerPrefs.GetString(key + ".runner", ""));
            return true;
        }

        // Keeps the finish if it beats the stored one (or there is none) and says whether it did. A tie keeps the older.
        public bool Submit(string track, SpeedClass speed, int ticks, string runner)
        {
            if (ticks <= 0) throw new ArgumentOutOfRangeException(nameof(ticks), ticks, "A finish takes at least one tick.");
            if (string.IsNullOrEmpty(runner)) throw new ArgumentException("A runner is required.", nameof(runner));
            if (TryGet(track, speed, out Record best) && best.Ticks <= ticks) return false;
            string key = Key(track, speed);
            PlayerPrefs.SetInt(key + ".ticks", ticks);
            PlayerPrefs.SetString(key + ".runner", runner);
            PlayerPrefs.Save();
            return true;
        }

        public void Clear(string track, SpeedClass speed)
        {
            string key = Key(track, speed);
            PlayerPrefs.DeleteKey(key + ".ticks");
            PlayerPrefs.DeleteKey(key + ".runner");
        }

        // A track is named by its definition asset (Circuit_Stadium); renaming the asset starts its records over.
        private string Key(string track, SpeedClass speed)
        {
            if (string.IsNullOrEmpty(track)) throw new ArgumentException("A track is required.", nameof(track));
            if (!Enum.IsDefined(typeof(SpeedClass), speed)) throw new ArgumentOutOfRangeException(nameof(speed), speed, "Unknown speed class.");
            return prefix + track + "." + speed;
        }
    }
}
