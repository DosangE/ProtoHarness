using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProtoHarness.ChainRush.Track;
using UnityEngine;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class TrackDefinitionTests
    {
        // R4c (2026-10-18): a square circuit, straights 140 / 60 / 140 / 60 m and four R22 quarter turns.
        private static readonly double StadiumLap = 2d * 140d + 2d * 60d + 2d * Math.PI * 22d;

        private TrackDefinition definition;

        [SetUp]
        public void SetUp() => definition = ScriptableObject.CreateInstance<TrackDefinition>();

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(definition);

        private void SetField(string name, object value)
        {
            FieldInfo field = typeof(TrackDefinition).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"no field {name}");
            field.SetValue(definition, value);
        }

        private void SetSegments(params TrackDefinition.Segment[] segments) => SetField("segments", new List<TrackDefinition.Segment>(segments));

        private static TrackDefinition.Segment[] Stadium(float secondStraightRest = 64f)
        {
            return new[]
            {
                TrackDefinition.Segment.Straight(15f), TrackDefinition.Segment.Straight(20f, 0.1f), TrackDefinition.Segment.Straight(40f, -0.1f),
                TrackDefinition.Segment.Straight(20f, 0f), TrackDefinition.Segment.Straight(9f), TrackDefinition.Segment.Straight(15f),
                TrackDefinition.Segment.Gap(6f), TrackDefinition.Segment.Straight(15f),
                TrackDefinition.Segment.Arc(22f, 90f),
                TrackDefinition.Segment.Straight(60f), TrackDefinition.Segment.Arc(22f, 90f),
                TrackDefinition.Segment.Straight(30f), TrackDefinition.Segment.Straight(15f), TrackDefinition.Segment.Gap(16f),
                TrackDefinition.Segment.Straight(15f), TrackDefinition.Segment.Straight(secondStraightRest),
                TrackDefinition.Segment.Arc(22f, 90f),
                TrackDefinition.Segment.Straight(60f), TrackDefinition.Segment.Arc(22f, 90f),
            };
        }

        private string Error()
        {
            Assert.That(definition.TryValidate(out string error), Is.False, "the definition should be rejected");
            return error;
        }

        [Test]
        public void Defaults_Stadium_IsValid()
        {
            Assert.That(definition.TryValidate(out string error), Is.True, error);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void Defaults_Stadium_LapLengthAndSettings()
        {
            Assert.That(definition.LapLength, Is.EqualTo(StadiumLap).Within(1e-3));
            Assert.That(definition.LapCount, Is.EqualTo(3));
            Assert.That(definition.CheckpointCount, Is.EqualTo(3));
            Assert.That(definition.CheckpointS(1), Is.EqualTo(StadiumLap * 0.5).Within(1e-3));
            Assert.That(definition.StartSlot(0), Is.Zero);
            Assert.That(definition.Anchors.Count, Is.EqualTo(1));
        }

        [Test]
        public void Gaps_Stadium_AreTheJumpGapOnAAndTheGrappleGapOnC()
        {
            List<(double Start, double End)> gaps = definition.Gaps();
            Assert.That(gaps.Count, Is.EqualTo(2));
            Assert.That(gaps[0].Start, Is.EqualTo(119d).Within(1e-3));
            Assert.That(gaps[0].End, Is.EqualTo(125d).Within(1e-3));
            double straightCStart = 140d + 60d + Math.PI * 22d;
            Assert.That(gaps[1].Start, Is.EqualTo(straightCStart + 45d).Within(1e-2));
            Assert.That(gaps[1].End - gaps[1].Start, Is.EqualTo(16d).Within(1e-3));
            Assert.That(definition.Anchors[0].S, Is.EqualTo(gaps[1].Start + 8d).Within(0.02), "The anchor hangs over the middle of the grapple gap.");
        }

        [Test]
        public void BuildCenterline_Stadium_ClosesAtTheLapLengthAndTheHillCloses()
        {
            Centerline line = definition.BuildCenterline(Vector3.zero, 0f);
            Assert.That(line.IsLoop, Is.True);
            Assert.That(line.EndS, Is.EqualTo(definition.LapLength).Within(1e-9), "S sums match the definition's lap length exactly.");
            float highest = 0f;
            for (double s = 0d; s < line.EndS; s += 1d) highest = Mathf.Max(highest, line.FrameAt(s).Position.y);
            Assert.That(highest, Is.EqualTo(2f).Within(0.1f), "The hill is about 2 m high.");
            Assert.That(line.FrameAt(line.EndS - 1e-3).Position.y, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void BuildCenterline_OriginAndHeading_StartThere()
        {
            Centerline line = definition.BuildCenterline(new Vector3(100f, 5f, -20f), 90f);
            TrackFrame start = line.FrameAt(0d);
            Assert.That(Vector3.Distance(start.Position, new Vector3(100f, 5f, -20f)), Is.LessThan(1e-3f));
            Assert.That(start.Forward.x, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(line.IsLoop, Is.True);
        }

        [Test]
        public void TryValidate_EndNotMeetingStart_SaysHowFarOffAndBuildThrows()
        {
            SetSegments(Stadium(secondStraightRest: 69f));
            string error = Error();
            Assert.That(error, Does.Contain("does not close").And.Contain("5.00 m"));
            Assert.Throws<InvalidOperationException>(() => definition.BuildCenterline(Vector3.zero, 0f));
        }

        [Test]
        public void TryValidate_ArcTighterThanTwentyMetres_IsRejected()
        {
            var segments = Stadium();
            segments[8] = TrackDefinition.Segment.Arc(15f, 90f);
            SetSegments(segments);
            Assert.That(Error(), Does.Contain("segment 8").And.Contain("radius"));
        }

        [Test]
        public void TryValidate_GapOnASlope_IsRejected()
        {
            var segments = Stadium();
            segments[5] = TrackDefinition.Segment.Straight(15f, 0.02f);
            SetSegments(segments);
            Assert.That(Error(), Does.Contain("flat"));
        }

        [Test]
        public void TryValidate_GapWithShortRunway_IsRejected()
        {
            var segments = Stadium();
            // Straight C: 10 m of runway before the grapple gap instead of 45 m.
            segments[11] = TrackDefinition.Segment.Straight(0.1f);
            segments[12] = TrackDefinition.Segment.Straight(10f);
            segments[15] = TrackDefinition.Segment.Straight(84.9f);
            SetSegments(segments);
            Assert.That(Error(), Does.Contain("segment 13").And.Contain("flat straight before"));
        }

        [Test]
        public void TryValidate_AnchorNotOverAGap_IsRejected()
        {
            SetField("anchors", new List<TrackDefinition.Anchor> { new TrackDefinition.Anchor(50f, 0f, 10f) });
            Assert.That(Error(), Does.Contain("anchor 0").And.Contain("not over a gap"));
        }

        [Test]
        public void TryValidate_AnchorTooLowOrOffTheRoad_IsRejected()
        {
            SetField("anchors", new List<TrackDefinition.Anchor> { new TrackDefinition.Anchor(322.12f, 0f, 0.5f) });
            Assert.That(Error(), Does.Contain("anchor 0"));
            SetField("anchors", new List<TrackDefinition.Anchor> { new TrackDefinition.Anchor(322.12f, 9f, 10f) });
            Assert.That(Error(), Does.Contain("anchor 0"));
        }

        [Test]
        public void TryValidate_CheckpointsOutOfOrder_AreRejected()
        {
            SetField("checkpointFractions", new[] { 0.5f, 0.25f });
            Assert.That(Error(), Does.Contain("checkpoint 1"));
            SetField("checkpointFractions", new[] { 0.5f, 1f });
            Assert.That(Error(), Does.Contain("checkpoint 1"));
        }

        [Test]
        public void TryValidate_LapCountAndStartSlots_AreChecked()
        {
            SetField("lapCount", 0);
            Assert.That(Error(), Does.Contain("lap count"));
            SetField("lapCount", 3);
            SetField("startSlots", new[] { -1f });
            Assert.That(Error(), Does.Contain("start slot 0"));
            SetField("startSlots", Array.Empty<float>());
            Assert.That(Error(), Does.Contain("start slot"));
        }

        [Test]
        public void TryValidate_NoCheckpoints_IsFine()
        {
            SetField("checkpointFractions", Array.Empty<float>());
            Assert.That(definition.TryValidate(out string error), Is.True, error);
        }

        [Test]
        public void TryValidate_TooFewOrTooShortASegmentList_IsRejected()
        {
            SetSegments(TrackDefinition.Segment.Straight(50f));
            Assert.That(Error(), Does.Contain("at least two"));
            SetSegments(TrackDefinition.Segment.Straight(20f), TrackDefinition.Segment.Straight(20f));
            Assert.That(Error(), Does.Contain("too short"));
        }

        [Test]
        public void TryValidate_ArcOver180Degrees_IsRejected()
        {
            var segments = Stadium();
            segments[8] = TrackDefinition.Segment.Arc(22f, 190f);
            SetSegments(segments);
            Assert.That(Error(), Does.Contain("segment 8"));
        }

        [Test]
        public void Segment_Arc_LengthMatchesCenterlineAppendArc()
        {
            var line = new Centerline(Vector3.zero, 0f);
            TrackDefinition.Segment.Arc(40f, 180f).AppendTo(line);
            Assert.That(line.EndS, Is.EqualTo((double)TrackDefinition.Segment.Arc(40f, 180f).Length));
        }

        // ---- road width and swing poles (user 2026-10-22, the Skyway's wide straight) ------------------------------

        [Test]
        public void HalfWidthAt_Stadium_IsTheRoadsEverywhereAndThereAreNoPoles()
        {
            for (double s = 0d; s < StadiumLap; s += 25d) Assert.That(definition.HalfWidthAt(s), Is.EqualTo(RoadProfile.DeckHalfWidth), "at S " + s);
            Assert.That(definition.WidestHalfWidth, Is.EqualTo(RoadProfile.DeckHalfWidth));
            Assert.That(definition.Poles, Is.Empty);
            Assert.That(definition.HalfWidthAt(StadiumLap + 10d), Is.EqualTo(RoadProfile.DeckHalfWidth), "S folds into the lap.");
        }

        // The 60 m straight B widens 6 -> 12, the next corner stays at 12, and straight C's first 30 m (no width of its own,
        // so the road's 6) narrows back.
        [Test]
        public void HalfWidthAt_ATaperedStretch_GoesLinearlyOverEachPiece()
        {
            var segments = Stadium();
            segments[9] = TrackDefinition.Segment.Straight(60f, 0f, 12f);
            segments[10] = TrackDefinition.Segment.Arc(22f, 90f, 0f, 12f);
            SetSegments(segments);
            Assert.That(definition.TryValidate(out string error), Is.True, error);
            double straightB = 140d + 11d * Math.PI;
            double straightC = straightB + 60d + 11d * Math.PI;
            Assert.That(definition.HalfWidthAt(straightB - 1d), Is.EqualTo(6f).Within(1e-4f));
            Assert.That(definition.HalfWidthAt(straightB + 30d), Is.EqualTo(9f).Within(1e-4f));
            Assert.That(definition.HalfWidthAt(straightB + 60d), Is.EqualTo(12f).Within(1e-4f));
            Assert.That(definition.HalfWidthAt(straightB + 80d), Is.EqualTo(12f).Within(1e-4f));
            Assert.That(definition.HalfWidthAt(straightC + 15d), Is.EqualTo(9f).Within(1e-4f));
            Assert.That(definition.HalfWidthAt(straightC + 31d), Is.EqualTo(6f).Within(1e-4f));
            Assert.That(definition.WidestHalfWidth, Is.EqualTo(12f));
            Assert.That(definition.StartHalfWidth(9), Is.EqualTo(6f));
            Assert.That(definition.EndHalfWidth(11), Is.EqualTo(6f), "A piece naming no width ends at the road's.");
        }

        [Test]
        public void TryValidate_WidthChangingTooFastOrOutOfRange_IsRejected()
        {
            var segments = Stadium();
            segments[4] = TrackDefinition.Segment.Straight(9f, 0f, 12f);
            SetSegments(segments);
            Assert.That(Error(), Does.Contain("segment 4").And.Contain("half width changes"));
            segments = Stadium();
            segments[9] = TrackDefinition.Segment.Straight(60f, 0f, TrackDefinition.MaxHalfWidth + 1f);
            SetSegments(segments);
            Assert.That(Error(), Does.Contain("segment 9").And.Contain("end half width"));
            segments[9] = TrackDefinition.Segment.Straight(60f, 0f, TrackDefinition.MinHalfWidth - 1f);
            SetSegments(segments);
            Assert.That(Error(), Does.Contain("segment 9").And.Contain("end half width"));
        }

        [Test]
        public void TryValidate_WidthChangingAcrossAGap_IsRejected()
        {
            var segments = Stadium();
            segments[12] = TrackDefinition.Segment.Straight(15f, 0f, 9f);
            SetSegments(segments);
            Assert.That(Error(), Does.Contain("segment 13").And.Contain("width across a gap"));
        }

        // A pole stands on the road at its S and offset, and the built centerline offers it like a corner pole.
        [Test]
        public void BuildCenterline_APole_StandsBesideTheRoadAndIsOffered()
        {
            SetField("poles", new List<TrackDefinition.Pole> { new TrackDefinition.Pole(100f, -4f, 10f) });
            Assert.That(definition.TryValidate(out string error), Is.True, error);
            Centerline line = definition.BuildCenterline(Vector3.zero, 0f);
            var poles = new List<Vector3>();
            line.GetPoles(poles);
            Assert.That(poles.Count, Is.EqualTo(5), "Four corner poles, then the added one.");
            Assert.That(Vector3.Distance(poles[4], new Vector3(-4f, 0f, 100f)), Is.LessThan(1e-3f), "Straight A runs up +z: 4 m left is -x.");
            Assert.That(line.TryFindPole(new Vector3(0f, 0f, 95f), 0f, 95f, 6f, out Vector3 pole), Is.True);
            Assert.That(Vector3.Distance(pole, poles[4]), Is.LessThan(1e-3f));
        }

        [Test]
        public void TryValidate_PoleOffTheRoadOutsideTheLapOrWithABadReach_IsRejected()
        {
            SetField("poles", new List<TrackDefinition.Pole> { new TrackDefinition.Pole(100f, 7f, 10f) });
            Assert.That(Error(), Does.Contain("pole 0").And.Contain("within the road"));
            SetField("poles", new List<TrackDefinition.Pole> { new TrackDefinition.Pole(100f, 0f, TrackDefinition.MinPoleReach - 1f) });
            Assert.That(Error(), Does.Contain("pole 0").And.Contain("reach"));
            SetField("poles", new List<TrackDefinition.Pole> { new TrackDefinition.Pole(-1f, 0f, 10f) });
            Assert.That(Error(), Does.Contain("pole 0").And.Contain("within the lap"));
            SetField("poles", new List<TrackDefinition.Pole> { new TrackDefinition.Pole((float)StadiumLap + 1f, 0f, 10f) });
            Assert.That(Error(), Does.Contain("pole 0").And.Contain("within the lap"));
        }

        // A kicker's lip is the end of its piece (user 2026-10-25, Finger Lanes): the built centerline offers it, with its
        // lift, to a runner crossing it forward, not backward, not short of it, and across the lap's seam too.
        [Test]
        public void BuildCenterline_AKicker_OffersItsLipToARunnerCrossingItForward()
        {
            var segments = Stadium();
            segments[0] = TrackDefinition.Segment.Kicker(15f, 0f, 0.3f);
            SetSegments(segments);
            Assert.That(definition.TryValidate(out string error), Is.True, error);
            Centerline line = definition.BuildCenterline(Vector3.zero, 0f);
            Assert.That(line.LaunchCount, Is.EqualTo(1));
            Assert.That(line.TryLaunchBetween(14.6d, 15.0d, out float lift), Is.True);
            Assert.That(lift, Is.EqualTo(0.3f));
            Assert.That(line.TryLaunchBetween(14.0d, 14.9d, out _), Is.False, "Short of the lip.");
            Assert.That(line.TryLaunchBetween(15.0d, 15.4d, out _), Is.False, "Already past it.");
            Assert.That(line.TryLaunchBetween(15.4d, 14.6d, out _), Is.False, "Backward.");
            line.AddLaunch(StadiumLap - 0.2d, 0.25f);
            Assert.That(line.TryLaunchBetween(StadiumLap - 0.5d, 0.3d, out lift), Is.True, "Across the seam.");
            Assert.That(lift, Is.EqualTo(0.25f));
        }

        [Test]
        public void TryValidate_LaunchOutOfRange_IsRejected()
        {
            var segments = Stadium();
            segments[0] = TrackDefinition.Segment.Kicker(15f, 0f, TrackDefinition.MaxLaunch + 1f);
            SetSegments(segments);
            Assert.That(Error(), Does.Contain("segment 0").And.Contain("launch"));
            segments[0] = TrackDefinition.Segment.Kicker(15f, 0f, -1f);
            SetSegments(segments);
            Assert.That(Error(), Does.Contain("segment 0").And.Contain("launch"));
            segments[0] = TrackDefinition.Segment.Kicker(15f, 0f, float.NaN);
            SetSegments(segments);
            Assert.That(Error(), Does.Contain("segment 0").And.Contain("launch"));
        }

        // A pad is inside its straight, not at its end as a kicker is. Crossing uses the same forward interval and loop
        // seam rules as the launch; a track without pads offers none.
        [Test]
        public void BuildCenterline_ABoostZone_OffersOnlyForwardCrossingsIncludingTheSeam()
        {
            Centerline plain = definition.BuildCenterline(Vector3.zero, 0f);
            Assert.That(plain.BoostZoneCount, Is.Zero);
            Assert.That(plain.TryBoostBetween(0d, 100d), Is.False);
            var segments = Stadium();
            segments[1] = TrackDefinition.Segment.BoostStraight(20f, 10f, 0.1f);
            SetSegments(segments);
            Assert.That(definition.TryValidate(out string error), Is.True, error);
            Centerline line = definition.BuildCenterline(Vector3.zero, 0f);
            Assert.That(line.BoostZoneCount, Is.EqualTo(1));
            Assert.That(line.TryBoostBetween(24.6d, 25d), Is.True, "The pad is 10 m inside the second straight.");
            Assert.That(line.TryBoostBetween(24d, 24.9d), Is.False, "Short of it.");
            Assert.That(line.TryBoostBetween(25d, 25.4d), Is.False, "Already passed.");
            Assert.That(line.TryBoostBetween(25.4d, 24.6d), Is.False, "Backward.");
            Assert.That(line.TryBoostBetween(24.6d, 24.6d), Is.False, "Nowhere.");
            line.AddBoostZone(line.EndS - 0.2d);
            Assert.That(line.TryBoostBetween(line.EndS - 0.5d, 0.3d), Is.True, "Across the seam, before it.");
            line.AddBoostZone(line.EndS + 0.1d);
            Assert.That(line.TryBoostBetween(line.EndS - 0.05d, 0.3d), Is.True, "The pad S wraps too.");
            var zones = new List<double> { -1d };
            line.GetBoostZones(zones);
            Assert.That(zones, Is.EqualTo(new[] { -1d, 25d, line.EndS - 0.2d, line.EndS + 0.1d }), "Append in pad order.");
            line.ShiftOrigin(new Vector3(100f, 5f, -20f));
            Assert.That(line.TryBoostBetween(24.6d, 25d), Is.True, "S stays through an origin shift.");
            line.Clear();
            Assert.That(line.BoostZoneCount, Is.Zero);
            Assert.That(line.TryBoostBetween(24.6d, 25d), Is.False, "Clear removes pads too.");
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(15f)]
        [TestCase(16f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TryValidate_BoostAtOutsideItsStraight_IsRejected(float at)
        {
            var segments = Stadium();
            segments[0] = TrackDefinition.Segment.BoostStraight(15f, at);
            SetSegments(segments);
            Assert.That(Error(), Does.Contain("segment 0").And.Contain("boost zone"));
        }

        // Serialized malformed pieces cannot be made by the factories: exercise the same fields the inspector saves.
        [TestCase("radius", 25f)]
        [TestCase("gap", true)]
        [TestCase("launch", 0.3f)]
        public void TryValidate_BoostOnAnArcGapOrKicker_IsRejected(string name, object value)
        {
            object malformed = TrackDefinition.Segment.BoostStraight(15f, 10f);
            FieldInfo field = typeof(TrackDefinition.Segment).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "no segment field " + name);
            field.SetValue(malformed, value);
            var segments = Stadium();
            segments[0] = (TrackDefinition.Segment)malformed;
            SetSegments(segments);
            Assert.That(Error(), Does.Contain("segment 0").And.Contain("boost zone"));
        }

        [Test]
        public void BoostZones_OnAnOpenLine_KeepTheirSAndRejectNonfinitePositions()
        {
            var line = new Centerline(Vector3.zero, 0f);
            line.AppendStraight(50f);
            line.AddBoostZone(25d);
            line.TrimBefore(10d);
            Assert.That(line.TryBoostBetween(24d, 26d), Is.True);
            Assert.That(line.TryBoostBetween(26d, 24d), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => line.AddBoostZone(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => line.AddBoostZone(double.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => line.GetBoostZones(null));
        }

        // Where a fall just after a pad comes back (RunnerMotor's respawn, Palm Coast's jump, 2026-10-30): the nearest pad
        // at or behind S within the reach, across a loop's seam too; none ahead, none farther back, none on a plain track.
        [Test]
        public void TryBoostZoneBehind_FindsTheNearestPadBackWithinTheReach()
        {
            Centerline plain = definition.BuildCenterline(Vector3.zero, 0f);
            Assert.That(plain.TryBoostZoneBehind(100d, 1000d, out _), Is.False);
            var segments = Stadium();
            segments[1] = TrackDefinition.Segment.BoostStraight(20f, 10f, 0.1f);
            SetSegments(segments);
            Centerline line = definition.BuildCenterline(Vector3.zero, 0f);
            line.AddBoostZone(15d);
            Assert.That(line.TryBoostZoneBehind(40d, 35d, out double pad), Is.True);
            Assert.That(pad, Is.EqualTo(25d), "The nearer of the two pads behind.");
            Assert.That(line.TryBoostZoneBehind(25d, 35d, out pad), Is.True, "A pad right at S counts.");
            Assert.That(pad, Is.EqualTo(25d));
            Assert.That(line.TryBoostZoneBehind(61d, 35d, out _), Is.False, "Both pads are more than 35 m back.");
            Assert.That(line.TryBoostZoneBehind(10d, 35d, out _), Is.False, "Pads ahead do not count.");
            line.AddBoostZone(line.EndS - 2d);
            Assert.That(line.TryBoostZoneBehind(3d, 35d, out pad), Is.True, "Back across the seam.");
            Assert.That(pad, Is.EqualTo(-2d).Within(1e-9d), "The pad's S on this side of the seam, so S minus a backoff stays on the lap behind.");
            Assert.Throws<ArgumentOutOfRangeException>(() => line.TryBoostZoneBehind(double.NaN, 35d, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => line.TryBoostZoneBehind(10d, -1d, out _));
        }

        // A shortcut across the stadium's infield (user 2026-10-25, Finger Lanes' nails): off straight A at S 96 (flat, after
        // the hill) a right quarter turn of R15, 74 m east, another one onto straight C at the same height (z 96), 44 m into
        // C. C starts at 140 + pi x 11 + 60 + pi x 11.
        private static readonly double InfieldShortcutTo = 244d + 22d * Math.PI;

        private void SetInfieldShortcut(float fromS = 96f, double toS = 0d)
        {
            SetField("shortcuts", new List<TrackDefinition.Shortcut>
            {
                new TrackDefinition.Shortcut(fromS, (float)(toS > 0d ? toS : InfieldShortcutTo), 15f, 74f, 3.6f),
            });
        }

        // Off the main road the shortcut's road takes over: its S runs from where it leaves to where it comes back in
        // proportion along it, and its frame faces along it. On the main road, even beside the junction, the loop answers.
        [Test]
        public void BuildCenterline_AShortcut_IsTheTrackOffTheMainRoadAndCarriesSAcross()
        {
            SetInfieldShortcut();
            Assert.That(definition.TryValidate(out string error), Is.True, error);
            Centerline line = definition.BuildCenterline(Vector3.zero, 0f);
            Assert.That(line.BranchCount, Is.EqualTo(1));
            // Halfway along the shortcut's straight: x 15 + 37, z 96 + 15.
            double along = Math.PI * 15d / 2d + 37d, length = Math.PI * 15d + 74d;
            TrackCoord middle = line.Project(new Vector3(52f, 0f, 111f));
            Assert.That(middle.S, Is.EqualTo(96d + along / length * (InfieldShortcutTo - 96d)).Within(0.01d));
            Assert.That(middle.D, Is.EqualTo(0f).Within(0.01f));
            Assert.That(Vector3.Distance(line.Frame(new Vector3(52f, 0f, 111f)).Forward, Vector3.right), Is.LessThan(1e-4f), "East along the straight.");
            TrackCoord beside = line.Project(new Vector3(5f, 0f, 104f));
            Assert.That(beside.S, Is.EqualTo(104d).Within(0.01d), "On the main road beside the junction: the loop's S.");
            Assert.That(beside.D, Is.EqualTo(5f).Within(0.01f));
            Assert.That(Vector3.Distance(line.Frame(new Vector3(5f, 0f, 104f)).Forward, Vector3.forward), Is.LessThan(1e-4f));
            var poles = new List<Vector3>();
            line.GetPoles(poles);
            Assert.That(poles.Count, Is.EqualTo(6), "Four corner poles, then the shortcut's two.");
            Assert.That(Vector3.Distance(poles[4], new Vector3(15f, 0f, 96f)), Is.LessThan(1e-3f));
            Assert.That(Vector3.Distance(poles[5], new Vector3(89f, 0f, 96f)), Is.LessThan(1e-3f));
        }

        // The main road's right guard opens over each crossing: there the shortcut's inner (R15 - 3.6) and outer (R15 + 3.6)
        // edge cross the main road's edge (6 m off its centerline, 9 m from the corner pole), widened by a guard either side.
        [Test]
        public void ShortcutGuardGaps_AreTheCrossingsOfTheTwoRoads()
        {
            SetInfieldShortcut();
            Assert.That(definition.TryValidate(out string error), Is.True, error);
            List<(double From, double To)> gaps = definition.ShortcutGuardGaps();
            double inner = Math.Sqrt(11.4d * 11.4d - 81d), outer = Math.Sqrt(18.6d * 18.6d - 81d);
            Assert.That(gaps.Count, Is.EqualTo(2));
            Assert.That(gaps[0].From, Is.EqualTo(96d + inner - 0.3d).Within(1e-3d));
            Assert.That(gaps[0].To, Is.EqualTo(96d + outer + 0.3d).Within(1e-3d));
            Assert.That(gaps[1].From, Is.EqualTo(InfieldShortcutTo - outer - 0.3d).Within(1e-3d));
            Assert.That(gaps[1].To, Is.EqualTo(InfieldShortcutTo - inner + 0.3d).Within(1e-3d));
        }

        [Test]
        public void TryValidate_ShortcutMissingTheRoadOrLeavingOverAHill_IsRejected()
        {
            SetInfieldShortcut(96f, InfieldShortcutTo - 3d);
            Assert.That(Error(), Does.Contain("shortcut 0").And.Contain("away from the main road"));
            SetInfieldShortcut(10f, InfieldShortcutTo - 86d);
            Assert.That(Error(), Does.Contain("shortcut 0").And.Contain("flat straight where it leaves"));
            SetField("shortcuts", new List<TrackDefinition.Shortcut> { new TrackDefinition.Shortcut(96f, (float)InfieldShortcutTo, 15f, 74f, 6f) });
            Assert.That(Error(), Does.Contain("shortcut 0").And.Contain("half width"));
        }

        [Test]
        public void CheckpointS_BadIndex_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => definition.CheckpointS(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => definition.StartSlot(1));
        }
    }
}
