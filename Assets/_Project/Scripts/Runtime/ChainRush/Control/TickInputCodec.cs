using System.IO;

namespace ProtoHarness.ChainRush.Control
{
    // The eight buttons of a TickInput as one byte, shared by everything that writes a TickInput as bytes (the input
    // log file, the network). Steer travels as its own float. A byte with an unknown bit is refused, so a file or a
    // message from a newer build, or a corrupt one, throws instead of being read as something else. Since reversing
    // (2026-10-22) all eight bits are known: a ninth button needs a wider format, not just a new bit.
    public static class TickInputCodec
    {
        private const byte Primary = 1;
        private const byte Release = 2;
        private const byte Attack = 4;
        private const byte SwingHeld = 8;
        private const byte ChainAction = 16;
        private const byte SwingPressed = 32;
        // Set when the throttle is let go, so bytes written before it existed read as full throttle.
        private const byte Coasting = 64;
        // Set while backing up; bytes written before it existed read as not reversing.
        private const byte Reversing = 128;
        private const byte Known = Primary | Release | Attack | SwingHeld | ChainAction | SwingPressed | Coasting | Reversing;

        public static byte ToFlags(in TickInput input)
        {
            byte flags = 0;
            if (input.PrimaryPressed) flags |= Primary;
            if (input.ReleasePressed) flags |= Release;
            if (input.AttackPressed) flags |= Attack;
            if (input.SwingHeld) flags |= SwingHeld;
            if (input.ChainActionPressed) flags |= ChainAction;
            if (input.SwingPressed) flags |= SwingPressed;
            if (input.Coasting) flags |= Coasting;
            if (input.Reversing) flags |= Reversing;
            return flags;
        }

        // TickInput's constructor rejects a steer outside [-1, 1] (NaN too).
        public static TickInput FromFlags(float steer, byte flags)
        {
            if ((flags & ~Known) != 0) throw new InvalidDataException($"TickInputCodec: unknown input flags 0x{flags:X2}.");
            return new TickInput(steer, (flags & Primary) != 0, (flags & Release) != 0, (flags & Attack) != 0, (flags & SwingHeld) != 0,
                (flags & ChainAction) != 0, (flags & SwingPressed) != 0, (flags & Coasting) != 0, (flags & Reversing) != 0);
        }
    }
}
