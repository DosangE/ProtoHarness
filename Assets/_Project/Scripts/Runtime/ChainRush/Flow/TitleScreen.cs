using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using ProtoHarness.ChainRush.Visuals;

namespace ProtoHarness.ChainRush.Flow
{
    // The entry scene: the game's name, and Enter (or a click) into the lobby. Drawn like ChainRushHud (OnGUI,
    // a 1600 x 900 canvas scaled to the screen, Malgun Gothic).
    public sealed class TitleScreen : MonoBehaviour
    {
        private static readonly Color Ink = NightUi.Background;
        private static readonly Color Mint = NightUi.Accent;
        private static readonly Color Muted = NightUi.Muted;
        [SerializeField] private Texture2D nightBackground;
        [SerializeField] private Texture2D runnerArtwork;
        private GUIStyle small;
        private GUIStyle body;
        private GUIStyle title;
        private GUIStyle button;
        private GUIStyle accentTitle;
        private GUIStyle brand;
        private Font font;
        private Font displayFont;
        private bool leaving;

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
            SceneManager.LoadScene(RaceLaunch.LobbyScene);
        }

        private GUIStyle Style(int size, Color color, FontStyle weight = FontStyle.Normal)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.font = font;
            style.fontSize = size;
            style.fontStyle = weight;
            style.normal.textColor = color;
            style.padding = new RectOffset(0, 0, 0, 0);
            return style;
        }

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
        }

        private static void Block(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void OnGUI()
        {
            if (small == null) InitializeStyles();
            if (!enabled) return;
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            NightUi.Backdrop(nightBackground, width, height);
            GUI.Label(new Rect(64, 38, 500, 52), "CHAIN RUSH", brand);
            GUI.Label(new Rect(width - 305, 49, 250, 28), "STADIUM / AFTER DARK", small);
            GUI.Label(new Rect(96, 200, 700, 30), "N I G H T   C I R C U I T", small);
            GUI.Label(new Rect(87, 245, 780, 190), "CHAIN", title);
            GUI.Label(new Rect(87, 397, 780, 190), "RUSH", accentTitle);
            GUI.Label(new Rect(96, 585, 700, 36), "도시가 잠들면, 레이스가 시작된다.", body);
            NightUi.Portrait(runnerArtwork, 0, new Rect(width * .55f, 190, 440, 550));
            Rect enter = new Rect(96, height - 210, 450, 84);
            Block(enter, NightUi.Action);
            bool start = GUI.Button(enter, "레이스 시작     ENTER   →", button);
            GUI.Label(new Rect(width * .61f, 764, 500, 26), "ALPHA  /  ARMORED RUNNER", small);
            GUI.Label(new Rect(64, height - 60, 700, 24), "ENTER 시작", small);
            GUI.Label(new Rect(width - 440, height - 60, 380, 24), "CHAIN RUSH  /  NIGHT DRIFT", small);
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
