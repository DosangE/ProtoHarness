using System;
using System.Collections.Generic;
using System.IO;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProtoHarness.Editor.ChainRush
{
    // Like FingerLanesEnvironmentBuilder: persistent decoration, no road, physics or runtime behaviour, plus Palm Coast's
    // own road and wall materials as a TrackLook (user 2026-10-30, "카트라이더 비치 해변 드라이브 참고"). The scenes follow
    // the record video's lap in order, by share of the lap: a pastel city start, the climb beside the sea with yachts, the
    // palm beach road, a red steel bridge, a pink stone town, the harbour corners, the umbrella beach, the bridge again,
    // the stair corners and the city straight home. Only the mood is taken: no original names, signs or assets. It builds
    // and saves the prefab, the materials and the look; saving the decoration scene, the build's scene list and the lobby
    // entry are left to the user (as for Finger Lanes).
    public static class PalmCoastEnvironmentBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/PalmCoastExterior.prefab";
        public const string ScenePath = "Assets/_Project/Scenes/ChainRushTrackPalmCoast.unity";
        public const string LookPath = "Assets/_Project/Data/TrackLook_PalmCoast.asset";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/";
        // The land's top, below the lowest road (the finish hill's -2 m), and the sea's surface.
        private const float Ground = -2.6f;
        private const float Sea = -4.2f;
        // Where things stand: 0.3 m into the ground, below every kind of land (each lies a little lower, LandDrop), so
        // nothing floats over the lowest.
        private const float Footing = Ground - 0.3f;

        [MenuItem("ProtoHarness/Chain Rush/Create Palm Coast Exterior")]
        public static void CreateExterior()
        {
            RequireIdle();
            if (File.Exists(PrefabPath) || File.Exists(ScenePath))
                throw new InvalidOperationException("Palm Coast exterior already exists; use Rebuild Palm Coast Exterior.");
            BuildLook();
            BuildPrefab();
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            scene.name = "ChainRushTrackPalmCoast";
            GameObject prefab = Require<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Create Palm Coast exterior");
            EditorSceneManager.MarkSceneDirty(scene);
            if (original.IsValid()) SceneManager.SetActiveScene(original);
            Selection.activeGameObject = instance;
            Debug.Log($"Palm Coast exterior created. Save the decoration scene as {ScenePath}, register it in Build Profiles, and give the lobby's PALM COAST entry the scene name and {LookPath}.", instance);
        }

        [MenuItem("ProtoHarness/Chain Rush/Rebuild Palm Coast Exterior")]
        public static void RebuildExterior()
        {
            RequireIdle();
            if (!File.Exists(PrefabPath)) throw new InvalidOperationException("Create Palm Coast Exterior first.");
            BuildLook();
            BuildPrefab();
        }

        private static void RequireIdle()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Exit Play mode and wait for compilation first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scenes first.");
        }

        // Warm dark asphalt (the reference's road), lavender walls (its low walls by the sea and through the town) and
        // white lane markings (CircuitScenery.PaintMarkings).
        private static void BuildLook()
        {
            Material road = Palette("Road", new Color(0.30f, 0.29f, 0.29f));
            Material wall = Palette("Wall", new Color(0.66f, 0.60f, 0.82f));
            Material markings = Palette("Marking", new Color(0.93f, 0.93f, 0.90f), 0.05f);
            TrackLook look = AssetDatabase.LoadAssetAtPath<TrackLook>(LookPath);
            if (look == null)
            {
                look = ScriptableObject.CreateInstance<TrackLook>();
                AssetDatabase.CreateAsset(look, LookPath);
            }
            var data = new SerializedObject(look);
            data.FindProperty("road").objectReferenceValue = road;
            data.FindProperty("rail").objectReferenceValue = wall;
            data.FindProperty("markings").objectReferenceValue = markings;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (!look.TryValidate(out string error)) throw new InvalidOperationException(error);
            EditorUtility.SetDirty(look);
            AssetDatabase.SaveAssets();
        }

        private static void BuildPrefab()
        {
            TrackDefinition definition = Require<TrackDefinition>(ChainRushCircuitSceneBuilder.PalmCoastPath);
            if (!definition.TryValidate(out string error)) throw new InvalidOperationException(error);
            string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var layout = new Layout(definition);
                GameObject root = layout.Build(preview);
                FingerLanesEnvironmentBuilder.Validate(root);
                layout.ValidateClearance(root);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success || saved == null) throw new IOException("Could not save Palm Coast exterior prefab.");
                if (guid.Length != 0 && AssetDatabase.AssetPathToGUID(PrefabPath) != guid)
                    throw new InvalidOperationException("Rebuilding changed the exterior prefab GUID.");
                AssetDatabase.SaveAssets();
                Debug.Log($"Palm Coast exterior: {saved.GetComponentsInChildren<Renderer>(true).Length} renderers; physics=0; runtime scripts=0; road corridor clear.", saved);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static T Require<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Required asset is missing: " + path);
            return asset;
        }

        // The builder owns these materials: a rebuild makes the missing ones and sets every one's colour and glow again,
        // keeping its GUID. No glow unless asked: the first build's glow on everything made the town look fluorescent
        // (user 2026-10-30, "형광색 건물들도 별로다").
        private static Material Palette(string name, Color color, float glow = 0f)
        {
            string path = MaterialFolder + "M_Palm" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Require<Material>(MaterialFolder + "M_StadiumSportsWhite.mat").shader;
                material = new Material(shader) { name = "M_Palm" + name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.16f);
            material.SetFloat("_Metallic", 0f);
            if (glow > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * glow);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private enum Scenery { City, Climb, Beach, Bridge, Town, Harbour, Umbrellas, Stairs }

        private sealed class Layout
        {
            private const float PalmCrown = 5.2f;
            // How far the land reaches beyond the road's edge (the city's second row of towers stands on it), and how much
            // lower each kind of ground lies than the one before.
            private const float LandReach = 52f;
            private const float LandDrop = 0.08f;
            private readonly TrackDefinition definition;
            private readonly Centerline line;
            private readonly double lap;
            private readonly List<(double Start, double End)> gaps;
            private readonly List<Vector3> samples = new List<Vector3>();
            private readonly List<float> widths = new List<float>();
            private readonly List<(Vector3 Position, float Radius)> occupied = new List<(Vector3, float)>();
            private readonly Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            private readonly Mesh sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            private readonly Mesh cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            // Soft, sun-faded colours (user 2026-10-30): no glow except the sea's sparkle and the glass.
            private readonly Material sand = Palette("Sand", new Color(0.90f, 0.82f, 0.64f));
            private readonly Material sea = Palette("Sea", new Color(0.20f, 0.58f, 0.68f), 0.08f);
            private readonly Material grass = Palette("Grass", new Color(0.45f, 0.62f, 0.36f));
            private readonly Material paving = Palette("Paving", new Color(0.74f, 0.74f, 0.72f));
            private readonly Material trunk = Palette("Trunk", new Color(0.50f, 0.37f, 0.25f));
            private readonly Material leaf = Palette("Leaf", new Color(0.27f, 0.52f, 0.30f));
            private readonly Material red = Palette("BridgeRed", new Color(0.74f, 0.22f, 0.18f));
            private readonly Material cyan = Palette("Cyan", new Color(0.62f, 0.78f, 0.82f));
            private readonly Material white = Palette("White", new Color(0.94f, 0.93f, 0.90f));
            private readonly Material pink = Palette("Pink", new Color(0.88f, 0.70f, 0.68f));
            private readonly Material terracotta = Palette("Terracotta", new Color(0.74f, 0.47f, 0.40f));
            private readonly Material violet = Palette("Violet", new Color(0.48f, 0.40f, 0.68f));
            private readonly Material glass = Palette("Glass", new Color(0.36f, 0.50f, 0.60f), 0.06f);
            private readonly Material yellow = Palette("Yellow", new Color(0.92f, 0.78f, 0.30f));
            private readonly Material cream = Palette("Cream", new Color(0.92f, 0.88f, 0.78f));
            private readonly Material stone = Palette("Stone", new Color(0.80f, 0.78f, 0.74f));
            private readonly Material apricot = Palette("Apricot", new Color(0.93f, 0.76f, 0.58f));
            private readonly Material mint = Palette("Mint", new Color(0.68f, 0.82f, 0.74f));
            private readonly Material concrete = Palette("Concrete", new Color(0.62f, 0.62f, 0.60f));
            private readonly Material metal = Palette("Metal", new Color(0.52f, 0.54f, 0.56f));
            private readonly Material wood = Palette("Wood", new Color(0.55f, 0.40f, 0.28f));
            private readonly Material tire = Palette("Tire", new Color(0.12f, 0.12f, 0.13f));
            private readonly Material dark = Require<Material>(MaterialFolder + "M_ArtStadiumInk.mat");

            internal Layout(TrackDefinition definition)
            {
                this.definition = definition;
                line = definition.BuildCenterline(Vector3.zero, 0f);
                lap = definition.LapLength;
                gaps = definition.Gaps();
                if (cube == null || sphere == null || cylinder == null) throw new InvalidOperationException("Missing built-in decoration meshes.");
                if (gaps.Count != 1) throw new InvalidOperationException("Palm Coast layout changed: one sea gap expected; review the sea and the yachts.");
                for (double s = 0d; s <= lap; s += 0.5d)
                {
                    samples.Add(line.FrameAt(s).Position);
                    widths.Add(definition.HalfWidthAt(s));
                }
            }

            // The record video's lap (7.0 .. 60.0 s, DECISIONS "PALM COAST") read as shares of the lap.
            private Scenery SceneryAt(double s)
            {
                double f = s / lap;
                return f < 0.11d ? Scenery.City : f < 0.23d ? Scenery.Climb : f < 0.34d ? Scenery.Beach : f < 0.40d ? Scenery.Bridge
                    : f < 0.53d ? Scenery.Town : f < 0.60d ? Scenery.Harbour : f < 0.68d ? Scenery.Umbrellas : f < 0.75d ? Scenery.Bridge
                    : f < 0.87d ? Scenery.Stairs : Scenery.City;
            }

            internal GameObject Build(Scene scene)
            {
                var root = new GameObject("Palm Coast Exterior");
                SceneManager.MoveGameObjectToScene(root, scene);
                Land(Group("01 Sea and land below road", root.transform));
                StartArch(Group("02 Start arch", root.transform));
                Bridges(Group("03 Red steel bridges", root.transform));
                Yachts(Group("04 Yachts", root.transform));
                Buildings(Group("05 City towers and town houses", root.transform));
                // Street life before the umbrellas and palms, and umbrellas before palms: each keeps its places, the palms
                // fill what is left.
                StreetLife(Group("06 Street life", root.transform));
                Umbrellas(Group("07 Beach umbrellas", root.transform));
                Palms(Group("08 Palms", root.transform));
                CornerBoards(Group("09 Corner chevrons", root.transform));
                return root;
            }

            private void Land(Transform parent)
            {
                Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                foreach (Vector3 p in samples)
                {
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                Vector3 middle = (min + max) * 0.5f;
                Box("Sea", parent, new Vector3(middle.x, Sea, middle.z), new Vector3(max.x - min.x + 900f, 0.2f, max.z - min.z + 900f), sea);
                // A strip of land under the road, left out over the sea gap so the jump flies over open water and under the
                // bridges so they cross it. The strips overlap to close the curves; each kind of ground sits a little lower
                // than the one before (LandDrop), so two kinds overlapping never share a plane and flicker (user 2026-10-30).
                for (double s = 0d; s < lap; s += 8d)
                {
                    Scenery here = SceneryAt(s);
                    if (NearGap(s, 14d) || here == Scenery.Bridge) continue;
                    TrackFrame frame = line.FrameAt(s);
                    (Material top, int layer) = here == Scenery.Beach || here == Scenery.Umbrellas || here == Scenery.Climb ? (sand, 0)
                        : here == Scenery.Town || here == Scenery.Stairs ? (terracotta, 1)
                        : here == Scenery.City || here == Scenery.Harbour ? (paving, 2) : (grass, 3);
                    GameObject strip = Box("Land under road", parent, new Vector3(frame.Position.x, Ground - 0.5f - layer * LandDrop, frame.Position.z),
                        new Vector3((definition.HalfWidthAt(s) + LandReach) * 2f, 1f, 11f), top);
                    strip.transform.localRotation = Flat(frame.Forward);
                }
            }

            private bool NearGap(double s, double margin)
            {
                foreach ((double start, double end) in gaps)
                    if (s > start - margin && s < end + margin) return true;
                return false;
            }

            private void StartArch(Transform parent)
            {
                TrackFrame frame = line.FrameAt(0d);
                Transform arch = Group("Checkered start arch", parent);
                arch.SetPositionAndRotation(new Vector3(frame.Position.x, frame.Position.y, frame.Position.z), Flat(frame.Forward));
                float half = definition.HalfWidthAt(0d) + 1.8f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Arch post", arch, new Vector3(side * half, 5.4f, 0f), new Vector3(1.4f, 11.6f, 1.4f), white);
                    for (int band = 0; band < 6; band++)
                        Box("Arch post stripe", arch, new Vector3(side * half, 1f + band * 1.9f, 0f), new Vector3(1.5f, 0.6f, 1.5f), band % 2 == 0 ? violet : cyan);
                }
                Box("Arch beam", arch, new Vector3(0f, 11.4f, 0f), new Vector3(half * 2f + 1.4f, 2.6f, 1.2f), white);
                int squares = Mathf.CeilToInt(half * 2f / 1.2f);
                for (int i = 0; i < squares; i++)
                    for (int row = 0; row < 2; row++)
                        Box("Checker", arch, new Vector3(-half + 0.6f + i * 1.2f, 10.75f + row * 1.3f, 0.66f), new Vector3(1.2f, 1.3f, 0.1f), (i + row) % 2 == 0 ? dark : white);
                // The name faces the runners coming to the line (and on the grid), the checkers the ones leaving it.
                Lettering(arch, "PALM COAST", 14f, -0.68f);
                occupied.Add((arch.position, half + 2f));
            }

            private void Bridges(Transform parent)
            {
                int index = 0;
                for (double s = 4d; s < lap; s += 12d)
                {
                    if (SceneryAt(s) != Scenery.Bridge) continue;
                    TrackFrame frame = line.FrameAt(s);
                    // Below the runner only upright posts, 3 m out: the clearance check measures world-aligned bounds, and a
                    // diagonal brace on a diagonal stretch of road swells them in toward the road. The truss is overhead.
                    float half = definition.HalfWidthAt(s) + 3f;
                    Transform portal = Group("Bridge portal " + index++, parent);
                    portal.SetPositionAndRotation(frame.Position, Flat(frame.Forward));
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Box("Truss post", portal, new Vector3(side * half, 5.2f, 0f), new Vector3(0.7f, 11.4f, 0.7f), red);
                        // The bridges cross open water (no land under them): a pier from the post down into the sea.
                        float drop = frame.Position.y - Sea;
                        Box("Bridge pier", portal, new Vector3(side * half, -drop * 0.5f - 0.5f, 0f), new Vector3(1.4f, drop, 1.4f), white);
                        Box("Truss lower chord", portal, new Vector3(side * half, 9.6f, 0f), new Vector3(0.5f, 0.4f, 12f), red);
                        Box("Truss upper chord", portal, new Vector3(side * half, 10.7f, 0f), new Vector3(0.6f, 0.6f, 12f), red);
                        Beam("Truss brace", portal, new Vector3(side * half, 9.6f, -6f), new Vector3(side * half, 10.7f, 0f), 0.3f, red);
                        Beam("Truss brace", portal, new Vector3(side * half, 10.7f, 0f), new Vector3(side * half, 9.6f, 6f), 0.3f, red);
                    }
                    Box("Truss top beam", portal, new Vector3(0f, 10.7f, 0f), new Vector3(half * 2f + 0.7f, 0.6f, 0.6f), red);
                    // Overhead X bracing to the next portal, so the bridge reads as one steel truss (all above 9 m).
                    Beam("Truss roof brace", portal, new Vector3(-half, 10.7f, 0f), new Vector3(half, 10.7f, 12f), 0.3f, red);
                    Beam("Truss roof brace", portal, new Vector3(half, 10.7f, 0f), new Vector3(-half, 10.7f, 12f), 0.3f, red);
                }
            }

            // Yachts out on the water beside the jump, both sides of the climb, clear of the road.
            private void Yachts(Transform parent)
            {
                double gapMiddle = (gaps[0].Start + gaps[0].End) * 0.5d;
                int index = 0;
                for (int k = -2; k <= 2; k++)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        TrackFrame frame = line.FrameAt(gapMiddle + k * 24d);
                        // Beyond the land under the road (its half width is the road's plus LandReach).
                        Vector3 position = frame.Position + frame.Right * side * (definition.HalfWidthAt(gapMiddle) + LandReach + 12f + (k + 2) % 3 * 14f);
                        position.y = Sea;
                        if (!Available(position, 8f)) continue;
                        Transform yacht = Group("Yacht " + index++, parent);
                        yacht.SetPositionAndRotation(position, Quaternion.Euler(0f, 30f * k + (side < 0 ? 0f : 180f), 0f));
                        Box("Hull", yacht, new Vector3(0f, 0.9f, 0f), new Vector3(3.6f, 1.8f, 12f), white);
                        Box("Hull stripe", yacht, new Vector3(0f, 0.7f, 0f), new Vector3(3.7f, 0.4f, 11.6f), cyan);
                        Box("Cabin", yacht, new Vector3(0f, 2.4f, -1f), new Vector3(2.6f, 1.4f, 5f), white);
                        Box("Cabin glass", yacht, new Vector3(0f, 2.5f, 1.55f), new Vector3(2.4f, 0.8f, 0.1f), glass);
                        Box("Mast", yacht, new Vector3(0f, 7f, 0.5f), new Vector3(0.25f, 9.5f, 0.25f), white);
                        Beam("Sail", yacht, new Vector3(0f, 3.4f, 1f), new Vector3(0f, 11f, 4.5f), 0.12f, white);
                        occupied.Add((position, 8f));
                    }
            }

            private void Buildings(Transform parent)
            {
                int index = 0;
                for (double s = 10d; s < lap; s += 24d)
                {
                    Scenery here = SceneryAt(s);
                    bool city = here == Scenery.City || here == Scenery.Harbour;
                    bool town = here == Scenery.Town || here == Scenery.Stairs;
                    if (!city && !town) continue;
                    TrackFrame frame = line.FrameAt(s);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float width = city ? 12f + index % 3 * 2f : 9f + index % 2 * 2f;
                        float depth = city ? 12f : 9f;
                        float radius = new Vector2(width, depth).magnitude * 0.5f + 1f;
                        Vector3 position = frame.Position + frame.Right * side * (definition.HalfWidthAt(s) + 6f + radius);
                        position.y = Footing;
                        Quaternion facing = Flat(-frame.Right * side);
                        if (Available(position, radius))
                        {
                            if (city) Tower(parent, position, facing, width, depth, 20f + index % 4 * 7f, index);
                            else TownHouse(parent, position, facing, width, depth, 8f + index % 3 * 2.5f, index);
                            occupied.Add((position, radius));
                        }
                        // The city's skyline: a taller tower behind, half a step along, as dense as the reference's high-rises.
                        if (city)
                        {
                            TrackFrame behind = line.FrameAt(s + 12d);
                            Vector3 back = behind.Position + behind.Right * side * (definition.HalfWidthAt(s) + 6f + radius * 3f + 4f);
                            back.y = Footing;
                            if (Available(back, radius))
                            {
                                Tower(parent, back, Flat(-behind.Right * side), width + 2f, depth + 2f, 34f + index % 3 * 9f, index + 1000);
                                occupied.Add((back, radius));
                            }
                        }
                        index++;
                    }
                }
            }

            // A fixed scatter in 0..1 from a seed, so every building differs and a rebuild gives the same street.
            private static float Hash(int seed) => Mathf.Repeat(Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f, 1f);

            private Material Pick(int seed, params Material[] choices) => choices[Mathf.Min(choices.Length - 1, (int)(Hash(seed) * choices.Length))];

            // A city tower of one to three tiers, each narrower than the one below, with window bands or columns, a
            // cornice per tier, a shop front with an awning and a blank sign, sometimes balconies, and roof clutter (a
            // water tank, an antenna, plant boxes). Everything varies by index (user 2026-10-30: the same boxes repeated
            // read as a ghost town).
            private void Tower(Transform parent, Vector3 position, Quaternion facing, float width, float depth, float height, int index)
            {
                Transform tower = Group("City tower " + index, parent);
                tower.SetPositionAndRotation(position, facing);
                Material facade = Pick(index * 3 + 1, cream, stone, cyan, pink, apricot, mint, white);
                Material trim = Pick(index * 5 + 2, white, concrete, stone);
                int tiers = 1 + (int)(Hash(index * 7 + 3) * 3f);
                bool columns = Hash(index * 11 + 4) > 0.5f;
                bool balconies = !columns && Hash(index * 13 + 5) > 0.45f;
                float y = 0f, w = width, d = depth, tierHeight = height / tiers;
                for (int t = 0; t < tiers; t++)
                {
                    Box("Tier", tower, new Vector3(0f, y + tierHeight * 0.5f, 0f), new Vector3(w, tierHeight, d), facade);
                    Box("Tier cornice", tower, new Vector3(0f, y + tierHeight, 0f), new Vector3(w + 0.4f, 0.4f, d + 0.4f), trim);
                    float front = d * 0.5f + 0.06f;
                    float bottom = t == 0 ? 4.6f : y + 1f;
                    if (columns)
                    {
                        int count = Mathf.Max(2, (int)(w * 0.8f / 2.4f));
                        for (int c = 0; c < count; c++)
                        {
                            float x = -w * 0.4f + (c + 0.5f) * w * 0.8f / count;
                            float tall = y + tierHeight - 1f - bottom;
                            if (tall > 1f) Box("Window column", tower, new Vector3(x, bottom + tall * 0.5f, front), new Vector3(1.2f, tall, 0.12f), glass);
                        }
                    }
                    else
                    {
                        for (float b = bottom + 0.8f; b < y + tierHeight - 1f; b += 3.2f)
                        {
                            Box("Window band", tower, new Vector3(0f, b, front), new Vector3(w * 0.84f, 1.4f, 0.12f), glass);
                            if (balconies && (int)(b / 3.2f) % 2 == 0)
                                Box("Balcony", tower, new Vector3(0f, b - 0.9f, front + 0.6f), new Vector3(w * 0.6f, 0.2f, 1.2f), trim);
                        }
                    }
                    y += tierHeight;
                    w *= 0.78f;
                    d *= 0.8f;
                }
                // The shop front: dark glass, a door, a coloured awning and a sign board without lettering.
                float shopFront = depth * 0.5f + 0.08f;
                Box("Shop glass", tower, new Vector3(0f, 1.9f, shopFront), new Vector3(width * 0.8f, 2.8f, 0.12f), dark);
                Box("Shop door", tower, new Vector3(width * 0.25f, 1.4f, shopFront + 0.06f), new Vector3(1.4f, 2.6f, 0.1f), trim);
                Box("Shop awning", tower, new Vector3(0f, 3.7f, depth * 0.5f + 1f), new Vector3(width * 0.9f, 0.25f, 2f), Pick(index * 17 + 6, violet, terracotta, mint, apricot, cyan));
                Box("Shop sign", tower, new Vector3(0f, 4.4f, shopFront + 0.05f), new Vector3(width * 0.5f, 0.8f, 0.15f), Pick(index * 19 + 7, white, yellow, cyan, pink));
                // Roof clutter on the top tier.
                float roof = y;
                float r = Hash(index * 23 + 8);
                if (r < 0.4f)
                {
                    Shape("Water tank", tower, new Vector3(w * 0.2f, roof + 2.2f, -d * 0.1f), new Vector3(2.4f, 2.4f, 2.4f), metal, cylinder);
                    for (int leg = -1; leg <= 1; leg += 2)
                        Box("Tank leg", tower, new Vector3(w * 0.2f + leg * 0.8f, roof + 0.5f, -d * 0.1f), new Vector3(0.2f, 1f, 0.2f), metal);
                }
                else if (r < 0.7f)
                {
                    Box("Antenna", tower, new Vector3(-w * 0.2f, roof + 3f, 0f), new Vector3(0.18f, 6f, 0.18f), metal);
                    Box("Roof plant room", tower, new Vector3(w * 0.15f, roof + 1.2f, 0f), new Vector3(w * 0.35f, 2.4f, d * 0.4f), trim);
                }
                else
                {
                    for (int box = 0; box < 3; box++)
                        Box("Roof unit", tower, new Vector3(-w * 0.3f + box * w * 0.3f, roof + 0.6f, d * 0.15f), new Vector3(1.4f, 1.2f, 1.4f), concrete);
                }
                Box("Roof railing", tower, new Vector3(0f, roof + 0.6f, d * 0.5f - 0.1f), new Vector3(w, 0.15f, 0.12f), metal);
            }

            // A two- or three-storey house of the pink town: pitched tiled or flat roof by turns, shuttered windows with
            // flower boxes, a door, and on some an upper balcony or a striped awning.
            private void TownHouse(Transform parent, Vector3 position, Quaternion facing, float width, float depth, float height, int index)
            {
                Transform house = Group("Town house " + index, parent);
                house.SetPositionAndRotation(position, facing);
                Material walls = Pick(index * 3 + 11, pink, apricot, cream, white, mint);
                Material shutters = Pick(index * 5 + 12, cyan, mint, white, terracotta);
                Box("Plaster walls", house, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), walls);
                Box("Plinth", house, new Vector3(0f, 0.4f, 0f), new Vector3(width + 0.2f, 0.8f, depth + 0.2f), stone);
                if (Hash(index * 7 + 13) > 0.45f)
                {
                    float rise = width * 0.22f;
                    float slope = Mathf.Atan2(rise, width * 0.5f) * Mathf.Rad2Deg;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        GameObject panel = Box("Tiled roof", house, new Vector3(side * width * 0.25f, height + rise * 0.5f, 0f),
                            new Vector3(new Vector2(width * 0.5f, rise).magnitude + 0.4f, 0.3f, depth + 0.6f), terracotta);
                        panel.transform.localRotation = Quaternion.Euler(0f, 0f, -side * slope);
                    }
                    for (int tier = 0; tier < 4; tier++)
                        Box("Gable", house, new Vector3(0f, height + rise * (tier + 0.5f) / 4f, 0f), new Vector3(width * (1f - (tier + 0.5f) / 4f) * 0.95f, rise / 4f, depth * 0.96f), walls);
                }
                else
                {
                    Box("Flat roof", house, new Vector3(0f, height + 0.2f, 0f), new Vector3(width + 0.3f, 0.4f, depth + 0.3f), stone);
                    Box("Parapet", house, new Vector3(0f, height + 0.7f, depth * 0.5f), new Vector3(width + 0.3f, 0.6f, 0.25f), walls);
                }
                int storeys = height > 10f ? 3 : 2;
                float front = depth * 0.5f + 0.06f;
                for (int floor = 1; floor < storeys; floor++)
                    for (int col = -1; col <= 1; col++)
                    {
                        float y = floor * height / storeys + 1.1f;
                        Box("Window", house, new Vector3(col * width * 0.3f, y, front), new Vector3(1.2f, 1.6f, 0.12f), glass);
                        for (int side = -1; side <= 1; side += 2)
                            Box("Shutter", house, new Vector3(col * width * 0.3f + side * 0.85f, y, front + 0.04f), new Vector3(0.45f, 1.7f, 0.08f), shutters);
                        Box("Flower box", house, new Vector3(col * width * 0.3f, y - 1f, front + 0.25f), new Vector3(1.3f, 0.3f, 0.4f), wood);
                        Box("Flowers", house, new Vector3(col * width * 0.3f, y - 0.75f, front + 0.25f), new Vector3(1.2f, 0.25f, 0.3f), Pick(index * 31 + col + 14, pink, leaf, yellow, violet));
                    }
                Box("Door", house, new Vector3(-width * 0.22f, 1.3f, front), new Vector3(1.3f, 2.4f, 0.12f), Pick(index * 37 + 15, wood, cyan, terracotta));
                Box("Ground window", house, new Vector3(width * 0.2f, 1.6f, front), new Vector3(2.2f, 1.4f, 0.12f), glass);
                float extra = Hash(index * 41 + 16);
                if (extra < 0.4f)
                {
                    Box("Balcony slab", house, new Vector3(0f, height / storeys + 0.2f, front + 0.6f), new Vector3(width * 0.5f, 0.2f, 1.2f), stone);
                    Box("Balcony rail", house, new Vector3(0f, height / storeys + 0.75f, front + 1.15f), new Vector3(width * 0.5f, 0.9f, 0.08f), white);
                }
                else if (extra < 0.75f)
                {
                    Material stripe = Pick(index * 43 + 17, violet, terracotta, cyan, mint);
                    for (int s = 0; s < 5; s++)
                        Box("Striped awning", house, new Vector3((s - 2) * width * 0.17f, 3.1f, depth * 0.5f + 1f), new Vector3(width * 0.17f, 0.25f, 2f), s % 2 == 0 ? stripe : white);
                }
            }

            // Street life along the city and the town, just outside the road: lamp posts, benches, planters, flag poles
            // and parked cars, chosen by place (user 2026-10-30, "유령도시같은 느낌").
            private void StreetLife(Transform parent)
            {
                int index = 0;
                for (double s = 5d; s < lap; s += 9d)
                {
                    Scenery here = SceneryAt(s);
                    if (here != Scenery.City && here != Scenery.Harbour && here != Scenery.Town && here != Scenery.Stairs) continue;
                    TrackFrame frame = line.FrameAt(s);
                    int side = index % 2 == 0 ? -1 : 1;
                    float pick = Hash(index * 7 + 101);
                    float radius = pick < 0.3f ? 2.5f : pick < 0.5f ? 1.3f : 0.8f;
                    // 1.5 radii out: the square kerb turned with a diagonal road grows its world-aligned bounds by up to 1.41.
                    Vector3 position = frame.Position + frame.Right * side * (definition.HalfWidthAt(s) + 2.4f + radius * 1.5f);
                    position.y = Footing;
                    index++;
                    if (!Available(position, radius)) continue;
                    Transform spot = Group("Street " + index, parent);
                    spot.SetPositionAndRotation(position, Flat(frame.Forward));
                    // The land is 2.6 m below the road: a paved kerb block from the footing brings the props up to the road.
                    float top = frame.Position.y - Footing;
                    Box("Kerb", spot, new Vector3(0f, top * 0.5f, 0f), new Vector3(radius * 2f, top, radius * 2f), paving);
                    if (pick < 0.3f) Car(spot, top, index);
                    else if (pick < 0.5f)
                    {
                        Box("Bench seat", spot, new Vector3(0f, top + 0.45f, 0f), new Vector3(0.5f, 0.12f, 1.8f), wood);
                        Box("Bench back", spot, new Vector3(side * 0.22f, top + 0.8f, 0f), new Vector3(0.1f, 0.5f, 1.8f), wood);
                        for (int leg = -1; leg <= 1; leg += 2)
                            Box("Bench leg", spot, new Vector3(0f, top + 0.2f, leg * 0.75f), new Vector3(0.45f, 0.4f, 0.1f), metal);
                    }
                    else if (pick < 0.7f)
                    {
                        Box("Lamp post", spot, new Vector3(0f, top + 2.6f, 0f), new Vector3(0.16f, 5.2f, 0.16f), metal);
                        Box("Lamp arm", spot, new Vector3(-side * 0.5f, top + 5.1f, 0f), new Vector3(1f, 0.12f, 0.12f), metal);
                        Box("Lamp head", spot, new Vector3(-side * 0.9f, top + 5f, 0f), new Vector3(0.45f, 0.2f, 0.3f), white);
                    }
                    else if (pick < 0.85f)
                    {
                        Box("Planter", spot, new Vector3(0f, top + 0.4f, 0f), new Vector3(1.2f, 0.8f, 1.2f), stone);
                        Shape("Shrub", spot, new Vector3(0f, top + 1.2f, 0f), new Vector3(1.3f, 1.1f, 1.3f), leaf, sphere);
                    }
                    else
                    {
                        Box("Flag pole", spot, new Vector3(0f, top + 3.5f, 0f), new Vector3(0.12f, 7f, 0.12f), white);
                        Box("Flag", spot, new Vector3(0f, top + 6.2f, 0.7f), new Vector3(0.05f, 1.2f, 1.4f), Pick(index * 13 + 102, cyan, violet, yellow, red, mint));
                    }
                    occupied.Add((position, radius));
                }
            }

            private void Car(Transform spot, float top, int index)
            {
                Material paint = Pick(index * 29 + 103, red, cyan, white, yellow, mint, concrete);
                Box("Car body", spot, new Vector3(0f, top + 0.6f, 0f), new Vector3(1.8f, 0.7f, 4.2f), paint);
                Box("Car cabin", spot, new Vector3(0f, top + 1.2f, -0.2f), new Vector3(1.6f, 0.6f, 2.2f), paint);
                Box("Car windows", spot, new Vector3(0f, top + 1.2f, -0.2f), new Vector3(1.65f, 0.4f, 2.1f), dark);
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        GameObject wheel = Shape("Wheel", spot, new Vector3(x * 0.85f, top + 0.33f, z * 1.35f), new Vector3(0.66f, 0.25f, 0.66f), tire, cylinder);
                        wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }
            }

            private void Palms(Transform parent)
            {
                // Both sides every 12 m (user 2026-10-30, denser); the crowns' spacing keeps them apart where they would touch.
                for (double s = 6d; s < lap; s += 12d)
                {
                    Scenery here = SceneryAt(s);
                    if (here == Scenery.Bridge || NearGap(s, 6d)) continue;
                    TrackFrame frame = line.FrameAt(s);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        // The fronds spread about 4.6 m from the trunk, so a palm's radius is its crown's; Clear wants the
                        // road's half width + 1.5 m + that radius between the trunk and the road's middle.
                        Vector3 position = frame.Position + frame.Right * side * (definition.HalfWidthAt(s) + 7f);
                        position.y = Footing;
                        if (!Available(position, PalmCrown)) continue;
                        Palm(parent, position, 9f + (int)(s + side) % 4, (float)((s + side * 90d) * 37d % 360d));
                        occupied.Add((position, PalmCrown));
                    }
                }
            }

            // A gently leaning trunk of stacked rings and seven drooping fronds round a coconut cluster.
            private void Palm(Transform parent, Vector3 position, float height, float turn)
            {
                Transform palm = Group("Palm tree", parent);
                palm.SetPositionAndRotation(position, Quaternion.Euler(0f, turn, 0f));
                const int Rings = 6;
                Vector3 top = Vector3.zero;
                for (int i = 0; i < Rings; i++)
                {
                    float t = (i + 0.5f) / Rings;
                    Vector3 at = new Vector3(Mathf.Sin(t * 1.2f) * 1.4f, height * t, 0f);
                    Shape("Trunk ring", palm, at, new Vector3(0.75f - t * 0.2f, height / Rings + 0.1f, 0.75f - t * 0.2f), trunk, cylinder);
                    top = at + Vector3.up * height / Rings * 0.5f;
                }
                Shape("Coconuts", palm, top + Vector3.down * 0.3f, Vector3.one * 1f, trunk, sphere);
                for (int f = 0; f < 7; f++)
                {
                    float angle = f * 360f / 7f;
                    Vector3 out1 = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, 1f);
                    GameObject frond = Box("Frond", palm, top + out1 * 2.2f + Vector3.down * 0.4f, new Vector3(1.1f, 0.12f, 4.6f), leaf);
                    frond.transform.localRotation = Quaternion.LookRotation(out1) * Quaternion.Euler(22f, 0f, 0f);
                }
            }

            private void Umbrellas(Transform parent)
            {
                int index = 0;
                for (double s = 3d; s < lap; s += 11d)
                {
                    Scenery here = SceneryAt(s);
                    if (here != Scenery.Umbrellas && here != Scenery.Beach) continue;
                    TrackFrame frame = line.FrameAt(s);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 position = frame.Position + frame.Right * side * (definition.HalfWidthAt(s) + 11f + index % 3 * 3f);
                        position.y = Footing;
                        if (!Available(position, 2.6f)) continue;
                        Transform spot = Group("Beach umbrella " + index, parent);
                        spot.SetPositionAndRotation(position, Flat(frame.Forward));
                        Box("Umbrella pole", spot, new Vector3(0f, 1.6f, 0f), new Vector3(0.12f, 3.2f, 0.12f), white);
                        Shape("Umbrella canopy", spot, new Vector3(0f, 3.3f, 0f), new Vector3(4.2f, 0.35f, 4.2f), index % 2 == 0 ? violet : white, cylinder);
                        Shape("Canopy crown", spot, new Vector3(0f, 3.55f, 0f), new Vector3(2.2f, 0.3f, 2.2f), index % 2 == 0 ? white : violet, cylinder);
                        Box("Sun lounger", spot, new Vector3(1.6f, 0.35f, 0.6f), new Vector3(0.9f, 0.25f, 2.2f), white);
                        Box("Lounger back", spot, new Vector3(1.6f, 0.65f, -0.4f), new Vector3(0.9f, 0.6f, 0.2f), white);
                        occupied.Add((position, 2.6f));
                        index++;
                    }
                }
            }

            // A yellow and dark chevron board on the outside of every corner, as on the reference's sharp turns.
            private void CornerBoards(Transform parent)
            {
                double s = 0d;
                int index = 0;
                foreach (TrackDefinition.Segment segment in definition.Segments)
                {
                    double start = s;
                    s += segment.Length;
                    if (!segment.IsArc || Mathf.Abs(segment.Degrees) < 30f) continue;
                    double middle = start + segment.Length * 0.5d;
                    TrackFrame frame = line.FrameAt(middle);
                    // The outside of a right turn (positive degrees) is the left.
                    float outside = segment.Degrees > 0f ? -1f : 1f;
                    Vector3 position = frame.Position + frame.Right * outside * (definition.HalfWidthAt(middle) + 3.4f);
                    if (!Clear(position, 1.6f)) continue;
                    Transform board = Group("Corner chevrons " + index++, parent);
                    board.SetPositionAndRotation(position, Flat(-frame.Right * outside));
                    for (int side = -1; side <= 1; side += 2)
                        Box("Board leg", board, new Vector3(side * 1.4f, 0.4f, 0f), new Vector3(0.18f, 2.4f, 0.18f), dark);
                    Box("Board", board, new Vector3(0f, 1.9f, 0f), new Vector3(3.6f, 1.3f, 0.15f), dark);
                    // Seen from the road the board's +x runs back along the track on the left, forward on the right: the
                    // chevrons point the way the runner goes.
                    float point = segment.Degrees > 0f ? -1f : 1f;
                    for (int c = -1; c <= 1; c++)
                    {
                        Beam("Chevron", board, new Vector3(c * 1f - point * 0.3f, 1.45f, 0.12f), new Vector3(c * 1f + point * 0.3f, 1.9f, 0.12f), 0.16f, yellow);
                        Beam("Chevron", board, new Vector3(c * 1f + point * 0.3f, 1.9f, 0.12f), new Vector3(c * 1f - point * 0.3f, 2.35f, 0.12f), 0.16f, yellow);
                    }
                    occupied.Add((position, 2f));
                }
            }

            private void Lettering(Transform parent, string words, float width, float z)
            {
                // Solid 3x5 glyphs, so lettering obeys depth and needs no font/texture dependencies (as Finger Lanes').
                var glyphs = new Dictionary<char, string>
                {
                    { 'P', "110101110100100" }, { 'A', "010101111101101" }, { 'L', "100100100100111" },
                    { 'M', "101111111101101" }, { 'C', "111100100100111" }, { 'O', "111101101101111" },
                    { 'S', "111100111001111" }, { 'T', "111010010010010" }, { ' ', "000000000000000" }
                };
                float step = width / words.Length;
                float cell = step / 4f;
                for (int i = 0; i < words.Length; i++)
                    for (int row = 0; row < 5; row++)
                        for (int col = 0; col < 3; col++)
                            if (glyphs[words[i]][row * 3 + col] == '1')
                                Box("Solid letter " + words[i], parent, new Vector3(-width * 0.5f + i * step + (col + 0.5f) * cell, 11.4f + (2 - row) * cell * 0.9f, z),
                                    new Vector3(cell * 0.9f, cell * 0.8f, 0.14f), violet);
            }

            private bool Clear(Vector3 position, float radius)
            {
                for (int i = 0; i < samples.Count; i++)
                {
                    float limit = widths[i] + 1.5f + radius;
                    float dx = position.x - samples[i].x, dz = position.z - samples[i].z;
                    if (dx * dx + dz * dz < limit * limit) return false;
                }
                return true;
            }

            private bool Available(Vector3 position, float radius)
            {
                if (!Clear(position, radius)) return false;
                foreach (var other in occupied)
                {
                    float limit = radius + other.Radius;
                    float dx = position.x - other.Position.x, dz = position.z - other.Position.z;
                    if (dx * dx + dz * dz < limit * limit) return false;
                }
                return true;
            }

            // Every renderer stays outside the road corridor, except the land and sea below it and what spans the road
            // above the runner: the start arch's beam and lettering and the bridges' top beams (9 m and more above the road).
            internal void ValidateClearance(GameObject root)
            {
                float minimum = float.PositiveInfinity;
                var intrusions = new List<string>();
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Bounds bounds = renderer.bounds;
                    if (bounds.max.y <= Ground + 0.01f) continue;
                    for (int i = 0; i < samples.Count; i++)
                    {
                        float dx = Mathf.Max(bounds.min.x - samples[i].x, 0f, samples[i].x - bounds.max.x);
                        float dz = Mathf.Max(bounds.min.z - samples[i].z, 0f, samples[i].z - bounds.max.z);
                        float margin = Mathf.Sqrt(dx * dx + dz * dz) - widths[i];
                        if (margin >= 0.7f) continue;
                        if (bounds.min.y >= samples[i].y + 9f) continue;
                        minimum = Mathf.Min(minimum, margin);
                        intrusions.Add($"{renderer.transform.parent.name}/{renderer.name}: margin {margin:F2} m at {samples[i]}");
                        break;
                    }
                }
                if (intrusions.Count != 0) throw new InvalidOperationException("Decoration intrudes into the road corridor:\n" + string.Join("\n", intrusions));
                Debug.Log("Palm Coast decoration clearance: no renderer within 0.7 m of the road edge below 9 m over the road.");
            }

            private static Quaternion Flat(Vector3 forward)
            {
                forward.y = 0f;
                return Quaternion.LookRotation(forward.sqrMagnitude > 1e-6f ? forward : Vector3.forward);
            }

            private static Transform Group(string name, Transform parent)
            {
                var group = new GameObject(name).transform;
                group.SetParent(parent, false);
                return group;
            }

            private GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
                => Shape(name, parent, position, scale, material, cube);

            private void Beam(string name, Transform parent, Vector3 a, Vector3 b, float width, Material material)
            {
                GameObject beam = Box(name, parent, (a + b) * 0.5f, new Vector3(width, (b - a).magnitude, width), material);
                beam.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            }

            private static GameObject Shape(string name, Transform parent, Vector3 position, Vector3 scale, Material material, Mesh mesh)
            {
                var shape = new GameObject(name);
                shape.transform.SetParent(parent, false);
                shape.transform.localPosition = position;
                // Built-in FBX bounds differ: cube is 1 m, sphere/cylinder are 2 m. Callers specify actual dimensions.
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
        }
    }
}
