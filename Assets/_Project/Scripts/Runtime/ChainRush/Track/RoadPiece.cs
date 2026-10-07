using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ProtoHarness.ChainRush.Track
{
    // One reusable stretch of procedural road (COURSE.md 3-2, T3a): the slab with its collider, the visible
    // guard rails and lights, and an invisible guard wall collider. The objects and meshes are made once;
    // Build refills the meshes for a new S range so a pool can move pieces down the track without
    // allocating. The root sits at the centerline point at FromS and the meshes are local to it, so an
    // origin shift only moves the root.
    public sealed class RoadPiece
    {
        private readonly GameObject root;
        private readonly Mesh roadMesh;
        private readonly Mesh railMesh;
        private readonly Mesh wallMesh;
        private readonly Mesh lightMesh;
        private readonly MeshCollider roadCollider;
        private readonly MeshCollider wallCollider;
        private readonly GameObject rails;
        private readonly GameObject wall;
        private readonly GameObject lights;
        private readonly List<Vector3> vertices = new List<Vector3>(1024);
        private readonly List<int> triangles = new List<int>(2048);

        public RoadPiece(string name, Transform parent, Material road, Material rail, Material light)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A road piece needs a name.", nameof(name));
            if (road == null) throw new ArgumentNullException(nameof(road));
            if (rail == null) throw new ArgumentNullException(nameof(rail));
            if (light == null) throw new ArgumentNullException(nameof(light));
            root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, false);
            roadMesh = NewMesh(name + " road");
            railMesh = NewMesh(name + " guard rails");
            wallMesh = NewMesh(name + " guard wall");
            lightMesh = NewMesh(name + " guard lights");
            Visible("Road", roadMesh, road);
            // Colliders sit on their own objects. A MeshCollider added beside the road's MeshFilter kept a null
            // mesh after Build assigned one (2026-10-07 PlayMode diagnostic); the guard wall layout worked.
            roadCollider = Child("Road collider").AddComponent<MeshCollider>();
            rails = Visible("Guard rails", railMesh, rail);
            lights = Visible("Guard lights", lightMesh, light);
            wall = Child("Guard wall");
            wallCollider = wall.AddComponent<MeshCollider>();
            root.SetActive(false);
        }

        public Transform Transform => root.transform;
        public bool IsBuilt { get; private set; }
        public double FromS { get; private set; }
        public double ToS { get; private set; }
        public RoadProfile Profile { get; private set; }

        // Rebuilds this piece for [fromS, toS] of line with profile and shows it.
        public void Build(Centerline line, double fromS, double toS, RoadProfile profile)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (root == null) throw new InvalidOperationException("RoadPiece: its objects were destroyed; make a new piece.");
            if (!profile.IsValid) throw new ArgumentException("Road profile is uninitialized; construct it with a width and thickness.", nameof(profile));
            // Checks the range before anything changes, so a rejected build leaves the piece as it was.
            RoadMeshBuilder.Samples(fromS, toS);
            Vector3 origin = line.FrameAt(fromS).Position;
            root.transform.SetPositionAndRotation(origin, Quaternion.identity);

            Clear();
            RoadMeshBuilder.AppendRoad(line, fromS, toS, profile, origin, vertices, triangles);
            roadCollider.sharedMesh = null;
            Fill(roadMesh);
            roadCollider.sharedMesh = roadMesh;

            bool guarded = profile.HasGuard;
            rails.SetActive(guarded);
            wall.SetActive(guarded);
            lights.SetActive(guarded);
            // Detach before refilling so the collider never holds a mesh mid-rebuild; reassigning cooks it again.
            wallCollider.sharedMesh = null;
            if (guarded)
            {
                Clear();
                RoadMeshBuilder.AppendGuards(line, fromS, toS, profile, RoadMeshBuilder.GuardRailHeight, origin, vertices, triangles);
                Fill(railMesh);
                Clear();
                RoadMeshBuilder.AppendGuards(line, fromS, toS, profile, RoadMeshBuilder.GuardBlockHeight, origin, vertices, triangles);
                Fill(wallMesh);
                wallCollider.sharedMesh = wallMesh;
                Clear();
                RoadMeshBuilder.AppendGuardLights(line, fromS, toS, profile, origin, vertices, triangles);
                Fill(lightMesh);
            }

            FromS = fromS;
            ToS = toS;
            Profile = profile;
            IsBuilt = true;
            root.SetActive(true);
        }

        // Hides the piece until the next Build, for a pool to hold it.
        public void Hide()
        {
            root.SetActive(false);
            IsBuilt = false;
        }

        public void ShiftOrigin(Vector3 offset)
        {
            root.transform.position += offset;
        }

        // Destroys the objects and meshes this piece made. The materials belong to the caller.
        public void Destroy()
        {
            Object.Destroy(root);
            Object.Destroy(roadMesh);
            Object.Destroy(railMesh);
            Object.Destroy(wallMesh);
            Object.Destroy(lightMesh);
        }

        private void Clear()
        {
            vertices.Clear();
            triangles.Clear();
        }

        private void Fill(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
        }

        private static Mesh NewMesh(string name) => new Mesh { name = name };

        private GameObject Child(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            return child;
        }

        private GameObject Visible(string name, Mesh mesh, Material material)
        {
            GameObject child = Child(name);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
            return child;
        }
    }
}
