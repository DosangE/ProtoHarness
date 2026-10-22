using System;
using UnityEngine;

namespace ProtoHarness.ChainRush.Flow
{
    // The player's settings (M2-c, user 2026-10-20): three volume levels and the speed field of view, changed on the
    // lobby's settings page and read by the race's audio and camera. Presentation only; the simulation never reads them.
    // Kept in PlayerPrefs under a key prefix and read once on construction, so the race reads memory, not PlayerPrefs,
    // every frame. The game uses Player, tests their own prefix so they never touch the player's settings.
    public sealed class GameSettings
    {
        public const int MaxLevel = 10;

        // The defaults sound and look as the race did before there were settings.
        private const int DefaultLevel = MaxLevel;
        private const bool DefaultSpeedFieldOfView = true;

        public static GameSettings Player { get; } = new GameSettings("ChainRush.Settings.");

        // The PlayerPrefs keys, the prefix and the setting's name, made once.
        private readonly string masterKey;
        private readonly string musicKey;
        private readonly string effectsKey;
        private readonly string speedFieldOfViewKey;

        public GameSettings(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) throw new ArgumentException("A key prefix is required.", nameof(prefix));
            masterKey = prefix + "MasterLevel";
            musicKey = prefix + "MusicLevel";
            effectsKey = prefix + "EffectsLevel";
            speedFieldOfViewKey = prefix + "SpeedFieldOfView";
            MasterLevel = ReadLevel(masterKey);
            MusicLevel = ReadLevel(musicKey);
            EffectsLevel = ReadLevel(effectsKey);
            SpeedFieldOfView = ReadSwitch(speedFieldOfViewKey, DefaultSpeedFieldOfView);
        }

        public int MasterLevel { get; private set; }
        public int MusicLevel { get; private set; }
        public int EffectsLevel { get; private set; }
        // Whether the camera widens with speed (FollowCamera); the corner swing still widens it either way.
        public bool SpeedFieldOfView { get; private set; }

        // The levels as volume factors, 0..1.
        public float Master => MasterLevel / (float)MaxLevel;
        public float Music => MusicLevel / (float)MaxLevel;
        public float Effects => EffectsLevel / (float)MaxLevel;

        public void SetMasterLevel(int level) => MasterLevel = WriteLevel(masterKey, level);
        public void SetMusicLevel(int level) => MusicLevel = WriteLevel(musicKey, level);
        public void SetEffectsLevel(int level) => EffectsLevel = WriteLevel(effectsKey, level);

        public void SetSpeedFieldOfView(bool on)
        {
            PlayerPrefs.SetInt(speedFieldOfViewKey, on ? 1 : 0);
            PlayerPrefs.Save();
            SpeedFieldOfView = on;
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(masterKey);
            PlayerPrefs.DeleteKey(musicKey);
            PlayerPrefs.DeleteKey(effectsKey);
            PlayerPrefs.DeleteKey(speedFieldOfViewKey);
            MasterLevel = MusicLevel = EffectsLevel = DefaultLevel;
            SpeedFieldOfView = DefaultSpeedFieldOfView;
        }

        private static int WriteLevel(string key, int level)
        {
            if (level < 0 || level > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level, "A level is within 0.." + MaxLevel + ".");
            PlayerPrefs.SetInt(key, level);
            PlayerPrefs.Save();
            return level;
        }

        // A stored value out of range (a damaged or hand-edited file) is reported with its key and value, and only that
        // setting starts from its default; the stored value is left as it is until the player changes the setting.
        private static int ReadLevel(string key)
        {
            int level = PlayerPrefs.GetInt(key, DefaultLevel);
            if (level >= 0 && level <= MaxLevel) return level;
            Debug.LogError($"GameSettings: {key} holds {level}, outside 0..{MaxLevel}; using {DefaultLevel} until it is set again.");
            return DefaultLevel;
        }

        private static bool ReadSwitch(string key, bool byDefault)
        {
            int value = PlayerPrefs.GetInt(key, byDefault ? 1 : 0);
            if (value == 0 || value == 1) return value == 1;
            Debug.LogError($"GameSettings: {key} holds {value}, neither 0 nor 1; using {(byDefault ? 1 : 0)} until it is set again.");
            return byDefault;
        }
    }
}
