using System;
using System.Collections.Generic;
using System.IO;
using ProtoHarness.ChainRush.Control;
using UnityEngine;

namespace ProtoHarness.Net
{
    // What goes over the wire. Input (client to server): the tick it is for and that tick's controls. State (server to
    // the racer it belongs to): an encoded SimSnapshot (SimSnapshotCodec), which carries its own tick. End (server to the
    // racer it belongs to, after its last state): the tick the run ended on, why, and the controls the server ran that tick
    // on. Remote (server to the other racer): where a racer was after a tick, for drawing it. Standings (server to every
    // racer, each time they change): every racer's line in the race (RaceStandings) and which of them the message is for.
    // Order (server to every racer, each time it changes): each running racer's place among those running (RaceOrder).
    // Go (server to every racer, once, when every racer has a client): the number of racers; the client counts down and starts.
    // Decoding is strict: a short or long message, an end reason or status this build does not know, or a standings line
    // RaceStandings or an order RaceOrder cannot make, throws.
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

        // Standings: the racer the message is for, the number of racers, then for each racer its status, end tick, place and
        // whether the place is final (7 bytes).
        public static byte[] EncodeStandings(int you, IReadOnlyList<RaceStandings.Entry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (entries.Count < 1 || entries.Count > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(entries), entries.Count, "RaceWire: 1 to 255 racers.");
            if (you < 0 || you >= entries.Count) throw new ArgumentOutOfRangeException(nameof(you), you, $"RaceWire: there are {entries.Count} racers.");
            using var stream = new MemoryStream(5 + entries.Count * 7);
            using var writer = new BinaryWriter(stream);
            writer.Write(you);
            writer.Write((byte)entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                RaceStandings.Entry e = entries[i];
                string problem = Problem(e, entries.Count, i);
                if (problem != null) throw new ArgumentException(problem, nameof(entries));
                writer.Write((byte)e.Status);
                writer.Write(e.EndTick);
                writer.Write((byte)e.Place);
                writer.Write(e.Final ? (byte)1 : (byte)0);
            }
            writer.Flush();
            return stream.ToArray();
        }

        public static RaceStandings.Entry[] DecodeStandings(byte[] bytes, out int you)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream);
            you = reader.ReadInt32();
            int count = reader.ReadByte();
            if (count < 1) throw new InvalidDataException("RaceWire: standings with no racers.");
            if (you < 0 || you >= count) throw new InvalidDataException($"RaceWire: standings for racer {you} of {count}.");
            var entries = new RaceStandings.Entry[count];
            for (int i = 0; i < count; i++)
            {
                var status = (RaceStandings.Status)reader.ReadByte();
                int endTick = reader.ReadInt32();
                int place = reader.ReadByte();
                byte final = reader.ReadByte();
                if (final > 1) throw new InvalidDataException($"RaceWire: racer {i}'s final flag is {final}.");
                entries[i] = new RaceStandings.Entry(status, endTick, place, final == 1);
                string problem = Problem(entries[i], count, i);
                if (problem != null) throw new InvalidDataException(problem);
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("RaceWire: unread bytes after the standings.");
            return entries;
        }

        // Order: the tick of their own runs the running racers were compared on (-1 for none), the number of racers, then each
        // racer's place among those running (RaceOrder, 0 = none; 1 byte).
        public static byte[] EncodeOrder(int tick, IReadOnlyList<int> places)
        {
            if (places == null) throw new ArgumentNullException(nameof(places));
            if (places.Count < 1 || places.Count > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(places), places.Count, "RaceWire: 1 to 255 racers.");
            string problem = OrderProblem(tick, places);
            if (problem != null) throw new ArgumentException(problem, nameof(places));
            using var stream = new MemoryStream(5 + places.Count);
            using var writer = new BinaryWriter(stream);
            writer.Write(tick);
            writer.Write((byte)places.Count);
            for (int i = 0; i < places.Count; i++) writer.Write((byte)places[i]);
            writer.Flush();
            return stream.ToArray();
        }

        public static int[] DecodeOrder(byte[] bytes, out int tick)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream);
            tick = reader.ReadInt32();
            int count = reader.ReadByte();
            if (count < 1) throw new InvalidDataException("RaceWire: an order with no racers.");
            var places = new int[count];
            for (int i = 0; i < count; i++) places[i] = reader.ReadByte();
            if (stream.Position != stream.Length) throw new InvalidDataException("RaceWire: unread bytes after an order.");
            string problem = OrderProblem(tick, places);
            if (problem != null) throw new InvalidDataException(problem);
            return places;
        }

        // Go: the number of racers in the race (1 byte).
        public static byte[] EncodeGo(int racers)
        {
            if (racers < 1 || racers > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(racers), racers, "RaceWire: 1 to 255 racers.");
            return new[] { (byte)racers };
        }

        public static int DecodeGo(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length != 1) throw new InvalidDataException($"RaceWire: a go is 1 byte, not {bytes.Length}.");
            if (bytes[0] < 1) throw new InvalidDataException("RaceWire: a go for no racers.");
            return bytes[0];
        }

        // What is wrong with an order RaceOrder cannot make, or null: places run 0 to the number of racers, and racers are
        // placed only once they have been compared on a tick.
        private static string OrderProblem(int tick, IReadOnlyList<int> places)
        {
            if (tick < -1) return $"RaceWire: an order compared on tick {tick}.";
            for (int i = 0; i < places.Count; i++)
            {
                if (places[i] < 0 || places[i] > places.Count) return $"RaceWire: racer {i}'s place is {places[i]} of {places.Count}.";
                if (places[i] > 0 && tick < 0) return $"RaceWire: racer {i} has place {places[i]}, but nobody was compared.";
            }
            return null;
        }

        // What is wrong with a line RaceStandings cannot make, or null. A racer still going has no end, no place and nothing
        // final; a fall has an end, no place and is final; a finish has an end and a place among the racers.
        private static string Problem(RaceStandings.Entry e, int count, int racer)
        {
            bool ok;
            switch (e.Status)
            {
                case RaceStandings.Status.Waiting:
                case RaceStandings.Status.Running:
                    ok = e.EndTick == -1 && e.Place == 0 && !e.Final;
                    break;
                case RaceStandings.Status.Failed:
                    ok = e.EndTick >= 1 && e.Place == 0 && e.Final;
                    break;
                case RaceStandings.Status.Finished:
                    ok = e.EndTick >= 1 && e.Place >= 1 && e.Place <= count;
                    break;
                default:
                    return $"RaceWire: racer {racer} has unknown status {(byte)e.Status}.";
            }
            return ok ? null : $"RaceWire: racer {racer}'s line cannot be (status {e.Status}, end tick {e.EndTick}, place {e.Place}, final {e.Final}).";
        }
    }
}
