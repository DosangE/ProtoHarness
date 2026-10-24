using ProtoHarness.ChainRush.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProtoHarness.ChainRush.Flow.Lobby
{
    // The runners, as portrait tiles along the bottom. The highlighted runner's look turns in the preview (drag across it
    // with the left button) beside its stats; a click on a tile only highlights it, and the start button or Enter
    // starts the race.
    public sealed class RunnerPage : LobbyPage
    {
        // The preview sits between the list and the right-hand panels, as tall as the stats panel's bottom; its texture is
        // drawn at twice the lobby's units.
        private const float PreviewWidth = 480f, PreviewTop = 165f, PreviewHeight = 480f;
        private const int PreviewTextureScale = 2;
        // Transparent, so the backdrop shows around the runner.
        private static readonly Color PreviewBackground = new Color(0, 0, 0, 0);
        private static readonly string[] StatNames = { "최고 속도", "가속", "게이지 충전", "무게" };
        // A stat bar is full at this scale; the runners differ by a few percent, so the bars start from StatBarFloor.
        private const float StatBarFloor = 0.7f, StatBarCeiling = 1.3f;

        private readonly string[] labels;
        // Per runner: the four stats (StatNames order) and their "x1.03" labels, made once.
        private readonly float[,] stats;
        private readonly string[,] statTexts;
        // A drag that began on the preview turns it until the button is released.
        private bool dragging;

        public RunnerPage(LobbyContext context)
            : base(context, LobbyMenu.Page.Runner, "RUNNER", ListHelp + "      마우스 드래그  회전")
        {
            int count = Context.Runners.Count;
            labels = new string[count];
            stats = new float[count, StatNames.Length];
            statTexts = new string[count, StatNames.Length];
            for (int i = 0; i < count; i++)
            {
                RunnerProfile runner = Context.Runners[i];
                labels[i] = runner.DisplayName;
                float[] values = { runner.TopSpeedScale, runner.AccelerationScale, runner.GaugeChargeScale, runner.Weight };
                for (int s = 0; s < values.Length; s++)
                {
                    stats[i, s] = values[s];
                    statTexts[i, s] = "×" + values[s].ToString("0.00");
                }
            }
        }

        // The preview the page shows: one copy of each look, its texture sized for the page. Throws ArgumentException for
        // a look with nothing to draw.
        public static RunnerPreview MakePreview(GameObject[] looks) =>
            new RunnerPreview(looks, (int)PreviewWidth * PreviewTextureScale, (int)PreviewHeight * PreviewTextureScale, PreviewBackground);

        public override string Heading => "CHOOSE YOUR RUNNER";
        public override int Count => labels.Length;
        public override string Label(int row) => labels[row];
        public override bool ClickChooses => false;

        public override Step Confirm(int row)
        {
            Context.Runner = row;
            return Step.Launch;
        }

        public override Step Back() => Step.Open(LobbyMenu.Page.Speed, Context.Speed);

        public override void Opened(int highlight)
        {
            dragging = false;
            Context.Preview.Show(highlight);
        }

        public override void Closed() => Context.Preview.Hide();
        public override void Highlighted(int row) => Context.Preview.Show(row);

        // Turns the preview while the left button, pressed on it, is held.
        public override void Point(Mouse mouse)
        {
            if (mouse.leftButton.wasPressedThisFrame) dragging = OverPreview(mouse.position.ReadValue());
            else if (!mouse.leftButton.isPressed) dragging = false;
            if (dragging) Context.Preview.Turn(mouse.delta.ReadValue().x / LobbyView.GuiScale());
        }

        // A tile per runner along the bottom, left of the start button.
        public override Rect RowRect(int row, int count, float width, float height)
        {
            float tileWidth = (width - 580) / count;
            return new Rect(64 + row * tileWidth, height - 240, tileWidth - 12, 140);
        }

        public override void DrawRow(LobbyView view, Rect face, int row)
        {
            NightUi.Portrait(view.RunnerArtwork, row, new Rect(face.center.x - 42, face.y + 1, 84, 105));
            GUI.Label(new Rect(face.x + 8, face.y + 103, face.width - 16, 34), labels[row], view.TileLabel);
        }

        // The preview, the stats and the start button. Returns whether the start button was pressed.
        public override bool DrawSide(LobbyView view, int cursor, float width, float height)
        {
            DrawPreview(view, width, cursor);
            DrawStats(view, width, cursor);
            var start = new Rect(width - 484, height - 240, 420, 140);
            return NightUi.CardButton(start, "레이스 시작   ENTER  →", view.EnterButton, NightUi.CardLook.Action);
        }

        // The preview's left edge in the lobby's units: centred between the list (ends at 732) and the right-hand panels.
        private static float PreviewLeft(float width) => width * .27f;

        // Whether a mouse position (screen pixels, y up) is over the preview.
        private static bool OverPreview(Vector2 point)
        {
            float scale = LobbyView.GuiScale();
            float x = point.x / scale, y = (Screen.height - point.y) / scale;
            float left = PreviewLeft(Screen.width / scale);
            return x >= left && x < left + PreviewWidth && y >= PreviewTop && y < PreviewTop + PreviewHeight;
        }

        // The highlighted runner's look as the preview draws it, or a line when the runner has none.
        private void DrawPreview(LobbyView view, float width, int shown)
        {
            var rect = new Rect(PreviewLeft(width), PreviewTop, PreviewWidth, PreviewHeight);
            if (Context.Preview.HasModel(shown)) GUI.DrawTexture(rect, Context.Preview.Texture, ScaleMode.StretchToFill, true);
            else GUI.Label(new Rect(rect.x + 24, rect.y + rect.height * 0.5f - 15, rect.width - 48, 30), "디자인 없음", view.Body);
        }

        // The highlighted runner's stats as bars between StatBarFloor and StatBarCeiling, with their scales, and the
        // track and speed class chosen.
        private void DrawStats(LobbyView view, float width, int shown)
        {
            NightUi.PanelCard(new Rect(width - 484, 238, 420, 382));
            GUI.Label(new Rect(width - 452, 261, 360, 28), "RUNNER PROFILE", view.Small);
            GUI.Label(new Rect(width - 452, 298, 360, 62), labels[shown], view.Heading);
            for (int s = 0; s < StatNames.Length; s++)
            {
                float y = 390 + s * 43;
                GUI.Label(new Rect(width - 452, y, 120, 28), StatNames[s], view.Small);
                var bar = new Rect(width - 308, y + 9, 150, 6);
                NightUi.Block(bar, LobbyView.Slate);
                bar.width *= Mathf.InverseLerp(StatBarFloor, StatBarCeiling, stats[shown, s]);
                NightUi.Block(bar, LobbyView.Mint);
                GUI.Label(new Rect(width - 135, y, 65, 28), statTexts[shown, s], view.Small);
            }
            GUI.Label(new Rect(width - 452, 574, 360, 28), Context.Tracks[Context.Track].Title + "  /  " + SpeedPage.LabelAt(Context.Speed), view.Small);
        }
    }
}
