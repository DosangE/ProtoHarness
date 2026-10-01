using UnityEngine.InputSystem;

namespace ProtoHarness.ChainRush.Control
{
    // Desktop mapping: A/D or arrows steer, left click is the primary action (jump or grapple),
    // releasing left or pressing right releases the chain, Space attacks.
    public sealed class KeyboardMouseInputSource : IInputSource
    {
        private readonly InputLatch latch = new InputLatch();

        public void Poll()
        {
            float steer = 0f;
            bool primary = false;
            bool release = false;
            bool attack = false;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) steer -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) steer += 1f;
                attack = keyboard.spaceKey.wasPressedThisFrame;
            }
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                primary = mouse.leftButton.wasPressedThisFrame;
                release = mouse.leftButton.wasReleasedThisFrame || mouse.rightButton.wasPressedThisFrame;
            }
            latch.Record(steer, primary, release, attack);
        }

        public TickInput Consume() => latch.Consume();

        public void Clear() => latch.Clear();
    }
}
