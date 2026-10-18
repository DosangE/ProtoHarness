using System;

namespace ProtoHarness.ChainRush.Control
{
    // Keeps button edges seen between two ticks so a press is never lost when no tick runs in that frame.
    // Steer is a level and always holds the latest value; edges are delivered once, then cleared.
    public sealed class InputLatch
    {
        private float steer;
        private bool primaryPressed;
        private bool releasePressed;
        private bool attackPressed;
        private bool swingHeld;
        private bool chainActionPressed;
        private bool swingPressed;

        // The held swing key is a level like steer; the chain action and the swing press are edges like the buttons.
        public void Record(float steer, bool primaryPressed, bool releasePressed, bool attackPressed,
            bool swingHeld = false, bool chainActionPressed = false, bool swingPressed = false)
        {
            if (!(steer >= -1f && steer <= 1f))
                throw new ArgumentOutOfRangeException(nameof(steer), steer, "Steer must be within [-1, 1].");
            this.steer = steer;
            this.primaryPressed |= primaryPressed;
            this.releasePressed |= releasePressed;
            this.attackPressed |= attackPressed;
            this.swingHeld = swingHeld;
            this.chainActionPressed |= chainActionPressed;
            this.swingPressed |= swingPressed;
        }

        public TickInput Consume()
        {
            var input = new TickInput(steer, primaryPressed, releasePressed, attackPressed, swingHeld, chainActionPressed, swingPressed);
            primaryPressed = false;
            releasePressed = false;
            attackPressed = false;
            chainActionPressed = false;
            swingPressed = false;
            return input;
        }

        public void Clear()
        {
            steer = 0f;
            primaryPressed = false;
            releasePressed = false;
            attackPressed = false;
            swingHeld = false;
            chainActionPressed = false;
            swingPressed = false;
        }
    }
}
