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
        private static readonly double StadiumLap = 2d * 140d + 2d * Math.PI * 40d;

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
                TrackDefinition.Segment.Arc(40f, 180f),
                TrackDefinition.Segment.Straight(30f), TrackDefinition.Segment.Straight(15f), TrackDefinition.Segment.Gap(16f),
                TrackDefinition.Segment.Straight(15f), TrackDefinition.Segment.Straight(secondStraightRest),
                TrackDefinition.Segment.Arc(40f, 180f),
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
        public void Gaps_Stadium_AreTheJumpGapOnAAndTheGrappleGapOnB()
        {
            List<(double Start, double End)> gaps = definition.Gaps();
            Assert.That(gaps.Count, Is.EqualTo(2));
            Assert.That(gaps[0].Start, Is.EqualTo(119d).Within(1e-3));
            Assert.That(gaps[0].End, Is.EqualTo(125d).Within(1e-3));
            double straightBStart = 140d + Math.PI * 40d;
            Assert.That(gaps[1].Start, Is.EqualTo(straightBStart + 45d).Within(1e-2));
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
        public void TryValidate_ArcTighterThanThirtyMetres_IsRejected()
        {
            var segments = Stadium();
            segments[8] = TrackDefinition.Segment.Arc(25f, 180f);
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
            // Straight B: 10 m of runway before the grapple gap instead of 45 m.
            segments[9] = TrackDefinition.Segment.Straight(0.1f);
            segments[10] = TrackDefinition.Segment.Straight(10f);
            segments[13] = TrackDefinition.Segment.Straight(84.9f);
            SetSegments(segments);
            Assert.That(Error(), Does.Contain("segment 11").And.Contain("flat straight before"));
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
            SetField("anchors", new List<TrackDefinition.Anchor> { new TrackDefinition.Anchor(318.66f, 0f, 0.5f) });
            Assert.That(Error(), Does.Contain("anchor 0"));
            SetField("anchors", new List<TrackDefinition.Anchor> { new TrackDefinition.Anchor(318.66f, 9f, 10f) });
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
            segments[8] = TrackDefinition.Segment.Arc(40f, 190f);
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

        [Test]
        public void CheckpointS_BadIndex_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => definition.CheckpointS(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => definition.StartSlot(1));
        }
    }
}
