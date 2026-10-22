using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush.Visuals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProtoHarness.Tests.EditMode
{
    // A runner's look (RunnerLook): a model with the same hierarchy lends its meshes and materials to the race's runner
    // model, paired by child order; anything that does not fit is refused before the model changes.
    public sealed class RunnerLookTests
    {
        private readonly List<Object> made = new List<Object>();
        private Shader shader;

        [SetUp]
        public void SetUp()
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null, "URP Lit shader is required for the test materials.");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object thing in made)
                if (thing != null) Object.DestroyImmediate(thing);
            made.Clear();
        }

        [Test]
        public void Apply_MatchingLook_CopiesMeshesAndMaterialsAndKeepsTransforms()
        {
            Transform model = Build("Armored Runner", "model", 0f);
            Transform look = Build("RunnerBeta", "look", 10f);
            Vector3[] positions = LocalPositions(model);
            RunnerLook.Apply(model, look.gameObject);
            MeshFilter[] modelFilters = model.GetComponentsInChildren<MeshFilter>(true);
            MeshFilter[] lookFilters = look.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(modelFilters.Length, Is.EqualTo(4));
            for (int i = 0; i < modelFilters.Length; i++)
            {
                Assert.That(modelFilters[i].sharedMesh, Is.SameAs(lookFilters[i].sharedMesh), modelFilters[i].name);
                Assert.That(modelFilters[i].GetComponent<MeshRenderer>().sharedMaterials,
                    Is.EqualTo(lookFilters[i].GetComponent<MeshRenderer>().sharedMaterials), modelFilters[i].name);
            }
            Assert.That(model.Find("Torso Joint").GetComponent<MeshRenderer>().sharedMaterials.Length, Is.EqualTo(2),
                "Every material of a renderer is copied.");
            Assert.That(LocalPositions(model), Is.EqualTo(positions), "The model's transforms stay as they were.");
            Assert.That(model.name, Is.EqualTo("Armored Runner"), "Only meshes and materials change.");
        }

        [Test]
        public void Apply_TwoSiblingsOfOneName_PairsThemByOrder()
        {
            Transform model = Build("Armored Runner", "model", 0f);
            Transform look = Build("RunnerBeta", "look", 10f);
            RunnerLook.Apply(model, look.gameObject);
            Transform modelTorso = model.Find("Torso Joint"), lookTorso = look.Find("Torso Joint");
            Assert.That(modelTorso.GetChild(0).name, Is.EqualTo(modelTorso.GetChild(1).name), "The test needs two of one name.");
            for (int i = 0; i < 2; i++)
                Assert.That(modelTorso.GetChild(i).GetComponent<MeshFilter>().sharedMesh,
                    Is.SameAs(lookTorso.GetChild(i).GetComponent<MeshFilter>().sharedMesh), "Back circuit " + i);
            Assert.That(modelTorso.GetChild(0).GetComponent<MeshFilter>().sharedMesh,
                Is.Not.SameAs(modelTorso.GetChild(1).GetComponent<MeshFilter>().sharedMesh));
        }

        [Test]
        public void Apply_ChildNameDiffers_ThrowsAndLeavesTheModel()
        {
            Transform model = Build("Armored Runner", "model", 0f);
            Transform look = Build("RunnerBeta", "look", 10f);
            look.Find("Torso Joint").GetChild(1).name = "Front circuit";
            AssertRefused(model, look, "Torso Joint/Back circuit[2]");
        }

        [Test]
        public void Apply_ChildCountDiffers_ThrowsAndLeavesTheModel()
        {
            Transform model = Build("Armored Runner", "model", 0f);
            Transform look = Build("RunnerBeta", "look", 10f);
            Track(new GameObject("Spur")).transform.SetParent(look.Find("Left Leg Joint"), false);
            AssertRefused(model, look, "Left Leg Joint");
        }

        [Test]
        public void Apply_RendererMissing_ThrowsAndLeavesTheModel()
        {
            Transform model = Build("Armored Runner", "model", 0f);
            Transform look = Build("RunnerBeta", "look", 10f);
            // The last part in order, so every part before it was checked and paired, and still nothing changes.
            Object.DestroyImmediate(look.Find("Left Leg Joint/Boot").GetComponent<MeshRenderer>());
            AssertRefused(model, look, "Left Leg Joint/Boot");
        }

        [Test]
        public void Apply_Null_Throws()
        {
            Transform model = Build("Armored Runner", "model", 0f);
            Assert.Throws<ArgumentNullException>(() => RunnerLook.Apply(null, model.gameObject));
            Assert.Throws<ArgumentNullException>(() => RunnerLook.Apply(model, null));
        }

        // ---- helpers -------------------------------------------------------------------------------

        // The look is refused with the path in the message, and the model keeps every mesh and material it had.
        private void AssertRefused(Transform model, Transform look, string path)
        {
            MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
            var meshes = new Mesh[filters.Length];
            var materials = new Material[filters.Length][];
            for (int i = 0; i < filters.Length; i++)
            {
                meshes[i] = filters[i].sharedMesh;
                materials[i] = filters[i].GetComponent<MeshRenderer>().sharedMaterials;
            }
            var error = Assert.Throws<ArgumentException>(() => RunnerLook.Apply(model, look.gameObject));
            Assert.That(error.Message, Does.Contain(look.name));
            Assert.That(error.Message, Does.Contain("'" + path + "'"));
            for (int i = 0; i < filters.Length; i++)
            {
                Assert.That(filters[i].sharedMesh, Is.SameAs(meshes[i]), filters[i].name);
                Assert.That(filters[i].GetComponent<MeshRenderer>().sharedMaterials, Is.EqualTo(materials[i]), filters[i].name);
            }
        }

        // root
        //   Torso Joint (rendered, two materials)
        //     Back circuit (rendered)
        //     Back circuit (rendered)
        //   Left Leg Joint (not rendered)
        //     Boot (rendered)
        // Every part has its own mesh and materials, named after tag; offset moves every part so transforms differ.
        private Transform Build(string rootName, string tag, float offset)
        {
            Transform root = Track(new GameObject(rootName)).transform;
            Transform torso = Part("Torso Joint", root, tag, offset, 2);
            Part("Back circuit", torso, tag, offset, 1);
            Part("Back circuit", torso, tag, offset, 1);
            Transform leg = Part("Left Leg Joint", root, tag, offset, 0);
            Part("Boot", leg, tag, offset, 1);
            return root;
        }

        private Transform Part(string name, Transform parent, string tag, float offset, int materialCount)
        {
            var part = Track(new GameObject(name)).transform;
            part.SetParent(parent, false);
            part.localPosition = new Vector3(offset + parent.childCount, offset, parent.childCount * 0.5f);
            if (materialCount == 0) return part;
            string id = tag + " " + name + " " + parent.childCount;
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = Track(new Mesh { name = id });
            var materials = new Material[materialCount];
            for (int i = 0; i < materialCount; i++) materials[i] = Track(new Material(shader) { name = id + " " + i });
            part.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return part;
        }

        private static Vector3[] LocalPositions(Transform root)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            var positions = new Vector3[all.Length];
            for (int i = 0; i < all.Length; i++) positions[i] = all[i].localPosition;
            return positions;
        }

        private T Track<T>(T thing) where T : Object
        {
            made.Add(thing);
            return thing;
        }
    }
}
