using System;
using UnityEngine;

namespace ProtoHarness.ChainRush.Flow
{
    // The player's settings (M2-c, user 2026-10-20): three volume levels and the speed field of view, changed on the
    // lobby's settings page and read by the race's audio and camera, and the screen's window mode, resolution and VSync
    // (2026-10-26), put into effect by ScreenOptions. Presentation only; the simulation never reads them.
    // Kept in PlayerPrefs under a key prefix and read once on construction, so the race reads memory, not PlayerPrefs,
    // every frame. The game uses Player, tests their own prefix so they never touch the player's settings.
    public sealed class GameSettings
    {
        public const int MaxLevel = 10;

        // The defaults sound and look as the race did before there were settings.
        private const int DefaultLevel = MaxLevel;
        private const bool DefaultSpeedFieldOfView = true;
        // The screen as the build starts without settings: full screen, the monitor's resolution, VSync off.
        private const bool DefaultWindowed = false;
        private const int DefaultResolution = ScreenOptions.AutoResolution;
        private const bool DefaultVSync = false;

        public static GameSettings Player { get; } = new GameSettings("ChainRush.Settings.");

        // The PlayerPrefs keys, the prefix and the setting's name, made once.
        private readonly string masterKey;
        private readonly string musicKey;
        private readonly string effectsKey;
        private readonly string speedFieldOfViewKey;
        private readonly string windowedKey;
        private readonly string resolutionKey;
        private readonly string vSyncKey;

        public GameSettings(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) throw new ArgumentException("A key prefix is required.", nameof(prefix));
            masterKey = prefix + "MasterLevel";
            musicKey = prefix + "MusicLevel";
            effectsKey = prefix + "EffectsLevel";
            speedFieldOfViewKey = prefix + "SpeedFieldOfView";
            windowedKey = prefix + "Windowed";
            resolutionKey = prefix + "Resolution";
            vSyncKey = prefix + "VSync";
            MasterLevel = ReadLevel(masterKey);
            MusicLevel = ReadLevel(musicKey);
            EffectsLevel = ReadLevel(effectsKey);
            SpeedFieldOfView = ReadSwitch(speedFieldOfViewKey, DefaultSpeedFieldOfView);
            Windowed = ReadSwitch(windowedKey, DefaultWindowed);
            ResolutionIndex = ReadIndex(resolutionKey, ScreenOptions.ResolutionCount - 1, DefaultResolution);
            VSync = ReadSwitch(vSyncKey, DefaultVSync);
        }

        public int MasterLevel { get; private set; }
        public int MusicLevel { get; private set; }
        public int EffectsLevel { get; private set; }
        // Whether the camera widens with speed (FollowCamera); the corner swing still widens it either way.
        public bool SpeedFieldOfView { get; private set; }
        // A window instead of the full screen.
        public bool Windowed { get; private set; }
        // Which of ScreenOptions' resolutions, 0 (ScreenOptions.AutoResolution) .. ScreenOptions.ResolutionCount - 1.
        public int ResolutionIndex { get; private set; }
        public bool VSync { get; private set; }

        // The levels as volume factors, 0..1.
        public float Master => MasterLevel / (float)MaxLevel;
        public float Music => MusicLevel / (float)MaxLevel;
        public float Effects => EffectsLevel / (float)MaxLevel;

        public void SetMasterLevel(int level) => MasterLevel = WriteIndex(masterKey, level, MaxLevel, nameof(level));
        public void SetMusicLevel(int level) => MusicLevel = WriteIndex(musicKey, level, MaxLevel, nameof(level));
        public void SetEffectsLevel(int level) => EffectsLevel = WriteIndex(effectsKey, level, MaxLevel, nameof(level));

        public void SetSpeedFieldOfView(bool on) => SpeedFieldOfView = WriteSwitch(speedFieldOfViewKey, on);
        public void SetWindowed(bool on) => Windowed = WriteSwitch(windowedKey, on);
        public void SetVSync(bool on) => VSync = WriteSwitch(vSyncKey, on);

        public void SetResolution(int resolution) =>
            ResolutionIndex = WriteIndex(resolutionKey, resolution, ScreenOptions.ResolutionCount - 1, nameof(resolution));

        public void Clear()
        {
            PlayerPrefs.DeleteKey(masterKey);
            PlayerPrefs.DeleteKey(musicKey);
            PlayerPrefs.DeleteKey(effectsKey);
            PlayerPrefs.DeleteKey(speedFieldOfViewKey);
            PlayerPrefs.DeleteKey(windowedKey);
            PlayerPrefs.DeleteKey(resolutionKey);
            PlayerPrefs.DeleteKey(vSyncKey);
            MasterLevel = MusicLevel = EffectsLevel = DefaultLevel;
            SpeedFieldOfView = DefaultSpeedFieldOfView;
            Windowed = DefaultWindowed;
            ResolutionIndex = DefaultResolution;
            VSync = DefaultVSync;
        }

        private static int WriteIndex(string key, int value, int max, string name)
        {
            if (value < 0 || value > max)
                throw new ArgumentOutOfRangeException(name, value, "A " + name + " is within 0.." + max + ".");
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
            return value;
        }

        // A stored value out of range (a damaged or hand-edited file) is reported with its key and value, and only that
        // setting starts from its default; the stored value is left as it is until the player changes the setting.
        private static int ReadLevel(string key) => ReadIndex(key, MaxLevel, DefaultLevel);

        private static int ReadIndex(string key, int max, int byDefault)
        {
            int value = PlayerPrefs.GetInt(key, byDefault);
            if (value >= 0 && value <= max) return value;
            Debug.LogError($"GameSettings: {key} holds {value}, outside 0..{max}; using {byDefault} until it is set again.");
            return byDefault;
        }

        private static bool WriteSwitch(string key, bool on)
        {
            PlayerPrefs.SetInt(key, on ? 1 : 0);
            PlayerPrefs.Save();
            return on;
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
