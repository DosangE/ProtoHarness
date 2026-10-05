using System;
using System.Collections.Generic;
using System.IO;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProtoHarness.Editor.ChainRush
{
    [InitializeOnLoad]
    public static class ChainRushSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/ChainRushPrototype.unity";
        public const string RulesPath = "Assets/_Project/Data/RunRules_Default.asset";
        private const string MaterialFolder = "Assets/_Project/Art/Materials";

        static ChainRushSceneBuilder()
        {
            TestRunnerApi.RegisterTestCallback(new TestCallbacks());
        }

        [MenuItem("ProtoHarness/Chain Rush/Create Prototype Scene %#g")]
        public static void CreatePrototypeScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before creating the scene.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("ChainRushPrototype already exists. Open it instead of overwriting it.");
            var rules = AssetDatabase.LoadAssetAtPath<RunRules>(RulesPath);
            if (rules == null) throw new InvalidOperationException("Run rules asset is missing: " + RulesPath);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder("Assets/_Project/Scenes");
            EnsureFolder(MaterialFolder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material deck = Material("Deck", new Color(0.24f, 0.34f, 0.38f), 0f);
            Material frame = Material("Frame", new Color(0.035f, 0.09f, 0.12f), 0f);
            Material mint = Material("Link", new Color(0.25f, 0.95f, 0.72f), 1.7f);
            Material gold = Material("Signal", new Color(1f, 0.69f, 0.19f), 1.1f);
            Material coral = Material("Hazard", new Color(1f, 0.2f, 0.14f), 0.75f);
            Material suit = Material("Suit", new Color(0.82f, 0.9f, 0.91f), 0f);
            Material city = Material("City", new Color(0.08f, 0.18f, 0.23f), 0f);
            Material glass = Material("Visor", new Color(0.08f, 0.19f, 0.24f), 0.25f);

            var world = new GameObject("Skyline Course").transform;
            var anchors = new List<Transform>();
            var targets = new List<CourseTarget>();
            for (int section = 0; section < 9; section++)
            {
                float start = section == 0 ? -8f : 8f + section * 56f;
                float end = 48f + section * 56f;
                var chunk = new GameObject("Sector " + (section + 1).ToString("00")).transform;
                chunk.SetParent(world);
                float center = (start + end) * 0.5f;
                GameObject deckObject = Cube("Deck", chunk, new Vector3(0f, -0.6f, center), new Vector3(12f, 1.2f, end - start), deck, true);
                AddGuards(deckObject.transform, frame, mint);
                Cube("Undercarriage", chunk, new Vector3(0f, -2f, center), new Vector3(10.8f, 1.7f, end - start - 2f), frame);
                for (int side = -1; side <= 1; side += 2)
                {
                    Cube("Edge light", chunk, new Vector3(side * 5.92f, 0.04f, center), new Vector3(0.12f, 0.08f, end - start), mint);
                    Cube("Edge structure", chunk, new Vector3(side * 5.65f, -1.7f, center), new Vector3(0.32f, 2.2f, end - start), frame);
                }
                for (float z = start + 2f; z < end - 2f; z += 4f)
                {
                    Cube("Lane dash", chunk, new Vector3(0f, 0.015f, z), new Vector3(0.11f, 0.025f, 1.6f), suit);
                    Cube("Deck seam", chunk, new Vector3(0f, 0.01f, z + 1f), new Vector3(11.5f, 0.02f, 0.035f), frame);
                }
                if (section < 8)
                {
                    Cube("Jump marker", chunk, new Vector3(0f, 0.05f, end - 4f), new Vector3(11.7f, 0.08f, 0.35f), gold);
                    Cube("Edge warning", chunk, new Vector3(0f, 0.04f, end - 0.3f), new Vector3(11.7f, 0.07f, 0.2f), gold);
                    for (int arrow = 0; arrow < 3; arrow++)
                    {
                        var left = Cube("Jump chevron", chunk, new Vector3(-0.45f, 0.05f, end - 8f - arrow * 2f), new Vector3(0.12f, 0.04f, 1.2f), gold);
                        left.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                        var right = Cube("Jump chevron", chunk, new Vector3(0.45f, 0.05f, end - 8f - arrow * 2f), new Vector3(0.12f, 0.04f, 1.2f), gold);
                        right.transform.rotation = Quaternion.Euler(0f, -45f, 0f);
                    }
                    float anchorX = section < 2 ? 0f : (section % 2 == 0 ? -2f : 2f);
                    var anchor = new GameObject("Link Anchor " + (section + 1)).transform;
                    anchor.SetParent(chunk);
                    anchor.position = new Vector3(anchorX, 10f, end + 8f);
                    Primitive("Anchor core", PrimitiveType.Sphere, anchor, Vector3.zero, Vector3.one * 0.8f, mint);
                    Ring("Anchor halo", anchor, Vector3.zero, 1.4f, 0.075f, mint);
                    Cube("Suspension", chunk, new Vector3(anchorX, 12f, end + 8f), new Vector3(0.12f, 3.8f, 0.12f), mint);
                    Cube("Gantry", chunk, new Vector3(0f, 14f, end + 8f), new Vector3(19f, 0.6f, 0.6f), frame);
                    for (int side = -1; side <= 1; side += 2)
                        Cube("Gantry mast", chunk, new Vector3(side * 9f, 2f, end + 8f), new Vector3(0.5f, 24f, 0.5f), frame);
                    anchors.Add(anchor);
                }
                if (section < 8)
                {
                    float targetX = section == 0 ? 0f : (section % 2 == 0 ? -2.5f : 2.5f);
                    targets.Add(Target(chunk, new Vector3(targetX, 1.1f, start + 26f), true, gold, frame));
                    if (section > 0)
                        targets.Add(Target(chunk, new Vector3(-targetX, 1.1f, start + 15f), false, coral, frame));
                }
                Sign("Sector sign", chunk, new Vector3(-5.3f, 1.2f, start + 2f), (section + 1).ToString("00"), mint);
            }

            for (int i = 0; i < 52; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                float height = 10f + (i * 17 % 31);
                float x = side * (22f + i * 7 % 27);
                float z = -40f + i * 12f;
                Cube("Skyline tower " + i, world, new Vector3(x, -28f + height / 2f, z), new Vector3(7f + i % 5, height, 8f), city);
                Cube("Tower signal", world, new Vector3(x, -28f + height, z), new Vector3(0.3f, 2f, 0.3f), i % 3 == 0 ? gold : mint);
                for (int row = 0; row < 4; row++)
                    Cube("Tower stripe", world, new Vector3(x, -25f + height * row / 5f, z - 4.03f), new Vector3(4f, 0.09f, 0.04f), mint);
            }
            Cube("Finish left", world, new Vector3(-6f, 4f, 496f), new Vector3(0.55f, 8f, 0.55f), mint);
            Cube("Finish right", world, new Vector3(6f, 4f, 496f), new Vector3(0.55f, 8f, 0.55f), mint);
            Cube("Finish top", world, new Vector3(0f, 8f, 496f), new Vector3(12.5f, 0.55f, 0.55f), mint);
            Sign("Finish label", world, new Vector3(0f, 6.5f, 496f), "FINISH", suit, 1.2f);

            var gameObject = new GameObject("Chain Rush");
            var game = gameObject.AddComponent<ChainRushGame>();
            var audio = gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            var playerObject = new GameObject("Runner");
            playerObject.transform.position = new Vector3(0f, 1.05f, 5f);
            var controller = playerObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.38f;
            controller.center = Vector3.zero;
            controller.skinWidth = 0.04f;
            controller.minMoveDistance = 0f;
            controller.stepOffset = 0.2f;
            var motor = playerObject.AddComponent<RunnerMotor>();
            var grapple = playerObject.AddComponent<GrappleController>();
            var tilt = playerObject.AddComponent<RunnerTilt>();
            var model = new GameObject("Runner Visual").transform;
            model.SetParent(playerObject.transform, false);
            Primitive("Torso", PrimitiveType.Capsule, model, new Vector3(0f, -0.05f, 0f), new Vector3(0.62f, 0.52f, 0.48f), suit);
            Primitive("Helmet", PrimitiveType.Sphere, model, new Vector3(0f, 0.62f, 0f), Vector3.one * 0.59f, suit);
            Primitive("Visor", PrimitiveType.Cube, model, new Vector3(0f, 0.64f, 0.23f), new Vector3(0.45f, 0.17f, 0.14f), glass);
            Primitive("Pack", PrimitiveType.Cube, model, new Vector3(0f, 0.05f, -0.28f), new Vector3(0.39f, 0.5f, 0.2f), frame);
            Primitive("Pack light", PrimitiveType.Cube, model, new Vector3(0f, 0.1f, -0.4f), new Vector3(0.25f, 0.1f, 0.04f), mint);
            for (int side = -1; side <= 1; side += 2)
            {
                Primitive("Boot", PrimitiveType.Cube, model, new Vector3(side * 0.18f, -0.68f, 0.05f), new Vector3(0.24f, 0.43f, 0.35f), frame);
                Primitive("Arm", PrimitiveType.Capsule, model, new Vector3(side * 0.4f, 0.06f, 0f), new Vector3(0.21f, 0.3f, 0.22f), suit);
            }
            var hand = new GameObject("Chain Origin").transform;
            hand.SetParent(model, false);
            hand.localPosition = new Vector3(0.45f, 0.3f, 0.25f);
            var rope = new GameObject("Chain Line").AddComponent<LineRenderer>();
            rope.transform.SetParent(playerObject.transform, false);
            rope.sharedMaterial = mint;
            rope.widthMultiplier = 0.065f;
            rope.numCapVertices = 4;
            rope.positionCount = 2;
            rope.enabled = false;
            var strike = new GameObject("Strike Arc").transform;
            strike.SetParent(playerObject.transform, false);
            strike.localPosition = new Vector3(0f, 0f, 1.6f);
            Ring("Strike ring", strike, Vector3.zero, 1.1f, 0.09f, gold);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.095f, 0.13f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 350f;
            camera.fieldOfView = 67f;
            cameraObject.AddComponent<AudioListener>();
            var follow = cameraObject.AddComponent<FollowCamera>();
            camera.transform.position = playerObject.transform.position + new Vector3(0f, 5.5f, -10f);
            camera.transform.LookAt(playerObject.transform.position + Vector3.up * 1.5f + Vector3.forward * 9f);
            var hud = gameObject.AddComponent<ChainRushHud>();
            Assign(motor, "controller", controller, "game", game, "grapple", grapple);
            Assign(tilt, "motor", motor, "body", model);
            Assign(grapple, "game", game, "motor", motor, "rope", rope, "ropeOrigin", hand);
            AssignArray(grapple, "anchors", anchors.ToArray());
            Assign(follow, "target", motor, "game", game, "viewCamera", camera);
            Assign(game, "player", motor, "grapple", grapple, "followCamera", follow, "attackVisual", strike, "audioSource", audio, "rules", rules);
            AssignArray(game, "targets", targets.ToArray());
            Assign(hud, "game", game, "player", motor, "grapple", grapple, "viewCamera", camera);

            var sun = new GameObject("Skyline Key Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.83f, 0.93f, 1f);
            sun.intensity = 1.9f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.55f, 0.62f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 245f;
            RenderSettings.sun = sun;
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save ChainRush scene.");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = gameObject;
            Debug.Log("ChainRush: prototype scene saved. 9 platforms, 8 anchors, 496m finish. Press Play, then Enter.");
        }

        [MenuItem("ProtoHarness/Chain Rush/Open Prototype Scene")]
        public static void OpenPrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        // Adds the same deck guards the builder now makes to an existing, open scene (both ChainRush scenes
        // predate them). Skips decks that already have guards. Inactive copies are included: called on an
        // inactive root, GetComponentsInChildren still returns its children, so the endless scene's stored
        // prototype course gets guards too and keeps matching the prototype scene. Undo-able; the scene is
        // marked dirty and must be saved.
        [MenuItem("ProtoHarness/Chain Rush/Add Deck Guards To Open Scene")]
        public static void AddDeckGuardsToOpenScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before editing the scene.");
            Scene scene = SceneManager.GetActiveScene();
            Material rail = Material("Frame", new Color(0.035f, 0.09f, 0.12f), 0f);
            Material light = Material("Link", new Color(0.25f, 0.95f, 0.72f), 1.7f);
            int added = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>())
                {
                    if (item.name != "Deck" || item.GetComponent<BoxCollider>() == null || item.parent == null) continue;
                    if (item.parent.Find(GuardRailName) != null) continue;
                    foreach (GameObject created in AddGuards(item, rail, light)) Undo.RegisterCreatedObjectUndo(created, "Add deck guards");
                    added++;
                }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("ChainRush: added guards to " + added + " decks in " + scene.path + ". Save the scene to keep them.");
        }

        private const string GuardRailName = "Guard rail";
        private const float GuardRailHeight = 1.1f;
        private const float GuardRailThickness = 0.3f;
        // Taller than the 2.9 m jump apex, so a jump cannot clear a guard sideways.
        private const float GuardBlockHeight = 4f;

        // A visible rail just outside each long edge of a deck, as siblings of the deck. The rail's collider
        // reaches GuardBlockHeight above the deck top; the light strip on top has none. Gaps between decks
        // get no guards, so falling there stays possible (COURSE.md T2a).
        internal static List<GameObject> AddGuards(Transform deck, Material rail, Material light)
        {
            Transform parent = deck.parent;
            Vector3 size = deck.localScale;
            float top = deck.localPosition.y + size.y * 0.5f;
            var created = new List<GameObject>(4);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = deck.localPosition.x + side * (size.x + GuardRailThickness) * 0.5f;
                Vector3 railCenter = parent.TransformPoint(new Vector3(x, top + GuardRailHeight * 0.5f, deck.localPosition.z));
                GameObject guard = Cube(GuardRailName, parent, railCenter, new Vector3(GuardRailThickness, GuardRailHeight, size.z), rail, true);
                var box = guard.GetComponent<BoxCollider>();
                float localTop = (GuardBlockHeight - GuardRailHeight * 0.5f) / GuardRailHeight;
                box.size = new Vector3(1f, localTop + 0.5f, 1f);
                box.center = new Vector3(0f, (localTop - 0.5f) * 0.5f, 0f);
                created.Add(guard);
                Vector3 lightCenter = parent.TransformPoint(new Vector3(x, top + GuardRailHeight + 0.03f, deck.localPosition.z));
                created.Add(Cube("Guard light", parent, lightCenter, new Vector3(0.12f, 0.06f, size.z), light));
            }
            return created;
        }

        // Merge gate run (CLAUDE.md §9-2): everything except the Device category.
        [MenuItem("ProtoHarness/Chain Rush/Run PlayMode Tests")]
        public static void RunTests() => RunPlayMode("!Device");

        // Tests that need virtual Keyboard/Mouse devices. Not part of the merge gate.
        [MenuItem("ProtoHarness/Chain Rush/Run Device Input Tests")]
        public static void RunDeviceTests() => RunPlayMode("Device");

        private static void RunPlayMode(string category)
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, assemblyNames = new[] { "ProtoHarness.Tests.PlayMode" }, categoryNames = new[] { category } }));
        }

        private sealed class TestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                string path = Path.Combine(Path.GetTempPath(), "ChainRush-PlayMode-results.xml");
                TestRunnerApi.SaveResultToFile(result, path);
                Debug.Log("ChainRush tests: " + result.ResultState + "; passed=" + result.PassCount + "; failed=" + result.FailCount + "; XML=" + path);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }

        internal static Material Material(string name, Color color, float emission)
        {
            string path = MaterialFolder + "/M_" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
            var material = new Material(shader) { name = "M_" + name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.35f);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool solid = false)
        {
            GameObject cube = Primitive(name, PrimitiveType.Cube, parent, Vector3.zero, scale, material);
            cube.transform.position = position;
            cube.GetComponent<Collider>().enabled = solid;
            return cube;
        }

        internal static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            item.GetComponent<Collider>().enabled = false;
            return item;
        }

        internal static void Ring(string name, Transform parent, Vector3 localPosition, float radius, float width, Material material)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.transform.localPosition = localPosition;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 40;
            line.widthMultiplier = width;
            line.sharedMaterial = material;
            for (int i = 0; i < 40; i++)
            {
                float angle = i * Mathf.PI * 2f / 40f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
        }

        private static CourseTarget Target(Transform parent, Vector3 position, bool destructible, Material material, Material frame)
        {
            var root = new GameObject(destructible ? "Strike Drone" : "Hazard Barrier");
            root.transform.SetParent(parent);
            root.transform.position = position;
            var target = root.AddComponent<CourseTarget>();
            GameObject visual = Primitive("Visual", PrimitiveType.Cube, root.transform, Vector3.zero,
                destructible ? Vector3.one * 1.05f : new Vector3(2.5f, 2.2f, 1.1f), material);
            if (destructible) Ring("Drone halo", visual.transform, Vector3.zero, 1.1f, 0.04f, material);
            else
            {
                Primitive("Barrier stripe", PrimitiveType.Cube, visual.transform, new Vector3(0f, 0f, -0.51f), new Vector3(0.9f, 0.15f, 0.04f), frame);
            }
            Assign(target, "visual", visual.transform);
            var serialized = new SerializedObject(target);
            serialized.FindProperty("destructible").boolValue = destructible;
            serialized.FindProperty("contactHalfSize").vector3Value = destructible ? Vector3.one * 0.6f : new Vector3(1.25f, 1.1f, 0.55f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return target;
        }

        private static void Sign(string name, Transform parent, Vector3 position, string text, Material material, float size = 0.7f)
        {
            var sign = new GameObject(name).AddComponent<TextMesh>();
            sign.transform.SetParent(parent);
            sign.transform.position = position;
            sign.text = text;
            sign.fontSize = 64;
            sign.characterSize = size;
            sign.anchor = TextAnchor.MiddleCenter;
            sign.color = material.GetColor("_BaseColor");
        }

        internal static void Assign(UnityEngine.Object target, params object[] pairs)
        {
            var serialized = new SerializedObject(target);
            for (int i = 0; i < pairs.Length; i += 2)
                serialized.FindProperty((string)pairs[i]).objectReferenceValue = (UnityEngine.Object)pairs[i + 1];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void AssignArray<T>(UnityEngine.Object target, string property, T[] items) where T : UnityEngine.Object
        {
            var serialized = new SerializedObject(target);
            SerializedProperty array = serialized.FindProperty(property);
            array.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
