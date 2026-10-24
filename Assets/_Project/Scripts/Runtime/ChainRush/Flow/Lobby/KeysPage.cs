using UnityEngine;

namespace ProtoHarness.ChainRush.Flow.Lobby
{
    // The keys, as lines of text instead of rows. Enter or ESC goes back to the settings' keys row.
    public sealed class KeysPage : LobbyPage
    {
        private static readonly string[] Lines =
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

        public KeysPage(LobbyContext context) : base(context, LobbyMenu.Page.Keys, "KEYS") { }

        public override int Count => 0;
        public override string Label(int row) => throw new System.ArgumentOutOfRangeException(nameof(row), row, "LobbyPage: the keys page has no rows.");
        public override Step Confirm(int row) => Back();
        public override Step Back() => Step.Open(LobbyMenu.Page.Settings, SettingsPage.KeysRow);

        public override int DrawBody(LobbyView view, int cursor, float width, float height)
        {
            for (int i = 0; i < Lines.Length; i++) GUI.Label(new Rect(64, 245 + i * 48, 650, 36), Lines[i], view.Body);
            return -1;
        }
    }
}
