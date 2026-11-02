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

        // The detail pass (user 2026-11-02, "R4 디테일 FINGER"), read from the record video's contact sheets: right behind
        // the guards a band of sandstone walls under flower boxes, lattice fences and hedges, green lamps with banners in
        // its joints; yellow chevron boards round the outside of every corner and flower planters inside; advertising
        // boards on the straights; a chequered start gate; smaller plaster houses with orange tiles; windmills far off.
        private sealed class Layout
        {
            // The band behind the guards: its road side this far off the road's edge, this deep, in modules this long with
            // a joint between them (where the lamps stand).
            private const float BandIn = 1.1f;
            private const float BandDepth = 1.2f;
            private const float ModuleLength = 8f;
            private const float ModuleGap = 0.6f;
            // What spans the road starts this far above it (RoadCorridor's overhead exemption is 9 m).
            private const float OverheadClear = 9.4f;
            // A house's front stands this far off the road's edge, behind the band.
            private const float HouseFront = 4.6f;
            private const double GateS = 2d;

            private readonly TrackDefinition definition;
            private readonly Centerline line;
            private readonly RoadCorridor corridor;
            // What stands close by the guards (the band, lamps, corner boards and planters) keeps only this off the road;
            // houses, gardens and boards behind the band keep the wider corridor.
            private readonly RoadCorridor near;
            private readonly Material cream = Palette("Cream", new Color(0.94f, 0.83f, 0.61f));
            private readonly Material coral = Palette("Coral", new Color(0.68f, 0.24f, 0.17f));
            private readonly Material blue = Palette("Blue", new Color(0.24f, 0.49f, 0.65f));
            private readonly Material sage = Palette("Sage", new Color(0.37f, 0.57f, 0.32f));
            private readonly Material stone = Palette("Stone", new Color(0.71f, 0.69f, 0.59f));
            private readonly Material water = Palette("Water", new Color(0.15f, 0.49f, 0.59f));
            private readonly Material tile = Palette("Tile", new Color(0.78f, 0.40f, 0.20f));
            private readonly Material apricot = Palette("Apricot", new Color(0.95f, 0.76f, 0.56f));
            private readonly Material rose = Palette("Rose", new Color(0.90f, 0.64f, 0.60f));
            private readonly Material sand = Palette("Sand", new Color(0.84f, 0.70f, 0.48f));
            private readonly Material wood = Palette("Wood", new Color(0.52f, 0.34f, 0.20f));
            private readonly Material soil = Palette("Soil", new Color(0.30f, 0.21f, 0.14f));
            private readonly Material tulip = Palette("Tulip", new Color(0.84f, 0.15f, 0.15f));
            private readonly Material marigold = Palette("Marigold", new Color(0.96f, 0.56f, 0.12f));
            private readonly Material lavender = Palette("Lavender", new Color(0.57f, 0.44f, 0.80f));
            private readonly Material hedge = Palette("Hedge", new Color(0.20f, 0.40f, 0.20f));
            private readonly Material lampGreen = Palette("LampGreen", new Color(0.16f, 0.38f, 0.28f));
            private readonly Material violet = Palette("Violet", new Color(0.45f, 0.24f, 0.60f));
            private readonly Material chevron = Palette("Chevron", new Color(0.98f, 0.79f, 0.10f));
            private readonly Material white = Require("M_StadiumSportsWhite.mat");
            private readonly Material dark = Require("M_ArtStadiumInk.mat");
            private readonly Material gold = Require("M_ArtStadiumAmber.mat");
            private readonly Material grass = Require("M_StadiumSportsTurfLight.mat");

            internal Layout(TrackDefinition definition)
            {
                this.definition = definition;
                line = definition.BuildCenterline(Vector3.zero, 0f);
                corridor = new RoadCorridor(definition, line, 0.5d, 1.5f);
                near = new RoadCorridor(definition, line, 0.5d, 0.5f);
            }

            // Everything above the land is checked, the landscape's hills too (DECISIONS 2026-10-27 후속 1); the tower's
            // overpass and the start gate's beam are above the camera and the runner.
            internal void ValidateClearance(GameObject root) => corridor.ValidateClearance(root, Ground + 0.09f);

            internal GameObject Build(Scene scene)
            {
                var root = new GameObject("Finger Lanes Exterior");
                SceneManager.MoveGameObjectToScene(root, scene);
                Landscape(Group("01 Landscape below road", root.transform));
                Landmarks(Group("02 Clock tower and stone bridge", root.transform));
                StartGate(Group("06 Start gate", root.transform));
                // Trackside first, each keeping the places of the ones before; the village fills round them.
                Trackside(Group("07 Garden walls, fences and hedges", root.transform), Group("05 Lamps and banners", root.transform));
                Corners(Group("08 Corner chevrons and planters", root.transform));
                Billboards(Group("09 Advertising boards", root.transform));
                ShortcutFlags(Group("10 Shortcut flags", root.transform));
                Windmills(Group("11 Windmills", root.transform));
                Village(Group("03 Village houses and shops", root.transform));
                Gardens(Group("04 Pocket gardens", root.transform));
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
                // Paved paths down the middle of each finger and across the south, flush with the land (under every road's
                // slab, so the clearance check passes them): the tips at x 25, 125, 225, 325 close the fingers from the
                // north, those at x 75, 175, 275 from the south.
                for (int i = 0; i < 7; i++)
                {
                    float x = 25f + i * 50f;
                    float from = i % 2 == 0 ? -42f : -8f;
                    float to = i % 2 == 0 ? (i % 4 == 0 ? 39f : 67f) : 100f;
                    Box("Village path", parent, new Vector3(x, Ground + 0.03f, (from + to) * 0.5f), new Vector3(3f, 0.06f, to - from), stone);
                }
                Box("Village path", parent, new Vector3(173f, Ground + 0.03f, -42f), new Vector3(322f, 0.06f, 3f), stone);
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
                near.Reserve(tower.position, 21f);

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
                near.Reserve(bridge.position, 22f);
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

            // A chequered gate over the start (the reference's start line): two pillars off the road with banners, the beam
            // and the name board OverheadClear above it, so everything over the road clears the corridor's overhead line.
            private void StartGate(Transform parent)
            {
                TrackFrame frame = line.FrameAt(GateS);
                Transform gate = Group("FINGER LANES start gate", parent);
                gate.SetPositionAndRotation(new Vector3(frame.Position.x, Ground, frame.Position.z), Flat(frame.Forward));
                float road = frame.Position.y - Ground;
                float pillar = definition.HalfWidthAt(GateS) + 2.2f;
                float beam = road + OverheadClear;
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Pillar stone foot", gate, new Vector3(side * pillar, 0.8f, 0f), new Vector3(2f, 1.6f, 2f), stone);
                    Box("Gate pillar", gate, new Vector3(side * pillar, (beam + 3.6f) * 0.5f, 0f), new Vector3(1.4f, beam + 3.6f, 1.4f), cream);
                    Box("Pillar cap", gate, new Vector3(side * pillar, beam + 3.8f, 0f), new Vector3(1.8f, 0.4f, 1.8f), gold);
                    Box("Pillar banner", gate, new Vector3(side * pillar, road + 4.6f, -0.76f), new Vector3(1f, 4.2f, 0.08f), side < 0 ? violet : lampGreen);
                    corridor.Reserve(gate.TransformPoint(new Vector3(side * pillar, 0f, 0f)), 1.5f);
                    near.Reserve(gate.TransformPoint(new Vector3(side * pillar, 0f, 0f)), 1.5f);
                }
                float span = pillar * 2f - 1.4f;
                Box("Gate beam", gate, new Vector3(0f, beam + 0.6f, 0f), new Vector3(span, 1.2f, 1.2f), dark);
                int tiles = Mathf.CeilToInt(span / 1.2f);
                for (int t = 0; t < tiles; t += 2)
                    for (int face = -1; face <= 1; face += 2)
                        Box("Chequer", gate, new Vector3(-span * 0.5f + (t + 0.5f) * span / tiles, beam + 0.6f, face * 0.62f), new Vector3(span / tiles, 1.2f, 0.05f), white);
                Box("Name board", gate, new Vector3(0f, beam + 2.4f, 0f), new Vector3(17f, 2.4f, 0.5f), blue);
                Box("Name board trim", gate, new Vector3(0f, beam + 3.7f, 0f), new Vector3(17.4f, 0.3f, 0.7f), cream);
                Lettering(gate, "FINGER LANES", beam + 2.4f, 0.85f);
            }

            private void Lettering(Transform parent, string word, float y, float scale)
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
                float pitch = 1.55f * scale;
                float total = word.Length * pitch;
                for (int i = 0; i < word.Length; i++)
                {
                    if (word[i] == ' ') continue;
                    for (int row = 0; row < 5; row++)
                        for (int col = 0; col < 3; col++)
                            if (glyphs[word[i]][row * 3 + col] == '1')
                                Box("Solid letter " + word[i], parent, new Vector3(-total * 0.5f + i * pitch + (col * 0.38f + 0.4f) * scale, y + (2 - row) * 0.3f * scale, -0.33f), new Vector3(0.32f * scale, 0.25f * scale, 0.14f), cream);
                }
            }

            // Along every straight, both sides, a band of modules right behind the guard, each by its hash a sandstone
            // wall under a flower box, a lattice fence before lavender, or a clipped hedge; a module that would come near
            // another road (a shortcut's mouth) or a landmark is left out. A lamp stands in every fourth joint.
            private void Trackside(Transform walls, Transform lamps)
            {
                int module = 0, joint = 0, lamp = 0;
                double s0 = 0d;
                foreach (TrackDefinition.Segment segment in definition.Segments)
                {
                    double start = s0;
                    s0 += segment.Length;
                    if (segment.IsArc || segment.Length < 12f) continue;
                    int count = Mathf.Max(1, Mathf.RoundToInt((segment.Length - 2f) / ModuleLength));
                    double pitch = (segment.Length - 2d) / count;
                    for (int k = 0; k < count; k++)
                    {
                        double from = start + 1d + k * pitch, to = from + pitch - ModuleGap;
                        for (int side = -1; side <= 1; side += 2) Module(walls, from, to, side, module++);
                        if (k == count - 1 || joint++ % 4 != 2) continue;
                        Lamp(lamps, to + ModuleGap * 0.5d, lamp % 2 == 0 ? -1 : 1, lamp);
                        lamp++;
                    }
                }
            }

            private void Module(Transform parent, double from, double to, int side, int index)
            {
                float half = Mathf.Max(definition.HalfWidthAt(from), definition.HalfWidthAt(to));
                float offset = half + BandIn + BandDepth * 0.5f;
                for (double s = from; s <= to + 1e-6d; s += 1d)
                    if (!near.Available(BandPoint(s, side, offset), BandDepth * 0.5f)) return;
                double mid = (from + to) * 0.5d;
                TrackFrame frame = line.FrameAt(mid);
                Transform piece = Group("Band module " + index, parent);
                piece.SetPositionAndRotation(BandPoint(mid, side, offset), Flat(frame.Forward));
                float length = (float)(to - from);
                float top = frame.Position.y - Ground;
                // Tall enough to stand over the 1.1 m guard (first build 2026-11-02: flowers 0.15 m over it read as a line).
                float kind = Hash(index * 7 + 101);
                if (kind < 0.45f)
                {
                    Box("Sandstone wall", piece, new Vector3(0f, (top + 1.4f) * 0.5f, 0f), new Vector3(BandDepth, top + 1.4f, length), sand);
                    Box("Wall coping", piece, new Vector3(0f, top + 1.47f, 0f), new Vector3(BandDepth + 0.15f, 0.15f, length + 0.1f), stone);
                    Box("Flower box soil", piece, new Vector3(0f, top + 1.67f, 0f), new Vector3(BandDepth - 0.3f, 0.25f, length - 0.3f), soil);
                    Box("Flowers", piece, new Vector3(0f, top + 2.05f, 0f), new Vector3(BandDepth - 0.4f, 0.55f, length - 0.5f), Pick(index * 11 + 102, tulip, marigold, lavender, tulip));
                }
                else if (kind < 0.7f)
                {
                    // The fence on the road side, lavender behind it.
                    float x = -side * 0.3f;
                    int posts = Mathf.Max(2, Mathf.RoundToInt(length / 2.5f) + 1);
                    for (int p = 0; p < posts; p++)
                        Box("Fence post", piece, new Vector3(x, (top + 1.9f) * 0.5f, -length * 0.5f + length * p / (posts - 1)), new Vector3(0.18f, top + 1.9f, 0.18f), wood);
                    for (int r = 0; r < 2; r++)
                        Box("Fence rail", piece, new Vector3(x, top + 1.1f + r * 0.6f, 0f), new Vector3(0.1f, 0.14f, length), wood);
                    Box("Lavender row", piece, new Vector3(side * 0.25f, (top + 1.4f) * 0.5f, 0f), new Vector3(0.7f, top + 1.4f, length - 0.4f), lavender);
                }
                else
                {
                    Box("Clipped hedge", piece, new Vector3(0f, (top + 1.8f) * 0.5f, 0f), new Vector3(BandDepth, top + 1.8f, length), hedge);
                    Box("Hedge top", piece, new Vector3(0f, top + 1.9f, 0f), new Vector3(BandDepth - 0.3f, 0.2f, length - 0.3f), sage);
                }
                for (double s = from; s <= to + 1e-6d; s += 2d) near.Reserve(BandPoint(s, side, offset), BandDepth * 0.5f);
            }

            // On the land, offset metres to the side of the road at s.
            private Vector3 BandPoint(double s, int side, float offset)
            {
                TrackFrame frame = line.FrameAt(s);
                Vector3 p = frame.Position + frame.Right * side * offset;
                p.y = Ground;
                return p;
            }

            // A green lamp with two lanterns along the road and a banner hanging off its far side, facing the runner (the
            // reference's banner poles). Nothing reaches over the road.
            private void Lamp(Transform parent, double s, int side, int index)
            {
                Vector3 position = BandPoint(s, side, definition.HalfWidthAt(s) + BandIn + BandDepth * 0.5f);
                if (!near.Clear(position, 0.3f)) return;
                TrackFrame frame = line.FrameAt(s);
                Transform lamp = Group("Village lamp " + index, parent);
                lamp.SetPositionAndRotation(position, Flat(frame.Forward));
                float top = frame.Position.y - Ground;
                Box("Lamp foot", lamp, new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.6f, 0.5f), stone);
                Box("Lamp post", lamp, new Vector3(0f, (top + 6f) * 0.5f, 0f), new Vector3(0.24f, top + 6f, 0.24f), lampGreen);
                Box("Lantern bar", lamp, new Vector3(0f, top + 6f, 0f), new Vector3(0.14f, 0.14f, 1.8f), lampGreen);
                for (int end = -1; end <= 1; end += 2)
                    Box("Lantern", lamp, new Vector3(0f, top + 5.6f, end * 0.8f), new Vector3(0.45f, 0.65f, 0.45f), gold);
                Box("Banner arm", lamp, new Vector3(side * 0.6f, top + 4.9f, 0f), new Vector3(1.2f, 0.08f, 0.08f), dark);
                Box("Banner", lamp, new Vector3(side * 0.7f, top + 3.6f, 0f), new Vector3(0.9f, 2.5f, 0.06f), Pick(index * 13 + 103, violet, lampGreen, coral, blue));
                near.Reserve(position, 0.6f);
            }

            // Every corner here turns 45° or more: on its outside yellow boards of dark chevrons pointing the way it
            // turns, raised over the guard on two legs (SKYWAY's corner boards); on its inside round flower planters,
            // upright cylinders whose world bounds do not swell as the corner turns.
            private void Corners(Transform parent)
            {
                double s0 = 0d;
                int index = 0;
                foreach (TrackDefinition.Segment segment in definition.Segments)
                {
                    double start = s0;
                    s0 += segment.Length;
                    if (!segment.IsArc || Mathf.Abs(segment.Degrees) < 45f) continue;
                    // The outside of a right turn (positive degrees) is the left.
                    int outside = segment.Degrees > 0f ? -1 : 1;
                    int count = Mathf.Max(1, Mathf.RoundToInt(segment.Length / 13f));
                    for (int k = 0; k < count; k++)
                    {
                        double s = start + segment.Length * (k + 0.5d) / count;
                        Vector3 position = BandPoint(s, outside, definition.HalfWidthAt(s) + 2.8f);
                        if (!near.Available(position, 0.8f)) continue;
                        TrackFrame frame = line.FrameAt(s);
                        Transform board = Group("Corner chevrons " + index++, parent);
                        board.SetPositionAndRotation(position, Flat(-frame.Right * outside));
                        float y = frame.Position.y - Ground;
                        for (int leg = -1; leg <= 1; leg += 2)
                            Box("Board leg", board, new Vector3(leg * 1.5f, (y + 1.4f) * 0.5f, 0f), new Vector3(0.2f, y + 1.4f, 0.2f), dark);
                        Box("Board", board, new Vector3(0f, y + 2.1f, 0f), new Vector3(3.8f, 1.4f, 0.15f), chevron);
                        // The board's +x is the way the runner goes (LookRotation toward the road from the outside).
                        float point = outside;
                        for (int c = -1; c <= 1; c += 2)
                        {
                            Beam("Chevron", board, new Vector3(c * 0.8f - point * 0.3f, y + 1.6f, 0.1f), new Vector3(c * 0.8f + point * 0.3f, y + 2.1f, 0.1f), 0.22f, dark);
                            Beam("Chevron", board, new Vector3(c * 0.8f + point * 0.3f, y + 2.1f, 0.1f), new Vector3(c * 0.8f - point * 0.3f, y + 2.6f, 0.1f), 0.22f, dark);
                        }
                        near.Reserve(position, 2f);
                    }
                    for (double s = start + 4d; s < s0 - 3d; s += 7d)
                    {
                        // 2.2 m out: the planter's world bounds are a square whose corners reach 0.37 m past its round.
                        Vector3 position = BandPoint(s, -outside, definition.HalfWidthAt(s) + 2.2f);
                        if (!near.Available(position, 0.9f)) continue;
                        float y = line.FrameAt(s).Position.y - Ground;
                        Transform planter = Group("Corner planter", parent);
                        planter.position = position;
                        Shape("Stone planter", planter, new Vector3(0f, (y + 0.2f) * 0.5f, 0f), new Vector3(1.8f, y + 0.2f, 1.8f), stone, Cylinder);
                        Shape("Planter flowers", planter, new Vector3(0f, y + 0.45f, 0f), new Vector3(1.7f, 0.9f, 1.7f), Pick((int)s * 3 + 104, tulip, marigold, lavender), Sphere);
                        near.Reserve(position, 0.9f);
                    }
                }
            }

            // A colourful board behind the band in the middle of each straight of 40 m or more (two on a long one), sides
            // alternating, its face to the road: a big eye and two lines of writing (the reference's character posters).
            private void Billboards(Transform parent)
            {
                double s0 = 0d;
                int index = 0;
                foreach (TrackDefinition.Segment segment in definition.Segments)
                {
                    double start = s0;
                    s0 += segment.Length;
                    if (segment.IsArc || segment.Length < 40f) continue;
                    int count = segment.Length > 90f ? 2 : 1;
                    for (int k = 0; k < count; k++)
                    {
                        index++;
                        int side = index % 2 == 0 ? -1 : 1;
                        double s = start + segment.Length * (k + 1d) / (count + 1d);
                        Vector3 position = BandPoint(s, side, definition.HalfWidthAt(s) + 5.2f);
                        if (!corridor.Available(position, 3.4f)) continue;
                        TrackFrame frame = line.FrameAt(s);
                        Transform board = Group("Advertising board " + index, parent);
                        board.SetPositionAndRotation(position, Flat(-frame.Right * side));
                        float y = frame.Position.y - Ground;
                        Material back = Pick(index * 7 + 105, coral, blue, chevron, violet, marigold, sage);
                        for (int leg = -1; leg <= 1; leg += 2)
                            Box("Board leg", board, new Vector3(leg * 2.6f, (y + 2f) * 0.5f, -0.1f), new Vector3(0.2f, y + 2f, 0.2f), dark);
                        Box("Board frame", board, new Vector3(0f, y + 3.6f, -0.05f), new Vector3(6.4f, 3.2f, 0.12f), dark);
                        Box("Board face", board, new Vector3(0f, y + 3.6f, 0.03f), new Vector3(6f, 2.8f, 0.06f), back);
                        GameObject eye = Shape("Poster eye", board, new Vector3(-1.6f, y + 3.6f, 0.08f), new Vector3(2.2f, 0.06f, 2.2f), white, Cylinder);
                        eye.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        GameObject pupil = Shape("Poster pupil", board, new Vector3(-1.4f, y + 3.5f, 0.12f), new Vector3(0.9f, 0.06f, 0.9f), dark, Cylinder);
                        pupil.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        Box("Headline", board, new Vector3(1.1f, y + 4.2f, 0.08f), new Vector3(2.8f, 0.5f, 0.05f), white);
                        Box("Strapline", board, new Vector3(1.1f, y + 3.4f, 0.08f), new Vector3(2.4f, 0.3f, 0.05f), cream);
                        corridor.Reserve(position, 3.4f);
                    }
                }
            }

            private void ShortcutFlags(Transform parent)
            {
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
                        corridor.Reserve(position, 1.4f);
                    }
                }
            }

            // Windmills on the green beyond the village, sails toward the track (the reference's skyline).
            private void Windmills(Transform parent)
            {
                Vector3[] places =
                {
                    new Vector3(-90f, Ground, 95f), new Vector3(455f, Ground, 118f),
                    new Vector3(-95f, Ground, -105f), new Vector3(455f, Ground, -125f)
                };
                for (int i = 0; i < places.Length; i++)
                {
                    if (!corridor.Available(places[i], 7f)) throw new InvalidOperationException("Windmill overlaps the road or a landmark.");
                    Transform mill = Group("Windmill " + i, parent);
                    Vector3 toTrack = new Vector3(175f, Ground, 0f) - places[i];
                    mill.SetPositionAndRotation(places[i], Flat(toTrack));
                    Shape("Mill tower", mill, new Vector3(0f, 5f, 0f), new Vector3(7f, 10f, 7f), cream, Cylinder);
                    Shape("Mill upper storey", mill, new Vector3(0f, 12f, 0f), new Vector3(5.6f, 4f, 5.6f), white, Cylinder);
                    Shape("Mill cap", mill, new Vector3(0f, 14.4f, 0f), new Vector3(6.4f, 4f, 6.4f), coral, Sphere);
                    Box("Mill door", mill, new Vector3(0f, 1.4f, 3.45f), new Vector3(1.4f, 2.8f, 0.2f), wood);
                    var hub = new Vector3(0f, 13.2f, 3.7f);
                    Shape("Sail hub", mill, hub, Vector3.one * 1.2f, dark, Sphere);
                    for (int blade = 0; blade < 4; blade++)
                    {
                        float angle = (blade * 90f + i * 20f) * Mathf.Deg2Rad;
                        var along = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                        var across = new Vector3(-along.y, along.x, 0f);
                        Beam("Sail spar", mill, hub, hub + along * 8f, 0.3f, wood);
                        GameObject sail = Box("Sail", mill, hub + along * 5f + across * 0.9f + Vector3.forward * 0.1f, new Vector3(1.7f, 6f, 0.08f), white);
                        sail.transform.localRotation = Quaternion.FromToRotation(Vector3.up, along);
                    }
                    corridor.Reserve(places[i], 7f);
                }
            }

            // Plaster houses with orange tiles, fronts to the road behind the band, every 18 m on each side where there is
            // room (a quarter left out by the hash); a quieter row on the green's far edges. Each house's size, colours,
            // roof and front differ by its hash. Only beside a straight: a house turned with a corner swells its world
            // bounds toward the road (first build 2026-11-02: margins 0.43-0.67 m), so the corners have boards and trees.
            private void Village(Transform parent)
            {
                int index = 0;
                for (double s = 9d; s < definition.LapLength; s += 18d)
                {
                    TrackFrame frame = line.FrameAt(s);
                    bool straight = frame.Curvature == 0f && line.FrameAt(s - 9d).Curvature == 0f && line.FrameAt(s + 9d).Curvature == 0f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        index++;
                        if (!straight || Hash(index * 5 + 106) < 0.25f) continue;
                        float width = 7f + Hash(index * 3 + 107) * 3.5f;
                        float depth = 6.5f + Hash(index * 7 + 108) * 2f;
                        float radius = new Vector2(width, depth).magnitude * 0.5f;
                        Vector3 position = frame.Position + frame.Right * side * (definition.HalfWidthAt(s) + HouseFront + depth * 0.5f);
                        position.y = Ground;
                        if (!corridor.Available(position, radius)) continue;
                        House(parent, position, Flat(-frame.Right * side), width, depth, 6.5f + Hash(index * 11 + 109) * 3.5f, index);
                        corridor.Reserve(position, radius);
                    }
                }
                // A second, quieter row makes the outside read as a village rather than isolated roadside props.
                for (int i = 0; i < 14; i++)
                {
                    Vector3 position = new Vector3(-46f + i * 33f, Ground, i % 2 == 0 ? -112f : 121f);
                    if (!corridor.Available(position, 8f)) continue;
                    House(parent, position, Quaternion.Euler(0f, i % 2 == 0 ? 0f : 180f, 0f), 10f, 8.5f, 9f + i % 3 * 1.5f, index + i);
                    corridor.Reserve(position, 8f);
                }
            }

            // SKYWAY's town house in this village's colours: front to local +z, two storeys.
            private void House(Transform parent, Vector3 position, Quaternion facing, float width, float depth, float height, int index)
            {
                Transform house = Group("Village house " + index, parent);
                house.SetPositionAndRotation(position, facing);
                Material walls = Pick(index * 3 + 21, cream, apricot, rose, white, blue, cream);
                Material shutters = Pick(index * 5 + 22, blue, sage, wood, coral);
                Box("Stone plinth", house, new Vector3(0f, 0.4f, 0f), new Vector3(width + 0.3f, 0.8f, depth + 0.3f), stone);
                Box("Plaster walls", house, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), walls);
                float roofKind = Hash(index * 9 + 24);
                Material roofing = roofKind < 0.7f ? tile : roofKind < 0.85f ? coral : blue;
                float rise = width * 0.26f;
                float slope = Mathf.Atan2(rise, width * 0.5f) * Mathf.Rad2Deg;
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject panel = Box("Tiled roof", house, new Vector3(side * width * 0.25f, height + rise * 0.5f, 0f),
                        new Vector3(new Vector2(width * 0.5f, rise).magnitude + 0.5f, 0.3f, depth + 0.7f), roofing);
                    panel.transform.localRotation = Quaternion.Euler(0f, 0f, -side * slope);
                }
                for (int tier = 0; tier < 3; tier++)
                    Box("Gable", house, new Vector3(0f, height + rise * (tier + 0.5f) / 3f, 0f), new Vector3(width * (1f - (tier + 0.5f) / 3f) * 0.95f, rise / 3f, depth * 0.96f), walls);
                if (Hash(index * 19 + 25) > 0.5f)
                    Box("Chimney", house, new Vector3(width * 0.28f, height + rise * 0.9f, depth * 0.2f), new Vector3(0.8f, rise * 1.2f, 0.8f), stone);
                float front = depth * 0.5f + 0.06f;
                int columns = width > 8.5f ? 3 : 2;
                for (int col = 0; col < columns; col++)
                {
                    float x = (col - (columns - 1) * 0.5f) * width * 0.3f;
                    float y = height * 0.5f + 1.2f;
                    // The open shutters are one board behind the glass, showing either side of it.
                    Box("Shutters", house, new Vector3(x, y, front), new Vector3(2f, 1.5f, 0.08f), shutters);
                    Box("Window", house, new Vector3(x, y, front + 0.04f), new Vector3(1.1f, 1.4f, 0.08f), blue);
                    if (Hash(index * 37 + col) > 0.45f)
                        Box("Flower box", house, new Vector3(x, y - 0.85f, front + 0.2f), new Vector3(1.3f, 0.3f, 0.36f), Pick(index * 41 + col, tulip, marigold, lavender, tulip));
                }
                Box("Door", house, new Vector3(-width * 0.22f, 1.3f, front), new Vector3(1.3f, 2.4f, 0.12f), Pick(index * 43 + 26, wood, blue, coral));
                Box("Shop window", house, new Vector3(width * 0.18f, 1.6f, front), new Vector3(2.4f, 1.5f, 0.12f), blue);
                float extra = Hash(index * 47 + 27);
                if (extra < 0.4f)
                {
                    Material stripe = Pick(index * 53 + 28, coral, blue, sage, marigold, violet);
                    for (int s = 0; s < 5; s++)
                        Box("Striped awning", house, new Vector3((s - 2) * width * 0.16f, 3.1f, depth * 0.5f + 0.9f), new Vector3(width * 0.16f, 0.22f, 1.8f), s % 2 == 0 ? stripe : cream);
                }
                else if (extra < 0.7f)
                {
                    Box("Balcony slab", house, new Vector3(0f, height * 0.5f + 0.2f, front + 0.55f), new Vector3(width * 0.5f, 0.2f, 1.1f), stone);
                    Box("Balcony rail", house, new Vector3(0f, height * 0.5f + 0.75f, front + 1.05f), new Vector3(width * 0.5f, 0.9f, 0.08f), white);
                }
            }

            // The clock tower's roof: tiled panels over a stepped gable.
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

            // Flower gardens where the fingers leave room, trees in the gaps along the road, and the start square's trees.
            private void Gardens(Transform parent)
            {
                int garden = 0;
                for (int x = 25; x <= 325; x += 50)
                    for (int z = -14; z <= 62; z += 38)
                    {
                        Vector3 position = new Vector3(x, Ground, z);
                        if (!corridor.Available(position, 7.5f)) continue;
                        Transform island = Group("Pocket garden " + garden, parent);
                        island.position = position;
                        Box("Garden paving", island, new Vector3(0f, 0.12f, 0f), new Vector3(12f, 0.24f, 12f), stone);
                        Box("Raised lawn", island, new Vector3(0f, 0.3f, 0f), new Vector3(8f, 0.35f, 8f), sage);
                        for (int bed = 0; bed < 4; bed++)
                        {
                            Vector3 at = Quaternion.Euler(0f, bed * 90f, 0f) * new Vector3(0f, 0.45f, 4.9f);
                            Box("Flower bed", island, at, bed % 2 == 0 ? new Vector3(9f, 0.5f, 1f) : new Vector3(1f, 0.5f, 9f), Pick(garden * 4 + bed + 110, tulip, marigold, lavender, white));
                        }
                        Tree(island, new Vector3(-1.5f, 0.5f, -1.2f), 5.5f);
                        Tree(island, new Vector3(1.6f, 0.5f, 1.4f), 4.6f);
                        corridor.Reserve(position, 7.5f);
                        garden++;
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
                for (int i = 0; i < 5; i++)
                {
                    Vector3 p = new Vector3(-28f, Ground, -30f + i * 16f);
                    if (!corridor.Available(p, 3.4f)) continue;
                    Tree(parent, p, 6f);
                }
            }

            private void Tree(Transform parent, Vector3 position, float height)
            {
                Transform tree = Group("Round village tree", parent);
                tree.localPosition = position;
                Box("Tree planter", tree, new Vector3(0f, 0.25f, 0f), new Vector3(2.4f, 0.5f, 2.4f), stone);
                Box("Tree trunk", tree, new Vector3(0f, height * 0.4f, 0f), new Vector3(0.55f, height * 0.8f, 0.55f), wood);
                Shape("Rounded crown", tree, new Vector3(0f, height * 0.8f, 0f), new Vector3(4.9f, height * 0.65f, 4.9f), sage, Sphere);
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
