using NUnit.Framework;
using ProtoHarness.ChainRush;
using UnityEngine;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class RunRulesTests
    {
        private RunRules rules;

        [SetUp]
        public void SetUp() => rules = ScriptableObject.CreateInstance<RunRules>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(rules);

        [Test]
        public void Defaults_Created_MatchPreSOGameValues()
        {
            Assert.That(rules.MaxHealth, Is.EqualTo(3));
            Assert.That(rules.DamageInvulnerability, Is.EqualTo(1.25f).Within(1e-5f));
            Assert.That(rules.AttackCooldown, Is.EqualTo(0.35f).Within(1e-5f));
            Assert.That(rules.AttackVisualDuration, Is.EqualTo(0.18f).Within(1e-5f));
        }
    }
}
