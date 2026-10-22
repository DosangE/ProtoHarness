using System;
using System.IO;
using ProtoHarness.ChainRush.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProtoHarness.Editor.ChainRush
{
    // Decoration only, like ChainRushTrackStadium: no road, physics or gameplay components are saved.
    // The prefab is durable; the new additive scene is deliberately left for the user's normal Save Scene command.
    public static class SkywayEnvironmentBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/SkywayExterior.prefab";
        public const string ScenePath = "Assets/_Project/Scenes/ChainRushTrackSkyway.unity";
        private const string DefinitionPath = "Assets/_Project/Data/Circuit_Skyway.asset";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/";
        private const float Ground = -1.3f;

        [MenuItem("ProtoHarness/Chain Rush/Create Skyway Exterior")]
        public static void CreateExterior()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before creating the exterior.");
            if (File.Exists(PrefabPath) || File.Exists(ScenePath))
                throw new InvalidOperationException("Skyway exterior already exists. Open it instead of replacing it.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the open scenes before creating the Skyway exterior.");
            TrackDefinition definition = Require<TrackDefinition>(DefinitionPath);
            if (!definition.TryValidate(out string error)) throw new InvalidOperationException(error);
            var layout = new Layout(definition);
            Scene scene = default;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (candidate.path.Length == 0 && candidate.name == "ChainRushTrackSkyway" && candidate.rootCount == 0)
                    scene = candidate;
            }
            if (!scene.IsValid()) scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            scene.name = "ChainRushTrackSkyway";
            if (SceneManager.GetActiveScene().handle != scene.handle && !SceneManager.SetActiveScene(scene))
                throw new InvalidOperationException("Cannot activate the new decoration scene.");
            GameObject root = layout.Build();
            Validate(root);
            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
            if (prefab == null) throw new IOException("Could not save the Skyway exterior prefab.");
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log($"Skyway exterior: prefab saved at {PrefabPath}; {root.GetComponentsInChildren<Renderer>(true).Length} renderers; physics=0; gameplay scripts=0. Save the open scene as {ScenePath}, add it to Build Profiles, then connect the lobby.", root);
        }

        // Lays the exterior out again on the current Circuit_Skyway (its layout and road width changed, DECISIONS 2026-10-22)
        // and writes it over the existing prefab, so its GUID and the decoration scene's instance stay. Built in a preview
        // scene: the open scenes are not touched.
        [MenuItem("ProtoHarness/Chain Rush/Rebuild Skyway Exterior")]
        public static void RebuildExterior()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before rebuilding the exterior.");
            if (!File.Exists(PrefabPath)) throw new InvalidOperationException("There is no Skyway exterior to rebuild; use Create Skyway Exterior.");
            TrackDefinition definition = Require<TrackDefinition>(DefinitionPath);
            if (!definition.TryValidate(out string error)) throw new InvalidOperationException(error);
            string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new Layout(definition).Build(preview);
                Validate(root);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                if (!saved || prefab == null) throw new IOException("Could not save the Skyway exterior prefab.");
                if (AssetDatabase.AssetPathToGUID(PrefabPath) != guid) throw new InvalidOperationException("The rebuilt prefab lost its GUID.");
                Debug.Log($"Skyway exterior rebuilt at {PrefabPath} (GUID kept); {prefab.GetComponentsInChildren<Renderer>(true).Length} renderers; physics=0; gameplay scripts=0.", prefab);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        // Use after the user has saved/registered the scene. This edits only the selected lobby entry and never saves it.
        [MenuItem("ProtoHarness/Chain Rush/Connect Saved Skyway Exterior To Lobby")]
        public static void ConnectLobby()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before connecting the lobby.");
            if (!File.Exists(ScenePath) || SceneUtility.GetBuildIndexByScenePath(ScenePath) < 0)
                throw new InvalidOperationException("Save ChainRushTrackSkyway and enable it in Build Profiles before connecting the lobby.");
            TrackDefinition definition = Require<TrackDefinition>(DefinitionPath);
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

        public static void Validate(GameObject root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (root.GetComponentsInChildren<Collider>(true).Length != 0
                || root.GetComponentsInChildren<Rigidbody>(true).Length != 0
                || root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException("The Skyway exterior must contain decoration only: no physics or runtime scripts.");
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                    if (material == null) throw new InvalidOperationException("Missing material: " + renderer.name);
        }

        // Upgrades this builder's own open instance; never touches another prefab or deletes an asset.
        public static void RefreshLandmarkLettering(GameObject root)
        {
            Validate(root);
            Transform landmarks = root.transform.Find("07 Skyway landmarks");
            if (landmarks == null) throw new InvalidOperationException("Skyway landmark group is missing.");
            foreach (Transform landmark in landmarks)
            {
                foreach (TextMesh label in landmark.GetComponentsInChildren<TextMesh>())
                    Undo.DestroyObjectImmediate(label.gameObject);
                if (landmark.Find("SKYWAY lettering") != null)
                    throw new InvalidOperationException("The landmark already has solid lettering.");
                Layout.Lettering(landmark, Require<Material>(MaterialFolder + "M_ArtStadiumMint.mat"),
                    Require<Material>(MaterialFolder + "M_ArtStadiumAmber.mat"));
            }
            PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        private static T Require<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Required asset is missing: " + path);
            return asset;
        }

        private sealed class Layout
        {
            private readonly TrackDefinition definition;
            private readonly Centerline line;
            private readonly double length;
            // Per sample, how far from the centerline the decoration keeps away: the road's half width there plus 7 m.
            private readonly float[] clearances;
            private readonly Vector3[] samples;
            private readonly UnityEngine.Mesh cube = Resources.GetBuiltinResource<UnityEngine.Mesh>("Cube.fbx");
            private readonly UnityEngine.Mesh sphere = Resources.GetBuiltinResource<UnityEngine.Mesh>("Sphere.fbx");
            private readonly Material ink = Require<Material>(MaterialFolder + "M_ArtStadiumInk.mat");
            private readonly Material steel = Require<Material>(MaterialFolder + "M_ArtStadiumSteel.mat");
            private readonly Material mint = Require<Material>(MaterialFolder + "M_ArtStadiumMint.mat");
            private readonly Material amber = Require<Material>(MaterialFolder + "M_ArtStadiumAmber.mat");
            private readonly Material city = Require<Material>(MaterialFolder + "M_City.mat");
            private readonly Material roof = Require<Material>(MaterialFolder + "M_RoofPanels.mat");
            private readonly Material concrete = Require<Material>(MaterialFolder + "M_StadiumSportsConcrete.mat");
            private readonly Material white = Require<Material>(MaterialFolder + "M_StadiumSportsWhite.mat");
            private readonly Material foliage = Require<Material>(MaterialFolder + "M_StadiumSportsTurfDark.mat");

            internal Layout(TrackDefinition definition)
            {
                this.definition = definition;
                line = definition.BuildCenterline(Vector3.zero, 0f);
                length = definition.LapLength;
                samples = new Vector3[Mathf.CeilToInt((float)length / 2f)];
                clearances = new float[samples.Length];
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = line.FrameAt(i * 2d).Position;
                    clearances[i] = Clearance(i * 2d);
                }
                if (cube == null || sphere == null) throw new InvalidOperationException("Built-in decoration meshes are missing.");
            }

            private float Clearance(double s) => definition.HalfWidthAt(s) + 7f;

            // Builds in the active scene, or in into when it is given (Rebuild's preview scene).
            internal GameObject Build(Scene into = default)
            {
                var root = new GameObject("Skyway Exterior");
                if (into.IsValid()) SceneManager.MoveGameObjectToScene(root, into);
                Transform terrain = Group("01 Landscape", root.transform);
                Box("City foundation", terrain, new Vector3(-85f, Ground - 0.5f, -55f), new Vector3(740f, 1f, 550f), city);
                Transform village = Group("02 Village and shops", root.transform);
                for (float s = 0f; s < length; s += 34f)
                {
                    TrackFrame f = line.FrameAt(s);
                    // No village beside the climb, the viaduct and its exit.
                    if (f.Position.y > 2f) continue;
                    float clearance = Clearance(s);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int index = Mathf.RoundToInt(s / 34f) * 2 + (side + 1) / 2;
                        float width = 9f + index % 3 * 1.5f;
                        float depth = 9f + index % 2 * 2f;
                        float radius = new Vector2(width + 1.6f, depth + 1.6f).magnitude * 0.5f;
                        Vector3 p = f.Position + f.Right * side * (clearance + radius + 3f);
                        p.y = Ground;
                        if (Clear(p, radius)) House(village, p, Quaternion.LookRotation(-f.Right * side), width, depth, 7f + index % 4 * 2.3f, index);
                    }
                }

                Transform gardens = Group("03 Gardens and avenue", root.transform);
                for (float s = 14f; s < length; s += 29f)
                {
                    TrackFrame f = line.FrameAt(s);
                    int side = ((int)(s / 29f) % 2 == 0) ? -1 : 1;
                    Vector3 p = f.Position + f.Right * side * 19f;
                    p.y = Ground;
                    if (Clear(p, 3.5f)) Tree(gardens, p, 4.2f + (int)s % 3 * 0.45f);
                }
                for (int x = 0; x < 5; x++)
                    for (int z = 0; z < 3; z++)
                    {
                        Vector3 p = new Vector3(-185f + x * 28f, Ground, -125f + z * 32f);
                        if (!Clear(p, 10f)) continue;
                        Box("Garden island", gardens, p + Vector3.up * 0.12f, new Vector3(18f, 0.24f, 21f), foliage);
                        Tree(gardens, p + new Vector3(-4f, 0f, -4f), 5.4f);
                        Tree(gardens, p + new Vector3(4f, 0f, 5f), 4.6f);
                    }

                Transform supports = Group("04 Elevated viaduct piers", root.transform);
                // Wherever the road runs high enough over the ground; the cap spans the road's width there.
                for (float s = 0f; s < length; s += 32f)
                {
                    TrackFrame f = line.FrameAt(s);
                    float top = f.Position.y - 1.5f;
                    if (top < Ground + 3f) continue;
                    Transform pier = Group("Pier S" + s.ToString("0"), supports);
                    pier.SetPositionAndRotation(new Vector3(f.Position.x, Ground, f.Position.z), Quaternion.LookRotation(f.Forward));
                    float height = top - Ground;
                    Box("Plinth", pier, new Vector3(0f, 0.35f, 0f), new Vector3(5.8f, 0.7f, 5.5f), ink);
                    Box("Concrete pier", pier, new Vector3(0f, height * 0.5f, 0f), new Vector3(2.7f, height, 2.9f), concrete);
                    Box("Hammerhead cap", pier, new Vector3(0f, height - 0.45f, 0f), new Vector3(definition.HalfWidthAt(s) * 2f - 0.8f, 0.9f, 3.4f), steel);
                }

                Transform lamps = Group("05 Boulevard lamps", root.transform);
                for (float s = 22f; s < length; s += 42f)
                {
                    TrackFrame f = line.FrameAt(s);
                    int side = (int)(s / 42f) % 2 == 0 ? -1 : 1;
                    Vector3 p = f.Position + f.Right * (Clearance(s) + 1.5f) * side;
                    if (!Clear(p, 0.8f)) continue;
                    Transform lamp = Group("Lamp S" + s.ToString("0"), lamps);
                    lamp.SetPositionAndRotation(new Vector3(p.x, Ground, p.z), Quaternion.LookRotation(-f.Right * side));
                    float height = Mathf.Max(7f, f.Position.y - Ground + 5f);
                    Box("Mast", lamp, new Vector3(0f, height * 0.5f, 0f), new Vector3(0.24f, height, 0.24f), steel);
                    Box("Light housing", lamp, new Vector3(0f, height, 0.6f), new Vector3(0.7f, 0.28f, 1.6f), ink);
                    Box("Warm diffuser", lamp, new Vector3(0f, height - 0.16f, 0.6f), new Vector3(0.52f, 0.07f, 1.3f), amber);
                }

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

                Transform landmarks = Group("07 Skyway landmarks", root.transform);
                Landmark(landmarks, new Vector3(-28f, Ground, 28f), Quaternion.Euler(0f, 90f, 0f), "SKYWAY", "GARDEN DISTRICT");
                Landmark(landmarks, new Vector3(-220f, Ground, -153f), Quaternion.Euler(0f, 180f, 0f), "SKYWAY", "ELEVATED EXPRESS");
                return root;
            }

            private bool Clear(Vector3 p, float radius)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    float limit = clearances[i] + radius;
                    float dx = p.x - samples[i].x, dz = p.z - samples[i].z;
                    if (dx * dx + dz * dz < limit * limit) return false;
                }
                return true;
            }

            private void House(Transform parent, Vector3 position, Quaternion rotation, float width, float depth, float height, int index)
            {
                Transform house = Group("Village building " + index, parent);
                house.SetPositionAndRotation(position, rotation);
                Box("Raised foundation", house, new Vector3(0f, 0.3f, 0f), new Vector3(width + 1.2f, 0.6f, depth + 1.2f), concrete);
                Box("Facade", house, new Vector3(0f, height * 0.5f + 0.5f, 0f), new Vector3(width, height, depth), index % 3 == 0 ? white : steel);
                Box("Upper cornice", house, new Vector3(0f, height + 0.55f, 0f), new Vector3(width + 0.8f, 0.35f, depth + 0.8f), white);
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject roofSide = Box("Pitched roof", house, new Vector3(side * width * 0.24f, height + 1.5f, 0f), new Vector3(width * 0.57f, 0.35f, depth + 1.2f), roof);
                    roofSide.transform.localRotation = Quaternion.Euler(0f, 0f, -side * 24f);
                }
                for (int floor = 0; floor < 2; floor++)
                    for (int column = -1; column <= 1; column++)
                        Box("Window", house, new Vector3(column * width * 0.28f, 2.2f + floor * (height - 2.7f) * 0.58f, depth * 0.5f + 0.04f), new Vector3(1.15f, 1.45f, 0.08f), (column + floor + index) % 3 == 0 ? mint : amber);
                Box("Shop canopy", house, new Vector3(0f, 3.5f, depth * 0.5f + 0.9f), new Vector3(width * 0.82f, 0.22f, 1.8f), index % 3 == 0 ? mint : roof);
                Box("Door", house, new Vector3(0f, 1.65f, depth * 0.5f + 0.08f), new Vector3(1.35f, 2.5f, 0.1f), ink);
            }

            private void Tree(Transform parent, Vector3 position, float height)
            {
                Transform tree = Group("Planter tree", parent);
                tree.position = position;
                Box("Planter", tree, new Vector3(0f, 0.3f, 0f), new Vector3(3f, 0.6f, 3f), concrete);
                Box("Trunk", tree, new Vector3(0f, height * 0.4f, 0f), new Vector3(0.45f, height * 0.8f, 0.45f), roof);
                Shape("Crown", tree, new Vector3(0f, height, 0f), new Vector3(4.8f, 5.4f, 4.8f), foliage, sphere);
            }

            private void Tower(Transform parent, Vector3 position, float width, float height, int index)
            {
                if (!Clear(position, width * 0.75f)) throw new InvalidOperationException("Skyline tower overlaps the reserved track corridor.");
                Transform tower = Group("Skyline tower " + index, parent);
                tower.position = position;
                Box("Podium", tower, new Vector3(0f, 2f, 0f), new Vector3(width + 4f, 4f, width + 4f), ink);
                Box("Tower", tower, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, width), city);
                Box("Crown", tower, new Vector3(0f, height, 0f), new Vector3(width + 0.6f, 0.8f, width + 0.6f), steel);
                for (int floor = 0; floor < 4; floor++)
                {
                    float y = 7f + floor * (height - 10f) / 4f;
                    Box("North window band", tower, new Vector3(0f, y, width * 0.5f + 0.04f), new Vector3(width * 0.8f, 0.55f, 0.08f), floor % 3 == 0 ? amber : steel);
                    Box("South window band", tower, new Vector3(0f, y, -width * 0.5f - 0.04f), new Vector3(width * 0.8f, 0.55f, 0.08f), floor % 3 == 0 ? amber : steel);
                }
                Box("Corner light", tower, new Vector3(width * 0.5f + 0.06f, height * 0.5f, -width * 0.5f), new Vector3(0.14f, height * 0.75f, 0.14f), index % 3 == 0 ? mint : amber);
            }

            private void Landmark(Transform parent, Vector3 position, Quaternion rotation, string title, string subtitle)
            {
                if (!Clear(position, 8.5f)) throw new InvalidOperationException("Landmark overlaps the reserved track corridor.");
                Transform sign = Group(title + " landmark - " + subtitle, parent);
                sign.SetPositionAndRotation(position, rotation);
                Box("Plinth", sign, new Vector3(0f, 0.35f, 0f), new Vector3(16f, 0.7f, 4.5f), concrete);
                Box("Left support", sign, new Vector3(-6.8f, 6.5f, 0f), new Vector3(0.55f, 13f, 0.55f), steel);
                Box("Right support", sign, new Vector3(6.8f, 6.5f, 0f), new Vector3(0.55f, 13f, 0.55f), steel);
                Box("Sign panel", sign, new Vector3(0f, 11f, 0f), new Vector3(15f, 5.5f, 0.65f), ink);
                Box("Amber header", sign, new Vector3(0f, 13.65f, -0.36f), new Vector3(15f, 0.16f, 0.08f), amber);
                Lettering(sign, mint, amber);
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
                UnityEngine.Mesh mesh = Resources.GetBuiltinResource<UnityEngine.Mesh>("Cube.fbx");
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

            private static Transform Group(string name, Transform parent)
            {
                var group = new GameObject(name).transform;
                group.SetParent(parent, false);
                return group;
            }

            private GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
                => Shape(name, parent, position, scale, material, cube);

            private static GameObject Shape(string name, Transform parent, Vector3 position, Vector3 scale, Material material, UnityEngine.Mesh mesh)
            {
                var shape = new GameObject(name);
                shape.transform.SetParent(parent, false);
                shape.transform.localPosition = position;
                shape.transform.localScale = scale;
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
