using System;
using System.Collections.Generic;
using System.IO;
using ProtoHarness.ChainRush.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ProtoHarness.Editor.ChainRush.ExteriorKit;

namespace ProtoHarness.Editor.ChainRush
{
    // Decoration only, like ChainRushTrackStadium: no physics or gameplay components are saved. The unreachable
    // highway slabs beyond the guards are scenery too (user 2026-11-02).
    // The prefab is durable; the new additive scene is deliberately left for the user's normal Save Scene command.
    // Its own road, wall and lane-marking materials are a TrackLook (user 2026-11-02, "해변 서킷처럼 … 서킷 도로와 벽에
    // 대한 디자인"), read from a record video of the original's elevated run: blue-grey asphalt, the viaduct's pale
    // concrete barriers, white lines.
    public static class SkywayEnvironmentBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/SkywayExterior.prefab";
        public const string LookPath = "Assets/_Project/Data/TrackLook_Skyway.asset";
        public const string ScenePath = "Assets/_Project/Scenes/ChainRushTrackSkyway.unity";
        private const string DefinitionPath = "Assets/_Project/Data/Circuit_Skyway.asset";
        private const float Ground = -1.3f;

        [MenuItem("ProtoHarness/Chain Rush/Create Skyway Exterior")]
        public static void CreateExterior()
        {
            ExteriorAssets.RequireIdle();
            if (File.Exists(PrefabPath) || File.Exists(ScenePath))
                throw new InvalidOperationException("Skyway exterior already exists; use Rebuild Skyway Exterior.");
            BuildLook();
            BuildPrefab();
            GameObject instance = ExteriorAssets.OpenDecorationScene(PrefabPath, "ChainRushTrackSkyway");
            Debug.Log($"Skyway exterior created. Save the decoration scene as {ScenePath}, add it to Build Profiles, then give the lobby's SKYWAY LOOP entry the scene name and {LookPath}.", instance);
        }

        // Lays the exterior out again on the current Circuit_Skyway (its layout and road width changed, DECISIONS 2026-10-22)
        // and writes it over the existing prefab, so its GUID and the decoration scene's instance stay.
        [MenuItem("ProtoHarness/Chain Rush/Rebuild Skyway Exterior")]
        public static void RebuildExterior()
        {
            ExteriorAssets.RequireIdle();
            if (!File.Exists(PrefabPath)) throw new InvalidOperationException("There is no Skyway exterior to rebuild; use Create Skyway Exterior.");
            BuildLook();
            BuildPrefab();
        }

        private static void BuildPrefab()
        {
            var layout = new Layout(ExteriorAssets.RequireDefinition(DefinitionPath));
            ExteriorAssets.SavePrefab(PrefabPath, layout.Build, layout.ValidateClearance);
        }

        // Grey asphalt and concrete barriers (user 2026-11-02, "실제 아스팔트 도로 같은 디자인"); 3.6 m lanes, and a two-way
        // highway's double yellow middle line on the 30 m viaduct only (its half width 15; the 12 m roads have three
        // lanes round the middle). A hazier day over the city, the outer blocks fading into it.
        // The look and its sky only, the decoration left as it is (user 2026-11-02, the track skies).
        [MenuItem("ProtoHarness/Chain Rush/Rebuild Skyway Look")]
        public static void BuildLook()
            => ExteriorAssets.BuildLook(LookPath,
                ExteriorAssets.Surface("Skyway", "Road", new Color(0.26f, 0.27f, 0.29f), ExteriorAssets.Grain.Asphalt, 4f, 0.12f),
                ExteriorAssets.Surface("Skyway", "Wall", new Color(0.74f, 0.74f, 0.71f), ExteriorAssets.Grain.Concrete, 3f, 0.08f),
                ExteriorAssets.Palette("Skyway", "Marking", new Color(0.93f, 0.93f, 0.90f), 0.05f),
                ExteriorAssets.Palette("Skyway", "Accent", new Color(0.96f, 0.78f, 0.18f), 0.05f),
                new ExteriorAssets.Lanes(3.6f, 14f, true, false),
                ExteriorAssets.BuildSky("Skyway", new Color(0.33f, 0.55f, 0.86f), new Color(0.80f, 0.85f, 0.91f), new Color(0.98f, 0.98f, 0.97f), 0.5f, false, 180f, 620f));

        private static Material Require(string name) => ExteriorAssets.Require<Material>(ExteriorAssets.MaterialFolder + name);

        // Use after the user has saved/registered the scene. This edits only the selected lobby entry and never saves it.
        [MenuItem("ProtoHarness/Chain Rush/Connect Saved Skyway Exterior To Lobby")]
        public static void ConnectLobby()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before connecting the lobby.");
            if (!File.Exists(ScenePath) || SceneUtility.GetBuildIndexByScenePath(ScenePath) < 0)
                throw new InvalidOperationException("Save ChainRushTrackSkyway and enable it in Build Profiles before connecting the lobby.");
            TrackDefinition definition = ExteriorAssets.Require<TrackDefinition>(DefinitionPath);
            bool connected = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.path != "Assets/_Project/Scenes/ChainRushLobby.unity") continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (var menu in root.GetComponentsInChildren<ProtoHarness.ChainRush.Flow.LobbyMenu>(true))
                    {
                        var data = new SerializedObject(menu);
                        SerializedProperty tracks = data.FindProperty("tracks");
                        for (int j = 0; j < tracks.arraySize; j++)
                        {
                            SerializedProperty entry = tracks.GetArrayElementAtIndex(j);
                            if (entry.FindPropertyRelative("definition").objectReferenceValue != definition) continue;
                            Undo.RecordObject(menu, "Connect Skyway exterior");
                            entry.FindPropertyRelative("environmentScene").stringValue = "ChainRushTrackSkyway";
                            data.ApplyModifiedProperties();
                            EditorSceneManager.MarkSceneDirty(scene);
                            connected = true;
                        }
                    }
                }
            }
            if (!connected) throw new InvalidOperationException("Open ChainRushLobby with the existing SKYWAY LOOP entry first.");
            Debug.Log("Skyway exterior connected. Save ChainRushLobby with the normal editor Save command.");
        }

        // Upgrades this builder's own open instance; never touches another prefab or deletes an asset.
        public static void RefreshLandmarkLettering(GameObject root)
        {
            ExteriorAssets.ValidateDecoration(root);
            Transform landmarks = root.transform.Find("07 Skyway landmarks");
            if (landmarks == null) throw new InvalidOperationException("Skyway landmark group is missing.");
            foreach (Transform landmark in landmarks)
            {
                foreach (TextMesh label in landmark.GetComponentsInChildren<TextMesh>())
                    Undo.DestroyObjectImmediate(label.gameObject);
                if (landmark.Find("SKYWAY lettering") != null)
                    throw new InvalidOperationException("The landmark already has solid lettering.");
                Layout.Lettering(landmark, Require("M_ArtStadiumMint.mat"),
                    Require("M_ArtStadiumAmber.mat"));
            }
            PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        // R4 detail (user 2026-11-02, "skyway 맵 장식 디테일 살려줘. 맵이 너무 휑하다"): read from the original's record video
        // (scratchpad contact sheets of UZWC4qtamfk) — a bright town of plaster houses and trees under and beside the
        // viaduct, advertising boards behind the ground road's barrier, yellow and black chevron boards on the corners,
        // lamp masts and blue sign gantries over the elevated road, a checkered start arch. Before it the land was one dark
        // slab with box houses only beside the ground road.
        private sealed class Layout
        {
            private readonly TrackDefinition definition;
            private readonly Centerline line;
            // How far outside the road's edge the town keeps away.
            private const float KeepOut = 7f;
            // Trackside things (boards, masts, gantry legs) stand closer: the clearance check's own margin is 0.7 m.
            private const float NearKeepOut = 1.2f;
            // Overhead parts (lamp arms, gantry beams and signs, the start arch's beam) start this far above the highest
            // road near them; the clearance check lets things 9 m and more above the road through.
            private const float OverheadClear = 9.4f;
            private const string PierGroup = "04 Elevated viaduct piers";
            // The land reaches well past the circuit (740 x 550 m before, its edge in sight from the viaduct).
            private const float FoundationX = 1500f, FoundationZ = 1200f;
            // How far the unreachable highway runs on past each of its guards.
            private const float Continuation = 300f;
            private const string DeckGroup = "09 Highway continuation";
            private const string LandGroup = "01 Landscape";
            private const string OuterGroup = "15 Outer city";
            private readonly double length;
            private readonly RoadCorridor corridor;
            private readonly RoadCorridor near;
            // The road's points that are low enough for things standing on the land to meet: where it runs high, the land
            // under it is the avenue's.
            private readonly System.Collections.Generic.List<(Vector3 Position, float Reach)> lowRoad = new System.Collections.Generic.List<(Vector3, float)>();
            private readonly Material ink = Require("M_ArtStadiumInk.mat");
            private readonly Material steel = Require("M_ArtStadiumSteel.mat");
            private readonly Material mint = Require("M_ArtStadiumMint.mat");
            private readonly Material amber = Require("M_ArtStadiumAmber.mat");
            private readonly Material concrete = Require("M_StadiumSportsConcrete.mat");
            private readonly Material paving = Palette("Paving", new Color(0.80f, 0.77f, 0.72f));
            private readonly Material lawn = Palette("Lawn", new Color(0.48f, 0.66f, 0.36f));
            private readonly Material asphalt = Palette("Asphalt", new Color(0.30f, 0.31f, 0.33f));
            private readonly Material sidewalk = Palette("Sidewalk", new Color(0.86f, 0.72f, 0.66f));
            private readonly Material paint = Palette("Paint", new Color(0.93f, 0.93f, 0.90f));
            private readonly Material cream = Palette("Cream", new Color(0.94f, 0.89f, 0.76f));
            private readonly Material pink = Palette("Pink", new Color(0.91f, 0.70f, 0.66f));
            private readonly Material apricot = Palette("Apricot", new Color(0.95f, 0.76f, 0.55f));
            private readonly Material sky = Palette("Sky", new Color(0.66f, 0.80f, 0.90f));
            private readonly Material plaster = Palette("Plaster", new Color(0.95f, 0.94f, 0.91f));
            private readonly Material sage = Palette("Sage", new Color(0.72f, 0.84f, 0.74f));
            private readonly Material tile = Palette("Tile", new Color(0.74f, 0.38f, 0.28f));
            private readonly Material slate = Palette("Slate", new Color(0.38f, 0.44f, 0.54f));
            private readonly Material glass = Palette("Glass", new Color(0.40f, 0.54f, 0.66f), 0.05f);
            private readonly Material wood = Palette("Wood", new Color(0.55f, 0.40f, 0.28f));
            private readonly Material leaf = Palette("Leaf", new Color(0.33f, 0.58f, 0.32f));
            private readonly Material leafDark = Palette("LeafDark", new Color(0.22f, 0.45f, 0.27f));
            private readonly Material trunk = Palette("Trunk", new Color(0.47f, 0.35f, 0.24f));
            private readonly Material yellow = Palette("Yellow", new Color(0.97f, 0.80f, 0.18f));
            private readonly Material black = Palette("Black", new Color(0.12f, 0.12f, 0.13f));
            private readonly Material red = Palette("Red", new Color(0.86f, 0.24f, 0.21f));
            private readonly Material orange = Palette("Orange", new Color(0.96f, 0.56f, 0.20f));
            private readonly Material violet = Palette("Violet", new Color(0.53f, 0.41f, 0.73f));
            private readonly Material signBlue = Palette("SignBlue", new Color(0.15f, 0.37f, 0.72f));
            private readonly Material tire = Palette("Tire", new Color(0.13f, 0.13f, 0.14f));

            internal Layout(TrackDefinition definition)
            {
                this.definition = definition;
                line = definition.BuildCenterline(Vector3.zero, 0f);
                length = definition.LapLength;
                corridor = new RoadCorridor(definition, line, 2d, KeepOut);
                near = new RoadCorridor(definition, line, 1d, NearKeepOut);
                for (double s = 0d; s < length; s += 2d)
                {
                    TrackFrame f = line.FrameAt(s);
                    if (f.Position.y > 3f) continue;
                    (float left, float right) = Edges(s);
                    lowRoad.Add((f.Position, Mathf.Max(-left, right)));
                }
            }

            private static Material Palette(string name, Color color, float glow = 0f) => ExteriorAssets.Palette("Skyway", name, color, glow);

            private float Clearance(double s) => definition.HalfWidthAt(s) + KeepOut;

            private double Wrap(double s) => (s % length + length) % length;

            // The road's edges at s, metres right of the centerline: the main road's, widened on a bay's side (the on-ramp's
            // ground bay, the highway's other lanes).
            private (float Left, float Right) Edges(double s)
            {
                float half = definition.HalfWidthAt(s);
                float left = -half, right = half;
                foreach (TrackDefinition.Bay bay in definition.Bays)
                {
                    if (s < bay.FromS || s > bay.ToS) continue;
                    float extra = bay.StraightDeck ? bay.Width : bay.WidthAt(s);
                    if (bay.Left) left -= extra;
                    else right += extra;
                }
                return (left, right);
            }

            private float Edge(double s, int side)
            {
                (float left, float right) = Edges(s);
                return side < 0 ? left : right;
            }

            // The highest the road runs within reach of s: what spans it overhead clears that by OverheadClear.
            private float HighestRoad(double s, double reach)
            {
                float top = float.NegativeInfinity;
                for (double t = s - reach; t <= s + reach; t += 2d) top = Mathf.Max(top, line.FrameAt(Wrap(t)).Position.y);
                return top;
            }

            // Whether something of radius on the land stays margin outside the road wherever it runs low (under the high
            // road is the avenue's).
            private bool ClearOfLowRoad(Vector3 position, float radius, float margin)
            {
                foreach ((Vector3 p, float reach) in lowRoad)
                {
                    float limit = reach + margin + radius;
                    float dx = position.x - p.x, dz = position.z - p.z;
                    if (dx * dx + dz * dz < limit * limit) return false;
                }
                return true;
            }

            // Everything above the ground stays outside the road; the viaduct's piers and caps stay under its slab.
            internal void ValidateClearance(GameObject root)
            {
                Transform piers = root.transform.Find(PierGroup);
                if (piers == null) throw new InvalidOperationException("Skyway pier group is missing: " + PierGroup);
                Transform deck = root.transform.Find(DeckGroup);
                if (deck == null) throw new InvalidOperationException("Skyway continuation group is missing: " + DeckGroup);
                Transform land = root.transform.Find(LandGroup), outer = root.transform.Find(OuterGroup);
                if (land == null || outer == null) throw new InvalidOperationException("Skyway land or outer city group is missing.");
                // The merged land and outer city spread round the road, so their bounds always hold it: vertex by vertex.
                corridor.ValidateClearance(root, Ground + 0.01f, piers, new[] { deck, land, outer });
            }

            internal GameObject Build(Scene scene)
            {
                var root = new GameObject("Skyway Exterior");
                SceneManager.MoveGameObjectToScene(root, scene);
                Transform landscape = Group(LandGroup, root.transform);
                Land(landscape);

                // The fixed landmarks and the skyline first, so the town fills round them.
                Transform landmarks = Group("07 Skyway landmarks", root.transform);
                Landmark(landmarks, new Vector3(-28f, Ground, 28f), Quaternion.Euler(0f, 90f, 0f), "SKYWAY", "GARDEN DISTRICT");
                Landmark(landmarks, new Vector3(-220f, Ground, -153f), Quaternion.Euler(0f, 180f, 0f), "SKYWAY", "ELEVATED EXPRESS");
                Transform skyline = Group("06 Skyline", root.transform);
                for (int i = 0; i < 22; i++)
                {
                    float x = -365f + i * 28f;
                    float z = i % 2 == 0 ? -252f : 158f;
                    float height = 24f + (i * 17 % 47);
                    Tower(skyline, new Vector3(x, Ground, z), 13f + i % 3 * 3f, height, i);
                }
                Tower(skyline, new Vector3(-375f, Ground, -80f), 25f, 64f, 22);
                Tower(skyline, new Vector3(205f, Ground, -90f), 22f, 52f, 23);
                // The existing highway runs on to the horizon past both guards (user 2026-11-02, "맵 외곽쪽이 부족해보인다.
                // 특히 고가도로쪽"); the town keeps off it.
                Transform supports = Group(PierGroup, root.transform);
                Transform decks = Group(DeckGroup, root.transform);
                foreach (TrackDefinition.Bay bay in definition.Bays)
                    if (bay.StraightDeck) HighwayContinuation(decks, supports, bay);

                Transform boards = Group("08 On-ramp end boards", root.transform);
                foreach (TrackDefinition.Bay bay in definition.Bays)
                    if (!bay.StraightDeck) BayEndBoard(boards, bay);
                // Trackside, each keeping the places of the ones before: the arch, the gantries, the corner boards, the
                // advertising boards, then the lamps between them.
                StartArch(Group("10 Start arch", root.transform));
                Gantries(Group("11 Overhead sign gantries", root.transform));
                CornerBoards(Group("12 Corner chevrons", root.transform));
                Billboards(Group("13 Advertising boards", root.transform));
                Lamps(Group("05 Road lamps and banners", root.transform));
                HighwaySide(Group("14 Flats and billboards by the viaduct", root.transform));

                Transform village = Group("02 Village and shops", root.transform);
                int index = 0;
                for (float s = 0f; s < length; s += 26f)
                {
                    TrackFrame f = line.FrameAt(s);
                    for (int side = -1; side <= 1; side += 2)
                        for (int row = 0; row < 2; row++)
                        {
                            index++;
                            if (row == 1 && Hash(index * 5 + 1) < 0.4f) continue;
                            float width = 8f + Hash(index * 3 + 2) * 5f;
                            float depth = 8f + Hash(index * 7 + 3) * 3f;
                            float radius = new Vector2(width + 1.6f, depth + 2.4f).magnitude * 0.5f;
                            float out1 = Mathf.Abs(Edge(s, side)) + KeepOut + radius + 2f + row * (radius * 2f + 5f);
                            Vector3 p = f.Position + f.Right * side * out1;
                            p.y = Ground;
                            if (!corridor.Available(p, radius)) continue;
                            TownHouse(village, p, Flat(-f.Right * side), width, depth, 6.5f + Hash(index * 11 + 4) * 6f, index);
                            corridor.Reserve(p, radius);
                        }
                }

                Transform gardens = Group("03 Gardens and avenue", root.transform);
                index = 0;
                for (float s = 7f; s < length; s += 13f)
                {
                    TrackFrame f = line.FrameAt(s);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        index++;
                        Vector3 p = f.Position + f.Right * side * (Mathf.Abs(Edge(s, side)) + KeepOut + 3f + Hash(index * 13 + 5) * 4f);
                        p.y = Ground;
                        if (!corridor.Available(p, 2.6f)) continue;
                        Tree(gardens, p, index);
                        corridor.Reserve(p, 2.6f);
                    }
                }
                for (int x = 0; x < 5; x++)
                    for (int z = 0; z < 3; z++)
                    {
                        Vector3 p = new Vector3(-185f + x * 28f, Ground, -125f + z * 32f);
                        if (!corridor.Available(p, 10f)) continue;
                        Box("Garden island", gardens, p + Vector3.up * 0.12f, new Vector3(18f, 0.24f, 21f), lawn);
                        Tree(gardens, p + new Vector3(-4f, 0f, -4f), x * 3 + z);
                        Tree(gardens, p + new Vector3(4f, 0f, 5f), x * 3 + z + 40);
                        corridor.Reserve(p, 10f);
                    }
                Transform outerCity = Group(OuterGroup, root.transform);
                Outskirts(outerCity);

                // Wherever the road runs high enough over the ground; the cap spans the road's width there.
                for (float s = 0f; s < length; s += 32f)
                {
                    TrackFrame f = line.FrameAt(s);
                    float half = definition.HalfWidthAt(s);
                    Vector3 centre = f.Position;
                    foreach (TrackDefinition.Bay bay in definition.Bays)
                        if (bay.StraightDeck && s >= bay.FromS && s <= bay.ToS)
                        {
                            centre -= f.Right * bay.Width * 0.5f;
                            half += bay.Width * 0.5f;
                        }
                    float top = f.Position.y - 1.5f;
                    if (top < Ground + 3f) continue;
                    Transform pier = Group("Pier S" + s.ToString("0"), supports);
                    pier.SetPositionAndRotation(new Vector3(centre.x, Ground, centre.z), Quaternion.LookRotation(f.Forward));
                    float height = top - Ground;
                    Box("Plinth", pier, new Vector3(0f, 0.35f, 0f), new Vector3(5.8f, 0.7f, 5.5f), ink);
                    Box("Concrete pier", pier, new Vector3(0f, height * 0.5f, 0f), new Vector3(2.7f, height, 2.9f), concrete);
                    Box("Pier band", pier, new Vector3(0f, 2.2f, 0f), new Vector3(2.8f, 0.5f, 3f), yellow);
                    Box("Hammerhead cap", pier, new Vector3(0f, height - 0.45f, 0f), new Vector3(half * 2f - 0.8f, 0.9f, 3.4f), steel);
                }
                MergeByMaterial(landscape, "Land");
                MergeByMaterial(outerCity, "Outer");
                return root;
            }

            // The land's and the outer city's thousands of plain parts become one mesh per material, saved as
            // Art/Models/SkywayExterior_<key>_<material>.asset (user 2026-11-02, "메시로 합치고": 10661 renderers, twice
            // PALM COAST's). The parts' transforms are baked in; the group keeps its place. A rebuild writes the same
            // assets over (ExteriorAssets.SaveMesh keeps their GUIDs).
            private static void MergeByMaterial(Transform group, string key)
            {
                var byMaterial = new Dictionary<Material, List<CombineInstance>>();
                Matrix4x4 toGroup = group.worldToLocalMatrix;
                foreach (MeshFilter filter in group.GetComponentsInChildren<MeshFilter>(true))
                {
                    Material material = filter.GetComponent<MeshRenderer>().sharedMaterial;
                    if (!byMaterial.TryGetValue(material, out List<CombineInstance> parts)) byMaterial[material] = parts = new List<CombineInstance>();
                    parts.Add(new CombineInstance { mesh = filter.sharedMesh, transform = toGroup * filter.transform.localToWorldMatrix });
                }
                for (int i = group.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(group.GetChild(i).gameObject);
                foreach (KeyValuePair<Material, List<CombineInstance>> pair in byMaterial)
                {
                    var mesh = new UnityEngine.Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                    mesh.RecalculateBounds();
                    UnityEngine.Mesh saved = ExteriorAssets.SaveMesh(mesh, $"Assets/_Project/Art/Models/SkywayExterior_{key}_{pair.Key.name}.asset");
                    UnityEngine.Object.DestroyImmediate(mesh);
                    var merged = new GameObject(key + " " + pair.Key.name);
                    merged.transform.SetParent(group, false);
                    merged.AddComponent<MeshFilter>().sharedMesh = saved;
                    MeshRenderer renderer = merged.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = pair.Key;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    GameObjectUtility.SetStaticEditorFlags(merged, StaticEditorFlags.BatchingStatic);
                }
            }

            // Pale paving over the whole district with lawn blocks on a 40 m grid, and under the high road a town avenue:
            // asphalt with dashed lines either side of the piers, pink sidewalks and parked cars. Every flat part lies at
            // most 0.09 m above the land, under every road's slab, so the clearance check passes them anywhere.
            private void Land(Transform parent)
            {
                Box("City foundation", parent, new Vector3(-85f, Ground - 0.5f, -55f), new Vector3(FoundationX, 1f, FoundationZ), paving);
                int index = 0;
                for (float x = -85f - FoundationX * 0.5f; x < -85f + FoundationX * 0.5f - 30f; x += 40f)
                    for (float z = -55f - FoundationZ * 0.5f; z < -55f + FoundationZ * 0.5f - 30f; z += 40f)
                    {
                        if (Hash(++index * 17 + 6) > 0.55f) continue;
                        Box("Lawn block", parent, new Vector3(x + 20f, Ground + 0.02f, z + 20f), new Vector3(34f, 0.04f, 34f), lawn);
                    }
                Transform avenue = Group("Avenue under the viaduct", parent);
                int piece = 0;
                for (double s = 0d; s < length; s += 4d)
                {
                    TrackFrame f = line.FrameAt(s);
                    if (f.Position.y < 6f) continue;
                    piece++;
                    Transform at = Group("Avenue S" + s.ToString("0"), avenue);
                    at.SetPositionAndRotation(new Vector3(f.Position.x, Ground, f.Position.z), Flat(f.Forward));
                    Box("Avenue asphalt", at, new Vector3(0f, 0.045f, 0f), new Vector3(18f, 0.03f, 6.5f), asphalt);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Box("Sidewalk", at, new Vector3(side * 10.6f, 0.06f, 0f), new Vector3(3.2f, 0.06f, 6.5f), sidewalk);
                        if (piece % 2 == 0) Box("Lane dash", at, new Vector3(side * 4.4f, 0.075f, 0f), new Vector3(0.18f, 0.03f, 3f), paint);
                    }
                    if (Hash(piece * 23 + 7) > 0.18f) continue;
                    int lane = Hash(piece * 29 + 8) < 0.5f ? -1 : 1;
                    Vector3 spot = at.TransformPoint(new Vector3(lane * 6.6f, 0f, 0f));
                    if (!ClearOfLowRoad(spot, 2.5f, 1.5f)) continue;
                    Transform car = Group("Parked car", at);
                    car.localPosition = new Vector3(lane * 6.6f, 0f, 0f);
                    car.localRotation = Quaternion.Euler(0f, lane < 0 ? 180f : 0f, 0f);
                    Car(car, piece);
                }
            }

            private void Car(Transform spot, int index)
            {
                Material colour = Pick(index * 31 + 9, red, sky, plaster, yellow, sage, orange);
                Box("Car body", spot, new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 0.7f, 4.2f), colour);
                Box("Car cabin", spot, new Vector3(0f, 1.2f, -0.2f), new Vector3(1.6f, 0.6f, 2.2f), colour);
                Box("Car windows", spot, new Vector3(0f, 1.2f, -0.2f), new Vector3(1.65f, 0.4f, 2.1f), ink);
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        GameObject wheel = Shape("Wheel", spot, new Vector3(x * 0.85f, 0.33f, z * 1.35f), new Vector3(0.66f, 0.25f, 0.66f), tire, Cylinder);
                        wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }
            }

            // Over the start line: striped posts, a dark beam with the solid SKYWAY lettering facing the grid and a band of
            // checkers under it. Everything over the road starts OverheadClear above it.
            private void StartArch(Transform parent)
            {
                TrackFrame frame = line.FrameAt(0d);
                (float left, float right) = Edges(0d);
                float half = Mathf.Max(-left, right) + 1.9f;
                float clear = HighestRoad(0d, 20d) - frame.Position.y + OverheadClear;
                Transform arch = Group("Checkered start arch", parent);
                arch.SetPositionAndRotation(frame.Position, Flat(frame.Forward));
                float depth = frame.Position.y - Ground;
                for (int side = -1; side <= 1; side += 2)
                {
                    float top = clear + 3.4f;
                    Box("Arch post", arch, new Vector3(side * half, (top - depth) * 0.5f, 0f), new Vector3(0.9f, top + depth, 0.9f), plaster);
                    for (int band = 0; band < 5; band++)
                        Box("Arch post stripe", arch, new Vector3(side * half, 0.6f + band * 1.6f, 0f), new Vector3(0.95f, 0.7f, 0.95f), band % 2 == 0 ? yellow : black);
                    near.Reserve(arch.TransformPoint(new Vector3(side * half, 0f, 0f)), 1f);
                }
                // The beam spans clear + 1 .. clear + 3.2 over the checkers; Lettering draws at 10.65 .. 12.5 m on its front
                // (-Z), so the arch's lettering group is lowered to put the word in the beam's middle.
                Box("Arch beam", arch, new Vector3(0f, clear + 2.1f, 0f), new Vector3(half * 2f + 0.9f, 2.2f, 0.7f), ink);
                int squares = Mathf.CeilToInt(half * 2f / 1f);
                for (int i = 0; i < squares; i++)
                    for (int row = 0; row < 2; row++)
                        for (int face = -1; face <= 1; face += 2)
                            Box("Checker", arch, new Vector3(-half + 0.5f + i * 1f, clear + 0.25f + row * 0.5f, face * 0.3f), new Vector3(1f, 0.5f, 0.12f), (i + row) % 2 == 0 ? black : plaster);
                Transform word = Group("Arch lettering", arch);
                word.localPosition = new Vector3(0f, clear + 2.1f - 11.575f, 0.02f);
                // Lettering reads from the landmark's side; runners coming to the arch see its front from the other way.
                word.localScale = new Vector3(-1f, 1f, 1f);
                Lettering(word, mint, amber);
            }

            // Blue direction signs on gantries over the long high straights (the reference's elevated run), legs on the land
            // just outside the road, beam and signs overhead.
            private void Gantries(Transform parent)
            {
                double s0 = 0d;
                int index = 0;
                foreach (TrackDefinition.Segment segment in definition.Segments)
                {
                    double start = s0;
                    s0 += segment.Length;
                    if (segment.IsArc || segment.IsGap || segment.Length < 60f) continue;
                    if (line.FrameAt(start + segment.Length * 0.5d).Position.y < 6f) continue;
                    int count = Mathf.Max(1, (int)(segment.Length / 100f));
                    for (int k = 0; k < count; k++)
                    {
                        double s = start + segment.Length * (k + 0.5d) / count;
                        // Not on a swing pole's spot.
                        bool pole = false;
                        foreach (TrackDefinition.Pole p in definition.Poles) pole |= Math.Abs(p.S - s) < 8d;
                        if (pole) s += 15d;
                        Gantry(parent, s, index++, k == count - 1 && count > 1);
                    }
                }
            }

            private void Gantry(Transform parent, double s, int index, bool exit)
            {
                TrackFrame frame = line.FrameAt(s);
                (float left, float right) = Edges(s);
                float leftLeg = left - 1.9f, rightLeg = right + 1.9f;
                Vector3 a = frame.TransformPoint(new Vector3(leftLeg, 0f, 0f)), b = frame.TransformPoint(new Vector3(rightLeg, 0f, 0f));
                if (!near.Available(new Vector3(a.x, Ground, a.z), 0.5f) || !near.Available(new Vector3(b.x, Ground, b.z), 0.5f)) return;
                Transform gantry = Group("Sign gantry " + index, parent);
                gantry.SetPositionAndRotation(frame.Position, Flat(frame.Forward));
                float clear = HighestRoad(s, 20d) - frame.Position.y + OverheadClear;
                float depth = frame.Position.y - Ground;
                for (int side = 0; side < 2; side++)
                {
                    float x = side == 0 ? leftLeg : rightLeg;
                    float top = clear + 3.2f;
                    Box("Gantry leg", gantry, new Vector3(x, (top - depth) * 0.5f, 0f), new Vector3(0.6f, top + depth, 0.6f), steel);
                    near.Reserve(gantry.TransformPoint(new Vector3(x, 0f, 0f)), 1f);
                }
                Box("Gantry truss", gantry, new Vector3((leftLeg + rightLeg) * 0.5f, clear + 2.9f, 0f), new Vector3(rightLeg - leftLeg + 0.6f, 0.5f, 0.6f), steel);
                Box("Gantry truss", gantry, new Vector3((leftLeg + rightLeg) * 0.5f, clear + 0.35f, 0.25f), new Vector3(rightLeg - leftLeg + 0.6f, 0.3f, 0.3f), steel);
                // Over the race's lanes a straight-on arrow; on the last gantry before an exit a second sign turning right.
                Sign(gantry, new Vector3(0f, clear + 1.6f, -0.45f), 0f);
                if (exit) Sign(gantry, new Vector3(right - 1.6f - 2.4f, clear + 1.6f, -0.45f), 45f);
                else if (left < -definition.HalfWidthAt(s) - 1f) Sign(gantry, new Vector3(left * 0.5f - 3f, clear + 1.6f, -0.45f), 0f);
            }

            // A blue sign panel facing the runners (-Z) with a white arrow turned by degrees (0 straight up, positive right).
            private void Sign(Transform parent, Vector3 centre, float degrees)
            {
                Box("Sign panel", parent, centre, new Vector3(4.6f, 2.4f, 0.12f), signBlue);
                Box("Sign border", parent, centre + new Vector3(0f, 0f, 0.02f), new Vector3(4.8f, 2.6f, 0.08f), plaster);
                Quaternion turn = Quaternion.Euler(0f, 0f, degrees);
                Vector3 front = new Vector3(0f, 0f, -0.08f);
                Vector3 tail = turn * new Vector3(0f, -0.8f, 0f), tip = turn * new Vector3(0f, 0.8f, 0f);
                Beam("Arrow stem", parent, centre + front + tail, centre + front + tip * 0.75f, 0.28f, plaster);
                Beam("Arrow head", parent, centre + front + tip, centre + front + turn * new Vector3(-0.55f, 0.25f, 0f), 0.24f, plaster);
                Beam("Arrow head", parent, centre + front + tip, centre + front + turn * new Vector3(0.55f, 0.25f, 0f), 0.24f, plaster);
            }

            // A yellow board of dark chevrons on the outside of every corner of 45° or more, one more on a long one, raised
            // over the barrier on two legs from the land (the reference's corners).
            private void CornerBoards(Transform parent)
            {
                double s0 = 0d;
                int index = 0;
                foreach (TrackDefinition.Segment segment in definition.Segments)
                {
                    double start = s0;
                    s0 += segment.Length;
                    if (!segment.IsArc || Mathf.Abs(segment.Degrees) < 45f) continue;
                    int count = segment.Length > 60f ? 2 : 1;
                    // The outside of a right turn (positive degrees) is the left.
                    int outside = segment.Degrees > 0f ? -1 : 1;
                    for (int k = 0; k < count; k++)
                    {
                        double s = start + segment.Length * (k + 1d) / (count + 1d);
                        TrackFrame frame = line.FrameAt(s);
                        Vector3 position = frame.TransformPoint(new Vector3(Edge(s, outside) + outside * 2.8f, 0f, 0f));
                        position.y = Ground;
                        if (!near.Available(position, 0.8f)) continue;
                        Transform board = Group("Corner chevrons " + index++, parent);
                        board.SetPositionAndRotation(position, Flat(-frame.Right * outside));
                        float y = frame.Position.y - Ground;
                        for (int leg = -1; leg <= 1; leg += 2)
                            Box("Board leg", board, new Vector3(leg * 1.5f, (y + 1.4f) * 0.5f, 0f), new Vector3(0.2f, y + 1.4f, 0.2f), black);
                        Box("Board", board, new Vector3(0f, y + 2.1f, 0f), new Vector3(3.8f, 1.4f, 0.15f), yellow);
                        // The board's +x is the way the runner goes (LookRotation toward the road from the outside).
                        float point = outside;
                        for (int c = -1; c <= 1; c++)
                        {
                            Beam("Chevron", board, new Vector3(c * 1.1f - point * 0.3f, y + 1.6f, 0.1f), new Vector3(c * 1.1f + point * 0.3f, y + 2.1f, 0.1f), 0.2f, black);
                            Beam("Chevron", board, new Vector3(c * 1.1f + point * 0.3f, y + 2.1f, 0.1f), new Vector3(c * 1.1f - point * 0.3f, y + 2.6f, 0.1f), 0.2f, black);
                        }
                        near.Reserve(position, 2f);
                    }
                }
            }

            // Colourful advertising boards just behind the ground road's barrier, along its straights (the reference's
            // start straight and first corners). Straights only: a board turned on a curve swells its world bounds toward
            // the road.
            private void Billboards(Transform parent)
            {
                int index = 0;
                for (double s = 4d; s < length; s += 7d)
                {
                    TrackFrame frame = line.FrameAt(s);
                    if (frame.Position.y > 1f || frame.Curvature != 0f) continue;
                    if (Mathf.Abs(frame.Forward.x) > 0.01f && Mathf.Abs(frame.Forward.z) > 0.01f) continue;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        index++;
                        Vector3 position = frame.TransformPoint(new Vector3(Edge(s, side) + side * 1.75f, 0f, 0f));
                        position.y = Ground;
                        if (!near.Available(position, 0.5f)) continue;
                        Transform board = Group("Advertising board " + index, parent);
                        board.SetPositionAndRotation(position, Flat(-frame.Right * side));
                        float y = frame.Position.y - Ground;
                        Material back = Pick(index * 7 + 10, red, signBlue, yellow, violet, orange, sage);
                        Material fore = Pick(index * 11 + 12, plaster, yellow, sky, pink);
                        for (int leg = -1; leg <= 1; leg += 2)
                            Box("Board leg", board, new Vector3(leg * 2.6f, (y + 1.3f) * 0.5f, -0.1f), new Vector3(0.2f, y + 1.3f, 0.2f), black);
                        Box("Board frame", board, new Vector3(0f, y + 2.8f, -0.05f), new Vector3(6.4f, 3.2f, 0.12f), ink);
                        Box("Board face", board, new Vector3(0f, y + 2.8f, 0.03f), new Vector3(6f, 2.8f, 0.06f), back);
                        GameObject disc = Shape("Mascot disc", board, new Vector3(-1.6f, y + 2.8f, 0.08f), new Vector3(2.1f, 0.06f, 2.1f), fore, Cylinder);
                        disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        Box("Headline", board, new Vector3(1.1f, y + 3.4f, 0.08f), new Vector3(2.8f, 0.5f, 0.05f), fore);
                        Box("Strapline", board, new Vector3(1.1f, y + 2.6f, 0.08f), new Vector3(2.4f, 0.3f, 0.05f), plaster);
                        Box("Strapline", board, new Vector3(0.9f, y + 2.1f, 0.08f), new Vector3(2f, 0.3f, 0.05f), plaster);
                        near.Reserve(position, 3.3f);
                    }
                }
            }

            // Lamp masts on the land just outside the road, alternating sides every 36 m, the arm and lamp over the road
            // overhead, and a long banner on the mast's far side facing along the road (the reference's banner poles).
            private void Lamps(Transform parent)
            {
                int index = 0;
                for (double s = 10d; s < length; s += 36d)
                {
                    index++;
                    int side = index % 2 == 0 ? -1 : 1;
                    TrackFrame frame = line.FrameAt(s);
                    Vector3 position = frame.TransformPoint(new Vector3(Edge(s, side) + side * 1.9f, 0f, 0f));
                    position.y = Ground;
                    if (!near.Available(position, 0.4f)) continue;
                    Transform lamp = Group("Road lamp " + index, parent);
                    lamp.SetPositionAndRotation(position, Flat(-frame.Right * side));
                    float y = frame.Position.y - Ground;
                    float arm = HighestRoad(s, 20d) - Ground + OverheadClear + 0.15f;
                    Box("Mast", lamp, new Vector3(0f, arm * 0.5f, 0f), new Vector3(0.26f, arm, 0.26f), steel);
                    Box("Arm", lamp, new Vector3(0f, arm, 1.7f), new Vector3(0.18f, 0.18f, 3.4f), steel);
                    Box("Lamp head", lamp, new Vector3(0f, arm - 0.05f, 3.4f), new Vector3(0.6f, 0.25f, 1.2f), ink);
                    Box("Lamp glow", lamp, new Vector3(0f, arm - 0.19f, 3.4f), new Vector3(0.44f, 0.04f, 1f), amber);
                    Box("Banner", lamp, new Vector3(0f, y + 4.2f, -0.75f), new Vector3(0.05f, 3f, 1.2f), Pick(index * 13 + 14, red, signBlue, yellow, violet, sage));
                    Box("Banner bar", lamp, new Vector3(0f, y + 5.75f, -0.75f), new Vector3(0.1f, 0.1f, 1.4f), steel);
                    near.Reserve(position, 1f);
                }
            }

            // User 2026-11-02: beyond the transverse guards the existing highway still runs on, Continuation metres each
            // way to the horizon ("특히 고가도로쪽"): a plain asphalt slab (the road's grain stretched over a box's 0..1 UVs),
            // its lanes painted, traffic both ways, lamps, a sign gantry and piers. Scenery only (ExteriorKit.Box has no
            // collider); the group is checked vertex by vertex. Nothing on it stands within 18 m of the guard: the
            // clearance check counts a circle of the highway's half width round its last point as road. What stands on the
            // land keeps clear of every road (near), as the piers keep off the low road.
            private void HighwayContinuation(Transform parent, Transform supports, TrackDefinition.Bay bay)
            {
                Centerline deck = bay.DeckLine(line);
                float half = definition.HalfWidthAt(bay.FromS) + bay.Width * 0.5f;
                const float Free = 18f;
                int index = 0;
                for (int side = -1; side <= 1; side += 2)
                {
                    TrackFrame frame = deck.FrameAt(side < 0 ? 0d : deck.EndS);
                    Vector3 centre = frame.Position - frame.Right * bay.Width * 0.5f;
                    Transform slab = Group(side < 0 ? "Highway east beyond guard" : "Highway west beyond guard", parent);
                    slab.SetPositionAndRotation(centre, Quaternion.LookRotation(frame.Forward));
                    float height = frame.Position.y - Ground;
                    Box("Unreachable highway slab", slab, new Vector3(0f, -definition.RoadThickness * 0.5f, side * Continuation * 0.5f),
                        new Vector3(half * 2f, definition.RoadThickness, Continuation), asphalt);
                    for (int edge = -1; edge <= 1; edge += 2)
                        Box("Barrier", slab, new Vector3(edge * (half + 0.15f), 0.55f, side * (Continuation + Free) * 0.5f), new Vector3(0.3f, 1.1f, Continuation - Free), concrete);
                    for (int yellowLine = -1; yellowLine <= 1; yellowLine += 2)
                        Box("Centre line", slab, new Vector3(yellowLine * 0.15f, 0.015f, side * (Continuation + Free) * 0.5f), new Vector3(0.12f, 0.03f, Continuation - Free), yellow);
                    for (float z = Free; z < Continuation - 3f; z += 12f)
                        for (int lane = 1; lane <= 3; lane++)
                            for (int across = -1; across <= 1; across += 2)
                                Box("Lane dash", slab, new Vector3(across * lane * 3.6f, 0.015f, side * (z + 1.5f)), new Vector3(0.15f, 0.03f, 3f), paint);
                    // Traffic: drive on the right, so the lanes right of the middle go the race's way.
                    for (float z = Free + 4f; z < Continuation - 6f; z += 9f)
                    {
                        index++;
                        if (Hash(index * 127 + 42) < 0.45f) continue;
                        int lane = 1 + (int)(Hash(index * 131 + 43) * 4f) % 4;
                        int way = Hash(index * 137 + 44) < 0.5f ? -1 : 1;
                        Transform car = Group("Highway car", slab);
                        car.localPosition = new Vector3(way * (lane - 0.5f) * 3.6f, 0f, side * z);
                        car.localRotation = Quaternion.Euler(0f, way > 0 ? 0f : 180f, 0f);
                        Car(car, index);
                    }
                    // Lamps on alternate edges; their masts, the gantry's legs and the piers stand on the land.
                    for (float z = Free + 10f; z < Continuation; z += 40f)
                    {
                        int edge = (int)(z / 40f) % 2 == 0 ? -1 : 1;
                        Vector3 mast = slab.TransformPoint(new Vector3(edge * (half + 1.9f), 0f, side * z));
                        mast.y = Ground;
                        if (!near.Available(mast, 0.4f) || !ClearOfLowRoad(mast, 0.4f, 1f)) continue;
                        Transform lamp = Group("Highway lamp", slab);
                        lamp.position = mast;
                        lamp.localRotation = Quaternion.Euler(0f, edge < 0 ? 90f : -90f, 0f);
                        float arm = height + OverheadClear + 0.15f;
                        Box("Mast", lamp, new Vector3(0f, arm * 0.5f, 0f), new Vector3(0.26f, arm, 0.26f), steel);
                        Box("Arm", lamp, new Vector3(0f, arm, 1.7f), new Vector3(0.18f, 0.18f, 3.4f), steel);
                        Box("Lamp head", lamp, new Vector3(0f, arm - 0.05f, 3.4f), new Vector3(0.6f, 0.25f, 1.2f), ink);
                        Box("Lamp glow", lamp, new Vector3(0f, arm - 0.19f, 3.4f), new Vector3(0.44f, 0.04f, 1f), amber);
                        near.Reserve(mast, 1f);
                    }
                    Vector3 legA = slab.TransformPoint(new Vector3(-half - 1.9f, 0f, side * 70f)), legB = slab.TransformPoint(new Vector3(half + 1.9f, 0f, side * 70f));
                    legA.y = legB.y = Ground;
                    if (near.Available(legA, 0.5f) && near.Available(legB, 0.5f) && ClearOfLowRoad(legA, 0.5f, 1f) && ClearOfLowRoad(legB, 0.5f, 1f))
                    {
                        Transform gantry = Group("Highway gantry", slab);
                        gantry.localPosition = new Vector3(0f, 0f, side * 70f);
                        gantry.localRotation = side < 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                        for (int leg = -1; leg <= 1; leg += 2)
                            Box("Gantry leg", gantry, new Vector3(leg * (half + 1.9f), (OverheadClear + 3.2f - height) * 0.5f, 0f), new Vector3(0.6f, OverheadClear + 3.2f + height, 0.6f), steel);
                        Box("Gantry truss", gantry, new Vector3(0f, OverheadClear + 2.9f, 0f), new Vector3(half * 2f + 4.4f, 0.5f, 0.6f), steel);
                        Sign(gantry, new Vector3(5.4f, OverheadClear + 1.6f, -0.45f), 0f);
                        Sign(gantry, new Vector3(-5.4f, OverheadClear + 1.6f, 0.45f), 180f);
                    }
                    for (float z = 16f; z < Continuation; z += 32f)
                    {
                        Vector3 position = centre + frame.Forward * side * z;
                        position.y = Ground;
                        if (!ClearOfLowRoad(position, 3f, 1f) || !near.Clear(position, 1.5f)) continue;
                        Transform pier = Group("Continuation pier", supports);
                        pier.SetPositionAndRotation(position, Quaternion.LookRotation(frame.Forward));
                        float pierHeight = frame.Position.y - 1.5f - Ground;
                        Box("Concrete pier", pier, new Vector3(0f, pierHeight * 0.5f, 0f), new Vector3(2.7f, pierHeight, 2.9f), concrete);
                        Box("Pier band", pier, new Vector3(0f, 2.2f, 0f), new Vector3(2.8f, 0.5f, 3f), yellow);
                        Box("Hammerhead cap", pier, new Vector3(0f, pierHeight - 0.45f, 0f), new Vector3(half * 2f - 0.8f, 0.9f, 3.4f), steel);
                    }
                    for (float z = 0f; z <= Continuation; z += 10f)
                        corridor.Reserve(new Vector3(centre.x, Ground, centre.z) + frame.Forward * side * z, half + 4f);
                }
            }

            // Beside every high stretch, mid-rise flats taller than the viaduct (from it the plain houses showed only roofs),
            // and along the highway big billboards on poles turned to the runners on it.
            private void HighwaySide(Transform parent)
            {
                // The billboards first: the flats would take their places.
                int index = 0;
                foreach (TrackDefinition.Bay bay in definition.Bays)
                {
                    if (!bay.StraightDeck) continue;
                    int side = 1;
                    for (double s = bay.FromS + 25d; s < bay.ToS - 10d; s += 55d)
                    {
                        side = -side;
                        TrackFrame frame = line.FrameAt(s);
                        Vector3 p = frame.TransformPoint(new Vector3(Edge(s, side) + side * (KeepOut + 6f), 0f, 0f));
                        p.y = Ground;
                        if (!corridor.Available(p, 6f)) continue;
                        HighwayBillboard(parent, p, frame, side, index++);
                        corridor.Reserve(p, 6f);
                    }
                }
                for (double s = 0d; s < length; s += 30d)
                {
                    TrackFrame f = line.FrameAt(s);
                    if (f.Position.y < 6f) continue;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        index++;
                        float width = 14f + Hash(index * 139 + 45) * 6f, depth = 12f + Hash(index * 149 + 46) * 4f;
                        float radius = new Vector2(width + 4.6f, depth + 4.6f).magnitude * 0.5f;
                        Vector3 p = f.Position + f.Right * side * (Mathf.Abs(Edge(s, side)) + KeepOut + radius + 1f);
                        p.y = Ground;
                        if (!corridor.Available(p, radius)) continue;
                        float height = f.Position.y - Ground + 6f + Hash(index * 151 + 47) * 12f;
                        Apartment(parent, p, Flat(-f.Right * side), width, depth, height, index, Mathf.Clamp((int)((height - 6f) / 3.2f), 2, 8), true);
                        corridor.Reserve(p, radius);
                    }
                }
            }

            // A 10 x 4.5 m board on two poles, its top over the viaduct, turned toward runners coming along the highway.
            private void HighwayBillboard(Transform parent, Vector3 position, TrackFrame frame, int side, int index)
            {
                Transform board = Group("Highway billboard " + index, parent);
                Vector3 toRunners = -frame.Forward - frame.Right * side * 0.6f;
                board.SetPositionAndRotation(position, Flat(toRunners));
                float bottom = frame.Position.y - Ground + 3.5f;
                for (int pole = -1; pole <= 1; pole += 2)
                    Box("Billboard pole", board, new Vector3(pole * 3f, bottom * 0.5f, -0.3f), new Vector3(0.45f, bottom, 0.45f), steel);
                Box("Billboard frame", board, new Vector3(0f, bottom + 2.4f, -0.1f), new Vector3(10.4f, 4.9f, 0.3f), ink);
                Box("Billboard face", board, new Vector3(0f, bottom + 2.4f, 0.08f), new Vector3(10f, 4.5f, 0.06f), Pick(index * 157 + 48, red, signBlue, yellow, violet, orange, sage));
                Material fore = Pick(index * 163 + 49, plaster, yellow, sky, pink);
                GameObject disc = Shape("Mascot disc", board, new Vector3(-2.8f, bottom + 2.4f, 0.13f), new Vector3(3.6f, 0.06f, 3.6f), fore, Cylinder);
                disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Box("Headline", board, new Vector3(1.8f, bottom + 3.4f, 0.13f), new Vector3(5f, 0.9f, 0.05f), fore);
                Box("Strapline", board, new Vector3(1.8f, bottom + 2.1f, 0.13f), new Vector3(4.4f, 0.45f, 0.05f), plaster);
                Box("Strapline", board, new Vector3(1.4f, bottom + 1.3f, 0.13f), new Vector3(3.6f, 0.45f, 0.05f), plaster);
                Box("Catwalk", board, new Vector3(0f, bottom - 0.1f, 0.5f), new Vector3(10f, 0.12f, 0.9f), steel);
            }

            // A plaster block of flats: walls, bands of glass round all four sides, a cornice; near the road a dark shop
            // floor with a canopy and roof plant too.
            private void Apartment(Transform parent, Vector3 position, Quaternion facing, float width, float depth, float height, int index, int bands, bool shops)
            {
                Transform block = Group("Apartment block " + index, parent);
                block.SetPositionAndRotation(position, facing);
                Material walls = Pick(index * 167 + 50, cream, pink, apricot, sky, plaster, sage);
                Box("Walls", block, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), walls);
                if (shops)
                {
                    Box("Shop floor", block, new Vector3(0f, 2f, 0f), new Vector3(width + 0.3f, 4f, depth + 0.3f), ink);
                    Box("Shop canopy", block, new Vector3(0f, 4.1f, depth * 0.5f + 0.9f), new Vector3(width * 0.9f, 0.25f, 1.8f), Pick(index * 173 + 51, red, signBlue, sage, orange));
                }
                for (int b = 0; b < bands; b++)
                {
                    float y = 6f + (height - 7.5f) * (b + 0.5f) / bands;
                    Box("Window band", block, new Vector3(0f, y, 0f), new Vector3(width + 0.08f, 1.3f, depth * 0.82f), glass);
                    Box("Window band", block, new Vector3(0f, y, 0f), new Vector3(width * 0.82f, 1.3f, depth + 0.08f), glass);
                }
                Box("Cornice", block, new Vector3(0f, height + 0.25f, 0f), new Vector3(width + 0.6f, 0.5f, depth + 0.6f), plaster);
                if (shops) Box("Roof plant", block, new Vector3(width * 0.2f, height + 1.1f, -depth * 0.15f), new Vector3(3f, 1.6f, 2.4f), steel);
            }

            // The rest of the land filled with blocks of flats on a 44 m grid (a few lots left as parks), rising with the
            // distance from the circuit to a city edge. Plain blocks with two window bands: there are hundreds.
            private void Outskirts(Transform parent)
            {
                Vector3 middle = Vector3.zero;
                int samples = 0;
                for (double s = 0d; s < length; s += 10d)
                {
                    middle += line.FrameAt(s).Position;
                    samples++;
                }
                middle /= samples;
                int index = 0;
                for (float x = -85f - FoundationX * 0.5f + 30f; x < -85f + FoundationX * 0.5f - 20f; x += 44f)
                    for (float z = -55f - FoundationZ * 0.5f + 30f; z < -55f + FoundationZ * 0.5f - 20f; z += 44f)
                    {
                        index++;
                        if (Hash(index * 179 + 52) < 0.15f) continue;
                        Vector3 p = new Vector3(x + (Hash(index * 181 + 53) - 0.5f) * 8f, Ground, z + (Hash(index * 191 + 54) - 0.5f) * 8f);
                        float width = 16f + Hash(index * 193 + 55) * 10f, depth = 14f + Hash(index * 197 + 56) * 10f;
                        float radius = new Vector2(width, depth).magnitude * 0.5f;
                        if (!corridor.Available(p, radius)) continue;
                        float far = new Vector2(p.x - middle.x, p.z - middle.z).magnitude;
                        float height = 10f + Mathf.Clamp01((far - 200f) / 450f) * 50f + Hash(index * 199 + 57) * 16f;
                        Apartment(parent, p, Quaternion.Euler(0f, index % 4 * 90f, 0f), width, depth, height, index + 5000, 2, false);
                        corridor.Reserve(p, radius);
                    }
            }

            // Behind a bay's end guard, facing the runners in it: a yellow board of dark chevrons pointing the way the race
            // goes on (user 2026-11-02, the on-ramp's blocked straight). It stands past the guard and clear of the road on:
            // past it by more than the bay's half width, which the clearance check counts as road round the bay's last point.
            private void BayEndBoard(Transform parent, TrackDefinition.Bay bay)
            {
                TrackFrame frame = line.FrameAt(bay.ToS);
                float side = bay.Left ? -1f : 1f;
                float main = definition.HalfWidthAt(bay.ToS);
                float inner = main + 1.6f, outer = main + bay.Width;
                Transform board = Group("Bay end board", parent);
                board.SetPositionAndRotation(frame.Position + frame.Right * side * (inner + outer) * 0.5f + frame.Forward * (bay.Width * 0.5f + 0.8f), Quaternion.LookRotation(-frame.Forward));
                float width = outer - inner;
                for (int post = -1; post <= 1; post += 2)
                    Box("Board post", board, new Vector3(post * (width * 0.5f - 0.3f), (2.6f - (Ground - frame.Position.y)) * 0.5f + (Ground - frame.Position.y), -0.1f),
                        new Vector3(0.2f, 2.6f - (Ground - frame.Position.y), 0.2f), ink);
                Box("Board", board, new Vector3(0f, 2f, 0f), new Vector3(width, 1.4f, 0.12f), amber);
                // Seen from the bay the board's +x is the runner's left: the chevrons point to -x when the race goes on to the
                // bay's right (a left bay), as the ramp does.
                float point = bay.Left ? -1f : 1f;
                int count = Mathf.Max(2, (int)(width / 1.6f));
                for (int c = 0; c < count; c++)
                {
                    float x = -width * 0.5f + (c + 0.5f) * width / count;
                    Beam("Chevron", board, new Vector3(x - point * 0.3f, 1.55f, 0.08f), new Vector3(x + point * 0.3f, 2f, 0.08f), 0.18f, ink);
                    Beam("Chevron", board, new Vector3(x + point * 0.3f, 2f, 0.08f), new Vector3(x - point * 0.3f, 2.45f, 0.08f), 0.18f, ink);
                }
                near.Reserve(new Vector3(board.position.x, Ground, board.position.z), width * 0.5f + 1f);
            }

            // A two- or three-storey plaster house: a tiled or slate pitched roof or a flat one with a parapet, shuttered
            // windows with flower boxes, a door, and a striped awning or a balcony on some (as PALM COAST's town houses).
            private void TownHouse(Transform parent, Vector3 position, Quaternion facing, float width, float depth, float height, int index)
            {
                Transform house = Group("Village building " + index, parent);
                house.SetPositionAndRotation(position, facing);
                Material walls = Pick(index * 3 + 21, cream, pink, apricot, sky, plaster, sage);
                Material shutters = Pick(index * 5 + 22, signBlue, sage, plaster, tile, wood);
                Box("Plaster walls", house, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), walls);
                Box("Plinth", house, new Vector3(0f, 0.4f, 0f), new Vector3(width + 0.2f, 0.8f, depth + 0.2f), concrete);
                if (Hash(index * 7 + 23) > 0.3f)
                {
                    Material roofing = Hash(index * 9 + 24) > 0.35f ? tile : slate;
                    float rise = width * 0.24f;
                    float slope = Mathf.Atan2(rise, width * 0.5f) * Mathf.Rad2Deg;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        GameObject panel = Box("Pitched roof", house, new Vector3(side * width * 0.25f, height + rise * 0.5f, 0f),
                            new Vector3(new Vector2(width * 0.5f, rise).magnitude + 0.5f, 0.3f, depth + 0.7f), roofing);
                        panel.transform.localRotation = Quaternion.Euler(0f, 0f, -side * slope);
                    }
                    for (int tier = 0; tier < 3; tier++)
                        Box("Gable", house, new Vector3(0f, height + rise * (tier + 0.5f) / 3f, 0f), new Vector3(width * (1f - (tier + 0.5f) / 3f) * 0.95f, rise / 3f, depth * 0.96f), walls);
                    if (Hash(index * 19 + 25) > 0.5f)
                        Box("Chimney", house, new Vector3(width * 0.28f, height + rise * 0.9f, depth * 0.2f), new Vector3(0.8f, rise * 1.2f, 0.8f), tile);
                }
                else
                {
                    Box("Flat roof", house, new Vector3(0f, height + 0.2f, 0f), new Vector3(width + 0.3f, 0.4f, depth + 0.3f), concrete);
                    Box("Parapet", house, new Vector3(0f, height + 0.7f, depth * 0.5f), new Vector3(width + 0.3f, 0.6f, 0.25f), walls);
                }
                int storeys = height > 10f ? 3 : 2;
                float front = depth * 0.5f + 0.06f;
                for (int floor = 1; floor < storeys; floor++)
                    for (int col = -1; col <= 1; col++)
                    {
                        float y = floor * height / storeys + 1.1f;
                        Box("Window", house, new Vector3(col * width * 0.3f, y, front), new Vector3(1.2f, 1.5f, 0.12f), glass);
                        for (int side = -1; side <= 1; side += 2)
                            Box("Shutter", house, new Vector3(col * width * 0.3f + side * 0.85f, y, front + 0.04f), new Vector3(0.42f, 1.6f, 0.08f), shutters);
                        if (Hash(index * 37 + col + floor * 3) > 0.5f)
                            Box("Flower box", house, new Vector3(col * width * 0.3f, y - 0.9f, front + 0.22f), new Vector3(1.3f, 0.3f, 0.36f), Pick(index * 41 + col, red, pink, yellow, leaf));
                    }
                Box("Door", house, new Vector3(-width * 0.22f, 1.3f, front), new Vector3(1.3f, 2.4f, 0.12f), Pick(index * 43 + 26, wood, signBlue, red));
                Box("Shop window", house, new Vector3(width * 0.18f, 1.6f, front), new Vector3(2.4f, 1.5f, 0.12f), glass);
                float extra = Hash(index * 47 + 27);
                if (extra < 0.45f)
                {
                    Material stripe = Pick(index * 53 + 28, red, signBlue, sage, orange, violet);
                    for (int s = 0; s < 5; s++)
                        Box("Striped awning", house, new Vector3((s - 2) * width * 0.16f, 3.1f, depth * 0.5f + 0.9f), new Vector3(width * 0.16f, 0.22f, 1.8f), s % 2 == 0 ? stripe : plaster);
                }
                else if (extra < 0.75f)
                {
                    Box("Balcony slab", house, new Vector3(0f, height / storeys + 0.2f, front + 0.55f), new Vector3(width * 0.5f, 0.2f, 1.1f), concrete);
                    Box("Balcony rail", house, new Vector3(0f, height / storeys + 0.75f, front + 1.05f), new Vector3(width * 0.5f, 0.9f, 0.08f), plaster);
                }
            }

            // A round tree, a tall cypress or a cluster of bushes, by the place's hash.
            private void Tree(Transform parent, Vector3 position, int index)
            {
                Transform tree = Group("Planter tree", parent);
                tree.position = position;
                float kind = Hash(index * 59 + 29);
                float size = 0.85f + Hash(index * 61 + 30) * 0.35f;
                if (kind < 0.5f)
                {
                    Box("Planter", tree, new Vector3(0f, 0.25f, 0f), new Vector3(2.2f, 0.5f, 2.2f), concrete);
                    Box("Trunk", tree, new Vector3(0f, 1.8f * size, 0f), new Vector3(0.4f, 3.6f * size, 0.4f), trunk);
                    Shape("Crown", tree, new Vector3(0f, 4.6f * size, 0f), new Vector3(4.6f, 4.2f, 4.6f) * size, Pick(index * 67 + 31, leaf, leafDark), Sphere);
                    Shape("Crown top", tree, new Vector3(0.6f, 6f * size, 0.4f), new Vector3(2.8f, 2.6f, 2.8f) * size, leaf, Sphere);
                }
                else if (kind < 0.8f)
                {
                    Box("Trunk", tree, new Vector3(0f, 0.6f, 0f), new Vector3(0.35f, 1.2f, 0.35f), trunk);
                    Shape("Cypress", tree, new Vector3(0f, 4.2f * size, 0f), new Vector3(2.4f, 7.2f, 2.4f) * size, leafDark, Sphere);
                }
                else
                {
                    for (int b = 0; b < 3; b++)
                    {
                        Vector3 at = Quaternion.Euler(0f, b * 120f + index * 37f, 0f) * new Vector3(1.1f, 0f, 0f);
                        Shape("Bush", tree, at + Vector3.up * 0.8f, new Vector3(2f, 1.6f, 2f) * size, Pick(index * 71 + b, leaf, leafDark), Sphere);
                    }
                }
            }

            // A plaster apartment tower: a dark podium of shops, a glass band on every floor round all four sides, a
            // cornice and a rooftop water tank or sign.
            private void Tower(Transform parent, Vector3 position, float width, float height, int index)
            {
                if (!corridor.Clear(position, width * 0.75f)) throw new InvalidOperationException("Skyline tower overlaps the reserved track corridor.");
                Transform tower = Group("Skyline tower " + index, parent);
                tower.position = position;
                Material walls = Pick(index * 73 + 32, cream, pink, apricot, sky, plaster, sage);
                Box("Podium", tower, new Vector3(0f, 2f, 0f), new Vector3(width + 4f, 4f, width + 4f), ink);
                Box("Podium canopy", tower, new Vector3(0f, 4.15f, 0f), new Vector3(width + 4.6f, 0.3f, width + 4.6f), Pick(index * 79 + 33, red, signBlue, sage, orange));
                Box("Tower", tower, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, width), walls);
                for (float y = 6f; y < height - 1.5f; y += 3.2f)
                {
                    Box("Window band", tower, new Vector3(0f, y, 0f), new Vector3(width + 0.08f, 1.3f, width * 0.84f), glass);
                    Box("Window band", tower, new Vector3(0f, y, 0f), new Vector3(width * 0.84f, 1.3f, width + 0.08f), glass);
                }
                Box("Cornice", tower, new Vector3(0f, height + 0.3f, 0f), new Vector3(width + 0.8f, 0.6f, width + 0.8f), plaster);
                if (index % 2 == 0)
                {
                    Shape("Water tank", tower, new Vector3(width * 0.2f, height + 2f, width * 0.15f), new Vector3(3f, 2.8f, 3f), wood, Cylinder);
                    for (int leg = 0; leg < 4; leg++)
                        Box("Tank leg", tower, new Vector3(width * 0.2f + (leg % 2 * 2 - 1) * 1f, height + 0.6f, width * 0.15f + (leg / 2 * 2 - 1) * 1f), new Vector3(0.15f, 1.2f, 0.15f), steel);
                }
                else Box("Roof sign", tower, new Vector3(0f, height + 1.6f, -width * 0.3f), new Vector3(width * 0.7f, 2f, 0.2f), Pick(index * 83 + 34, red, yellow, signBlue));
                corridor.Reserve(position, width * 0.75f + 2f);
            }

            private void Landmark(Transform parent, Vector3 position, Quaternion rotation, string title, string subtitle)
            {
                if (!corridor.Clear(position, 8.5f)) throw new InvalidOperationException("Landmark overlaps the reserved track corridor.");
                Transform sign = Group(title + " landmark - " + subtitle, parent);
                sign.SetPositionAndRotation(position, rotation);
                Box("Plinth", sign, new Vector3(0f, 0.35f, 0f), new Vector3(16f, 0.7f, 4.5f), concrete);
                Box("Left support", sign, new Vector3(-6.8f, 6.5f, 0f), new Vector3(0.55f, 13f, 0.55f), steel);
                Box("Right support", sign, new Vector3(6.8f, 6.5f, 0f), new Vector3(0.55f, 13f, 0.55f), steel);
                Box("Sign panel", sign, new Vector3(0f, 11f, 0f), new Vector3(15f, 5.5f, 0.65f), ink);
                Box("Amber header", sign, new Vector3(0f, 13.65f, -0.36f), new Vector3(15f, 0.16f, 0.08f), amber);
                Lettering(sign, mint, amber);
                corridor.Reserve(position, 10f);
            }

            internal static void Lettering(Transform parent, Material mint, Material amber)
            {
                Transform letters = Group("SKYWAY lettering", parent);
                // Solid geometry is occluded by the sign board from behind; a dynamic GUI font was visible through it.
                float[][] glyphs =
                {
                    new[] { 1f,1f,0f,1f, 0f,1f,0f,0.5f, 0f,0.5f,1f,0.5f, 1f,0.5f,1f,0f, 1f,0f,0f,0f },
                    new[] { 0f,0f,0f,1f, 0f,0.5f,1f,1f, 0f,0.5f,1f,0f },
                    new[] { 0f,1f,0.5f,0.5f, 1f,1f,0.5f,0.5f, 0.5f,0.5f,0.5f,0f },
                    new[] { 0f,1f,0.2f,0f, 0.2f,0f,0.5f,0.5f, 0.5f,0.5f,0.8f,0f, 0.8f,0f,1f,1f },
                    new[] { 0f,0f,0.5f,1f, 0.5f,1f,1f,0f, 0.2f,0.4f,0.8f,0.4f },
                    new[] { 0f,1f,0.5f,0.5f, 1f,1f,0.5f,0.5f, 0.5f,0.5f,0.5f,0f },
                };
                UnityEngine.Mesh mesh = Cube;
                // Looking at the front (-Z) reverses local X, so the word runs from +X to -X.
                for (int glyph = 0; glyph < glyphs.Length; glyph++)
                {
                    float[] strokes = glyphs[glyph];
                    for (int i = 0; i < strokes.Length; i += 4)
                    {
                        Vector2 a = new Vector2(5.45f - glyph * 1.9f - strokes[i] * 1.25f, 10.65f + strokes[i + 1] * 1.85f);
                        Vector2 b = new Vector2(5.45f - glyph * 1.9f - strokes[i + 2] * 1.25f, 10.65f + strokes[i + 3] * 1.85f);
                        Vector2 center = (a + b) * 0.5f;
                        Vector2 delta = b - a;
                        GameObject stroke = Shape("Letter stroke", letters, new Vector3(center.x, center.y, -0.41f), new Vector3(delta.magnitude + 0.12f, 0.14f, 0.12f), mint, mesh);
                        stroke.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                    }
                }
                Shape("District underline", letters, new Vector3(0f, 9.7f, -0.41f), new Vector3(10.7f, 0.1f, 0.1f), amber, mesh);
            }
        }
    }
}
