using System;
using System.Collections.Generic;
using System.IO;
using ProtoHarness.ChainRush.Track;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static ProtoHarness.Editor.ChainRush.ExteriorKit;

namespace ProtoHarness.Editor.ChainRush
{
    // Like the other tracks' builders (user 2026-11-02, "확장성과 재사용성"): the stadium's exterior as one prefab that a
    // rebuild lays out again and checks against the road, and its own road, wall and lane-marking materials as a
    // TrackLook. The night sports complex the art pass baked into mesh assets (DECISIONS 2026-10-20, Art/Models/
    // StadiumSports_*) is placed as it was in the decoration scene; the builder has no code that makes those meshes, but
    // cuts the old oval track and pitch out of them for a football field, and dresses the gaps as a water jump and a
    // pole vault (user 2026-11-02). Swapping the decoration scene's old roots for this prefab, and saving it, is left to
    // the user.
    // LED boards, a finish gantry and athletics/infield furniture dress the night (user 2026-11-03, STADIUM detail).
    // user 2026-11-03, "웸블리 스타디움, 서울 종합경기운동장 같은 느낌": a large bowl, saddle roofline and white arch.
    public static class StadiumEnvironmentBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/StadiumExterior.prefab";
        public const string ScenePath = "Assets/_Project/Scenes/ChainRushTrackStadium.unity";
        public const string LookPath = "Assets/_Project/Data/TrackLook_Stadium.asset";
        private const string DefinitionPath = "Assets/_Project/Data/Circuit_Stadium.asset";
        private const string ModelFolder = ExteriorAssets.ModelFolder;
        // What lies at or below this is ground for the clearance check: the baked ground (-0.21 m), the new infield
        // grass and the water in the pits.
        private const float Ground = -0.15f;

        [MenuItem("ProtoHarness/Chain Rush/Create Stadium Exterior")]
        public static void CreateExterior()
        {
            ExteriorAssets.RequireIdle();
            if (File.Exists(PrefabPath)) throw new InvalidOperationException("Stadium exterior already exists; use Rebuild Stadium Exterior.");
            BuildLook();
            GameObject prefab = BuildPrefab();
            Debug.Log($"Stadium exterior created. In {ScenePath} replace the old roots (Stadium Environment, Stadium Art Dressing, Stadium Sports Complex) with one {PrefabPath} at the origin and save it; give the lobby's STADIUM entry {LookPath}.", prefab);
        }

        [MenuItem("ProtoHarness/Chain Rush/Rebuild Stadium Exterior")]
        public static void RebuildExterior()
        {
            ExteriorAssets.RequireIdle();
            if (!File.Exists(PrefabPath)) throw new InvalidOperationException("Create Stadium Exterior first.");
            BuildLook();
            BuildPrefab();
        }

        // A running track (user 2026-11-02, "계주용 (레인이 많은) 디자인"): red rubber with solid white lines 1.22 m apart
        // and no middle line, pale walls that read under the floodlights, yellow take-off boards at the water jumps. The
        // sky stays the race scene's night (the title's "AFTER DARK"; user 2026-11-02, night for the stadium only) with
        // stars and a few dark clouds over a navy horizon, the fog a little further out than the scene's 45-210 m.
        // The look and its sky only, the decoration left as it is (user 2026-11-02, the track skies).
        [MenuItem("ProtoHarness/Chain Rush/Rebuild Stadium Look")]
        public static void BuildLook()
            => ExteriorAssets.BuildLook(LookPath,
                ExteriorAssets.Surface("Stadium", "Road", new Color(0.60f, 0.24f, 0.17f), ExteriorAssets.Grain.Rubber, 2f, 0.06f),
                ExteriorAssets.Surface("Stadium", "Wall", new Color(0.80f, 0.83f, 0.82f), ExteriorAssets.Grain.Concrete, 3f, 0.08f),
                ExteriorAssets.Palette("Stadium", "Marking", new Color(0.93f, 0.93f, 0.90f), 0.05f),
                ExteriorAssets.Palette("Stadium", "Accent", new Color(0.96f, 0.78f, 0.18f), 0.05f),
                new ExteriorAssets.Lanes(1.22f, 99f, false, true),
                ExteriorAssets.BuildSky("Stadium", new Color(0.012f, 0.016f, 0.05f), new Color(0.06f, 0.06f, 0.13f), new Color(0.11f, 0.11f, 0.19f), 0.22f, true, 60f, 300f));

        private static GameObject BuildPrefab()
        {
            var layout = new Layout(ExteriorAssets.RequireDefinition(DefinitionPath));
            return ExteriorAssets.SavePrefab(PrefabPath, layout.Build, layout.ValidateClearance);
        }

        private static Material Palette(string name, Color color, float glow = 0f) => ExteriorAssets.Palette("Stadium", name, color, glow);

        private sealed class Layout
        {
            private const string ComplexGroup = "01 Sports complex";
            private const string BowlGroup = "08 Upper bowl", RoofGroup = "09 Roof", ArchGroup = "10 Arch", ScreensGroup = "11 Big screens";
            // Offsets are measured OUTSIDE the road edge (negative D), never from the centre of the pitch.
            private const float BowlInner = 36f, Overhang = 6f, SeatBack = 72f, BowlBack = 76f;
            // Keep the first tier below a fixed concourse/eave; the upper rows follow the saddle from 36 to 44 m.
            private const float ConcourseTop = 12.5f, SeatFront = 14f, BowlLow = 36f, SaddleRise = 8f;
            // 42 / 1.3 rounds to 32 rows, each 1.3125 m deep. Saddle risers are (36..44 - 14) / 32
            // = 0.6875..0.9375 m; keep the saddle rather than imposing the lower tier's 0.65 m rise.
            private const int SeatRows = 32;
            private const float SeatPitch = 1.65f, SeatWidth = 0.78f, SeatDepth = 0.68f, SeatThickness = 0.16f;
            private const float SeatTop = 0.47f, BackDepth = 0.13f, BackHeight = 0.64f, BackOffset = 0.32f;
            private const int SeatsPerBlock = 12, AisleSeats = 2, UpperVertexLimit = 700000;
            // Flaring skin, roof rise, inward roof reach, vertical thickness and front-edge fall, all in metres.
            private const float WallFlare = 6f, RoofRise = 8f, RoofFront = 24f, RoofThickness = 1.2f, RoofFall = 4f;
            // Centreline station limits; offset curves use the same stations and close with the first station itself.
            private const double ArcStation = 1d, StraightStation = 2d;
            private const float PortalPitch = 25f, PortalWidth = 4f, PortalHeight = 3f, PortalInset = 0.05f;
            private const int WallSteps = 16;
            // Ribs stand proud of the curved skin and run through the 8 m roof support zone.
            private const float RibPitch = 20f, RibRadius = 0.4f;
            // The arch must ride over the roof, not into it (first rebuild 2026-11-03: 95 m with feet 13 m out sank into
            // the 52 m side roof). Feet 50 m beyond the ground skin put them ~190 m from the centre; the roof's back is
            // ~134 m out, where 120 * (1 - (134/190)^2) = 60 m clears the side roof's 52 m (checked in Arch).
            private const float ArchHeight = 120f, ArchRadius = 2.5f, CollarRadius = 2.7f, CollarTube = 0.2f;
            private const float ArchFootClear = 50f, ArchStep = 2f, CollarPitch = 6f;
            private const int TubeSides = 12, CollarSides = 24;
            private const float ScreenOffset = 40f, ScreenBottom = 26f, ScreenWidth = 30f, ScreenHeight = 12f, ScreenTilt = 15f;
            // The new infield's grass stands this far inside the track's inner edge, its top here.
            private const float InfieldInset = 0.8f;
            private const float InfieldTop = -0.17f;
            // An American football field (yards in metres): 100 yards between the goal lines, 10-yard end zones, 53 1/3
            // yards across.
            private const float Yard = 0.9144f;
            private const float FieldWidth = 160f / 3f * Yard;
            // Water in the pits under the gaps, just above the baked ground (-0.21 m).
            private const float WaterTop = -0.18f;
            // Trackside dimensions in metres (user 2026-11-03, STADIUM detail): the screen's road face, total depth,
            // module length and open joint; the gap events keep six metres clear beyond either end of each pit.
            private const float BoardFront = 1.6f, BoardDepth = 0.3f, BoardLength = 6f, BoardGap = 0.4f;
            private const double GapBuffer = 6d;
            // RoadCorridor exempts overhead bounds from 9 m; leave another 0.4 m as in FINGER's StartGate.
            private const float OverheadClear = 9.4f;
            // The pillar's near face is 1 m beyond the guard's outer face; its width includes its foot.
            private const float PillarClear = 1f, PillarWidth = 0.8f;
            // A hurdle every ~3 m along the OUTSIDE arc; small pads every ~1.2 m along the INSIDE arc.
            private const float HurdlePitch = 3f, HurdleOffset = 2f, PadPitch = 1.2f, PadOffset = 1.2f;
            // Both sets are trimmed outside the lower tier and over the infield: the oval's lane lines, its small pitch's
            // lines and goals (user 2026-11-02, the infield becomes a football field). The oval's clay and the pitch's turf
            // (StadiumSports_Clay, _TurfLight) are left out, and so is the outer turf (StadiumSports_TurfDark): every one of
            // its vertices lies 38-45 m outside the road edge (measured 2026-11-03), under the upper bowl, so trimming
            // left it empty (review 2026-11-03: an empty mesh was saved and an empty renderer kept). An empty result throws.
            private static readonly (string Mesh, string Material)[] Trimmed =
            {
                ("Concrete", "M_StadiumSportsConcrete"), ("SeatBlue", "M_StadiumSportsSeatBlue"),
                ("Steel", "M_ArtStadiumSteel"), ("Mint", "M_ArtStadiumMint"), ("SeatMuted", "M_ArtStadiumSeats"),
                ("White", "M_StadiumSportsWhite"), ("Ink", "M_ArtStadiumInk"), ("Amber", "M_ArtStadiumAmber"),
            };
            private readonly TrackDefinition definition;
            private readonly Centerline line;
            private readonly RoadCorridor corridor;
            // As in FINGER: a second corridor for narrow furniture close to the guard. A 0.5 m keep-out plus the
            // board's 0.15 m radius fits its 1.6 m front; the main corridor still runs the final 0.7 m bounds check.
            private readonly RoadCorridor near;
            // The infield's outline in x and z, a convex loop just inside the track.
            private readonly List<Vector2> infield = new List<Vector2>();
            private readonly List<double> bowlStations = new List<double>();
            private readonly Material infieldGrass = Palette("Infield", new Color(0.20f, 0.38f, 0.22f));
            private readonly Material fieldLight = Palette("FieldLight", new Color(0.30f, 0.55f, 0.29f));
            private readonly Material fieldDark = Palette("FieldDark", new Color(0.24f, 0.47f, 0.25f));
            private readonly Material fieldLine = Palette("FieldLine", new Color(0.95f, 0.95f, 0.92f), 0.05f);
            private readonly Material endZoneNavy = Palette("EndZoneNavy", new Color(0.12f, 0.20f, 0.40f));
            private readonly Material endZoneMint = Palette("EndZoneMint", new Color(0.16f, 0.52f, 0.46f));
            private readonly Material goalPost = Palette("GoalPost", new Color(0.96f, 0.80f, 0.20f));
            private readonly Material padding = Palette("Padding", new Color(0.14f, 0.26f, 0.50f));
            private readonly Material water = Palette("Water", new Color(0.10f, 0.62f, 0.70f), 0.15f);
            private readonly Material ink = Palette("BarrierBlack", new Color(0.10f, 0.10f, 0.11f));
            private readonly Material white = Palette("BarrierWhite", new Color(0.94f, 0.94f, 0.92f));
            private readonly Material metal = Palette("VaultMetal", new Color(0.70f, 0.72f, 0.75f));
            private readonly Material barYellow = Palette("VaultBar", new Color(0.96f, 0.80f, 0.20f), 0.1f);
            // Distinct detail names keep the existing stadium palette's assets (especially Padding) separate.
            private readonly Material detailSteel = Palette("DetailSteel", new Color(0.12f, 0.15f, 0.20f));
            private readonly Material floodLamp = Palette("FloodLamp", new Color(1f, 0.91f, 0.74f), 6f);
            private readonly Material ledMint = Palette("LedMint", new Color(0.18f, 0.92f, 0.73f), 0.55f);
            private readonly Material ledAmber = Palette("LedAmber", new Color(1f, 0.58f, 0.13f), 0.5f);
            private readonly Material ledMagenta = Palette("LedMagenta", new Color(0.88f, 0.18f, 0.58f), 0.5f);
            private readonly Material flagAmber = Require("M_ArtStadiumAmber");
            private readonly Material bowlConcrete = Palette("BowlConcrete", new Color(0.18f, 0.19f, 0.21f));
            private readonly Material seatConcrete = Require("M_StadiumSportsConcrete");
            private readonly Material seatBlue = Require("M_StadiumSportsSeatBlue");
            private readonly Material seatMuted = Require("M_ArtStadiumSeats");
            private readonly Material seatWhite = Require("M_StadiumSportsWhite");
            private readonly Material bowlWhite = Palette("BowlWhite", new Color(0.89f, 0.90f, 0.87f));
            private readonly Material bowlEntry = Palette("BowlEntry", new Color(1f, 0.72f, 0.40f), 1.5f);
            private readonly Material screenNavy = Palette("ScreenNavy", new Color(0.025f, 0.055f, 0.14f), 0.6f);

            internal Layout(TrackDefinition definition)
            {
                this.definition = definition;
                line = definition.BuildCenterline(Vector3.zero, 0f);
                corridor = new RoadCorridor(definition, line, 0.5d, 1.5f);
                near = new RoadCorridor(definition, line, 0.5d, 0.5f);
                // The loop turns right, so its inside is to the right of the centerline.
                foreach (TrackDefinition.Segment segment in definition.Segments)
                    if (segment.IsArc && segment.Degrees < 0f) throw new InvalidOperationException("Stadium layout changed: a left turn; review the infield.");
                for (double s = 0d; s < definition.LapLength; s += 1d)
                {
                    TrackFrame frame = line.FrameAt(s);
                    Vector3 p = frame.Position + frame.Right * (definition.HalfWidthAt(s) + InfieldInset);
                    infield.Add(new Vector2(p.x, p.z));
                }
                double start = 0d;
                foreach (TrackDefinition.Segment segment in definition.Segments)
                {
                    int count = (int)Math.Ceiling(segment.Length / (segment.IsArc ? ArcStation : StraightStation));
                    if (count < 1) throw new InvalidOperationException("Stadium bowl needs positive segment lengths.");
                    for (int k = 0; k < count; k++) bowlStations.Add(start + segment.Length * k / count);
                    start += segment.Length;
                }
                if (bowlStations.Count < 3) throw new InvalidOperationException("Stadium bowl has no closed outline.");
            }

            // The baked complex surrounds the road, so its world bounds always hold it: it is checked vertex by vertex.
            internal void ValidateClearance(GameObject root)
            {
                var byVertex = new List<Transform>();
                foreach (string name in new[] { ComplexGroup, BowlGroup, RoofGroup, ArchGroup, ScreensGroup })
                {
                    Transform group = root.transform.Find(name);
                    if (group == null) throw new InvalidOperationException("Stadium group is missing: " + name);
                    byVertex.Add(group);
                }
                corridor.ValidateClearance(root, Ground, null, byVertex);
            }

            internal GameObject Build(Scene scene)
            {
                var root = new GameObject("Stadium Exterior");
                SceneManager.MoveGameObjectToScene(root, scene);
                SportsComplex(Group(ComplexGroup, root.transform));
                FootballField(Group("02 Football field", root.transform));
                GapEvents(Group("03 Water jump and pole vault", root.transform));
                Transform boards = Group("05 Trackside boards", root.transform);
                Transform finish = Group("06 Finish gantry", root.transform);
                Transform furniture = Group("07 Corners and infield", root.transform);
                // Reserve fixed furniture before filling the straights; the removed tower group leaves 04 unused.
                FinishGantry(finish);
                ReserveGapEvents();
                TracksideBoards(boards);
                CornersAndInfield(furniture);
                UpperBowl(Group(BowlGroup, root.transform));
                Roof(Group(RoofGroup, root.transform));
                Arch(Group(ArchGroup, root.transform));
                BigScreens(Group(ScreensGroup, root.transform));
                return root;
            }

            // Coordinates use the track's horizontal axes, with an absolute height above the baked ground. Road grade
            // must not ripple the stands or roof. Reusing station zero at the wrap makes every profile close exactly.
            private Vector3 BowlPoint(double s, float outside, float height)
            {
                TrackFrame frame = line.FrameAt(s);
                Vector3 p = frame.Position - frame.Right * (definition.HalfWidthAt(s) + outside);
                p.y = Ground + height;
                return p;
            }

            private float BowlTop(double s)
            {
                Vector2 centre = InfieldCentre();
                Vector3 p = BowlPoint(s, BowlBack, 0f);
                float theta = Mathf.Atan2(p.x - centre.x, p.z - centre.y); // +z is zero: sides high, ends low.
                return BowlLow + SaddleRise * (1f - Mathf.Cos(2f * theta)) * 0.5f;
            }

            private static float SkinOffset(float fraction) => BowlBack + WallFlare * (1f - fraction) * (1f - fraction);

            // Directly accumulate one buffer per material, as SKYWAY's MergeByMaterial does after placing its parts.
            // Every quad owns its four vertices: risers never share normals with the horizontal treads.
            private sealed class Surface
            {
                private readonly List<Vector3> vertices = new List<Vector3>();
                private readonly List<int> triangles = new List<int>();
                internal int VertexCount => vertices.Count;

                internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing)
                {
                    int first = vertices.Count;
                    vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                    Triangle(first, first + 1, first + 2, facing);
                    Triangle(first, first + 2, first + 3, facing);
                }

                private void Triangle(int a, int b, int c, Vector3 facing)
                {
                    float cross = Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), facing);
                    if (Mathf.Abs(cross) < 1e-7f) throw new InvalidOperationException("Stadium surface has a degenerate or edge-on triangle.");
                    triangles.Add(a); triangles.Add(cross > 0f ? b : c); triangles.Add(cross > 0f ? c : b);
                }

                internal void Save(Transform parent, string key, Material material)
                {
                    if (triangles.Count == 0) throw new InvalidOperationException("Stadium surface is empty: " + key);
                    var mesh = new Mesh { name = "StadiumExterior_" + key, indexFormat = IndexFormat.UInt32 };
                    mesh.SetVertices(vertices);
                    mesh.SetTriangles(triangles, 0);
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    Baked(parent, key, mesh, material);
                }
            }

            private static Surface ForMaterial(Dictionary<Material, Surface> surfaces, Material material)
            {
                if (!surfaces.TryGetValue(material, out Surface surface)) surfaces.Add(material, surface = new Surface());
                return surface;
            }

            private static void SaveSurfaces(Transform parent, string key, Dictionary<Material, Surface> surfaces)
            {
                // Strip either existing stadium palette prefix, including the lower tier's art seat materials.
                const string PalettePrefix = "M_Stadium", ArtPrefix = "M_ArtStadium";
                foreach (KeyValuePair<Material, Surface> pair in surfaces)
                {
                    string name = pair.Key.name;
                    int prefixLength;
                    if (name.StartsWith(PalettePrefix, StringComparison.Ordinal)) prefixLength = PalettePrefix.Length;
                    else if (name.StartsWith(ArtPrefix, StringComparison.Ordinal)) prefixLength = ArtPrefix.Length;
                    else throw new InvalidOperationException("Stadium surface material is not a stadium palette: " + name);
                    pair.Value.Save(parent, key + "_" + name.Substring(prefixLength), pair.Key);
                }
            }

            private void Profile(Surface surface, double a, double b, float oa, float ya, float ob, float yb,
                float pa, float za, float pb, float zb, Vector3 facing)
                => surface.Quad(BowlPoint(a, oa, ya), BowlPoint(b, ob, yb), BowlPoint(b, pb, zb), BowlPoint(a, pa, za), facing);

            private void UpperBowl(Transform parent)
            {
                var surfaces = new Dictionary<Material, Surface>();
                Surface concrete = ForMaterial(surfaces, bowlConcrete), skin = ForMaterial(surfaces, bowlWhite);
                Surface steps = ForMaterial(surfaces, seatConcrete);
                float portalDistance = PortalPitch, ribDistance = RibPitch;
                for (int i = 0; i < bowlStations.Count; i++)
                {
                    double a = bowlStations[i], b = bowlStations[(i + 1) % bowlStations.Count];
                    Vector3 inward = line.FrameAt(a).Right + line.FrameAt(b).Right;
                    float ta = BowlTop(a), tb = BowlTop(b);
                    Profile(concrete, a, b, BowlInner, 0f, BowlInner, 0f, BowlInner, ConcourseTop, BowlInner, ConcourseTop, inward);
                    Profile(concrete, a, b, BowlInner, ConcourseTop, BowlInner, ConcourseTop,
                        BowlInner - Overhang, ConcourseTop, BowlInner - Overhang, ConcourseTop, Vector3.down);
                    Profile(concrete, a, b, BowlInner - Overhang, ConcourseTop, BowlInner - Overhang, ConcourseTop,
                        BowlInner - Overhang, SeatFront, BowlInner - Overhang, SeatFront, inward);
                    for (int row = 0; row < SeatRows; row++)
                    {
                        float f0 = (float)row / SeatRows, f1 = (float)(row + 1) / SeatRows;
                        float o0 = Mathf.Lerp(BowlInner - Overhang, SeatBack, f0), o1 = Mathf.Lerp(BowlInner - Overhang, SeatBack, f1);
                        float ya = Mathf.Lerp(SeatFront, ta, f0), yb = Mathf.Lerp(SeatFront, tb, f0);
                        float za = Mathf.Lerp(SeatFront, ta, f1), zb = Mathf.Lerp(SeatFront, tb, f1);
                        // Keep the first tread at (30,14); the last riser ends exactly on (72,Top).
                        Profile(steps, a, b, o0, ya, o0, yb, o1, ya, o1, yb, Vector3.up);
                        Profile(steps, a, b, o1, ya, o1, yb, o1, za, o1, zb, inward);
                    }
                    Profile(concrete, a, b, SeatBack, ta, SeatBack, tb, BowlBack, ta, BowlBack, tb, Vector3.up);
                    for (int layer = 0; layer < WallSteps; layer++)
                    {
                        float f0 = (float)layer / WallSteps, f1 = (float)(layer + 1) / WallSteps;
                        Profile(skin, a, b, SkinOffset(f0), ta * f0, SkinOffset(f0), tb * f0,
                            SkinOffset(f1), ta * f1, SkinOffset(f1), tb * f1, -inward);
                    }
                    float wallLength = Vector3.Distance(BowlPoint(a, BowlInner, 0f), BowlPoint(b, BowlInner, 0f));
                    portalDistance += wallLength;
                    if (portalDistance >= PortalPitch)
                    {
                        // Four-metre opening plate follows the offset curve, 5 cm in front of the inner wall.
                        double half = PortalWidth * 0.5d / OffsetStretch(a, BowlInner);
                        Profile(ForMaterial(surfaces, bowlEntry), a - half, a + half,
                            BowlInner - PortalInset, 0f, BowlInner - PortalInset, 0f,
                            BowlInner - PortalInset, PortalHeight, BowlInner - PortalInset, PortalHeight, inward);
                        portalDistance = 0f;
                    }
                    ribDistance += Vector3.Distance(BowlPoint(a, BowlBack, 0f), BowlPoint(b, BowlBack, 0f));
                    if (ribDistance >= RibPitch)
                    {
                        var path = new List<Vector3>();
                        for (int layer = 0; layer <= WallSteps; layer++)
                        {
                            float f = (float)layer / WallSteps;
                            path.Add(BowlPoint(a, SkinOffset(f) + RibRadius, ta * f));
                        }
                        path.Add(BowlPoint(a, BowlBack + RibRadius, ta + RoofRise));
                        Tube(skin, path, RibRadius, TubeSides, false);
                        ribDistance = 0f;
                    }
                }
                Dictionary<Material, Surface> seats = UpperSeats();
                // Current 400 m straights + four R22 quarter-turns, 6 m road half-width: ~14,880 chairs.
                // 9 quads * 4 vertices = 36/chair (~535,680), plus ~154,540 steps/walls/ribs/portals:
                // ~690,220 vertices for ALL of 08, in 4 structure + 4 seat renderers. Check before saving assets.
                int vertices = 0;
                foreach (Surface surface in surfaces.Values) vertices += surface.VertexCount;
                foreach (Surface surface in seats.Values) vertices += surface.VertexCount;
                if (vertices > UpperVertexLimit)
                    throw new InvalidOperationException("Stadium upper bowl exceeds " + UpperVertexLimit + " vertices: " + vertices);
                SaveSurfaces(parent, "Bowl", surfaces);
                SaveSurfaces(parent, "Seats", seats);
            }

            private Dictionary<Material, Surface> UpperSeats()
            {
                var seats = new Dictionary<Material, Surface>();
                for (int row = 0; row < SeatRows; row++)
                {
                    float fraction = (float)row / SeatRows;
                    // Pan's front edge is 8 cm behind the tread's front; backrest remains within this tread.
                    float outside = Mathf.Lerp(BowlInner - Overhang, SeatBack, fraction) + SeatDepth * 0.5f + 0.08f;
                    double length = 0d;
                    for (int i = 0; i < bowlStations.Count; i++)
                    {
                        double a = bowlStations[i], b = i + 1 < bowlStations.Count ? bowlStations[i + 1] : definition.LapLength;
                        length += (b - a) * OffsetStretch((a + b) * 0.5d, outside);
                    }
                    // Whole 1.65 m slots leave at least a full pitch across the closing seam too.
                    int slots = (int)Math.Floor(length / SeatPitch), slot = 0;
                    double distance = SeatPitch * 0.5d;
                    for (int i = 0; i < bowlStations.Count; i++)
                    {
                        double a = bowlStations[i], b = i + 1 < bowlStations.Count ? bowlStations[i + 1] : definition.LapLength;
                        float stretch = OffsetStretch((a + b) * 0.5d, outside);
                        double span = (b - a) * stretch;
                        while (distance < span && slot < slots)
                        {
                            if (slot % (SeatsPerBlock + AisleSeats) < SeatsPerBlock)
                            {
                                double s = a + distance / stretch;
                                // Match the actual tread's station interpolation rather than floating over its saddle.
                                float tread = Mathf.Lerp(SeatFront, Mathf.Lerp(BowlTop(a), BowlTop(b), (float)(distance / span)), fraction);
                                TrackFrame frame = line.FrameAt(s);
                                Vector3 centre = BowlPoint(s, outside, tread + SeatTop - SeatThickness * 0.5f);
                                float color = Hash(row * 65537 + slot);
                                Material material = color < 0.75f ? seatBlue : color < 0.90f ? seatMuted : color < 0.95f ? flagAmber : seatWhite;
                                Surface surface = ForMaterial(seats, material);
                                Vector3 back = -frame.Right, along = frame.Forward;
                                SeatBox(surface, centre, along, back, SeatDepth, SeatThickness, false);
                                SeatBox(surface, centre + back * BackOffset + Vector3.up * (SeatThickness + BackHeight) * 0.5f,
                                    along, back, BackDepth, BackHeight, true);
                            }
                            slot++;
                            distance += SeatPitch;
                        }
                        distance -= span;
                    }
                }
                return seats;
            }

            // Measured StadiumSports_SeatBlue boxes (user 2026-11-03). Independent vertices give each face a hard normal.
            // No bottoms; the pan also omits its field-hidden rear face. Pan 4 faces + backrest 5 = 36 vertices.
            private static void SeatBox(Surface surface, Vector3 centre, Vector3 along, Vector3 back, float depth, float height, bool rear)
            {
                Vector3 x = along * (SeatWidth * 0.5f), z = back * (depth * 0.5f), y = Vector3.up * (height * 0.5f);
                Vector3 a = centre - x - y - z, b = centre + x - y - z;
                Vector3 c = centre + x - y + z, d = centre - x - y + z;
                surface.Quad(a + 2f * y, b + 2f * y, c + 2f * y, d + 2f * y, Vector3.up);
                surface.Quad(a, b, b + 2f * y, a + 2f * y, -back);
                surface.Quad(a, d, d + 2f * y, a + 2f * y, -along);
                surface.Quad(b, c, c + 2f * y, b + 2f * y, along);
                if (rear) surface.Quad(d, c, c + 2f * y, d + 2f * y, back);
            }

            private float OffsetStretch(double s, float outside)
            {
                TrackFrame frame = line.FrameAt(s);
                float stretch = 1f + frame.Curvature * (definition.HalfWidthAt(s) + outside);
                if (!(stretch > 0f)) throw new InvalidOperationException("Stadium offset curve is inverted.");
                return stretch;
            }

            private void Roof(Transform parent)
            {
                // One-metre light strip on the soffit's inward lip; its 2 cm drop avoids coincident dark faces.
                const float LightWidth = 1f, LightDrop = 0.02f;
                var surfaces = new Dictionary<Material, Surface>();
                Surface top = ForMaterial(surfaces, bowlWhite), bottom = ForMaterial(surfaces, detailSteel), lamp = ForMaterial(surfaces, floodLamp);
                for (int i = 0; i < bowlStations.Count; i++)
                {
                    double a = bowlStations[i], b = bowlStations[(i + 1) % bowlStations.Count];
                    float ya = BowlTop(a) + RoofRise, yb = BowlTop(b) + RoofRise;
                    Vector3 inward = line.FrameAt(a).Right + line.FrameAt(b).Right;
                    Profile(top, a, b, BowlBack, ya, BowlBack, yb, RoofFront, ya - RoofFall, RoofFront, yb - RoofFall, Vector3.up);
                    Profile(bottom, a, b, BowlBack, ya - RoofThickness, BowlBack, yb - RoofThickness,
                        RoofFront, ya - RoofFall - RoofThickness, RoofFront, yb - RoofFall - RoofThickness, Vector3.down);
                    Profile(top, a, b, RoofFront, ya - RoofFall, RoofFront, yb - RoofFall,
                        RoofFront, ya - RoofFall - RoofThickness, RoofFront, yb - RoofFall - RoofThickness, inward);
                    Profile(top, a, b, BowlBack, ya, BowlBack, yb, BowlBack, ya - RoofThickness, BowlBack, yb - RoofThickness, -inward);
                    float fallAtLight = RoofFall * (BowlBack - RoofFront - LightWidth) / (BowlBack - RoofFront);
                    Profile(lamp, a, b, RoofFront, ya - RoofFall - RoofThickness - LightDrop, RoofFront, yb - RoofFall - RoofThickness - LightDrop,
                        RoofFront + LightWidth, ya - fallAtLight - RoofThickness - LightDrop, RoofFront + LightWidth, yb - fallAtLight - RoofThickness - LightDrop, Vector3.down);
                }
                SaveSurfaces(parent, "Roof", surfaces);
                // user 2026-11-03 "2층 좌석이 어둡다": let the directional light reach seats under the roof ring.
                // Keep shadow receiving and every other group's renderer settings as built by Baked.
                foreach (MeshRenderer renderer in parent.GetComponentsInChildren<MeshRenderer>())
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            // Parallel-transport circular sections so collars and curved ribs have no axis-switch twist.
            private static void Tube(Surface surface, IReadOnlyList<Vector3> path, float radius, int sides, bool closed)
            {
                if (path.Count < 2) throw new InvalidOperationException("Stadium tube has no length.");
                var rings = new Vector3[path.Count, sides];
                Vector3 previousTangent = Vector3.zero, u = Vector3.zero;
                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 tangent = path[closed ? (i + 1) % path.Count : Mathf.Min(i + 1, path.Count - 1)]
                        - path[closed ? (i + path.Count - 1) % path.Count : Mathf.Max(0, i - 1)];
                    if (tangent.sqrMagnitude < 1e-8f) throw new InvalidOperationException("Stadium tube has a repeated station.");
                    tangent.Normalize();
                    if (i == 0)
                    {
                        // Choose a well-separated initial axis; all later sections transport this same basis.
                        Vector3 axis = Mathf.Abs(Vector3.Dot(tangent, Vector3.forward)) < 0.9f ? Vector3.forward : Vector3.right;
                        u = Vector3.Cross(tangent, axis);
                    }
                    else u = Quaternion.FromToRotation(previousTangent, tangent) * u;
                    u.Normalize();
                    Vector3 v = Vector3.Cross(tangent, u).normalized;
                    previousTangent = tangent;
                    for (int k = 0; k < sides; k++)
                    {
                        float angle = 2f * Mathf.PI * k / sides;
                        rings[i, k] = path[i] + radius * (u * Mathf.Cos(angle) + v * Mathf.Sin(angle));
                    }
                }
                int spans = closed ? path.Count : path.Count - 1;
                for (int i = 0; i < spans; i++)
                    for (int k = 0; k < sides; k++)
                    {
                        int j = (i + 1) % path.Count, l = (k + 1) % sides;
                        Vector3 facing = (rings[i, k] + rings[i, l] - 2f * path[i]) + (rings[j, k] + rings[j, l] - 2f * path[j]);
                        surface.Quad(rings[i, k], rings[j, k], rings[j, l], rings[i, l], facing);
                    }
            }

            // Find an axis-aligned ray's road station by intersecting its centreline chords with the plane through the
            // infield centre. Refuse a curved/oblique intersection: feet and screens rely on the measured cardinal straights.
            private double AxisStation(Vector3 direction)
            {
                Vector2 c = InfieldCentre();
                Vector3 centre = new Vector3(c.x, 0f, c.y), across = Vector3.Cross(Vector3.up, direction);
                for (int i = 0; i < bowlStations.Count; i++)
                {
                    double a = bowlStations[i], b = i + 1 < bowlStations.Count ? bowlStations[i + 1] : definition.LapLength;
                    Vector3 p = line.FrameAt(a).Position, q = line.FrameAt(b).Position;
                    float da = Vector3.Dot(p - centre, across), db = Vector3.Dot(q - centre, across);
                    if ((da > 0f && db > 0f) || (da < 0f && db < 0f) || Mathf.Abs(db - da) < 1e-6f) continue;
                    double s = a + (b - a) * (-da / (db - da));
                    TrackFrame frame = line.FrameAt(s);
                    if (Vector3.Dot(frame.Position - centre, direction) <= 0f) continue;
                    if (Vector3.Dot(-frame.Right, direction) < 0.999f || Mathf.Abs(frame.Curvature) > 1e-6f)
                        throw new InvalidOperationException("Stadium cardinal straight changed; review arch feet and screens.");
                    return s;
                }
                throw new InvalidOperationException("Stadium cardinal ray does not meet the track.");
            }

            private void Arch(Transform parent)
            {
                Vector2 c = InfieldCentre();
                double leftS = AxisStation(Vector3.left), rightS = AxisStation(Vector3.right);
                Vector3 left = BowlPoint(leftS, SkinOffset(0f) + ArchFootClear, 0f);
                Vector3 right = BowlPoint(rightS, SkinOffset(0f) + ArchFootClear, 0f);
                // One symmetric parabola in the vertical x/y plane through the infield centre. Use the larger measured
                // reach on BOTH sides: even a sampled centre slightly off the midpoint leaves both feet beyond the skin.
                left.z = right.z = c.y;
                float leftReach = c.x - left.x, rightReach = right.x - c.x;
                if (!(leftReach > 0f && rightReach > 0f)) throw new InvalidOperationException("Stadium arch feet do not straddle the field.");
                float reach = Mathf.Max(leftReach, rightReach);
                var path = new List<Vector3>();
                int halfCount = Mathf.CeilToInt(reach / ArchStep), count = halfCount * 2;
                for (int i = 0; i <= count; i++)
                {
                    float u = (float)(i - halfCount) / halfCount;
                    path.Add(new Vector3(c.x + reach * u, Ground + ArchHeight * (1f - u * u), c.y));
                }
                // Far above the 9 m corridor ceiling over the road; reject a future layout that violates a 60 m minimum.
                // Over the roof's back edge (the lowest the arch gets over the bowl) it must also clear the roof's top.
                const float MinimumArchRoadHeight = 60f;
                foreach (double s in new[] { leftS, rightS })
                {
                    Vector3 road = line.FrameAt(s).Position;
                    float ratio = (Mathf.Abs(road.x - c.x) + definition.HalfWidthAt(s)) / reach;
                    float clear = Ground + ArchHeight * (1f - ratio * ratio) - CollarRadius - CollarTube - road.y;
                    if (clear < MinimumArchRoadHeight) throw new InvalidOperationException("Stadium arch road clearance is below 60 m: " + clear);
                    float back = Mathf.Abs(BowlPoint(s, BowlBack, 0f).x - c.x) / reach;
                    float overRoof = Ground + ArchHeight * (1f - back * back) - CollarRadius - CollarTube - (Ground + BowlTop(s) + RoofRise);
                    if (overRoof < 1f) throw new InvalidOperationException("Stadium arch sinks into the roof: " + overRoof + " m over its back edge.");
                }
                var surface = new Surface();
                Tube(surface, path, ArchRadius, TubeSides, false);
                float distance = CollarPitch;
                for (int i = 1; i < path.Count; i++)
                {
                    float span = Vector3.Distance(path[i - 1], path[i]);
                    while (distance <= span)
                    {
                        Vector3 centre = Vector3.Lerp(path[i - 1], path[i], distance / span);
                        Vector3 tangent = (path[i] - path[i - 1]).normalized, u = Vector3.Cross(tangent, Vector3.forward).normalized;
                        var ring = new List<Vector3>();
                        for (int k = 0; k < CollarSides; k++)
                        {
                            float angle = 2f * Mathf.PI * k / CollarSides;
                            ring.Add(centre + CollarRadius * (u * Mathf.Cos(angle) + Vector3.forward * Mathf.Sin(angle)));
                        }
                        Tube(surface, ring, CollarTube, TubeSides, true);
                        distance += CollarPitch;
                    }
                    distance -= span;
                }
                surface.Save(parent, "Arch", bowlWhite);
            }

            private void BigScreens(Transform parent)
            {
                // A 30 x 12 m luminous face, a 60 cm frame and a 15 degree downward normal. Local +z faces the pitch.
                const float Frame = 0.6f, Depth = 0.7f, FaceZ = 0.4f, BarDepth = 0.03f, HangerWidth = 0.16f;
                // Abstract score graphics: four bars 2 m apart, progressively 3 m shorter, with 2.5 m side margins.
                const float BarPitch = 2f, BarMargin = 5f, BarShorten = 3f, BarHeight = 0.6f, HangerSpan = 0.35f;
                const int Bars = 4;
                for (int end = -1; end <= 1; end += 2)
                {
                    double s = AxisStation(Vector3.forward * end);
                    Transform screen = Group("Suspended screen " + end, parent);
                    Vector3 position = BowlPoint(s, ScreenOffset, ScreenBottom + ScreenHeight * 0.5f * Mathf.Cos(ScreenTilt * Mathf.Deg2Rad)
                        + FaceZ * 0.5f * Mathf.Sin(ScreenTilt * Mathf.Deg2Rad));
                    screen.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.back * end) * Quaternion.Euler(ScreenTilt, 0f, 0f));
                    Box("Dark frame", screen, new Vector3(0f, 0f, -Depth * 0.5f), new Vector3(ScreenWidth + Frame * 2f, ScreenHeight + Frame * 2f, Depth), detailSteel);
                    Box("Navy luminous face", screen, Vector3.zero, new Vector3(ScreenWidth, ScreenHeight, FaceZ), screenNavy);
                    for (int bar = 0; bar < Bars; bar++)
                        Box("Abstract horizontal bar", screen, new Vector3(0f, (bar - (Bars - 1) * 0.5f) * BarPitch, FaceZ * 0.5f + BarDepth * 0.5f),
                            new Vector3(ScreenWidth - BarMargin - bar * BarShorten, BarHeight, BarDepth), bar % 2 == 0 ? ledMint : ledAmber);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 attachment = screen.TransformPoint(new Vector3(side * ScreenWidth * HangerSpan, ScreenHeight * 0.5f + Frame, -Depth * 0.5f));
                        TrackCoord coord = line.Project(attachment);
                        float outside = -coord.D - definition.HalfWidthAt(coord.S);
                        float underside = Ground + BowlTop(coord.S) + RoofRise - RoofThickness - RoofFall * (BowlBack - outside) / (BowlBack - RoofFront);
                        if (outside < RoofFront || outside > BowlBack || underside <= attachment.y)
                            throw new InvalidOperationException("Stadium screen hanger does not meet the roof soffit.");
                        // Parent is untransformed: attachment and the soffit point are already in root coordinates.
                        Beam("Roof hanger", parent, attachment, new Vector3(attachment.x, underside, attachment.z), HangerWidth, detailSteel);
                    }
                }
            }

            // Sample long narrow modules like FINGER's band, rather than a 3 m bounding circle that would reject a
            // board only 1.6 m off the edge. Half-metre stations include both ends and are reserved with the same radius.
            private Vector3 TracksidePoint(double s, int side, float edgeOffset)
            {
                TrackFrame frame = line.FrameAt(s);
                return frame.Position + frame.Right * side * (definition.HalfWidthAt(s) + edgeOffset);
            }

            private void ReserveGapEvents()
            {
                // Match WaterJumpBarrier's outer beam span (1.15..4.75 m) and PoleVault's 1.6 m square bases at
                // half + 2.5 m, including Anchor.Offset. The existing event geometry itself is left untouched.
                const float BarrierFrom = 1.15f, BarrierTo = 4.75f, BarrierRadius = 0.25f, VaultRadius = 1.2f;
                foreach ((double start, double end) in definition.Gaps())
                {
                    TrackDefinition.Anchor? vault = null;
                    foreach (TrackDefinition.Anchor anchor in definition.Anchors)
                        if (anchor.S > start && anchor.S < end) vault = anchor;
                    if (vault.HasValue)
                    {
                        TrackDefinition.Anchor anchor = vault.Value;
                        TrackFrame frame = line.FrameAt(anchor.S);
                        float reach = definition.HalfWidthAt(anchor.S) + 2.5f;
                        for (int side = -1; side <= 1; side += 2)
                            near.Reserve(frame.Position + frame.Right * (anchor.Offset + side * reach), VaultRadius);
                    }
                    else
                        for (int side = -1; side <= 1; side += 2)
                            for (float offset = BarrierFrom; offset <= BarrierTo; offset += 0.4f)
                                near.Reserve(TracksidePoint(start, side, offset), BarrierRadius);
                }
            }

            private bool NearGap(double from, double to)
            {
                // Include the lap seam: a later layout may put a pit next to the finish.
                foreach ((double start, double end) in definition.Gaps())
                    for (int lap = -1; lap <= 1; lap++)
                        if (to >= start + lap * definition.LapLength - GapBuffer && from <= end + lap * definition.LapLength + GapBuffer) return true;
                return false;
            }

            private void TracksideBoards(Transform parent)
            {
                const float MinimumStraight = 12f, EndInset = 1f, Station = 0.5f;
                // Nominal face 0.4..1.2 m leaves 5 cm within the requested 0.35..1.25 m envelope for the eased grades
                // on the opening straights. Its six-metre chord differs from those road heights by at most 2.25 cm.
                // Feet end at its bottom. Four renderers per module.
                const float Bottom = 0.4f, Height = 0.8f, ScreenProud = 0.05f;
                double s0 = 0d;
                int index = 0;
                foreach (TrackDefinition.Segment segment in definition.Segments)
                {
                    double start = s0;
                    s0 += segment.Length;
                    if (segment.IsArc || segment.IsGap || segment.Length < MinimumStraight) continue;
                    for (double from = start + EndInset; from + BoardLength <= s0 - EndInset; from += BoardLength + BoardGap)
                    {
                        double to = from + BoardLength;
                        if (NearGap(from, to)) continue;
                        bool available = true;
                        for (double s = from; s <= to; s += Station)
                            if (!near.Available(TracksidePoint(s, -1, BoardFront + BoardDepth * 0.5f), BoardDepth * 0.5f)) available = false;
                        if (!available) continue;
                        Vector3 a = TracksidePoint(from, -1, BoardFront + BoardDepth * 0.5f);
                        Vector3 b = TracksidePoint(to, -1, BoardFront + BoardDepth * 0.5f);
                        Transform board = Group("LED module " + index, parent);
                        // TrackFrame.Forward is horizontal (TrackFrame.cs); the endpoint chord also follows the rise.
                        board.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(b - a));
                        board.localScale = new Vector3(1f, 1f, (b - a).magnitude / BoardLength);
                        // Local +x faces the road from its outside; the screen's front is the 1.6 m front. The frame's stands
                        // ScreenProud behind it: on one plane the two flickered (user 2026-11-06).
                        Box("Dark board frame", board, new Vector3(-ScreenProud * 0.5f, Bottom + Height * 0.5f, 0f), new Vector3(BoardDepth - ScreenProud, Height, BoardLength), detailSteel);
                        Box("LED screen", board, new Vector3(BoardDepth * 0.5f - ScreenProud, Bottom + Height * 0.5f, 0f), new Vector3(ScreenProud * 2f, Height - 0.16f, BoardLength - 0.2f), Pick(index * 7 + 113, ledMint, ledAmber, ledMagenta));
                        for (int foot = -1; foot <= 1; foot += 2)
                            Box("Board foot", board, new Vector3(0f, Bottom * 0.5f, foot * 2f), new Vector3(BoardDepth, Bottom, 0.3f), detailSteel);
                        for (double s = from; s <= to; s += Station)
                            near.Reserve(TracksidePoint(s, -1, BoardFront + BoardDepth * 0.5f), BoardDepth * 0.5f);
                        index++;
                    }
                }
            }

            private void FinishGantry(Transform parent)
            {
                const double FinishS = 0d;
                const float BeamHeight = 1.2f, CameraOffset = 2.6f, CameraHeight = 4f;
                TrackFrame frame = line.FrameAt(FinishS);
                parent.SetPositionAndRotation(frame.Position, Flat(frame.Forward));
                float pillar = definition.HalfWidthAt(FinishS) + RoadMeshBuilder.GuardThickness + PillarClear + PillarWidth * 0.5f;
                // With the 0.3 m guard, pillar centre is edge + 1.7 m, far face + 2.1 m < the eave's + 3.8 m.
                // Thus even at the eave's 8.4 m height the outer pillar is 1.7 m inside it (user 2026-11-03).
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Timing pillar", parent, new Vector3(side * pillar, (OverheadClear + BeamHeight) * 0.5f, 0f), new Vector3(PillarWidth, OverheadClear + BeamHeight, PillarWidth), detailSteel);
                    near.Reserve(frame.Position + frame.Right * side * pillar, 0.6f);
                }
                float span = pillar * 2f + PillarWidth;
                Box("Timing beam", parent, new Vector3(0f, OverheadClear + BeamHeight * 0.5f, 0f), new Vector3(span, BeamHeight, 0.6f), ink);
                // Two rows of alternating white tiles on the beam's approaching face; dark cells are the beam itself.
                int tiles = Mathf.CeilToInt(span / BeamHeight);
                for (int tile = 0; tile < tiles; tile++)
                    Box("Finish chequer", parent, new Vector3(-span * 0.5f + (tile + 0.5f) * span / tiles, OverheadClear + (tile % 2 + 0.5f) * BeamHeight * 0.5f, -0.33f), new Vector3(span / tiles, BeamHeight * 0.5f, 0.06f), white);
                Transform clock = Group("Timing clock", parent);
                clock.localPosition = new Vector3(0f, OverheadClear + BeamHeight + 0.9f, 0f);
                Box("Black clock plate", clock, Vector3.zero, new Vector3(5f, 1.6f, 0.3f), ink);
                // Three static zero-like digits and a colon, made from mint bars, with no text/runtime component.
                for (int digit = 0; digit < 3; digit++)
                {
                    float x = -1.6f + digit * 1.5f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Box("Clock horizontal segment", clock, new Vector3(x, side * 0.5f, -0.19f), new Vector3(0.65f, 0.12f, 0.08f), ledMint);
                        Box("Clock vertical segment", clock, new Vector3(x + side * 0.32f, 0f, -0.19f), new Vector3(0.12f, 1f, 0.08f), ledMint);
                    }
                }
                for (int dot = -1; dot <= 1; dot += 2)
                    Box("Clock colon", clock, new Vector3(-0.85f, dot * 0.22f, -0.19f), new Vector3(0.12f, 0.12f, 0.08f), ledMint);
                // At edge + 2.6 m the camera's 0.6 m head also stays inside the eave (+ 3.8 m).
                float camera = definition.HalfWidthAt(FinishS) + CameraOffset;
                Box("Photo finish post", parent, new Vector3(-camera, CameraHeight * 0.5f, 0f), new Vector3(0.18f, CameraHeight, 0.18f), metal);
                Box("Photo finish camera", parent, new Vector3(-camera, CameraHeight, 0f), new Vector3(0.6f, 0.35f, 0.5f), ink);
                near.Reserve(frame.Position - frame.Right * camera, 0.4f);
            }

            private void CornersAndInfield(Transform parent)
            {
                double s0 = 0d;
                int index = 0;
                foreach (TrackDefinition.Segment segment in definition.Segments)
                {
                    double start = s0;
                    s0 += segment.Length;
                    if (!segment.IsArc) continue;
                    if (!(segment.Degrees > 0f)) throw new InvalidOperationException("Stadium corner furniture requires right turns.");
                    float half = definition.HalfWidthAt(start + segment.Length * 0.5d);
                    // SKYWAY's evenly divided arc placement, but spacing measured on each offset row's own radius.
                    int hurdles = Mathf.Max(1, Mathf.RoundToInt(segment.Length * (segment.Radius + half + HurdleOffset) / segment.Radius / HurdlePitch));
                    for (int k = 0; k < hurdles; k++)
                    {
                        double s = start + segment.Length * (k + 0.5d) / hurdles;
                        TrackFrame frame = line.FrameAt(s);
                        Transform hurdle = Group("Trackside hurdle " + index++, parent);
                        hurdle.SetPositionAndRotation(TracksidePoint(s, -1, HurdleOffset), Flat(frame.Forward));
                        // Length 1.2 m along the travel direction, top 0.84 m; one dark central band, two legs.
                        const float HurdleWidth = 1.2f, HurdleTop = 0.84f, BarHeight = 0.12f;
                        Box("White hurdle bar", hurdle, new Vector3(0f, HurdleTop - BarHeight * 0.5f, 0f), new Vector3(0.16f, BarHeight, HurdleWidth), white);
                        Box("Black hurdle band", hurdle, new Vector3(0f, HurdleTop - BarHeight * 0.5f, 0f), new Vector3(0.18f, BarHeight, 0.3f), ink);
                        for (int leg = -1; leg <= 1; leg += 2)
                            Box("Hurdle leg", hurdle, new Vector3(0f, (HurdleTop - BarHeight) * 0.5f, leg * 0.48f), new Vector3(0.14f, HurdleTop - BarHeight, 0.14f), metal);
                    }
                    float innerRadius = segment.Radius - half - PadOffset;
                    if (!(innerRadius > 0f)) throw new InvalidOperationException("Stadium inner padding row has no positive radius.");
                    int pads = Mathf.Max(1, Mathf.RoundToInt(segment.Length * innerRadius / segment.Radius / PadPitch));
                    for (int k = 0; k < pads; k++)
                    {
                        double s = start + segment.Length * (k + 0.5d) / pads;
                        TrackFrame frame = line.FrameAt(s);
                        // Short 0.7 x 0.2 m pads: at 45 degrees their world AABB reaches 0.45 m toward the road,
                        // leaving ~0.75 m from the 1.2 m offset, above RoadCorridor's 0.7 m bounds margin.
                        Transform pad = Group("Inner corner pad", parent);
                        pad.SetPositionAndRotation(TracksidePoint(s, 1, PadOffset), Flat(frame.Forward));
                        Box("Navy padding block", pad, new Vector3(0f, 0.4f, 0f), new Vector3(0.2f, 0.8f, 0.7f), padding);
                    }
                }
                InfieldFurniture(parent);
            }

            // The existing football field is centred by InfieldCentre, its long axis +z. FrameAt(0)'s horizontal axes
            // are those axes; fail if that contract changes rather than silently furnishing a different field.
            private void InfieldFurniture(Transform parent)
            {
                const float BenchOffset = 4f, BenchLength = 10f, BenchDepth = 2f, CanopyHeight = 2.7f;
                const float FlagOffset = 4f, FlagPitch = 7f, FlagHeight = 6f;
                TrackFrame frame = line.FrameAt(0d);
                if (Vector3.Dot(frame.Right, Vector3.right) < 0.999f || Vector3.Dot(frame.Forward, Vector3.forward) < 0.999f)
                    throw new InvalidOperationException("Stadium field axes changed; review benches, flags and floodlights.");
                Vector2 c = InfieldCentre();
                Vector3 centre = new Vector3(c.x, InfieldTop, c.y);
                for (int side = -1; side <= 1; side += 2)
                {
                    Transform bench = Group("Team bench " + side, parent);
                    bench.SetPositionAndRotation(centre + frame.Right * side * (FieldWidth * 0.5f + BenchOffset), Flat(-frame.Right * side));
                    RequireInfieldFootprint(bench, new Vector3(BenchLength, 0f, BenchDepth));
                    Box("Bench seat", bench, new Vector3(0f, 0.55f, 0f), new Vector3(BenchLength, 0.18f, 0.6f), padding);
                    Box("Bench back", bench, new Vector3(0f, 1f, -0.3f), new Vector3(BenchLength, 0.7f, 0.15f), padding);
                    for (int leg = -1; leg <= 1; leg += 2)
                    {
                        Box("Bench support", bench, new Vector3(leg * 3.5f, 0.25f, 0f), new Vector3(0.2f, 0.5f, 0.6f), metal);
                        for (int end = -1; end <= 1; end += 2)
                            Box("Canopy post", bench, new Vector3(leg * (BenchLength * 0.5f - 0.2f), CanopyHeight * 0.5f, end * (BenchDepth * 0.5f - 0.15f)), new Vector3(0.12f, CanopyHeight, 0.12f), metal);
                    }
                    Box("Team canopy", bench, new Vector3(0f, CanopyHeight, 0f), new Vector3(BenchLength, 0.2f, BenchDepth), side < 0 ? endZoneNavy : endZoneMint);
                    for (int flag = 0; flag < 6; flag++)
                    {
                        Transform pole = Group("End zone flag " + side + " " + flag, parent);
                        pole.SetPositionAndRotation(centre + frame.Right * ((flag - 2.5f) * FlagPitch) + frame.Forward * side * (60f * Yard + FlagOffset), Flat(frame.Forward));
                        // The banner extends 1.2 m toward +x; check all corners of that full footprint, not just the pole.
                        RequireInfieldFootprint(pole, new Vector3(2.6f, 0f, 0.3f));
                        Box("Flag pole", pole, new Vector3(0f, FlagHeight * 0.5f, 0f), new Vector3(0.12f, FlagHeight, 0.12f), metal);
                        Box("Team flag", pole, new Vector3(0.6f, FlagHeight - 0.7f, 0f), new Vector3(1.2f, 1.2f, 0.08f), Pick(flag + side * 17 + 211, endZoneNavy, endZoneMint, flagAmber));
                    }
                }
            }

            private void RequireInfieldFootprint(Transform item, Vector3 size)
            {
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                        if (!InInfield(item.TransformPoint(new Vector3(x * size.x * 0.5f, 0f, z * size.z * 0.5f))))
                            throw new InvalidOperationException(item.name + " does not fit the stadium's infield.");
            }

            // At the origin and unscaled, casting and taking shadows, as placed by the art pass; the cleared meshes and the
            // new infield are saved beside the baked ones (Art/Models/StadiumExterior_*).
            private void SportsComplex(Transform parent)
            {
                foreach ((string mesh, string material) in Trimmed)
                {
                    Mesh cleared = WithoutInfield(ExteriorAssets.Require<Mesh>(ModelFolder + "StadiumSports_" + mesh + ".asset"), Array.IndexOf(OldBoards, mesh) >= 0);
                    cleared.name = "StadiumExterior_" + mesh;
                    Baked(parent, mesh, cleared, Require(material));
                }
                Baked(parent, "Infield grass", InfieldMesh(), infieldGrass);
            }


            private static void Baked(Transform parent, string name, Mesh mesh, Material material)
            {
                var part = new GameObject(name);
                part.transform.SetParent(parent, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            private static Material Require(string material) => ExteriorAssets.Require<Material>(ExteriorAssets.MaterialFolder + material + ".mat");

            // The meshes that hold the old end scoreboards (o 23-32 m, up to 23 m high). The height cut below left their
            // lower halves standing, a sign cut in two under the new bowl (first rebuild 2026-11-03); beyond o=20 these
            // two go whole. Their side parts (o <= 19 m) stay.
            private static readonly string[] OldBoards = { "Mint", "Ink" };

            // One pass over triangles: remove each triangle wholly in the infield, buried outside o=30, or above the
            // first-tier roof cutoff (o>20 and y>12; for oldBoards, o>20 at any height). Keep crossing triangles rather
            // than cutting through first-tier seats.
            private Mesh WithoutInfield(Mesh source, bool oldBoards)
            {
                const float OldRoofOffset = 20f, OldRoofHeight = 12f;
                Vector3[] vertices = source.vertices;
                var inside = new bool[vertices.Length];
                var buried = new bool[vertices.Length];
                var roof = new bool[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    TrackCoord coord = line.Project(vertices[i]);
                    float outside = -coord.D - definition.HalfWidthAt(coord.S);
                    inside[i] = InInfield(vertices[i]);
                    buried[i] = outside > BowlInner - Overhang;
                    roof[i] = outside > OldRoofOffset && (oldBoards || vertices[i].y > OldRoofHeight);
                }
                var remap = new int[vertices.Length];
                for (int i = 0; i < remap.Length; i++) remap[i] = -1;
                var used = new List<int>();
                var compact = new List<Vector3>();
                var submeshes = new List<int[]>();
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    int[] triangles = source.GetTriangles(sub);
                    var kept = new List<int>();
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                        if ((inside[a] && inside[b] && inside[c]) || (buried[a] && buried[b] && buried[c]) || (roof[a] && roof[b] && roof[c])) continue;
                        for (int k = 0; k < 3; k++)
                        {
                            int index = triangles[i + k];
                            if (remap[index] < 0)
                            {
                                remap[index] = compact.Count;
                                compact.Add(vertices[index]);
                                used.Add(index);
                            }
                            kept.Add(remap[index]);
                        }
                    }
                    submeshes.Add(kept.ToArray());
                }
                // Nothing left means the part belongs under the bowl entirely: leave it out of Trimmed, don't save it empty.
                if (compact.Count == 0) throw new InvalidOperationException("Stadium baked part is empty after trimming: " + source.name);
                var copy = new Mesh { name = source.name, indexFormat = IndexFormat.UInt32 };
                copy.SetVertices(compact);
                // Preserve the baked shading/UVs. Unreferenced vertices must go: RoadCorridor inspects every vertex,
                // including any old field goal or roof vertex no longer used by a triangle.
                if (source.normals.Length != 0) copy.normals = Remap(source.normals, used);
                if (source.tangents.Length != 0) copy.tangents = Remap(source.tangents, used);
                if (source.colors.Length != 0) copy.colors = Remap(source.colors, used);
                for (int channel = 0; channel < 8; channel++)
                {
                    var uv = new List<Vector4>();
                    source.GetUVs(channel, uv);
                    if (uv.Count != 0) copy.SetUVs(channel, new List<Vector4>(Remap(uv, used)));
                }
                copy.subMeshCount = submeshes.Count;
                for (int sub = 0; sub < submeshes.Count; sub++) copy.SetTriangles(submeshes[sub], sub);
                copy.RecalculateBounds();
                return copy;
            }

            private static T[] Remap<T>(IReadOnlyList<T> source, IReadOnlyList<int> used)
            {
                var result = new T[used.Count];
                for (int i = 0; i < used.Count; i++) result[i] = source[used[i]];
                return result;
            }

            // Whether position (x and z) lies inside the infield's convex outline.
            private bool InInfield(Vector3 position)
            {
                int sign = 0;
                for (int i = 0; i < infield.Count; i++)
                {
                    Vector2 a = infield[i], b = infield[(i + 1) % infield.Count];
                    float cross = (b.x - a.x) * (position.z - a.y) - (b.y - a.y) * (position.x - a.x);
                    if (Mathf.Abs(cross) < 1e-6f) continue;
                    int side = cross > 0f ? 1 : -1;
                    if (sign == 0) sign = side;
                    else if (side != sign) return false;
                }
                return true;
            }

            private Vector2 InfieldCentre()
            {
                Vector2 sum = Vector2.zero;
                foreach (Vector2 p in infield) sum += p;
                return sum / infield.Count;
            }

            // A flat fan over the infield's outline, facing up.
            private Mesh InfieldMesh()
            {
                Vector2 centre = InfieldCentre();
                var vertices = new List<Vector3> { new Vector3(centre.x, InfieldTop, centre.y) };
                foreach (Vector2 p in infield) vertices.Add(new Vector3(p.x, InfieldTop, p.y));
                var triangles = new List<int>();
                for (int i = 0; i < infield.Count; i++)
                {
                    int a = i + 1, b = (i + 1) % infield.Count + 1;
                    // Unity's front face's normal is (b - a) x (c - a); keep it pointing up.
                    bool up = Vector3.Cross(vertices[a] - vertices[0], vertices[b] - vertices[0]).y > 0f;
                    triangles.Add(0);
                    triangles.Add(up ? a : b);
                    triangles.Add(up ? b : a);
                }
                var uvs = new List<Vector2>();
                foreach (Vector3 v in vertices) uvs.Add(new Vector2(v.x, v.z));
                var mesh = new Mesh { name = "StadiumExterior_Infield" };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.SetUVs(0, uvs);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }

            // An American football field on the infield's long axis (z): mown stripes every five yards, the two end zones,
            // yard lines, the side and end lines, hash marks every yard, a midfield disc and a goal post behind each end
            // line (user 2026-11-02, "중앙에 … 미식축구용 경기장").
            private void FootballField(Transform parent)
            {
                Vector2 centre = InfieldCentre();
                Transform field = Group("American football field", parent);
                field.position = new Vector3(centre.x, 0f, centre.y);
                float half = 60f * Yard;
                if (!InInfield(new Vector3(centre.x - FieldWidth * 0.5f - 2f, 0f, centre.y - half - 3f)) || !InInfield(new Vector3(centre.x + FieldWidth * 0.5f + 2f, 0f, centre.y + half + 3f)))
                    throw new InvalidOperationException("The football field does not fit the stadium's infield.");
                const float Turf = InfieldTop + 0.04f, Paint = InfieldTop + 0.07f;
                for (int stripe = 0; stripe < 20; stripe++)
                {
                    float z = -50f * Yard + (stripe + 0.5f) * 5f * Yard;
                    Box("Mown stripe", field, new Vector3(0f, Turf - 0.02f, z), new Vector3(FieldWidth, 0.04f, 5f * Yard), stripe % 2 == 0 ? fieldLight : fieldDark);
                }
                for (int end = -1; end <= 1; end += 2)
                    Box("End zone", field, new Vector3(0f, Turf - 0.02f, end * 55f * Yard), new Vector3(FieldWidth, 0.04f, 10f * Yard), end < 0 ? endZoneNavy : endZoneMint);
                for (int yard = -50; yard <= 50; yard += 5)
                    Box(yard == 0 ? "Fifty-yard line" : "Yard line", field, new Vector3(0f, Paint, yard * Yard), new Vector3(FieldWidth, 0.02f, yard % 50 == 0 ? 0.3f : 0.12f), fieldLine);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Sideline", field, new Vector3(side * (FieldWidth * 0.5f + 0.9f), Paint, 0f), new Vector3(1.8f, 0.02f, 120f * Yard + 3.6f), fieldLine);
                    Box("End line", field, new Vector3(0f, Paint, side * (half + 0.9f)), new Vector3(FieldWidth + 3.6f, 0.02f, 1.8f), fieldLine);
                    for (int yard = -49; yard <= 49; yard++)
                    {
                        if (yard % 5 == 0) continue;
                        Box("Hash mark", field, new Vector3(side * 2.82f, Paint, yard * Yard), new Vector3(0.6f, 0.02f, 0.1f), fieldLine);
                        Box("Sideline mark", field, new Vector3(side * (FieldWidth * 0.5f - 0.5f), Paint, yard * Yard), new Vector3(0.6f, 0.02f, 0.1f), fieldLine);
                    }
                    GoalPost(field, side * (half + 1.8f), side);
                }
                Shape("Midfield disc", field, new Vector3(0f, Paint + 0.005f, 0f), new Vector3(9f, 0.02f, 9f), endZoneMint, Cylinder);
                Shape("Midfield ring", field, new Vector3(0f, Paint, 0f), new Vector3(10f, 0.02f, 10f), fieldLine, Cylinder);
            }

            // A yellow goal post: a padded post behind the end line, a gooseneck reaching over it, the crossbar 10 ft up and
            // the uprights 18 ft 6 in apart.
            private void GoalPost(Transform parent, float z, int end)
            {
                const float Bar = 3.05f, Spread = 5.64f, Upright = 9f;
                Transform post = Group("Goal post", parent);
                post.localPosition = new Vector3(0f, InfieldTop, z);
                Box("Post padding", post, new Vector3(0f, 1f, end * 1.5f), new Vector3(0.6f, 2f, 0.6f), padding);
                Box("Post", post, new Vector3(0f, Bar * 0.5f, end * 1.5f), new Vector3(0.25f, Bar, 0.25f), goalPost);
                Box("Gooseneck", post, new Vector3(0f, Bar, end * 0.75f), new Vector3(0.2f, 0.2f, 1.5f), goalPost);
                Box("Crossbar", post, new Vector3(0f, Bar, 0f), new Vector3(Spread, 0.18f, 0.18f), goalPost);
                for (int side = -1; side <= 1; side += 2)
                    Box("Upright", post, new Vector3(side * Spread * 0.5f, Bar + Upright * 0.5f, 0f), new Vector3(0.12f, Upright, 0.12f), goalPost);
            }

            // The gaps as athletics events (user 2026-11-02, "단순히 낙사구간이라기 보단 스타디움에서 어떠한 구간"): water fills
            // each pit, so a runner who falls lands in the water; the gap with a grapple anchor over it is a pole vault,
            // its bar across the road at the anchor's height between two uprights; the others are steeplechase water
            // jumps, the black and white barrier standing out beside the road at the take-off.
            private void GapEvents(Transform parent)
            {
                int index = 0;
                foreach ((double start, double end) in definition.Gaps())
                {
                    double middle = (start + end) * 0.5d;
                    TrackFrame frame = line.FrameAt(middle);
                    float half = definition.HalfWidthAt(middle);
                    Transform pit = Group("Water pit " + index++, parent);
                    pit.SetPositionAndRotation(frame.Position, Flat(frame.Forward));
                    Box("Water", pit, new Vector3(0f, WaterTop - 0.15f, 0f), new Vector3(half * 2f + 1.2f, 0.3f, (float)(end - start) + 0.6f), water);
                    TrackDefinition.Anchor? vault = null;
                    foreach (TrackDefinition.Anchor anchor in definition.Anchors)
                        if (anchor.S > start && anchor.S < end) vault = anchor;
                    if (vault.HasValue) PoleVault(parent, vault.Value);
                    else WaterJumpBarrier(parent, start, half);
                }
            }

            private void PoleVault(Transform parent, TrackDefinition.Anchor anchor)
            {
                TrackFrame frame = line.FrameAt(anchor.S);
                float half = definition.HalfWidthAt(anchor.S);
                Transform vault = Group("Pole vault", parent);
                vault.SetPositionAndRotation(frame.Position + frame.Right * anchor.Offset, Flat(frame.Forward));
                float reach = half + 2.5f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Upright base", vault, new Vector3(side * reach, 0.3f, 0f), new Vector3(1.6f, 0.6f, 1.6f), ink);
                    Box("Upright", vault, new Vector3(side * reach, (anchor.Height + 0.8f) * 0.5f, 0f), new Vector3(0.3f, anchor.Height + 0.8f, 0.3f), metal);
                    Box("Bar peg", vault, new Vector3(side * (reach - 0.3f), anchor.Height, 0f), new Vector3(0.3f, 0.12f, 0.12f), metal);
                }
                // A yellow and white banded bar.
                int bands = Mathf.CeilToInt(reach * 2f);
                for (int b = 0; b < bands; b++)
                {
                    float x0 = -reach + b * reach * 2f / bands;
                    Box("Vault bar", vault, new Vector3(x0 + reach / bands, anchor.Height, 0f), new Vector3(reach * 2f / bands, 0.1f, 0.1f), b % 2 == 0 ? barYellow : white);
                }
            }

            // Black and white barrier beams on posts either side of the road, in line with the take-off edge.
            private void WaterJumpBarrier(Transform parent, double start, float half)
            {
                TrackFrame frame = line.FrameAt(start);
                Transform barrier = Group("Water jump barrier", parent);
                barrier.SetPositionAndRotation(frame.Position, Flat(frame.Forward));
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int post = 0; post < 2; post++)
                        Box("Barrier post", barrier, new Vector3(side * (half + 1.4f + post * 3f), 0.45f, 0f), new Vector3(0.15f, 0.9f, 0.15f), ink);
                    for (int band = 0; band < 6; band++)
                        Box("Barrier beam", barrier, new Vector3(side * (half + 1.15f + band * 0.6f + 0.3f), 0.84f, 0f), new Vector3(0.6f, 0.13f, 0.13f), band % 2 == 0 ? ink : white);
                }
            }
        }
    }
}
