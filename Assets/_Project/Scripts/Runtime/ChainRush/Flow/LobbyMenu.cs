using System;
using ProtoHarness.ChainRush.Audio;
using ProtoHarness.ChainRush.Track;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using ProtoHarness.ChainRush.Visuals;

namespace ProtoHarness.ChainRush.Flow
{
    // The lobby: main menu (single race / multi, locked / settings), then for a single race the track, the speed class
    // and the runner, then RaceLaunch and the race scene. ↑/↓ choose, Enter confirms, ESC goes back (from the main menu,
    // to the title). Locked entries are shown but refuse with a notice. Drawn like ChainRushHud (OnGUI, 1600 x 900).
    // The settings page (M2-c) lists the volumes and the speed field of view, changed with ←/→, and opens the keys page.
    // The runner page shows the highlighted runner's look (RunnerPreview), turned by dragging the mouse across it; a click
    // on a runner only highlights it, and the enter button (or Enter) starts the race.
    // The main, track and speed pages describe the highlighted entry in a panel on the right (user 2026-10-21). The track
    // page also lists tracks still to come (PlaceholderTracks), locked. Rows and buttons are NightUi pressable cards; moving,
    // choosing, going back and a refusal each play their UiSound cue.
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
        private static readonly string[] MainDescriptions =
        {
            "혼자 코스를 달려 기록에 도전합니다.\n트랙과 속도, 러너를 골라 출발하세요.\n트랙과 속도마다 최고 기록이 남습니다.",
            "다른 러너들과 같은 코스에서 순위를 겨룹니다.\n멀티 레이스는 M7 에서 열립니다.",
            "볼륨과 속도 시야 효과를 조절하고\n조작 키를 확인합니다.",
        };
        // Tracks still to come, listed after the scene's tracks and locked; they have no definition yet.
        private static readonly string[] PlaceholderTracks = { "NEON HARBOR" };
        private static readonly string[] PlaceholderDescriptions =
        {
            "네온이 비치는 항구 부두를 도는 코스.\n준비 중인 트랙입니다.",
        };
        private const string PlaceholderRecord = "준비 중";
        private const int SettingMaster = 0, SettingMusic = 1, SettingEffects = 2, SettingSpeedView = 3, SettingKeys = 4;
        private static readonly string[] SettingsOptions = { "전체 볼륨", "음악", "효과음", "속도 시야 효과", "조작 키" };
        private static readonly bool[] SettingsLocked = { false, false, false, false, false };
        // A volume row's value, by level ("0" .. "10"), made once.
        private static readonly string[] LevelTexts = MakeLevelTexts();
        private static readonly SpeedClass[] Speeds = { SpeedClass.Normal, SpeedClass.Fast, SpeedClass.VeryFast };
        private static readonly string[] SpeedOptions = { "보통", "빠름", "매우빠름" };
        private static readonly bool[] SpeedLocked = { true, false, true };
        private static readonly string[] SpeedDescriptions =
        {
            "처음 달리는 러너를 위한 속도.\n코스를 익히기 좋습니다.\n아직 열리지 않았습니다.",
            "기본 레이스 속도.\n지금 달릴 수 있는 단계입니다.",
            "숙련자를 위한 가장 빠른 속도.\n아직 열리지 않았습니다.",
        };
        // The description panel's small heading, by page (Page order); the pages without the panel have none.
        private static readonly string[] DescriptionTags = { "MODE", "", "TRACK", "SPEED CLASS", "", "" };
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
        private const float PreviewWidth = 480f, PreviewTop = 165f, PreviewHeight = 480f;
        private const int PreviewTextureScale = 2;
        private static readonly string[] Keys =
        {
            "↑   가속 (스윙 중에도)      ↓ 유지   브레이크 · 후진",
            "← / →   조향",
            "왼쪽 ALT   점프 · 공중에서 다시 누르고 유지하면 그래플",
            "ALT 떼기 / ↓   그래플 해제",
            "왼쪽 SHIFT 유지   코너 봉 스윙",
            "왼쪽 CTRL   부스터 (게이지 1칸)",
            "SPACE   공격",
            "ESC   일시정지 (계속 · 재시작 · 로비)",
        };
        private static readonly Color Ink = NightUi.Background;
        private static readonly Color Mint = NightUi.Accent;
        private static readonly Color Muted = NightUi.Muted;
        private static readonly Color Slate = NightUi.Border;
        // Ink over the lobby's background, opaque, so the preview's texture matches the panels around it.
        private static readonly Color PreviewBackground = new Color(0, 0, 0, 0);

        [SerializeField] private TrackChoice[] tracks;
        [SerializeField] private RunnerProfile[] runners;
        [SerializeField] private Texture2D nightBackground;
        [SerializeField] private Texture2D runnerArtwork;
        // The scene's tracks, then PlaceholderTracks; a placeholder's index is past tracks' end.
        private string[] trackOptions;
        private bool[] trackLocked;
        private string[] trackDescriptions;
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
        private Font displayFont;
        private GUIStyle brand;
        private GUIStyle pageHeading;
        private GUIStyle tileLabel;
        private GUIStyle infoTitle;
        private GUIStyle infoBody;
        private GUIStyle lockTag;
        private GUIStyle helpLine;
        private int pendingRow = -1;

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
        // The runner the setup names: on the runner page the highlighted one (a click only picks it since 2026-10-20),
        // elsewhere the one last entered with. No longer drawn: the RACE SETUP panel gave way to the descriptions (2026-10-21).
        public int SetupRunner => page == Page.Runner ? cursor : runner;
        // The highlighted entry's description on the main, track and speed pages; empty on the others.
        public string Description
        {
            get
            {
                string[] descriptions = Descriptions(page);
                return cursor < descriptions.Length ? descriptions[cursor] : "";
            }
        }
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
            int trackCount = tracks.Length + PlaceholderTracks.Length;
            trackOptions = new string[trackCount];
            trackLocked = new bool[trackCount];
            trackRecords = new string[trackCount];
            trackDescriptions = new string[trackCount];
            for (int i = 0; i < tracks.Length; i++)
            {
                if (tracks[i].Definition == null || string.IsNullOrEmpty(tracks[i].Title) || tracks[i].EnvironmentScene == null)
                {
                    Debug.LogError($"LobbyMenu: track {i} needs a title and a definition.", this);
                    enabled = false;
                    return;
                }
                trackOptions[i] = tracks[i].Title;
                trackDescriptions[i] = TrackDescription(tracks[i].Title);
                // A track without a description still races; the empty panel is the loud part, with this error.
                if (trackDescriptions[i].Length == 0)
                    Debug.LogError($"LobbyMenu: track {i} ({tracks[i].Title}) has no description in TrackDescription.", this);
            }
            for (int p = 0; p < PlaceholderTracks.Length; p++)
            {
                int i = tracks.Length + p;
                trackOptions[i] = PlaceholderTracks[p];
                trackLocked[i] = true;
                trackRecords[i] = PlaceholderRecord;
                trackDescriptions[i] = PlaceholderDescriptions[p];
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

        // A scene track's description, by its title; empty for a title not written here.
        private static string TrackDescription(string title)
        {
            switch (title)
            {
                case "STADIUM": return "야간 조명 아래의 경기장 서킷.\n지금 달릴 수 있는 트랙입니다.";
                case "SKYWAY LOOP": return "마을을 지나 고가도로를 달리는 2바퀴 코스.\n긴 고가 직선 뒤 급한 내리막이 기다립니다.";
                default: return "";
            }
        }

        // Reads the best records again: on waking (a race may have just set one) and whenever the track page opens.
        // The placeholders keep PlaceholderRecord.
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
            int was = cursor;
            cursor = ((cursor + delta) % count + count) % count;
            if (cursor != was) UiSound.Play(UiSound.Cue.Move);
            notice = "";
            if (page == Page.Runner) preview.Show(cursor);
        }

        // Changes the highlighted setting: a volume by delta levels, stopping at 0 and MaxLevel (no wrapping); the speed
        // field of view switches whichever way. Nothing off the settings page or on the keys row.
        public void Adjust(int delta)
        {
            if (!enabled || leaving || page != Page.Settings) return;
            notice = "";
            if (cursor != SettingKeys) UiSound.Play(UiSound.Cue.Move);
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
                notice = page == Page.Main ? "멀티는 M7 에서 열립니다."
                    : page == Page.Track ? "새 트랙은 이후 마일스톤에서 열립니다." : "다른 속도 단계는 M2-b 에서 열립니다.";
                UiSound.Play(UiSound.Cue.Denied);
                return;
            }
            notice = "";
            UiSound.Play(UiSound.Cue.Confirm);
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
            UiSound.Play(UiSound.Cue.Back);
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
        private static float PreviewLeft(float width) => width * .27f;

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

        private string[] Descriptions(Page of) =>
            of == Page.Main ? MainDescriptions : of == Page.Track ? trackDescriptions : of == Page.Speed ? SpeedDescriptions : NoOptions;

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

        private GUIStyle Style(int size, Color color, FontStyle weight = FontStyle.Normal) => NightUi.Style(font, size, color, weight);

        private void InitializeStyles()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 24);
            displayFont = NightUi.DisplayFont();
            if (font == null || displayFont == null)
            {
                Debug.LogError("LobbyMenu: could not create the font.", this);
                enabled = false;
                return;
            }
            small = Style(15, Muted);
            body = Style(21, Color.white);
            heading = NightUi.Style(displayFont, 44, Color.white, FontStyle.BoldAndItalic);
            brand = NightUi.Style(displayFont, 34, Color.white, FontStyle.BoldAndItalic);
            pageHeading = NightUi.Style(displayFont, 52, Color.white, FontStyle.BoldAndItalic);
            tileLabel = NightUi.Style(displayFont, 23, Color.white, FontStyle.Bold);
            tileLabel.alignment = TextAnchor.MiddleCenter;
            item = Style(26, Color.white, FontStyle.Bold);
            item.alignment = TextAnchor.MiddleLeft;
            lockedItem = Style(26, Muted);
            lockedItem.alignment = TextAnchor.MiddleLeft;
            recordLine = Style(18, Muted);
            recordLine.alignment = TextAnchor.MiddleRight;
            enterButton = Style(28, Ink, FontStyle.Bold);
            enterButton.alignment = TextAnchor.MiddleCenter;
            backButton = Style(22, Color.white, FontStyle.Bold);
            backButton.alignment = TextAnchor.MiddleCenter;
            infoTitle = Style(38, Color.white, FontStyle.Bold);
            infoBody = Style(21, Color.white);
            infoBody.wordWrap = true;
            lockTag = NightUi.Style(displayFont, 16, NightUi.Action, FontStyle.Bold);
            lockTag.alignment = TextAnchor.MiddleRight;
            helpLine = Style(15, Muted);
            helpLine.alignment = TextAnchor.MiddleRight;
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
            NightUi.Backdrop(nightBackground, width, height);
            GUI.Label(new Rect(64, 34, 400, 54), "CHAIN RUSH", brand);
            GUI.Label(new Rect(64, 108, 900, 28), "SINGLE RACE  /  " + PageTitles[(int)page], small);
            GUI.Label(new Rect(61, 145, 1050, 68), page == Page.Runner ? "CHOOSE YOUR RUNNER" : PageTitles[(int)page], pageHeading);
            // Back sits bottom left, the key help bottom right on its line (user 2026-10-21).
            var backRect = new Rect(64, height - 84, 186, 48);
            bool back = NightUi.CardButton(backRect, "←  이전   ESC", backButton);
            pendingRow = -1;
            if (page == Page.Keys) DrawKeys();
            else DrawList(width, height);
            GUI.Label(new Rect(64, height - 140, 1000, 28), notice, body);
            GUI.Label(new Rect(width * .5f, height - 74, width * .5f - 64, 28), page == Page.Settings ? SettingsHelp : page == Page.Runner ? RunnerHelp : Help, helpLine);

            bool enter = false;
            if (page == Page.Runner)
            {
                DrawPreview(width, cursor);
                DrawRunnerStats(width, cursor);
                var start = new Rect(width - 484, height - 240, 420, 140);
                enter = NightUi.CardButton(start, "레이스 시작   ENTER  →", enterButton, NightUi.CardLook.Action);
            }
            else if (Descriptions(page).Length > 0) DrawDescription(width);
            GUI.matrix = previous;
            // Acted on after drawing: either changes the page the rest of this draw would read.
            if (back) Back();
            else if (enter) Confirm();
            else if (pendingRow >= 0)
            {
                if (page == Page.Runner) Highlight(pendingRow);
                else { cursor = pendingRow; Confirm(); }
            }
        }

        // The highlighted entry: its page's tag, its name, LOCKED when it is, its description and, for a track, its record.
        private void DrawDescription(float width)
        {
            var panel = new Rect(width - 620, 238, 556, 330);
            NightUi.PanelCard(panel);
            float left = panel.x + 34, inner = panel.width - 68;
            GUI.Label(new Rect(left, panel.y + 26, inner, 28), DescriptionTags[(int)page], small);
            if (Locked(page)[cursor]) GUI.Label(new Rect(left, panel.y + 26, inner, 28), "LOCKED  ·  준비 중", lockTag);
            GUI.Label(new Rect(left, panel.y + 62, inner, 52), Options(page)[cursor], infoTitle);
            NightUi.Block(new Rect(left, panel.y + 126, 48, 3), NightUi.Action);
            GUI.Label(new Rect(left, panel.y + 148, inner, 120), Descriptions(page)[cursor], infoBody);
            if (page == Page.Track)
            {
                recordLine.normal.textColor = Muted;
                recordLine.alignment = TextAnchor.MiddleLeft;
                GUI.Label(new Rect(left, panel.yMax - 54, inner, 28), trackRecords[cursor], recordLine);
                recordLine.alignment = TextAnchor.MiddleRight;
            }
        }

        // The highlighted runner's look as the preview draws it, or a line when the runner has none.
        private void DrawPreview(float width, int shown)
        {
            Rect rect = PreviewRect(width);
            if (preview.HasModel(shown)) GUI.DrawTexture(rect, preview.Texture, ScaleMode.StretchToFill, true);
            else GUI.Label(new Rect(rect.x + 24, rect.y + rect.height * 0.5f - 15, rect.width - 48, 30), "디자인 없음", body);
        }

        // The highlighted runner's stats as bars between StatBarFloor and StatBarCeiling, with their scales.
        private void DrawRunnerStats(float width, int shown)
        {
            NightUi.PanelCard(new Rect(width - 484, 238, 420, 382));
            GUI.Label(new Rect(width - 452, 261, 360, 28), "RUNNER PROFILE", small);
            GUI.Label(new Rect(width - 452, 298, 360, 62), runnerOptions[shown], heading);
            for (int s = 0; s < StatNames.Length; s++)
            {
                float y = 390 + s * 43;
                GUI.Label(new Rect(width - 452, y, 120, 28), StatNames[s], small);
                var bar = new Rect(width - 308, y + 9, 150, 6);
                NightUi.Block(bar, Slate);
                bar.width *= Mathf.InverseLerp(StatBarFloor, StatBarCeiling, runnerStats[shown, s]);
                NightUi.Block(bar, Mint);
                GUI.Label(new Rect(width - 135, y, 65, 28), runnerStatText[shown, s], small);
            }
            GUI.Label(new Rect(width - 452, 574, 360, 28), trackOptions[track] + "  /  " + SpeedOptions[speed], small);
        }

        private void DrawList(float width, float height)
        {
            string[] options = Options(page);
            bool[] locked = Locked(page);
            // A click is confirmed after the list is drawn: confirming opens another page, and the rest of this loop
            // would index that page's arrays with this page's rows (a click on 싱글 레이스 threw, 2026-10-20).
            for (int i = 0; i < options.Length; i++)
            {
                bool runnerPage = page == Page.Runner;
                float tileWidth = (width - 580) / options.Length;
                var row = runnerPage ? new Rect(64 + i * tileWidth, height - 240, tileWidth - 12, 140)
                    : new Rect(64, 238 + i * 86, 600, 70);
                NightUi.CardLook look = locked[i] ? NightUi.CardLook.Locked
                    : i == cursor ? NightUi.CardLook.Selected : NightUi.CardLook.Plain;
                // The row's content follows the card's face, which sinks while pressed.
                row = NightUi.PressableCard(row, look, out bool clicked);
                if (clicked) pendingRow = i;
                GUIStyle style = locked[i] ? lockedItem : item;
                if (runnerPage)
                {
                    NightUi.Portrait(runnerArtwork, i, new Rect(row.center.x - 42, row.y + 1, 84, 105));
                    GUI.Label(new Rect(row.x + 8, row.y + 103, row.width - 16, 34), options[i], tileLabel);
                }
                else GUI.Label(new Rect(row.x + 26, row.y, row.width - 26, row.height), locked[i] ? options[i] + "   (잠김)" : options[i], style);
                if (page == Page.Track)
                {
                    recordLine.normal.textColor = Muted;
                    GUI.Label(new Rect(row.x + 225, row.y, row.width - 249, row.height), trackRecords[i], recordLine);
                }
                else if (page == Page.Settings && i != SettingKeys)
                {
                    recordLine.normal.textColor = Mint;
                    if (i < SettingSpeedView)
                    {
                        int level = i == SettingMaster ? settings.MasterLevel : i == SettingMusic ? settings.MusicLevel : settings.EffectsLevel;
                        var bar = new Rect(row.x + 283, row.y + 33, 180, 5);
                        NightUi.Block(bar, Slate); bar.width *= (float)level / GameSettings.MaxLevel; NightUi.Block(bar, Mint);
                    }
                    GUI.Label(new Rect(row.x + 485, row.y, 88, row.height), SettingValue(i), recordLine);
                }
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
            for (int i = 0; i < Keys.Length; i++) GUI.Label(new Rect(64, 245 + i * 48, 650, 36), Keys[i], body);
        }

        private void OnDestroy()
        {
            if (font != null) Destroy(font);
            if (displayFont != null) Destroy(displayFont);
            preview?.Release();
        }
    }
}
