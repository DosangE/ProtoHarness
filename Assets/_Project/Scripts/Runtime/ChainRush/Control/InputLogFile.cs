using System;
using System.IO;

namespace ProtoHarness.ChainRush.Control
{
    // An InputLog as a file: "PHIL", a version, the number of ticks, then each tick's steer (a float) and buttons (one
    // byte, TickInputCodec). Loading is strict: a wrong header, a wrong length or a bad input throws. With the same seed
    // a saved log repeats the run it came from (DESIGN.md P2); the network tests use it to hand a recorded run to a
    // player process that has no bot.
    public static class InputLogFile
    {
        private static readonly byte[] Magic = { (byte)'P', (byte)'H', (byte)'I', (byte)'L' };
        private const int Version = 1;
        private const int HeaderBytes = 12;
        private const int TickBytes = 5;

        public static byte[] Encode(InputLog log)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            using var stream = new MemoryStream(HeaderBytes + log.Count * TickBytes);
            using var writer = new BinaryWriter(stream);
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(log.Count);
            for (int i = 0; i < log.Count; i++)
            {
                TickInput input = log[i];
                writer.Write(input.Steer);
                writer.Write(TickInputCodec.ToFlags(input));
            }
            writer.Flush();
            return stream.ToArray();
        }

        public static InputLog Decode(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length < HeaderBytes) throw new InvalidDataException($"InputLogFile: {bytes.Length} bytes is shorter than the {HeaderBytes}-byte header.");
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream);
            for (int i = 0; i < Magic.Length; i++)
                if (reader.ReadByte() != Magic[i]) throw new InvalidDataException("InputLogFile: not an input log (wrong header).");
            int version = reader.ReadInt32();
            if (version != Version) throw new InvalidDataException($"InputLogFile: version {version}, this build reads {Version}.");
            int count = reader.ReadInt32();
            if (count < 0 || bytes.Length != HeaderBytes + (long)count * TickBytes)
                throw new InvalidDataException($"InputLogFile: the header says {count} ticks but the file holds {(bytes.Length - HeaderBytes) / (double)TickBytes:F2}.");
            var log = new InputLog();
            for (int i = 0; i < count; i++)
            {
                float steer = reader.ReadSingle();
                byte flags = reader.ReadByte();
                log.Add(TickInputCodec.FromFlags(steer, flags));
            }
            return log;
        }

        public static void Save(string path, InputLog log) => File.WriteAllBytes(path, Encode(log));

        public static InputLog Load(string path) => Decode(File.ReadAllBytes(path));
    }
}
