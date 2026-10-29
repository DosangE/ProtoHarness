using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ProtoHarness.ChainRush.Flow
{
    // The path of one run for its ghost (M4, DECISIONS 2026-10-29 "M4 기록 키·고스트 형식"): where the runner was and
    // which way it faced after every tick, entry i after tick i + 1 (ticks count from 1 after StartRun). A path, not the
    // inputs: a ghost keeps the run it came from even after the simulation is tuned. As bytes: "PHGT", a version, the
    // number of ticks, then each tick's position (three floats) and heading (a float, world yaw in degrees as
    // RacerState.Heading). Decoding is strict like InputLogFile: a wrong header, a wrong length or a non-finite value throws.
    public sealed class GhostTrace
    {
        public readonly struct Sample
        {
            public Vector3 Position { get; }
            public float Heading { get; }

            public Sample(Vector3 position, float heading)
            {
                Position = position;
                Heading = heading;
            }
        }

        private static readonly byte[] Magic = { (byte)'P', (byte)'H', (byte)'G', (byte)'T' };
        private const int Version = 1;
        private const int HeaderBytes = 12;
        private const int TickBytes = 16;

        // A three-lap stadium run is about 4000 ticks; room for that without growing mid-race.
        private readonly List<Sample> samples = new List<Sample>(8192);

        public int Count => samples.Count;

        public Sample this[int index]
        {
            get
            {
                if (index < 0 || index >= samples.Count) throw new ArgumentOutOfRangeException(nameof(index), index, $"The trace holds {samples.Count} ticks.");
                return samples[index];
            }
        }

        public void Add(Vector3 position, float heading)
        {
            if (!IsFinite(position) || !float.IsFinite(heading))
                throw new ArgumentException($"GhostTrace: tick {samples.Count + 1} is not finite ({position}, {heading}).");
            samples.Add(new Sample(position, heading));
        }

        public void Clear() => samples.Clear();

        public static byte[] Encode(GhostTrace trace)
        {
            if (trace == null) throw new ArgumentNullException(nameof(trace));
            using var stream = new MemoryStream(HeaderBytes + trace.Count * TickBytes);
            using var writer = new BinaryWriter(stream);
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(trace.Count);
            for (int i = 0; i < trace.Count; i++)
            {
                Sample sample = trace.samples[i];
                writer.Write(sample.Position.x);
                writer.Write(sample.Position.y);
                writer.Write(sample.Position.z);
                writer.Write(sample.Heading);
            }
            writer.Flush();
            return stream.ToArray();
        }

        public static GhostTrace Decode(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length < HeaderBytes) throw new InvalidDataException($"GhostTrace: {bytes.Length} bytes is shorter than the {HeaderBytes}-byte header.");
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream);
            for (int i = 0; i < Magic.Length; i++)
                if (reader.ReadByte() != Magic[i]) throw new InvalidDataException("GhostTrace: not a ghost (wrong header).");
            int version = reader.ReadInt32();
            if (version != Version) throw new InvalidDataException($"GhostTrace: version {version}, this build reads {Version}.");
            int count = reader.ReadInt32();
            if (count < 0 || bytes.Length != HeaderBytes + (long)count * TickBytes)
                throw new InvalidDataException($"GhostTrace: the header says {count} ticks but the file holds {(bytes.Length - HeaderBytes) / (double)TickBytes:F2}.");
            var trace = new GhostTrace();
            for (int i = 0; i < count; i++)
            {
                var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                float heading = reader.ReadSingle();
                if (!IsFinite(position) || !float.IsFinite(heading))
                    throw new InvalidDataException($"GhostTrace: tick {i + 1} is not finite ({position}, {heading}).");
                trace.samples.Add(new Sample(position, heading));
            }
            return trace;
        }

        private static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
