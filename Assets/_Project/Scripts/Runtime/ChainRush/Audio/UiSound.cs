using System;

namespace ProtoHarness.ChainRush.Audio
{
    // The menus' sound moments in one place: the screens call Play, and whatever plays the sounds listens to Played.
    // Nothing listens yet (the sounds come later, user 2026-10-21), so for now Play only tells the listeners, if any.
    public static class UiSound
    {
        public enum Cue
        {
            // The highlight moved: ↑/↓, ←/→ on a setting, or the mouse coming onto a button.
            Move,
            // An entry or a button was chosen.
            Confirm,
            // One page back (ESC or the back button).
            Back,
            // A locked entry refused.
            Denied,
        }

        public static event Action<Cue> Played;

        public static void Play(Cue cue)
        {
            if (cue < Cue.Move || cue > Cue.Denied) throw new ArgumentOutOfRangeException(nameof(cue), cue, "UiSound: no such cue.");
            Played?.Invoke(cue);
        }
    }
}
