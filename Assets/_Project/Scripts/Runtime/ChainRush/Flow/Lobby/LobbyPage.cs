using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProtoHarness.ChainRush.Flow.Lobby
{
    // One lobby page: its rows (name, lock, description), what choosing a row or going back does, and how its rows and
    // panels are drawn inside LobbyView's frame. LobbyMenu keeps the page shown and the cursor, plays the cues and does
    // the Step a page returns. A new page is a subclass, a LobbyMenu.Page value and an entry in LobbyMenu's page list.
    public abstract class LobbyPage
    {
        // What choosing a row or going back asks of the menu.
        public readonly struct Step
        {
            public enum Kind { Stay, Open, Launch, Title }

            private Step(Kind what, LobbyMenu.Page page, int highlight)
            {
                What = what;
                Page = page;
                Highlight = highlight;
            }

            public Kind What { get; }
            // For Open: the page and the row it opens on.
            public LobbyMenu.Page Page { get; }
            public int Highlight { get; }

            public static Step Stay => new Step(Kind.Stay, default, 0);
            // The race the choices make, then the race scene.
            public static Step Launch => new Step(Kind.Launch, default, 0);
            // Back to the title scene.
            public static Step Title => new Step(Kind.Title, default, 0);
            public static Step Open(LobbyMenu.Page page, int highlight) => new Step(Kind.Open, page, highlight);
        }

        // The help line under most lists; the settings and runner pages name their own keys too.
        protected const string ListHelp = "↑ / ↓  선택      ENTER  결정      ESC  뒤로";

        protected LobbyPage(LobbyContext context, LobbyMenu.Page id, string title, string help = ListHelp)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Id = id;
            Title = title;
            Help = help;
            Breadcrumb = "SINGLE RACE  /  " + title;
        }

        protected LobbyContext Context { get; }
        public LobbyMenu.Page Id { get; }
        public string Title { get; }
        public string Help { get; }
        // The small line over the heading.
        public string Breadcrumb { get; }
        // The big heading: the title unless a page says otherwise.
        public virtual string Heading => Title;

        public abstract int Count { get; }
        public abstract string Label(int row);
        public virtual bool IsLocked(int row) => false;
        // What a locked row refuses with. A page that locks a row must say why.
        public virtual string LockedNotice =>
            throw new InvalidOperationException($"LobbyPage: the {Id} page locks a row but has no notice.");
        // The description panel's small heading; empty for a page without the panel.
        public virtual string DescriptionTag => "";
        public virtual string Description(int row) => "";
        // A line at the foot of the description panel (a track's record); empty for none.
        public virtual string DescriptionFoot(int row) => "";
        // Whether ←/→ change this page's rows.
        public virtual bool Adjusts => false;
        // Whether a click on a row chooses it (as Enter) or only highlights it (as ↑/↓).
        public virtual bool ClickChooses => true;

        // Chooses an unlocked row (the menu has refused locked ones).
        public abstract Step Confirm(int row);
        public abstract Step Back();
        public virtual void Adjust(int row, int delta) { }
        public virtual void Opened(int highlight) { }
        public virtual void Closed() { }
        // The highlight moved (or was moved onto the row it was on).
        public virtual void Highlighted(int row) { }
        // The mouse, every frame while this page is shown and the lobby is not leaving.
        public virtual void Point(Mouse mouse) { }

        // A row's card, in the lobby's units: a column on the left by default.
        public virtual Rect RowRect(int row, int count, float width, float height) => new Rect(64, 238 + row * 86, 600, 70);

        // A row's content on its card's face: its name by default.
        public virtual void DrawRow(LobbyView view, Rect face, int row) => view.RowLabel(face, Label(row), IsLocked(row));

        // Under the heading: the rows by default. Returns the row clicked, or -1.
        public virtual int DrawBody(LobbyView view, int cursor, float width, float height) => view.DrawList(this, cursor, width, height);

        // Right of the rows: the description panel when the page has one. Returns whether the page's main button
        // (the runner page's start) was pressed.
        public virtual bool DrawSide(LobbyView view, int cursor, float width, float height)
        {
            if (DescriptionTag.Length > 0) view.DrawDescription(this, cursor, width);
            return false;
        }
    }
}
