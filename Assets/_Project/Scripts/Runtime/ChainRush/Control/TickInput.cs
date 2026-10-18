using System;

namespace ProtoHarness.ChainRush.Control
{
    // Everything the simulation may know about the player's controls for one tick.
    // Sources (keyboard, touch, replay, network) all reduce to this; the simulation never reads a device.
    public readonly struct TickInput
    {
        public float Steer { get; }
        public bool PrimaryPressed { get; }
        public bool ReleasePressed { get; }
        public bool AttackPressed { get; }
        // Held: the swing key is down. A corner pole swing lasts while it is (NEXT_REQUESTS.md R4b).
        public bool SwingHeld { get; }
        // Edge: spend a gauge slot on the chain action that fits the moment.
        public bool ChainActionPressed { get; }
        // Edge: the swing key went down. Only a press hooks a pole; holding the key into reach does not.
        public bool SwingPressed { get; }

        public TickInput(float steer, bool primaryPressed, bool releasePressed, bool attackPressed,
            bool swingHeld = false, bool chainActionPressed = false, bool swingPressed = false)
        {
            // NaN fails both comparisons, so it is rejected here too.
            if (!(steer >= -1f && steer <= 1f))
                throw new ArgumentOutOfRangeException(nameof(steer), steer, "Steer must be within [-1, 1].");
            Steer = steer;
            PrimaryPressed = primaryPressed;
            ReleasePressed = releasePressed;
            AttackPressed = attackPressed;
            SwingHeld = swingHeld;
            ChainActionPressed = chainActionPressed;
            SwingPressed = swingPressed;
        }
    }
}
