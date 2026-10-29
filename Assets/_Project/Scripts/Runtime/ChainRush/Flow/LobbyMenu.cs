using System;
using ProtoHarness.ChainRush.Audio;
using ProtoHarness.ChainRush.Flow.Lobby;
using ProtoHarness.ChainRush.Track;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProtoHarness.ChainRush.Flow
{
    // The lobby: main menu (single race / multi, locked / settings), then for a single race the speed class, the track
    // (with that speed's records, user 2026-10-29) and the runner, then RaceLaunch and the race scene. ↑/↓ choose, Enter confirms, ESC goes back (from the main menu,
    // to the title). Locked entries are shown but refuse with a notice. Drawn like ChainRushHud (OnGUI, 1600 x 900).
    // Each page is a LobbyPage (Flow/Lobby/: its rows, what choosing and going back do, how it is drawn); this keeps the
    // page shown and the cursor, reads the keys and the mouse, plays the cues and changes pages and scenes. Moving,
    // choosing, going back and a refusal each play their UiSound cue.
    public sealed class LobbyMenu : MonoBehaviour
    {
        // In the order of the page list built in Awake.
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

        [SerializeField] private TrackChoice[] tracks;
        [SerializeField] private RunnerProfile[] runners;
        [SerializeField] private Texture2D nightBackground;
        [SerializeField] private Texture2D runnerArtwork;
        private BestRecords records = BestRecords.Player;
        // Set in Awake, not here: GameSettings reads PlayerPrefs when it is made, which Unity refuses in a field initializer.
        private GameSettings settings;
        private LobbyContext context;
        // Indexed by Page.
        private LobbyPage[] pages;
        private TrackPage trackPage;
        private LobbyPage page;
        private int cursor;
        private RunnerPreview preview;
        private LobbyView view;
        private string notice = "";
        private bool leaving;

        // Where the track page reads best records from (the player's; a test gives its own).
        public BestRecords Records
        {
            get => records;
            set
            {
                records = value ?? throw new ArgumentNullException(nameof(value));
                if (context == null) return;
                context.Records = records;
                trackPage.RefreshRecords();
            }
        }
        // Where the settings page reads and writes the settings (the player's; a test gives its own).
        public GameSettings Settings
        {
            get => settings;
            set
            {
                settings = value ?? throw new ArgumentNullException(nameof(value));
                if (context != null) context.Settings = settings;
            }
        }
        public Page CurrentPage => page.Id;
        // The track page's record line for a track: "최고 기록 (빠름)  01:19.2  ·  BETA", or "빠름 기록 없음".
        public string TrackRecordText(int index) => trackPage.RecordText(index);
        public int Cursor => cursor;
        // The runner the setup names: on the runner page the highlighted one (a click only picks it since 2026-10-20),
        // elsewhere the one last entered with. No longer drawn: the RACE SETUP panel gave way to the descriptions (2026-10-21).
        public int SetupRunner => page.Id == Page.Runner ? cursor : context.Runner;
        // The highlighted entry's description on the main, track and speed pages; empty on the others.
        public string Description => page.Description(cursor);
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
            for (int i = 0; i < tracks.Length; i++)
            {
                if (tracks[i].Definition == null || string.IsNullOrEmpty(tracks[i].Title) || tracks[i].EnvironmentScene == null)
                {
                    Debug.LogError($"LobbyMenu: track {i} needs a title and a definition.", this);
                    enabled = false;
                    return;
                }
                // A track without a description still races; the empty panel is the loud part, with this error.
                if (TrackPage.DescriptionOf(tracks[i].Title).Length == 0)
                    Debug.LogError($"LobbyMenu: track {i} ({tracks[i].Title}) has no description in TrackPage.DescriptionOf.", this);
            }
            if (runners == null || runners.Length == 0)
            {
                Debug.LogError("LobbyMenu: at least one runner is required.", this);
                enabled = false;
                return;
            }
            for (int i = 0; i < runners.Length; i++)
            {
                if (runners[i] == null || !runners[i].TryValidate(out string error))
                {
                    Debug.LogError($"LobbyMenu: runner {i} is missing or invalid.", this);
                    enabled = false;
                    return;
                }
            }
            var looks = new GameObject[runners.Length];
            for (int i = 0; i < runners.Length; i++) looks[i] = runners[i].Look;
            try
            {
                preview = RunnerPage.MakePreview(looks);
            }
            catch (ArgumentException e)
            {
                // A look with nothing to draw: the lobby stops, as for any other invalid runner.
                Debug.LogException(e, this);
                enabled = false;
                return;
            }
            context = new LobbyContext(tracks, runners, preview, settings, records);
            trackPage = new TrackPage(context);
            pages = new LobbyPage[]
            {
                new MainPage(context), new SettingsPage(context), trackPage,
                new SpeedPage(context), new RunnerPage(context), new KeysPage(context),
            };
            for (int i = 0; i < pages.Length; i++)
                if (pages[i].Id != (Page)i)
                    throw new InvalidOperationException($"LobbyMenu: page {i} is {pages[i].Id}, not {(Page)i}; keep the list in Page order.");
            page = pages[(int)Page.Main];
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
            if (mouse != null && !leaving) page.Point(mouse);
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.upArrowKey.wasPressedThisFrame) Move(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame) Move(1);
            else if (keyboard.leftArrowKey.wasPressedThisFrame) Adjust(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame) Adjust(1);
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) Confirm();
            else if (keyboard.escapeKey.wasPressedThisFrame) Back();
        }

        // Moves the highlight, wrapping around the list.
        public void Move(int delta)
        {
            int count = page.Count;
            if (count == 0) return;
            int was = cursor;
            cursor = ((cursor + delta) % count + count) % count;
            if (cursor != was) UiSound.Play(UiSound.Cue.Move);
            notice = "";
            page.Highlighted(cursor);
        }

        // Changes the highlighted row on a page that takes ←/→ (settings); nothing elsewhere.
        public void Adjust(int delta)
        {
            if (!enabled || leaving || !page.Adjusts) return;
            notice = "";
            page.Adjust(cursor, delta);
        }

        // Chooses the highlighted entry, or refuses a locked one with the page's notice.
        public void Confirm()
        {
            if (!enabled || leaving) return;
            if (page.Count > 0 && page.IsLocked(cursor))
            {
                notice = page.LockedNotice;
                UiSound.Play(UiSound.Cue.Denied);
                return;
            }
            notice = "";
            UiSound.Play(UiSound.Cue.Confirm);
            Go(page.Confirm(cursor));
        }

        // One page back; from the main menu, back to the title.
        public void Back()
        {
            if (!enabled || leaving) return;
            notice = "";
            UiSound.Play(UiSound.Cue.Back);
            Go(page.Back());
        }

        // Highlights a row without choosing it, as ↑/↓ would.
        public void Highlight(int row)
        {
            int count = page.Count;
            if (row < 0 || row >= count)
                throw new ArgumentOutOfRangeException(nameof(row), row, $"LobbyMenu: the {page.Id} page has {count} rows.");
            Move(row - cursor);
        }

        // The race the current choices make (track, speed class and runner as last confirmed).
        public RaceSetup BuildSetup()
        {
            TrackChoice track = tracks[context.Track];
            return new RaceSetup(track.Definition, track.Title, track.EnvironmentScene, RaceMode.Single,
                SpeedPage.ClassAt(context.Speed), runners[context.Runner]);
        }

        private void Go(LobbyPage.Step step)
        {
            switch (step.What)
            {
                case LobbyPage.Step.Kind.Stay:
                    break;
                case LobbyPage.Step.Kind.Open:
                    Open(step.Page, step.Highlight);
                    break;
                case LobbyPage.Step.Kind.Launch:
                    RaceLaunch.Request(BuildSetup());
                    leaving = true;
                    SceneManager.LoadScene(RaceLaunch.RaceScene);
                    break;
                case LobbyPage.Step.Kind.Title:
                    leaving = true;
                    SceneManager.LoadScene(RaceLaunch.TitleScene);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step.What, "LobbyMenu: unknown step.");
            }
        }

        private void Open(Page next, int highlight)
        {
            page.Closed();
            page = pages[(int)next];
            cursor = highlight;
            page.Opened(cursor);
        }

        private void OnGUI()
        {
            if (view == null) view = new LobbyView(nightBackground, runnerArtwork);
            if (!view.Ready)
            {
                Debug.LogError("LobbyMenu: could not create the font.", this);
                enabled = false;
                return;
            }
            Matrix4x4 previous = GUI.matrix;
            float scale = LobbyView.GuiScale();
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            bool back = view.DrawFrame(page, width, height);
            int clicked = page.DrawBody(view, cursor, width, height);
            view.DrawFooter(notice, page.Help, width, height);
            bool enter = page.DrawSide(view, cursor, width, height);
            GUI.matrix = previous;
            // Acted on after drawing: each changes the page the rest of this draw would read.
            if (back) Back();
            else if (enter) Confirm();
            else if (clicked >= 0)
            {
                if (page.ClickChooses) { cursor = clicked; Confirm(); }
                else Highlight(clicked);
            }
        }

        private void OnDestroy()
        {
            view?.Release();
            preview?.Release();
        }
    }
}
