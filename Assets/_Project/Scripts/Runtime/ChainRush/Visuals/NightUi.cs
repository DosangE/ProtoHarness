using UnityEngine;

namespace ProtoHarness.ChainRush.Visuals
{
    // Shared IMGUI art for Night Drift. Input and race state stay with each screen.
    public static class NightUi
    {
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
