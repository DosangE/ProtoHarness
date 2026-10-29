using System;
using ProtoHarness.ChainRush.Audio;
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
        [SerializeField] private Texture2D nightBackground;
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
        private Font displayFont;
        private GUIStyle speedNumber;
        private GUIStyle raceWord;
        private GUIStyle accentWord;
        private GUIStyle totalTime;
        private GUIStyle compare;
        private GUIStyle compareDelta;
        private GUIStyle compareNew;
        private int pauseCursor;
        private RespawnLook.Watch respawn;
        private static readonly string[] PauseLabels = { "계속", "재시작", "로비로" };
        private const int CueSize = 160;
        private const string CueGlyphs = "321GO!";
        // How long "GO!" stays up once a run has started.
        private const float GoSeconds = 1f;
        private static readonly Color Ink = NightUi.Panel;
        private static readonly Color Mint = NightUi.Accent;
        private static readonly Color Muted = NightUi.Muted;
        private static readonly Color Coral = new Color(1f, 0.36f, 0.3f);
        // Over the game while paused: dims it without hiding it.
        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color Row = NightUi.Border;

        // The highlighted pause entry; back on Continue whenever the game is not paused.
        public PauseChoice PauseCursor => (PauseChoice)pauseCursor;

        // The title at the top left: the lobby's track name, or what the scene is when it was opened directly.
        public string Heading => FromLobby ? assembler.Setup.Title : game.IsCircuit ? "CIRCUIT" : "CHAIN RUSH";

        private void Awake()
        {
            if (game == null || player == null || grapple == null || viewCamera == null)
            {
                Debug.LogError("ChainRushHud: game, player, grapple and camera are required.", this);
                enabled = false;
            }
        }

        // How dark the respawn fade has the screen right now (RespawnLook): 1 the moment a fallen runner is put back.
        public float RespawnFade => RespawnLook.FadeAlpha(respawn.Since(Time.time));

        private void Update()
        {
            respawn.Update(player.IsRespawning, Time.time);
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
            int was = pauseCursor;
            do pauseCursor = ((pauseCursor + delta) % count + count) % count;
            while (!Offers((PauseChoice)pauseCursor));
            if (pauseCursor != was) UiSound.Play(UiSound.Cue.Move);
        }

        // Does what a pause entry says: go on, count in a new run from the start line, or leave for the lobby.
        public void ChoosePause(PauseChoice choice)
        {
            if (!game.IsPaused) throw new InvalidOperationException("ChainRushHud: the pause menu is not open.");
            if (!Offers(choice)) throw new InvalidOperationException($"ChainRushHud: this race's pause menu has no {choice}.");
            UiSound.Play(choice == PauseChoice.Lobby ? UiSound.Cue.Back : UiSound.Cue.Confirm);
            switch (choice)
            {
                case PauseChoice.Continue: game.TogglePause(); break;
                case PauseChoice.Restart: game.StartCountdown(); break;
                case PauseChoice.Lobby: assembler.ReturnToLobby(); break;
            }
        }

        private GUIStyle Style(int size, Color color, FontStyle weight = FontStyle.Normal) => NightUi.Style(font, size, color, weight);

        private void InitializeStyles()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 24);
            displayFont = NightUi.DisplayFont();
            if (font == null || displayFont == null)
            {
                Debug.LogError("ChainRushHud: could not create the HUD font.", this);
                enabled = false;
                return;
            }
            small = Style(15, Muted);
            body = Style(21, Color.white);
            heading = NightUi.Style(displayFont, 36, Color.white, FontStyle.BoldAndItalic);
            title = NightUi.Style(displayFont, 96, Color.white, FontStyle.BoldAndItalic);
            number = NightUi.Style(displayFont, 44, Color.white, FontStyle.BoldAndItalic);
            speedNumber = NightUi.Style(displayFont, 94, Color.white, FontStyle.BoldAndItalic);
            speedNumber.alignment = TextAnchor.MiddleRight;
            raceWord = NightUi.Style(displayFont, 112, Color.white, FontStyle.BoldAndItalic);
            accentWord = NightUi.Style(displayFont, 112, Mint, FontStyle.BoldAndItalic);
            totalTime = NightUi.Style(displayFont, 84, Color.white, FontStyle.BoldAndItalic);
            compare = Style(18, Color.white);
            compareDelta = Style(18, Color.white, FontStyle.Bold);
            compareDelta.alignment = TextAnchor.UpperRight;
            compareNew = Style(18, Mint, FontStyle.Bold);
            compareNew.alignment = TextAnchor.UpperRight;
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
                // Over everything, the screen coming up from black after a fall hides the camera's cut.
                float fade = RespawnFade;
                if (fade > 0f) NightUi.Block(new Rect(0, 0, width, height), new Color(0f, 0f, 0f, fade));
            }
            GUI.matrix = previous;
        }

        private bool FromLobby => assembler != null && assembler.IsLaunched;

        private void DrawLoading(float width, float height)
        {
            Rect panel = new Rect(width / 2f - 210, height / 2f - 40, 420, 80);
            NightUi.Block(panel, Ink);
            GUI.Label(panel, "트랙을 불러오는 중", waiting);
        }

        // The countdown's 3, 2, 1 and the "GO!" after it, large in the middle of the screen.
        private void DrawCue(float width, float height, string text)
        {
            NightUi.ShadowLabel(new Rect(width / 2f - 170, height / 2f - 130, 340, 220), text, cue);
        }

        private void DrawHeader(float width)
        {
            NightUi.ShadowLabel(new Rect(64, 38, 410, 54), Heading, heading);
            GUI.Label(new Rect(66, 97, 350, 28), game.IsCircuit ? "SINGLE RACE / CIRCUIT" : "SKYLINE TRIAL", small);
            for (int i = 0; i < 3; i++)
                NightUi.Block(new Rect(66 + i * 39, 138, 30, 5), i < game.Health ? Mint : Row);
            if (game.IsCircuit && game.Circuit.Laps != null)
                NightUi.ShadowLabel(new Rect(width - 330, 31, 266, 72), game.Circuit.Laps.CurrentLap + " / " + game.Circuit.Laps.LapCount + " LAP", number);
            GUI.Label(new Rect(width - 330, 118, 100, 26), "TIME", small);
            NightUi.ShadowLabel(new Rect(width - 226, 100, 190, 58), FormatTime(game.Elapsed), heading);
        }

        private void DrawControls(float width, float height)
        {
            NightUi.Block(new Rect(0, height - 72, width, 72), new Color(.025f, .04f, .065f, .85f));
            GUI.Label(new Rect(64, height - 48, width - 128, 30), game.HasPresentation
                ? "↑ 가속   ↓ 후진   ← / → 조향     ALT 점프 · 그래플     SHIFT 스윙     CTRL 부스터     SPACE 공격     ESC 일시정지     M 음소거"
                : "↑ 가속   ↓ 후진   ← / → 조향     ALT 점프 · 그래플     SHIFT 스윙     CTRL 부스터     SPACE 공격     ESC 일시정지", small);
        }

        private void DrawReady(float width, float height)
        {
            NightUi.Block(new Rect(32, 178, 750, height - 298), Ink);
            GUI.Label(new Rect(69, 169, 640, 26), game.IsCircuit ? "FIELD TEST  /  003                         TIME ATTACK" : "FIELD TEST  /  001                         MOVEMENT PROTOTYPE", small);
            GUI.Label(new Rect(62, 222, 660, 130), "CHAIN", title);
            GUI.Label(new Rect(62, 326, 660, 130), "THE SKY.", title);
            NightUi.Block(new Rect(70, 470, 65, 4), Mint);
            GUI.Label(new Rect(70, 500, 640, 37), "달리고, 걸고, 도약하세요.", heading);
            GUI.Label(new Rect(70, 551, 655, 35), game.IsCircuit ? "서킷을 " + game.Circuit.LapCount + "랩 달려 가장 빠른 기록을 세우세요." : "공중의 앵커를 이어 타고 496m 결승선에 도달하세요.", body);
            GUI.Label(new Rect(70, 590, 655, 30), "노란 선에서 ALT 점프 → 공중에서 다시 ALT 누르고 유지", body);
            Rect start = new Rect(70, height - 240, 370, 68);
            if (game.ManualStart)
            {
                if (NightUi.CardButton(start, "ENTER   /   START RUN  →", button, NightUi.CardLook.Action))
                {
                    UiSound.Play(UiSound.Cue.Confirm);
                    game.StartCountdown();
                }
            }
            else
            {
                NightUi.Block(start, new Color(0.2f, 0.3f, 0.33f));
                GUI.Label(start, "출발 신호를 기다리는 중", waiting);
            }
            GUI.Label(new Rect(470, height - 218, 240, 24), game.IsCircuit ? game.Circuit.LapCount + "랩  ·  타임어택" : "약 50초  ·  체력 3칸", small);

            NightUi.Block(new Rect(width - 420, height - 390, 388, 255), Ink);
            GUI.Label(new Rect(width - 391, height - 361, 320, 30), "READ THE COURSE", body);
            GUI.Label(new Rect(width - 391, height - 308, 335, 27), "01   MINT      공중 그래플 앵커", small);
            GUI.Label(new Rect(width - 391, height - 269, 335, 27), game.IsCircuit ? "02   GAP      틈은 점프 / 그래플" : "02   CORAL   위험물 / 좌우 회피", small);
            GUI.Label(new Rect(width - 391, height - 230, 335, 27), game.IsCircuit ? "03   LINE     출발선 · 체크포인트" : "03   GOLD     표적 / SPACE 공격", small);
            GUI.Label(new Rect(width - 391, height - 179, 330, 24), "INSPIRED BY CHAIN-RUSH  /  DOSANGE", small);
        }

        private void DrawPlay(float width, float height, float scale)
        {
            NightUi.Block(new Rect(66, 163, 300, 4), Row);
            NightUi.Block(new Rect(66, 163, 300 * game.Progress, 4), Mint);
            if (!game.IsCircuit) GUI.Label(new Rect(66, 185, 460, 26), "ROUTE / " + Mathf.FloorToInt(game.Progress * 496f) + " M", small);
            NightUi.ShadowLabel(new Rect(width - 346, height - 302, 274, 124), player.HorizontalSpeed.ToString("00.0"), speedNumber);
            GUI.Label(new Rect(width - 150, height - 169, 90, 26), "M / S", small);
            int speedMarks = Mathf.CeilToInt(20 * Mathf.Clamp01(player.HorizontalSpeed / Mathf.Max(player.RunSpeed, player.SlingTopSpeed)));
            for (int i = 0; i < 20; i++) NightUi.Block(new Rect(width - 332 + i * 13, height - 125, 9, 5), i < speedMarks ? Mint : Row);
            DrawBoost(width, height);
            string status = grapple.IsAttached ? "LINKED / 놓으면 도약" : player.IsGrounded ? "RUN / 노란 선에서 점프" : "AIR / ALT 로 앵커 잡기";
            if (player.IsSwinging) status = "SWING / SHIFT 유지 · ←/→ 길이";
            if (grapple.JustMissed) status = "OUT OF RANGE  /  앵커에 더 가까이";
            NightUi.Block(new Rect(64, height - 185, 430, 63), Ink);
            NightUi.Block(new Rect(64, height - 185, 3, 63), Mint);
            GUI.Label(new Rect(84, height - 167, 400, 32), status, small);
            Transform anchor = grapple.Candidate;
            if (anchor != null)
            {
                Vector3 screen = viewCamera.WorldToScreenPoint(anchor.position);
                if (screen.z > 0f)
                {
                    float x = screen.x / scale;
                    float y = (Screen.height - screen.y) / scale;
                    Color color = grapple.IsAttached ? Color.white : Mint;
                    NightUi.Block(new Rect(x - 26, y - 26, 15, 3), color);
                    NightUi.Block(new Rect(x - 26, y - 26, 3, 15), color);
                    NightUi.Block(new Rect(x + 11, y + 23, 15, 3), color);
                    NightUi.Block(new Rect(x + 23, y + 11, 3, 15), color);
                    GUI.Label(new Rect(x + 34, y - 10, 170, 25), "LINK / " + Vector3.Distance(player.transform.position, anchor.position).ToString("0") + "m", small);
                }
            }
            if (grapple.HasPoleCandidate) DrawPoleCandidate(scale);
            if (game.DamageFlash)
            {
                Color flash = Coral;
                flash.a = Mathf.PingPong(Time.time * 2f, 0.25f);
                NightUi.Block(new Rect(0, 0, 9, height), flash);
                NightUi.Block(new Rect(width - 9, 0, 9, height), flash);
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
            NightUi.Block(new Rect(x - 26, y - 26, 15, 3), color);
            NightUi.Block(new Rect(x - 26, y - 26, 3, 15), color);
            NightUi.Block(new Rect(x + 11, y + 23, 15, 3), color);
            NightUi.Block(new Rect(x + 23, y + 11, 3, 15), color);
            GUI.Label(new Rect(x + 34, y - 10, 200, 25), "POLE / SHIFT  " + Vector3.Distance(player.transform.position, top).ToString("0") + "m", small);
        }

        // Booster gauge at the bottom middle, above the status line (KartRider style): the bar fills toward the next booster
        // as the runner swings, and each whole gauge slot is a booster waiting in an item box beside it, spent by Ctrl.
        // Only how the gauge is shown: the slots are still RacerState.Gauge's whole part, its fraction the bar.
        private void DrawBoost(float width, float height)
        {
            const float BarWidth = 300f, Box = 48f, Gap = 8f;
            int boxes = (int)RacerState.MaxGauge;
            float left = width / 2f - (BarWidth + boxes * (Gap + Box)) / 2f;
            float top = height - 184f;
            float gauge = player.Gauge;
            int boosters = Mathf.Min(boxes, Mathf.FloorToInt(gauge));
            bool full = boosters >= boxes;
            float fill = full ? 1f : gauge - boosters;
            NightUi.Block(new Rect(left, top, BarWidth, Box), Ink);
            GUI.Label(new Rect(left + 18, top + 7, 220, 20), "BOOST  /  CTRL", small);
            GUI.Label(new Rect(left + BarWidth - 78, top + 7, 60, 20), full ? "MAX" : Mathf.FloorToInt(fill * 100f) + "%", small);
            var bar = new Rect(left + 18, top + 32, BarWidth - 36, 8);
            NightUi.Block(bar, Row);
            bar.width *= fill;
            NightUi.Block(bar, Mint);
            for (int i = 0; i < boxes; i++)
            {
                var slot = new Rect(left + BarWidth + Gap + i * (Gap + Box), top, Box, Box);
                NightUi.Block(slot, Ink);
                var item = new Rect(slot.x + 6, slot.y + 6, Box - 12, Box - 12);
                NightUi.Block(item, i < boosters ? Mint : Row);
                if (i < boosters) GUI.Label(item, "▶▶", button);
            }
        }

        private void DrawResult(float width, float height)
        {
            NightUi.Backdrop(nightBackground, width, height);
            GUI.Label(new Rect(64, 35, 500, 56), "CHAIN RUSH", heading);
            float left = width * .06f, x = width * .53f, y = height * .22f;
            float panelWidth = width * .41f;
            GUI.Label(new Rect(left, y + 10, width * .45f, 142), game.HasFinished ? "RACE" : "SIGNAL", raceWord);
            GUI.Label(new Rect(left, y + 128, width * .45f, 142), game.HasFinished ? "FINISH" : "LOST", accentWord);
            bool compared = game.HasFinished && FromLobby && assembler.HasLastFinish;
            GUI.Label(new Rect(left + 4, y + 294, width * .40f, 80), !game.HasFinished ? "다시 도전하세요. 점프 후 앵커를 잡으세요."
                : compared && assembler.LastFinish.IsNewRunnerBest ? "새 기록! 고스트로 저장했습니다."
                : "결승선 도착. 다음 기록에 도전하세요.", small);
            NightUi.Surface(new Rect(x, y, panelWidth, 520));
            NightUi.Block(new Rect(x, y, panelWidth, 3), game.HasFailed ? Coral : Mint);
            GUI.Label(new Rect(x + 34, y + 30, panelWidth - 68, 30), "TOTAL TIME / RACE RESULT", small);
            float total = game.IsCircuit && game.Circuit.Laps.IsFinished ? Ticks.ToSeconds(game.Circuit.Laps.TotalTicks) : game.Elapsed;
            GUI.Label(new Rect(x + 30, y + 76, panelWidth - 60, 112), FormatTime(total), totalTime);
            if (game.IsCircuit)
            {
                var laps = game.Circuit.Laps;
                for (int lap = 1; lap <= laps.CompletedLaps; lap++)
                {
                    float row = y + 210 + (lap - 1) * 50;
                    NightUi.Block(new Rect(x + 34, row - 7, panelWidth - 68, 1), Row);
                    GUI.Label(new Rect(x + 34, row, 180, 34), "LAP 0" + lap, body);
                    GUI.Label(new Rect(x + panelWidth - 200, row, 166, 34), FormatTime(Ticks.ToSeconds(laps.LapTicks(lap))), body);
                }
            }
            else GUI.Label(new Rect(x + 34, y + 222, panelWidth - 68, 38), "LINKS " + game.Grapples + "   HITS " + game.Hits, body);
            if (compared) DrawComparison(assembler.LastFinish, x + 34, y + 340, panelWidth - 68);
            bool again = false, lobby = false;
            if (game.ManualStart)
            {
                float actionWidth = FromLobby ? (panelWidth - 82) / 2 : panelWidth - 68;
                Rect action = new Rect(x + 34, y + 405, actionWidth, 72);
                again = NightUi.CardButton(action, "다시 달리기", button, NightUi.CardLook.Action);
                if (FromLobby)
                {
                    var back = new Rect(action.xMax + 14, action.y, actionWidth, action.height);
                    lobby = NightUi.CardButton(back, "ESC / 로비로", pauseItem);
                }
            }
            DrawControls(width, height);
            if (lobby)
            {
                UiSound.Play(UiSound.Cue.Back);
                assembler.ReturnToLobby();
            }
            else if (again)
            {
                UiSound.Play(UiSound.Cue.Confirm);
                game.StartCountdown();
            }
        }

        // Below the laps (three at most end at y + 344 of the panel) and above the buttons: this finish against the
        // runner's best and the track's best as they were before it, the difference in mint when it beat them (M4-2).
        private void DrawComparison(Flow.FinishComparison finish, float x, float y, float width)
        {
            NightUi.Block(new Rect(x, y - 2, width, 1), Row);
            GUI.Label(new Rect(x, y + 4, width, 26), finish.RunnerLine, compare);
            GUI.Label(new Rect(x, y + 4, width, 26), finish.RunnerDelta, finish.IsNewRunnerBest ? compareNew : compareDelta);
            GUI.Label(new Rect(x, y + 32, width, 26), finish.BestLine, compare);
            GUI.Label(new Rect(x, y + 32, width, 26), finish.BestDelta, finish.IsNewBest ? compareNew : compareDelta);
        }

        // The pause menu over the dimmed game: the race so far and the entries this race offers, the highlighted one in
        // mint. A click chooses after the rows are drawn, as the lobby does (choosing changes what is drawn).
        private void DrawPause(float width, float height)
        {
            NightUi.Block(new Rect(0, 0, width, height), Dim);
            float x = width / 2f - 260;
            float y = height / 2f - 230;
            NightUi.Block(new Rect(x, y, 520, 460), Ink);
            NightUi.Block(new Rect(x, y, 520, 5), Mint);
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
                if (NightUi.CardButton(row, PauseLabels[i], highlighted ? button : pauseItem,
                    highlighted ? NightUi.CardLook.Action : NightUi.CardLook.Plain)) clicked = i;
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
            if (displayFont != null) Destroy(displayFont);
        }
    }
}
