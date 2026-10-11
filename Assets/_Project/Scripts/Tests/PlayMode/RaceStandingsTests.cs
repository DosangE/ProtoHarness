using System;
using System.IO;
using NUnit.Framework;
using ProtoHarness.Net;

namespace ProtoHarness.Tests.PlayMode
{
    // The race's standings as the server keeps them (DESIGN.md P3 C1c-2), and the message that carries them. Places go by
    // the ticks each run took from its own start; a place stays provisional while another racer could still finish in
    // fewer ticks. Needs neither a scene nor a connection.
    [Category("Net")]
    public sealed class RaceStandingsTests
    {
        private const RaceWire.EndReason Finished = RaceWire.EndReason.Finished;
        private const RaceWire.EndReason Failed = RaceWire.EndReason.Failed;

        [Test]
        public void Standings_TheFirstToFinishOnTicks_IsFirst_AndFinalOnlyOnceTheOtherHasRunPastThatTick()
        {
            var s = new RaceStandings(2);
            Assert.That(RaceStandings.Describe(s.ToArray()), Is.EqualTo("0=Waiting 1=Waiting"));
            Assert.That(s.Start(0), Is.True);
            Assert.That(s.Start(1), Is.True);
            s.Advance(0, 7923);
            s.Advance(1, 7000);
            Assert.That(s.End(0, 7924, Finished), Is.True);
            Assert.That(RaceStandings.Describe(s.ToArray()), Is.EqualTo("0=Finished@7924#1? 1=Running"), "Racer 1 could still finish in fewer ticks.");
            Assert.That(s[0].Final, Is.False);

            int version = s.Version;
            Assert.That(s.Advance(1, 7923), Is.False, "Past 7923 and still going, racer 1 can still finish on 7924 and share the place.");
            Assert.That(s.Version, Is.EqualTo(version));
            Assert.That(s.Advance(1, 7924), Is.True, "Past 7924 and still going, racer 1 cannot finish in 7924 ticks or fewer.");
            Assert.That(s.Version, Is.EqualTo(version + 1));
            Assert.That(RaceStandings.Describe(s.ToArray()), Is.EqualTo("0=Finished@7924#1 1=Running"));

            Assert.That(s.End(1, 8100, Finished), Is.True);
            Assert.That(RaceStandings.Describe(s.ToArray()), Is.EqualTo("0=Finished@7924#1 1=Finished@8100#2"));
            Assert.That(s[1].Final, Is.True, "Nobody is left to beat it.");
        }

        [Test]
        public void Standings_ALaterStarterThatFinishesInFewerTicks_TakesAProvisionalFirstPlace()
        {
            var s = new RaceStandings(2);
            s.Start(0);
            s.Advance(0, 7923);
            s.End(0, 7924, Finished);
            // Racer 1 started later in real time, so it is behind in ticks when racer 0 finishes.
            s.Start(1);
            s.Advance(1, 6000);
            Assert.That(s[0].Place, Is.EqualTo(1));
            Assert.That(s[0].Final, Is.False);
            s.Advance(1, 7899);
            Assert.That(s.End(1, 7900, Finished), Is.True);
            Assert.That(RaceStandings.Describe(s.ToArray()), Is.EqualTo("0=Finished@7924#2 1=Finished@7900#1"));
            Assert.That(s[0].Final && s[1].Final, Is.True);
        }

        [Test]
        public void Standings_EqualTicks_ShareAPlace_AndTheNextPlaceIsSkipped()
        {
            var s = new RaceStandings(3);
            for (int i = 0; i < 3; i++) s.Start(i);
            s.End(0, 500, Finished);
            s.End(1, 500, Finished);
            s.Advance(2, 499);
            Assert.That(RaceStandings.Describe(s.ToArray()), Is.EqualTo("0=Finished@500#1? 1=Finished@500#1? 2=Running"));
            s.End(2, 500, Finished);
            Assert.That(RaceStandings.Describe(s.ToArray()), Is.EqualTo("0=Finished@500#1 1=Finished@500#1 2=Finished@500#1"));

            var t = new RaceStandings(3);
            for (int i = 0; i < 3; i++) t.Start(i);
            t.End(0, 500, Finished);
            t.End(1, 500, Finished);
            t.End(2, 600, Finished);
            Assert.That(RaceStandings.Describe(t.ToArray()), Is.EqualTo("0=Finished@500#1 1=Finished@500#1 2=Finished@600#3"));
        }

        [Test]
        public void Standings_ARacerStillWaiting_KeepsEveryPlaceProvisional()
        {
            var s = new RaceStandings(2);
            s.Start(0);
            s.End(0, 300, Finished);
            Assert.That(RaceStandings.Describe(s.ToArray()), Is.EqualTo("0=Finished@300#1? 1=Waiting"), "Racer 1 has not even started.");
            Assert.That(s.Start(1), Is.True, "A start is a change.");
            Assert.That(s[0].Final, Is.False, "Just started, racer 1 has run past tick 0 only.");
            s.Advance(1, 300);
            Assert.That(s[0].Final, Is.True);
        }

        [Test]
        public void Standings_AFall_HasNoPlace_IsFinal_AndDoesNotHoldBackAFinisher()
        {
            var s = new RaceStandings(2);
            s.Start(0);
            s.Start(1);
            s.Advance(1, 656);
            Assert.That(s.End(1, 657, Failed), Is.True);
            Assert.That(RaceStandings.Describe(s.ToArray()), Is.EqualTo("0=Running 1=Failed@657"));
            Assert.That(s[1].Place, Is.EqualTo(0));
            Assert.That(s[1].Final, Is.True);
            s.End(0, 7924, Finished);
            Assert.That(RaceStandings.Describe(s.ToArray()), Is.EqualTo("0=Finished@7924#1 1=Failed@657"), "A fall cannot finish, so the place is final at once.");
        }

        [Test]
        public void Standings_TicksThatChangeNoLine_AreNotAChange()
        {
            var s = new RaceStandings(2);
            s.Start(0);
            s.Start(1);
            int version = s.Version;
            for (int tick = 1; tick <= 50; tick++)
            {
                Assert.That(s.Advance(0, tick), Is.False);
                Assert.That(s.Advance(1, tick), Is.False);
            }
            Assert.That(s.Advance(1, 50), Is.False, "The same tick again.");
            Assert.That(s.Version, Is.EqualTo(version), "Running on with nobody finished changes no line.");
        }

        [Test]
        public void Standings_EventsOutOfOrder_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RaceStandings(0));
            var s = new RaceStandings(2);
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Start(2), "No such racer.");
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Start(-1));
            Assert.Throws<InvalidOperationException>(() => s.Advance(0, 1), "Not started.");
            Assert.Throws<InvalidOperationException>(() => s.End(0, 1, Failed), "Not started.");
            s.Start(0);
            Assert.Throws<InvalidOperationException>(() => s.Start(0), "Started twice.");
            s.Advance(0, 10);
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Advance(0, 9), "Ticks never go back.");
            Assert.Throws<ArgumentOutOfRangeException>(() => s.End(0, 10, Failed), "It already ran past tick 10 and was still going.");
            Assert.Throws<ArgumentOutOfRangeException>(() => s.End(0, 11, (RaceWire.EndReason)7));
            s.End(0, 11, Failed);
            Assert.Throws<InvalidOperationException>(() => s.End(0, 12, Finished), "Ended twice.");
            Assert.Throws<InvalidOperationException>(() => s.Advance(0, 12), "An ended run does not advance.");
            Assert.That(RaceStandings.Describe(s.ToArray()), Is.EqualTo("0=Failed@11 1=Waiting"), "A refused event changes nothing.");
        }

        // ---- the standings message ---------------------------------------------------------------

        [Test]
        public void Wire_StandingsRoundTrip_ForEachRacer()
        {
            var s = new RaceStandings(4);
            for (int i = 0; i < 3; i++) s.Start(i);
            s.End(0, 7924, Finished);
            s.End(1, 657, Failed);
            s.Advance(2, 100);
            RaceStandings.Entry[] entries = s.ToArray();
            Assert.That(RaceStandings.Describe(entries), Is.EqualTo("0=Finished@7924#1? 1=Failed@657 2=Running 3=Waiting"));
            for (int you = 0; you < 4; you++)
            {
                byte[] bytes = RaceWire.EncodeStandings(you, entries);
                Assert.That(bytes.Length, Is.EqualTo(5 + 4 * 7));
                RaceStandings.Entry[] back = RaceWire.DecodeStandings(bytes, out int decodedYou);
                Assert.That(decodedYou, Is.EqualTo(you));
                Assert.That(RaceStandings.Describe(back), Is.EqualTo(RaceStandings.Describe(entries)));
                for (int i = 0; i < 4; i++)
                {
                    Assert.That(back[i].Status, Is.EqualTo(entries[i].Status), $"racer {i}");
                    Assert.That(back[i].EndTick, Is.EqualTo(entries[i].EndTick), $"racer {i}");
                    Assert.That(back[i].Place, Is.EqualTo(entries[i].Place), $"racer {i}");
                    Assert.That(back[i].Final, Is.EqualTo(entries[i].Final), $"racer {i}");
                }
            }
        }

        [Test]
        public void Wire_CorruptStandings_Throw()
        {
            var s = new RaceStandings(2);
            s.Start(0);
            s.Start(1);
            s.End(0, 900, Finished);
            byte[] bytes = RaceWire.EncodeStandings(1, s.ToArray());
            // Layout: you (4) | count (1) | per racer: status (1), end tick (4), place (1), final (1).
            const int racer0 = 5;
            const int racer1 = 5 + 7;

            var shortBytes = new byte[bytes.Length - 1];
            Array.Copy(bytes, shortBytes, shortBytes.Length);
            Assert.Throws<EndOfStreamException>(() => RaceWire.DecodeStandings(shortBytes, out _));
            var longBytes = new byte[bytes.Length + 1];
            Array.Copy(bytes, longBytes, bytes.Length);
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeStandings(longBytes, out _));

            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeStandings(With(bytes, 0, 2), out _), "Racer 2 of 2.");
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeStandings(With(bytes, 4, 0), out _), "No racers.");
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeStandings(With(bytes, racer1, 9), out _), "Unknown status.");
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeStandings(With(bytes, racer0 + 6, 2), out _), "A final flag of 2.");
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeStandings(With(bytes, racer0 + 5, 0), out _), "A finish with no place.");
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeStandings(With(bytes, racer0 + 5, 3), out _), "Third of two.");
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeStandings(With(bytes, racer1 + 5, 1), out _), "A racer still going with a place.");
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeStandings(With(bytes, racer1 + 6, 1), out _), "A racer still going cannot be final.");
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeStandings(With(bytes, racer1 + 1, 5), out _), "A racer still going has no end tick.");
            Assert.Throws<InvalidDataException>(() => RaceWire.DecodeStandings(With(bytes, racer0, 3), out _), "A fall has no place.");

            Assert.Throws<ArgumentOutOfRangeException>(() => RaceWire.EncodeStandings(2, s.ToArray()), "Racer 2 of 2.");
            Assert.Throws<ArgumentOutOfRangeException>(() => RaceWire.EncodeStandings(0, new RaceStandings.Entry[0]));
            Assert.Throws<ArgumentNullException>(() => RaceWire.EncodeStandings(0, null));
            Assert.Throws<ArgumentException>(() => RaceWire.EncodeStandings(0, new[] { new RaceStandings.Entry(RaceStandings.Status.Running, 5, 0, false) }));
            Assert.Throws<ArgumentNullException>(() => RaceWire.DecodeStandings(null, out _));
        }

        private static byte[] With(byte[] bytes, int index, byte value)
        {
            var copy = (byte[])bytes.Clone();
            copy[index] = value;
            return copy;
        }
    }
}
