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

        [Test]
        public void Ticks_Defaults_MatchDurationsAtFiftyHertz()
        {
            Assert.That(tuning.WarningTicks, Is.EqualTo(15));
            Assert.That(tuning.EntranceTicks, Is.EqualTo(20));
            Assert.That(tuning.AttackWindowTicks, Is.EqualTo(60));
            Assert.That(tuning.FlightTicks, Is.EqualTo(8));
            Assert.That(tuning.RecoveryTicks, Is.EqualTo(15));
        }

        [Test]
        public void NextGapTicks_DistanceZero_ReturnsStartGapInTicks()
        {
            Assert.That(tuning.NextGapTicks(0d), Is.EqualTo(125));
        }
    }
}
