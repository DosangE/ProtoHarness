using ProtoHarness.ChainRush.Audio;
using UnityEngine;

namespace ProtoHarness.ChainRush.Visuals
{
    // Shared IMGUI art for Night Drift. Input and race state stay with each screen.
    // Cards (user 2026-10-21): a block lifted off the backdrop by a shadow and outlined, lighter than Panel. A pressable
    // card greys a little under the mouse and sinks onto its shadow while held; the mouse coming onto one plays
    // UiSound.Cue.Move. What a click means (and its sound) is the screen's.
    public static class NightUi
    {
        public enum CardLook
        {
            // A panel, an entry or a secondary button.
            Plain,
            // The highlighted entry: brighter, with a mint line along the bottom.
            Selected,
            // A locked entry: dimmer, its outline the plain border.
            Locked,
            // The main button: yellow.
            Action,
        }

        private static readonly Color CardFill = new Color(24f / 255, 36f / 255, 54f / 255, .97f);
        private static readonly Color CardSelected = new Color(30f / 255, 62f / 255, 74f / 255, .97f);
        private static readonly Color CardLocked = new Color(18f / 255, 26f / 255, 38f / 255, .94f);
        private static readonly Color CardEdge = new Color(88f / 255, 229f / 255, 238f / 255, .35f);
        private static readonly Color CardShadow = new Color(0, 0, 0, .55f);
        private static readonly Color CardHover = new Color(.62f, .65f, .70f, .16f);
        private static readonly Color CardHeld = new Color(.30f, .32f, .36f, .22f);
        // The shadow's offset, and how far a held card sinks toward it.
        private static readonly Vector2 ShadowOffset = new Vector2(7, 9), PressSink = new Vector2(4, 5);
        // The card the mouse was last on, and the frame it was seen there (UiSound on coming onto another).
        private static Rect hoverRect;
        private static int hoverFrame = -2;
        public static readonly Color Background = new Color(8f / 255, 13f / 255, 23f / 255);
        public static readonly Color Panel = new Color(16f / 255, 24f / 255, 37f / 255);
        public static readonly Color Accent = new Color(88f / 255, 229f / 255, 238f / 255);
        public static readonly Color Action = new Color(243f / 255, 223f / 255, 83f / 255);
        public static readonly Color Muted = new Color(158f / 255, 172f / 255, 190f / 255);
        public static readonly Color Border = new Color(38f / 255, 52f / 255, 68f / 255);
        public static readonly Color Selected = new Color(0.10f, 0.19f, 0.23f);

        public static Font DisplayFont() => Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Arial" }, 32);

        public static GUIStyle Style(Font font, int size, Color color, FontStyle weight = FontStyle.Normal)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                font = font, fontSize = size, fontStyle = weight, padding = new RectOffset(0, 0, 0, 0)
            };
            style.normal.textColor = color;
            return style;
        }

        public static void Block(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        public static void Backdrop(Texture texture, float width, float height)
        {
            Block(new Rect(0, 0, width, height), Background);
            if (texture != null) GUI.DrawTexture(new Rect(0, 0, width, height), texture, ScaleMode.StretchToFill);
        }

        public static void Surface(Rect rect, bool selected = false)
        {
            Block(rect, selected ? Selected : Panel);
            if (selected) Block(new Rect(rect.x, rect.yMax - 4, rect.width, 4), Accent);
        }

        // Draws a card at rect and returns its face: rect itself, or moved by PressSink when pressed.
        public static Rect Card(Rect rect, CardLook look = CardLook.Plain, bool hovered = false, bool pressed = false)
        {
            Block(new Rect(rect.x + ShadowOffset.x, rect.y + ShadowOffset.y, rect.width, rect.height), CardShadow);
            Rect face = pressed ? new Rect(rect.position + PressSink, rect.size) : rect;
            Block(face, look == CardLook.Selected ? CardSelected : look == CardLook.Locked ? CardLocked
                : look == CardLook.Action ? Action : CardFill);
            if (look != CardLook.Action)
            {
                Color edge = look == CardLook.Locked ? Border : CardEdge;
                Block(new Rect(face.x, face.y, face.width, 1), edge);
                Block(new Rect(face.x, face.yMax - 1, face.width, 1), edge);
                Block(new Rect(face.x, face.y, 1, face.height), edge);
                Block(new Rect(face.xMax - 1, face.y, 1, face.height), edge);
            }
            if (look == CardLook.Selected) Block(new Rect(face.x, face.yMax - 4, face.width, 4), Accent);
            if (pressed) Block(face, CardHeld);
            else if (hovered) Block(face, CardHover);
            return face;
        }

        // A panel card with a mint line along its top.
        public static void PanelCard(Rect rect)
        {
            Card(rect);
            Block(new Rect(rect.x, rect.y, rect.width, 3), Accent);
        }

        // A card that takes clicks over all of rect. Returns the face to draw the content on; clicked is true on the
        // click's release, as GUI.Button.
        public static Rect PressableCard(Rect rect, CardLook look, out bool clicked)
        {
            bool hovered = GUI.enabled && rect.Contains(Event.current.mousePosition);
            // hotControl is the control the mouse went down on; held over this card, it is this card being pressed.
            bool pressed = hovered && GUIUtility.hotControl != 0;
            if (hovered && Event.current.type == EventType.Repaint)
            {
                if (rect != hoverRect || Time.frameCount > hoverFrame + 1) UiSound.Play(UiSound.Cue.Move);
                hoverRect = rect;
                hoverFrame = Time.frameCount;
            }
            Rect face = Card(rect, look, hovered, pressed);
            clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            return face;
        }

        // A pressable card with a label on its face.
        public static bool CardButton(Rect rect, string text, GUIStyle style, CardLook look = CardLook.Plain)
        {
            Rect face = PressableCard(rect, look, out bool clicked);
            GUI.Label(face, text, style);
            return clicked;
        }

        public static void ShadowLabel(Rect rect, string text, GUIStyle style)
        {
            Color color = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, .8f);
            GUI.Label(new Rect(rect.x + 3, rect.y + 3, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        // The atlas contains five front views in Alpha/Beta/Delta/Theta/Omega order.
        public static void Portrait(Texture atlas, int runner, Rect rect)
        {
            if (atlas == null) return;
            GUI.DrawTextureWithTexCoords(rect, atlas, new Rect(runner / 5f, 0, 0.2f, 1), true);
        }

        // Deterministic original city art, baked to a PNG by the editor integration command.
        public static Texture2D BuildBackdrop()
        {
            const int width = 1600, height = 900, horizon = 430;
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float floor = y < horizon ? 0.035f * y / horizon : 0;
                    float dx = (x - 1160f) / 490f, dy = (y - 450f) / 330f;
                    float halo = Mathf.Max(0, 1 - dx * dx - dy * dy) * 0.025f;
                    pixels[y * width + x] = Background + new Color(floor * 0.4f, floor + halo, floor * 1.4f + halo, 0);
                }
            for (int i = 0; i < 25; i++)
            {
                int x = i * 137 % width, w = 49 + i * 17 % 83, h = 95 + i * 47 % 285;
                Fill(pixels, width, height, x, horizon, w, h, new Color(.039f + i % 3 * .004f, .066f + i % 3 * .005f, .106f + i % 3 * .008f));
                for (int row = 0; row < 11; row++)
                    for (int col = 0; col < 4; col++)
                        if ((row + col + i) % 4 == 0)
                            Fill(pixels, width, height, x + 9 + col * 17, horizon + 13 + row * 22, 3, 4, new Color(.075f, .11f, .16f));
                if (i % 6 == 0) Fill(pixels, width, height, x + w - 9, horizon + 19, 2, h / 2, new Color(.08f, .24f, .28f));
            }
            Color grid = new Color(.07f, .10f, .15f);
            for (int i = -8; i < 10; i++) Line(pixels, width, height, 1050, horizon, 800 + i * 250, 0, grid);
            for (int i = 1; i < 8; i++)
            {
                int y = horizon - horizon * i * i / 64;
                Fill(pixels, width, height, 0, y, width, 1, grid);
            }
            Fill(pixels, width, height, 1100, 655, 390, 2, new Color(.07f, .18f, .22f));
            Fill(pixels, width, height, 1488, 430, 2, 227, new Color(.07f, .18f, .22f));
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false) { name = "Night City" };
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static void Fill(Color[] p, int width, int height, int x, int y, int w, int h, Color color)
        {
            for (int yy = Mathf.Max(0, y); yy < Mathf.Min(height, y + h); yy++)
                for (int xx = Mathf.Max(0, x); xx < Mathf.Min(width, x + w); xx++) p[yy * width + xx] = color;
        }

        private static void Line(Color[] p, int width, int height, int x0, int y0, int x1, int y1, Color color)
        {
            int count = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int i = 0; i <= count; i++)
            {
                float t = count == 0 ? 0 : (float)i / count;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                if (x >= 0 && x < width && y >= 0 && y < height) p[y * width + x] = color;
            }
        }
    }
}
