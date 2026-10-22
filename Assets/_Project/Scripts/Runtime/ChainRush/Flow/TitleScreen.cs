using ProtoHarness.ChainRush.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using ProtoHarness.ChainRush.Visuals;

namespace ProtoHarness.ChainRush.Flow
{
    // The entry scene: the game's name, and Enter (or a click) into the lobby. Drawn like ChainRushHud (OnGUI,
    // a 1600 x 900 canvas scaled to the screen, Malgun Gothic). The logo follows KartRider's title (user 2026-10-21): a
    // big slanted name crossed by speed stripes over a checker band, and a pulsing start button. The right-hand middle is
    // left empty until the runner art is settled; the top right names the version and the build.
    public sealed class TitleScreen : MonoBehaviour
    {
        // Not the Player Settings bundle version (ProjectSettings is off limits, CLAUDE.md §0); raised by hand.
        public const string Version = "0.0.1";
        private static readonly Color Ink = NightUi.Background;
        private static readonly Color Mint = NightUi.Accent;
        private static readonly Color Muted = NightUi.Muted;
        // The stripes' lean in degrees about z; with OnGUI's y pointing down, negative raises the right end.
        private const float StripeAngle = -9f;
        private const int CheckerSize = 14, CheckerColumns = 34;
        [SerializeField] private Texture2D nightBackground;
        private GUIStyle small;
        private GUIStyle body;
        private GUIStyle title;
        private GUIStyle button;
        private GUIStyle accentTitle;
        private GUIStyle brand;
        private GUIStyle versionLabel;
        private GUIStyle buildLabel;
        private string versionText;
        private string buildText;
        private Font font;
        private Font displayFont;
        private bool leaving;

        // "TEST BUILD" in the editor and development builds, "RELEASE" in a release build.
        public static string BuildName => Debug.isDebugBuild ? "TEST BUILD" : "RELEASE";

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) Enter();
        }

        // Goes on to the lobby.
        public void Enter()
        {
            if (leaving) return;
            leaving = true;
            UiSound.Play(UiSound.Cue.Confirm);
            SceneManager.LoadScene(RaceLaunch.LobbyScene);
        }

        private GUIStyle Style(int size, Color color, FontStyle weight = FontStyle.Normal) => NightUi.Style(font, size, color, weight);

        private void InitializeStyles()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 24);
            displayFont = NightUi.DisplayFont();
            if (font == null || displayFont == null)
            {
                Debug.LogError("TitleScreen: could not create the font.", this);
                enabled = false;
                return;
            }
            small = Style(15, Muted);
            body = Style(21, Color.white);
            title = NightUi.Style(displayFont, 160, Color.white, FontStyle.BoldAndItalic);
            accentTitle = NightUi.Style(displayFont, 160, Mint, FontStyle.BoldAndItalic);
            brand = NightUi.Style(displayFont, 34, Color.white, FontStyle.BoldAndItalic);
            button = Style(26, Ink, FontStyle.Bold);
            button.alignment = TextAnchor.MiddleCenter;
            versionLabel = NightUi.Style(displayFont, 20, Color.white, FontStyle.Bold);
            versionLabel.alignment = TextAnchor.MiddleLeft;
            buildLabel = NightUi.Style(displayFont, 16, NightUi.Action, FontStyle.Bold);
            buildLabel.alignment = TextAnchor.MiddleRight;
            versionText = "v" + Version;
            buildText = BuildName;
        }

        // Draws a block leaning StripeAngle degrees about pivot (all in the title's units).
        private static void Stripe(Matrix4x4 canvas, Vector2 pivot, Rect rect, Color color)
        {
            GUI.matrix = canvas * Matrix4x4.TRS(pivot, Quaternion.Euler(0, 0, StripeAngle), Vector3.one)
                * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
            NightUi.Block(rect, color);
            GUI.matrix = canvas;
        }

        // A checkered flag band, two rows deep.
        private static void Checker(float x, float y)
        {
            for (int row = 0; row < 2; row++)
                for (int column = 0; column < CheckerColumns; column++)
                    NightUi.Block(new Rect(x + column * CheckerSize, y + row * CheckerSize, CheckerSize, CheckerSize),
                        (row + column) % 2 == 0 ? new Color(1, 1, 1, .85f) : new Color(1, 1, 1, .08f));
        }

        private void OnGUI()
        {
            if (small == null) InitializeStyles();
            if (!enabled) return;
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            Matrix4x4 canvas = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.matrix = canvas;
            NightUi.Backdrop(nightBackground, width, height);
            GUI.Label(new Rect(64, 38, 500, 52), "CHAIN RUSH", brand);

            // Version and build, top right.
            var tag = new Rect(width - 324, 40, 260, 44);
            NightUi.Card(tag);
            NightUi.Block(new Rect(tag.x, tag.y, 4, tag.height), Mint);
            GUI.Label(new Rect(tag.x + 20, tag.y, 110, tag.height), versionText, versionLabel);
            GUI.Label(new Rect(tag.x + 110, tag.y, tag.width - 128, tag.height), buildText, buildLabel);

            // Speed stripes behind the logo, leaning up to the right.
            var pivot = new Vector2(480, 420);
            Stripe(canvas, pivot, new Rect(-300, 352, 1380, 18), new Color(Mint.r, Mint.g, Mint.b, .55f));
            Stripe(canvas, pivot, new Rect(-300, 380, 1220, 6), new Color(1f, 1f, 1f, .35f));
            Stripe(canvas, pivot, new Rect(-300, 395, 1060, 3), NightUi.Action);
            Stripe(canvas, pivot, new Rect(-300, 520, 900, 2), new Color(Mint.r, Mint.g, Mint.b, .35f));

            GUI.Label(new Rect(100, 205, 700, 30), "N I G H T   C I R C U I T", small);
            NightUi.ShadowLabel(new Rect(87, 235, 780, 190), "CHAIN", title);
            NightUi.ShadowLabel(new Rect(167, 387, 780, 190), "RUSH", accentTitle);
            Checker(100, 590);
            GUI.Label(new Rect(100, 640, 700, 36), "도시가 잠들면, 레이스가 시작된다.", body);

            // The start button, its glow breathing so the eye finds it.
            Rect enter = new Rect(100, height - 190, 450, 84);
            float glow = Mathf.PingPong(Time.unscaledTime * 0.8f, 1f);
            NightUi.Block(new Rect(enter.x - 6, enter.y - 6, enter.width + 12, enter.height + 12),
                new Color(NightUi.Action.r, NightUi.Action.g, NightUi.Action.b, .12f + .3f * glow));
            bool start = NightUi.CardButton(enter, "레이스 시작     ENTER   →", button, NightUi.CardLook.Action);
            GUI.Label(new Rect(64, height - 60, 700, 24), "ENTER 시작", small);
            GUI.matrix = previous;
            if (start) Enter();
        }

        private void OnDestroy()
        {
            if (font != null) Destroy(font);
            if (displayFont != null) Destroy(displayFont);
        }
    }
}
