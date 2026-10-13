using System;
using NUnit.Framework;
using ProtoHarness.Net;

namespace ProtoHarness.Tests.PlayMode
{
    // The lines a client's standings panel draws (RaceStandingsView, P3 C1c-2b), from RaceStandings as the server keeps them
    // and, from C1c-3, the order of the racers still running (RaceOrder).
    // Needs neither a scene nor a connection.
    [Category("Net")]
    public sealed class RaceStandingsTextTests
    {
        private const RaceWire.EndReason Finished = RaceWire.EndReason.Finished;
        private const RaceWire.EndReason Failed = RaceWire.EndReason.Failed;

        private static string Text(RaceStandings s, int you) => string.Join(" | ", Array.ConvertAll(RaceStandingsText.Lines(s.ToArray(), you), l => l.ToString()));

        [Test]
        public void Lines_EachStatus_HasItsWords()
        {
            var s = new RaceStandings(4);
            s.Start(1);
            s.Start(2);
            s.Start(3);
            s.Advance(1, 7923);
            s.End(1, 7924, Finished);
            s.Advance(2, 600);
            s.End(3, 657, Failed);
            // 7924 ticks of 0.02 s are 158.48 s.
            Assert.That(Text(s, 2), Is.EqualTo("1 RACER 1 02:38.5  잠정 | – YOU 주행 중 | – RACER 0 대기 | – RACER 3 낙하"));
        }

        [Test]
        public void Lines_APlace_ReadsFinalOnceNobodyCanBeatIt()
        {
            var s = new RaceStandings(2);
            s.Start(0);
            s.Start(1);
            s.End(0, 500, Finished);
            Assert.That(Text(s, 0), Is.EqualTo("1 YOU 00:10.0  잠정 | – RACER 1 주행 중"));
            s.Advance(1, 500);
            Assert.That(Text(s, 0), Is.EqualTo("1 YOU 00:10.0  확정 | – RACER 1 주행 중"));
        }

        [Test]
        public void Lines_Finishers_GoByPlaceNotByNumber_AndATieKeepsTheNumberOrder()
        {
            var s = new RaceStandings(3);
            for (int i = 0; i < 3; i++) s.Start(i);
            s.End(0, 900, Finished);
            s.End(2, 700, Finished);
            s.End(1, 700, Finished);
            RaceStandingsText.Line[] lines = RaceStandingsText.Lines(s.ToArray(), 0);
            Assert.That(Array.ConvertAll(lines, l => l.Racer), Is.EqualTo(new[] { 1, 2, 0 }));
            Assert.That(Text(s, 0), Is.EqualTo("1 RACER 1 00:14.0  확정 | 1 RACER 2 00:14.0  확정 | 3 YOU 00:18.0  확정"));
        }

        [Test]
        public void Lines_MarkOnlyTheOwnRacerAsYou()
        {
            var s = new RaceStandings(2);
            RaceStandingsText.Line[] lines = RaceStandingsText.Lines(s.ToArray(), 1);
            Assert.That(lines[0].IsYou, Is.False);
            Assert.That(lines[1].IsYou, Is.True);
            Assert.That(lines[1].Name, Is.EqualTo("YOU"));
        }

        [Test]
        public void Lines_WithoutStandingsOrWithAnUnknownRacer_Throw()
        {
            var s = new RaceStandings(2);
            Assert.Throws<ArgumentNullException>(() => RaceStandingsText.Lines(null, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => RaceStandingsText.Lines(s.ToArray(), -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => RaceStandingsText.Lines(s.ToArray(), 2));
        }

        // ---- with the order of the racers still running (P3 C1c-3) ----------------------------------

        private static string Text(RaceStandings s, int[] order, int you) => string.Join(" | ", Array.ConvertAll(RaceStandingsText.Lines(s.ToArray(), order, you), l => l.ToString()));

        [Test]
        public void Lines_WithAnOrder_PlaceRunningRacersAfterTheFinishers_ByIt()
        {
            var s = new RaceStandings(4);
            for (int i = 0; i < 4; i++) s.Start(i);
            s.End(1, 7924, Finished);
            s.End(3, 657, Failed);
            // Racer 2 is ahead of racer 0 among those running; the order no longer places the two that ended.
            Assert.That(Text(s, new[] { 2, 0, 1, 0 }, 0), Is.EqualTo("1 RACER 1 02:38.5  잠정 | 2 RACER 2 주행 중 | 3 YOU 주행 중 | – RACER 3 낙하"));
        }

        [Test]
        public void Lines_WithAnOrder_ATieSharesThePlace_AndKeepsTheNumberOrder()
        {
            var s = new RaceStandings(3);
            for (int i = 0; i < 3; i++) s.Start(i);
            Assert.That(Text(s, new[] { 2, 1, 1 }, 2), Is.EqualTo("1 RACER 1 주행 중 | 1 YOU 주행 중 | 3 RACER 0 주행 중"));
        }

        [Test]
        public void Lines_WhenTheOrderAndTheStandingsDisagree_GoByTheStandings()
        {
            var s = new RaceStandings(2);
            s.Start(0);
            s.Start(1);
            Assert.That(Text(s, new[] { 1, 0 }, 0), Is.EqualTo("1 YOU 주행 중 | – RACER 1 주행 중"), "Running, but not placed by the order yet.");
            s.End(0, 500, Finished);
            // The order still places racer 0, which the standings have finished.
            Assert.That(Text(s, new[] { 1, 2 }, 0), Is.EqualTo("1 YOU 00:10.0  잠정 | 2 RACER 1 주행 중"));
            Assert.That(Text(s, null, 0), Is.EqualTo(Text(s, 0)), "No order yet is the standings alone.");
        }

        [Test]
        public void Lines_WithAnOrderOfAnotherSize_Throw()
        {
            var s = new RaceStandings(2);
            Assert.Throws<ArgumentException>(() => RaceStandingsText.Lines(s.ToArray(), new[] { 1 }, 0));
            Assert.Throws<ArgumentException>(() => RaceStandingsText.Lines(s.ToArray(), new[] { 1, 1, 1 }, 0));
        }

        [Test]
        public void Header_CountsTheRacers()
        {
            Assert.That(RaceStandingsText.Header(1), Is.EqualTo("RACE  /  1 RACER"));
            Assert.That(RaceStandingsText.Header(2), Is.EqualTo("RACE  /  2 RACERS"));
        }
    }
}
