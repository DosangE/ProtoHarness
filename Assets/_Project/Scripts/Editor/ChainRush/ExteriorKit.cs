using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProtoHarness.Editor.ChainRush
{
    // The shapes every exterior is made of (built-in cube, sphere and cylinder, or a kit FBX's mesh; no shadows, static
    // batching) and the fixed scatter that varies them by place. The builders call these as their own (using static).
    public static class ExteriorKit
    {
        private static Mesh cube, sphere, cylinder;

        public static Mesh Cube => Builtin(ref cube, "Cube.fbx");
        public static Mesh Sphere => Builtin(ref sphere, "Sphere.fbx");
        public static Mesh Cylinder => Builtin(ref cylinder, "Cylinder.fbx");

        private static Mesh Builtin(ref Mesh cache, string name)
        {
            if (cache == null) cache = Resources.GetBuiltinResource<Mesh>(name);
            if (cache == null) throw new InvalidOperationException("Missing built-in decoration mesh: " + name);
            return cache;
        }

        // A mesh from a decoration kit FBX in Art/Models (docs/ART_REQUEST_PROPS.md): one object = one mesh = one material
        // slot, pivot at the bounds centre like the built-in shapes, so it replaces a Shape call's mesh without moving it.
        public static Mesh KitMesh(string kit, string name)
        {
            if (string.IsNullOrEmpty(kit)) throw new ArgumentException("A kit name is required.", nameof(kit));
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A mesh name is required.", nameof(name));
            string path = ExteriorAssets.ModelFolder + kit + ".fbx";
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Mesh mesh && mesh.name == name)
                {
                    if (mesh.subMeshCount != 1)
                        throw new InvalidOperationException($"Kit mesh {name} in {path} has {mesh.subMeshCount} material slots; a kit mesh has one.");
                    return mesh;
                }
            throw new InvalidOperationException($"Missing decoration kit mesh {name} in {path}");
        }

        public static Transform Group(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        public static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
            => Shape(name, parent, position, scale, material, Cube);

        // A square bar of width from a to b.
        public static GameObject Beam(string name, Transform parent, Vector3 a, Vector3 b, float width, Material material)
        {
            GameObject beam = Box(name, parent, (a + b) * 0.5f, new Vector3(width, (b - a).magnitude, width), material);
            beam.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            return beam;
        }

        // scale is the shape's size in metres: the built-in cube is 1 m, the sphere and the cylinder 2 m.
        public static GameObject Shape(string name, Transform parent, Vector3 position, Vector3 scale, Material material, Mesh mesh)
        {
            var shape = new GameObject(name);
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            Vector3 meshSize = mesh.bounds.size;
            shape.transform.localScale = new Vector3(scale.x / meshSize.x, scale.y / meshSize.y, scale.z / meshSize.z);
            shape.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = shape.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(shape, StaticEditorFlags.BatchingStatic);
            return shape;
        }

        // Facing forward, level.
        public static Quaternion Flat(Vector3 forward)
        {
            forward.y = 0f;
            return Quaternion.LookRotation(forward.sqrMagnitude > 1e-6f ? forward : Vector3.forward);
        }

        // A fixed scatter in 0..1 from a seed, so every building differs and a rebuild gives the same street.
        public static float Hash(int seed) => Mathf.Repeat(Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f, 1f);

        public static Material Pick(int seed, params Material[] choices) => choices[Mathf.Min(choices.Length - 1, (int)(Hash(seed) * choices.Length))];
    }
}
