using UnityEngine;

namespace ProtoHarness.ChainRush
{
    public sealed class ChainRushHud : MonoBehaviour
    {
        [SerializeField] private ChainRushGame game;
        [SerializeField] private RunnerMotor player;
        [SerializeField] private GrappleController grapple;
        [SerializeField] private Camera viewCamera;
        private GUIStyle small;
        private GUIStyle body;
        private GUIStyle heading;
        private GUIStyle title;
        private GUIStyle number;
        private GUIStyle button;
        private Font font;
        private static readonly Color Ink = new Color(0.025f, 0.055f, 0.08f, 0.94f);
        private static readonly Color Mint = new Color(0.43f, 1f, 0.8f);
        private static readonly Color Muted = new Color(0.58f, 0.71f, 0.75f);
        private static readonly Color Coral = new Color(1f, 0.36f, 0.3f);

        private void Awake()
        {
            if (game == null || player == null || grapple == null || viewCamera == null)
            {
                Debug.LogError("ChainRushHud: game, player, grapple and camera are required.", this);
                enabled = false;
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
            if (game.IsReady) DrawReady(width, height);
            else if (game.IsPaused || game.HasFailed || game.HasFinished) DrawResult(width, height);
            else DrawPlay(width, height, scale);
            GUI.matrix = previous;
        }

        private void DrawHeader(float width)
        {
            Block(new Rect(32, 26, 310, 74), Ink);
            Block(new Rect(32, 26, 5, 74), Mint);
            GUI.Label(new Rect(53, 37, 270, 38), "CHAIN / RUSH", heading);
            GUI.Label(new Rect(55, 78, 280, 20), game.IsEndless ? "03D    /    ENDLESS PURSUIT" : "03D    /    SKYLINE TRIAL", small);
            Block(new Rect(width - 382, 26, 350, 74), Ink);
            GUI.Label(new Rect(width - 359, 37, 155, 22), "SUIT INTEGRITY", small);
            for (int i = 0; i < 3; i++)
                Block(new Rect(width - 356 + i * 49, 68, 37, 10), i < game.Health ? Mint : new Color(0.17f, 0.23f, 0.26f));
            GUI.Label(new Rect(width - 177, 43, 130, 45), game.Elapsed.ToString("00.0") + "s", heading);
        }

        private void DrawControls(float width, float height)
        {
            Block(new Rect(32, height - 92, width - 64, 60), Ink);
            GUI.Label(new Rect(55, height - 75, 145, 30), "A / D   이동", body);
            GUI.Label(new Rect(232, height - 75, 390, 30), "좌클릭   점프 → 공중에서 다시 잡기", body);
            GUI.Label(new Rect(650, height - 75, 270, 30), "유지  스윙 / 놓기  해제", body);
            GUI.Label(new Rect(951, height - 75, 210, 30), "SPACE   공격", body);
            GUI.Label(new Rect(width - 405, height - 75, 390, 30), game.HasPresentation ? "R  재시작   ESC  정지   M  음소거" : "R  재시작     ESC  일시정지", body);
        }

        private void DrawReady(float width, float height)
        {
            Block(new Rect(32, 128, 750, height - 248), Ink);
            GUI.Label(new Rect(69, 169, 640, 26), game.IsEndless ? "FIELD TEST  /  002                         ENDLESS COMBAT" : "FIELD TEST  /  001                         MOVEMENT PROTOTYPE", small);
            GUI.Label(new Rect(62, 222, 660, 130), "CHAIN", title);
            GUI.Label(new Rect(62, 326, 660, 130), "THE SKY.", title);
            Block(new Rect(70, 470, 65, 4), Mint);
            GUI.Label(new Rect(70, 500, 640, 37), "달리고, 걸고, 도약하세요.", heading);
            GUI.Label(new Rect(70, 551, 655, 35), game.IsEndless ? "끝없는 옥상을 달리며 침입 드론을 체인으로 격파하세요." : "공중의 앵커를 이어 타고 496m 결승선에 도달하세요.", body);
            GUI.Label(new Rect(70, 590, 655, 30), "노란 선에서 점프 → 공중에서 다시 클릭하고 유지", body);
            Rect start = new Rect(70, height - 240, 370, 68);
            Block(start, Mint);
            if (GUI.Button(start, "ENTER   /   START RUN  →", button)) game.StartRun();
            GUI.Label(new Rect(470, height - 218, 240, 24), game.IsEndless ? "무한 생존  ·  체력 3칸" : "약 50초  ·  체력 3칸", small);

            Block(new Rect(width - 420, height - 390, 388, 255), Ink);
            GUI.Label(new Rect(width - 391, height - 361, 320, 30), "READ THE COURSE", body);
            GUI.Label(new Rect(width - 391, height - 308, 335, 27), "01   MINT      공중 그래플 앵커", small);
            GUI.Label(new Rect(width - 391, height - 269, 335, 27), game.IsEndless ? "02   경고      위 / 왼쪽 / 오른쪽 진입" : "02   CORAL   위험물 / 좌우 회피", small);
            GUI.Label(new Rect(width - 391, height - 230, 335, 27), game.IsEndless ? "03   SPACE   조준 표시 후 1.2초 이내" : "03   GOLD     표적 / SPACE 공격", small);
            GUI.Label(new Rect(width - 391, height - 179, 330, 24), "INSPIRED BY CHAIN-RUSH  /  DOSANGE", small);
        }

        private void DrawPlay(float width, float height, float scale)
        {
            Block(new Rect(width / 2f - 210, 34, 420, 62), Ink);
            GUI.Label(new Rect(width / 2f - 184, 42, 380, 24), game.IsEndless ? "ENDLESS / " + game.Distance.ToString("0") + " M     HITS / " + game.Hits : "ROUTE  /  " + Mathf.FloorToInt(game.Progress * 496f) + " M   →   496 M", small);
            Block(new Rect(width / 2f - 184, 78, 368, 4), new Color(0.2f, 0.3f, 0.33f));
            Block(new Rect(width / 2f - 184, 78, 368 * (game.IsEndless ? (float)(game.Distance % 500d / 500d) : game.Progress), 4), Mint);
            if (game.IsEndless) DrawEncounter(width, scale);
            Block(new Rect(32, height - 220, 168, 105), Ink);
            GUI.Label(new Rect(54, height - 207, 140, 20), "VELOCITY / M·S", small);
            GUI.Label(new Rect(52, height - 180, 140, 50), player.Speed.ToString("00.0"), number);
            DrawGauge(height);
            string status = grapple.IsAttached ? "LINKED  /  놓으면 도약" : player.IsGrounded ? "RUN  /  노란 선에서 점프" : "AIR  /  좌클릭으로 앵커 잡기";
            if (player.IsDrifting) status = "DRIFT  /  미끄러지며 체인 게이지 충전";
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
            if (game.DamageFlash)
            {
                Color flash = Coral;
                flash.a = Mathf.PingPong(Time.time * 2f, 0.25f);
                Block(new Rect(0, 0, 9, height), flash);
                Block(new Rect(width - 9, 0, 9, height), flash);
            }
        }

        // Chain gauge under the speed box: one bar per slot, filled by drifting, spent by Ctrl.
        private void DrawGauge(float height)
        {
            Block(new Rect(32, height - 108, 168, 46), Ink);
            GUI.Label(new Rect(54, height - 104, 140, 20), "CHAIN  /  CTRL", small);
            float gauge = player.Gauge;
            for (int slot = 0; slot < (int)RacerState.MaxGauge; slot++)
            {
                var bar = new Rect(54 + slot * 64, height - 80, 58, 8);
                Block(bar, new Color(0.2f, 0.3f, 0.33f));
                bar.width *= Mathf.Clamp01(gauge - slot);
                Block(bar, Mint);
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
            GUI.Label(new Rect(x + 42, y + 34, 600, 25), "SKYLINE TRIAL / MISSION STATUS", small);
            string label = game.IsPaused ? "PAUSED" : game.HasFinished ? "ROUTE COMPLETE." : "SIGNAL LOST.";
            GUI.Label(new Rect(x + 40, y + 84, 610, 54), label, number);
            string subtitle = game.IsPaused ? "잠시 쉬어가세요. ESC로 계속합니다." : game.HasFinished ? "결승선 도착. 다음 기록에 도전하세요." : "다시 도전하세요. 노란 선에서 점프 후 앵커를 잡으세요.";
            GUI.Label(new Rect(x + 42, y + 147, 610, 35), subtitle, body);
            GUI.Label(new Rect(x + 42, y + 207, 600, 34), "TIME   " + game.Elapsed.ToString("0.0") + "s      LINKS   " + game.Grapples + "      HITS   " + game.Hits, body);
            Rect action = new Rect(x + 42, y + 283, 586, 66);
            Block(action, Mint);
            if (GUI.Button(action, game.IsPaused ? "ESC  /  CONTINUE  →" : "R  /  RUN AGAIN  →", button))
            {
                if (game.IsPaused) game.TogglePause(); else game.StartRun();
            }
        }

        private void OnDestroy()
        {
            if (font != null) Destroy(font);
        }
    }
}
