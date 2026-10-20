using NUnit.Framework;
using ProtoHarness.ChainRush.Visuals;
using UnityEngine;

namespace ProtoHarness.Tests.EditMode
{
    public sealed class MetalChainTests
    {
        private GameObject owner;
        private MetalChain chain;

        [TearDown]
        public void TearDown()
        {
            chain?.Dispose();
            if (owner != null) Object.DestroyImmediate(owner);
        }

        [Test]
        public void Present_RotatedOwner_BoundsContainTheChainAndFacesPointOutward()
        {
            owner = new GameObject("Chain test owner");
            owner.transform.SetPositionAndRotation(new Vector3(120, -3, 50), Quaternion.Euler(15, 47, 0));
            chain = new MetalChain(owner.transform);
            Vector3 start = new Vector3(121, 1, 52), end = new Vector3(129, 6, 71);
            chain.Present(start, end);
            Mesh mesh = owner.GetComponentInChildren<MeshFilter>().sharedMesh;
            Vector3[] v = mesh.vertices, n = mesh.normals;
            int[] triangles = mesh.triangles;
            Assert.That(chain.IsVisible, Is.True);
            Assert.That(owner.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(owner.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(mesh.bounds.Contains(owner.transform.InverseTransformPoint(start)), Is.True);
            Assert.That(mesh.bounds.Contains(owner.transform.InverseTransformPoint(end)), Is.True);
            foreach (Vector3 p in v)
            {
                Assert.That(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z), Is.False);
                Assert.That(mesh.bounds.Contains(p), Is.True);
            }
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                Vector3 face = Vector3.Cross(v[b] - v[a], v[c] - v[a]).normalized;
                Assert.That(Vector3.Dot(face, (n[a] + n[b] + n[c]).normalized), Is.GreaterThan(.1f), "Back-facing chain surface at triangle " + i / 3);
            }
        }

        [Test]
        public void Present_LengthChanges_UsesTheSameMeshAndRendererAndHidesAtZeroLength()
        {
            owner = new GameObject("Chain test owner");
            chain = new MetalChain(owner.transform);
            chain.Present(Vector3.zero, Vector3.forward * 35);
            Mesh mesh = owner.GetComponentInChildren<MeshFilter>().sharedMesh;
            Material material = owner.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            int longCount = chain.LinkCount;
            chain.Present(Vector3.zero, Vector3.right * 5);
            Assert.That(chain.LinkCount, Is.LessThan(longCount));
            Assert.That(owner.GetComponentsInChildren<MeshRenderer>(), Has.Length.EqualTo(1));
            Assert.That(owner.GetComponentInChildren<MeshFilter>().sharedMesh, Is.SameAs(mesh));
            Assert.That(owner.GetComponentInChildren<MeshRenderer>().sharedMaterial, Is.SameAs(material));
            chain.Present(Vector3.one, Vector3.one);
            Assert.That(chain.IsVisible, Is.False);
            chain.Present(Vector3.zero, Vector3.up * 20);
            Assert.That(chain.IsVisible, Is.True);
        }

        [Test]
        public void Dispose_ReleasesItsModelMeshAndMaterial_AndIsSafeTwice()
        {
            owner = new GameObject("Chain test owner");
            chain = new MetalChain(owner.transform);
            chain.Present(Vector3.zero, Vector3.forward);
            Mesh mesh = owner.GetComponentInChildren<MeshFilter>().sharedMesh;
            Material material = owner.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            chain.Dispose();
            Assert.That(mesh == null, Is.True);
            Assert.That(material == null, Is.True);
            Assert.That(owner.transform.childCount, Is.Zero);
            Assert.DoesNotThrow(() => chain.Dispose());
        }
    }
}
