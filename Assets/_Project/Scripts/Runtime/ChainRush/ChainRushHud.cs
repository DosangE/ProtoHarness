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
        private StateEdge respawn;
        private StateEdge boost;
        private StateEdge hook;
        private StateEdge swing;
        private StateEdge miss;
        private StateEdge poleReady;
        private int spentBoostSlot = -1;
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
                return;
            }
            gameObject.AddComponent<ControlFeedback>().Initialize(game, player, grapple);
        }

        // How dark the respawn fade has the screen right now (RespawnLook): 1 the moment a fallen runner is put back.
        public float RespawnFade => RespawnLook.FadeAlpha(respawn.Since(Time.time));

        // These envelopes belong to rendered frames, not ticks, and never feed back into the simulation.
        public float BoostFlash => game.IsRunning ? FeedbackLook.Fade(boost.Since(Time.time), FeedbackLook.BoostFlashTime) : 0f;
        public float HookFlash => game.IsRunning ? Mathf.Max(
            FeedbackLook.Fade(hook.Since(Time.time), FeedbackLook.HookRingTime),
            FeedbackLook.Fade(swing.Since(Time.time), FeedbackLook.HookRingTime)) : 0f;

        private void Update()
        {
            respawn.Update(player.IsRespawning, Time.time);
            if (game.IsRunning)
            {
                float now = Time.time;
                if (boost.Update(player.IsSlingPulling || game.Racer.GrappleEmpowered, now))
                    spentBoostSlot = Mathf.FloorToInt(player.Gauge);
                hook.Update(grapple.IsAttached, now);
                swing.Update(player.IsSwinging, now);
                miss.Update(grapple.JustMissed, now);
                poleReady.Update(grapple.HasPoleCandidate, now);
            }
            else if (!game.IsPaused)
            {
                boost = hook = swing = miss = poleReady = default;
                spentBoostSlot = -1;
            }
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
            Transform anchor = grapple.Candidate;
            if (grapple.IsAttached || anchor != null)
            {
                Vector3 point = grapple.IsAttached ? grapple.AnchorPosition : anchor.position;
                Color color = Color.Lerp(Mint, Color.white, FeedbackLook.Fade(hook.Since(Time.time), FeedbackLook.HookRingTime));
                DrawWorldBracket(point, scale, FeedbackLook.BracketScale(hook.Since(Time.time)), color);
            }
            if (player.IsSwinging || grapple.HasPoleCandidate) DrawPoleCandidate(width, height, scale);
            DrawMiss(scale);
            DrawBoostFlash(width, height);
            if (game.DamageFlash)
            {
                Color flash = Coral;
                flash.a = Mathf.PingPong(Time.time * 2f, 0.25f);
                NightUi.Block(new Rect(0, 0, 9, height), flash);
                NightUi.Block(new Rect(width - 9, 0, 9, height), flash);
            }
        }

        // The offered pole pulses at its bite band; hooking it tightens the same bracket around the actual swing center.
        // A pole off the screen (to the side, or behind the camera) keeps its target pinned at the screen's edge toward
        // it, smaller, with an arrow pointing at it.
        private void DrawPoleCandidate(float width, float height, float scale)
        {
            bool swinging = player.IsSwinging;
            Vector3 pole = swinging ? game.Racer.SwingCenter : grapple.PoleCandidate;
            float since = swinging ? swing.Since(Time.time) : poleReady.Since(Time.time);
            float size = swinging ? FeedbackLook.BracketScale(since) : FeedbackLook.PoleScale(since);
            float flash = FeedbackLook.Fade(since, swinging ? FeedbackLook.HookRingTime : FeedbackLook.PoleEnterTime);
            Color color = Color.Lerp(Mint, Color.white, flash);
            float fill = swinging ? 0f : Mathf.Lerp(FeedbackLook.TargetFillMin, FeedbackLook.TargetFillMax, FeedbackLook.Pulse(since));
            Vector3 screen = viewCamera.WorldToScreenPoint(SwingLook.PoleBite(pole));
            var point = new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
            // Behind the camera the projection comes out mirrored through the screen's center.
            if (screen.z < 0f) point = new Vector2(width - point.x, height - point.y);
            if (screen.z > 0f && FeedbackLook.IsOnScreen(point, width, height))
            {
                DrawTarget(point.x, point.y, size, color, fill);
                return;
            }
            Vector2 pinned = FeedbackLook.PinToEdge(point, width, height, out Vector2 direction);
            float edgeSize = size * FeedbackLook.EdgeTargetScale;
            DrawTarget(pinned.x, pinned.y, edgeSize, color, fill);
            // Past the square's side in that direction, so the arrow never overlaps the target.
            float reach = PoleTargetHalf * edgeSize / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
            DrawArrow(pinned + direction * (reach + FeedbackLook.ArrowGap), direction, color);
        }

        private const float PoleTargetHalf = 30f;

        // The pole's target is bolder than an anchor's bracket (user 2026-10-29 "사각형 타겟 … 잘 보이게"): all four
        // corners, thicker, outlined in ink so it reads against bright scenery, over a faint fill that breathes with the
        // ring on the pole.
        private static void DrawTarget(float x, float y, float size, Color color, float fill)
        {
            const float Arm = 18f, Thick = 5f, Edge = 2f;
            float half = PoleTargetHalf * size, arm = Arm * size;
            Color inside = color;
            inside.a = fill;
            if (fill > 0f) NightUi.Block(new Rect(x - half, y - half, half * 2f, half * 2f), inside);
            DrawCorners(x, y, half + Edge, arm + Edge * 2f, Thick + Edge * 2f, Ink);
            DrawCorners(x, y, half, arm, Thick, color);
        }

        // An L at each corner of the square half from (x, y): arms arm long and thick wide, reaching inward.
        private static void DrawCorners(float x, float y, float half, float arm, float thick, Color color)
        {
            float left = x - half, top = y - half, right = x + half, bottom = y + half;
            NightUi.Block(new Rect(left, top, arm, thick), color);
            NightUi.Block(new Rect(left, top, thick, arm), color);
            NightUi.Block(new Rect(right - arm, top, arm, thick), color);
            NightUi.Block(new Rect(right - thick, top, thick, arm), color);
            NightUi.Block(new Rect(left, bottom - thick, arm, thick), color);
            NightUi.Block(new Rect(left, bottom - arm, thick, arm), color);
            NightUi.Block(new Rect(right - arm, bottom - thick, arm, thick), color);
            NightUi.Block(new Rect(right - thick, bottom - arm, thick, arm), color);
        }

        // A small triangle whose base is centered on at, pointing along direction (HUD coordinates, y down): columns of
        // blocks narrowing to the tip over a slightly larger one in ink, like the target's outline. Drawn in a turned
        // GUI matrix and put back after.
        private static void DrawArrow(Vector2 at, Vector2 direction, Color color)
        {
            const int Columns = 6;
            const float Column = 3f, Base = 26f, Step = 4f, Edge = 2f;
            Matrix4x4 previous = GUI.matrix;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GUI.matrix = previous * Matrix4x4.TRS(new Vector3(at.x, at.y, 0f), Quaternion.Euler(0f, 0f, angle), Vector3.one);
            for (int i = 0; i <= Columns; i++)
            {
                float tall = Base + Edge * 2f - i * Step;
                NightUi.Block(new Rect(i * Column - Edge, -tall * 0.5f, Column, tall), Ink);
            }
            for (int i = 0; i < Columns; i++)
            {
                float tall = Base - i * Step;
                NightUi.Block(new Rect(i * Column, -tall * 0.5f, Column, tall), color);
            }
            GUI.matrix = previous;
        }

        private void DrawWorldBracket(Vector3 point, float scale, float size, Color color)
        {
            Vector3 screen = viewCamera.WorldToScreenPoint(point);
            if (screen.z <= 0f) return;
            DrawBracket(screen.x / scale, (Screen.height - screen.y) / scale, size, color);
        }

        // Keep the four corner blocks, scaling their offsets and lengths together as a hook draws them inward.
        private static void DrawBracket(float x, float y, float size, Color color)
        {
            NightUi.Block(new Rect(x - 26 * size, y - 26 * size, 15 * size, 3), color);
            NightUi.Block(new Rect(x - 26 * size, y - 26 * size, 3, 15 * size), color);
            NightUi.Block(new Rect(x + 11 * size, y + 23 * size, 15 * size, 3), color);
            NightUi.Block(new Rect(x + 23 * size, y + 11 * size, 3, 15 * size), color);
        }

        private void DrawMiss(float scale)
        {
            float since = miss.Since(Time.time);
            float fade = FeedbackLook.Fade(since, FeedbackLook.MissShakeTime);
            if (fade <= 0f) return;
            Vector3 screen = grapple.Candidate != null ? viewCamera.WorldToScreenPoint(grapple.Candidate.position)
                : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 1f);
            if (screen.z <= 0f) return;
            Color color = Coral;
            color.a = fade;
            DrawBracket(screen.x / scale + FeedbackLook.ShakeOffset(since), (Screen.height - screen.y) / scale, 1f, color);
        }

        // Layered edge strips fall off toward the view, with short lines moving inward as the burst fades.
        private void DrawBoostFlash(float width, float height)
        {
            float fade = BoostFlash;
            if (fade <= 0f) return;
            for (int layer = 0; layer < 5; layer++)
            {
                Color color = Mint;
                color.a = fade * (0.3f - layer * 0.05f);
                float x = layer * 12f;
                NightUi.Block(new Rect(x, 0f, 12f, height), color);
                NightUi.Block(new Rect(width - x - 12f, 0f, 12f, height), color);
            }
            for (int i = 0; i < 7; i++)
            {
                Color color = Mint;
                color.a = fade * 0.7f;
                float x = 12f + (1f - fade) * (45f + i * 7f);
                float y = height * (0.18f + i * 0.1f);
                float length = (45f + (i % 3) * 18f) * fade;
                NightUi.Block(new Rect(x, y, length, 2f), color);
                NightUi.Block(new Rect(width - x - length, y, length, 2f), color);
            }
        }

        // Booster gauge at the bottom middle (KartRider style): the bar fills toward the next booster
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
            float burst = FeedbackLook.Fade(boost.Since(Time.time), FeedbackLook.GaugeBurstTime);
            for (int i = 0; i < boxes; i++)
            {
                var slot = new Rect(left + BarWidth + Gap + i * (Gap + Box), top, Box, Box);
                NightUi.Block(slot, Ink);
                var item = new Rect(slot.x + 6, slot.y + 6, Box - 12, Box - 12);
                NightUi.Block(item, i < boosters ? Mint : Row);
                if (i < boosters) GUI.Label(item, "▶▶", button);
                if (i == spentBoostSlot && burst > 0f)
                {
                    Color color = Color.Lerp(Mint, Color.white, burst);
                    color.a = burst;
                    float spread = (1f - burst) * 12f;
                    NightUi.Block(new Rect(item.x - spread, item.y - spread, item.width + spread * 2f, item.height + spread * 2f), color);
                }
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
                : compared && assembler.LastFinish.IsNewBest ? "새 기록! 고스트로 저장했습니다."
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
        // track's best at this speed as it was before it, the difference in mint when it beat it (M4-2).
        private void DrawComparison(Flow.FinishComparison finish, float x, float y, float width)
        {
            NightUi.Block(new Rect(x, y - 2, width, 1), Row);
            GUI.Label(new Rect(x, y + 4, width, 26), finish.BestLine, compare);
            GUI.Label(new Rect(x, y + 4, width, 26), finish.BestDelta, finish.IsNewBest ? compareNew : compareDelta);
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
