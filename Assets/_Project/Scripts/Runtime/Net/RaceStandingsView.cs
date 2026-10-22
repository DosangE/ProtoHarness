using System;
using System.Diagnostics;
using UnityEngine;
using ProtoHarness.ChainRush.Visuals;
using Debug = UnityEngine.Debug;

namespace ProtoHarness.Net
{
    // Draws the standings a RaceClient holds as a panel under the HUD's header, on the left where the HUD draws nothing while
    // a run goes. Added by the client at run time, like RemoteGhost, so the scene needs no edit. Nothing is drawn before the
    // first standings arrive. The lines are rebuilt only when new standings or a new order of the running racers have come
    // (RaceClient.StandingsReceived, OrderReceived).
    // Right after the first standings the editor client stalled for about 95 ms, long enough for its inputs to reach the
    // server late (DECISIONS P3 C1c-2b). So the font and its glyphs are made in Initialize, and the panel's first draw is
    // done unseen on the first repaint, before the run has standings; both draws log how long they took
    // (RACE-STANDINGS-VIEW). Without a graphics device (-nographics) nothing is drawn.
    public sealed class RaceStandingsView : MonoBehaviour
    {
        // Every character RaceStandingsText writes: the header, names, places, times and status words.
        private const string Glyphs = " /:.–0123456789RACESYOU대기주행중낙하잠정확정";
        private const int SmallSize = 15;
        private const int BodySize = 19;

        // The same ink and colours as ChainRushHud.
        private static readonly Color Ink = new Color(0.025f, 0.055f, 0.08f, 0.94f);
        private static readonly Color Mint = new Color(0.43f, 1f, 0.8f);
        private static readonly Color Muted = new Color(0.58f, 0.71f, 0.75f);
        private static readonly Color Coral = new Color(1f, 0.36f, 0.3f);
        private const float Left = 32f;
        private const float Top = 128f;
        private const float Width = 310f;
        private const float LineHeight = 30f;

        private RaceClient client;
        private RaceStandingsText.Line[] lines;
        private string header;
        private int shownReceived;
        private int shownOrder;
        private Font font;
        private GUIStyle small;
        private GUIStyle body;
        private GUIStyle you;
        private GUIStyle fallen;
        private bool warmed;
        private bool firstDrawLogged;

        // How many lines the newest standings drew (0 before the first arrive).
        public int LinesShown => lines?.Length ?? 0;

        public void Initialize(RaceClient client)
        {
            this.client = client != null ? client : throw new ArgumentNullException(nameof(client));
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.Log("RaceStandingsView: no graphics device; the standings are not drawn.");
                enabled = false;
                return;
            }
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 24);
            if (font == null)
            {
                Debug.LogError("RaceStandingsView: could not create the font.", this);
                enabled = false;
                return;
            }
            font.RequestCharactersInTexture(Glyphs, SmallSize, FontStyle.Normal);
            font.RequestCharactersInTexture(Glyphs, BodySize, FontStyle.Normal);
            font.RequestCharactersInTexture(Glyphs, BodySize, FontStyle.Bold);
        }

        private GUIStyle Style(int size, Color color, FontStyle weight = FontStyle.Normal) => NightUi.Style(font, size, color, weight);

        // GUI.skin can only be read inside OnGUI, so the styles are made on the first draw; the font is already made.
        private void InitializeStyles()
        {
            small = Style(SmallSize, Muted);
            body = Style(BodySize, Color.white);
            you = Style(BodySize, Mint, FontStyle.Bold);
            fallen = Style(BodySize, Coral);
        }

        private void OnGUI()
        {
            if (client == null)
            {
                Debug.LogError("RaceStandingsView: call Initialize before it draws.", this);
                enabled = false;
                return;
            }
            if (small == null) InitializeStyles();
            bool repaint = Event.current.type == EventType.Repaint;
            if (!warmed)
            {
                if (!repaint) return;
                long warmStart = Stopwatch.GetTimestamp();
                DrawUnseen();
                warmed = true;
                Debug.Log($"RACE-STANDINGS-VIEW warmed in {MillisecondsSince(warmStart):F1} ms (connected {client.Connected}).");
                return;
            }
            if (client.Standings == null) return;
            long drawStart = repaint && !firstDrawLogged ? Stopwatch.GetTimestamp() : 0;
            if (lines == null || shownReceived != client.StandingsReceived || shownOrder != client.OrderReceived)
            {
                lines = RaceStandingsText.Lines(client.Standings, client.Order, client.You);
                header = RaceStandingsText.Header(lines.Length);
                shownReceived = client.StandingsReceived;
                shownOrder = client.OrderReceived;
            }
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = CanvasMatrix();
            float height = 50f + lines.Length * LineHeight;
            NightUi.Block(new Rect(Left, Top, Width, height), Ink);
            NightUi.Block(new Rect(Left, Top, 5f, height), Mint);
            GUI.Label(new Rect(Left + 21f, Top + 13f, Width - 40f, 20f), header, small);
            for (int i = 0; i < lines.Length; i++)
            {
                RaceStandingsText.Line line = lines[i];
                GUIStyle style = line.IsYou ? you : line.Of == RaceStandings.Status.Failed ? fallen : body;
                float y = Top + 40f + i * LineHeight;
                GUI.Label(new Rect(Left + 21f, y, 28f, 26f), line.Place, style);
                GUI.Label(new Rect(Left + 52f, y, 100f, 26f), line.Name, style);
                GUI.Label(new Rect(Left + 152f, y, Width - 165f, 26f), line.Status, style);
            }
            GUI.matrix = previous;
            if (drawStart != 0)
            {
                firstDrawLogged = true;
                Debug.Log($"RACE-STANDINGS-VIEW first draw in {MillisecondsSince(drawStart):F1} ms ({lines.Length} lines).");
            }
        }

        // The HUD's scale: a 1600 x 900 canvas fitted to the screen.
        private static Matrix4x4 CanvasMatrix()
        {
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            return Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        }

        // Everything the panel draws, once, fully transparent: the blocks and every glyph in each style.
        private void DrawUnseen()
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = CanvasMatrix();
            Color unseen = new Color(1f, 1f, 1f, 0f);
            NightUi.Block(new Rect(Left, Top, Width, 80f), unseen);
            GUI.color = unseen;
            GUI.Label(new Rect(Left + 21f, Top + 13f, Width - 40f, 20f), Glyphs, small);
            GUI.Label(new Rect(Left + 21f, Top + 40f, Width - 40f, 26f), Glyphs, body);
            GUI.Label(new Rect(Left + 21f, Top + 40f, Width - 40f, 26f), Glyphs, you);
            GUI.Label(new Rect(Left + 21f, Top + 40f, Width - 40f, 26f), Glyphs, fallen);
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        private static double MillisecondsSince(long start) => (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;

        private void OnDestroy()
        {
            if (font != null) Destroy(font);
        }
    }
}
