using System;
using System.Collections.Generic;
using System.IO;
using ProtoHarness.ChainRush.Track;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ProtoHarness.Editor.ChainRush.ExteriorKit;

namespace ProtoHarness.Editor.ChainRush
{
    // Like SkywayEnvironmentBuilder: persistent decoration, no road, physics or runtime behaviour. It builds and saves only
    // the prefab; saving the decoration scene, the build's scene list and the lobby entry are left to the user (DECISIONS
    // 2026-10-27 후속 2: a save-register-connect method here could only be run through Unity_RunCommand, so it went).
    // Its own road, wall and lane-marking materials are a TrackLook (user 2026-11-02, "해변 서킷처럼 … 서킷 도로와 벽에
    // 대한 디자인"), read from a record video of the original's village: slate-blue asphalt, the low blue walls along
    // the straights, white lines.
    public static class FingerLanesEnvironmentBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/FingerLanesExterior.prefab";
        public const string LookPath = "Assets/_Project/Data/TrackLook_FingerLanes.asset";
        public const string ScenePath = "Assets/_Project/Scenes/ChainRushTrackFingerLanes.unity";
        private const string DefinitionPath = "Assets/_Project/Data/Circuit_FingerLanes.asset";
        private const float Ground = -1.3f;

        [MenuItem("ProtoHarness/Chain Rush/Create Finger Lanes Exterior")]
        public static void CreateExterior()
        {
            ExteriorAssets.RequireIdle();
            if (File.Exists(PrefabPath) || File.Exists(ScenePath))
                throw new InvalidOperationException("Finger Lanes exterior already exists; use Rebuild Finger Lanes Exterior.");
            BuildLook();
            BuildPrefab();
            GameObject instance = ExteriorAssets.OpenDecorationScene(PrefabPath, "ChainRushTrackFingerLanes");
            Debug.Log($"Finger Lanes exterior created. Save the decoration scene as {ScenePath}, register it in Build Profiles, and give the lobby's FINGER LANES entry the scene name and {LookPath}.", instance);
        }

        [MenuItem("ProtoHarness/Chain Rush/Rebuild Finger Lanes Exterior")]
        public static void RebuildExterior()
        {
            ExteriorAssets.RequireIdle();
            if (!File.Exists(PrefabPath)) throw new InvalidOperationException("Create Finger Lanes Exterior first.");
            BuildLook();
            BuildPrefab();
        }

        private static void BuildPrefab()
        {
            var layout = new Layout(ExteriorAssets.RequireDefinition(DefinitionPath));
            ExteriorAssets.SavePrefab(PrefabPath, layout.Build, layout.ValidateClearance);
        }

        // No glow: the first build asked for 0.22 but the saved materials never kept the emission keyword, so they have
        // always drawn without it (checked 2026-11-02, M_Finger*.mat m_ValidKeywords: []).
        private static Material Palette(string name, Color color) => ExteriorAssets.Palette("Finger", name, color);

        // Slate asphalt and the low blue walls (user 2026-11-02, "아스팔트 기반 실제 도로 느낌"); the reference's double
        // yellow middle line with a dashed lane either side.
        private static void BuildLook()
            => ExteriorAssets.BuildLook(LookPath,
                ExteriorAssets.Surface("Finger", "Road", new Color(0.29f, 0.31f, 0.36f), ExteriorAssets.Grain.Asphalt, 4f, 0.12f),
                ExteriorAssets.Surface("Finger", "Wall", new Color(0.30f, 0.50f, 0.74f), ExteriorAssets.Grain.Concrete, 3f, 0.08f),
                ExteriorAssets.Palette("Finger", "Marking", new Color(0.93f, 0.93f, 0.90f), 0.05f),
                ExteriorAssets.Palette("Finger", "Accent", new Color(0.96f, 0.78f, 0.18f), 0.05f),
                new ExteriorAssets.Lanes(3.6f, 0f, true, false));

        private static Material Require(string name) => ExteriorAssets.Require<Material>(ExteriorAssets.MaterialFolder + name);

        private sealed class Layout
        {
            private readonly TrackDefinition definition;
            private readonly Centerline line;
            private readonly RoadCorridor corridor;
            private readonly Material cream = Palette("Cream", new Color(0.94f, 0.83f, 0.61f));
            private readonly Material coral = Palette("Coral", new Color(0.68f, 0.24f, 0.17f));
            private readonly Material blue = Palette("Blue", new Color(0.24f, 0.49f, 0.65f));
            private readonly Material sage = Palette("Sage", new Color(0.37f, 0.57f, 0.32f));
            private readonly Material stone = Palette("Stone", new Color(0.71f, 0.69f, 0.59f));
            private readonly Material water = Palette("Water", new Color(0.15f, 0.49f, 0.59f));
            private readonly Material white = Require("M_StadiumSportsWhite.mat");
            private readonly Material dark = Require("M_ArtStadiumInk.mat");
            private readonly Material gold = Require("M_ArtStadiumAmber.mat");
            private readonly Material grass = Require("M_StadiumSportsTurfLight.mat");

            internal Layout(TrackDefinition definition)
            {
                this.definition = definition;
                line = definition.BuildCenterline(Vector3.zero, 0f);
                corridor = new RoadCorridor(definition, line, 0.5d, 1.5f);
            }

            // Everything above the land is checked, the landscape's hills too (DECISIONS 2026-10-27 후속 1); the tower's
            // overpass is above the camera and the runner.
            internal void ValidateClearance(GameObject root) => corridor.ValidateClearance(root, Ground + 0.09f);

            internal GameObject Build(Scene scene)
            {
                var root = new GameObject("Finger Lanes Exterior");
                SceneManager.MoveGameObjectToScene(root, scene);
                Landscape(Group("01 Landscape below road", root.transform));
                Landmarks(Group("02 Clock tower and stone bridge", root.transform));
                corridor.Reserve(new Vector3(-20f, Ground, 14f), 9f);
                Village(Group("03 Village houses and shops", root.transform));
                Gardens(Group("04 Pocket gardens", root.transform));
                StreetFurniture(Group("05 Lamps and shortcut flags", root.transform));
                StartSquare(Group("06 Start square", root.transform));
                return root;
            }

            private void Landscape(Transform parent)
            {
                // Four slabs leave an actual opening for the canal instead of hiding water under a solid ground plane.
                Box("Village green west", parent, new Vector3(85f, Ground - 0.55f, 0f), new Vector3(410f, 1f, 390f), grass);
                Box("Village green east", parent, new Vector3(440f, Ground - 0.55f, 0f), new Vector3(60f, 1f, 390f), grass);
                Box("Village green north bank", parent, new Vector3(350f, Ground - 0.55f, 99.5f), new Vector3(120f, 1f, 191f), grass);
                Box("Village green south bank", parent, new Vector3(350f, Ground - 0.55f, -106.5f), new Vector3(120f, 1f, 177f), grass);
                // Water stays below the existing road slab; no new driving surface is introduced.
                Box("Canal water", parent, new Vector3(350f, -5.2f, -7f), new Vector3(120f, 0.15f, 19f), water);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Canal stone bank", parent, new Vector3(350f, -3.5f, -7f + side * 11f), new Vector3(120f, 4.2f, 2f), stone);
                    Box("Canal end retaining wall", parent, new Vector3(350f + side * 59.5f, -3.5f, -7f), new Vector3(1f, 4.2f, 22f), stone);
                }
                for (int i = 0; i < 9; i++)
                {
                    float x = -92f + i * 67f;
                    Shape("Distant rounded hill", parent, new Vector3(x, -10f, 158f + i % 2 * 25f), new Vector3(100f, 42f + i % 3 * 9f, 65f), sage, Sphere);
                    Shape("Distant rounded hill", parent, new Vector3(x, -13f, -172f - i % 2 * 18f), new Vector3(95f, 40f + i % 3 * 8f, 60f), sage, Sphere);
                }
            }

            private void Landmarks(Transform parent)
            {
                // 35 pieces since the left edge's S bend (2026-10-28); the tower's passage is still piece 23, the bridge's bump 15.
                if (definition.Segments.Count != 35 || definition.Shortcuts.Count != 2)
                    throw new InvalidOperationException("Finger Lanes layout changed; review landmark and shortcut placement before rebuilding.");
                // The pieces are found by number, so check they still are what stands on them (DECISIONS 2026-10-28 후속 ①):
                // the tower's narrow passage, and the bridge bump's climb with the kicker after it.
                TrackDefinition.Segment passage = definition.Segments[23];
                if (passage.IsArc || !(passage.EndHalfWidth > 0f && passage.EndHalfWidth < definition.RoadHalfWidth))
                    throw new InvalidOperationException("Finger Lanes piece 23 is no longer the clock tower's narrow passage; move the tower.");
                if (definition.Segments[15].IsArc || !(definition.Segments[15].EndGrade > 0f) || !(definition.Segments[16].Launch > 0f))
                    throw new InvalidOperationException("Finger Lanes pieces 15-16 are no longer the bridge bump's climb and kicker; move the bridge.");
                TrackFrame clock = line.FrameAt(StartOf(23) + definition.Segments[23].Length * 0.5d);
                Transform tower = Group("Clock tower passage", parent);
                tower.SetPositionAndRotation(new Vector3(clock.Position.x, Ground, clock.Position.z), Quaternion.LookRotation(clock.Forward));
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Tower stone foot", tower, new Vector3(side * 8.7f, 0.7f, 0f), new Vector3(5f, 1.4f, 22f), stone);
                    Box("Tower passage pier", tower, new Vector3(side * 8.7f, 6.8f, 0f), new Vector3(4.4f, 12.2f, 20f), cream);
                    Box("Pier blue inset", tower, new Vector3(side * 8.7f, 6.4f, -10.06f), new Vector3(2.8f, 7f, 0.12f), blue);
                    Box("Pier capital", tower, new Vector3(side * 8.7f, 12.8f, 0f), new Vector3(5.2f, 0.7f, 21.5f), white);
                }
                Box("Clock tower upper hall", tower, new Vector3(0f, 19.4f, 0f), new Vector3(22f, 13f, 20f), cream);
                Box("Passage stone lintel", tower, new Vector3(0f, 13f, 0f), new Vector3(23.5f, 1.3f, 22f), stone);
                Box("Clock tower cornice", tower, new Vector3(0f, 26f, 0f), new Vector3(24f, 0.8f, 22f), white);
                Roof(tower, 24f, 23f, 26.6f, coral);
                Box("Bell turret", tower, new Vector3(0f, 32.2f, 0f), new Vector3(4.3f, 5f, 4.3f), cream);
                Roof(tower, 6f, 6f, 34.8f, blue);
                Box("Turret finial", tower, new Vector3(0f, 38f, 0f), new Vector3(0.25f, 3f, 0.25f), gold);
                for (int side = -1; side <= 1; side += 2) ClockFace(tower, side);
                corridor.Reserve(tower.position, 21f);

                TrackFrame bridgeFrame = line.FrameAt(StartOf(15) + 5d);
                Transform bridge = Group("Stone bridge over canal", parent);
                bridge.SetPositionAndRotation(new Vector3(bridgeFrame.Position.x, 0f, bridgeFrame.Position.z), Quaternion.LookRotation(bridgeFrame.Forward));
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int j = 0; j <= 10; j++)
                    {
                        float t = Mathf.PI * j / 10f;
                        var p = new Vector3(side * 8.8f, -5.7f + Mathf.Sin(t) * 3.6f, Mathf.Cos(t) * 12f);
                        GameObject block = Box("Arch voussoir", bridge, p, new Vector3(2.3f, 1.2f, 3.5f), stone);
                        block.transform.localRotation = Quaternion.Euler(Mathf.Cos(t) * 28f, 0f, 0f);
                    }
                    for (int j = -1; j <= 1; j += 2)
                        Box("Bridge abutment", bridge, new Vector3(side * 8.8f, -3.9f, j * 14f), new Vector3(3.2f, 5f, 5f), stone);
                    for (int j = -2; j <= 2; j++)
                    {
                        Box("Bridge parapet post", bridge, new Vector3(side * 8.9f, 0.35f, j * 6f), new Vector3(1f, 2.2f, 1f), stone);
                        Box("Bridge parapet cap", bridge, new Vector3(side * 8.9f, 1.55f, j * 6f), new Vector3(1.25f, 0.25f, 1.25f), white);
                    }
                    Box("Bridge parapet rail", bridge, new Vector3(side * 8.9f, 0.8f, 0f), new Vector3(0.5f, 0.6f, 25f), cream);
                }
                corridor.Reserve(bridge.position, 22f);
            }

            private void ClockFace(Transform tower, int side)
            {
                Transform face = Group("Clock face " + side, tower);
                face.localPosition = new Vector3(0f, 20.8f, side * 10.18f);
                face.localRotation = Quaternion.Euler(0f, side < 0 ? 0f : 180f, 0f);
                GameObject rim = Shape("Clock gold rim", face, Vector3.zero, new Vector3(8.4f, 0.22f, 8.4f), gold, Cylinder);
                rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                GameObject dial = Shape("Clock ivory dial", face, new Vector3(0f, 0f, -0.27f), new Vector3(7.7f, 0.09f, 7.7f), white, Cylinder);
                dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                for (int hour = 0; hour < 12; hour++)
                {
                    float angle = hour * Mathf.PI / 6f;
                    GameObject mark = Box("Hour marker", face, new Vector3(Mathf.Sin(angle) * 3.15f, Mathf.Cos(angle) * 3.15f, -0.4f), new Vector3(0.17f, hour % 3 == 0 ? 0.65f : 0.35f, 0.12f), dark);
                    mark.transform.localRotation = Quaternion.Euler(0f, 0f, -hour * 30f);
                }
                Beam("Minute hand", face, new Vector3(0f, 0f, -0.52f), new Vector3(-1.1f, 2.2f, -0.52f), 0.2f, dark);
                Beam("Hour hand", face, new Vector3(0f, 0f, -0.56f), new Vector3(1.6f, 0.8f, -0.56f), 0.26f, dark);
                Shape("Clock hub", face, new Vector3(0f, 0f, -0.65f), Vector3.one * 0.45f, gold, Sphere);
            }

            private void Village(Transform parent)
            {
                int index = 0;
                for (double s = 12d; s < definition.LapLength; s += 30d)
                {
                    TrackFrame frame = line.FrameAt(s);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float width = 8f + index % 3 * 1.5f;
                        float depth = 8f + index % 2 * 2f;
                        float radius = new Vector2(width + 2.4f, depth + 3.8f).magnitude * 0.5f;
                        Vector3 position = frame.Position + frame.Right * side * (definition.HalfWidthAt(s) + 4.5f + radius);
                        position.y = Ground;
                        if (corridor.Available(position, radius))
                        {
                            House(parent, position, Quaternion.LookRotation(-frame.Right * side), width, depth, 7f + index % 3 * 2.2f, index);
                            corridor.Reserve(position, radius + 1f);
                        }
                        index++;
                    }
                }
                // A second, quieter row makes the outside read as a village rather than isolated roadside props.
                for (int i = 0; i < 14; i++)
                {
                    Vector3 position = new Vector3(-46f + i * 33f, Ground, i % 2 == 0 ? -112f : 121f);
                    if (!corridor.Available(position, 9f)) continue;
                    House(parent, position, Quaternion.Euler(0f, i % 2 == 0 ? 0f : 180f, 0f), 11f, 10f, 10f + i % 3 * 2f, index + i);
                    corridor.Reserve(position, 10f);
                }
            }

            private void House(Transform parent, Vector3 position, Quaternion rotation, float width, float depth, float height, int index)
            {
                Transform house = Group("Village house " + index, parent);
                house.SetPositionAndRotation(position, rotation);
                Material facade = index % 4 == 0 ? blue : index % 4 == 1 ? white : cream;
                Box("Stone foundation", house, new Vector3(0f, 0.35f, 0f), new Vector3(width + 1f, 0.7f, depth + 1f), stone);
                Box("Plaster facade", house, new Vector3(0f, height * 0.5f + 0.6f, 0f), new Vector3(width, height, depth), facade);
                Box("Floor cornice", house, new Vector3(0f, height * 0.53f, 0f), new Vector3(width + 0.3f, 0.25f, depth + 0.3f), white);
                Box("Eaves", house, new Vector3(0f, height + 0.6f, 0f), new Vector3(width + 0.6f, 0.45f, depth + 0.6f), white);
                Roof(house, width + 1.6f, depth + 1.6f, height + 0.85f, index % 4 == 2 ? blue : coral);
                Box("Chimney", house, new Vector3(-width * 0.24f, height + 2.4f, -depth * 0.2f), new Vector3(1f, 3f, 1.2f), stone);
                for (int floor = 0; floor < 2; floor++)
                    for (int col = -1; col <= 1; col++)
                    {
                        float y = 2.7f + floor * (height - 2.4f) * 0.58f;
                        float z = depth * 0.5f + 0.06f;
                        Box("Ivory window frame", house, new Vector3(col * width * 0.29f, y, z), new Vector3(1.7f, 1.9f, 0.14f), white);
                        Box("Blue window glass", house, new Vector3(col * width * 0.29f, y, z + 0.1f), new Vector3(1.35f, 1.55f, 0.09f), blue);
                        Box("Window mullion", house, new Vector3(col * width * 0.29f, y, z + 0.17f), new Vector3(0.09f, 1.55f, 0.07f), white);
                    }
                Box("Shop door", house, new Vector3(0f, 1.8f, depth * 0.5f + 0.27f), new Vector3(1.6f, 2.5f, 0.2f), dark);
                if (index % 3 != 0)
                {
                    for (int stripe = 0; stripe < 5; stripe++)
                        Box("Striped shop awning", house, new Vector3((stripe - 2) * width * 0.16f, 3.7f, depth * 0.5f + 1f), new Vector3(width * 0.16f, 0.25f, 2f), stripe % 2 == 0 ? coral : cream);
                    Box("Shop sign frame", house, new Vector3(0f, 5.1f, depth * 0.5f + 0.18f), new Vector3(width * 0.7f, 0.9f, 0.25f), stone);
                    for (int dot = -1; dot <= 1; dot++)
                        Shape("Shop sign rosette", house, new Vector3(dot * 1.2f, 5.1f, depth * 0.5f + 0.4f), Vector3.one * 0.4f, gold, Sphere);
                }
            }

            private void Roof(Transform parent, float width, float depth, float bottom, Material material)
            {
                float rise = width * 0.24f;
                // Stepped gable infill uses the same persistent built-in cube mesh as the roofs and walls.
                for (int tier = 0; tier < 5; tier++)
                    Box("Gable infill", parent, new Vector3(0f, bottom + rise * (tier + 0.5f) / 5f, 0f), new Vector3(width * (1f - (tier + 0.5f) / 5f) * 0.9f, rise / 5f, depth * 0.93f), cream);
                float slope = Mathf.Atan2(rise, width * 0.5f) * Mathf.Rad2Deg;
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject panel = Box("Pitched tiled roof", parent, new Vector3(side * width * 0.25f, bottom + rise * 0.5f, 0f), new Vector3(new Vector2(width * 0.5f, rise).magnitude + 0.35f, 0.36f, depth), material);
                    panel.transform.localRotation = Quaternion.Euler(0f, 0f, -side * slope);
                }
                Box("Roof ridge", parent, new Vector3(0f, bottom + rise, 0f), new Vector3(0.45f, 0.4f, depth + 0.25f), material);
            }

            private void Gardens(Transform parent)
            {
                int garden = 0;
                for (int x = 25; x <= 325; x += 50)
                    for (int z = -14; z <= 62; z += 38)
                    {
                        Vector3 position = new Vector3(x, Ground, z);
                        if (!corridor.Available(position, 9.2f)) continue;
                        Transform island = Group("Pocket garden " + garden++, parent);
                        island.position = position;
                        Box("Garden paving", island, new Vector3(0f, 0.12f, 0f), new Vector3(12f, 0.24f, 12f), stone);
                        Box("Raised lawn", island, new Vector3(0f, 0.3f, 0f), new Vector3(10f, 0.35f, 10f), sage);
                        Tree(island, new Vector3(-2.5f, 0.5f, -2f), 5.5f);
                        Tree(island, new Vector3(2.5f, 0.5f, 2f), 4.6f);
                        corridor.Reserve(position, 9.5f);
                    }
                for (double s = 8d; s < definition.LapLength; s += 18d)
                {
                    TrackFrame frame = line.FrameAt(s);
                    int side = (int)(s / 18d) % 2 == 0 ? -1 : 1;
                    Vector3 position = frame.Position + frame.Right * side * (definition.HalfWidthAt(s) + 5.4f);
                    position.y = Ground;
                    if (!corridor.Available(position, 3.1f)) continue;
                    Tree(parent, position, 5.2f + (int)s % 3 * 0.6f);
                    corridor.Reserve(position, 3.5f);
                }
            }

            private void Tree(Transform parent, Vector3 position, float height)
            {
                Transform tree = Group("Round village tree", parent);
                tree.localPosition = position;
                Box("Tree planter", tree, new Vector3(0f, 0.25f, 0f), new Vector3(2.4f, 0.5f, 2.4f), stone);
                Box("Tree trunk", tree, new Vector3(0f, height * 0.4f, 0f), new Vector3(0.55f, height * 0.8f, 0.55f), coral);
                Shape("Rounded crown", tree, new Vector3(0f, height * 0.8f, 0f), new Vector3(4.9f, height * 0.65f, 4.9f), sage, Sphere);
            }

            private void StreetFurniture(Transform parent)
            {
                for (double s = 16d; s < definition.LapLength; s += 36d)
                {
                    TrackFrame frame = line.FrameAt(s);
                    int side = (int)(s / 36d) % 2 == 0 ? -1 : 1;
                    Vector3 position = frame.Position + frame.Right * side * (definition.HalfWidthAt(s) + 2.5f);
                    position.y = Ground;
                    if (!corridor.Available(position, 0.85f)) continue;
                    Transform lamp = Group("Village lantern S" + s.ToString("0"), parent);
                    lamp.SetPositionAndRotation(position, Quaternion.LookRotation(frame.Forward));
                    Box("Lamp foot", lamp, new Vector3(0f, 0.25f, 0f), new Vector3(0.8f, 0.5f, 0.8f), stone);
                    Box("Lamp post", lamp, new Vector3(0f, 3.2f, 0f), new Vector3(0.2f, 6.4f, 0.2f), dark);
                    Box("Lantern housing", lamp, new Vector3(0f, 6.6f, 0f), new Vector3(0.75f, 1f, 0.75f), gold);
                    Box("Lantern cap", lamp, new Vector3(0f, 7.15f, 0f), new Vector3(1f, 0.2f, 1f), dark);
                }
                for (int index = 0; index < definition.Shortcuts.Count; index++)
                {
                    TrackDefinition.Shortcut shortcut = definition.Shortcuts[index];
                    Centerline branch = TrackDefinition.ShortcutLine(line, shortcut);
                    TrackFrame middle = branch.FrameAt(shortcut.Length * 0.5d);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 position = middle.Position + middle.Right * side * (shortcut.HalfWidth + 3.3f);
                        position.y = Ground;
                        if (!corridor.Clear(position, 1.4f)) throw new InvalidOperationException("Shortcut flag overlaps a road corridor.");
                        Transform flag = Group("Shortcut " + (index + 1) + " pennant " + side, parent);
                        flag.SetPositionAndRotation(position, Quaternion.LookRotation(middle.Forward));
                        Box("Pennant stone foot", flag, new Vector3(0f, 0.4f, 0f), new Vector3(1.2f, 0.8f, 1.2f), stone);
                        Box("Pennant pole", flag, new Vector3(0f, 4.1f, 0f), new Vector3(0.18f, 8.2f, 0.18f), dark);
                        Box("Shortcut colored banner", flag, new Vector3(0.65f, 6.5f, 0f), new Vector3(1.3f, 2.5f, 0.15f), index == 0 ? coral : blue);
                        Beam("Shortcut arrow", flag, new Vector3(0.4f, 6.2f, -0.12f), new Vector3(0.95f, 6.5f, -0.12f), 0.16f, cream);
                        Beam("Shortcut arrow", flag, new Vector3(0.95f, 6.5f, -0.12f), new Vector3(0.4f, 6.8f, -0.12f), 0.16f, cream);
                    }
                }
            }

            private void StartSquare(Transform parent)
            {
                Transform sign = Group("FINGER LANES start monument", parent);
                sign.SetPositionAndRotation(new Vector3(-20f, Ground, 14f), Quaternion.Euler(0f, -90f, 0f));
                Box("Monument plinth", sign, new Vector3(0f, 0.4f, 0f), new Vector3(14f, 0.8f, 4f), stone);
                for (int side = -1; side <= 1; side += 2)
                    Box("Monument post", sign, new Vector3(side * 5.7f, 4.5f, 0f), new Vector3(0.8f, 9f, 0.8f), cream);
                Box("Blue sign panel", sign, new Vector3(0f, 7.8f, 0f), new Vector3(13f, 4.3f, 0.5f), blue);
                Box("Sign upper trim", sign, new Vector3(0f, 10f, 0f), new Vector3(13.5f, 0.35f, 0.7f), cream);
                Lettering(sign, "FINGER", 8.5f);
                Lettering(sign, "LANES", 6.7f);
                for (int i = 0; i < 5; i++)
                {
                    Vector3 p = new Vector3(-28f, Ground, -30f + i * 16f);
                    if (!corridor.Available(p, 3.4f)) continue;
                    Tree(parent, p, 6f);
                }
            }

            private void Lettering(Transform parent, string word, float y)
            {
                // Solid 3x5 glyphs, so lettering obeys depth and needs no font/texture dependencies.
                var glyphs = new Dictionary<char, string>
                {
                    { 'F', "111100110100100" }, { 'I', "111010010010111" },
                    { 'N', "101111111111101" }, { 'G', "111100101101111" },
                    { 'E', "111100110100111" }, { 'R', "110101110101101" },
                    { 'L', "100100100100111" }, { 'A', "010101111101101" },
                    { 'S', "111100111001111" }
                };
                float total = word.Length * 1.55f;
                for (int i = 0; i < word.Length; i++)
                    for (int row = 0; row < 5; row++)
                        for (int col = 0; col < 3; col++)
                            if (glyphs[word[i]][row * 3 + col] == '1')
                                Box("Solid letter " + word[i], parent, new Vector3(-total * 0.5f + i * 1.55f + col * 0.38f + 0.4f, y + (2 - row) * 0.3f, -0.33f), new Vector3(0.32f, 0.25f, 0.14f), cream);
            }

            private double StartOf(int index)
            {
                double s = 0d;
                for (int i = 0; i < index; i++) s += definition.Segments[i].Length;
                return s;
            }
        }
    }
}
