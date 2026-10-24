using UnityEngine;

namespace ProtoHarness.ChainRush.Flow.Lobby
{
    // The tracks: the scene's (LobbyMenu.TrackChoice) with each one's best record at the chosen speed class, then the
    // tracks still to come (Placeholders), locked. Choosing one goes on to the speed page.
    public sealed class TrackPage : LobbyPage
    {
        // Tracks still to come, listed after the scene's tracks and locked; they have no definition yet.
        private static readonly string[] Placeholders = { "NEON HARBOR" };
        private static readonly string[] PlaceholderDescriptions =
        {
            "네온이 비치는 항구 부두를 도는 코스.\n준비 중인 트랙입니다.",
        };
        private const string PlaceholderRecord = "준비 중";

        // The scene's tracks, then Placeholders; a placeholder's index is past the scene's tracks.
        private readonly string[] labels;
        private readonly bool[] locks;
        private readonly string[] descriptions;
        // Per track, its best record at the chosen speed class (RefreshRecords).
        private readonly string[] records;

        public TrackPage(LobbyContext context) : base(context, LobbyMenu.Page.Track, "TRACK")
        {
            int sceneTracks = Context.Tracks.Count;
            int count = sceneTracks + Placeholders.Length;
            labels = new string[count];
            locks = new bool[count];
            descriptions = new string[count];
            records = new string[count];
            for (int i = 0; i < sceneTracks; i++)
            {
                labels[i] = Context.Tracks[i].Title;
                descriptions[i] = DescriptionOf(Context.Tracks[i].Title);
            }
            for (int p = 0; p < Placeholders.Length; p++)
            {
                int i = sceneTracks + p;
                labels[i] = Placeholders[p];
                locks[i] = true;
                records[i] = PlaceholderRecord;
                descriptions[i] = PlaceholderDescriptions[p];
            }
            RefreshRecords();
        }

        public override int Count => labels.Length;
        public override string Label(int row) => labels[row];
        public override bool IsLocked(int row) => locks[row];
        public override string LockedNotice => "새 트랙은 이후 마일스톤에서 열립니다.";
        public override string DescriptionTag => "TRACK";
        public override string Description(int row) => descriptions[row];
        public override string DescriptionFoot(int row) => records[row];

        // The record line for a track: "최고 기록  01:19.2 · BETA", "기록 없음", or PlaceholderRecord.
        public string RecordText(int row) => records[row];

        // A scene track's description, by its title; empty for a title not written here.
        public static string DescriptionOf(string title)
        {
            switch (title)
            {
                case "STADIUM": return "야간 조명 아래의 경기장 서킷.\n지금 달릴 수 있는 트랙입니다.";
                case "SKYWAY LOOP": return "마을을 지나 고가도로를 달리는 2바퀴 코스.\n긴 고가 직선 뒤 급한 내리막이 기다립니다.";
                default: return "";
            }
        }

        // Reads the best records again: on waking (a race may have just set one), whenever this page opens and when the
        // records' store changes. The placeholders keep PlaceholderRecord.
        public void RefreshRecords()
        {
            SpeedClass speed = SpeedPage.ClassAt(Context.Speed);
            for (int i = 0; i < Context.Tracks.Count; i++)
                records[i] = Context.Records.TryGet(Context.Tracks[i].Definition.name, speed, out BestRecords.Record best)
                    ? "최고 기록  " + ChainRushHud.FormatTime(Ticks.ToSeconds(best.Ticks)) + "  ·  " + best.Runner
                    : "기록 없음";
        }

        public override Step Confirm(int row)
        {
            Context.Track = row;
            return Step.Open(LobbyMenu.Page.Speed, Context.Speed);
        }

        public override Step Back() => Step.Open(LobbyMenu.Page.Main, MainPage.SingleRow);

        public override void Opened(int highlight) => RefreshRecords();

        // The name, then the track's record at the row's right end.
        public override void DrawRow(LobbyView view, Rect face, int row)
        {
            base.DrawRow(view, face, row);
            GUI.Label(new Rect(face.x + 225, face.y, face.width - 249, face.height), records[row], view.RowValue);
        }
    }
}
