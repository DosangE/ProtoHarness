using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush.Track;
using UnityEngine;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class RoadMeshBuilderTests
    {
        private const float Tolerance = 1e-3f;
        private const float HalfWidth = RoadProfile.DeckHalfWidth;
        private const float Thickness = RoadProfile.DeckThickness;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();

        [SetUp]
        public void SetUp()
        {
            vertices.Clear();
            triangles.Clear();
        }

        // Straight 10 m, then an R30 right quarter turn easing up to a 20% grade, then 20 m easing back to flat.
        private static Centerline CurvedHill()
        {
            var line = new Centerline(Vector3.zero, 0f);
            line.AppendStraight(10f);
            line.AppendArc(30f, 90f, 0.2f);
            line.AppendStraight(20f, 0f);
            return line;
        }

        // One box sweep: four faces of two vertices per sample, plus two caps of four.
        private static int SweepVertices(int samples) => (samples + 1) * 8 + 8;
        private static int SweepIndices(int samples) => samples * 24 + 12;

        [Test]
        public void Samples_ByLength_AtMostOneMetreApart()
        {
            Assert.That(RoadMeshBuilder.Samples(0d, 40d), Is.EqualTo(40));
            Assert.That(RoadMeshBuilder.Samples(0d, 40.5d), Is.EqualTo(41));
            Assert.That(RoadMeshBuilder.Samples(10d, 10.2d), Is.EqualTo(1));
        }

        [Test]
        public void AppendRoad_Straight_TopEdgesAtHalfWidthAndLevel()
        {
            var line = new Centerline(Vector3.zero, 0f);
            line.AppendStraight(40f);
            RoadMeshBuilder.AppendRoad(line, 0d, 40d, RoadProfile.Guarded, Vector3.zero, vertices, triangles);
            Assert.That(vertices.Count, Is.EqualTo(SweepVertices(40)));
            Assert.That(triangles.Count, Is.EqualTo(SweepIndices(40)));
            foreach (Vector3 vertex in vertices)
            {
                Assert.That(Mathf.Abs(vertex.x), Is.EqualTo(HalfWidth).Within(Tolerance));
                Assert.That(vertex.y, Is.EqualTo(0f).Within(Tolerance).Or.EqualTo(-Thickness).Within(Tolerance));
                Assert.That(vertex.z, Is.InRange(-Tolerance, 40f + Tolerance));
            }
        }

        [Test]
        public void AppendRoad_CurvedHill_EveryVertexOnSectionCorner()
        {
            Centerline line = CurvedHill();
            RoadMeshBuilder.AppendRoad(line, 0d, line.EndS, RoadProfile.Guarded, Vector3.zero, vertices, triangles);
            foreach (Vector3 vertex in vertices)
            {
                TrackCoord coord = line.Project(vertex);
                Assert.That(Mathf.Abs(coord.D), Is.EqualTo(HalfWidth).Within(Tolerance), "Off the road edge at S " + coord.S);
                Assert.That(coord.H, Is.EqualTo(0f).Within(Tolerance).Or.EqualTo(-Thickness).Within(Tolerance), "Off the surface or bottom at S " + coord.S);
            }
        }

        [Test]
        public void AppendRoad_ArcR30_EdgesAtInnerAndOuterRadius()
        {
            Centerline line = CurvedHill();
            RoadMeshBuilder.AppendRoad(line, 0d, line.EndS, RoadProfile.Guarded, Vector3.zero, vertices, triangles);
            // Starting at the origin facing +z and turning right, the arc's center is 30 m to the right of S = 10.
            var center = new Vector3(30f, 0f, 10f);
            double arcEnd = 10d + 30d * Math.PI / 2d;
            int inArc = 0;
            foreach (Vector3 vertex in vertices)
            {
                double s = line.Project(vertex).S;
                if (s < 10d + Tolerance || s > arcEnd - Tolerance) continue;
                inArc++;
                Vector3 flat = vertex - center;
                flat.y = 0f;
                Assert.That(flat.magnitude, Is.EqualTo(30f - HalfWidth).Within(Tolerance).Or.EqualTo(30f + HalfWidth).Within(Tolerance));
            }
            Assert.That(inArc, Is.GreaterThan(100));
        }

        // Unity draws clockwise triangles; each one's normal must point out of the slab on the face it is on.
        [Test]
        public void AppendRoad_CurvedHill_TrianglesFaceOutward()
        {
            Centerline line = CurvedHill();
            double toS = line.EndS;
            RoadMeshBuilder.AppendRoad(line, 0d, toS, RoadProfile.Guarded, Vector3.zero, vertices, triangles);
            for (int t = 0; t < triangles.Count; t += 3)
            {
                Vector3 a = vertices[triangles[t]];
                Vector3 b = vertices[triangles[t + 1]];
                Vector3 c = vertices[triangles[t + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                TrackCoord ca = line.Project(a), cb = line.Project(b), cc = line.Project(c);
                TrackFrame frame = line.FrameAt((ca.S + cb.S + cc.S) / 3d);
                Vector3 expected;
                if (AllNear(ca.S, cb.S, cc.S, 0d)) expected = -line.FrameAt(0d).Forward;
                else if (AllNear(ca.S, cb.S, cc.S, toS)) expected = line.FrameAt(toS).Forward;
                else if (AllNear(ca.H, cb.H, cc.H, 0f)) expected = Vector3.up;
                else if (AllNear(ca.H, cb.H, cc.H, -Thickness)) expected = Vector3.down;
                else if (AllNear(ca.D, cb.D, cc.D, HalfWidth)) expected = frame.Right;
                else if (AllNear(ca.D, cb.D, cc.D, -HalfWidth)) expected = -frame.Right;
                else
                {
                    Assert.Fail("Triangle " + t / 3 + " lies on no face of the slab.");
                    return;
                }
                // The top and bottom tilt with the 20% grade (cos 11 deg = 0.98).
                Assert.That(Vector3.Dot(normal, expected), Is.GreaterThan(0.9f), "Triangle " + t / 3 + " faces inward.");
            }
        }

        [Test]
        public void AppendGuards_BothEdges_SitJustOutsideRoad()
        {
            Centerline line = CurvedHill();
            RoadMeshBuilder.AppendGuards(line, 0d, line.EndS, RoadProfile.Guarded, RoadMeshBuilder.GuardBlockHeight, Vector3.zero, vertices, triangles);
            int samples = RoadMeshBuilder.Samples(0d, line.EndS);
            Assert.That(vertices.Count, Is.EqualTo(2 * SweepVertices(samples)));
            Assert.That(triangles.Count, Is.EqualTo(2 * SweepIndices(samples)));
            foreach (Vector3 vertex in vertices)
            {
                TrackCoord coord = line.Project(vertex);
                Assert.That(Mathf.Abs(coord.D), Is.EqualTo(HalfWidth).Within(Tolerance).Or.EqualTo(HalfWidth + RoadMeshBuilder.GuardThickness).Within(Tolerance));
                Assert.That(coord.H, Is.EqualTo(0f).Within(Tolerance).Or.EqualTo(RoadMeshBuilder.GuardBlockHeight).Within(Tolerance));
            }
        }

        [Test]
        public void AppendGuards_OpenLeftEdge_OnlyRightGuard()
        {
            Centerline line = CurvedHill();
            var profile = new RoadProfile(HalfWidth, Thickness, false, true);
            RoadMeshBuilder.AppendGuards(line, 0d, line.EndS, profile, RoadMeshBuilder.GuardRailHeight, Vector3.zero, vertices, triangles);
            Assert.That(vertices.Count, Is.EqualTo(SweepVertices(RoadMeshBuilder.Samples(0d, line.EndS))));
            foreach (Vector3 vertex in vertices) Assert.That(line.Project(vertex).D, Is.GreaterThan(HalfWidth - Tolerance));
        }

        [Test]
        public void AppendGuardsAndLights_NoGuards_AppendNothing()
        {
            Centerline line = CurvedHill();
            var open = new RoadProfile(HalfWidth, Thickness, false, false);
            RoadMeshBuilder.AppendGuards(line, 0d, line.EndS, open, RoadMeshBuilder.GuardRailHeight, Vector3.zero, vertices, triangles);
            RoadMeshBuilder.AppendGuardLights(line, 0d, line.EndS, open, Vector3.zero, vertices, triangles);
            Assert.That(vertices, Is.Empty);
            Assert.That(triangles, Is.Empty);
        }

        [Test]
        public void AppendGuardLights_Guarded_OnTopOfRails()
        {
            Centerline line = CurvedHill();
            RoadMeshBuilder.AppendGuardLights(line, 0d, line.EndS, RoadProfile.Guarded, Vector3.zero, vertices, triangles);
            float center = HalfWidth + RoadMeshBuilder.GuardThickness * 0.5f;
            foreach (Vector3 vertex in vertices)
            {
                TrackCoord coord = line.Project(vertex);
                Assert.That(Mathf.Abs(Mathf.Abs(coord.D) - center), Is.EqualTo(RoadMeshBuilder.GuardLightWidth * 0.5f).Within(Tolerance));
                Assert.That(coord.H, Is.EqualTo(RoadMeshBuilder.GuardRailHeight).Within(Tolerance)
                    .Or.EqualTo(RoadMeshBuilder.GuardRailHeight + RoadMeshBuilder.GuardLightHeight).Within(Tolerance));
            }
        }

        [Test]
        public void AppendRoad_SameInput_SameVertices()
        {
            RoadMeshBuilder.AppendRoad(CurvedHill(), 3.5d, 61.25d, RoadProfile.Guarded, Vector3.zero, vertices, triangles);
            var again = new List<Vector3>();
            var againTriangles = new List<int>();
            RoadMeshBuilder.AppendRoad(CurvedHill(), 3.5d, 61.25d, RoadProfile.Guarded, Vector3.zero, again, againTriangles);
            Assert.That(again, Is.EqualTo(vertices));
            Assert.That(againTriangles, Is.EqualTo(triangles));
        }

        [Test]
        public void AppendRoad_Origin_SubtractedFromEveryVertex()
        {
            Centerline line = CurvedHill();
            RoadMeshBuilder.AppendRoad(line, 0d, 30d, RoadProfile.Guarded, Vector3.zero, vertices, triangles);
            var origin = new Vector3(12f, -3f, 40f);
            var local = new List<Vector3>();
            RoadMeshBuilder.AppendRoad(line, 0d, 30d, RoadProfile.Guarded, origin, local, new List<int>());
            for (int i = 0; i < vertices.Count; i++)
                Assert.That(Vector3.Distance(local[i] + origin, vertices[i]), Is.LessThan(Tolerance));
        }

        [Test]
        public void AppendRoad_AppendsAfterExistingContent()
        {
            Centerline line = CurvedHill();
            RoadMeshBuilder.AppendRoad(line, 0d, 10d, RoadProfile.Guarded, Vector3.zero, vertices, triangles);
            int firstCount = vertices.Count;
            RoadMeshBuilder.AppendRoad(line, 10d, 20d, RoadProfile.Guarded, Vector3.zero, vertices, triangles);
            Assert.That(vertices.Count, Is.EqualTo(2 * firstCount));
            for (int i = triangles.Count / 2; i < triangles.Count; i++) Assert.That(triangles[i], Is.GreaterThanOrEqualTo(firstCount));
        }

        [Test]
        public void AppendRoad_InvalidInput_Throws()
        {
            Centerline line = CurvedHill();
            RoadProfile guarded = RoadProfile.Guarded;
            Assert.Throws<ArgumentOutOfRangeException>(() => RoadMeshBuilder.AppendRoad(line, 10d, 10d, guarded, Vector3.zero, vertices, triangles));
            Assert.Throws<ArgumentOutOfRangeException>(() => RoadMeshBuilder.AppendRoad(line, 10d, 5d, guarded, Vector3.zero, vertices, triangles));
            Assert.Throws<ArgumentOutOfRangeException>(() => RoadMeshBuilder.AppendRoad(line, double.NaN, 5d, guarded, Vector3.zero, vertices, triangles));
            Assert.Throws<ArgumentException>(() => RoadMeshBuilder.AppendRoad(line, 0d, 5d, default, Vector3.zero, vertices, triangles));
            Assert.Throws<ArgumentNullException>(() => RoadMeshBuilder.AppendRoad(null, 0d, 5d, guarded, Vector3.zero, vertices, triangles));
            Assert.Throws<ArgumentNullException>(() => RoadMeshBuilder.AppendRoad(line, 0d, 5d, guarded, Vector3.zero, null, triangles));
            Assert.Throws<ArgumentOutOfRangeException>(() => RoadMeshBuilder.AppendGuards(line, 0d, 5d, guarded, 0f, Vector3.zero, vertices, triangles));
            Assert.That(vertices, Is.Empty, "A rejected call must not append anything.");
        }

        [Test]
        public void RoadProfile_InvalidSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoadProfile(0f, 1f, true, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoadProfile(6f, -1f, true, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoadProfile(float.NaN, 1f, true, true));
            Assert.That(default(RoadProfile).IsValid, Is.False);
        }

        private static bool AllNear(double a, double b, double c, double value) =>
            Math.Abs(a - value) < Tolerance && Math.Abs(b - value) < Tolerance && Math.Abs(c - value) < Tolerance;
    }
}
