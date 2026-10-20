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
    // The settings page (M2-c) lists the volumes and the speed field of view, changed with ←/→, and opens the keys page.
    // The runner page shows the highlighted runner's look (RunnerPreview), turned by dragging the mouse across it; a click
    // on a runner only highlights it, and the enter button (or Enter) starts the race.
    public sealed class LobbyMenu : MonoBehaviour
    {
        public enum Page { Main, Settings, Track, Speed, Runner, Keys }

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
        private const int SettingMaster = 0, SettingMusic = 1, SettingEffects = 2, SettingSpeedView = 3, SettingKeys = 4;
        private static readonly string[] SettingsOptions = { "전체 볼륨", "음악", "효과음", "속도 시야 효과", "조작 키" };
        private static readonly bool[] SettingsLocked = { false, false, false, false, false };
        // A volume row's value, by level ("0" .. "10"), made once.
        private static readonly string[] LevelTexts = MakeLevelTexts();
        private static readonly SpeedClass[] Speeds = { SpeedClass.Normal, SpeedClass.Fast, SpeedClass.VeryFast };
        private static readonly string[] SpeedOptions = { "보통", "빠름", "매우빠름" };
        private static readonly bool[] SpeedLocked = { true, false, true };
        private static readonly string[] StatNames = { "최고 속도", "가속", "게이지 충전", "무게" };
        // A stat bar is full at this scale; the runners differ by a few percent, so the bars start from StatBarFloor.
        private const float StatBarFloor = 0.7f, StatBarCeiling = 1.3f;
        private static readonly string[] NoOptions = new string[0];
        private static readonly bool[] NoLocks = new bool[0];
        private static readonly string[] PageTitles = { "MAIN MENU", "SETTINGS", "TRACK", "SPEED", "RUNNER", "KEYS" };
        // The help line under the list; the settings page also names ←/→.
        private static readonly string Help = "↑ / ↓  선택      ENTER  결정      ESC  뒤로";
        private static readonly string SettingsHelp = "↑ / ↓  선택      ← / →  조절      ENTER  결정      ESC  뒤로";
        private static readonly string RunnerHelp = "↑ / ↓  선택      ENTER  결정      ESC  뒤로      마우스 드래그  회전";
        // The preview sits between the list and the right-hand panels, as tall as the stats panel's bottom; its texture is
        // drawn at twice the lobby's units.
        private const float PreviewWidth = 364f, PreviewTop = 128f, PreviewHeight = 540f;
        private const int PreviewTextureScale = 2;
        private static readonly string[] Keys =
        {
            "↑   가속",
            "← / →   조향",
            "왼쪽 ALT   점프 · 공중에서 다시 누르고 유지하면 그래플",
            "ALT 떼기 / ↓   그래플 해제",
            "왼쪽 SHIFT 유지   코너 봉 스윙",
            "왼쪽 CTRL   부스터 (게이지 1칸)",
            "SPACE   공격",
            "ESC   일시정지 (계속 · 재시작 · 로비)",
        };
        private static readonly Color Ink = new Color(0.025f, 0.055f, 0.08f, 0.94f);
        private static readonly Color Mint = new Color(0.43f, 1f, 0.8f);
        private static readonly Color Muted = new Color(0.58f, 0.71f, 0.75f);
        private static readonly Color Slate = new Color(0.2f, 0.3f, 0.33f);
        // Ink over the lobby's background, opaque, so the preview's texture matches the panels around it.
        private static readonly Color PreviewBackground = new Color(0.026f, 0.056f, 0.081f, 1f);

        [SerializeField] private TrackChoice[] tracks;
        [SerializeField] private RunnerProfile[] runners;
        private string[] trackOptions;
        private bool[] trackLocked;
        // Per track, its best record at the chosen speed class (RefreshRecords).
        private string[] trackRecords;
        private BestRecords records = BestRecords.Player;
        // Set in Awake, not here: GameSettings reads PlayerPrefs when it is made, which Unity refuses in a field initializer.
        private GameSettings settings;
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
        private RunnerPreview preview;
        // A drag that began on the preview turns it until the button is released.
        private bool dragging;
        private string notice = "";
        private bool leaving;
        private GUIStyle small;
        private GUIStyle body;
        private GUIStyle heading;
        private GUIStyle item;
        private GUIStyle lockedItem;
        private GUIStyle recordLine;
        private GUIStyle enterButton;
        private GUIStyle backButton;
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
        // Where the settings page reads and writes the settings (the player's; a test gives its own).
        public GameSettings Settings
        {
            get => settings;
            set => settings = value ?? throw new ArgumentNullException(nameof(value));
        }
        public Page CurrentPage => page;
        // The track page's record line for a track: "최고 기록  01:19.2 · BETA", or "기록 없음".
        public string TrackRecordText(int index) => trackRecords[index];
        public int Cursor => cursor;
        // The runner RACE SETUP names: on the runner page the highlighted one (a click only picks it since 2026-10-20),
        // elsewhere the one last entered with.
        public int SetupRunner => page == Page.Runner ? cursor : runner;
        // The runner page's preview (null if the lobby failed to wake).
        public RunnerPreview Preview => preview;
        // The last refusal (a locked entry), shown under the list; empty when there is none.
        public string Notice => notice;

        private void Awake()
        {
            if (settings == null) settings = GameSettings.Player;
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
            var looks = new GameObject[runners.Length];
            for (int i = 0; i < runners.Length; i++) looks[i] = runners[i].Look;
            try
            {
                preview = new RunnerPreview(looks, (int)PreviewWidth * PreviewTextureScale, (int)PreviewHeight * PreviewTextureScale, PreviewBackground);
            }
            catch (ArgumentException e)
            {
                // A look with nothing to draw: the lobby stops, as for any other invalid runner.
                Debug.LogException(e, this);
                enabled = false;
                return;
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
            var mouse = Mouse.current;
            if (mouse != null) DragPreview(mouse);
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.upArrowKey.wasPressedThisFrame) Move(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame) Move(1);
            else if (keyboard.leftArrowKey.wasPressedThisFrame) Adjust(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame) Adjust(1);
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) Confirm();
            else if (keyboard.escapeKey.wasPressedThisFrame) Back();
        }

        // Turns the preview while the left button, pressed on it, is held.
        private void DragPreview(Mouse mouse)
        {
            if (page != Page.Runner || leaving) dragging = false;
            else if (mouse.leftButton.wasPressedThisFrame) dragging = OverPreview(mouse.position.ReadValue());
            else if (!mouse.leftButton.isPressed) dragging = false;
            if (dragging) preview.Turn(mouse.delta.ReadValue().x / GuiScale());
        }

        // Moves the highlight, wrapping around the list.
        public void Move(int delta)
        {
            int count = Options(page).Length;
            if (count == 0) return;
            cursor = ((cursor + delta) % count + count) % count;
            notice = "";
            if (page == Page.Runner) preview.Show(cursor);
        }

        // Changes the highlighted setting: a volume by delta levels, stopping at 0 and MaxLevel (no wrapping); the speed
        // field of view switches whichever way. Nothing off the settings page or on the keys row.
        public void Adjust(int delta)
        {
            if (!enabled || leaving || page != Page.Settings) return;
            notice = "";
            switch (cursor)
            {
                case SettingMaster: settings.SetMasterLevel(Step(settings.MasterLevel, delta)); break;
                case SettingMusic: settings.SetMusicLevel(Step(settings.MusicLevel, delta)); break;
                case SettingEffects: settings.SetEffectsLevel(Step(settings.EffectsLevel, delta)); break;
                case SettingSpeedView: settings.SetSpeedFieldOfView(!settings.SpeedFieldOfView); break;
            }
        }

        private static int Step(int level, int delta) => Mathf.Clamp(level + delta, 0, GameSettings.MaxLevel);

        // Chooses the highlighted entry. On the runner page this starts the race. On the settings page a volume row
        // does nothing (←/→ change it), the speed field of view switches and the keys row opens the keys page.
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
                    if (cursor == SettingSpeedView) settings.SetSpeedFieldOfView(!settings.SpeedFieldOfView);
                    else if (cursor == SettingKeys) Open(Page.Keys, 0);
                    break;
                case Page.Keys:
                    Open(Page.Settings, SettingKeys);
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
                case Page.Keys: Open(Page.Settings, SettingKeys); break;
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
            if (next == Page.Runner) preview.Show(cursor);
            else preview.Hide();
        }

        // The lobby's units (1600 x 900) to screen pixels, as OnGUI scales them.
        private static float GuiScale() => Mathf.Min(Screen.width / 1600f, Screen.height / 900f);

        // The preview's left edge in the lobby's units: centred between the list (ends at 732) and the right-hand panels.
        private static float PreviewLeft(float width) => 732f + (width - 452f - 732f - PreviewWidth) * 0.5f;

        // Where the preview is drawn, in the lobby's units (OnGUI, y down).
        private static Rect PreviewRect(float width) => new Rect(PreviewLeft(width), PreviewTop, PreviewWidth, PreviewHeight);

        // Whether a mouse position (screen pixels, y up) is over the preview.
        private static bool OverPreview(Vector2 point)
        {
            float scale = GuiScale();
            float x = point.x / scale, y = (Screen.height - point.y) / scale;
            float left = PreviewLeft(Screen.width / scale);
            return x >= left && x < left + PreviewWidth && y >= PreviewTop && y < PreviewTop + PreviewHeight;
        }

        private string[] Options(Page of) =>
            of == Page.Main ? MainOptions : of == Page.Settings ? SettingsOptions : of == Page.Track ? trackOptions
            : of == Page.Speed ? SpeedOptions : of == Page.Runner ? runnerOptions : NoOptions;

        private bool[] Locked(Page of) =>
            of == Page.Main ? MainLocked : of == Page.Settings ? SettingsLocked : of == Page.Track ? trackLocked
            : of == Page.Speed ? SpeedLocked : of == Page.Runner ? runnerLocked : NoLocks;

        // A settings row's value: the level, or 켬 / 끔. The keys row has none.
        private string SettingValue(int row) =>
            row == SettingMaster ? LevelTexts[settings.MasterLevel] : row == SettingMusic ? LevelTexts[settings.MusicLevel]
            : row == SettingEffects ? LevelTexts[settings.EffectsLevel] : row == SettingSpeedView ? (settings.SpeedFieldOfView ? "켬" : "끔") : "";

        private static string[] MakeLevelTexts()
        {
            var texts = new string[GameSettings.MaxLevel + 1];
            for (int level = 0; level < texts.Length; level++) texts[level] = level.ToString();
            return texts;
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
            enterButton = Style(32, Ink, FontStyle.Bold);
            enterButton.alignment = TextAnchor.MiddleCenter;
            backButton = Style(22, Color.white, FontStyle.Bold);
            backButton.alignment = TextAnchor.MiddleCenter;
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
            float scale = GuiScale();
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            Block(new Rect(0, 0, width, height), new Color(0.04f, 0.07f, 0.1f, 1f));
            Block(new Rect(32, 26, 310, 74), Ink);
            Block(new Rect(32, 26, 5, 74), Mint);
            GUI.Label(new Rect(53, 37, 270, 38), "CHAIN / RUSH", heading);
            GUI.Label(new Rect(55, 78, 280, 20), "LOBBY    /    " + PageTitles[(int)page], small);
            // Beside the title on every page: ESC by mouse (user 2026-10-20), back one page or, from the main menu, to the title.
            var backRect = new Rect(362, 26, 200, 74);
            Block(backRect, Ink);
            Block(new Rect(backRect.x, backRect.yMax - 4, backRect.width, 4), Muted);
            bool back = GUI.Button(backRect, "←  이전   ESC", backButton);

            Block(new Rect(32, 128, 700, height - 248), Ink);
            if (page == Page.Keys) DrawKeys();
            else DrawList();
            GUI.Label(new Rect(70, height - 170, 640, 26), notice, body);
            GUI.Label(new Rect(70, height - 92, 900, 24), page == Page.Settings ? SettingsHelp : page == Page.Runner ? RunnerHelp : Help, small);

            Block(new Rect(width - 452, 128, 420, 250), Ink);
            GUI.Label(new Rect(width - 420, 152, 360, 24), "RACE SETUP", small);
            GUI.Label(new Rect(width - 420, 196, 360, 30), "트랙     " + trackOptions[track], body);
            GUI.Label(new Rect(width - 420, 240, 360, 30), "속도     " + SpeedOptions[speed], body);
            GUI.Label(new Rect(width - 420, 284, 360, 30), "러너     " + runnerOptions[SetupRunner], body);
            GUI.Label(new Rect(width - 420, 328, 360, 24), "모드     싱글", small);
            bool enter = false;
            if (page == Page.Runner)
            {
                DrawPreview(width, cursor);
                DrawRunnerStats(width, cursor);
                // Under the stats, down to the list panel's bottom: the one way into the race besides Enter.
                var start = new Rect(width - 452, 688, 420, height - 120 - 688);
                Block(start, Mint);
                enter = GUI.Button(start, "입장   ENTER  →", enterButton);
            }
            GUI.matrix = previous;
            // Acted on after drawing: either changes the page the rest of this draw would read.
            if (back) Back();
            else if (enter) Confirm();
        }

        // The highlighted runner's look as the preview draws it, or a line when the runner has none.
        private void DrawPreview(float width, int shown)
        {
            Rect rect = PreviewRect(width);
            Block(rect, Ink);
            if (preview.HasModel(shown)) GUI.DrawTexture(rect, preview.Texture, ScaleMode.StretchToFill, false);
            else GUI.Label(new Rect(rect.x + 24, rect.y + rect.height * 0.5f - 15, rect.width - 48, 30), "디자인 없음", body);
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
            // A click is confirmed after the list is drawn: confirming opens another page, and the rest of this loop
            // would index that page's arrays with this page's rows (a click on 싱글 레이스 threw, 2026-10-20).
            int clicked = -1;
            for (int i = 0; i < options.Length; i++)
            {
                var row = new Rect(70, 170 + i * 84, 620, 68);
                Block(row, i == cursor ? (locked[i] ? Slate : Mint) : new Color(0.08f, 0.13f, 0.16f, 1f));
                GUIStyle style = locked[i] ? lockedItem : item;
                Color text = style.normal.textColor;
                if (i == cursor && !locked[i]) style.normal.textColor = Ink;
                if (GUI.Button(new Rect(row.x + 26, row.y, row.width - 26, row.height), locked[i] ? options[i] + "   (잠김)" : options[i], style))
                    clicked = i;
                style.normal.textColor = text;
                if (page == Page.Track)
                {
                    recordLine.normal.textColor = i == cursor ? Ink : Muted;
                    GUI.Label(new Rect(row.x + 260, row.y, row.width - 284, row.height), trackRecords[i], recordLine);
                }
                else if (page == Page.Settings && i != SettingKeys)
                {
                    recordLine.normal.textColor = i == cursor ? Ink : Muted;
                    GUI.Label(new Rect(row.x + 260, row.y, row.width - 284, row.height), SettingValue(i), recordLine);
                }
            }
            if (clicked < 0) return;
            // On the runner page a click only picks the runner; the enter button (or Enter) starts the race (user 2026-10-20).
            if (page == Page.Runner) Highlight(clicked);
            else
            {
                cursor = clicked;
                Confirm();
            }
        }

        // Highlights a row without choosing it, as ↑/↓ would.
        public void Highlight(int row)
        {
            int count = Options(page).Length;
            if (row < 0 || row >= count)
                throw new ArgumentOutOfRangeException(nameof(row), row, $"LobbyMenu: the {page} page has {count} rows.");
            Move(row - cursor);
        }

        private void DrawKeys()
        {
            GUI.Label(new Rect(70, 168, 600, 36), "조작 키", heading);
            for (int i = 0; i < Keys.Length; i++) GUI.Label(new Rect(70, 226 + i * 44, 620, 30), Keys[i], body);
        }

        private void OnDestroy()
        {
            if (font != null) Destroy(font);
            preview?.Release();
        }
    }
}
