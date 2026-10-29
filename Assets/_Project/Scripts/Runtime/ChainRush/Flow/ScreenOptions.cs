using System;
using UnityEngine;

namespace ProtoHarness.ChainRush.Flow
{
    // Puts the stored screen settings (GameSettings: window mode, resolution, VSync) into effect, at the title and when
    // the settings page changes one (M2-c, DECISIONS 2026-10-26 "화면 설정"). The player only: in the editor the game
    // view keeps its own size, and whether a runtime QualitySettings change there is written back to
    // ProjectSettings/QualitySettings.asset (off limits, CLAUDE.md §0) is unconfirmed, so it says so once and changes nothing.
    public static class ScreenOptions
    {
        // Auto is the monitor's own resolution; the others are 16:9 like the 1600 x 900 canvas the menus are drawn on.
        public const int AutoResolution = 0;
        private static readonly Vector2Int[] Sizes =
        {
            Vector2Int.zero, new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440),
        };
        private static bool editorNoticeShown;

        public static int ResolutionCount => Sizes.Length;

        // A fixed resolution's width and height; AutoResolution has none (zero).
        public static Vector2Int Size(int resolution)
        {
            if (resolution < 0 || resolution >= Sizes.Length)
                throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "A resolution is within 0.." + (Sizes.Length - 1) + ".");
            return Sizes[resolution];
        }

        // Auto in a window: the largest fixed size smaller than the monitor both ways, so the title bar stays on screen
        // (a window the monitor's size overflows it, user 2026-10-26). A monitor no fixed size fits (1280 x 720 or less)
        // gets a 16:9 window a tenth smaller than it both ways.
        public static Vector2Int WindowedAutoSize(int monitorWidth, int monitorHeight)
        {
            if (monitorWidth <= 0 || monitorHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(monitorWidth), $"{monitorWidth} x {monitorHeight}", "A monitor has a size.");
            for (int i = Sizes.Length - 1; i > AutoResolution; i--)
                if (Sizes[i].x < monitorWidth && Sizes[i].y < monitorHeight) return Sizes[i];
            int width = Mathf.Min(monitorWidth * 9 / 10, monitorHeight * 9 / 10 * 16 / 9);
            return new Vector2Int(width, width * 9 / 16);
        }

        public static void Apply(GameSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (Application.isEditor)
            {
                if (!editorNoticeShown)
                    Debug.Log("ScreenOptions: the screen settings take effect in a player build only; the editor keeps its own.");
                editorNoticeShown = true;
                return;
            }
            Vector2Int size = Size(settings.ResolutionIndex);
            if (settings.ResolutionIndex == AutoResolution)
            {
                size = new Vector2Int(Display.main.systemWidth, Display.main.systemHeight);
                if (settings.Windowed) size = WindowedAutoSize(size.x, size.y);
            }
            Screen.SetResolution(size.x, size.y, settings.Windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);
            QualitySettings.vSyncCount = settings.VSync ? 1 : 0;
        }
    }
}
