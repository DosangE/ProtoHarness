using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush.Track;
using UnityEngine;

namespace ProtoHarness.Tests.PlayMode
{
    // Visible test roads that follow a centerline, built beside the prototype course for tests that hand
    // the same centerline to the game with SetTrack. Everything created is added to the caller's list so
    // its TearDown can destroy it.
    internal static class TestRoad
    {
        private const float SampleStep = 0.5f;
        private const float GuardStep = 1f;
        private const float GuardThickness = 0.3f;
        // Matches the deck guards in the scenes (ChainRushSceneBuilder): blocks well above the jump apex.
        private const float GuardHeight = 4f;

        // A road strip (mesh collider, faces up) from fromS to toS, optionally with guard walls whose inner
        // faces sit at +-halfWidth.
        public static void Build(Centerline line, double fromS, double toS, float halfWidth, bool guards, List<Object> built)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null, "URP Lit shader is required for the visible test road.");
            var material = new Material(shader) { name = "Test road" };
            material.SetColor("_BaseColor", new Color(0.3f, 0.42f, 0.46f));
            built.Add(material);

            int segments = Mathf.CeilToInt((float)((toS - fromS) / SampleStep));
            var vertices = new Vector3[(segments + 1) * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                TrackFrame frame = line.FrameAt(fromS + (toS - fromS) * i / segments);
                vertices[i * 2] = frame.TransformPoint(new Vector3(-halfWidth, 0f, 0f));
                vertices[i * 2 + 1] = frame.TransformPoint(new Vector3(halfWidth, 0f, 0f));
            }
            for (int i = 0; i < segments; i++)
            {
                int left = i * 2;
                int t = i * 6;
                triangles[t] = left;
                triangles[t + 1] = left + 2;
                triangles[t + 2] = left + 1;
                triangles[t + 3] = left + 1;
                triangles[t + 4] = left + 2;
                triangles[t + 5] = left + 3;
            }
            var mesh = new Mesh { name = "Test road" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            built.Add(mesh);
            var road = new GameObject("Test Road");
            road.AddComponent<MeshFilter>().sharedMesh = mesh;
            road.AddComponent<MeshRenderer>().sharedMaterial = material;
            road.AddComponent<MeshCollider>().sharedMesh = mesh;
            built.Add(road);

            if (guards)
            {
                int count = Mathf.CeilToInt((float)((toS - fromS) / GuardStep));
                float step = (float)((toS - fromS) / count);
                for (int i = 0; i < count; i++)
                {
                    TrackFrame frame = line.FrameAt(fromS + step * (i + 0.5f));
                    for (int side = -1; side <= 1; side += 2)
                    {
                        GameObject guard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        guard.name = "Test guard";
                        guard.transform.SetPositionAndRotation(
                            frame.TransformPoint(new Vector3(side * (halfWidth + GuardThickness * 0.5f), GuardHeight * 0.5f, 0f)),
                            Quaternion.LookRotation(frame.Forward));
                        // A little longer than the step so the chord segments overlap around curves.
                        guard.transform.localScale = new Vector3(GuardThickness, GuardHeight, step + 0.1f);
                        guard.GetComponent<Renderer>().sharedMaterial = material;
                        built.Add(guard);
                    }
                }
            }
            Physics.SyncTransforms();
        }
    }
}
