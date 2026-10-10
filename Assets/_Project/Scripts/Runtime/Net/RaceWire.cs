using System;
using System.IO;
using ProtoHarness.ChainRush.Control;
using UnityEngine;

namespace ProtoHarness.Net
{
    // What goes over the wire. Input (client to server): the tick it is for and that tick's controls. State (server to
    // the racer it belongs to): an encoded SimSnapshot (SimSnapshotCodec), which carries its own tick. End (server to the
    // racer it belongs to, after its last state): the tick the run ended on, why, and the controls the server ran that tick
    // on. Remote (server to the other racer): where a racer was after a tick, for drawing it. Decoding is strict: a short or
    // long message, or an end reason this build does not know, throws.
    public static class RaceWire
    {
        // Why a run ended. The values are the bytes on the wire.
        public enum EndReason : byte
        {
            Failed = 1,
            Finished = 2,
        }

        public static byte[] EncodeInput(int tick, in TickInput input)
        {
            using var stream = new MemoryStream(16);
            using var writer = new BinaryWriter(stream);
            writer.Write(tick);
            writer.Write(input.Steer);
            writer.Write(TickInputCodec.ToFlags(input));
            writer.Flush();
            return stream.ToArray();
        }

        public static TickInput DecodeInput(byte[] bytes, out int tick)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream);
            tick = reader.ReadInt32();
            float steer = reader.ReadSingle();
            byte flags = reader.ReadByte();
            if (stream.Position != stream.Length) throw new InvalidDataException("RaceWire: unread bytes after an input.");
            return TickInputCodec.FromFlags(steer, flags);
        }

        public static byte[] EncodeEnd(int tick, EndReason reason, in TickInput input)
        {
            if (!IsKnown(reason)) throw new ArgumentOutOfRangeException(nameof(reason), reason, "RaceWire: not an end reason.");
            using var stream = new MemoryStream(16);
            using var writer = new BinaryWriter(stream);
            writer.Write(tick);
            writer.Write((byte)reason);
            writer.Write(input.Steer);
            writer.Write(TickInputCodec.ToFlags(input));
            writer.Flush();
            return stream.ToArray();
        }

        public static TickInput DecodeEnd(byte[] bytes, out int tick, out EndReason reason)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream);
            tick = reader.ReadInt32();
            byte reasonByte = reader.ReadByte();
            float steer = reader.ReadSingle();
            byte flags = reader.ReadByte();
            if (stream.Position != stream.Length) throw new InvalidDataException("RaceWire: unread bytes after an end.");
            reason = (EndReason)reasonByte;
            if (!IsKnown(reason)) throw new InvalidDataException($"RaceWire: unknown end reason {reasonByte} (tick {tick}).");
            return TickInputCodec.FromFlags(steer, flags);
        }

        // Whether this build knows the end reason (ClientPredictor checks with it too).
        internal static bool IsKnown(EndReason reason) => reason == EndReason.Failed || reason == EndReason.Finished;

        public static byte[] EncodeRemote(int tick, Vector3 position)
        {
            using var stream = new MemoryStream(16);
            using var writer = new BinaryWriter(stream);
            writer.Write(tick);
            writer.Write(position.x);
            writer.Write(position.y);
            writer.Write(position.z);
            writer.Flush();
            return stream.ToArray();
        }

        public static Vector3 DecodeRemote(byte[] bytes, out int tick)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream);
            tick = reader.ReadInt32();
            var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            if (stream.Position != stream.Length) throw new InvalidDataException("RaceWire: unread bytes after a remote position.");
            return position;
        }
    }
}
