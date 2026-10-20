using System;
using ProtoHarness.ChainRush.Track;
using ProtoHarness.ChainRush.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProtoHarness.ChainRush
{
    public sealed class ChainRushHud : MonoBehaviour
    {
        // The pause menu's entries (ESC during a run, user 2026-10-20). Restart only where a run can be restarted
        // (ManualStart), back to the lobby only in a race the lobby launched.
        public enum PauseChoice { Continue, Restart, Lobby }

        [SerializeField] private ChainRushGame game;
        [SerializeField] private RunnerMotor player;
        [SerializeField] private GrappleController grapple;
        [SerializeField] private Camera viewCamera;
        [Tooltip("Optional: only the race scene the lobby loads has one. Without it the HUD shows its own start panel.")]
        [SerializeField] private Flow.RaceAssembler assembler;
        private GUIStyle small;
        private GUIStyle body;
        private GUIStyle heading;
        private GUIStyle title;
        private GUIStyle number;
        private GUIStyle button;
        private GUIStyle waiting;
        private GUIStyle cue;
        private GUIStyle pauseItem;
        private Font font;
        private int pauseCursor;
        private static readonly string[] PauseLabels = { "계속", "재시작", "로비로" };
        private const int CueSize = 160;
        private const string CueGlyphs = "321GO!";
        // How long "GO!" stays up once a run has started.
        private const float GoSeconds = 1f;
        private static readonly Color Ink = new Color(0.025f, 0.055f, 0.08f, 0.94f);
        private static readonly Color Mint = new Color(0.43f, 1f, 0.8f);
        private static readonly Color Muted = new Color(0.58f, 0.71f, 0.75f);
        private static readonly Color Coral = new Color(1f, 0.36f, 0.3f);
        // Over the game while paused: dims it without hiding it.
        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.5f);
        private static readonly Color Row = new Color(0.08f, 0.13f, 0.16f, 1f);

        // The highlighted pause entry; back on Continue whenever the game is not paused.
        public PauseChoice PauseCursor => (PauseChoice)pauseCursor;

        private void Awake()
        {
            if (game == null || player == null || grapple == null || viewCamera == null)
            {
                Debug.LogError("ChainRushHud: game, player, grapple and camera are required.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!game.IsPaused)
            {
                pauseCursor = (int)PauseChoice.Continue;
                return;
            }
            // ESC (continue) is the game's own key; the game ignores Enter while paused.
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.upArrowKey.wasPressedThisFrame) MovePause(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame) MovePause(1);
            else if (keyboard.enterKey.wasPressedThisFrame) ChoosePause(PauseCursor);
        }

        // Whether this race's pause menu has that entry.
        public bool Offers(PauseChoice choice) =>
            choice == PauseChoice.Continue || (choice == PauseChoice.Restart && game.ManualStart) || (choice == PauseChoice.Lobby && FromLobby);

        // Moves the pause highlight, wrapping and skipping entries this race does not offer (Continue always is).
        public void MovePause(int delta)
        {
            if (!game.IsPaused) throw new InvalidOperationException("ChainRushHud: the pause menu is not open.");
            int count = PauseLabels.Length;
            do pauseCursor = ((pauseCursor + delta) % count + count) % count;
            while (!Offers((PauseChoice)pauseCursor));
        }

        // Does what a pause entry says: go on, count in a new run from the start line, or leave for the lobby.
        public void ChoosePause(PauseChoice choice)
        {
            if (!game.IsPaused) throw new InvalidOperationException("ChainRushHud: the pause menu is not open.");
            if (!Offers(choice)) throw new InvalidOperationException($"ChainRushHud: this race's pause menu has no {choice}.");
            switch (choice)
            {
                case PauseChoice.Continue: game.TogglePause(); break;
                case PauseChoice.Restart: game.StartCountdown(); break;
                case PauseChoice.Lobby: assembler.ReturnToLobby(); break;
            }
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
                Debug.LogError("ChainRushHud: could not create the HUD font.", this);
                enabled = false;
                return;
            }
            small = Style(15, Muted);
            body = Style(21, Color.white);
            heading = Style(32, Color.white, FontStyle.Bold);
            title = Style(96, Color.white, FontStyle.Bold);
            number = Style(38, Mint, FontStyle.Bold);
            button = Style(22, Ink, FontStyle.Bold);
            button.alignment = TextAnchor.MiddleCenter;
            waiting = Style(22, Color.white, FontStyle.Bold);
            waiting.alignment = TextAnchor.MiddleCenter;
            pauseItem = Style(22, Color.white, FontStyle.Bold);
            pauseItem.alignment = TextAnchor.MiddleCenter;
            cue = Style(CueSize, Mint, FontStyle.Bold);
            cue.alignment = TextAnchor.MiddleCenter;
            // The first draw of new glyphs can stall a frame (DECISIONS P3 C1c-2b), and "GO!" is drawn on a run's first frame.
            font.RequestCharactersInTexture(CueGlyphs, CueSize, FontStyle.Bold);
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
            DrawHeader(width);
            DrawControls(width, height);
            // A race from the lobby skips the start panel: it counts in once its environment has loaded.
            if (game.IsReady && FromLobby) DrawLoading(width, height);
            else if (game.IsReady) DrawReady(width, height);
            else if (game.IsCountingDown) DrawCue(width, height, game.CountdownNumber.ToString());
            else if (game.IsPaused) DrawPause(width, height);
            else if (game.HasFailed || game.HasFinished) DrawResult(width, height);
            else
            {
                DrawPlay(width, height, scale);
                if (game.Elapsed < GoSeconds) DrawCue(width, height, "GO!");
            }
            GUI.matrix = previous;
        }

        private bool FromLobby => assembler != null && assembler.IsLaunched;

        private void DrawLoading(float width, float height)
        {
            Rect panel = new Rect(width / 2f - 210, height / 2f - 40, 420, 80);
            Block(panel, Ink);
            GUI.Label(panel, "트랙을 불러오는 중", waiting);
        }

        // The countdown's 3, 2, 1 and the "GO!" after it, large in the middle of the screen.
        private void DrawCue(float width, float height, string text)
        {
            Block(new Rect(width / 2f - 170, height / 2f - 130, 340, 220), Ink);
            GUI.Label(new Rect(width / 2f - 170, height / 2f - 130, 340, 220), text, cue);
        }

        private void DrawHeader(float width)
        {
            Block(new Rect(32, 26, 310, 74), Ink);
            Block(new Rect(32, 26, 5, 74), Mint);
            GUI.Label(new Rect(53, 37, 270, 38), "CHAIN / RUSH", heading);
            GUI.Label(new Rect(55, 78, 280, 20), game.IsEndless ? "03D    /    ENDLESS PURSUIT" : game.IsCircuit ? "03D    /    CIRCUIT TRIAL" : "03D    /    SKYLINE TRIAL", small);
            Block(new Rect(width - 382, 26, 350, 74), Ink);
            GUI.Label(new Rect(width - 359, 37, 155, 22), "SUIT INTEGRITY", small);
            for (int i = 0; i < 3; i++)
                Block(new Rect(width - 356 + i * 49, 68, 37, 10), i < game.Health ? Mint : new Color(0.17f, 0.23f, 0.26f));
            GUI.Label(new Rect(width - 177, 43, 130, 45), game.Elapsed.ToString("00.0") + "s", heading);
        }

        private void DrawControls(float width, float height)
        {
            Block(new Rect(32, height - 92, width - 64, 60), Ink);
            GUI.Label(new Rect(55, height - 75, 175, 30), "↑ 가속   ← / → 이동", body);
            GUI.Label(new Rect(232, height - 75, 390, 30), "ALT   점프 → 공중에서 다시 잡기", body);
            GUI.Label(new Rect(650, height - 75, 270, 30), "ALT 유지  스윙 / ↓  해제", body);
            GUI.Label(new Rect(951, height - 75, 210, 30), "SPACE   공격", body);
            GUI.Label(new Rect(width - 405, height - 75, 390, 30), game.HasPresentation ? "ESC  일시정지   M  음소거" : "ESC  일시정지", body);
        }

        private void DrawReady(float width, float height)
        {
            Block(new Rect(32, 128, 750, height - 248), Ink);
            GUI.Label(new Rect(69, 169, 640, 26), game.IsEndless ? "FIELD TEST  /  002                         ENDLESS COMBAT" : game.IsCircuit ? "FIELD TEST  /  003                         TIME ATTACK" : "FIELD TEST  /  001                         MOVEMENT PROTOTYPE", small);
            GUI.Label(new Rect(62, 222, 660, 130), "CHAIN", title);
            GUI.Label(new Rect(62, 326, 660, 130), "THE SKY.", title);
            Block(new Rect(70, 470, 65, 4), Mint);
            GUI.Label(new Rect(70, 500, 640, 37), "달리고, 걸고, 도약하세요.", heading);
            GUI.Label(new Rect(70, 551, 655, 35), game.IsEndless ? "끝없는 옥상을 달리며 침입 드론을 체인으로 격파하세요." : game.IsCircuit ? "서킷을 " + game.Circuit.LapCount + "랩 달려 가장 빠른 기록을 세우세요." : "공중의 앵커를 이어 타고 496m 결승선에 도달하세요.", body);
            GUI.Label(new Rect(70, 590, 655, 30), "노란 선에서 ALT 점프 → 공중에서 다시 ALT 누르고 유지", body);
            Rect start = new Rect(70, height - 240, 370, 68);
            if (game.ManualStart)
            {
                Block(start, Mint);
                if (GUI.Button(start, "ENTER   /   START RUN  →", button)) game.StartCountdown();
            }
            else
            {
                Block(start, new Color(0.2f, 0.3f, 0.33f));
                GUI.Label(start, "출발 신호를 기다리는 중", waiting);
            }
            GUI.Label(new Rect(470, height - 218, 240, 24), game.IsEndless ? "무한 생존  ·  체력 3칸" : game.IsCircuit ? game.Circuit.LapCount + "랩  ·  타임어택" : "약 50초  ·  체력 3칸", small);

            Block(new Rect(width - 420, height - 390, 388, 255), Ink);
            GUI.Label(new Rect(width - 391, height - 361, 320, 30), "READ THE COURSE", body);
            GUI.Label(new Rect(width - 391, height - 308, 335, 27), "01   MINT      공중 그래플 앵커", small);
            GUI.Label(new Rect(width - 391, height - 269, 335, 27), game.IsEndless ? "02   경고      위 / 왼쪽 / 오른쪽 진입" : game.IsCircuit ? "02   GAP      틈은 점프 / 그래플" : "02   CORAL   위험물 / 좌우 회피", small);
            GUI.Label(new Rect(width - 391, height - 230, 335, 27), game.IsEndless ? "03   SPACE   조준 표시 후 1.2초 이내" : game.IsCircuit ? "03   LINE     출발선 · 체크포인트" : "03   GOLD     표적 / SPACE 공격", small);
            GUI.Label(new Rect(width - 391, height - 179, 330, 24), "INSPIRED BY CHAIN-RUSH  /  DOSANGE", small);
        }

        private void DrawPlay(float width, float height, float scale)
        {
            Block(new Rect(width / 2f - 210, 34, 420, 62), Ink);
            GUI.Label(new Rect(width / 2f - 184, 42, 380, 24), game.IsEndless ? "ENDLESS / " + game.Distance.ToString("0") + " M     HITS / " + game.Hits : game.IsCircuit ? CircuitLine() : "ROUTE  /  " + Mathf.FloorToInt(game.Progress * 496f) + " M   →   496 M", small);
            Block(new Rect(width / 2f - 184, 78, 368, 4), new Color(0.2f, 0.3f, 0.33f));
            Block(new Rect(width / 2f - 184, 78, 368 * (game.IsEndless ? (float)(game.Distance % 500d / 500d) : game.Progress), 4), Mint);
            if (game.IsEndless) DrawEncounter(width, scale);
            Block(new Rect(32, height - 220, 168, 105), Ink);
            GUI.Label(new Rect(54, height - 207, 140, 20), "VELOCITY / M·S", small);
            GUI.Label(new Rect(52, height - 180, 140, 50), player.HorizontalSpeed.ToString("00.0"), number);
            DrawBoost(width, height);
            string status = grapple.IsAttached ? "LINKED  /  놓으면 도약" : player.IsGrounded ? "RUN  /  노란 선에서 점프" : "AIR  /  ALT 로 앵커 잡기";
            if (player.IsSwinging) status = "SWING  /  안쪽 키 줄 짧게 · 바깥 키 길게 · SHIFT 떼면 직진";
            if (grapple.JustMissed) status = "OUT OF RANGE  /  앵커에 더 가까이";
            Block(new Rect(width / 2f - 240, height - 151, 480, 40), Ink);
            GUI.Label(new Rect(width / 2f - 220, height - 145, 450, 28), status, body);
            Transform anchor = grapple.Candidate;
            if (anchor != null)
            {
                Vector3 screen = viewCamera.WorldToScreenPoint(anchor.position);
                if (screen.z > 0f)
                {
                    float x = screen.x / scale;
                    float y = (Screen.height - screen.y) / scale;
                    Color color = grapple.IsAttached ? Color.white : Mint;
                    Block(new Rect(x - 26, y - 26, 15, 3), color);
                    Block(new Rect(x - 26, y - 26, 3, 15), color);
                    Block(new Rect(x + 11, y + 23, 15, 3), color);
                    Block(new Rect(x + 23, y + 11, 3, 15), color);
                    GUI.Label(new Rect(x + 34, y - 10, 170, 25), "LINK / " + Vector3.Distance(player.transform.position, anchor.position).ToString("0") + "m", small);
                }
            }
            if (grapple.HasPoleCandidate) DrawPoleCandidate(scale);
            if (game.DamageFlash)
            {
                Color flash = Coral;
                flash.a = Mathf.PingPong(Time.time * 2f, 0.25f);
                Block(new Rect(0, 0, 9, height), flash);
                Block(new Rect(width - 9, 0, 9, height), flash);
            }
        }

        // The corner pole Shift would hook now, bracketed at the band the chain bites, like an anchor. A hook costs nothing,
        // so it is always mint.
        private void DrawPoleCandidate(float scale)
        {
            Vector3 top = SwingLook.PoleBite(grapple.PoleCandidate);
            Vector3 screen = viewCamera.WorldToScreenPoint(top);
            if (screen.z <= 0f) return;
            float x = screen.x / scale;
            float y = (Screen.height - screen.y) / scale;
            Color color = Mint;
            Block(new Rect(x - 26, y - 26, 15, 3), color);
            Block(new Rect(x - 26, y - 26, 3, 15), color);
            Block(new Rect(x + 11, y + 23, 15, 3), color);
            Block(new Rect(x + 23, y + 11, 3, 15), color);
            GUI.Label(new Rect(x + 34, y - 10, 200, 25), "POLE / SHIFT  " + Vector3.Distance(player.transform.position, top).ToString("0") + "m", small);
        }

        // Booster gauge at the bottom middle, above the status line (KartRider style): the bar fills toward the next booster
        // as the runner swings, and each whole gauge slot is a booster waiting in an item box beside it, spent by Ctrl.
        // Only how the gauge is shown: the slots are still RacerState.Gauge's whole part, its fraction the bar.
        private void DrawBoost(float width, float height)
        {
            const float BarWidth = 420f, Box = 52f, Gap = 10f;
            int boxes = (int)RacerState.MaxGauge;
            float left = width / 2f - (BarWidth + boxes * (Gap + Box)) / 2f;
            float top = height - 212f;
            float gauge = player.Gauge;
            int boosters = Mathf.Min(boxes, Mathf.FloorToInt(gauge));
            bool full = boosters >= boxes;
            float fill = full ? 1f : gauge - boosters;
            Block(new Rect(left, top, BarWidth, Box), Ink);
            GUI.Label(new Rect(left + 18, top + 7, 220, 20), "BOOST  /  CTRL", small);
            GUI.Label(new Rect(left + BarWidth - 78, top + 7, 60, 20), full ? "MAX" : Mathf.FloorToInt(fill * 100f) + "%", small);
            var bar = new Rect(left + 18, top + 32, BarWidth - 36, 8);
            Block(bar, new Color(0.2f, 0.3f, 0.33f));
            bar.width *= fill;
            Block(bar, Mint);
            for (int i = 0; i < boxes; i++)
            {
                var slot = new Rect(left + BarWidth + Gap + i * (Gap + Box), top, Box, Box);
                Block(slot, Ink);
                var item = new Rect(slot.x + 6, slot.y + 6, Box - 12, Box - 12);
                Block(item, i < boosters ? Mint : new Color(0.2f, 0.3f, 0.33f));
                if (i < boosters) GUI.Label(item, "▶▶", button);
            }
        }

        private void DrawEncounter(float width, float scale)
        {
            var enemy = game.Enemies;
            if (!enemy.HasEncounter) return;
            string direction = enemy.Direction == Combat.EnemyDirector.Entrance.Above ? "↓  상공" : enemy.Direction == Combat.EnemyDirector.Entrance.Left ? "→  왼쪽" : "←  오른쪽";
            string prompt = enemy.CanAttack ? "SPACE  /  체인 발사   " + enemy.Remaining.ToString("0.0") + "s" :
                enemy.State == Combat.EnemyDirector.EncounterState.Firing ? "CHAIN OUT  /  연결 중" :
                enemy.State == Combat.EnemyDirector.EncounterState.Retracting ? "TARGET BROKEN  /  회수" :
                enemy.State == Combat.EnemyDirector.EncounterState.Striking ? "MISSED  /  적 공격" : direction + "에서 적 진입 — 준비";
            Block(new Rect(width / 2f - 250, 118, 500, 86), Ink);
            GUI.Label(new Rect(width / 2f - 224, 135, 460, 32), prompt, body);
            Block(new Rect(width / 2f - 224, 181, 448 * (enemy.CanAttack ? enemy.WindowFraction : 1f), 5), enemy.CanAttack ? Mint : Coral);
            if (!enemy.CanAttack) return;
            Vector3 screen = viewCamera.WorldToScreenPoint(enemy.Target.position);
            if (screen.z <= 0f) return;
            float x = screen.x / scale;
            float y = (Screen.height - screen.y) / scale;
            Block(new Rect(x - 42, y - 42, 84, 3), Coral);
            Block(new Rect(x - 42, y + 39, 84, 3), Coral);
            GUI.Label(new Rect(x - 45, y + 47, 110, 24), "SPACE", body);
        }

        private void DrawResult(float width, float height)
        {
            float x = width / 2f - 335;
            float y = height / 2f - 215;
            Block(new Rect(x, y, 670, 410), Ink);
            Block(new Rect(x, y, 670, 5), game.HasFailed ? Coral : Mint);
            GUI.Label(new Rect(x + 42, y + 34, 600, 25), game.IsCircuit ? "CIRCUIT TRIAL / RACE STATUS" : "SKYLINE TRIAL / MISSION STATUS", small);
            string label = game.HasFinished ? (game.IsCircuit ? "RACE COMPLETE." : "ROUTE COMPLETE.") : "SIGNAL LOST.";
            GUI.Label(new Rect(x + 40, y + 84, 610, 54), label, number);
            string subtitle = game.HasFinished ? "결승선 도착. 다음 기록에 도전하세요." : "다시 도전하세요. 노란 선에서 점프 후 앵커를 잡으세요.";
            GUI.Label(new Rect(x + 42, y + 147, 610, 35), subtitle, body);
            GUI.Label(new Rect(x + 42, y + 207, 600, 34), game.IsCircuit ? CircuitResult() : "TIME   " + game.Elapsed.ToString("0.0") + "s      LINKS   " + game.Grapples + "      HITS   " + game.Hits, body);
            // A run that cannot be restarted from here (ManualStart off) shows no RUN AGAIN.
            if (!game.ManualStart) return;
            Rect action = new Rect(x + 42, y + 283, 586, 66);
            // A race from the lobby ends with two ways on: again, or back to the lobby.
            if (FromLobby)
            {
                action.width = 288;
                Rect lobby = new Rect(x + 340, y + 283, 288, 66);
                Block(lobby, Muted);
                if (GUI.Button(lobby, "ESC  /  로비로", button)) assembler.ReturnToLobby();
            }
            Block(action, Mint);
            if (GUI.Button(action, "ENTER  /  RUN AGAIN  →", button)) game.StartCountdown();
        }

        // The pause menu over the dimmed game: the race so far and the entries this race offers, the highlighted one in
        // mint. A click chooses after the rows are drawn, as the lobby does (choosing changes what is drawn).
        private void DrawPause(float width, float height)
        {
            Block(new Rect(0, 0, width, height), Dim);
            float x = width / 2f - 260;
            float y = height / 2f - 230;
            Block(new Rect(x, y, 520, 460), Ink);
            Block(new Rect(x, y, 520, 5), Mint);
            GUI.Label(new Rect(x + 42, y + 34, 440, 25), "ESC  계속      ↑ / ↓  선택      ENTER  결정", small);
            GUI.Label(new Rect(x + 40, y + 70, 440, 54), "PAUSED", number);
            if (game.IsCircuit) GUI.Label(new Rect(x + 42, y + 130, 440, 30), CircuitLine(), body);
            int clicked = -1;
            float rowY = y + 186;
            for (int i = 0; i < PauseLabels.Length; i++)
            {
                if (!Offers((PauseChoice)i)) continue;
                var row = new Rect(x + 42, rowY, 436, 66);
                bool highlighted = i == pauseCursor;
                Block(row, highlighted ? Mint : Row);
                if (GUI.Button(row, PauseLabels[i], highlighted ? button : pauseItem)) clicked = i;
                rowY += 84;
            }
            if (clicked >= 0) ChoosePause((PauseChoice)clicked);
        }

        // "LAP 2 / 3   01:23.4": the lap being run and the whole race time so far.
        private string CircuitLine()
        {
            LapCounter laps = game.Circuit.Laps;
            return "LAP  " + laps.CurrentLap + " / " + laps.LapCount + "     " + FormatTime(game.Elapsed);
        }

        // Lap times (completed laps only) and the total.
        private string CircuitResult()
        {
            LapCounter laps = game.Circuit.Laps;
            var text = new System.Text.StringBuilder();
            for (int lap = 1; lap <= laps.CompletedLaps; lap++) text.Append("L").Append(lap).Append(" ").Append(FormatTime(Ticks.ToSeconds(laps.LapTicks(lap)))).Append("   ");
            text.Append(laps.IsFinished ? "TOTAL " + FormatTime(Ticks.ToSeconds(laps.TotalTicks)) : "TIME " + FormatTime(game.Elapsed));
            return text.ToString();
        }

        // "01:23.4". The lobby shows best records the same way.
        internal static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            return minutes.ToString("00") + ":" + (seconds - minutes * 60f).ToString("00.0");
        }

        private void OnDestroy()
        {
            if (font != null) Destroy(font);
        }
    }
}
