namespace ProtoHarness.ChainRush.Flow.Lobby
{
    // The main menu: single race (to the track page), multi (locked until M7) and settings. Back leaves for the title.
    public sealed class MainPage : LobbyPage
    {
        public const int SingleRow = 0, MultiRow = 1, SettingsRow = 2;
        private static readonly string[] Labels = { "싱글 레이스", "멀티 레이스", "설정" };
        private static readonly bool[] Locks = { false, true, false };
        private static readonly string[] Descriptions =
        {
            "혼자 코스를 달려 기록에 도전합니다.\n트랙과 속도, 러너를 골라 출발하세요.\n트랙과 속도마다 최고 기록이 남습니다.",
            "다른 러너들과 같은 코스에서 순위를 겨룹니다.\n멀티 레이스는 M7 에서 열립니다.",
            "볼륨과 속도 시야 효과를 조절하고\n조작 키를 확인합니다.",
        };

        public MainPage(LobbyContext context) : base(context, LobbyMenu.Page.Main, "MAIN MENU") { }

        public override int Count => Labels.Length;
        public override string Label(int row) => Labels[row];
        public override bool IsLocked(int row) => Locks[row];
        public override string LockedNotice => "멀티는 M7 에서 열립니다.";
        public override string DescriptionTag => "MODE";
        public override string Description(int row) => Descriptions[row];

        public override Step Confirm(int row) =>
            row == SingleRow ? Step.Open(LobbyMenu.Page.Track, Context.Track)
            : row == SettingsRow ? Step.Open(LobbyMenu.Page.Settings, 0) : Step.Stay;

        public override Step Back() => Step.Title;
    }
}
