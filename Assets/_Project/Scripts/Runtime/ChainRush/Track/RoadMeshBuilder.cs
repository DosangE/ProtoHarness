using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoHarness.ChainRush.Track
{
    // Sweeps road and guard cross-sections along a centerline from fromS to toS (COURSE.md 3-2, T3a).
    // Every part is a closed box section in track axes (x = right, y = up from the centerline height),
    // sampled at most SampleStep apart, written relative to origin so a piece keeps small local values.
    // The output depends only on the centerline, the S range and the profile, so the same input gives the
    // same vertices. Methods append to the caller's buffers and allocate nothing themselves.
    public static class RoadMeshBuilder
    {
        // R30 arcs sag 1 / (8 x 30) = 4 mm between samples; 20% vertical curves well under 1 mm.
        public const float SampleStep = 1f;
        // The scene deck guards (ChainRushSceneBuilder.AddGuards): a 0.3 m rail showing 1.1 m above the deck,
        // a light strip on top, and a collider that reaches 4 m, above the 2.9 m jump apex.
        public const float GuardThickness = 0.3f;
        public const float GuardRailHeight = 1.1f;
        public const float GuardBlockHeight = 4f;
        public const float GuardLightWidth = 0.12f;
        public const float GuardLightHeight = 0.06f;

        // Sample count for a sweep; also the number of quads along each face.
        public static int Samples(double fromS, double toS)
        {
            RequireRange(fromS, toS);
            return Math.Max(1, (int)Math.Ceiling((toS - fromS) / SampleStep - 1e-6));
        }

        // The road slab: the top surface level at the centerline height, sides, bottom and end caps.
        public static void AppendRoad(Centerline line, double fromS, double toS, RoadProfile profile, Vector3 origin,
            List<Vector3> vertices, List<int> triangles)
        {
            Require(line, fromS, toS, profile, vertices, triangles);
            AppendSweep(line, fromS, toS, -profile.HalfWidth, profile.HalfWidth, -profile.Thickness, 0f, origin, vertices, triangles);
        }

        // A guard on each guarded edge, just outside the road, from the road top up to height: the visible
        // rail uses GuardRailHeight, the collider GuardBlockHeight. Open edges get nothing.
        public static void AppendGuards(Centerline line, double fromS, double toS, RoadProfile profile, float height, Vector3 origin,
            List<Vector3> vertices, List<int> triangles)
        {
            Require(line, fromS, toS, profile, vertices, triangles);
            if (!(height > 0f) || float.IsInfinity(height))
                throw new ArgumentOutOfRangeException(nameof(height), height, "Guard height must be positive and finite.");
            float inner = profile.HalfWidth;
            float outer = profile.HalfWidth + GuardThickness;
            if (profile.LeftGuard) AppendSweep(line, fromS, toS, -outer, -inner, 0f, height, origin, vertices, triangles);
            if (profile.RightGuard) AppendSweep(line, fromS, toS, inner, outer, 0f, height, origin, vertices, triangles);
        }

        // The light strip along the top of each rail.
        public static void AppendGuardLights(Centerline line, double fromS, double toS, RoadProfile profile, Vector3 origin,
            List<Vector3> vertices, List<int> triangles)
        {
            Require(line, fromS, toS, profile, vertices, triangles);
            float center = profile.HalfWidth + GuardThickness * 0.5f;
            float half = GuardLightWidth * 0.5f;
            const float bottom = GuardRailHeight;
            const float top = GuardRailHeight + GuardLightHeight;
            if (profile.LeftGuard) AppendSweep(line, fromS, toS, -center - half, -center + half, bottom, top, origin, vertices, triangles);
            if (profile.RightGuard) AppendSweep(line, fromS, toS, center - half, center + half, bottom, top, origin, vertices, triangles);
        }

        // Corners go clockwise around the section seen looking forward: top-left, top-right, bottom-right,
        // bottom-left. Face f runs from corner f to corner f + 1 (top, right, bottom, left), so with forward as
        // "up" each face's first edge is on its left and triangles wind clockwise seen from outside, Unity's
        // front face. Each face has its own vertices so the normals stay sharp at the corners.
        private static void AppendSweep(Centerline line, double fromS, double toS, float x0, float x1, float y0, float y1,
            Vector3 origin, List<Vector3> vertices, List<int> triangles)
        {
            int samples = Samples(fromS, toS);
            int first = vertices.Count;
            for (int i = 0; i <= samples; i++)
            {
                TrackFrame frame = line.FrameAt(fromS + (toS - fromS) * i / samples);
                for (int face = 0; face < 4; face++)
                {
                    vertices.Add(Corner(frame, face, x0, x1, y0, y1) - origin);
                    vertices.Add(Corner(frame, (face + 1) & 3, x0, x1, y0, y1) - origin);
                }
            }
            for (int i = 0; i < samples; i++)
                for (int face = 0; face < 4; face++)
                {
                    int a = first + (i * 4 + face) * 2;
                    int next = a + 8;
                    triangles.Add(a);
                    triangles.Add(next);
                    triangles.Add(a + 1);
                    triangles.Add(a + 1);
                    triangles.Add(next);
                    triangles.Add(next + 1);
                }
            AppendCap(line.FrameAt(fromS), false, x0, x1, y0, y1, origin, vertices, triangles);
            AppendCap(line.FrameAt(toS), true, x0, x1, y0, y1, origin, vertices, triangles);
        }

        // The start cap faces back along the track and the end cap forward.
        private static void AppendCap(TrackFrame frame, bool facingForward, float x0, float x1, float y0, float y1,
            Vector3 origin, List<Vector3> vertices, List<int> triangles)
        {
            int c = vertices.Count;
            for (int corner = 0; corner < 4; corner++) vertices.Add(Corner(frame, corner, x0, x1, y0, y1) - origin);
            if (facingForward)
            {
                triangles.Add(c); triangles.Add(c + 2); triangles.Add(c + 1);
                triangles.Add(c); triangles.Add(c + 3); triangles.Add(c + 2);
            }
            else
            {
                triangles.Add(c); triangles.Add(c + 1); triangles.Add(c + 2);
                triangles.Add(c); triangles.Add(c + 2); triangles.Add(c + 3);
            }
        }

        private static Vector3 Corner(TrackFrame frame, int corner, float x0, float x1, float y0, float y1)
        {
            float x = corner == 0 || corner == 3 ? x0 : x1;
            float y = corner < 2 ? y1 : y0;
            return frame.TransformPoint(new Vector3(x, y, 0f));
        }

        private static void Require(Centerline line, double fromS, double toS, RoadProfile profile, List<Vector3> vertices, List<int> triangles)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            if (triangles == null) throw new ArgumentNullException(nameof(triangles));
            if (!profile.IsValid) throw new ArgumentException("Road profile is uninitialized; construct it with a width and thickness.", nameof(profile));
            RequireRange(fromS, toS);
        }

        private static void RequireRange(double fromS, double toS)
        {
            if (double.IsNaN(fromS) || double.IsInfinity(fromS))
                throw new ArgumentOutOfRangeException(nameof(fromS), fromS, "Start S must be finite.");
            if (double.IsNaN(toS) || double.IsInfinity(toS) || !(toS > fromS))
                throw new ArgumentOutOfRangeException(nameof(toS), toS, "End S must be finite and past the start.");
        }
    }
}
