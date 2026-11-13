using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Bots;
using UnityEngine;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class BotLineupTests
    {
        private RunnerProfile[] profiles;

        [SetUp]
        public void SetUp()
        {
            profiles = new RunnerProfile[5];
            for (int i = 0; i < profiles.Length; i++) profiles[i] = ScriptableObject.CreateInstance<RunnerProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (RunnerProfile profile in profiles) UnityEngine.Object.DestroyImmediate(profile);
        }

        [TestCase(0), TestCase(123456), TestCase(-1), TestCase(int.MinValue), TestCase(int.MaxValue)]
        public void Pick_SameSeed_RepeatsWithoutDuplicatesOrThePlayer(int seed)
        {
            RunnerProfile[] before = (RunnerProfile[])profiles.Clone();
            foreach (RunnerProfile player in profiles)
            {
                RunnerProfile[] first = BotLineup.Pick(profiles, player, seed);
                Assert.That(first, Is.EqualTo(BotLineup.Pick(profiles, player, seed)));
                Assert.That(first.Length, Is.EqualTo(3));
                Assert.That(new HashSet<RunnerProfile>(first).Count, Is.EqualTo(3));
                Assert.That(first, Has.No.Member(player));
                foreach (RunnerProfile bot in first) Assert.That(profiles, Has.Member(bot));
            }
            Assert.That(profiles, Is.EqualTo(before), "The draw cannot shuffle the caller's pool.");
        }

        [Test]
        public void Pick_DifferentSeeds_ProduceDifferentLineups()
        {
            var lineups = new HashSet<string>();
            for (int seed = 0; seed < 32; seed++)
            {
                RunnerProfile[] bots = BotLineup.Pick(profiles, profiles[0], seed);
                lineups.Add(Array.IndexOf(profiles, bots[0]) + ":" + Array.IndexOf(profiles, bots[1]) + ":" + Array.IndexOf(profiles, bots[2]));
            }
            Assert.That(lineups.Count, Is.GreaterThan(1));
        }

        [Test]
        public void Pick_InvalidPoolPlayerOrCount_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentNullException>(() => BotLineup.Pick(null, profiles[0], 1));
            Assert.Throws<ArgumentNullException>(() => BotLineup.Pick(profiles, null, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => BotLineup.Pick(profiles, profiles[0], 1, 0));
            Assert.Throws<ArgumentException>(() => BotLineup.Pick(profiles, profiles[0], 1, 5));
            Assert.Throws<ArgumentException>(() => BotLineup.Pick(new[] { profiles[1], profiles[2] }, profiles[0], 1, 1));
            Assert.Throws<ArgumentException>(() => BotLineup.Pick(new[] { profiles[0], profiles[1], profiles[1] }, profiles[0], 1, 1));
            Assert.Throws<ArgumentException>(() => BotLineup.Pick(new[] { profiles[0], profiles[1], null }, profiles[0], 1, 1));
        }
    }
}
