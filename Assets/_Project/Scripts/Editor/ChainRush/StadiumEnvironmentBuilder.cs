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
    public static class StadiumEnvironmentBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/StadiumExterior.prefab";
        public const string ScenePath = "Assets/_Project/Scenes/ChainRushTrackStadium.unity";
        public const string LookPath = "Assets/_Project/Data/TrackLook_Stadium.asset";
        private const string DefinitionPath = "Assets/_Project/Data/Circuit_Stadium.asset";
        private const string ModelFolder = "Assets/_Project/Art/Models/";
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
            // The new infield's grass stands this far inside the track's inner edge, its top here.
            private const float InfieldInset = 0.8f;
            private const float InfieldTop = -0.17f;
            // An American football field (yards in metres): 100 yards between the goal lines, 10-yard end zones, 53 1/3
            // yards across.
            private const float Yard = 0.9144f;
            private const float FieldWidth = 160f / 3f * Yard;
            // Water in the pits under the gaps, just above the baked ground (-0.21 m).
            private const float WaterTop = -0.18f;
            // The baked meshes kept whole, and those the old infield is cut out of: the oval's lane lines, its small pitch's
            // lines and goals (user 2026-11-02, the infield becomes a football field). The oval's clay and the pitch's turf
            // (StadiumSports_Clay, _TurfLight) are left out.
            private static readonly (string Mesh, string Material)[] Kept =
            {
                ("Concrete", "M_StadiumSportsConcrete"), ("TurfDark", "M_StadiumSportsTurfDark"), ("SeatBlue", "M_StadiumSportsSeatBlue"),
                ("Steel", "M_ArtStadiumSteel"), ("Mint", "M_ArtStadiumMint"), ("SeatMuted", "M_ArtStadiumSeats"),
            };
            private static readonly (string Mesh, string Material)[] Cleared =
            {
                ("White", "M_StadiumSportsWhite"), ("Ink", "M_ArtStadiumInk"), ("Amber", "M_ArtStadiumAmber"),
            };
            private readonly TrackDefinition definition;
            private readonly Centerline line;
            private readonly RoadCorridor corridor;
            // The infield's outline in x and z, a convex loop just inside the track.
            private readonly List<Vector2> infield = new List<Vector2>();
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

            internal Layout(TrackDefinition definition)
            {
                this.definition = definition;
                line = definition.BuildCenterline(Vector3.zero, 0f);
                corridor = new RoadCorridor(definition, line, 0.5d, 1.5f);
                // The loop turns right, so its inside is to the right of the centerline.
                foreach (TrackDefinition.Segment segment in definition.Segments)
                    if (segment.IsArc && segment.Degrees < 0f) throw new InvalidOperationException("Stadium layout changed: a left turn; review the infield.");
                for (double s = 0d; s < definition.LapLength; s += 1d)
                {
                    TrackFrame frame = line.FrameAt(s);
                    Vector3 p = frame.Position + frame.Right * (definition.HalfWidthAt(s) + InfieldInset);
                    infield.Add(new Vector2(p.x, p.z));
                }
            }

            // The baked complex surrounds the road, so its world bounds always hold it: it is checked vertex by vertex.
            internal void ValidateClearance(GameObject root)
            {
                Transform complex = root.transform.Find(ComplexGroup);
                if (complex == null) throw new InvalidOperationException("Stadium complex group is missing: " + ComplexGroup);
                corridor.ValidateClearance(root, Ground, byVertex: complex);
            }

            internal GameObject Build(Scene scene)
            {
                var root = new GameObject("Stadium Exterior");
                SceneManager.MoveGameObjectToScene(root, scene);
                SportsComplex(Group(ComplexGroup, root.transform));
                FootballField(Group("02 Football field", root.transform));
                GapEvents(Group("03 Water jump and pole vault", root.transform));
                return root;
            }

            // At the origin and unscaled, casting and taking shadows, as placed by the art pass; the cleared meshes and the
            // new infield are saved beside the baked ones (Art/Models/StadiumExterior_*).
            private void SportsComplex(Transform parent)
            {
                foreach ((string mesh, string material) in Kept)
                    Baked(parent, mesh, ExteriorAssets.Require<Mesh>(ModelFolder + "StadiumSports_" + mesh + ".asset"), Require(material));
                foreach ((string mesh, string material) in Cleared)
                {
                    Mesh cleared = WithoutInfield(ExteriorAssets.Require<Mesh>(ModelFolder + "StadiumSports_" + mesh + ".asset"));
                    try
                    {
                        Baked(parent, mesh, ExteriorAssets.SaveMesh(cleared, ModelFolder + "StadiumExterior_" + mesh + ".asset"), Require(material));
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(cleared);
                    }
                }
                Mesh grass = InfieldMesh();
                try
                {
                    Baked(parent, "Infield grass", ExteriorAssets.SaveMesh(grass, ModelFolder + "StadiumExterior_Infield.asset"), infieldGrass);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(grass);
                }
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

            // A copy of source without the triangles that lie wholly over the infield.
            private Mesh WithoutInfield(Mesh source)
            {
                Mesh copy = UnityEngine.Object.Instantiate(source);
                Vector3[] vertices = source.vertices;
                var inside = new bool[vertices.Length];
                for (int i = 0; i < vertices.Length; i++) inside[i] = InInfield(vertices[i]);
                var kept = new List<int>();
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    int[] triangles = source.GetTriangles(sub);
                    kept.Clear();
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        if (inside[triangles[i]] && inside[triangles[i + 1]] && inside[triangles[i + 2]]) continue;
                        kept.Add(triangles[i]);
                        kept.Add(triangles[i + 1]);
                        kept.Add(triangles[i + 2]);
                    }
                    copy.SetTriangles(kept, sub);
                }
                return copy;
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
