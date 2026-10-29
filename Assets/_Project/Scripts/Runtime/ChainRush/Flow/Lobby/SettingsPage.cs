using ProtoHarness.ChainRush.Audio;
using ProtoHarness.ChainRush.Visuals;
using UnityEngine;

namespace ProtoHarness.ChainRush.Flow.Lobby
{
    // The settings (M2-c): three volumes and the speed field of view, changed with ←/→, the row that opens the keys
    // page, then the screen (2026-10-26): window mode, resolution and VSync, below the keys row so its place stays put.
    // A volume and the resolution stop at their ends (no wrapping); a switch flips whichever way, or on Enter. A screen
    // change takes effect at once (ScreenOptions.Apply). Reads and writes Context.Settings when asked, so a test may swap
    // the store after the lobby wakes.
    public sealed class SettingsPage : LobbyPage
    {
        public const int MasterRow = 0, MusicRow = 1, EffectsRow = 2, SpeedViewRow = 3, KeysRow = 4;
        public const int WindowRow = 5, ResolutionRow = 6, VSyncRow = 7;
        private static readonly string[] Labels = { "전체 볼륨", "음악", "효과음", "속도 시야 효과", "조작 키", "화면 모드", "해상도", "수직 동기화" };
        // A volume row's value, by level ("0" .. "10"), and the resolution row's, by index, made once.
        private static readonly string[] LevelTexts = MakeLevelTexts();
        private static readonly string[] ResolutionTexts = MakeResolutionTexts();

        public SettingsPage(LobbyContext context)
            : base(context, LobbyMenu.Page.Settings, "SETTINGS", "↑ / ↓  선택      ← / →  조절      ENTER  결정      ESC  뒤로") { }

        public override int Count => Labels.Length;
        public override string Label(int row) => Labels[row];
        public override bool Adjusts => true;

        // A volume or the resolution does nothing (←/→ change them), a switch flips, the keys row opens the keys page.
        public override Step Confirm(int row)
        {
            GameSettings settings = Context.Settings;
            switch (row)
            {
                case SpeedViewRow: settings.SetSpeedFieldOfView(!settings.SpeedFieldOfView); break;
                case KeysRow: return Step.Open(LobbyMenu.Page.Keys, 0);
                case WindowRow: settings.SetWindowed(!settings.Windowed); ScreenOptions.Apply(settings); break;
                case VSyncRow: settings.SetVSync(!settings.VSync); ScreenOptions.Apply(settings); break;
            }
            return Step.Stay;
        }

        public override Step Back() => Step.Open(LobbyMenu.Page.Main, MainPage.SettingsRow);

        // A volume or the resolution by delta steps, or a switch flipped; nothing on the keys row.
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
                case WindowRow: settings.SetWindowed(!settings.Windowed); ScreenOptions.Apply(settings); break;
                case ResolutionRow:
                    // At an end the screen is not set again to the size it already has.
                    int resolution = Mathf.Clamp(settings.ResolutionIndex + delta, 0, ScreenOptions.ResolutionCount - 1);
                    if (resolution == settings.ResolutionIndex) break;
                    settings.SetResolution(resolution);
                    ScreenOptions.Apply(settings);
                    break;
                case VSyncRow: settings.SetVSync(!settings.VSync); ScreenOptions.Apply(settings); break;
            }
        }

        // Eight rows are closer together than the default column, so the last ends above the notice line (y 760).
        public override Rect RowRect(int row, int count, float width, float height) => new Rect(64, 238 + row * 64, 600, 54);

        // The name, then for a volume its bar, and the value: the level, the setting's word, or the resolution. The keys
        // row has neither.
        public override void DrawRow(LobbyView view, Rect face, int row)
        {
            base.DrawRow(view, face, row);
            if (row == KeysRow) return;
            if (row < SpeedViewRow)
            {
                var bar = new Rect(face.x + 283, face.y + face.height * .5f - 2, 180, 5);
                NightUi.Block(bar, LobbyView.Slate);
                bar.width *= (float)Level(row) / GameSettings.MaxLevel;
                NightUi.Block(bar, LobbyView.Mint);
            }
            GUI.Label(new Rect(face.x + 383, face.y, 190, face.height), Value(row), view.SettingValue);
        }

        private int Level(int row) =>
            row == MasterRow ? Context.Settings.MasterLevel : row == MusicRow ? Context.Settings.MusicLevel : Context.Settings.EffectsLevel;

        private string Value(int row)
        {
            GameSettings settings = Context.Settings;
            switch (row)
            {
                case SpeedViewRow: return settings.SpeedFieldOfView ? "켬" : "끔";
                case WindowRow: return settings.Windowed ? "창" : "전체 화면";
                case ResolutionRow: return ResolutionTexts[settings.ResolutionIndex];
                case VSyncRow: return settings.VSync ? "켬" : "끔";
                default: return LevelTexts[Level(row)];
            }
        }

        private static int NextLevel(int level, int delta) => Mathf.Clamp(level + delta, 0, GameSettings.MaxLevel);

        private static string[] MakeLevelTexts()
        {
            var texts = new string[GameSettings.MaxLevel + 1];
            for (int level = 0; level < texts.Length; level++) texts[level] = level.ToString();
            return texts;
        }

        private static string[] MakeResolutionTexts()
        {
            var texts = new string[ScreenOptions.ResolutionCount];
            for (int i = 0; i < texts.Length; i++)
            {
                Vector2Int size = ScreenOptions.Size(i);
                texts[i] = i == ScreenOptions.AutoResolution ? "자동" : size.x + " × " + size.y;
            }
            return texts;
        }
    }
}
