using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Flow;
using ProtoHarness.ChainRush.Track;
using ProtoHarness.ChainRush.Tutorial;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProtoHarness.Tests.EditMode
{
    // The tutorial's table and judging (DECISIONS 2026-11-06 "튜토리얼"): every goal can be done on the floor, and each
    // kind of goal is met by what it says and nothing else.
    public sealed class TutorialCourseTests
    {
        private const float PoleReach = 6f;
        private const float MaxPoleAngle = 95f;
        private const float HookArcShare = 1f;

        private static TutorialCourse.Goal GoalOf(TutorialCourse.GoalKind kind)
        {
            foreach (TutorialCourse.Stage stage in TutorialCourse.Stages)
                foreach (TutorialCourse.Goal goal in stage.Goals)
                    if (goal.Kind == kind) return goal;
            Assert.Fail($"No stage has a {kind} goal.");
            return default;
        }

        [Test]
        public void Stages_EachHaveANameASummaryAndGoalsWithText()
        {
            Assert.That(TutorialCourse.Stages.Count, Is.GreaterThan(0));
            var names = new HashSet<string>();
            foreach (TutorialCourse.Stage stage in TutorialCourse.Stages)
            {
                Assert.That(stage.Name, Is.Not.Empty);
                Assert.That(names.Add(stage.Name), Is.True, stage.Name + " repeats another stage's name.");
                Assert.That(stage.Summary, Is.Not.Empty, stage.Name);
                Assert.That(stage.Goals.Count, Is.GreaterThan(0), stage.Name);
                foreach (TutorialCourse.Goal goal in stage.Goals) Assert.That(goal.Text, Is.Not.Empty, stage.Name);
            }
        }

        [Test]
        public void Stages_EveryZoneLiesWhollyOnTheFloor()
        {
            int zones = 0;
            foreach (TutorialCourse.Stage stage in TutorialCourse.Stages)
                foreach (TutorialCourse.Goal goal in stage.Goals)
                {
                    if (!goal.HasZone) continue;
                    zones++;
                    Vector3 fromPole = goal.Point - TutorialCourse.Pole;
                    fromPole.y = 0f;
                    Assert.That(fromPole.magnitude + goal.Radius, Is.LessThan(TutorialCourse.FloorRadius), $"{stage.Name}: {goal.Point} reaches the wall.");
                    Assert.That(fromPole.magnitude - goal.Radius, Is.GreaterThan(TutorialCourse.IslandRadius), $"{stage.Name}: {goal.Point} reaches the island.");
                }
            Assert.That(zones, Is.GreaterThan(0));
        }

        [Test]
        public void Stages_EachHasBriefingPagesWithATitleAndBody()
        {
            foreach (TutorialCourse.Stage stage in TutorialCourse.Stages)
            {
                Assert.That(stage.Pages.Count, Is.GreaterThan(0), stage.Name);
                foreach (TutorialCourse.Page page in stage.Pages)
                {
                    Assert.That(string.IsNullOrWhiteSpace(page.Title), Is.False, stage.Name);
                    Assert.That(string.IsNullOrWhiteSpace(page.Body), Is.False, stage.Name);
                    Assert.That(Enum.IsDefined(typeof(TutorialFigures.Kind), page.Figure), Is.True, page.Title);
                }
            }
        }

        // A booster must be earned first: only a swing fills the gauge.
        [Test]
        public void Stages_ABoosterAlwaysHasAGaugeSlotToSpend()
        {
            foreach (TutorialCourse.Stage stage in TutorialCourse.Stages)
            {
                bool filled = false;
                foreach (TutorialCourse.Goal goal in stage.Goals)
                {
                    if (goal.Kind == TutorialCourse.GoalKind.Gauge && goal.Amount >= 1f) filled = true;
                    if (goal.Kind == TutorialCourse.GoalKind.Boost) Assert.That(filled, Is.True, stage.Name);
                }
            }
        }

        [Test]
        public void Stages_BoosterAndFinaleHaveDistinctOrderedGoals()
        {
            var booster = TutorialCourse.Stages[4].Goals;
            var finale = TutorialCourse.Stages[5].Goals;
            Assert.That(Kinds(booster), Is.EqualTo(new[] { TutorialCourse.GoalKind.Gauge, TutorialCourse.GoalKind.Release,
                TutorialCourse.GoalKind.Boost, TutorialCourse.GoalKind.Reach }));
            Assert.That(Kinds(finale), Is.EqualTo(new[] { TutorialCourse.GoalKind.Reach, TutorialCourse.GoalKind.Gauge,
                TutorialCourse.GoalKind.Release, TutorialCourse.GoalKind.Boost, TutorialCourse.GoalKind.Stop }));
        }

        private static TutorialCourse.GoalKind[] Kinds(IReadOnlyList<TutorialCourse.Goal> goals)
        {
            var kinds = new TutorialCourse.GoalKind[goals.Count];
            for (int i = 0; i < goals.Count; i++) kinds[i] = goals[i].Kind;
            return kinds;
        }

        [TestCase(0f, false, true)]
        [TestCase(0.99f, false, true)]
        [TestCase(1f, false, false)]
        [TestCase(0f, true, false)]
        public void GoalText_BoostWithNoSlotAndNoActiveBooster_ExplainsHowToRefill(float gauge, bool boosting, bool empty)
        {
            TutorialCourse.Goal goal = GoalOf(TutorialCourse.GoalKind.Boost);
            Assert.That(TutorialCourse.GoalText(goal, gauge, boosting), Is.EqualTo(empty
                ? "게이지가 없습니다.\n봉에서 스윙해 다시 채우세요." : goal.Text));
        }

        [Test]
        public void GoalText_OtherGoalsKeepTheirInstructionsEvenWithNoGauge()
        {
            foreach (TutorialCourse.Stage stage in TutorialCourse.Stages)
                foreach (TutorialCourse.Goal goal in stage.Goals)
                    if (goal.Kind != TutorialCourse.GoalKind.Boost)
                        Assert.That(TutorialCourse.GoalText(goal, 0f, false), Is.EqualTo(goal.Text));
        }

        [Test]
        public void Stages_RopeGoalAsksForFiveMetres()
        {
            TutorialCourse.Goal goal = GoalOf(TutorialCourse.GoalKind.Rope);
            Assert.That(goal.Amount, Is.EqualTo(5f));
            Assert.That(goal.Text, Does.Contain("5 m"));
        }

        [Test]
        public void IsMet_Reach_NeedsTheZoneAndIgnoresHeight()
        {
            TutorialCourse.Goal goal = GoalOf(TutorialCourse.GoalKind.Reach);
            Assert.That(TutorialCourse.IsMet(goal, new TutorialCourse.Reading { Position = goal.Point + Vector3.up * 5f }), Is.True);
            Assert.That(TutorialCourse.IsMet(goal, new TutorialCourse.Reading { Position = goal.Point + Vector3.right * (goal.Radius + 0.1f) }), Is.False);
        }

        [Test]
        public void IsMet_Stop_NeedsTheZoneAtRest()
        {
            TutorialCourse.Goal goal = GoalOf(TutorialCourse.GoalKind.Stop);
            Assert.That(TutorialCourse.IsMet(goal, new TutorialCourse.Reading { Position = goal.Point, Speed = 0.5f }), Is.True);
            Assert.That(TutorialCourse.IsMet(goal, new TutorialCourse.Reading { Position = goal.Point, Speed = 5f }), Is.False, "Still moving.");
            Assert.That(TutorialCourse.IsMet(goal, new TutorialCourse.Reading { Position = goal.Point + Vector3.right * 100f }), Is.False, "Stopped elsewhere.");
        }

        [Test]
        public void IsMet_Reverse_RefusesARunnerDrivingIntoTheZoneForward()
        {
            TutorialCourse.Goal goal = GoalOf(TutorialCourse.GoalKind.Reverse);
            Assert.That(TutorialCourse.IsMet(goal, new TutorialCourse.Reading { Position = goal.Point, ForwardSpeed = -3f }), Is.True);
            Assert.That(TutorialCourse.IsMet(goal, new TutorialCourse.Reading { Position = goal.Point, ForwardSpeed = 3f }), Is.False);
        }

        [Test]
        public void IsMet_TheActions_AreMetByTheirOwnReadingOnly()
        {
            TutorialCourse.Goal swing = GoalOf(TutorialCourse.GoalKind.Swing);
            Assert.That(TutorialCourse.IsMet(swing, new TutorialCourse.Reading { SwungDegrees = swing.Amount }), Is.True);
            Assert.That(TutorialCourse.IsMet(swing, new TutorialCourse.Reading { SwungDegrees = swing.Amount - 1f }), Is.False);
            TutorialCourse.Goal rope = GoalOf(TutorialCourse.GoalKind.Rope);
            Assert.That(TutorialCourse.IsMet(rope, new TutorialCourse.Reading { RopeTaken = rope.Amount }), Is.True);
            Assert.That(TutorialCourse.IsMet(rope, new TutorialCourse.Reading { SwungDegrees = 360f }), Is.False);
            TutorialCourse.Goal gauge = GoalOf(TutorialCourse.GoalKind.Gauge);
            Assert.That(TutorialCourse.IsMet(gauge, new TutorialCourse.Reading { Gauge = gauge.Amount }), Is.True);
            Assert.That(TutorialCourse.IsMet(gauge, new TutorialCourse.Reading { Gauge = gauge.Amount - 0.1f }), Is.False);
            Assert.That(TutorialCourse.IsMet(GoalOf(TutorialCourse.GoalKind.Release), new TutorialCourse.Reading { Released = true }), Is.True);
            Assert.That(TutorialCourse.IsMet(GoalOf(TutorialCourse.GoalKind.Release), default), Is.False);
            Assert.That(TutorialCourse.IsMet(GoalOf(TutorialCourse.GoalKind.Boost), new TutorialCourse.Reading { Boosting = true }), Is.True);
            Assert.That(TutorialCourse.IsMet(GoalOf(TutorialCourse.GoalKind.Boost), default), Is.False);
        }

        // The ring is why the swing works on open floor: the pole is square to the track everywhere, so it can be hooked
        // from all round it, up to the ring's radius plus the reach, and from nowhere farther.
        [Test]
        public void Ring_ItsPoleHooksFromTheWholeFloorWithAdditionalReach()
        {
            TrackDefinition ring = TrackDefinition.Ring(TutorialCourse.TrackName, TutorialCourse.RingRadius);
            try
            {
                Centerline line = ring.BuildCenterline(Vector3.zero, 0f);
                var poles = new List<Vector3>();
                line.GetPoles(poles);
                Assert.That(poles, Is.Not.Empty);
                foreach (Vector3 pole in poles) Assert.That((pole - TutorialCourse.Pole).magnitude, Is.LessThan(0.01f));
                for (int degrees = 0; degrees < 360; degrees += 15)
                {
                    Vector3 radial = Quaternion.Euler(0f, degrees, 0f) * Vector3.forward;
                    foreach (float radius in new[] { TutorialCourse.IslandRadius + 1f, 15f, 30f, 60f, 100f, TutorialCourse.FloorRadius - 1f })
                        Assert.That(Hooks(line, TutorialCourse.Pole + radial * radius), Is.True, $"{degrees} degrees, radius {radius}.");
                }
            }
            finally
            {
                Object.DestroyImmediate(ring);
            }
        }

        private static bool Hooks(Centerline line, Vector3 position)
        {
            Vector3 forward = line.Frame(position).Forward;
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            return line.TryFindPole(position, yaw, MaxPoleAngle, PoleReach + TutorialCourse.FloorRadius, out _, HookArcShare);
        }

        [Test]
        public void Setup_NamesTheTutorialAndRefusesAStageThatIsNotThere()
        {
            var runner = ScriptableObject.CreateInstance<RunnerProfile>();
            RaceSetup setup = TutorialCourse.Setup(runner, null, 2);
            try
            {
                Assert.That(setup.Mode, Is.EqualTo(RaceMode.Tutorial));
                Assert.That(setup.FirstStage, Is.EqualTo(2));
                Assert.That(setup.Title, Is.EqualTo(TutorialCourse.Title));
                Assert.That(setup.EnvironmentScene, Is.Empty);
                Assert.Throws<ArgumentOutOfRangeException>(() => TutorialCourse.Setup(runner, null, -1));
                Assert.Throws<ArgumentOutOfRangeException>(() => TutorialCourse.Setup(runner, null, TutorialCourse.Stages.Count));
            }
            finally
            {
                Object.DestroyImmediate(setup.Track);
                Object.DestroyImmediate(runner);
            }
        }
    }
}
