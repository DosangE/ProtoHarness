using ProtoHarness.ChainRush.Audio;
using ProtoHarness.ChainRush.Visuals;
using UnityEngine;

namespace ProtoHarness.ChainRush.Flow.Lobby
{
    // The settings (M2-c): three volumes and the speed field of view, changed with ←/→, and the row that opens the keys
    // page. A volume stops at 0 and MaxLevel (no wrapping); the speed field of view switches whichever way, or on Enter.
    // Reads and writes Context.Settings when asked, so a test may swap the store after the lobby wakes.
    public sealed class SettingsPage : LobbyPage
    {
        public const int MasterRow = 0, MusicRow = 1, EffectsRow = 2, SpeedViewRow = 3, KeysRow = 4;
        private static readonly string[] Labels = { "전체 볼륨", "음악", "효과음", "속도 시야 효과", "조작 키" };
        // A volume row's value, by level ("0" .. "10"), made once.
        private static readonly string[] LevelTexts = MakeLevelTexts();

        public SettingsPage(LobbyContext context)
            : base(context, LobbyMenu.Page.Settings, "SETTINGS", "↑ / ↓  선택      ← / →  조절      ENTER  결정      ESC  뒤로") { }

        public override int Count => Labels.Length;
        public override string Label(int row) => Labels[row];
        public override bool Adjusts => true;

        // A volume row does nothing (←/→ change it), the speed field of view switches, the keys row opens the keys page.
        public override Step Confirm(int row)
        {
            if (row == SpeedViewRow) Context.Settings.SetSpeedFieldOfView(!Context.Settings.SpeedFieldOfView);
            else if (row == KeysRow) return Step.Open(LobbyMenu.Page.Keys, 0);
            return Step.Stay;
        }

        public override Step Back() => Step.Open(LobbyMenu.Page.Main, MainPage.SettingsRow);

        // A volume by delta levels, or the speed field of view switched; nothing on the keys row.
        public override void Adjust(int row, int delta)
        {
            GameSettings settings = Context.Settings;
            if (row != KeysRow) UiSound.Play(UiSound.Cue.Move);
            switch (row)
            {
                case MasterRow: settings.SetMasterLevel(NextLevel(settings.MasterLevel, delta)); break;
                case MusicRow: settings.SetMusicLevel(NextLevel(settings.MusicLevel, delta)); break;
                case EffectsRow: settings.SetEffectsLevel(NextLevel(settings.EffectsLevel, delta)); break;
                case SpeedViewRow: settings.SetSpeedFieldOfView(!settings.SpeedFieldOfView); break;
            }
        }

        // The name, then for a volume its bar, and the value: the level, or 켬 / 끔. The keys row has neither.
        public override void DrawRow(LobbyView view, Rect face, int row)
        {
            base.DrawRow(view, face, row);
            if (row == KeysRow) return;
            if (row < SpeedViewRow)
            {
                var bar = new Rect(face.x + 283, face.y + 33, 180, 5);
                NightUi.Block(bar, LobbyView.Slate);
                bar.width *= (float)Level(row) / GameSettings.MaxLevel;
                NightUi.Block(bar, LobbyView.Mint);
            }
            GUI.Label(new Rect(face.x + 485, face.y, 88, face.height), Value(row), view.SettingValue);
        }

        private int Level(int row) =>
            row == MasterRow ? Context.Settings.MasterLevel : row == MusicRow ? Context.Settings.MusicLevel : Context.Settings.EffectsLevel;

        private string Value(int row) => row < SpeedViewRow ? LevelTexts[Level(row)] : Context.Settings.SpeedFieldOfView ? "켬" : "끔";

        private static int NextLevel(int level, int delta) => Mathf.Clamp(level + delta, 0, GameSettings.MaxLevel);

        private static string[] MakeLevelTexts()
        {
            var texts = new string[GameSettings.MaxLevel + 1];
            for (int level = 0; level < texts.Length; level++) texts[level] = level.ToString();
            return texts;
        }
    }
}
