using NUnit.Framework;
using ProtoHarness.ChainRush.Combat;
using UnityEngine;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class EncounterTuningTests
    {
        private EncounterTuning tuning;

        [SetUp]
        public void SetUp() => tuning = ScriptableObject.CreateInstance<EncounterTuning>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(tuning);

        [Test]
        public void NextGap_DistanceZero_ReturnsStartGap()
        {
            Assert.That(tuning.NextGap(0d), Is.EqualTo(2.5f).Within(1e-5f));
        }

        [Test]
        public void NextGap_FarDistance_ClampsToMinGap()
        {
            Assert.That(tuning.NextGap(1000000d), Is.EqualTo(0.6f).Within(1e-5f));
        }

        [Test]
        public void EncounterDuration_Defaults_Equals2Point35()
        {
            Assert.That(tuning.EncounterDuration, Is.EqualTo(2.35f).Within(1e-5f));
        }
    }
}
