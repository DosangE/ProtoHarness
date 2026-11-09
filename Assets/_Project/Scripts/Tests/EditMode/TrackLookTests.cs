using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush.Race;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProtoHarness.Tests.EditMode
{
    // A track look's lanes (TrackLook.LaneLines, user 2026-11-02 "차선 디테일"): lines a fixed distance apart from the
    // middle, so a narrowing road ends its outer lines and keeps the rest in place; a middle line from the look's half
    // width up.
    public sealed class TrackLookTests
    {
        private readonly List<Object> made = new List<Object>();
        private readonly List<float> offsets = new List<float>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object thing in made)
                if (thing != null) Object.DestroyImmediate(thing);
            made.Clear();
        }

        [Test]
        public void LaneLines_NarrowerThanTheMiddleLine_SpreadsLanesRoundTheMiddle()
        {
            TrackLook look = Look(3.6f, 14f, true, false);
            Assert.That(look.LaneLines(6f, offsets), Is.EqualTo(TrackLook.Centre.None));
            Assert.That(offsets, Is.EqualTo(new[] { 1.8f }).Within(1e-4f), "A 12 m road: three lanes, a line either side of the middle one.");
        }

        [Test]
        public void LaneLines_WideRoad_HasTheDoubleMiddleLineAndLanesEitherSide()
        {
            TrackLook look = Look(3.6f, 14f, true, false);
            Assert.That(look.LaneLines(15f, offsets), Is.EqualTo(TrackLook.Centre.DoubleLine));
            Assert.That(offsets, Is.EqualTo(new[] { 3.6f, 7.2f, 10.8f }).Within(1e-4f), "A 30 m road: four lanes each way.");
        }

        [Test]
        public void LaneLines_SingleMiddle_WhenTheLookHasNoDoubleLine()
        {
            TrackLook look = Look(3.6f, 0f, false, false);
            Assert.That(look.LaneLines(7.2f, offsets), Is.EqualTo(TrackLook.Centre.Line));
            Assert.That(offsets, Is.EqualTo(new[] { 3.6f }).Within(1e-4f));
        }

        [Test]
        public void LaneLines_NarrowingRoad_EndsTheOuterLineAndKeepsTheInnerOneInPlace()
        {
            TrackLook look = Look(3.6f, 14f, true, false);
            var seen = new List<int>();
            for (float half = 11f; half >= 6f; half -= 0.25f)
            {
                look.LaneLines(half, offsets);
                Assert.That(offsets[0], Is.EqualTo(1.8f).Within(1e-4f), $"half width {half}: the inner line does not move");
                for (int i = 1; i < offsets.Count; i++) Assert.That(offsets[i] - offsets[i - 1], Is.EqualTo(3.6f).Within(1e-4f));
                float outerLane = half - TrackLook.EdgeLineInset - offsets[offsets.Count - 1];
                Assert.That(outerLane, Is.GreaterThanOrEqualTo(1.8f - 1e-4f), $"half width {half}: no lane narrower than half a lane");
                seen.Add(offsets.Count);
            }
            Assert.That(seen[0], Is.EqualTo(2), "A 22 m road has five lanes.");
            Assert.That(seen[seen.Count - 1], Is.EqualTo(1), "The 12 m road it narrows to has three.");
            for (int i = 1; i < seen.Count; i++) Assert.That(seen[i], Is.LessThanOrEqualTo(seen[i - 1]), "Lines only end as the road narrows.");
        }

        [Test]
        public void LaneLines_RunningTrack_SolidLinesALaneApartAndNoMiddle()
        {
            TrackLook look = Look(1.22f, 99f, false, true);
            Assert.That(look.LaneLines(6f, offsets), Is.EqualTo(TrackLook.Centre.None));
            Assert.That(offsets, Is.EqualTo(new[] { 0.61f, 1.83f, 3.05f, 4.27f }).Within(1e-4f));
            Assert.That(look.SolidLanes, Is.True);
        }

        [Test]
        public void TryValidate_LanesNarrowerThanAMetre_IsRefused()
        {
            // Applying the bad width runs the look's OnValidate, which logs the same error (as GameSettingsTests expect theirs).
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("lanes must be at least 1 m wide"));
            TrackLook look = Look(0.5f, 0f, false, false);
            Assert.That(look.TryValidate(out string error), Is.False);
            StringAssert.Contains("lanes must be at least 1 m wide", error);
        }

        [Test]
        public void TryValidate_SkyWhoseFogEndsBeforeItBegins_IsRefused()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("the sky's fog must begin"));
            TrackLook look = Look(3.6f, 0f, false, false);
            var sky = new Material(Shader.Find("Skybox/Panoramic")) { name = "Test sky" };
            made.Add(sky);
            var data = new SerializedObject(look);
            data.FindProperty("sky").objectReferenceValue = sky;
            data.FindProperty("fogStart").floatValue = 300f;
            data.FindProperty("fogEnd").floatValue = 200f;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(look.TryValidate(out string error), Is.False);
            StringAssert.Contains("the sky's fog must begin", error);
        }

        [Test]
        public void TryValidate_NoSky_DoesNotCheckTheFog()
        {
            TrackLook look = Look(3.6f, 0f, false, false);
            var data = new SerializedObject(look);
            data.FindProperty("fogStart").floatValue = 300f;
            data.FindProperty("fogEnd").floatValue = 200f;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(look.TryValidate(out string error), Is.True, error);
        }

        private TrackLook Look(float laneWidth, float centreFromHalfWidth, bool doubleCentre, bool solid)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null, "URP Lit shader is required for the test materials.");
            var road = new Material(shader) { name = "Test road" };
            var rail = new Material(shader) { name = "Test rail" };
            made.Add(road);
            made.Add(rail);
            TrackLook look = ScriptableObject.CreateInstance<TrackLook>();
            made.Add(look);
            var data = new SerializedObject(look);
            data.FindProperty("road").objectReferenceValue = road;
            data.FindProperty("rail").objectReferenceValue = rail;
            data.FindProperty("laneWidth").floatValue = laneWidth;
            data.FindProperty("centreFromHalfWidth").floatValue = centreFromHalfWidth;
            data.FindProperty("doubleCentre").boolValue = doubleCentre;
            data.FindProperty("solidLanes").boolValue = solid;
            data.ApplyModifiedPropertiesWithoutUndo();
            return look;
        }
    }
}
