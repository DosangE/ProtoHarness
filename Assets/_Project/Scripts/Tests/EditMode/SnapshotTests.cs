using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Track;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProtoHarness.Tests.EditMode
{
    // A snapshot must hold every piece of state its owner has, or a rollback silently loses some of it. The
    // reflection tests here fail when a field is added to RacerState or LapCounter and the snapshot is not
    // updated to match.
    public sealed class SnapshotTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private RunRules rules;

        [SetUp]
        public void SetUp() => rules = ScriptableObject.CreateInstance<RunRules>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(rules);

        private static List<FieldInfo> StateFields(Type type, params string[] except)
        {
            var fields = new List<FieldInfo>();
            foreach (FieldInfo field in type.GetFields(Instance))
            {
                if (field.Name.Contains("k__BackingField") || Array.IndexOf(except, field.Name) >= 0) continue;
                fields.Add(field);
            }
            return fields;
        }

        // A value of the field's type that differs for every `seed`.
        private static object ValueFor(FieldInfo field, int seed)
        {
            Type type = field.FieldType;
            if (type == typeof(int)) return seed * 3 + 7;
            if (type == typeof(float)) return seed * 0.5f + 0.25f;
            if (type == typeof(double)) return seed * 0.5d + 0.125d;
            if (type == typeof(bool)) return seed % 2 == 0;
            if (type == typeof(Vector3)) return new Vector3(seed, seed * 2f, -seed);
            if (type == typeof(int[])) return new[] { seed, seed + 1, seed + 2 };
            Assert.Fail($"Field {field.DeclaringType.Name}.{field.Name} has the type {type.Name}, which this test cannot fill: extend ValueFor.");
            return null;
        }

        private static void Fill(object target, List<FieldInfo> fields, int seed)
        {
            for (int i = 0; i < fields.Count; i++) fields[i].SetValue(target, ValueFor(fields[i], seed + i));
        }

        private static void AssertFieldsEqual(object expected, object actual, List<FieldInfo> fields)
        {
            foreach (FieldInfo field in fields)
            {
                object a = field.GetValue(expected);
                object b = field.GetValue(actual);
                if (a is int[] left) Assert.That((int[])b, Is.EqualTo(left), field.Name);
                else Assert.That(b, Is.EqualTo(a), field.Name);
            }
        }

        // ---- RacerState ------------------------------------------------------------------------------

        [Test]
        public void RacerSnapshot_HasEveryFieldOfRacerStateByNameAndType()
        {
            var state = new Dictionary<string, Type>();
            foreach (FieldInfo field in StateFields(typeof(RacerState), "rules")) state[field.Name.ToLowerInvariant()] = field.FieldType;
            var snapshot = new Dictionary<string, Type>();
            foreach (FieldInfo field in StateFields(typeof(RacerState.Snapshot))) snapshot[field.Name.ToLowerInvariant()] = field.FieldType;
            Assert.That(snapshot.Keys, Is.EquivalentTo(state.Keys), "A RacerState field is missing from (or extra in) RacerState.Snapshot.");
            foreach (KeyValuePair<string, Type> pair in state) Assert.That(snapshot[pair.Key], Is.EqualTo(pair.Value), pair.Key);
        }

        [Test]
        public void RacerState_CaptureThenRestore_BringsEveryFieldBack()
        {
            var racer = new RacerState(rules);
            List<FieldInfo> fields = StateFields(typeof(RacerState), "rules");
            Fill(racer, fields, 1);
            RacerState.Snapshot snapshot = racer.Capture();
            // A reference copy of the state as captured.
            var reference = new RacerState(rules);
            Fill(reference, fields, 1);
            Fill(racer, fields, 500);
            racer.Restore(snapshot);
            AssertFieldsEqual(reference, racer, fields);
        }

        [Test]
        public void RacerState_Snapshot_IsAValueCopy()
        {
            var racer = new RacerState(rules);
            racer.AddHit();
            racer.AddHit();
            racer.Velocity = new Vector3(1f, 2f, 3f);
            racer.Grounded = true;
            RacerState.Snapshot snapshot = racer.Capture();
            racer.AddHit();
            racer.Velocity = Vector3.zero;
            racer.Grounded = false;
            racer.Restore(snapshot);
            Assert.That(racer.Hits, Is.EqualTo(2));
            Assert.That(racer.Velocity, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(racer.Grounded, Is.True);
        }

        [Test]
        public void RacerState_Reset_KeepsTheControllersGroundedAnswer()
        {
            var racer = new RacerState(rules);
            racer.Grounded = true;
            racer.Reset();
            Assert.That(racer.Grounded, Is.True, "Grounded is the controller's answer, not a rule of the run; StartRun resets after the controller moved.");
        }

        [Test]
        public void RacerState_Restore_ReturnsInvulnerabilityAndCooldownsOfThatMoment()
        {
            var racer = new RacerState(rules);
            Assert.That(racer.TryTakeDamage(10), Is.True);
            racer.BeginAttackCooldown(10);
            RacerState.Snapshot snapshot = racer.Capture();
            racer.Reset();
            Assert.That(racer.IsInvulnerable(11), Is.False);
            racer.Restore(snapshot);
            Assert.That(racer.IsInvulnerable(11), Is.True);
            Assert.That(racer.CanAttack(11), Is.False);
            Assert.That(racer.Health, Is.EqualTo(rules.MaxHealth - 1));
        }

        // ---- LapCounter ------------------------------------------------------------------------------

        [Test]
        public void LapSnapshot_HasEveryStateFieldOfLapCounterByNameAndType()
        {
            // The lap length, the checkpoint list and the lap count are configuration: the same for every snapshot.
            var state = new Dictionary<string, Type>();
            foreach (FieldInfo field in StateFields(typeof(LapCounter), "lapLength", "checkpoints", "lapCount")) state[field.Name.ToLowerInvariant()] = field.FieldType;
            var snapshot = new Dictionary<string, Type>();
            foreach (FieldInfo field in StateFields(typeof(LapCounter.Snapshot))) snapshot[field.Name.ToLowerInvariant()] = field.FieldType;
            Assert.That(snapshot.Keys, Is.EquivalentTo(state.Keys), "A LapCounter state field is missing from (or extra in) LapCounter.Snapshot.");
            foreach (KeyValuePair<string, Type> pair in state) Assert.That(snapshot[pair.Key], Is.EqualTo(pair.Value), pair.Key);
        }

        private static readonly double[] Checkpoints = { 130d, 260d, 400d };
        private const double Lap = 531.3;

        private static void Drive(LapCounter counter, ref double s, ref int tick, double metres)
        {
            int steps = (int)Math.Round(metres / 0.2d);
            for (int i = 0; i < steps; i++)
            {
                s += 0.2d;
                tick++;
                counter.Update(s - Math.Floor(s / Lap) * Lap, tick);
            }
        }

        [Test]
        public void LapCounter_RestoreMidRace_ContinuesLikeAnUninterruptedRun()
        {
            // Uninterrupted reference.
            var reference = new LapCounter(Lap, Checkpoints, 3);
            double rs = 0d;
            int rt = 0;
            reference.Begin(0d, 0);
            Drive(reference, ref rs, ref rt, Lap * 3d + 2d);

            var counter = new LapCounter(Lap, Checkpoints, 3);
            double s = 0d;
            int tick = 0;
            counter.Begin(0d, 0);
            Drive(counter, ref s, ref tick, Lap * 1.4d);
            LapCounter.Snapshot snapshot = counter.Capture();
            double snapshotS = s;
            int snapshotTick = tick;
            // Run on to the finish, then go back to the snapshot and run on again.
            Drive(counter, ref s, ref tick, Lap * 1.6d + 2d);
            Assert.That(counter.IsFinished, Is.True);
            counter.Restore(snapshot);
            Assert.That(counter.IsFinished, Is.False);
            s = snapshotS;
            tick = snapshotTick;
            Drive(counter, ref s, ref tick, Lap * 1.6d + 2d);
            Assert.That(counter.IsFinished, Is.True);
            Assert.That(counter.TotalTicks, Is.EqualTo(reference.TotalTicks));
            for (int lap = 1; lap <= 3; lap++) Assert.That(counter.LapTicks(lap), Is.EqualTo(reference.LapTicks(lap)), "lap " + lap);
            for (int cp = 0; cp < 3; cp++) Assert.That(counter.CheckpointTick(2, cp), Is.EqualTo(reference.CheckpointTick(2, cp)), "checkpoint " + cp);
            Assert.That(counter.Progress, Is.EqualTo(reference.Progress));
        }

        [Test]
        public void LapCounter_Snapshot_IsAValueCopyOfItsArrays()
        {
            var counter = new LapCounter(Lap, Checkpoints, 2);
            double s = 0d;
            int tick = 0;
            counter.Begin(0d, 0);
            Drive(counter, ref s, ref tick, Lap + 10d);
            int firstLap = counter.LapTicks(1);
            LapCounter.Snapshot snapshot = counter.Capture();
            counter.Begin(0d, 0);
            Assert.That(counter.CompletedLaps, Is.Zero);
            counter.Restore(snapshot);
            Assert.That(counter.CompletedLaps, Is.EqualTo(1));
            Assert.That(counter.LapTicks(1), Is.EqualTo(firstLap), "Begin after Capture must not have cleared the snapshot's arrays.");
        }

        [Test]
        public void LapCounter_RestoreIntoADifferentConfiguration_Throws()
        {
            var two = new LapCounter(Lap, Checkpoints, 2);
            two.Begin(0d, 0);
            var three = new LapCounter(Lap, Checkpoints, 3);
            three.Begin(0d, 0);
            Assert.Throws<ArgumentException>(() => three.Restore(two.Capture()));
        }

        [Test]
        public void LapCounter_CaptureBeforeBegin_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => new LapCounter(Lap, Checkpoints, 3).Capture());
        }
    }
}
