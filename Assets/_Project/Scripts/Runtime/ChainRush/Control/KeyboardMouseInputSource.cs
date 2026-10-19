using UnityEngine.InputSystem;

namespace ProtoHarness.ChainRush.Control
{
    // Desktop mapping, KartRider style with no mouse (2026-10-18): the up arrow is the throttle (2026-10-19: let go, the
    // runner coasts to a stop and a booster ends), left/right arrows steer, left Alt is the primary
    // action (jump or grapple), letting go of left Alt or pressing down releases the chain, Space attacks. (Right Alt is
    // the Korean/English key on Korean keyboards, so it is not used.) Left Shift pressed
    // near a corner pole hooks it and held keeps the swing (the drift of this game, NEXT_REQUESTS.md R4b); left Ctrl
    // fires the chain action.
    public sealed class KeyboardMouseInputSource : IInputSource
    {
        private readonly InputLatch latch = new InputLatch();

        public void Poll()
        {
            float steer = 0f;
            bool primary = false;
            bool release = false;
            bool attack = false;
            bool swingHeld = false;
            bool chainAction = false;
            bool swingPressed = false;
            // No keyboard means no throttle either.
            bool coasting = true;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.isPressed) steer -= 1f;
                if (keyboard.rightArrowKey.isPressed) steer += 1f;
                primary = keyboard.leftAltKey.wasPressedThisFrame;
                release = keyboard.leftAltKey.wasReleasedThisFrame || keyboard.downArrowKey.wasPressedThisFrame;
                attack = keyboard.spaceKey.wasPressedThisFrame;
                swingHeld = keyboard.leftShiftKey.isPressed;
                swingPressed = keyboard.leftShiftKey.wasPressedThisFrame;
                chainAction = keyboard.leftCtrlKey.wasPressedThisFrame;
                coasting = !keyboard.upArrowKey.isPressed;
            }
            latch.Record(steer, primary, release, attack, swingHeld, chainAction, swingPressed, coasting);
        }

        public TickInput Consume() => latch.Consume();

        public void Clear() => latch.Clear();
    }
}
