namespace ProtoHarness.ChainRush.Flow.Lobby
{
    // The speed classes, all open (M3; SpeedScale has their scales). The first page of a single race (user 2026-10-29);
    // choosing one goes on to the track page, which shows that speed's records.
    public sealed class SpeedPage : LobbyPage
    {
        private static readonly SpeedClass[] Classes = { SpeedClass.Normal, SpeedClass.Fast, SpeedClass.VeryFast };
        private static readonly string[] Labels = { "보통", "빠름", "매우빠름" };
        private static readonly string[] Descriptions =
        {
            "처음 달리는 러너를 위한 속도.\n코스를 익히기 좋습니다.",
            "기본 레이스 속도.",
            "숙련자를 위한 가장 빠른 속도.",
        };

        public SpeedPage(LobbyContext context) : base(context, LobbyMenu.Page.Speed, "SPEED") { }

        // A row's speed class and name, for the other pages (LobbyContext.Speed is a row of this page).
        public static SpeedClass ClassAt(int row) => Classes[row];
        public static string LabelAt(int row) => Labels[row];

        public override int Count => Labels.Length;
        public override string Label(int row) => Labels[row];
        public override string DescriptionTag => "SPEED CLASS";
        public override string Description(int row) => Descriptions[row];

        public override Step Confirm(int row)
        {
            Context.Speed = row;
            return Step.Open(LobbyMenu.Page.Track, Context.Track);
        }

        public override Step Back() => Step.Open(LobbyMenu.Page.Main, MainPage.SingleRow);
    }
}
