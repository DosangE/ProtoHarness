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

        public void Record(float steer, bool primaryPressed, bool releasePressed, bool attackPressed)
        {
            if (!(steer >= -1f && steer <= 1f))
                throw new ArgumentOutOfRangeException(nameof(steer), steer, "Steer must be within [-1, 1].");
            this.steer = steer;
            this.primaryPressed |= primaryPressed;
            this.releasePressed |= releasePressed;
            this.attackPressed |= attackPressed;
        }

        public TickInput Consume()
        {
            var input = new TickInput(steer, primaryPressed, releasePressed, attackPressed);
            primaryPressed = false;
            releasePressed = false;
            attackPressed = false;
            return input;
        }

        public void Clear()
        {
            steer = 0f;
            primaryPressed = false;
            releasePressed = false;
            attackPressed = false;
        }
    }
}
