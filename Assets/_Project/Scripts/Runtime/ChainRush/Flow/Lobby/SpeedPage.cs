namespace ProtoHarness.ChainRush.Flow.Lobby
{
    // The speed classes. Only Fast is open (보통 and 매우빠름 come in M3). Choosing one goes on to the runner page.
    public sealed class SpeedPage : LobbyPage
    {
        private static readonly SpeedClass[] Classes = { SpeedClass.Normal, SpeedClass.Fast, SpeedClass.VeryFast };
        private static readonly string[] Labels = { "보통", "빠름", "매우빠름" };
        private static readonly bool[] Locks = { true, false, true };
        private static readonly string[] Descriptions =
        {
            "처음 달리는 러너를 위한 속도.\n코스를 익히기 좋습니다.\n아직 열리지 않았습니다.",
            "기본 레이스 속도.\n지금 달릴 수 있는 단계입니다.",
            "숙련자를 위한 가장 빠른 속도.\n아직 열리지 않았습니다.",
        };

        public SpeedPage(LobbyContext context) : base(context, LobbyMenu.Page.Speed, "SPEED") { }

        // A row's speed class and name, for the other pages (LobbyContext.Speed is a row of this page).
        public static SpeedClass ClassAt(int row) => Classes[row];
        public static string LabelAt(int row) => Labels[row];

        public override int Count => Labels.Length;
        public override string Label(int row) => Labels[row];
        public override bool IsLocked(int row) => Locks[row];
        public override string LockedNotice => "다른 속도 단계는 M2-b 에서 열립니다.";
        public override string DescriptionTag => "SPEED CLASS";
        public override string Description(int row) => Descriptions[row];

        public override Step Confirm(int row)
        {
            Context.Speed = row;
            return Step.Open(LobbyMenu.Page.Runner, Context.Runner);
        }

        public override Step Back() => Step.Open(LobbyMenu.Page.Track, Context.Track);
    }
}
