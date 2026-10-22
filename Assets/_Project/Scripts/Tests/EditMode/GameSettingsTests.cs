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

        [Test]
        public void Constructor_NoPrefix_Throws()
        {
            Assert.Throws<ArgumentException>(() => new GameSettings(""));
            Assert.Throws<ArgumentException>(() => new GameSettings(null));
        }
    }
}
