using System;
using ProtoHarness.ChainRush.Track;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProtoHarness.ChainRush.Flow
{
    // The lobby: main menu (single race / multi, locked / settings), then for a single race the track, the speed class
    // and the runner, then RaceLaunch and the race scene. ↑/↓ choose, Enter confirms, ESC goes back (from the main menu,
    // to the title). Locked entries are shown but refuse with a notice. Drawn like ChainRushHud (OnGUI, 1600 x 900).
    public sealed class LobbyMenu : MonoBehaviour
    {
        public enum Page { Main, Settings, Track, Speed, Runner }

        // One track the lobby offers: what the circuit is built from and the scene loaded around it for looks.
        [Serializable]
        public struct TrackChoice
        {
            [SerializeField] private string title;
            [SerializeField] private TrackDefinition definition;
            [Tooltip("Scene loaded additively around the race for looks; empty for none. It must be in the build's scene list.")]
            [SerializeField] private string environmentScene;

            public string Title => title;
            public TrackDefinition Definition => definition;
            public string EnvironmentScene => environmentScene;
        }

        private const int MainSingle = 0, MainMulti = 1, MainSettings = 2;
        private static readonly string[] MainOptions = { "싱글 레이스", "멀티 레이스", "설정" };
        private static readonly bool[] MainLocked = { false, true, false };
        private static readonly SpeedClass[] Speeds = { SpeedClass.Normal, SpeedClass.Fast, SpeedClass.VeryFast };
        private static readonly string[] SpeedOptions = { "보통", "빠름", "매우빠름" };
        private static readonly bool[] SpeedLocked = { true, false, true };
        private static readonly string[] StatNames = { "최고 속도", "가속", "게이지 충전", "무게" };
        // A stat bar is full at this scale; the runners differ by a few percent, so the bars start from StatBarFloor.
        private const float StatBarFloor = 0.7f, StatBarCeiling = 1.3f;
        private static readonly string[] NoOptions = new string[0];
        private static readonly bool[] NoLocks = new bool[0];
        private static readonly string[] PageTitles = { "MAIN MENU", "SETTINGS", "TRACK", "SPEED", "RUNNER" };
        private static readonly string[] Keys =
        {
            "↑   가속",
            "← / →   조향",
            "왼쪽 ALT   점프 · 공중에서 다시 누르고 유지하면 그래플",
            "ALT 떼기 / ↓   그래플 해제",
            "왼쪽 SHIFT 유지   코너 봉 스윙",
            "왼쪽 CTRL   부스터 (게이지 1칸)",
            "SPACE   공격",
            "R   재시작      ESC   일시정지",
        };
        private static readonly Color Ink = new Color(0.025f, 0.055f, 0.08f, 0.94f);
        private static readonly Color Mint = new Color(0.43f, 1f, 0.8f);
        private static readonly Color Muted = new Color(0.58f, 0.71f, 0.75f);
        private static readonly Color Slate = new Color(0.2f, 0.3f, 0.33f);

        [SerializeField] private TrackChoice[] tracks;
        [SerializeField] private RunnerProfile[] runners;
        private string[] trackOptions;
        private bool[] trackLocked;
        // Per track, its best record at the chosen speed class (RefreshRecords).
        private string[] trackRecords;
        private BestRecords records = BestRecords.Player;
        private string[] runnerOptions;
        private bool[] runnerLocked;
        // Per runner: the four stats (StatNames order) and their "x1.03" labels, made once.
        private float[,] runnerStats;
        private string[,] runnerStatText;
        private Page page;
        private int cursor;
        private int track;
        // Starts on Fast, the only class unlocked (Speeds[1]).
        private int speed = 1;
        private int runner;
        private string notice = "";
        private bool leaving;
        private GUIStyle small;
        private GUIStyle body;
        private GUIStyle heading;
        private GUIStyle item;
        private GUIStyle lockedItem;
        private GUIStyle recordLine;
        private Font font;

        // Where the track page reads best records from (the player's; a test gives its own).
        public BestRecords Records
        {
            get => records;
            set
            {
                records = value ?? throw new ArgumentNullException(nameof(value));
                if (trackRecords != null) RefreshRecords();
            }
        }
        public Page CurrentPage => page;
        // The track page's record line for a track: "최고 기록  01:19.2 · BETA", or "기록 없음".
        public string TrackRecordText(int index) => trackRecords[index];
        public int Cursor => cursor;
        // The last refusal (a locked entry), shown under the list; empty when there is none.
        public string Notice => notice;

        private void Awake()
        {
            if (tracks == null || tracks.Length == 0)
            {
                Debug.LogError("LobbyMenu: at least one track is required.", this);
                enabled = false;
                return;
            }
            trackOptions = new string[tracks.Length];
            trackLocked = new bool[tracks.Length];
            trackRecords = new string[tracks.Length];
            for (int i = 0; i < tracks.Length; i++)
            {
                if (tracks[i].Definition == null || string.IsNullOrEmpty(tracks[i].Title) || tracks[i].EnvironmentScene == null)
                {
                    Debug.LogError($"LobbyMenu: track {i} needs a title and a definition.", this);
                    enabled = false;
                    return;
                }
                trackOptions[i] = tracks[i].Title;
            }
            if (runners == null || runners.Length == 0)
            {
                Debug.LogError("LobbyMenu: at least one runner is required.", this);
                enabled = false;
                return;
            }
            runnerOptions = new string[runners.Length];
            runnerLocked = new bool[runners.Length];
            runnerStats = new float[runners.Length, StatNames.Length];
            runnerStatText = new string[runners.Length, StatNames.Length];
            for (int i = 0; i < runners.Length; i++)
            {
                if (runners[i] == null || !runners[i].TryValidate(out string error))
                {
                    Debug.LogError($"LobbyMenu: runner {i} is missing or invalid.", this);
                    enabled = false;
                    return;
                }
                runnerOptions[i] = runners[i].DisplayName;
                float[] stats = { runners[i].TopSpeedScale, runners[i].AccelerationScale, runners[i].GaugeChargeScale, runners[i].Weight };
                for (int s = 0; s < stats.Length; s++)
                {
                    runnerStats[i, s] = stats[s];
                    runnerStatText[i, s] = "×" + stats[s].ToString("0.00");
                }
            }
            RefreshRecords();
        }

        // Reads the best records again: on waking (a race may have just set one) and whenever the track page opens.
        private void RefreshRecords()
        {
            for (int i = 0; i < tracks.Length; i++)
                trackRecords[i] = records.TryGet(tracks[i].Definition.name, Speeds[speed], out BestRecords.Record best)
                    ? "최고 기록  " + ChainRushHud.FormatTime(Ticks.ToSeconds(best.Ticks)) + "  ·  " + best.Runner
                    : "기록 없음";
        }

        private void OnValidate()
        {
            if (tracks != null)
                for (int i = 0; i < tracks.Length; i++)
                    if (tracks[i].Definition == null || string.IsNullOrEmpty(tracks[i].Title))
                        Debug.LogError($"LobbyMenu: track {i} needs a title and a definition.", this);
            if (runners != null)
                for (int i = 0; i < runners.Length; i++)
                    if (runners[i] == null) Debug.LogError($"LobbyMenu: runner {i} is missing.", this);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.upArrowKey.wasPressedThisFrame) Move(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame) Move(1);
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) Confirm();
            else if (keyboard.escapeKey.wasPressedThisFrame) Back();
        }

        // Moves the highlight, wrapping around the list.
        public void Move(int delta)
        {
            int count = Options(page).Length;
            if (count == 0) return;
            cursor = ((cursor + delta) % count + count) % count;
            notice = "";
        }

        // Chooses the highlighted entry. On the runner page this starts the race.
        public void Confirm()
        {
            if (!enabled || leaving) return;
            if (Locked(page).Length > 0 && Locked(page)[cursor])
            {
                notice = page == Page.Main ? "멀티는 M7 에서 열립니다." : "다른 속도 단계는 M2-b 에서 열립니다.";
                return;
            }
            notice = "";
            switch (page)
            {
                case Page.Main:
                    if (cursor == MainSingle) Open(Page.Track, track);
                    else if (cursor == MainSettings) Open(Page.Settings, 0);
                    break;
                case Page.Settings:
                    Open(Page.Main, MainSettings);
                    break;
                case Page.Track:
                    track = cursor;
                    Open(Page.Speed, speed);
                    break;
                case Page.Speed:
                    speed = cursor;
                    Open(Page.Runner, runner);
                    break;
                case Page.Runner:
                    runner = cursor;
                    Launch();
                    break;
            }
        }

        // One page back; from the main menu, back to the title.
        public void Back()
        {
            if (!enabled || leaving) return;
            notice = "";
            switch (page)
            {
                case Page.Main:
                    leaving = true;
                    SceneManager.LoadScene(RaceLaunch.TitleScene);
                    break;
                case Page.Settings: Open(Page.Main, MainSettings); break;
                case Page.Track: Open(Page.Main, MainSingle); break;
                case Page.Speed: Open(Page.Track, track); break;
                case Page.Runner: Open(Page.Speed, speed); break;
            }
        }

        // The race the current choices make (track, speed class and runner as last confirmed).
        public RaceSetup BuildSetup() =>
            new RaceSetup(tracks[track].Definition, tracks[track].EnvironmentScene, RaceMode.Single, Speeds[speed], runners[runner]);

        private void Launch()
        {
            RaceLaunch.Request(BuildSetup());
            leaving = true;
            SceneManager.LoadScene(RaceLaunch.RaceScene);
        }

        private void Open(Page next, int highlight)
        {
            page = next;
            cursor = highlight;
            if (next == Page.Track) RefreshRecords();
        }

        private string[] Options(Page of) =>
            of == Page.Main ? MainOptions : of == Page.Track ? trackOptions : of == Page.Speed ? SpeedOptions : of == Page.Runner ? runnerOptions : NoOptions;

        private bool[] Locked(Page of) =>
            of == Page.Main ? MainLocked : of == Page.Track ? trackLocked : of == Page.Speed ? SpeedLocked : of == Page.Runner ? runnerLocked : NoLocks;

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
                Debug.LogError("LobbyMenu: could not create the font.", this);
                enabled = false;
                return;
            }
            small = Style(15, Muted);
            body = Style(21, Color.white);
            heading = Style(32, Color.white, FontStyle.Bold);
            item = Style(26, Color.white, FontStyle.Bold);
            item.alignment = TextAnchor.MiddleLeft;
            lockedItem = Style(26, Muted);
            lockedItem.alignment = TextAnchor.MiddleLeft;
            recordLine = Style(18, Muted);
            recordLine.alignment = TextAnchor.MiddleRight;
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
            Block(new Rect(0, 0, width, height), new Color(0.04f, 0.07f, 0.1f, 1f));
            Block(new Rect(32, 26, 310, 74), Ink);
            Block(new Rect(32, 26, 5, 74), Mint);
            GUI.Label(new Rect(53, 37, 270, 38), "CHAIN / RUSH", heading);
            GUI.Label(new Rect(55, 78, 280, 20), "LOBBY    /    " + PageTitles[(int)page], small);

            Block(new Rect(32, 128, 700, height - 248), Ink);
            if (page == Page.Settings) DrawKeys();
            else DrawList();
            GUI.Label(new Rect(70, height - 170, 640, 26), notice, body);
            GUI.Label(new Rect(70, height - 92, 900, 24), "↑ / ↓  선택      ENTER  결정      ESC  뒤로", small);

            Block(new Rect(width - 452, 128, 420, 250), Ink);
            GUI.Label(new Rect(width - 420, 152, 360, 24), "RACE SETUP", small);
            GUI.Label(new Rect(width - 420, 196, 360, 30), "트랙     " + trackOptions[track], body);
            GUI.Label(new Rect(width - 420, 240, 360, 30), "속도     " + SpeedOptions[speed], body);
            GUI.Label(new Rect(width - 420, 284, 360, 30), "러너     " + runnerOptions[runner], body);
            GUI.Label(new Rect(width - 420, 328, 360, 24), "모드     싱글", small);
            if (page == Page.Runner) DrawRunnerStats(width, cursor);
            GUI.matrix = previous;
        }

        // The highlighted runner's stats as bars between StatBarFloor and StatBarCeiling, with their scales.
        private void DrawRunnerStats(float width, int shown)
        {
            Block(new Rect(width - 452, 398, 420, 270), Ink);
            // As tall as the 32 px bold heading needs, or its bottom is cut off (user 2026-10-19).
            GUI.Label(new Rect(width - 420, 416, 360, 44), runnerOptions[shown], heading);
            for (int s = 0; s < StatNames.Length; s++)
            {
                float y = 484 + s * 44;
                GUI.Label(new Rect(width - 420, y, 120, 24), StatNames[s], small);
                var bar = new Rect(width - 300, y + 6, 180, 8);
                Block(bar, Slate);
                bar.width *= Mathf.InverseLerp(StatBarFloor, StatBarCeiling, runnerStats[shown, s]);
                Block(bar, Mint);
                GUI.Label(new Rect(width - 106, y, 70, 24), runnerStatText[shown, s], small);
            }
        }

        private void DrawList()
        {
            string[] options = Options(page);
            bool[] locked = Locked(page);
            for (int i = 0; i < options.Length; i++)
            {
                var row = new Rect(70, 170 + i * 84, 620, 68);
                Block(row, i == cursor ? (locked[i] ? Slate : Mint) : new Color(0.08f, 0.13f, 0.16f, 1f));
                GUIStyle style = locked[i] ? lockedItem : item;
                Color text = style.normal.textColor;
                if (i == cursor && !locked[i]) style.normal.textColor = Ink;
                if (GUI.Button(new Rect(row.x + 26, row.y, row.width - 26, row.height), locked[i] ? options[i] + "   (잠김)" : options[i], style))
                {
                    cursor = i;
                    Confirm();
                }
                style.normal.textColor = text;
                if (page == Page.Track)
                {
                    recordLine.normal.textColor = i == cursor ? Ink : Muted;
                    GUI.Label(new Rect(row.x + 260, row.y, row.width - 284, row.height), trackRecords[i], recordLine);
                }
            }
        }

        private void DrawKeys()
        {
            GUI.Label(new Rect(70, 168, 600, 36), "조작 키", heading);
            for (int i = 0; i < Keys.Length; i++) GUI.Label(new Rect(70, 226 + i * 44, 620, 30), Keys[i], body);
        }

        private void OnDestroy()
        {
            if (font != null) Destroy(font);
        }
    }
}
