using System;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class RacerStateTests
    {
        private RunRules rules;
        private RacerState racer;

        [SetUp]
        public void SetUp()
        {
            rules = ScriptableObject.CreateInstance<RunRules>();
            racer = new RacerState(rules);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(rules);

        [Test]
        public void Constructor_NullRules_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RacerState(null));
        }

        [Test]
        public void Constructor_Created_StartsAtFullHealthWithNoCounts()
        {
            Assert.That(racer.Health, Is.EqualTo(rules.MaxHealth));
            Assert.That(racer.Hits, Is.Zero);
            Assert.That(racer.Grapples, Is.Zero);
            Assert.That(racer.IsDown, Is.False);
            Assert.That(racer.IsInvulnerable(0), Is.False);
            Assert.That(racer.IsAttackShown(0), Is.False);
            Assert.That(racer.CanAttack(0), Is.True);
        }

        [Test]
        public void TryTakeDamage_OutsideWindow_LosesHealthAndStartsInvulnerability()
        {
            Assert.That(racer.TryTakeDamage(10), Is.True);
            Assert.That(racer.Health, Is.EqualTo(rules.MaxHealth - 1));
            Assert.That(racer.IsInvulnerable(10 + rules.DamageInvulnerabilityTicks - 1), Is.True);
            Assert.That(racer.IsInvulnerable(10 + rules.DamageInvulnerabilityTicks), Is.False);
        }

        [Test]
        public void TryTakeDamage_InsideWindow_IsIgnored()
        {
            racer.TryTakeDamage(10);
            Assert.That(racer.TryTakeDamage(10 + rules.DamageInvulnerabilityTicks - 1), Is.False);
            Assert.That(racer.Health, Is.EqualTo(rules.MaxHealth - 1));
        }

        [Test]
        public void TryTakeDamage_HealthReachesZero_IsDown()
        {
            int tick = 0;
            for (int i = 0; i < rules.MaxHealth; i++)
            {
                Assert.That(racer.TryTakeDamage(tick), Is.True);
                tick += rules.DamageInvulnerabilityTicks;
            }
            Assert.That(racer.Health, Is.Zero);
            Assert.That(racer.IsDown, Is.True);
        }

        [Test]
        public void BeginAttackCooldown_Started_BlocksAttackUntilCooldownEnds()
        {
            racer.BeginAttackCooldown(5);
            Assert.That(racer.CanAttack(5 + rules.AttackCooldownTicks - 1), Is.False);
            Assert.That(racer.CanAttack(5 + rules.AttackCooldownTicks), Is.True);
        }

        [Test]
        public void BeginAttackCooldown_Alone_DoesNotShowAttack()
        {
            racer.BeginAttackCooldown(5);
            Assert.That(racer.IsAttackShown(5), Is.False);
        }

        [Test]
        public void ShowAttack_Shown_CountsDownToZero()
        {
            racer.ShowAttack(20);
            Assert.That(racer.IsAttackShown(20), Is.True);
            Assert.That(racer.AttackTicksLeft(20), Is.EqualTo(rules.AttackVisualTicks));
            Assert.That(racer.IsAttackShown(20 + rules.AttackVisualTicks), Is.False);
            Assert.That(racer.AttackTicksLeft(20 + rules.AttackVisualTicks + 5), Is.Zero);
        }

        [Test]
        public void AddHitAndGrapple_Called_CountSeparately()
        {
            racer.AddHit();
            racer.AddHit();
            racer.AddGrapple();
            Assert.That(racer.Hits, Is.EqualTo(2));
            Assert.That(racer.Grapples, Is.EqualTo(1));
        }

        [Test]
        public void Reset_AfterPlay_RestoresStartState()
        {
            racer.TryTakeDamage(10);
            racer.ShowAttack(10);
            racer.BeginAttackCooldown(10);
            racer.AddHit();
            racer.AddGrapple();
            racer.Reset();
            Assert.That(racer.Health, Is.EqualTo(rules.MaxHealth));
            Assert.That(racer.Hits, Is.Zero);
            Assert.That(racer.Grapples, Is.Zero);
            Assert.That(racer.IsInvulnerable(0), Is.False);
            Assert.That(racer.IsAttackShown(0), Is.False);
            Assert.That(racer.CanAttack(0), Is.True);
        }

        [Test]
        public void Constructor_Created_StartsAtRestWithoutAnchor()
        {
            Assert.That(racer.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(racer.Steer, Is.Zero);
            Assert.That(racer.JumpQueued, Is.False);
            Assert.That(racer.CoyoteTime, Is.Zero);
            Assert.That(racer.HasAnchor, Is.False);
            Assert.That(racer.AnchorIndex, Is.EqualTo(RacerState.NoAnchor));
            Assert.That(racer.MissUntilTick, Is.Zero);
        }

        [Test]
        public void Velocity_WrittenByRef_IsSeenThroughProperty()
        {
            ref Vector3 velocity = ref racer.Velocity;
            velocity.y = 4f;
            Assert.That(racer.Velocity.y, Is.EqualTo(4f));
        }

        [Test]
        public void AttachThenDetach_TracksAnchorAndRope()
        {
            racer.Attach(2, 7.5f);
            Assert.That(racer.HasAnchor, Is.True);
            Assert.That(racer.AnchorIndex, Is.EqualTo(2));
            Assert.That(racer.RopeLength, Is.EqualTo(7.5f));
            racer.Detach();
            Assert.That(racer.HasAnchor, Is.False);
            Assert.That(racer.AnchorIndex, Is.EqualTo(RacerState.NoAnchor));
        }

        [Test]
        public void Attach_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => racer.Attach(-1, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => racer.Attach(0, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => racer.Attach(0, float.NaN));
            Assert.That(racer.HasAnchor, Is.False);
        }

        [Test]
        public void MarkMissThenClear_TracksDeadline()
        {
            racer.MarkMiss(40);
            Assert.That(racer.MissUntilTick, Is.EqualTo(40));
            racer.ClearMiss();
            Assert.That(racer.MissUntilTick, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => racer.MarkMiss(-1));
        }

        [Test]
        public void Reset_AfterMotion_ClearsMotionAndAnchor()
        {
            racer.Velocity = new Vector3(1f, 2f, 3f);
            racer.Steer = -1f;
            racer.JumpQueued = true;
            racer.CoyoteTime = 0.1f;
            racer.Attach(1, 6f);
            racer.MarkMiss(30);
            racer.Reset();
            Assert.That(racer.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(racer.Steer, Is.Zero);
            Assert.That(racer.JumpQueued, Is.False);
            Assert.That(racer.CoyoteTime, Is.Zero);
            Assert.That(racer.HasAnchor, Is.False);
            Assert.That(racer.RopeLength, Is.Zero);
            Assert.That(racer.MissUntilTick, Is.Zero);
        }

        [Test]
        public void Queries_NegativeTick_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => racer.IsInvulnerable(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => racer.TryTakeDamage(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => racer.CanAttack(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => racer.BeginAttackCooldown(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => racer.ShowAttack(-1));
        }
    }
}
