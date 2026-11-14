using System;
using NUnit.Framework;
using ProtoHarness.ChainRush.Race;
using UnityEngine;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class RunnerContactTests
    {
        private Vector3[] positions;
        private Vector3[] velocities;
        private Vector3[] moves;
        private Vector3[] changes;
        private float[] weights;
        private readonly float[] radii = { 0.38f, 0.38f };
        private readonly float[] heights = { 1.8f, 1.8f };

        [SetUp]
        public void SetUp()
        {
            positions = new[] { Vector3.zero, Vector3.right * 0.6f };
            velocities = new[] { Vector3.right * 8f + Vector3.forward * 3f, Vector3.right * 2f + Vector3.forward * 5f };
            weights = new[] { 1.2f, 0.9f };
            moves = new Vector3[2];
            changes = new Vector3[2];
        }

        private void Solve(float restitution = RunnerContact.Restitution) => RunnerContact.Solve(
            positions, velocities, radii, heights, weights, restitution, moves, changes);

        [Test]
        public void Solve_ClosingPair_SeparatesByInverseMassAndConservesNormalMomentum()
        {
            Vector3[] before = (Vector3[])positions.Clone();
            Solve();
            Assert.That(moves[0].magnitude, Is.LessThan(moves[1].magnitude));
            Assert.That(changes[0].magnitude, Is.LessThan(changes[1].magnitude));
            Assert.That((positions[1] + moves[1] - positions[0] - moves[0]).x, Is.EqualTo(0.76f).Within(0.000001f));
            Assert.That(changes[0].x, Is.LessThan(0f));
            Assert.That(changes[1].x, Is.GreaterThan(0f));
            Assert.That(weights[0] * changes[0].x + weights[1] * changes[1].x, Is.EqualTo(0f).Within(0.000001f));
            float separating = velocities[1].x + changes[1].x - velocities[0].x - changes[0].x;
            Assert.That(separating, Is.EqualTo(6f * RunnerContact.Restitution).Within(0.000001f));
            foreach (Vector3 change in changes) Assert.That(new Vector2(change.y, change.z), Is.EqualTo(Vector2.zero), "Vertical and tangent velocities stay.");
            Assert.That(positions, Is.EqualTo(before), "Inputs are read-only.");
        }

        [Test]
        public void Solve_SeparatingPair_OnlyCorrectsPositions()
        {
            velocities[0] = Vector3.left;
            velocities[1] = Vector3.right;
            Solve();
            Assert.That(moves[0].x, Is.LessThan(0f));
            Assert.That(changes, Is.EqualTo(new Vector3[2]));
        }

        [TestCase(1.8f), TestCase(3f)]
        public void Solve_NoVerticalOverlap_DoesNotContact(float height)
        {
            positions[1] += Vector3.up * height;
            Solve();
            Assert.That(moves, Is.EqualTo(new Vector3[2]));
            Assert.That(changes, Is.EqualTo(new Vector3[2]));
        }

        [Test]
        public void Solve_CoincidentCentresAndRepeatedCalls_AreDeterministicAndClearOutputs()
        {
            positions[1] = positions[0];
            Solve();
            Vector3[] firstMoves = (Vector3[])moves.Clone(), firstChanges = (Vector3[])changes.Clone();
            Solve();
            Assert.That(moves, Is.EqualTo(firstMoves));
            Assert.That(changes, Is.EqualTo(firstChanges));
            Assert.That(moves[0].x, Is.LessThan(0f));
            positions[1] = Vector3.right * 2f;
            Solve();
            Assert.That(moves, Is.EqualTo(new Vector3[2]));
            Assert.That(changes, Is.EqualTo(new Vector3[2]));
        }

        [TestCase(-0.01f), TestCase(1.01f), TestCase(float.NaN)]
        public void Solve_InvalidRestitution_ThrowsArgumentException(float restitution)
        {
            Assert.Throws<ArgumentException>(() => Solve(restitution));
        }

        [TestCase(0f), TestCase(-1f), TestCase(float.NaN), TestCase(float.PositiveInfinity)]
        public void Solve_InvalidWeight_ThrowsArgumentException(float weight)
        {
            weights[0] = weight;
            Assert.Throws<ArgumentException>(() => Solve());
        }

        [Test]
        public void Solve_MissingMismatchedOrAliasedArrays_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => RunnerContact.Solve(null, velocities, radii, heights, weights, 0.3f, moves, changes));
            Assert.Throws<ArgumentException>(() => RunnerContact.Solve(positions, new Vector3[1], radii, heights, weights, 0.3f, moves, changes));
            Assert.Throws<ArgumentException>(() => RunnerContact.Solve(positions, velocities, radii, heights, weights, 0.3f, positions, changes));
        }
    }
}
