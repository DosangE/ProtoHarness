using System;
using UnityEngine;

namespace ProtoHarness.ChainRush.Race
{
    // One deterministic pair sweep. Caller-owned outputs keep the field's tick free of allocations.
    public static class RunnerContact
    {
        public const float Restitution = 0.3f;

        public static void Solve(Vector3[] positions, Vector3[] velocities, float[] radii, float[] heights,
            float[] weights, float restitution, Vector3[] displacements, Vector3[] velocityChanges)
            => Solve(positions, velocities, radii, heights, weights, restitution, displacements, velocityChanges, positions == null ? 0 : positions.Length);

        // A packed prefix of cached arrays, for a field with finishers who no longer participate.
        public static void Solve(Vector3[] positions, Vector3[] velocities, float[] radii, float[] heights,
            float[] weights, float restitution, Vector3[] displacements, Vector3[] velocityChanges, int count)
            => Solve(positions, velocities, radii, heights, weights, restitution, displacements, velocityChanges, count, null);

        // Wall normals come only from the preceding real Move. They constrain positional mobility, never mass/impulse.
        internal static void Solve(Vector3[] positions, Vector3[] velocities, float[] radii, float[] heights,
            float[] weights, float restitution, Vector3[] displacements, Vector3[] velocityChanges, int count, Vector3[] wallNormals)
        {
            if (positions == null || velocities == null || radii == null || heights == null || weights == null
                || displacements == null || velocityChanges == null) throw new ArgumentException("RunnerContact: arrays are required.");
            int capacity = positions.Length;
            if (count < 0 || count > capacity || velocities.Length != capacity || radii.Length != capacity || heights.Length != capacity || weights.Length != capacity
                || displacements.Length != capacity || velocityChanges.Length != capacity || (wallNormals != null && wallNormals.Length != capacity))
                throw new ArgumentException("RunnerContact: all arrays must have the same length.");
            if (!(restitution >= 0f && restitution <= 1f)) throw new ArgumentException("RunnerContact: restitution must be within [0, 1].");
            if (ReferenceEquals(displacements, positions) || ReferenceEquals(displacements, velocities)
                || ReferenceEquals(velocityChanges, positions) || ReferenceEquals(velocityChanges, velocities)
                || ReferenceEquals(displacements, velocityChanges)) throw new ArgumentException("RunnerContact: output arrays must be separate from inputs and each other.");
            for (int i = 0; i < count; i++)
            {
                if (!(weights[i] > 0f) || float.IsInfinity(weights[i]) || !(radii[i] > 0f) || float.IsInfinity(radii[i])
                    || !(heights[i] >= 2f * radii[i]) || float.IsInfinity(heights[i]) || !Finite(positions[i]) || !Finite(velocities[i]))
                    throw new ArgumentException("RunnerContact: finite positions, velocities and positive capsule dimensions and weights are required.");
            }
            Array.Clear(displacements, 0, count);
            Array.Clear(velocityChanges, 0, count);
            for (int i = 0; i < count; i++)
                for (int j = i + 1; j < count; j++)
                {
                    Vector3 a = positions[i] + displacements[i];
                    Vector3 b = positions[j] + displacements[j];
                    if (Mathf.Abs(a.y - b.y) >= (heights[i] + heights[j]) * 0.5f) continue;
                    Vector3 delta = b - a;
                    delta.y = 0f;
                    float distance = delta.magnitude;
                    float overlap = radii[i] + radii[j] - distance;
                    if (overlap <= 0f) continue;
                    // Coincident centres use the same normal on every run, independent of velocity.
                    Vector3 normal = distance > 0f ? delta / distance : Vector3.right;
                    float shareA = weights[j] / (weights[i] + weights[j]);
                    float shareB = 1f - shareA;
                    bool blockedA = wallNormals != null && Vector3.Dot(-normal, wallNormals[i]) < 0f;
                    bool blockedB = wallNormals != null && Vector3.Dot(normal, wallNormals[j]) < 0f;
                    if (!blockedA || !blockedB)
                    {
                        float positionA = blockedA ? 0f : blockedB ? 1f : shareA;
                        displacements[i] -= normal * (overlap * positionA);
                        displacements[j] += normal * (overlap * (1f - positionA));
                    }
                    float closing = Vector3.Dot(velocities[j] + velocityChanges[j] - velocities[i] - velocityChanges[i], normal);
                    if (closing >= 0f) continue;
                    float change = -(1f + restitution) * closing;
                    velocityChanges[i] -= normal * (change * shareA);
                    velocityChanges[j] += normal * (change * shareB);
                }
        }

        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
