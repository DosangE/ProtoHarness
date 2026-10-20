using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProtoHarness.ChainRush.Visuals
{
    // A single reusable mesh of interlocked steel capsule links; no physics objects or per-frame allocations.
    public sealed class MetalChain : IDisposable
    {
        private const int Capacity = 256, Around = 24, Tube = 6;
        private const int VerticesPerLink = Around * Tube, IndicesPerLink = Around * Tube * 6;
        private const float Pitch = .27f, Radius = .084f, Straight = .09f, Wire = .025f;
        private readonly GameObject model;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly Material material;
        private readonly Texture2D grain;
        private readonly Vector3[] vertices = new Vector3[Capacity * VerticesPerLink];
        private readonly Vector3[] normals = new Vector3[Capacity * VerticesPerLink];
        private readonly Vector2[] uv = new Vector2[Capacity * VerticesPerLink];
        private readonly int[] indices = new int[Capacity * IndicesPerLink];
        private readonly Vector3[] shape = new Vector3[VerticesPerLink];
        private readonly Vector3[] shapeNormals = new Vector3[VerticesPerLink];
        private int drawn;
        public int LinkCount => drawn;
        public bool IsVisible => renderer != null && renderer.enabled;

        public MetalChain(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("MetalChain: the URP Lit shader is required.");
            model = new GameObject("Steel chain mesh");
            model.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Interlocked steel chain" };
            mesh.MarkDynamic();
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = model.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            material = new Material(shader) { name = "Brushed chain steel (runtime)", enableInstancing = true };
            grain = Grain();
            material.SetTexture("_BaseMap", grain);
            material.SetColor("_BaseColor", new Color(.63f, .69f, .75f));
            material.SetFloat("_Metallic", .87f);
            material.SetFloat("_Smoothness", .48f);
            material.SetColor("_EmissionColor", new Color(.018f, .024f, .029f));
            material.EnableKeyword("_EMISSION");
            renderer.sharedMaterial = material;
            renderer.enabled = false;
            BuildTemplate();
        }

        private void BuildTemplate()
        {
            for (int a = 0; a < Around; a++)
            {
                float u = a * Mathf.PI * 2 / Around;
                float x = Mathf.Cos(u), z = Mathf.Sin(u);
                Vector3 centre = new Vector3(x * Radius, 0, z * Radius + (a < Around / 2 ? Straight : -Straight));
                Vector3 outward = new Vector3(x, 0, z);
                for (int b = 0; b < Tube; b++)
                {
                    float v = b * Mathf.PI * 2 / Tube;
                    int i = a * Tube + b;
                    shapeNormals[i] = outward * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                    shape[i] = centre + shapeNormals[i] * Wire;
                }
            }
            for (int link = 0; link < Capacity; link++)
                for (int a = 0; a < Around; a++)
                    for (int b = 0; b < Tube; b++)
                    {
                        int v = link * VerticesPerLink + a * Tube + b;
                        uv[v] = new Vector2((float)a / Around, (float)b / Tube);
                        int nextA = link * VerticesPerLink + (a + 1) % Around * Tube;
                        int nextB = (b + 1) % Tube;
                        int i = link * IndicesPerLink + (a * Tube + b) * 6;
                        // Tangents around the capsule and its wire point outward in this order.
                        indices[i] = v; indices[i + 1] = link * VerticesPerLink + a * Tube + nextB; indices[i + 2] = nextA + b;
                        indices[i + 3] = indices[i + 1]; indices[i + 4] = nextA + nextB; indices[i + 5] = nextA + b;
                    }
        }

        public void Present(Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start;
            float length = delta.magnitude;
            if (length < .025f) { Hide(); return; }
            int count = Mathf.Clamp(Mathf.CeilToInt(length / Pitch), 1, Capacity);
            float step = length / count;
            Quaternion facing = Quaternion.LookRotation(delta);
            Matrix4x4 local = model.transform.worldToLocalMatrix;
            for (int link = 0; link < count; link++)
            {
                Quaternion rotation = facing * Quaternion.AngleAxis((link & 1) * 90, Vector3.forward);
                Vector3 centre = start + delta * ((link + .5f) / count);
                float stretch = step / Pitch;
                for (int vertex = 0; vertex < VerticesPerLink; vertex++)
                {
                    int i = link * VerticesPerLink + vertex;
                    Vector3 p = shape[vertex];
                    p.z *= stretch;
                    Vector3 n = shapeNormals[vertex];
                    n.z /= Mathf.Max(.01f, stretch);
                    vertices[i] = local.MultiplyPoint3x4(centre + rotation * p);
                    normals[i] = local.MultiplyVector(rotation * n.normalized).normalized;
                }
            }
            // Clear the old submesh before shrinking its vertex buffer.
            if (drawn != count) mesh.Clear(false);
            int used = count * VerticesPerLink;
            mesh.SetVertices(vertices, 0, used);
            mesh.SetNormals(normals, 0, used);
            if (drawn != count)
            {
                mesh.SetUVs(0, uv, 0, used);
                mesh.SetIndices(indices, 0, count * IndicesPerLink, MeshTopology.Triangles, 0, false);
            }
            Vector3 a = local.MultiplyPoint3x4(start), b = local.MultiplyPoint3x4(end);
            mesh.bounds = new Bounds((a + b) * .5f, new Vector3(Mathf.Abs(a.x - b.x) + .6f, Mathf.Abs(a.y - b.y) + .6f, Mathf.Abs(a.z - b.z) + .6f));
            drawn = count;
            renderer.enabled = true;
        }

        public void Hide() { if (renderer != null) renderer.enabled = false; }

        private static Texture2D Grain()
        {
            const int width = 128, height = 32;
            var p = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int hash = (x * 73 + y * 151 + x * y * 17) & 255;
                    float shade = .79f + hash / 255f * .17f;
                    if (y % 9 == 0) shade *= .84f; // fine longitudinal brushing
                    if (x >= 61 && x <= 64) shade *= .69f; // welded closure
                    p[y * width + x] = new Color(shade, shade, shade, 1);
                }
            var t = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Steel brushing and weld", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            t.SetPixels(p); t.Apply(false, true);
            return t;
        }

        public void Dispose()
        {
            Destroy(model); Destroy(mesh); Destroy(material); Destroy(grain);
        }

        private static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
