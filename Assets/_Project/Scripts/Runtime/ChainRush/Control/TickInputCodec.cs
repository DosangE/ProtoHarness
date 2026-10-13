using System.IO;

namespace ProtoHarness.ChainRush.Control
{
    // The five buttons of a TickInput as one byte, shared by everything that writes a TickInput as bytes (the input
    // log file, the network). Steer travels as its own float. A byte with an unknown bit is refused, so a file or a
    // message from a newer build, or a corrupt one, throws instead of being read as something else.
    public static class TickInputCodec
    {
        private const byte Primary = 1;
        private const byte Release = 2;
        private const byte Attack = 4;
        private const byte Drift = 8;
        private const byte ChainAction = 16;
        private const byte Known = Primary | Release | Attack | Drift | ChainAction;

        public static byte ToFlags(in TickInput input)
        {
            byte flags = 0;
            if (input.PrimaryPressed) flags |= Primary;
            if (input.ReleasePressed) flags |= Release;
            if (input.AttackPressed) flags |= Attack;
            if (input.Drift) flags |= Drift;
            if (input.ChainActionPressed) flags |= ChainAction;
            return flags;
        }

        // TickInput's constructor rejects a steer outside [-1, 1] (NaN too).
        public static TickInput FromFlags(float steer, byte flags)
        {
            if ((flags & ~Known) != 0) throw new InvalidDataException($"TickInputCodec: unknown input flags 0x{flags:X2}.");
            return new TickInput(steer, (flags & Primary) != 0, (flags & Release) != 0, (flags & Attack) != 0, (flags & Drift) != 0, (flags & ChainAction) != 0);
        }
    }
}
