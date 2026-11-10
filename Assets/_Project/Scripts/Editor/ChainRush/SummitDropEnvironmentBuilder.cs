using System;
using System.Collections.Generic;
using System.IO;
using ProtoHarness.ChainRush.Track;
using ProtoHarness.ChainRush.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static ProtoHarness.Editor.ChainRush.ExteriorKit;

namespace ProtoHarness.Editor.ChainRush
{
    // Original downhill scenery: the ground follows the road from the summit to the village.
    // Like Palm Coast, only assets are saved; Create opens an unsaved decoration scene last.
    public static class SummitDropEnvironmentBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/SummitDropExterior.prefab";
        public const string ScenePath = "Assets/_Project/Scenes/ChainRushTrackSummitDrop.unity";
        public const string LookPath = "Assets/_Project/Data/TrackLook_SummitDrop.asset";

        [MenuItem("ProtoHarness/Chain Rush/Create Summit Drop Exterior")]
        public static void CreateExterior()
        {
            RequireIdle();
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Summit Drop scene already exists; use Rebuild Summit Drop Exterior.");
            // BuildAssets allows inspection and iteration before opening the one unsaved scene.
            if (!File.Exists(PrefabPath)) BuildAssets();
            ExteriorAssets.Require<ProtoHarness.ChainRush.Race.TrackLook>(LookPath);
            GameObject instance = ExteriorAssets.OpenDecorationScene(PrefabPath, "ChainRushTrackSummitDrop");
            Debug.Log($"Summit Drop exterior created. Decoration scene is UNSAVED: save as {ScenePath}; use {LookPath} for SUMMIT DROP.", instance);
        }

        [MenuItem("ProtoHarness/Chain Rush/Rebuild Summit Drop Exterior")]
        public static void RebuildExterior()
        {
            if (!File.Exists(PrefabPath)) throw new InvalidOperationException("Build Summit Drop assets first.");
            BuildAssets();
        }

        // Asset-only entry point: no scene is opened, modified or saved during iteration.
        public static void BuildAssets()
        {
            RequireIdle();
            var layout = new Layout(ExteriorAssets.RequireDefinition(ChainRushCircuitSceneBuilder.SummitDropPath));
            ExteriorAssets.SavePrefab(PrefabPath, layout.Build, layout.ValidateClearance);
            // Last, so a layout that fails its checks leaves the look as it was.
            BuildLook();
        }

        private static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before building Summit Drop.");
            ExteriorAssets.RequireIdle();
        }

        [MenuItem("ProtoHarness/Chain Rush/Rebuild Summit Drop Look")]
        public static void BuildLook()
        {
            RequireIdle();
            ExteriorAssets.BuildLook(LookPath,
                ExteriorAssets.Surface("Summit", "Road", new Color(0.39f, 0.29f, 0.18f), ExteriorAssets.Grain.Asphalt, 2.8f, 0.02f),
                ExteriorAssets.Surface("Summit", "Wall", new Color(0.34f, 0.25f, 0.16f), ExteriorAssets.Grain.Concrete, 3f, 0.02f),
                Palette("Marking", new Color(0.49f, 0.40f, 0.27f)), Palette("Accent", new Color(0.67f, 0.51f, 0.24f)),
                new ExteriorAssets.Lanes(7.2f, 20f, false, false),
                ExteriorAssets.BuildSky("Summit", new Color(0.19f, 0.43f, 0.70f), new Color(0.73f, 0.80f, 0.82f),
                    new Color(0.93f, 0.94f, 0.92f), 0.23f, false, 230f, 850f));
        }

        private static Material Palette(string name, Color colour) => ExteriorAssets.Palette("Summit", name, colour);

        private sealed class Layout
        {
            private const float GroundStep = 2f;
            private const float PoleRadius = 3f;
            private readonly TrackDefinition definition;
            private readonly Centerline line;
            private readonly RoadCorridor corridor;
            private readonly List<TrackFrame> samples = new List<TrackFrame>();
            private readonly List<float> widths = new List<float>();
            private readonly List<Vector3> poles = new List<Vector3>();
            private readonly Dictionary<(int, int), float> heights = new Dictionary<(int, int), float>();
            private readonly List<(double Start, double End)> gaps;
            private readonly Material earth = Palette("Earth", new Color(0.31f, 0.30f, 0.20f));
            private readonly Material grass = Palette("Moss", new Color(0.28f, 0.35f, 0.22f));
            private readonly Material pineA = Palette("PineA", new Color(0.10f, 0.23f, 0.15f));
            private readonly Material pineB = Palette("PineB", new Color(0.16f, 0.29f, 0.18f));
            private readonly Material pineC = Palette("PineC", new Color(0.20f, 0.32f, 0.22f));
            private readonly Material rockA = Palette("RockA", new Color(0.39f, 0.40f, 0.37f));
            private readonly Material rockB = Palette("RockB", new Color(0.48f, 0.44f, 0.34f));
            private readonly Material rockC = Palette("RockC", new Color(0.31f, 0.34f, 0.33f));
            private readonly Material wood = Palette("Wood", new Color(0.33f, 0.23f, 0.14f));
            private readonly Material cutWood = Palette("CutWood", new Color(0.61f, 0.49f, 0.31f));
            private readonly Material ink = Palette("Ink", new Color(0.12f, 0.15f, 0.15f));
            private readonly Material cream = Palette("Cream", new Color(0.75f, 0.71f, 0.55f));
            private readonly Material amber = Palette("Amber", new Color(0.70f, 0.48f, 0.18f));
            private readonly Material water = Palette("Water", new Color(0.31f, 0.49f, 0.52f));
            private readonly Material foam = Palette("Foam", new Color(0.71f, 0.79f, 0.76f));
            private readonly Material roof = Palette("Roof", new Color(0.29f, 0.26f, 0.21f));
            private Mesh cone;
            private Mesh[] crags;
            private Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            private int terrainLeft, terrainRight, terrainBottom, terrainTop;
            private int trees, rocks, groundedRocks, skippedRocks;
            private float longestCragReach, lastCragReach;

            internal Layout(TrackDefinition definition)
            {
                this.definition = definition;
                line = definition.BuildCenterline(Vector3.zero, 0f);
                gaps = definition.Gaps();
                if (!definition.IsOpenCourse || gaps.Count != 1 || definition.Shortcuts.Count != 1)
                    throw new InvalidOperationException("Summit Drop layout changed: expected an open course, one gap and one shortcut.");
                corridor = new RoadCorridor(definition, line, 0.5d, 1.8f);
                line.GetPoles(poles);
                for (double s = -definition.StartRunway; s < definition.LapLength + definition.FinishRunout; s += 4d)
                    AddSample(line.FrameAt(s), definition.HalfWidthAt(Math.Max(0d, Math.Min(definition.LapLength, s))));
                AddSample(line.FrameAt(definition.LapLength + definition.FinishRunout), definition.HalfWidthAt(definition.LapLength));
                foreach (TrackDefinition.Shortcut shortcut in definition.Shortcuts)
                {
                    Centerline branch = TrackDefinition.ShortcutLine(line, shortcut);
                    for (double s = 0d; s <= shortcut.Length; s += 2d)
                    {
                        TrackFrame f = branch.FrameAt(s);
                        AddSample(new TrackFrame(shortcut.FromS + s / shortcut.Length * (shortcut.ToS - shortcut.FromS),
                            f.Position, f.Forward, f.Right, f.Grade, f.Curvature), shortcut.HalfWidth);
                    }
                }
                foreach (Vector3 pole in poles) corridor.Reserve(pole, PoleRadius + 3f);
            }

            private void AddSample(TrackFrame frame, float width)
            {
                samples.Add(frame);
                widths.Add(width);
                min = Vector3.Min(min, frame.Position);
                max = Vector3.Max(max, frame.Position);
            }

            internal GameObject Build(Scene scene)
            {
                var root = new GameObject("Summit Drop Exterior");
                SceneManager.MoveGameObjectToScene(root, scene);
                cone = ConeMesh();
                crags = new[] { KitMesh("RockKit", "CragA"), KitMesh("RockKit", "CragB"), KitMesh("RockKit", "CragC") };
                Terrain(Group("01 Descending mountain and valley", root.transform));
                MountainLedges(Group("09 Mountain overhangs below upper road", root.transform));
                // Reserve the four required corner landmarks before scattering optional scenery.
                Corners(Group("05 Zigzag rocks and chevrons", root.transform));
                RockWalls(Group("02 Summit and ledge rock walls", root.transform));
                Jump(Group("03 Ravine waterfall and broken bridge", root.transform));
                Fork(Group("04 Forest clearing shortcut", root.transform));
                Tunnel(Group("06 Rock tunnel", root.transform));
                Village(Group("07 Valley village and finish", root.transform));
                Forest(Group("08 Irregular pine forest", root.transform));
                PoleSpires(Group("10 Rock spires under corner poles", root.transform));
                // Validate bounds before merging; afterwards they span the whole mountain. A spire's tall bounds swell
                // toward a road beside it, so 10 is left to the vertex check in ValidateClearance, with 01, 06 and 09.
                // So are 02 and 05 (05's zigzag boards with its crags): a tilted crag whose foot follows the slope has
                // bounds that swell over a road below it (2026-11-10).
                foreach (Transform group in root.transform)
                    if (!group.name.StartsWith("01", StringComparison.Ordinal) && !group.name.StartsWith("06", StringComparison.Ordinal)
                        && !group.name.StartsWith("09", StringComparison.Ordinal) && !group.name.StartsWith("10", StringComparison.Ordinal)
                        && !group.name.StartsWith("02", StringComparison.Ordinal) && !group.name.StartsWith("05", StringComparison.Ordinal))
                        corridor.ValidateClearance(group.gameObject, float.NegativeInfinity);
                var transient = new HashSet<Mesh>();
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                    if (!EditorUtility.IsPersistent(filter.sharedMesh)) transient.Add(filter.sharedMesh);
                foreach (Transform group in root.transform) MergeByMaterial(group, group.name.Substring(0, 2));
                foreach (Mesh mesh in transient) UnityEngine.Object.DestroyImmediate(mesh);
                Debug.Log($"Summit Drop placed {trees} pines and {rocks} crags ({groundedRocks} with a foot reaching down the slope, longest {longestCragReach:F2} m; {skippedRocks} spots skipped at a drop); terrain follows main road, shortcut, runway and runout.", root);
                return root;
            }

            internal void ValidateClearance(GameObject root)
            {
                var groups = new List<Transform>();
                foreach (Transform child in root.transform) groups.Add(child);
                corridor.ValidateClearance(root, float.NegativeInfinity, null, groups, true);
                ValidatePoles(root);
                int renderers = root.GetComponentsInChildren<Renderer>(true).Length, vertices = 0;
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true)) vertices += filter.sharedMesh.vertexCount;
                if (renderers > 1500 || vertices > 2000000)
                    throw new InvalidOperationException($"Summit Drop budget exceeded: {renderers} renderers, {vertices} vertices.");
                Debug.Log($"Summit Drop validation PASS: main road and shortcut clear; {poles.Count} pole shafts clear; {renderers} renderers; {vertices} vertices; decoration physics=0.", root);
            }

            // Samples use a projected grade, not a constant summit height. The minimum constraint from
            // EVERY nearby road also protects the lower leg of switchbacks and the shortcut.
            private float Height(float x, float z)
            {
                float nearest = float.PositiveInfinity, ceiling = float.PositiveInfinity;
                float weightedHeight = 0f, totalWeight = 0f;
                int closest = 0;
                for (int i = 0; i < samples.Count; i++)
                {
                    TrackFrame f = samples[i];
                    float dx = x - f.Position.x, dz = z - f.Position.z, d2 = dx * dx + dz * dz;
                    if (d2 < nearest) { nearest = d2; closest = i; }
                    float localAlong = Mathf.Clamp(dx * f.Forward.x + dz * f.Forward.z, -2f, 2f);
                    float localS = (float)f.S + localAlong;
                    float localSide = dx * f.Right.x + dz * f.Right.z;
                    float localEdge = Mathf.Max(0f, Mathf.Sqrt(d2) - widths[i]);
                    bool cliffSide = localS < 420f || (localS >= 800f && localS < 1060f);
                    float localSlope = cliffSide ? (localSide < 0f ? -1.25f : 0.75f)
                        : (localS >= 1190f && localS < 1540f ? 0.025f : 0.16f);
                    float candidate = f.Position.y + f.Grade * localAlong - definition.RoadThickness - 0.18f
                        + Mathf.Clamp(Mathf.Max(0f, localEdge - 4f) * localSlope, -95f, 65f);
                    float weight = 1f / (d2 + 25f);
                    weight = weight * weight * weight;
                    weightedHeight += candidate * weight;
                    totalWeight += weight;
                    float reach = widths[i] + 6f;
                    if (d2 <= reach * reach)
                        ceiling = Mathf.Min(ceiling, f.Position.y + f.Grade * (dx * f.Forward.x + dz * f.Forward.z) - definition.RoadThickness - 0.18f);
                }
                TrackFrame at = samples[closest];
                Vector3 delta = new Vector3(x - at.Position.x, 0f, z - at.Position.z);
                float along = Vector3.Dot(delta, at.Forward);
                float s = (float)at.S + along;
                float edge = Mathf.Max(0f, Mathf.Sqrt(nearest) - widths[closest]);
                // Blending removes the height jumps at nearest-road Voronoi boundaries, while the
                // road ceiling below still guarantees support without ever raising land into a slab.
                float land = weightedHeight / totalWeight;
                float noise = (Mathf.PerlinNoise(x * 0.026f + 11f, z * 0.026f + 17f) - 0.5f) * Mathf.Min(edge * 0.35f, 14f);
                land += noise * Mathf.Clamp01((edge - 6f) / 14f);
                // Below the jump the ground opens into a deep, continuous ravine rather than a flat bridge support.
                foreach (var gap in gaps)
                {
                    float opening = Mathf.SmoothStep(0f, 1f, (s - (float)gap.Start + 6f) / 12f)
                        * Mathf.SmoothStep(0f, 1f, ((float)gap.End + 6f - s) / 12f);
                    land -= opening * 66f * Mathf.Clamp01(1f - edge / 140f);
                }
                // The distant skirts descend beneath the horizon fog, over 900 m beyond the course.
                land = Mathf.Lerp(land, -265f + noise, Mathf.SmoothStep(0f, 1f, (edge - 140f) / 480f));
                foreach (Vector3 pole in poles)
                    // An upper road's terrain can pass above a lower pole's entire shaft. Cutting
                    // that surface down to the lower pole excavates a false hole under the upper road.
                    if (land < pole.y + 23f && (new Vector2(x - pole.x, z - pole.z)).sqrMagnitude < 36f)
                        land = Mathf.Min(land, pole.y - 0.5f);
                return Mathf.Min(land, ceiling);
            }

            private float GridHeight(int x, int z)
            {
                var key = (x, z);
                if (!heights.TryGetValue(key, out float y))
                {
                    float px = x * GroundStep, pz = z * GroundStep;
                    // Match the coarse skirt's straight boundary exactly, avoiding T-junction cracks.
                    if ((px == terrainLeft || px == terrainRight) && z % 8 != 0)
                    {
                        float a = Mathf.Floor(pz / 16f) * 16f;
                        y = Mathf.Lerp(Height(px, a), Height(px, a + 16f), (pz - a) / 16f);
                    }
                    else if ((pz == terrainBottom || pz == terrainTop) && x % 8 != 0)
                    {
                        float a = Mathf.Floor(px / 16f) * 16f;
                        y = Mathf.Lerp(Height(a, pz), Height(a + 16f, pz), (px - a) / 16f);
                    }
                    else y = Height(px, pz);
                    heights[key] = y;
                }
                return y;
            }

            // Interpolate the actual terrain triangles so props sit on the saved mesh, even on steep slopes.
            private float GroundAt(Vector3 point)
            {
                float x = point.x / GroundStep, z = point.z / GroundStep;
                int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
                float u = x - ix, v = z - iz;
                float a = GridHeight(ix, iz), b = GridHeight(ix + 1, iz), c = GridHeight(ix, iz + 1), d = GridHeight(ix + 1, iz + 1);
                return u + v <= 1f ? a + (b - a) * u + (c - a) * v : d + (c - d) * (1f - u) + (b - d) * (1f - v);
            }

            private void Terrain(Transform parent)
            {
                int left = Mathf.FloorToInt((min.x - 96f) / 32f) * 32, right = Mathf.CeilToInt((max.x + 96f) / 32f) * 32;
                int bottom = Mathf.FloorToInt((min.z - 96f) / 32f) * 32, top = Mathf.CeilToInt((max.z + 96f) / 32f) * 32;
                terrainLeft = left; terrainRight = right; terrainBottom = bottom; terrainTop = top;
                // Two-metre faces close to the road; outer skirts are outside every road's footprint.
                Patch(parent, "Mountain", left, right, bottom, top, 2, grass);
                Patch(parent, "West skirt", left - 928, left, bottom - 928, top + 928, 16, earth);
                Patch(parent, "East skirt", right, right + 928, bottom - 928, top + 928, 16, earth);
                Patch(parent, "South skirt", left, right, bottom - 928, bottom, 16, earth);
                Patch(parent, "North skirt", left, right, top, top + 928, 16, earth);
            }

            private void Patch(Transform parent, string name, int x0, int x1, int z0, int z1, int step, Material material)
            {
                int nx = (x1 - x0) / step + 1, nz = (z1 - z0) / step + 1;
                var points = new List<Vector3>(nx * nz);
                var triangles = new List<int>((nx - 1) * (nz - 1) * 6);
                var cliffs = new List<int>();
                for (int z = z0; z <= z1; z += step)
                    for (int x = x0; x <= x1; x += step) points.Add(new Vector3(x, GridHeight(x / 2, z / 2), z));
                for (int z = 0; z < nz - 1; z++)
                    for (int x = 0; x < nx - 1; x++)
                    {
                        int a = z * nx + x, b = a + 1, c = a + nx, d = c + 1;
                        Face(a, c, b);
                        Face(b, c, d);
                    }
                TerrainPart(name, parent, points, triangles, material);
                if (cliffs.Count != 0) TerrainPart(name + " exposed rock", parent, points, cliffs, rockA);

                void Face(int a, int b, int c)
                {
                    Vector3 normal = Vector3.Cross(points[b] - points[a], points[c] - points[a]).normalized;
                    List<int> face = normal.y < 0.64f ? cliffs : triangles;
                    face.Add(a); face.Add(b); face.Add(c);
                }
            }

            private static void TerrainPart(string name, Transform parent, List<Vector3> vertices, List<int> triangles, Material material)
            {
                if (triangles.Count == 0) return;
                var used = new bool[vertices.Count];
                foreach (int index in triangles) used[index] = true;
                var remap = new int[vertices.Count];
                var compact = new List<Vector3>();
                for (int i = 0; i < used.Length; i++)
                    if (used[i]) { remap[i] = compact.Count; compact.Add(vertices[i]); }
                for (int i = 0; i < triangles.Count; i++) triangles[i] = remap[triangles[i]];
                MeshObject(name, parent, compact, triangles, material);
            }

            // How far below the ground a placed thing's base sits.
            private const float GroundSink = 0.45f;

            // reserve false leaves the spot free for the caller to Reserve once it has really placed something there:
            // a crag turned down at a drop must not block the next candidate 12 m on (Codex review 2026-11-10).
            private bool Place(double s, int side, float offset, float radius, float height, out Vector3 p, bool reserve = true)
            {
                TrackFrame f = line.FrameAt(s);
                p = f.Position + f.Right * side * (definition.HalfWidthAt(s) + offset);
                p.y = GroundAt(p) - GroundSink;
                if (!corridor.Available(p, radius * 1.45f)) return false;
                foreach (Vector3 pole in poles)
                    if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(pole.x, pole.z)) < radius + PoleRadius + 4f
                        && p.y + height > pole.y && p.y < pole.y + 21f) return false;
                if (reserve) corridor.Reserve(p, radius * 1.15f);
                return true;
            }

            // The upper road and the lower zigzag overlap in plan by more than 100 m in height.
            // A heightfield alone must follow the lower surface. Rock overhangs join the upper road's
            // shoulders to the mountain without filling the lower road or a lower pole's airspace.
            private void MountainLedges(Transform parent)
            {
                var tops = new List<Vector3>();
                var topTriangles = new List<int>();
                var stone = new List<Vector3>();
                var stoneTriangles = new List<int>();
                int ledges = 0;
                for (double s = -definition.StartRunway; s < line.EndS; s += 1d)
                {
                    double end = Math.Min(s + 1d, line.EndS);
                    bool gap = false;
                    foreach (var opening in gaps)
                        if (s < opening.End + 0.5d && end > opening.Start - 0.5d) gap = true;
                    if (gap) continue;
                    TrackFrame a = line.FrameAt(s), b = line.FrameAt(end);
                    float aw = definition.HalfWidthAt(Math.Max(0d, Math.Min(s, definition.LapLength)));
                    float bw = definition.HalfWidthAt(Math.Max(0d, Math.Min(end, definition.LapLength)));
                    bool needed = false;
                    for (int side = -1; side <= 1; side++)
                    {
                        needed |= a.Position.y - definition.RoadThickness - GroundAt(a.Position + a.Right * side * (aw - 0.1f)) > 0.4f;
                        needed |= b.Position.y - definition.RoadThickness - GroundAt(b.Position + b.Right * side * (bw - 0.1f)) > 0.4f;
                    }
                    if (!needed) continue;
                    ledges++;
                    float half = Mathf.Max(aw, bw) + 4f;
                    int across = Mathf.CeilToInt(half * 2f);
                    float ay = a.Position.y - definition.RoadThickness - 0.1f, by = b.Position.y - definition.RoadThickness - 0.1f;
                    for (int x = 0; x < across; x++)
                    {
                        float left = -half + half * 2f * x / across, right = -half + half * 2f * (x + 1) / across;
                        Vector3 al = a.Position + a.Right * left, ar = a.Position + a.Right * right;
                        Vector3 bl = b.Position + b.Right * left, br = b.Position + b.Right * right;
                        al.y = ar.y = ay; bl.y = br.y = by;
                        Face(al, bl, ar, br, tops, topTriangles, false);
                        Face(al - Vector3.up * 6f, bl - Vector3.up * 6f, ar - Vector3.up * 6f, br - Vector3.up * 6f, stone, stoneTriangles, true);
                        for (int tier = 0; tier < 3; tier++)
                        {
                            Vector3 drop = Vector3.up * (tier * 2f), next = drop + Vector3.up * 2f;
                            Face(al - drop, ar - drop, al - next, ar - next, stone, stoneTriangles, false);
                            Face(bl - drop, br - drop, bl - next, br - next, stone, stoneTriangles, true);
                            if (x == 0) Face(al - drop, bl - drop, al - next, bl - next, stone, stoneTriangles, true);
                            if (x == across - 1) Face(ar - drop, br - drop, ar - next, br - next, stone, stoneTriangles, false);
                        }
                    }
                }
                if (tops.Count != 0)
                {
                    MeshObject("Mountain ledge shoulders", parent, tops, topTriangles, grass);
                    MeshObject("Mountain ledge strata", parent, stone, stoneTriangles, rockC);
                }
                Debug.Log($"Summit Drop mountain overhangs: {ledges} one-metre road sections supported; jump left open.");

                void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, List<Vector3> v, List<int> t, bool reverse)
                {
                    int first = v.Count;
                    v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                    t.Add(first); t.Add(first + (reverse ? 2 : 1)); t.Add(first + (reverse ? 1 : 2));
                    t.Add(first + 2); t.Add(first + (reverse ? 3 : 1)); t.Add(first + (reverse ? 1 : 3));
                }
            }

            private void Forest(Transform parent)
            {
                int index = 1;
                for (double s = 8d; s < definition.LapLength + 35d; s += 7d)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        if (side < 0 && s < 420d || side < 0 && s >= 800d && s < 1190d || s >= 1840d && s < 1920d) continue;
                        bool dense = s >= 420d && s < 800d;
                        int rows = dense ? 4 : 2;
                        for (int row = 0; row < rows; row++)
                        {
                            int seed = index++;
                            if (!dense && Hash(seed * 13) < (s < 420d ? 0.65f : 0.35f)) continue;
                            double at = s + (Hash(seed * 17) - 0.5f) * 6d;
                            float radius = 2.3f + Hash(seed * 19) * 1.6f;
                            float height = (dense ? 16f : 11f) + Hash(seed * 23) * 9f;
                            if (!Place(at, side, 5f + radius * 1.5f + row * 11f + Hash(seed * 29) * 7f, radius, height, out Vector3 p)) continue;
                            Transform tree = Group("Pine " + seed, parent);
                            tree.SetPositionAndRotation(p, Quaternion.Euler(0f, Hash(seed * 31) * 360f, 0f));
                            Box("Trunk", tree, Vector3.up * height * 0.35f, new Vector3(0.6f, height * 0.7f, 0.6f), wood);
                            Material needles = Pick(seed * 37, pineA, pineB, pineC);
                            for (int tier = 0; tier < 3; tier++)
                                Shape("Needle crown", tree, Vector3.up * height * (0.36f + tier * 0.20f),
                                    new Vector3(radius * 2f * (1f - tier * 0.23f), height * 0.46f, radius * 2f * (1f - tier * 0.23f)), needles, cone);
                            trees++;
                        }
                    }
            }

            private void RockWalls(Transform parent)
            {
                int index = 0;
                for (double s = 12d; s < 1060d; s += 10d)
                {
                    bool ledge = s >= 800d;
                    if (s >= 420d && !ledge && Hash(index + 67) < 0.55f) { index++; continue; }
                    float radius = ledge ? 6f : 5f + Hash(index * 7) * 3f;
                    // A wall crag at a drop is left out (Rock returns false); the wall has more along it.
                    if (Place(s, 1, radius * 1.6f + 3f, radius, 24f, out Vector3 p, false))
                        Rock(parent, p, radius, ledge ? 25f + Hash(index * 11) * 8f : 12f + Hash(index * 11) * 14f, index);
                    index++;
                }
            }

            // Place reads the ground under the centre only, and a crag's flat bottom 10-16 m wide on a mountain slope
            // floated 3-12 m at its downhill side, up to 33 m at a drop (playtest 2026-11-10, docs/ART_REQUEST_PROPS.md
            // K4). Moving or stretching the whole rock buries its uphill side or reaches into the road below, so each crag
            // gets its own copy of the kit mesh whose lowest part follows the slope: a vertex in the bottom CragFoot of the
            // height comes down by how far the bottom floats over the ground under it, fully at the bottom and less going
            // up, so the top keeps its shape and the foot spreads down the slope. Where the ground falls away more than
            // CragReach of the height it is a drop, not a slope: no crag there (false).
            // A placed crag reserves its spot here, not in Place, so a spot turned down at a drop stays free.
            private const float CragFoot = 0.4f, CragReach = 0.6f;

            private bool Rock(Transform parent, Vector3 p, float radius, float height, int seed, bool evenAtADrop = false)
            {
                var size = new Vector3(radius * 2f, height, radius * 1.7f);
                Quaternion tilt = Quaternion.Euler(Hash(seed * 43) * 9f, Hash(seed * 47) * 360f, Hash(seed * 53) * 8f);
                Vector3 centre = p + Vector3.up * height * 0.42f;
                Mesh source = crags[Mathf.Min(crags.Length - 1, (int)(Hash(seed * 59) * crags.Length))];
                Bounds b = source.bounds;
                var scale = new Vector3(size.x / b.size.x, size.y / b.size.y, size.z / b.size.z);
                var vertices = new List<Vector3>();
                source.GetVertices(vertices);
                float reach = 0f;
                for (int i = 0; i < vertices.Count; i++)
                {
                    Vector3 local = Vector3.Scale(vertices[i] - b.center, scale);
                    Vector3 world = centre + tilt * local;
                    float up = (local.y + size.y * 0.5f) / size.y;
                    if (up < CragFoot)
                    {
                        float bottom = world.y - up * size.y;
                        float drop = Mathf.Max(0f, bottom - (GroundAt(world) - GroundSink));
                        reach = Mathf.Max(reach, drop);
                        world.y -= drop * (1f - up / CragFoot);
                    }
                    vertices[i] = world - centre;
                }
                if (reach > height * CragReach && !evenAtADrop) { skippedRocks++; return false; }
                var mesh = new Mesh { name = "SummitCrag_" + seed };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(source.triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                // The tilt and size are in the vertices, so the shape stands at scale 1 and no rotation.
                Shape("Crag " + seed, parent, centre, mesh.bounds.size, Pick(seed * 41, rockA, rockB, rockC), mesh);
                corridor.Reserve(p, radius * 1.15f);
                rocks++;
                lastCragReach = reach;
                longestCragReach = Mathf.Max(longestCragReach, reach);
                if (reach > 0.05f) groundedRocks++;
                return true;
            }

            private void Jump(Transform parent)
            {
                var gap = gaps[0];
                for (int end = 0; end < 2; end++)
                {
                    double s = end == 0 ? gap.Start - 4d : gap.End + 4d;
                    TrackFrame f = line.FrameAt(s);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        if (!Place(s, side, 4.4f, 1.5f, 6f, out Vector3 p)) continue;
                        Transform remnant = Group("Broken bridge abutment", parent);
                        remnant.SetPositionAndRotation(p, Flat(f.Forward));
                        Box("Broken post", remnant, Vector3.up * 2.6f, new Vector3(0.6f, 6f, 0.7f), wood);
                        for (int plank = 0; plank < 4; plank++)
                        {
                            GameObject board = Box("Splintered plank", remnant, new Vector3(0f, 1.8f - plank * 0.45f, (plank - 1) * 0.7f), new Vector3(1.5f, 0.16f, 3f), cutWood);
                            board.transform.localRotation = Quaternion.Euler(15f + plank * 19f, plank * 8f, side * 14f);
                        }
                    }
                }
                TrackFrame mid = line.FrameAt((gap.Start + gap.End) * 0.5d);
                Transform falls = Group("Small ravine waterfall", parent);
                Vector3 at = mid.Position + mid.Right * 32f;
                at.y = GroundAt(at);
                falls.SetPositionAndRotation(at, Flat(mid.Right));
                // All water lies far below the jump. Narrow ribbons and staggered shelves read as a cascade.
                for (int tier = 0; tier < 4; tier++)
                {
                    Box("Water ribbon", falls, new Vector3(0f, 4f + tier * 6f, tier * 2.4f), new Vector3(2.2f, 7f, 0.22f), water);
                    Box("White water", falls, new Vector3(0f, 1f + tier * 6f, tier * 2.4f - 0.3f), new Vector3(3.4f, 0.35f, 2f), foam);
                    Box("Cascade ledge", falls, new Vector3(0f, 0.4f + tier * 6f, tier * 2.4f), new Vector3(5f, 1f, 3f), rockC);
                }
                Box("Ravine pool", falls, new Vector3(0f, 0.1f, -2f), new Vector3(12f, 0.18f, 15f), water);
            }

            private void Fork(Transform parent)
            {
                TrackDefinition.Shortcut shortcut = definition.Shortcuts[0];
                TrackFrame f = line.FrameAt(shortcut.FromS);
                // 1.6 times the first sign so it reads from farther up the road (user 2026-11-09 "1.5배 이상").
                const float k = 1.6f;
                if (!Place(shortcut.FromS - 8d, -1, 5.3f, 1.4f * k, 4f * k, out Vector3 p))
                    throw new InvalidOperationException("The shortcut's wooden direction sign has no clear placement.");
                Transform sign = Group("Shortcut wooden direction sign", parent);
                sign.SetPositionAndRotation(p, Flat(f.Forward));
                // The read face is -Z, toward the runner coming down the road. The post stands behind the board (+Z)
                // and stops below its top edge, so no part of it shows on the arrow face (user 2026-11-09).
                float boardY = 3f * k, boardTop = boardY + 0.5f * k, boardHalfDepth = 0.1f * k, postWidth = 0.3f * k;
                float postTop = boardTop - 0.15f * k;
                Box("Post", sign, new Vector3(0f, (postTop - 0.3f) * 0.5f, boardHalfDepth + postWidth * 0.5f),
                    new Vector3(postWidth, postTop + 0.3f, postWidth), wood);
                Box("Sign board", sign, new Vector3(0f, boardY, 0f), new Vector3(2.8f * k, 1f * k, boardHalfDepth * 2f), cutWood);
                // The head sits on +X: seen from the -Z side that is the runner's right, the side the shortcut leaves on
                // (user 2026-11-09, a render showed the old -X head pointing away from the shortcut).
                float inkZ = -(boardHalfDepth + 0.05f * k), inkWidth = 0.14f * k;
                Vector3 tip = new Vector3(0.9f * k, boardY, inkZ);
                Beam("Arrow", sign, tip, new Vector3(-0.9f * k, boardY, inkZ), inkWidth, ink);
                Beam("Arrow", sign, tip, new Vector3(0.2f * k, boardY + 0.35f * k, inkZ), inkWidth, ink);
                Beam("Arrow", sign, tip, new Vector3(0.2f * k, boardY - 0.35f * k, inkZ), inkWidth, ink);
                for (double s = 1196d; s < 1525d; s += 14d)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        if (!Place(s, side, 4f, 2.8f, 2f, out Vector3 at)) continue;
                        Transform fence = Group("Clearing log fence", parent);
                        fence.SetPositionAndRotation(at, Flat(line.FrameAt(s).Forward));
                        for (int end = -1; end <= 1; end += 2)
                            Box("Fence stake", fence, new Vector3(0f, 0.8f, end * 2.2f), new Vector3(0.3f, 2f, 0.3f), wood);
                        for (int rail = 0; rail < 2; rail++) Beam("Log rail", fence, new Vector3(0f, 0.5f + rail * 0.7f, -2.5f), new Vector3(0f, 0.5f + rail * 0.7f, 2.5f), 0.28f, cutWood);
                    }
            }

            private void Corners(Transform parent)
            {
                double s = 0d;
                int index = 0;
                foreach (TrackDefinition.Segment segment in definition.Segments)
                {
                    double start = s;
                    s += segment.Length;
                    if (start < 1540d || start >= 1840d || !segment.IsArc) continue;
                    double middle = start + segment.Length * 0.5d;
                    int outside = segment.Degrees > 0f ? -1 : 1;
                    TrackFrame f = line.FrameAt(middle);
                    Vector3 p = default;
                    bool placed = false;
                    foreach (float offset in new[] { 7f, 12f, 18f, 25f })
                    {
                        if (!Place(middle, outside, offset, 2.6f, 5f, out p)) continue;
                        placed = true;
                        break;
                    }
                    if (!placed)
                        throw new InvalidOperationException("Zigzag corner board has no clear placement at S" + middle);
                    Transform board = Group("Zigzag chevrons " + index, parent);
                    board.SetPositionAndRotation(p, Flat(-f.Right * outside));
                    for (int leg = -1; leg <= 1; leg += 2) Box("Board leg", board, new Vector3(leg * 1.5f, 1.5f, 0f), new Vector3(0.18f, 3.5f, 0.18f), wood);
                    Box("Board", board, new Vector3(0f, 3.4f, 0f), new Vector3(4f, 1.5f, 0.2f), ink);
                    float direction = segment.Degrees > 0f ? -1f : 1f;
                    for (int arrow = -1; arrow <= 1; arrow++)
                    {
                        Beam("Chevron", board, new Vector3(arrow * 1.1f - direction * 0.3f, 2.9f, 0.14f), new Vector3(arrow * 1.1f + direction * 0.3f, 3.4f, 0.14f), 0.17f, amber);
                        Beam("Chevron", board, new Vector3(arrow * 1.1f + direction * 0.3f, 3.4f, 0.14f), new Vector3(arrow * 1.1f - direction * 0.3f, 3.9f, 0.14f), 0.17f, amber);
                    }
                    bool rockPlaced = false;
                    Vector3? firstClear = null;
                    foreach (float offset in new[] { 20f, 28f, 38f, 50f, 65f, 80f, 100f })
                    {
                        if (!Place(middle, outside, offset, 6f, 20f, out Vector3 r, false)) continue;
                        if (firstClear == null) firstClear = r;
                        if (!Rock(parent, r, 6f, 19f, index + 700)) continue; // at a drop: try further out
                        rockPlaced = true;
                        break;
                    }
                    // A required landmark: where every clear spot is at a drop, the first one takes a crag whose foot
                    // reaches all the way down. Said aloud, since its long foot is checked by vertex only.
                    if (!rockPlaced && firstClear.HasValue)
                    {
                        rockPlaced = Rock(parent, firstClear.Value, 6f, 19f, index + 700, true);
                        Debug.LogWarning($"Zigzag outside crag at S{middle:F0}: every clear spot is at a drop, so its foot reaches {lastCragReach:F1} m down.");
                    }
                    if (!rockPlaced) throw new InvalidOperationException("Zigzag outside crag has no clear placement at S" + middle);
                    index++;
                }
                if (index != 4) throw new InvalidOperationException("Expected four zigzag corner boards, got " + index);
            }

            private void Tunnel(Transform parent)
            {
                const double Start = 1848d, End = 1910d;
                const int Around = 40;
                // An inner and outer shell with two-metre longitudinal faces and sub-two-metre arch faces.
                // The spring line is 10.5 m above the road, higher than the 9 m corridor exemption.
                for (int layer = 0; layer < 2; layer++)
                {
                    var vertices = new List<Vector3>();
                    var triangles = new List<int>();
                    for (double s = Start; s <= End; s += 2d)
                    {
                        TrackFrame f = line.FrameAt(s);
                        float half = definition.HalfWidthAt(s) + 4.5f + layer * 4f;
                        for (int a = 0; a <= Around; a++)
                        {
                            float angle = a * Mathf.PI / Around;
                            float x = Mathf.Cos(angle) * half;
                            float y = 10.5f + Mathf.Sin(angle) * (7f + layer * 4f);
                            vertices.Add(f.TransformPoint(new Vector3(x, y, 0f)));
                        }
                    }
                    int rows = vertices.Count / (Around + 1);
                    for (int row = 0; row < rows - 1; row++)
                        for (int a = 0; a < Around; a++)
                        {
                            int v = row * (Around + 1) + a, next = v + Around + 1;
                            QuadIndices(triangles, v, v + 1, next, next + 1, layer != 0);
                        }
                    MeshObject(layer == 0 ? "Dark tunnel lining" : "Outer rock roof", parent, vertices, triangles, layer == 0 ? ink : rockC);
                }
                for (double s = Start; s < End; s += 2d)
                {
                    TrackFrame f = line.FrameAt(s + 1d);
                    float half = definition.HalfWidthAt(s) + 6.5f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 foot = f.Position + f.Right * side * half;
                        float bottom = GroundAt(foot) - f.Position.y - 1f;
                        int tiers = Mathf.CeilToInt((10.8f - bottom) / 2f);
                        float rise = (10.8f - bottom) / tiers;
                        for (int tier = 0; tier < tiers; tier++)
                        {
                            GameObject wall = Box("Tunnel rock side", parent, foot + Vector3.up * (bottom + rise * (tier + 0.5f)),
                                new Vector3(4f, rise + 0.04f, 2.3f), rockC);
                            wall.transform.localRotation = Flat(f.Forward);
                        }
                    }
                }
                // Both portal rims are made of short blocks; no large unsampled face bridges the driving space.
                foreach (double s in new[] { Start, End })
                {
                    TrackFrame f = line.FrameAt(s);
                    float half = definition.HalfWidthAt(s) + 6.5f;
                    for (int a = 0; a < 32; a++)
                    {
                        float angle = (a + 0.5f) * Mathf.PI / 32f;
                        Vector3 p = f.TransformPoint(new Vector3(Mathf.Cos(angle) * half, 12.4f + Mathf.Sin(angle) * 8.9f, 0f));
                        GameObject block = Box("Portal stone", parent, p, new Vector3(1.7f, 4f, 2.6f), a % 2 == 0 ? rockA : rockB);
                        block.transform.localRotation = Flat(f.Forward) * Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
                    }
                }
            }

            private void Village(Transform parent)
            {
                for (int i = 0; i < 6; i++)
                {
                    double s = 2148d + i * 19d;
                    int side = i % 2 == 0 ? -1 : 1;
                    if (!Place(s, side, 17f + Hash(i + 91) * 7f, 7f, 9f, out Vector3 p)) continue;
                    Transform hut = Group("Valley cabin " + i, parent);
                    hut.SetPositionAndRotation(p, Flat(-line.FrameAt(s).Right * side));
                    Box("Cabin footing", hut, Vector3.up * 0.3f, new Vector3(8.6f, 1f, 7.6f), rockB);
                    for (int log = 0; log < 9; log++) Box("Log walls", hut, Vector3.up * (0.8f + log * 0.45f), new Vector3(8f + log % 2 * 0.3f, 0.43f, 7f), wood);
                    for (int sideRoof = -1; sideRoof <= 1; sideRoof += 2)
                    {
                        GameObject panel = Box("Pitched timber roof", hut, new Vector3(sideRoof * 2.2f, 5.8f, 0f), new Vector3(5.3f, 0.35f, 8.2f), roof);
                        panel.transform.localRotation = Quaternion.Euler(0f, 0f, -sideRoof * 27f);
                    }
                    Box("Door", hut, new Vector3(0f, 1.7f, -3.56f), new Vector3(1.4f, 2.4f, 0.14f), ink);
                    for (int w = -1; w <= 1; w += 2) Box("Shuttered window", hut, new Vector3(w * 2.4f, 2.9f, -3.56f), new Vector3(1.3f, 1.2f, 0.16f), cutWood);
                    Box("Stone chimney", hut, new Vector3(2.3f, 6.1f, 1.8f), new Vector3(0.9f, 3.4f, 1f), rockC);
                }
                TrackFrame finish = line.FrameAt(definition.LapLength);
                Transform gate = Group("Timber finish gate", parent);
                gate.SetPositionAndRotation(finish.Position, Flat(finish.Forward));
                float half = definition.HalfWidthAt(definition.LapLength) + 3.4f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Timber gate post", gate, new Vector3(side * half, 5.5f, 0f), new Vector3(1f, 12.8f, 1f), wood);
                    Box("Flag mast", gate, new Vector3(side * (half + 2.3f), 5.7f, 0f), new Vector3(0.15f, 13f, 0.15f), cutWood);
                    Box("Mountain pennant", gate, new Vector3(side * (half + 2.3f), 11.5f, 1.1f), new Vector3(0.08f, 1.7f, 2.2f), side < 0 ? amber : cream);
                }
                Box("Finish beam", gate, new Vector3(0f, 11.2f, 0f), new Vector3(half * 2f + 1.5f, 1.8f, 1.3f), cutWood);
                // Solid glyphs, as on Palm Coast's arch, with this course's own finish wording.
                Lettering(gate, "SUMMIT DROP", 15f, 11.2f, -0.72f);
            }

            private void Lettering(Transform parent, string words, float width, float y, float z)
            {
                var glyphs = new Dictionary<char, string>
                {
                    { 'S', "111100111001111" }, { 'U', "101101101101111" }, { 'M', "101111111101101" },
                    { 'I', "111010010010111" }, { 'T', "111010010010010" }, { 'D', "110101101101110" },
                    { 'R', "110101110101101" }, { 'O', "111101101101111" }, { 'P', "110101110100100" }, { ' ', "000000000000000" }
                };
                float step = width / words.Length, cell = step / 4f;
                for (int i = 0; i < words.Length; i++)
                    for (int row = 0; row < 5; row++)
                        for (int col = 0; col < 3; col++)
                            if (glyphs[words[i]][row * 3 + col] == '1')
                                Box("Solid letter " + words[i], parent, new Vector3(-width * 0.5f + i * step + (col + 0.5f) * cell, y + (2 - row) * cell * 0.9f, z), new Vector3(cell * 0.85f, cell * 0.8f, 0.12f), ink);
            }

            // A pole's shaft ends SwingLook.PoleDepth below its point. Inside a turn over a cliff the mountain falls further
            // than that, so a rock spire carries the shaft down to the ground (user 2026-11-06 "봉이 공중에 떠있는것이").
            private void PoleSpires(Transform parent)
            {
                const int Sides = 8;
                const float Overlap = 1f, TopRadius = 1.1f, BaseRadius = 5f, Taper = 0.05f, RingEvery = 10f, Rough = 0.2f, ThinnestRoom = 0.3f;
                var points = new List<Vector3>();
                var triangles = new List<int>();
                int spires = 0;
                for (int i = 0; i < poles.Count; i++)
                {
                    Vector3 pole = poles[i];
                    float top = pole.y - SwingLook.PoleDepth + Overlap, bottom = GroundAt(pole) - Overlap;
                    // The shaft reaches the ground, or the ground stands over it.
                    if (top - bottom <= Overlap * 2f) continue;
                    float room = RoomBesideRoads(pole, bottom, top);
                    if (room < ThinnestRoom) throw new InvalidOperationException($"No room for a spire under the pole at {pole}: {room:F2} m.");
                    float wide = Mathf.Min(BaseRadius, TopRadius + (top - bottom) * Taper, room), narrow = Mathf.Min(TopRadius, room);
                    int rings = Mathf.Max(2, Mathf.CeilToInt((top - bottom) / RingEvery) + 1);
                    int first = points.Count;
                    for (int ring = 0; ring < rings; ring++)
                    {
                        float along = ring / (rings - 1f);
                        for (int side = 0; side < Sides; side++)
                        {
                            float angle = side * Mathf.PI * 2f / Sides;
                            float radius = Mathf.Lerp(wide, narrow, along) * (1f - Hash(i * 97 + ring * 13 + side) * Rough);
                            points.Add(new Vector3(pole.x + Mathf.Cos(angle) * radius, Mathf.Lerp(bottom, top, along), pole.z + Mathf.Sin(angle) * radius));
                        }
                    }
                    for (int ring = 0; ring + 1 < rings; ring++)
                        for (int side = 0; side < Sides; side++)
                        {
                            int a = first + ring * Sides + side, b = first + ring * Sides + (side + 1) % Sides;
                            QuadIndices(triangles, a, b, a + Sides, b + Sides, false);
                        }
                    int cap = points.Count, last = first + (rings - 1) * Sides;
                    points.Add(new Vector3(pole.x, top, pole.z));
                    for (int side = 0; side < Sides; side++)
                    {
                        triangles.Add(cap);
                        triangles.Add(last + (side + 1) % Sides);
                        triangles.Add(last + side);
                    }
                    spires++;
                }
                if (spires == 0) throw new InvalidOperationException("Summit Drop layout changed: no pole hangs over the ground, so 10 has nothing to hold.");
                MeshObject("Pole rock spires", parent, points, triangles, rockC);
                Debug.Log($"Summit Drop raised {spires} rock spires under corner poles.", parent);
            }

            // How far out from pole a spire may reach and stay clear of any road at its heights (RoadCorridor's edge margin).
            private float RoomBesideRoads(Vector3 pole, float bottom, float top)
            {
                const float EdgeMargin = 2f, Reach = 2f;
                float room = float.PositiveInfinity;
                for (int i = 0; i < samples.Count; i++)
                {
                    Vector3 road = samples[i].Position;
                    if (road.y < bottom - Reach || road.y > top + Reach) continue;
                    room = Mathf.Min(room, new Vector2(road.x - pole.x, road.z - pole.z).magnitude - widths[i] - EdgeMargin);
                }
                return room;
            }

            private void ValidatePoles(GameObject root)
            {
                int checks = 0;
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    Vector3[] v = filter.sharedMesh.vertices;
                    int[] t = filter.sharedMesh.triangles;
                    Matrix4x4 world = filter.transform.localToWorldMatrix;
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        Vector3 a = world.MultiplyPoint3x4(v[t[i]]), b = world.MultiplyPoint3x4(v[t[i + 1]]), c = world.MultiplyPoint3x4(v[t[i + 2]]);
                        foreach (Vector3 pole in poles)
                        {
                            if (Mathf.Max(a.y, b.y, c.y) < pole.y - 0.05f || Mathf.Min(a.y, b.y, c.y) > pole.y + 21f) continue;
                            if (pole.x < Mathf.Min(a.x, b.x, c.x) - PoleRadius || pole.x > Mathf.Max(a.x, b.x, c.x) + PoleRadius
                                || pole.z < Mathf.Min(a.z, b.z, c.z) - PoleRadius || pole.z > Mathf.Max(a.z, b.z, c.z) + PoleRadius) continue;
                            int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max((b - a).magnitude, (c - a).magnitude, (c - b).magnitude)));
                            for (int u = 0; u <= n; u++)
                                for (int w = 0; w <= n - u; w++)
                                {
                                    Vector3 p = a + (b - a) * ((float)u / n) + (c - a) * ((float)w / n);
                                    checks++;
                                    if (p.y < pole.y - 0.05f || p.y > pole.y + 21f) continue;
                                    if ((new Vector2(p.x - pole.x, p.z - pole.z)).sqrMagnitude < PoleRadius * PoleRadius)
                                        throw new InvalidOperationException($"{filter.name} obscures a pole shaft at {pole}, surface at {p}.");
                                }
                        }
                    }
                }
                Debug.Log($"Summit Drop pole clearance PASS: {poles.Count} poles, 3 m shaft envelope, {checks} triangle probes.", root);
            }

            private static void QuadIndices(List<int> triangles, int a, int b, int c, int d, bool reverse)
            {
                triangles.Add(a); triangles.Add(reverse ? b : c); triangles.Add(reverse ? c : b);
                triangles.Add(b); triangles.Add(reverse ? d : c); triangles.Add(reverse ? c : d);
            }

            private static GameObject MeshObject(string name, Transform parent, List<Vector3> vertices, List<int> triangles, Material material)
            {
                var mesh = new Mesh { name = "SummitSource_" + name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var part = new GameObject(name);
                part.transform.SetParent(parent, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                return part;
            }

            private static Mesh ConeMesh()
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                for (int i = 0; i < 9; i++)
                {
                    float a = i * Mathf.PI * 2f / 9f, b = (i + 1) * Mathf.PI * 2f / 9f;
                    int first = vertices.Count;
                    vertices.Add(new Vector3(Mathf.Cos(a), -0.5f, Mathf.Sin(a)));
                    vertices.Add(Vector3.up * 0.5f);
                    vertices.Add(new Vector3(Mathf.Cos(b), -0.5f, Mathf.Sin(b)));
                    triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                }
                var mesh = new Mesh { name = "SummitPineSource" };
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                return mesh;
            }

            // Same material merge and persistent mesh naming as SkywayEnvironmentBuilder.
            private static void MergeByMaterial(Transform group, string key)
            {
                var byMaterial = new Dictionary<Material, List<CombineInstance>>();
                foreach (MeshFilter filter in group.GetComponentsInChildren<MeshFilter>(true))
                {
                    Material material = filter.GetComponent<MeshRenderer>().sharedMaterial;
                    if (!byMaterial.TryGetValue(material, out List<CombineInstance> parts)) byMaterial[material] = parts = new List<CombineInstance>();
                    parts.Add(new CombineInstance { mesh = filter.sharedMesh, transform = group.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                }
                for (int i = group.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(group.GetChild(i).gameObject);
                foreach (var pair in byMaterial)
                {
                    var mesh = new Mesh { name = $"SummitDropExterior_{key}_{pair.Key.name}", indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                    mesh.RecalculateBounds();
                    var part = new GameObject(key + " " + pair.Key.name);
                    part.transform.SetParent(group, false);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer renderer = part.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = pair.Key;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    GameObjectUtility.SetStaticEditorFlags(part, StaticEditorFlags.BatchingStatic);
                }
            }
        }
    }
}
