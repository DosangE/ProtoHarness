using System;
using System.IO;
using NUnit.Framework;
using ProtoHarness.ChainRush.Flow;
using UnityEngine;

namespace ProtoHarness.Tests.EditMode
{
    // A run's path for its ghost (M4, DECISIONS 2026-10-29 "M4 기록 키·고스트 형식"): bytes that come back bit for bit and
    // refuse anything else, and the files that keep one per track and speed class (in a folder of the tests' own).
    public sealed class GhostTraceTests
    {
        private const string Track = "TestTrack";
        private GhostFiles files;

        [SetUp]
        public void SetUp()
        {
            files = new GhostFiles(Path.Combine(Path.GetTempPath(), "ProtoHarnessTests", "GhostTraceTests"));
            DeleteFolder();
        }

        [TearDown]
        public void TearDown() => DeleteFolder();

        private void DeleteFolder()
        {
            if (Directory.Exists(files.Folder)) Directory.Delete(files.Folder, true);
        }

        private static GhostTrace ThreeTicks()
        {
            var trace = new GhostTrace();
            trace.Add(new Vector3(1.5f, 0.25f, -3f), 0f);
            trace.Add(new Vector3(1.75f, 0.3f, -2.6f), 12.5f);
            trace.Add(new Vector3(-1e-7f, 12f, 1482.8f), -179.99f);
            return trace;
        }

        [Test]
        public void EncodeDecode_RoundTripsEveryValueExactly()
        {
            GhostTrace trace = ThreeTicks();
            GhostTrace back = GhostTrace.Decode(GhostTrace.Encode(trace));
            Assert.That(back.Count, Is.EqualTo(trace.Count));
            for (int i = 0; i < trace.Count; i++)
            {
                Assert.That(back[i].Position, Is.EqualTo(trace[i].Position), "tick " + (i + 1));
                Assert.That(back[i].Heading, Is.EqualTo(trace[i].Heading), "tick " + (i + 1));
            }
        }

        [Test]
        public void Encode_IsTwelveBytesThenSixteenPerTick()
        {
            Assert.That(GhostTrace.Encode(ThreeTicks()).Length, Is.EqualTo(12 + 3 * 16));
            Assert.That(GhostTrace.Decode(GhostTrace.Encode(new GhostTrace())).Count, Is.Zero);
        }

        [Test]
        public void Decode_AnythingElse_Throws()
        {
            byte[] good = GhostTrace.Encode(ThreeTicks());
            Assert.Throws<ArgumentNullException>(() => GhostTrace.Decode(null));
            Assert.Throws<InvalidDataException>(() => GhostTrace.Decode(new byte[11]), "shorter than the header");

            byte[] header = (byte[])good.Clone();
            header[0] = (byte)'X';
            Assert.Throws<InvalidDataException>(() => GhostTrace.Decode(header), "wrong header");

            byte[] version = (byte[])good.Clone();
            version[4] = 9;
            Assert.Throws<InvalidDataException>(() => GhostTrace.Decode(version), "another version");

            var cut = new byte[good.Length - 1];
            Array.Copy(good, cut, cut.Length);
            Assert.Throws<InvalidDataException>(() => GhostTrace.Decode(cut), "a cut-off tick");

            byte[] nan = (byte[])good.Clone();
            BitConverter.GetBytes(float.NaN).CopyTo(nan, 12);
            Assert.Throws<InvalidDataException>(() => GhostTrace.Decode(nan), "a non-finite position");
        }

        [Test]
        public void Add_NonFinite_Throws()
        {
            var trace = new GhostTrace();
            Assert.Throws<ArgumentException>(() => trace.Add(new Vector3(float.PositiveInfinity, 0f, 0f), 0f));
            Assert.Throws<ArgumentException>(() => trace.Add(Vector3.zero, float.NaN));
            Assert.That(trace.Count, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = trace[0]);
        }

        [Test]
        public void Files_SaveLoadDelete_PerTrackAndSpeed()
        {
            Assert.That(files.TryLoad(Track, SpeedClass.Fast, out _), Is.False, "No ghost yet.");
            files.Save(Track, SpeedClass.Fast, ThreeTicks());
            Assert.That(files.TryLoad(Track, SpeedClass.Fast, out GhostTrace loaded), Is.True);
            Assert.That(loaded.Count, Is.EqualTo(3));
            Assert.That(loaded[2].Position, Is.EqualTo(new Vector3(-1e-7f, 12f, 1482.8f)));
            Assert.That(files.TryLoad(Track, SpeedClass.Normal, out _), Is.False);
            Assert.That(files.TryLoad("OtherTrack", SpeedClass.Fast, out _), Is.False);
            Assert.That(File.Exists(Path.Combine(files.Folder, "TestTrack.Fast.ghost")), Is.True, "Named by track and speed.");
            files.Delete(Track, SpeedClass.Fast);
            Assert.That(files.TryLoad(Track, SpeedClass.Fast, out _), Is.False);
        }

        [Test]
        public void Files_ABrokenFile_ThrowsRatherThanReadingAsNone()
        {
            files.Save(Track, SpeedClass.Fast, ThreeTicks());
            string path = Directory.GetFiles(files.Folder)[0];
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            Assert.Throws<InvalidDataException>(() => files.TryLoad(Track, SpeedClass.Fast, out _));
        }

        [Test]
        public void Files_Arguments_Invalid_Throw()
        {
            Assert.Throws<ArgumentException>(() => new GhostFiles(""));
            Assert.Throws<ArgumentException>(() => files.TryLoad("", SpeedClass.Fast, out _));
            Assert.Throws<ArgumentException>(() => files.TryLoad("a/b", SpeedClass.Fast, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => files.TryLoad(Track, (SpeedClass)7, out _));
            Assert.Throws<ArgumentException>(() => files.Save(Track, SpeedClass.Fast, new GhostTrace()), "an empty ghost");
            Assert.Throws<ArgumentNullException>(() => files.Save(Track, SpeedClass.Fast, null));
        }
    }
}
