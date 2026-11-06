using ProtoHarness.ChainRush.Tutorial;
using UnityEngine;

namespace ProtoHarness.ChainRush.Flow.Lobby
{
    // The tutorial's stages, all open (user 2026-11-06): choosing one starts the tutorial there and goes on through the
    // stages after it. No speed, track or runner to choose: the tutorial has its own (TutorialCourse).
    public sealed class TutorialPage : LobbyPage
    {
        public TutorialPage(LobbyContext context) : base(context, LobbyMenu.Page.Tutorial, "TUTORIAL") { }

        public override int Count => TutorialCourse.Stages.Count;
        public override string Label(int row) => (row + 1) + ".  " + TutorialCourse.Stages[row].Name;
        public override string DescriptionTag => "STAGE";
        public override string Description(int row) => TutorialCourse.Stages[row].Summary;
        // Six rows fit above the footer only closer together than the default column.
        public override Rect RowRect(int row, int count, float width, float height) => new Rect(64, 232 + row * 72, 600, 62);

        public override Step Confirm(int row) => Step.Tutorial(row);
        public override Step Back() => Step.Open(LobbyMenu.Page.Main, MainPage.TutorialRow);
    }
}
