namespace ProtoHarness.ChainRush.Flow.Lobby
{
    // The first page of a single race (user 2026-11-13): a time attack alone, which keeps records and ghosts, or a race
    // against bots, which keeps neither. Either goes on to the speed page; a bot race draws its lineup's seed here, so
    // going back and forth through the later pages keeps the same bots.
    public sealed class SingleModePage : LobbyPage
    {
        public const int TimeAttackRow = 0, BotsRow = 1;
        private static readonly string[] Labels = { "타임어택", "봇전" };
        private static readonly string[] Descriptions =
        {
            "혼자 코스를 달려 기록에 도전합니다.\n속도와 트랙마다 최고 기록과 고스트가 남습니다.",
            "봇 3명과 같은 코스에서 순위를 겨룹니다.\n기록과 고스트는 남지 않습니다.",
        };

        public SingleModePage(LobbyContext context) : base(context, LobbyMenu.Page.Mode, "MODE") { }

        // The row of a mode on this page, for the pages that come back to it.
        public static int RowOf(RaceMode mode) => mode == RaceMode.Bots ? BotsRow : TimeAttackRow;

        public override int Count => Labels.Length;
        public override string Label(int row) => Labels[row];
        public override string DescriptionTag => "MODE";
        public override string Description(int row) => Descriptions[row];

        public override Step Confirm(int row)
        {
            Context.Mode = row == BotsRow ? RaceMode.Bots : RaceMode.Single;
            if (Context.Mode == RaceMode.Bots) Context.Seed = UnityEngine.Random.Range(0, int.MaxValue);
            return Step.Open(LobbyMenu.Page.Speed, Context.Speed);
        }

        public override Step Back() => Step.Open(LobbyMenu.Page.Main, MainPage.SingleRow);
    }
}
