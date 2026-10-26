using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ProtoHarness.ChainRush.Flow;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProtoHarness.Tests.EditMode
{
    // The player's settings (M2-c, user 2026-10-20). A test prefix keeps the player's settings untouched.
    public sealed class GameSettingsTests
    {
        private const string Prefix = "ChainRush.Test.GameSettingsTests.";
        private const string OtherPrefix = "ChainRush.Test.GameSettingsTests.Other.";
        private GameSettings settings;
        private GameSettings other;

        [SetUp]
        public void SetUp()
        {
            settings = new GameSettings(Prefix);
            other = new GameSettings(OtherPrefix);
            settings.Clear();
            other.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            settings.Clear();
            other.Clear();
        }

        [Test]
        public void Defaults_NothingStored_FullVolumeAndSpeedViewOn()
        {
            Assert.That(settings.MasterLevel, Is.EqualTo(GameSettings.MaxLevel));
            Assert.That(settings.MusicLevel, Is.EqualTo(GameSettings.MaxLevel));
            Assert.That(settings.EffectsLevel, Is.EqualTo(GameSettings.MaxLevel));
            Assert.That(settings.SpeedFieldOfView, Is.True);
            Assert.That(new GameSettings(Prefix).MasterLevel, Is.EqualTo(10), "A fresh instance reads the defaults too.");
        }

        [Test]
        public void Set_ThenNewInstance_ReadsTheSameValues()
        {
            settings.SetMasterLevel(7);
            settings.SetMusicLevel(3);
            settings.SetEffectsLevel(5);
            settings.SetSpeedFieldOfView(false);
            var loaded = new GameSettings(Prefix);
            Assert.That(loaded.MasterLevel, Is.EqualTo(7));
            Assert.That(loaded.MusicLevel, Is.EqualTo(3));
            Assert.That(loaded.EffectsLevel, Is.EqualTo(5));
            Assert.That(loaded.SpeedFieldOfView, Is.False);
        }

        [Test]
        public void SetLevel_ZeroAndMax_AreAllowed()
        {
            settings.SetMasterLevel(0);
            settings.SetMusicLevel(GameSettings.MaxLevel);
            settings.SetEffectsLevel(0);
            Assert.That(settings.MasterLevel, Is.EqualTo(0));
            Assert.That(settings.MusicLevel, Is.EqualTo(GameSettings.MaxLevel));
            Assert.That(settings.EffectsLevel, Is.EqualTo(0));
        }

        [Test]
        public void SetLevel_OutOfRange_ThrowsAndKeepsTheValue()
        {
            settings.SetMusicLevel(4);
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetMasterLevel(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetMasterLevel(11));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetMusicLevel(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetMusicLevel(11));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetEffectsLevel(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetEffectsLevel(11));
            Assert.That(settings.MasterLevel, Is.EqualTo(GameSettings.MaxLevel));
            Assert.That(settings.MusicLevel, Is.EqualTo(4));
            Assert.That(new GameSettings(Prefix).MusicLevel, Is.EqualTo(4), "A refused level is not stored.");
        }

        [Test]
        public void Factors_AreTheLevelOverMax()
        {
            settings.SetMasterLevel(5);
            settings.SetMusicLevel(0);
            settings.SetEffectsLevel(GameSettings.MaxLevel);
            Assert.That(settings.Master, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(settings.Music, Is.EqualTo(0f));
            Assert.That(settings.Effects, Is.EqualTo(1f));
        }

        [Test]
        public void Prefixes_AreKeptApart()
        {
            var player = GameSettings.Player;
            int playerMaster = player.MasterLevel, playerMusic = player.MusicLevel, playerEffects = player.EffectsLevel;
            bool playerSpeedView = player.SpeedFieldOfView;
            settings.SetMasterLevel(1);
            settings.SetMusicLevel(2);
            settings.SetEffectsLevel(3);
            settings.SetSpeedFieldOfView(!playerSpeedView);
            Assert.That(new GameSettings(OtherPrefix).MasterLevel, Is.EqualTo(GameSettings.MaxLevel), "Another test prefix keeps its defaults.");
            // Read-only for the player's settings: a fresh instance on Player's prefix still reads what Player holds.
            var stored = new GameSettings("ChainRush.Settings.");
            Assert.That(stored.MasterLevel, Is.EqualTo(playerMaster));
            Assert.That(stored.MusicLevel, Is.EqualTo(playerMusic));
            Assert.That(stored.EffectsLevel, Is.EqualTo(playerEffects));
            Assert.That(stored.SpeedFieldOfView, Is.EqualTo(playerSpeedView));
        }

        [Test]
        public void Constructor_StoredValueOutOfRange_LogsAndUsesTheDefaultForThatSettingOnly()
        {
            settings.SetMusicLevel(4);
            settings.SetEffectsLevel(6);
            PlayerPrefs.SetInt(Prefix + "MasterLevel", 99);
            PlayerPrefs.SetInt(Prefix + "SpeedFieldOfView", 7);
            LogAssert.Expect(LogType.Error, new Regex(@"^GameSettings: " + Regex.Escape(Prefix + "MasterLevel") + @" holds 99, outside 0\.\.10"));
            LogAssert.Expect(LogType.Error, new Regex(@"^GameSettings: " + Regex.Escape(Prefix + "SpeedFieldOfView") + @" holds 7, neither 0 nor 1"));
            var loaded = new GameSettings(Prefix);
            Assert.That(loaded.MasterLevel, Is.EqualTo(GameSettings.MaxLevel));
            Assert.That(loaded.SpeedFieldOfView, Is.True);
            Assert.That(loaded.MusicLevel, Is.EqualTo(4), "The other settings read as stored.");
            Assert.That(loaded.EffectsLevel, Is.EqualTo(6));
            Assert.That(PlayerPrefs.GetInt(Prefix + "MasterLevel", -1), Is.EqualTo(99), "The damaged value is not overwritten.");
            Assert.That(PlayerPrefs.GetInt(Prefix + "SpeedFieldOfView", -1), Is.EqualTo(7));
        }

        // ---- screen (2026-10-26) ------------------------------------------------------------------------------

        [Test]
        public void Screen_NothingStored_FullScreenAutoResolutionVSyncOff()
        {
            Assert.That(settings.Windowed, Is.False);
            Assert.That(settings.ResolutionIndex, Is.EqualTo(ScreenOptions.AutoResolution));
            Assert.That(settings.VSync, Is.False);
        }

        [Test]
        public void Screen_SetThenNewInstance_ReadsTheSameValues()
        {
            settings.SetWindowed(true);
            settings.SetResolution(ScreenOptions.ResolutionCount - 1);
            settings.SetVSync(true);
            var loaded = new GameSettings(Prefix);
            Assert.That(loaded.Windowed, Is.True);
            Assert.That(loaded.ResolutionIndex, Is.EqualTo(ScreenOptions.ResolutionCount - 1));
            Assert.That(loaded.VSync, Is.True);
            settings.Clear();
            Assert.That(settings.Windowed, Is.False);
            Assert.That(settings.ResolutionIndex, Is.EqualTo(ScreenOptions.AutoResolution));
            Assert.That(settings.VSync, Is.False);
            Assert.That(new GameSettings(Prefix).ResolutionIndex, Is.EqualTo(ScreenOptions.AutoResolution), "Clear removes the stored value.");
        }

        [Test]
        public void SetResolution_OutOfRange_ThrowsAndKeepsTheValue()
        {
            settings.SetResolution(2);
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetResolution(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetResolution(ScreenOptions.ResolutionCount));
            Assert.That(settings.ResolutionIndex, Is.EqualTo(2));
            Assert.That(new GameSettings(Prefix).ResolutionIndex, Is.EqualTo(2), "A refused resolution is not stored.");
        }

        [Test]
        public void Screen_StoredValueOutOfRange_LogsAndUsesTheDefaultForThatSettingOnly()
        {
            settings.SetVSync(true);
            PlayerPrefs.SetInt(Prefix + "Resolution", 9);
            PlayerPrefs.SetInt(Prefix + "Windowed", 2);
            int last = ScreenOptions.ResolutionCount - 1;
            LogAssert.Expect(LogType.Error, new Regex(@"^GameSettings: " + Regex.Escape(Prefix + "Windowed") + @" holds 2, neither 0 nor 1"));
            LogAssert.Expect(LogType.Error, new Regex(@"^GameSettings: " + Regex.Escape(Prefix + "Resolution") + @" holds 9, outside 0\.\." + last));
            var loaded = new GameSettings(Prefix);
            Assert.That(loaded.Windowed, Is.False);
            Assert.That(loaded.ResolutionIndex, Is.EqualTo(ScreenOptions.AutoResolution));
            Assert.That(loaded.VSync, Is.True, "The other settings read as stored.");
            Assert.That(PlayerPrefs.GetInt(Prefix + "Resolution", -1), Is.EqualTo(9), "The damaged value is not overwritten.");
        }

        [Test]
        public void ScreenOptions_Sizes_AutoFirstThenSixteenByNine()
        {
            Assert.That(ScreenOptions.Size(ScreenOptions.AutoResolution), Is.EqualTo(Vector2Int.zero));
            for (int i = 1; i < ScreenOptions.ResolutionCount; i++)
            {
                Vector2Int size = ScreenOptions.Size(i);
                Assert.That(size.x * 9, Is.EqualTo(size.y * 16), $"Resolution {i} ({size}) is 16:9.");
                Assert.That(size.x, Is.GreaterThan(ScreenOptions.Size(i - 1).x), "Smallest first.");
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => ScreenOptions.Size(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ScreenOptions.Size(ScreenOptions.ResolutionCount));
        }

        [Test]
        public void ScreenOptions_WindowedAuto_LargestSizeSmallerThanTheMonitor()
        {
            Assert.That(ScreenOptions.WindowedAutoSize(1920, 1080), Is.EqualTo(new Vector2Int(1600, 900)));
            Assert.That(ScreenOptions.WindowedAutoSize(2560, 1440), Is.EqualTo(new Vector2Int(1920, 1080)));
            Assert.That(ScreenOptions.WindowedAutoSize(3840, 2160), Is.EqualTo(new Vector2Int(2560, 1440)));
            Assert.That(ScreenOptions.WindowedAutoSize(1920, 1200), Is.EqualTo(new Vector2Int(1600, 900)), "Smaller both ways, not just in width.");
            Assert.That(ScreenOptions.WindowedAutoSize(1366, 768), Is.EqualTo(new Vector2Int(1280, 720)));
            Assert.That(ScreenOptions.WindowedAutoSize(1280, 720), Is.EqualTo(new Vector2Int(1152, 648)), "None fits: a tenth smaller.");
            Assert.That(ScreenOptions.WindowedAutoSize(1024, 768), Is.EqualTo(new Vector2Int(921, 518)), "None fits: the width bounds it.");
            Assert.Throws<ArgumentOutOfRangeException>(() => ScreenOptions.WindowedAutoSize(0, 720));
        }

        [Test]
        public void ScreenOptions_InTheEditor_LeavesVSyncAlone()
        {
            int vSync = QualitySettings.vSyncCount;
            settings.SetVSync(vSync == 0);
            ScreenOptions.Apply(settings);
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(vSync), "The editor's quality settings are not changed.");
            Assert.Throws<ArgumentNullException>(() => ScreenOptions.Apply(null));
        }

        [Test]
        public void Constructor_NoPrefix_Throws()
        {
            Assert.Throws<ArgumentException>(() => new GameSettings(""));
            Assert.Throws<ArgumentException>(() => new GameSettings(null));
        }
    }
}
