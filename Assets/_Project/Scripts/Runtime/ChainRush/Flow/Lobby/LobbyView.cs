using ProtoHarness.ChainRush.Visuals;
using UnityEngine;

namespace ProtoHarness.ChainRush.Flow.Lobby
{
    // The lobby's IMGUI look, in its units (1600 x 900, scaled like ChainRushHud): the fonts and styles, the frame every
    // page shares (backdrop, brand, heading, back button, notice, key help), the row list and the description panel.
    // Pages draw their own parts with its styles. Made inside OnGUI (styles need GUI.skin); Release frees the fonts.
    public sealed class LobbyView
    {
        public static readonly Color Ink = NightUi.Background;
        public static readonly Color Mint = NightUi.Accent;
        public static readonly Color Muted = NightUi.Muted;
        public static readonly Color Slate = NightUi.Border;

        private readonly Texture2D nightBackground;
        private readonly Font font;
        private readonly Font displayFont;
        private readonly GUIStyle brand;
        private readonly GUIStyle pageHeading;
        private readonly GUIStyle lockedItem;
        private readonly GUIStyle item;
        private readonly GUIStyle backButton;
        private readonly GUIStyle infoTitle;
        private readonly GUIStyle infoBody;
        private readonly GUIStyle lockTag;
        private readonly GUIStyle helpLine;
        private readonly GUIStyle footLine;

        public LobbyView(Texture2D nightBackground, Texture2D runnerArtwork)
        {
            this.nightBackground = nightBackground;
            RunnerArtwork = runnerArtwork;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 24);
            displayFont = NightUi.DisplayFont();
            if (!Ready) return;
            Small = Style(15, Muted);
            Body = Style(21, Color.white);
            Heading = NightUi.Style(displayFont, 44, Color.white, FontStyle.BoldAndItalic);
            brand = NightUi.Style(displayFont, 34, Color.white, FontStyle.BoldAndItalic);
            pageHeading = NightUi.Style(displayFont, 52, Color.white, FontStyle.BoldAndItalic);
            TileLabel = NightUi.Style(displayFont, 23, Color.white, FontStyle.Bold);
            TileLabel.alignment = TextAnchor.MiddleCenter;
            item = Style(26, Color.white, FontStyle.Bold);
            item.alignment = TextAnchor.MiddleLeft;
            lockedItem = Style(26, Muted);
            lockedItem.alignment = TextAnchor.MiddleLeft;
            // A row's value at its right end: a record in grey, a setting in mint; a record under a description, left.
            RowValue = Style(18, Muted);
            RowValue.alignment = TextAnchor.MiddleRight;
            SettingValue = Style(18, Mint);
            SettingValue.alignment = TextAnchor.MiddleRight;
            footLine = Style(18, Muted);
            footLine.alignment = TextAnchor.MiddleLeft;
            EnterButton = Style(28, Ink, FontStyle.Bold);
            EnterButton.alignment = TextAnchor.MiddleCenter;
            backButton = Style(22, Color.white, FontStyle.Bold);
            backButton.alignment = TextAnchor.MiddleCenter;
            infoTitle = Style(38, Color.white, FontStyle.Bold);
            infoBody = Style(21, Color.white);
            infoBody.wordWrap = true;
            lockTag = NightUi.Style(displayFont, 16, NightUi.Action, FontStyle.Bold);
            lockTag.alignment = TextAnchor.MiddleRight;
            helpLine = Style(15, Muted);
            helpLine.alignment = TextAnchor.MiddleRight;
        }

        // False when a font could not be made; the styles are then missing and nothing may be drawn.
        public bool Ready => font != null && displayFont != null;
        public Texture2D RunnerArtwork { get; }
        public GUIStyle Small { get; }
        public GUIStyle Body { get; }
        public GUIStyle Heading { get; }
        public GUIStyle TileLabel { get; }
        public GUIStyle RowValue { get; }
        public GUIStyle SettingValue { get; }
        public GUIStyle EnterButton { get; }

        // The lobby's units (1600 x 900) to screen pixels, as OnGUI scales them.
        public static float GuiScale() => Mathf.Min(Screen.width / 1600f, Screen.height / 900f);

        // The backdrop, the brand, the page's breadcrumb and heading, and the back button. Returns whether back was pressed.
        public bool DrawFrame(LobbyPage page, float width, float height)
        {
            NightUi.Backdrop(nightBackground, width, height);
            GUI.Label(new Rect(64, 34, 400, 54), "CHAIN RUSH", brand);
            GUI.Label(new Rect(64, 108, 900, 28), page.Breadcrumb, Small);
            GUI.Label(new Rect(61, 145, 1050, 68), page.Heading, pageHeading);
            // Back sits bottom left, the key help bottom right on its line (user 2026-10-21).
            var backRect = new Rect(64, height - 84, 186, 48);
            return NightUi.CardButton(backRect, "←  이전   ESC", backButton);
        }

        // The last refusal over the back button, and the page's key help.
        public void DrawFooter(string notice, string help, float width, float height)
        {
            GUI.Label(new Rect(64, height - 140, 1000, 28), notice, Body);
            GUI.Label(new Rect(width * .5f, height - 74, width * .5f - 64, 28), help, helpLine);
        }

        // The page's rows as pressable cards. Returns the row clicked, or -1: the click is acted on after drawing, since
        // choosing opens another page and the rest of the loop would read that page with this page's rows (a click on
        // 싱글 레이스 threw, 2026-10-20).
        public int DrawList(LobbyPage page, int cursor, float width, float height)
        {
            int clicked = -1;
            int count = page.Count;
            for (int i = 0; i < count; i++)
            {
                Rect row = page.RowRect(i, count, width, height);
                NightUi.CardLook look = page.IsLocked(i) ? NightUi.CardLook.Locked
                    : i == cursor ? NightUi.CardLook.Selected : NightUi.CardLook.Plain;
                // The row's content follows the card's face, which sinks while pressed.
                row = NightUi.PressableCard(row, look, out bool pressed);
                if (pressed) clicked = i;
                page.DrawRow(this, row, i);
            }
            return clicked;
        }

        // A row's name, greyed with (잠김) when locked.
        public void RowLabel(Rect face, string label, bool locked) =>
            GUI.Label(new Rect(face.x + 26, face.y, face.width - 26, face.height), locked ? label + "   (잠김)" : label, locked ? lockedItem : item);

        // The highlighted row: its page's tag, its name, LOCKED when it is, its description and its foot line.
        public void DrawDescription(LobbyPage page, int cursor, float width)
        {
            var panel = new Rect(width - 620, 238, 556, 330);
            NightUi.PanelCard(panel);
            float left = panel.x + 34, inner = panel.width - 68;
            GUI.Label(new Rect(left, panel.y + 26, inner, 28), page.DescriptionTag, Small);
            if (page.IsLocked(cursor)) GUI.Label(new Rect(left, panel.y + 26, inner, 28), "LOCKED  ·  준비 중", lockTag);
            GUI.Label(new Rect(left, panel.y + 62, inner, 52), page.Label(cursor), infoTitle);
            NightUi.Block(new Rect(left, panel.y + 126, 48, 3), NightUi.Action);
            GUI.Label(new Rect(left, panel.y + 148, inner, 120), page.Description(cursor), infoBody);
            string foot = page.DescriptionFoot(cursor);
            if (foot.Length > 0) GUI.Label(new Rect(left, panel.yMax - 54, inner, 28), foot, footLine);
        }

        public void Release()
        {
            if (font != null) Object.Destroy(font);
            if (displayFont != null) Object.Destroy(displayFont);
        }

        private GUIStyle Style(int size, Color color, FontStyle weight = FontStyle.Normal) => NightUi.Style(font, size, color, weight);
    }
}
