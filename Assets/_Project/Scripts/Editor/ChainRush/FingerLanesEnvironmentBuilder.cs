using System;
using System.Collections.Generic;
using System.IO;
using ProtoHarness.ChainRush.Flow;
using ProtoHarness.ChainRush.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProtoHarness.Editor.ChainRush
{
    // Like SkywayEnvironmentBuilder: persistent decoration, no road, physics or runtime behaviour.
    public static class FingerLanesEnvironmentBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/FingerLanesExterior.prefab";
        public const string ScenePath = "Assets/_Project/Scenes/ChainRushTrackFingerLanes.unity";
        private const string DefinitionPath = "Assets/_Project/Data/Circuit_FingerLanes.asset";
        private const string LobbyPath = "Assets/_Project/Scenes/ChainRushLobby.unity";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/";
        private const float Ground = -1.3f;

        [MenuItem("ProtoHarness/Chain Rush/Create Finger Lanes Exterior")]
        public static void CreateExterior()
        {
            RequireIdle();
            if (File.Exists(PrefabPath) || File.Exists(ScenePath))
                throw new InvalidOperationException("Finger Lanes exterior already exists; use Rebuild Finger Lanes Exterior.");
            BuildPrefab();
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            scene.name = "ChainRushTrackFingerLanes";
            GameObject prefab = Require<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Create Finger Lanes exterior");
            EditorSceneManager.MarkSceneDirty(scene);
            if (original.IsValid()) SceneManager.SetActiveScene(original);
            Selection.activeGameObject = instance;
            Debug.Log($"Finger Lanes exterior created. Save the decoration scene as {ScenePath}, register it in Build Profiles and connect the lobby.", instance);
        }

        [MenuItem("ProtoHarness/Chain Rush/Rebuild Finger Lanes Exterior")]
        public static void RebuildExterior()
        {
            RequireIdle();
            if (!File.Exists(PrefabPath)) throw new InvalidOperationException("Create Finger Lanes Exterior first.");
            BuildPrefab();
        }

        // Saving/registration is separate from building; invoke only with explicit approval of that scope.
        public static void SaveRegisterAndConnect()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            Scene exterior = default;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (candidate.name == "ChainRushTrackFingerLanes") exterior = candidate;
                else if (candidate.isDirty) throw new InvalidOperationException("Save the other open scenes before connecting the exterior.");
            }
            if (!exterior.IsValid() || exterior.rootCount != 1)
                throw new InvalidOperationException("Open the newly created exterior scene with its single prefab instance.");
            Validate(exterior.GetRootGameObjects()[0]);
            if (!EditorSceneManager.SaveScene(exterior, ScenePath)) throw new IOException("Could not save the exterior scene.");
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int existing = scenes.FindIndex(entry => entry.path == ScenePath);
            if (existing < 0) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            else scenes[existing] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = scenes.ToArray();

            Scene original = SceneManager.GetActiveScene();
            Scene lobby = SceneManager.GetSceneByPath(LobbyPath);
            bool opened = !lobby.isLoaded;
            if (opened) lobby = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Additive);
            try
            {
                TrackDefinition definition = Require<TrackDefinition>(DefinitionPath);
                int connected = 0;
                foreach (GameObject root in lobby.GetRootGameObjects())
                    foreach (LobbyMenu menu in root.GetComponentsInChildren<LobbyMenu>(true))
                    {
                        var data = new SerializedObject(menu);
                        SerializedProperty tracks = data.FindProperty("tracks");
                        for (int i = 0; i < tracks.arraySize; i++)
                        {
                            SerializedProperty entry = tracks.GetArrayElementAtIndex(i);
                            if (entry.FindPropertyRelative("definition").objectReferenceValue != definition) continue;
                            Undo.RecordObject(menu, "Connect Finger Lanes exterior");
                            entry.FindPropertyRelative("environmentScene").stringValue = "ChainRushTrackFingerLanes";
                            data.ApplyModifiedProperties();
                            connected++;
                        }
                    }
                if (connected != 1) throw new InvalidOperationException($"Expected one Finger Lanes lobby entry, found {connected}.");
                EditorSceneManager.MarkSceneDirty(lobby);
                if (!EditorSceneManager.SaveScene(lobby)) throw new IOException("Could not save the connected lobby.");
                Debug.Log("Finger Lanes exterior saved, registered and connected to its lobby entry.");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(lobby, true);
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            }
        }

        public static void Validate(GameObject root)
        {
            SkywayEnvironmentBuilder.Validate(root);
            if (root.GetComponentsInChildren<Camera>(true).Length != 0 || root.GetComponentsInChildren<Light>(true).Length != 0)
                throw new InvalidOperationException("Exterior must use the race's camera and lighting.");
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh == null) throw new InvalidOperationException("Missing decoration mesh: " + filter.name);
        }

        private static void RequireIdle()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Exit Play mode and wait for compilation first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scenes first.");
        }

        private static void BuildPrefab()
        {
            TrackDefinition definition = Require<TrackDefinition>(DefinitionPath);
            if (!definition.TryValidate(out string error)) throw new InvalidOperationException(error);
            string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var layout = new Layout(definition);
                GameObject root = layout.Build(preview);
                Validate(root);
                layout.ValidateClearance(root);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success || saved == null) throw new IOException("Could not save Finger Lanes exterior prefab.");
                if (guid.Length != 0 && AssetDatabase.AssetPathToGUID(PrefabPath) != guid)
                    throw new InvalidOperationException("Rebuilding changed the exterior prefab GUID.");
                AssetDatabase.SaveAssets();
                Debug.Log($"Finger Lanes exterior: {saved.GetComponentsInChildren<Renderer>(true).Length} renderers; physics=0; runtime scripts=0; main and shortcut corridors clear.", saved);
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

        private static Material Palette(string name, Color color)
        {
            string path = MaterialFolder + "M_Finger" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Require<Material>(MaterialFolder + "M_StadiumSportsWhite.mat").shader;
            material = new Material(shader) { name = "M_Finger" + name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.16f);
            material.SetFloat("_Metallic", 0f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.22f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private sealed class Layout
        {
            private readonly TrackDefinition definition;
            private readonly Centerline line;
            private readonly List<Vector3> samples = new List<Vector3>();
            private readonly List<float> widths = new List<float>();
            private readonly List<(Vector3 Position, float Radius)> occupied = new List<(Vector3, float)>();
            private readonly Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            private readonly Mesh sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            private readonly Mesh cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            private readonly Material cream = Palette("Cream", new Color(0.94f, 0.83f, 0.61f));
            private readonly Material coral = Palette("Coral", new Color(0.68f, 0.24f, 0.17f));
            private readonly Material blue = Palette("Blue", new Color(0.24f, 0.49f, 0.65f));
            private readonly Material sage = Palette("Sage", new Color(0.37f, 0.57f, 0.32f));
            private readonly Material stone = Palette("Stone", new Color(0.71f, 0.69f, 0.59f));
            private readonly Material water = Palette("Water", new Color(0.15f, 0.49f, 0.59f));
            private readonly Material white = Require<Material>(MaterialFolder + "M_StadiumSportsWhite.mat");
            private readonly Material dark = Require<Material>(MaterialFolder + "M_ArtStadiumInk.mat");
            private readonly Material gold = Require<Material>(MaterialFolder + "M_ArtStadiumAmber.mat");
            private readonly Material grass = Require<Material>(MaterialFolder + "M_StadiumSportsTurfLight.mat");

            internal Layout(TrackDefinition definition)
            {
                this.definition = definition;
                line = definition.BuildCenterline(Vector3.zero, 0f);
                if (cube == null || sphere == null || cylinder == null) throw new InvalidOperationException("Missing built-in decoration meshes.");
                AddSamples(line, definition.LapLength, -1f);
                foreach (TrackDefinition.Shortcut shortcut in definition.Shortcuts)
                    AddSamples(TrackDefinition.ShortcutLine(line, shortcut), shortcut.Length, shortcut.HalfWidth);
            }

            private void AddSamples(Centerline centerline, double length, float halfWidth)
            {
                for (double s = 0d; s <= length; s += 0.5d)
                {
                    samples.Add(centerline.FrameAt(s).Position);
                    widths.Add(halfWidth > 0f ? halfWidth : definition.HalfWidthAt(s));
                }
            }

            internal GameObject Build(Scene scene)
            {
                var root = new GameObject("Finger Lanes Exterior");
                SceneManager.MoveGameObjectToScene(root, scene);
                Landscape(Group("01 Landscape below road", root.transform));
                Landmarks(Group("02 Clock tower and stone bridge", root.transform));
                occupied.Add((new Vector3(-20f, Ground, 14f), 9f));
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
                    Shape("Distant rounded hill", parent, new Vector3(x, -10f, 158f + i % 2 * 25f), new Vector3(100f, 42f + i % 3 * 9f, 65f), sage, sphere);
                    Shape("Distant rounded hill", parent, new Vector3(x, -13f, -172f - i % 2 * 18f), new Vector3(95f, 40f + i % 3 * 8f, 60f), sage, sphere);
                }
            }

            private void Landmarks(Transform parent)
            {
                if (definition.Segments.Count != 32 || definition.Shortcuts.Count != 2)
                    throw new InvalidOperationException("Finger Lanes layout changed; review landmark and shortcut placement before rebuilding.");
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
                occupied.Add((tower.position, 21f));

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
                occupied.Add((bridge.position, 22f));
            }

            private void ClockFace(Transform tower, int side)
            {
                Transform face = Group("Clock face " + side, tower);
                face.localPosition = new Vector3(0f, 20.8f, side * 10.18f);
                face.localRotation = Quaternion.Euler(0f, side < 0 ? 0f : 180f, 0f);
                GameObject rim = Shape("Clock gold rim", face, Vector3.zero, new Vector3(8.4f, 0.22f, 8.4f), gold, cylinder);
                rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                GameObject dial = Shape("Clock ivory dial", face, new Vector3(0f, 0f, -0.27f), new Vector3(7.7f, 0.09f, 7.7f), white, cylinder);
                dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                for (int hour = 0; hour < 12; hour++)
                {
                    float angle = hour * Mathf.PI / 6f;
                    GameObject mark = Box("Hour marker", face, new Vector3(Mathf.Sin(angle) * 3.15f, Mathf.Cos(angle) * 3.15f, -0.4f), new Vector3(0.17f, hour % 3 == 0 ? 0.65f : 0.35f, 0.12f), dark);
                    mark.transform.localRotation = Quaternion.Euler(0f, 0f, -hour * 30f);
                }
                Beam("Minute hand", face, new Vector3(0f, 0f, -0.52f), new Vector3(-1.1f, 2.2f, -0.52f), 0.2f, dark);
                Beam("Hour hand", face, new Vector3(0f, 0f, -0.56f), new Vector3(1.6f, 0.8f, -0.56f), 0.26f, dark);
                Shape("Clock hub", face, new Vector3(0f, 0f, -0.65f), Vector3.one * 0.45f, gold, sphere);
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
                        if (Available(position, radius))
                        {
                            House(parent, position, Quaternion.LookRotation(-frame.Right * side), width, depth, 7f + index % 3 * 2.2f, index);
                            occupied.Add((position, radius + 1f));
                        }
                        index++;
                    }
                }
                // A second, quieter row makes the outside read as a village rather than isolated roadside props.
                for (int i = 0; i < 14; i++)
                {
                    Vector3 position = new Vector3(-46f + i * 33f, Ground, i % 2 == 0 ? -112f : 121f);
                    if (!Available(position, 9f)) continue;
                    House(parent, position, Quaternion.Euler(0f, i % 2 == 0 ? 0f : 180f, 0f), 11f, 10f, 10f + i % 3 * 2f, index + i);
                    occupied.Add((position, 10f));
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
                        Shape("Shop sign rosette", house, new Vector3(dot * 1.2f, 5.1f, depth * 0.5f + 0.4f), Vector3.one * 0.4f, gold, sphere);
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
                        if (!Available(position, 9.2f)) continue;
                        Transform island = Group("Pocket garden " + garden++, parent);
                        island.position = position;
                        Box("Garden paving", island, new Vector3(0f, 0.12f, 0f), new Vector3(12f, 0.24f, 12f), stone);
                        Box("Raised lawn", island, new Vector3(0f, 0.3f, 0f), new Vector3(10f, 0.35f, 10f), sage);
                        Tree(island, new Vector3(-2.5f, 0.5f, -2f), 5.5f);
                        Tree(island, new Vector3(2.5f, 0.5f, 2f), 4.6f);
                        occupied.Add((position, 9.5f));
                    }
                for (double s = 8d; s < definition.LapLength; s += 18d)
                {
                    TrackFrame frame = line.FrameAt(s);
                    int side = (int)(s / 18d) % 2 == 0 ? -1 : 1;
                    Vector3 position = frame.Position + frame.Right * side * (definition.HalfWidthAt(s) + 5.4f);
                    position.y = Ground;
                    if (!Available(position, 3.1f)) continue;
                    Tree(parent, position, 5.2f + (int)s % 3 * 0.6f);
                    occupied.Add((position, 3.5f));
                }
            }

            private void Tree(Transform parent, Vector3 position, float height)
            {
                Transform tree = Group("Round village tree", parent);
                tree.localPosition = position;
                Box("Tree planter", tree, new Vector3(0f, 0.25f, 0f), new Vector3(2.4f, 0.5f, 2.4f), stone);
                Box("Tree trunk", tree, new Vector3(0f, height * 0.4f, 0f), new Vector3(0.55f, height * 0.8f, 0.55f), coral);
                Shape("Rounded crown", tree, new Vector3(0f, height * 0.8f, 0f), new Vector3(4.9f, height * 0.65f, 4.9f), sage, sphere);
            }

            private void StreetFurniture(Transform parent)
            {
                for (double s = 16d; s < definition.LapLength; s += 36d)
                {
                    TrackFrame frame = line.FrameAt(s);
                    int side = (int)(s / 36d) % 2 == 0 ? -1 : 1;
                    Vector3 position = frame.Position + frame.Right * side * (definition.HalfWidthAt(s) + 2.5f);
                    position.y = Ground;
                    if (!Available(position, 0.85f)) continue;
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
                        if (!Clear(position, 1.4f)) throw new InvalidOperationException("Shortcut flag overlaps a road corridor.");
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
                    if (!Available(p, 3.4f)) continue;
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

            internal void ValidateClearance(GameObject root)
            {
                float minimum = float.PositiveInfinity;
                var intrusions = new List<string>();
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    // Foundation/water are below the road. Tower's overpass is above the camera/runner corridor.
                    Bounds bounds = renderer.bounds;
                    if (bounds.max.y <= -1.21f || bounds.min.y >= 9f) continue;
                    if (renderer.transform.IsChildOf(root.transform.Find("01 Landscape below road"))) continue;
                    for (int i = 0; i < samples.Count; i++)
                    {
                        float dx = Mathf.Max(bounds.min.x - samples[i].x, 0f, samples[i].x - bounds.max.x);
                        float dz = Mathf.Max(bounds.min.z - samples[i].z, 0f, samples[i].z - bounds.max.z);
                        float margin = Mathf.Sqrt(dx * dx + dz * dz) - widths[i];
                        minimum = Mathf.Min(minimum, margin);
                        if (margin >= 0.7f) continue;
                        intrusions.Add($"{renderer.transform.parent.name}/{renderer.name}: margin {margin:F2} m at {samples[i]}");
                        break;
                    }
                }
                if (intrusions.Count != 0) throw new InvalidOperationException("Decoration intrudes into road corridors:\n" + string.Join("\n", intrusions));
                Debug.Log($"Finger Lanes decoration clearance: nearest renderer AABB is {minimum:F2} m outside the main/shortcut road edge; tower overhead >= 9 m.");
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
