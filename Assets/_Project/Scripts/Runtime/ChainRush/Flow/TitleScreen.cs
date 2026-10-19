using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProtoHarness.ChainRush.Flow
{
    // The entry scene: the game's name, and Enter (or a click) into the lobby. Drawn like ChainRushHud (OnGUI,
    // a 1600 x 900 canvas scaled to the screen, Malgun Gothic).
    public sealed class TitleScreen : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.025f, 0.055f, 0.08f, 0.94f);
        private static readonly Color Mint = new Color(0.43f, 1f, 0.8f);
        private static readonly Color Muted = new Color(0.58f, 0.71f, 0.75f);
        private GUIStyle small;
        private GUIStyle body;
        private GUIStyle title;
        private GUIStyle button;
        private Font font;
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
            if (font == null)
            {
                Debug.LogError("TitleScreen: could not create the font.", this);
                enabled = false;
                return;
            }
            small = Style(15, Muted);
            body = Style(21, Color.white);
            title = Style(140, Color.white, FontStyle.Bold);
            button = Style(24, Ink, FontStyle.Bold);
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
            Block(new Rect(0, 0, width, height), Ink);
            Block(new Rect(96, 200, 8, 330), Mint);
            GUI.Label(new Rect(130, 196, 900, 26), "03D    /    CIRCUIT RACING PROTOTYPE", small);
            GUI.Label(new Rect(124, 232, 1200, 160), "CHAIN", title);
            GUI.Label(new Rect(124, 372, 1200, 160), "RUSH.", title);
            GUI.Label(new Rect(130, 548, 900, 34), "달리고, 걸고, 도약하세요.", body);
            Rect enter = new Rect(130, height - 250, 420, 72);
            Block(enter, Mint);
            if (GUI.Button(enter, "ENTER   /   시작하기  →", button)) Enter();
            GUI.Label(new Rect(130, height - 80, 600, 24), "INSPIRED BY CHAIN-RUSH  /  DOSANGE", small);
            GUI.matrix = previous;
        }

        private void OnDestroy()
        {
            if (font != null) Destroy(font);
        }
    }
}
