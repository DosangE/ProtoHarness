using System;
using System.IO;
using NUnit.Framework;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.Net;
using UnityEngine;

namespace ProtoHarness.Tests.PlayMode
{
    // The parts of the two-racer race that need neither a scene nor a connection (DESIGN.md P3 C1): drawing another racer
    // from late positions, the position message, and the input log file the player processes read.
    [Category("Net")]
    public sealed class NetRemoteTests
    {
        private const float TicksPerSecond = 50f;

        // ---- drawing another racer ----------------------------------------------------------------

        [Test]
        public void Remote_DrawsBetweenPositions_ThreeTicksBehindTheNewest()
        {
            var remote = new RemoteInterpolator(delayTicks: 4f, maxLagTicks: 25f);
            Assert.That(remote.TryEvaluate(out _), Is.False, "Nothing to draw before a position arrives.");
            for (int tick = 1; tick <= 10; tick++) remote.Add(tick, new Vector3(tick * 2f, 0f, 0f));

            remote.Advance(0f, TicksPerSecond);
            Assert.That(remote.DisplayTick, Is.EqualTo(6f), "The clock starts the delay behind the newest position.");
            Assert.That(remote.TryEvaluate(out Vector3 p), Is.True);
            Assert.That(p.x, Is.EqualTo(12f).Within(1e-4f));

            // A new position lets the clock run on at the tick rate, and it interpolates between the two around it.
            remote.Add(11, new Vector3(22f, 0f, 0f));
            remote.Advance(0.01f, TicksPerSecond);
            Assert.That(remote.DisplayTick, Is.EqualTo(6.5f).Within(1e-4f));
            Assert.That(remote.TryEvaluate(out p), Is.True);
            Assert.That(p.x, Is.EqualTo(13f).Within(1e-4f), "Half way between tick 6 (x 12) and tick 7 (x 14).");
        }

        [Test]
        public void Remote_WhenNothingNewArrives_WaitsOnTheLastPositionInsteadOfRunningOn()
        {
            var remote = new RemoteInterpolator(delayTicks: 4f, maxLagTicks: 25f);
            for (int tick = 1; tick <= 10; tick++) remote.Add(tick, new Vector3(tick, 0f, 0f));
            remote.Advance(0f, TicksPerSecond);
            // Two seconds pass and no message comes: the clock must stop at the newest position minus the delay, not extrapolate.
            for (int i = 0; i < 100; i++) remote.Advance(0.02f, TicksPerSecond);
            Assert.That(remote.DisplayTick, Is.EqualTo(6f));
            Assert.That(remote.TryEvaluate(out Vector3 p), Is.True);
            Assert.That(p.x, Is.EqualTo(6f).Within(1e-4f));
        }

        [Test]
        public void Remote_AfterALongSilence_CatchesUpInOneStepToTheLongestLag()
        {
            var remote = new RemoteInterpolator(delayTicks: 4f, maxLagTicks: 25f);
            for (int tick = 1; tick <= 10; tick++) remote.Add(tick, new Vector3(tick, 0f, 0f));
            remote.Advance(0f, TicksPerSecond);
            Assert.That(remote.DisplayTick, Is.EqualTo(6f));

            // The next message is for tick 100: the clock cannot lag more than 25 ticks behind it.
            remote.Add(100, new Vector3(100f, 0f, 0f));
            remote.Advance(0.02f, TicksPerSecond);
            Assert.That(remote.DisplayTick, Is.EqualTo(75f), "Pulled up to newest - maxLag.");
            Assert.That(remote.TryEvaluate(out Vector3 p), Is.True);
            Assert.That(p.x, Is.InRange(10f, 100f));
        }

        [Test]
        public void Remote_RefusesPositionsOutOfOrderAndBadSettings()
        {
            var remote = new RemoteInterpolator();
            remote.Add(5, Vector3.zero);
            Assert.Throws<ArgumentException>(() => remote.Add(5, Vector3.zero), "The same tick twice.");
            Assert.Throws<ArgumentException>(() => remote.Add(4, Vector3.zero), "An older tick.");
            Assert.Throws<ArgumentOutOfRangeException>(() => new RemoteInterpolator(-1f, 25f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RemoteInterpolator(float.NaN, 25f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RemoteInterpolator(10f, 5f), "The longest lag cannot be shorter than the delay.");
        }

        [Test]
        public void Remote_KeepsOnlyWhatTheClockCanStillReach()
        {
            var remote = new RemoteInterpolator(delayTicks: 2f, maxLagTicks: 10f);
            for (int tick = 1; tick <= 500; tick++)
            {
                remote.Add(tick, new Vector3(tick, 0f, 0f));
                remote.Advance(0.02f, TicksPerSecond);
            }
            Assert.That(remote.Count, Is.LessThan(40), "Old positions must be dropped as the clock moves on.");
            Assert.That(remote.TryEvaluate(out Vector3 p), Is.True);
            Assert.That(p.x, Is.EqualTo(498f).Within(1e-3f));
        }

        // ---- the position message ----------------------------------------------------------------

        [Test]
        public void Wire_RemotePositionRoundTripsAndABadLengthThrows()
        {
            byte[] bytes = RaceWire.EncodeRemote(321, new Vector3(1.5f, -2.25f, 3000.125f));
            Vector3 back = RaceWire.DecodeRemote(bytes, out int tick);
            Assert.That(tick, Is.EqualTo(321));
            Assert.That(back, Is.EqualTo(new Vector3(1.5f, -2.25f, 3000.125f)));
            var shortBytes = new byte[bytes.Length - 1];
            Array.Copy(bytes, shortBytes, shortBytes.Length);
            Assert.Throws<EndOfStreamException>(() => RaceWire.DecodeRemote(shortBytes, out _));
            var longBytes = new byte[bytes.Length + 1];
            Array.Copy(bytes, longBytes, bytes.Length);
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeRemote(longBytes, out _));
        }

        // ---- the input codec and the input log file -------------------------------------------

        [Test]
        public void InputCodec_EveryCombinationOfTheFiveButtonsRoundTrips_AndAnUnknownBitThrows()
        {
            for (int bits = 0; bits < 32; bits++)
            {
                var input = new TickInput(-0.5f, (bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0, (bits & 16) != 0);
                byte flags = TickInputCodec.ToFlags(input);
                Assert.That(flags, Is.EqualTo((byte)bits));
                TickInput back = TickInputCodec.FromFlags(-0.5f, flags);
                Assert.That(TickInputCodec.ToFlags(back), Is.EqualTo(flags));
                Assert.That(back.Steer, Is.EqualTo(-0.5f));
            }
            Assert.Throws<InvalidDataException>(() => TickInputCodec.FromFlags(0f, 32));
            Assert.Throws<InvalidDataException>(() => TickInputCodec.FromFlags(0f, 0x80));
            Assert.Throws<ArgumentOutOfRangeException>(() => TickInputCodec.FromFlags(1.5f, 0), "A steer outside [-1, 1] is refused.");
            Assert.Throws<ArgumentOutOfRangeException>(() => TickInputCodec.FromFlags(float.NaN, 0));
        }

        [Test]
        public void InputLogFile_RoundTripsALogAndRefusesAnythingElse()
        {
            var log = new InputLog();
            for (int i = 0; i < 300; i++)
                log.Add(new TickInput(Mathf.Sin(i * 0.1f), i % 7 == 0, i % 11 == 0, i % 13 == 0, i % 3 == 0, i % 5 == 0));
            byte[] bytes = InputLogFile.Encode(log);
            Assert.That(bytes.Length, Is.EqualTo(12 + 300 * 5));
            InputLog back = InputLogFile.Decode(bytes);
            Assert.That(back.Count, Is.EqualTo(300));
            for (int i = 0; i < 300; i++)
            {
                Assert.That(BitConverter.SingleToInt32Bits(back[i].Steer), Is.EqualTo(BitConverter.SingleToInt32Bits(log[i].Steer)), $"steer of tick {i + 1}");
                Assert.That(TickInputCodec.ToFlags(back[i]), Is.EqualTo(TickInputCodec.ToFlags(log[i])), $"buttons of tick {i + 1}");
            }
            Assert.That(InputLogFile.Decode(InputLogFile.Encode(new InputLog())).Count, Is.EqualTo(0), "An empty log is a valid file.");

            Assert.Throws<InvalidDataException>(() => InputLogFile.Decode(new byte[5]), "Shorter than the header.");
            byte[] wrongMagic = (byte[])bytes.Clone();
            wrongMagic[0] = (byte)'X';
            Assert.Throws<InvalidDataException>(() => InputLogFile.Decode(wrongMagic));
            byte[] wrongVersion = (byte[])bytes.Clone();
            wrongVersion[4] = 9;
            Assert.Throws<InvalidDataException>(() => InputLogFile.Decode(wrongVersion));
            var truncated = new byte[bytes.Length - 1];
            Array.Copy(bytes, truncated, truncated.Length);
            Assert.Throws<InvalidDataException>(() => InputLogFile.Decode(truncated));
            var padded = new byte[bytes.Length + 1];
            Array.Copy(bytes, padded, bytes.Length);
            Assert.Throws<InvalidDataException>(() => InputLogFile.Decode(padded));
            byte[] badButton = (byte[])bytes.Clone();
            badButton[12 + 4] = 0x40;
            Assert.Throws<InvalidDataException>(() => InputLogFile.Decode(badButton));
            byte[] nanSteer = (byte[])bytes.Clone();
            BitConverter.GetBytes(float.NaN).CopyTo(nanSteer, 12);
            Assert.Throws<ArgumentOutOfRangeException>(() => InputLogFile.Decode(nanSteer));
        }

        [Test]
        public void InputLogFile_SavesToDiskAndLoadsBack()
        {
            string path = Path.Combine(Path.GetTempPath(), "ProtoHarness-InputLogFile-test.bin");
            try
            {
                var log = new InputLog();
                for (int i = 0; i < 50; i++) log.Add(new TickInput(i / 50f, i % 2 == 0, false, false));
                InputLogFile.Save(path, log);
                InputLog back = InputLogFile.Load(path);
                Assert.That(back.Count, Is.EqualTo(50));
                Assert.That(back[49].Steer, Is.EqualTo(49 / 50f));
                Assert.Throws<FileNotFoundException>(() => InputLogFile.Load(path + ".missing"));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
