using System;
using System.IO;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Track;
using UnityEngine;

namespace ProtoHarness.Net
{
    // A SimSnapshot as bytes: every field's exact bits, in a fixed order, tick first. Two snapshots are the
    // same state exactly when their bytes are equal, so the client compares what it predicted with what the
    // server sent by comparing arrays. Decoding is strict: a short or long buffer throws.
    public static class SimSnapshotCodec
    {
        public static byte[] Encode(in SimSnapshot snapshot)
        {
            using var stream = new MemoryStream(256);
            using var writer = new BinaryWriter(stream);
            writer.Write(snapshot.Tick);
            WriteRacer(writer, snapshot.Racer);
            Write(writer, snapshot.Position);
            WriteLaps(writer, snapshot.Circuit.Laps);
            writer.Write(snapshot.Circuit.LastS);
            writer.Flush();
            return stream.ToArray();
        }

        public static SimSnapshot Decode(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream);
            int tick = reader.ReadInt32();
            RacerState.Snapshot racer = ReadRacer(reader);
            Vector3 position = ReadVector(reader);
            var circuit = new CircuitRace.Snapshot { Laps = ReadLaps(reader), LastS = reader.ReadDouble() };
            if (stream.Position != stream.Length)
                throw new InvalidDataException($"SimSnapshotCodec: {stream.Length - stream.Position} unread bytes after a snapshot.");
            return new SimSnapshot(tick, racer, position, circuit);
        }

        // The tick a snapshot was taken at, without decoding the rest.
        public static int TickOf(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length < sizeof(int)) throw new InvalidDataException("SimSnapshotCodec: a snapshot holds at least its tick.");
            return BitConverter.ToInt32(bytes, 0);
        }

        public static bool SameState(byte[] a, byte[] b)
        {
            if (a == null || b == null) throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static void Write(BinaryWriter w, Vector3 v)
        {
            w.Write(v.x);
            w.Write(v.y);
            w.Write(v.z);
        }

        private static Vector3 ReadVector(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        private static void WriteRacer(BinaryWriter w, in RacerState.Snapshot s)
        {
            w.Write(s.Gauge);
            w.Write(s.SlingTicks);
            w.Write(s.SwingTicks);
            w.Write(s.SwingRadius);
            w.Write(s.GrappleEmpowered);
            w.Write(s.Health);
            w.Write(s.Hits);
            w.Write(s.Grapples);
            w.Write(s.DamageUntilTick);
            w.Write(s.AttackUntilTick);
            w.Write(s.NextAttackTick);
            Write(w, s.Velocity);
            w.Write(s.Steer);
            w.Write(s.Heading);
            w.Write(s.TurnRate);
            w.Write(s.JumpQueued);
            w.Write(s.CoyoteTime);
            w.Write(s.AnchorIndex);
            w.Write(s.RopeLength);
            w.Write(s.MissUntilTick);
            w.Write(s.Grounded);
        }

        private static RacerState.Snapshot ReadRacer(BinaryReader r) => new RacerState.Snapshot
        {
            Gauge = r.ReadSingle(),
            SlingTicks = r.ReadInt32(),
            SwingTicks = r.ReadInt32(),
            SwingRadius = r.ReadSingle(),
            GrappleEmpowered = r.ReadBoolean(),
            Health = r.ReadInt32(),
            Hits = r.ReadInt32(),
            Grapples = r.ReadInt32(),
            DamageUntilTick = r.ReadInt32(),
            AttackUntilTick = r.ReadInt32(),
            NextAttackTick = r.ReadInt32(),
            Velocity = ReadVector(r),
            Steer = r.ReadSingle(),
            Heading = r.ReadSingle(),
            TurnRate = r.ReadSingle(),
            JumpQueued = r.ReadBoolean(),
            CoyoteTime = r.ReadSingle(),
            AnchorIndex = r.ReadInt32(),
            RopeLength = r.ReadSingle(),
            MissUntilTick = r.ReadInt32(),
            Grounded = r.ReadBoolean(),
        };

        private static void WriteLaps(BinaryWriter w, in LapCounter.Snapshot s)
        {
            w.Write(s.Progress);
            w.Write(s.LastS);
            w.Write(s.Started);
            w.Write(s.Completed);
            w.Write(s.NextCheckpoint);
            w.Write(s.StartTick);
            WriteInts(w, s.LapEndTicks);
            WriteInts(w, s.CheckpointTicks);
        }

        private static LapCounter.Snapshot ReadLaps(BinaryReader r) => new LapCounter.Snapshot
        {
            Progress = r.ReadDouble(),
            LastS = r.ReadDouble(),
            Started = r.ReadBoolean(),
            Completed = r.ReadInt32(),
            NextCheckpoint = r.ReadInt32(),
            StartTick = r.ReadInt32(),
            LapEndTicks = ReadInts(r),
            CheckpointTicks = ReadInts(r),
        };

        // -1 stands for a null array, so a default snapshot survives the round trip as it is.
        private static void WriteInts(BinaryWriter w, int[] values)
        {
            if (values == null) { w.Write(-1); return; }
            w.Write(values.Length);
            for (int i = 0; i < values.Length; i++) w.Write(values[i]);
        }

        private static int[] ReadInts(BinaryReader r)
        {
            int count = r.ReadInt32();
            if (count < -1) throw new InvalidDataException($"SimSnapshotCodec: array length {count}.");
            if (count == -1) return null;
            var values = new int[count];
            for (int i = 0; i < count; i++) values[i] = r.ReadInt32();
            return values;
        }
    }
}
