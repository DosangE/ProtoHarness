using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Track;
using UnityEngine;
using UnityEditor;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class StartGridTests
    {
        [TestCase(0), TestCase(13579), TestCase(-1), TestCase(int.MinValue), TestCase(int.MaxValue)]
        public void Assign_SameSeed_UniqueSlotsIncludingThePlayer(int seed)
        {
            int[] slots = StartGrid.Assign(4, seed);
            Assert.That(slots, Is.EqualTo(StartGrid.Assign(4, seed)));
            Assert.That(new HashSet<int>(slots).Count, Is.EqualTo(4));
            foreach (int slot in slots) Assert.That(slot, Is.InRange(0, 3));
        }

        [Test]
        public void Assign_DifferentSeeds_ShuffleThePlayerToo()
        {
            var playerSlots = new HashSet<int>();
            for (int seed = 0; seed < 64; seed++) playerSlots.Add(StartGrid.Assign(4, seed)[0]);
            Assert.That(playerSlots.Count, Is.EqualTo(4));
        }

        [TestCase("Stadium"), TestCase("SummitDrop")]
        public void Slot_FourRunners_FitTheWholeStartWindowOnOneRow(string track)
        {
            var definition = AssetDatabase.LoadAssetAtPath<TrackDefinition>("Assets/_Project/Data/Circuit_" + track + ".asset");
            Assert.That(definition, Is.Not.Null);
            float minimum = StartGrid.MinimumHalfWidth(definition);
            for (int i = 0; i < 4; i++)
            {
                Vector2 slot = StartGrid.Slot(definition, 4, i);
                Assert.That(slot.x, Is.EqualTo(0f));
                Assert.That(Mathf.Abs(slot.y) + StartGrid.BodyRadius + StartGrid.WallClearance, Is.LessThanOrEqualTo(minimum));
                if (i > 0) Assert.That(slot.y - StartGrid.Slot(definition, 4, i - 1).y, Is.EqualTo(2.4f).Within(0.000001f));
            }
            if (track == "SummitDrop") Assert.That(definition.StartRunway, Is.EqualTo(15f));
        }

        [Test]
        public void Slot_NarrowSection_UsesItsMinimumOrThrowsWhenBelowSpacingLimit()
        {
            var definition = ScriptableObject.CreateInstance<TrackDefinition>();
            try
            {
                typeof(TrackDefinition).GetField("roadHalfWidth", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(definition, 3f);
                float expected = (3f - 0.38f - 0.6f) * 2f / 3f;
                Assert.That(StartGrid.Slot(definition, 4, 1).y - StartGrid.Slot(definition, 4, 0).y, Is.EqualTo(expected).Within(0.000001f));
                // Setting the transient fixture directly avoids OnValidate logging for an intentionally invalid width.
                typeof(TrackDefinition).GetField("roadHalfWidth", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(definition, 2f);
                Assert.Throws<ArgumentException>(() => StartGrid.Slot(definition, 4, 0));
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void Slot_InvalidCountSlotOrDefinition_ThrowsArgumentException()
        {
            var definition = ScriptableObject.CreateInstance<TrackDefinition>();
            try
            {
                Assert.Throws<ArgumentNullException>(() => StartGrid.Slot(null, 4, 0));
                Assert.Throws<ArgumentOutOfRangeException>(() => StartGrid.Slot(definition, 0, 0));
                Assert.Throws<ArgumentOutOfRangeException>(() => StartGrid.Slot(definition, 4, 4));
                Assert.Throws<ArgumentOutOfRangeException>(() => StartGrid.Assign(9, 0));
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }
    }
}
