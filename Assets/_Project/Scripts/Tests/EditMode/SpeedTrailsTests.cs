using NUnit.Framework;
using ProtoHarness.ChainRush.Visuals;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class SpeedTrailsTests
    {
        [TestCase(false, false, false, SpeedTrails.Style.None)]
        [TestCase(true, false, false, SpeedTrails.Style.Swing)]
        [TestCase(false, true, false, SpeedTrails.Style.Booster)]
        [TestCase(true, true, false, SpeedTrails.Style.SwingBooster)]
        [TestCase(false, false, true, SpeedTrails.Style.BoostZone)]
        public void StyleOf_EachState_PicksItsOwnTail(bool swinging, bool booster, bool boostZone, SpeedTrails.Style expected)
            => Assert.That(SpeedTrails.StyleOf(swinging, booster, boostZone), Is.EqualTo(expected));

        [Test]
        public void StyleOf_BoosterOnABoostZoneCarry_ShowsTheBooster()
            => Assert.That(SpeedTrails.StyleOf(false, true, true), Is.EqualTo(SpeedTrails.Style.Booster));

        [Test]
        public void StyleOf_SwingOnABoostZoneCarry_ShowsTheSwing()
            => Assert.That(SpeedTrails.StyleOf(true, false, true), Is.EqualTo(SpeedTrails.Style.Swing));
    }
}
