using System;
using System.IO;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Audio;
using ProtoHarness.ChainRush.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ProtoHarness.Editor.ChainRush.ChainRushSceneBuilder;

namespace ProtoHarness.Editor.ChainRush
{
    public static class ChainRushPresentationBuilder
    {
        [MenuItem("ProtoHarness/Chain Rush/Apply Art Sound Animation %#j")]
        public static void ApplyPresentation()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit play mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ChainRushEndlessSceneBuilder.EndlessScenePath);
            ChainRushGame game = null;
            RunnerMotor runner = null;
            Transform world = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame g)) game = g;
                if (root.TryGetComponent(out RunnerMotor r)) runner = r;
                if (root.name == "Endless World") world = root.transform;
            }
            if (game == null || runner == null || world == null) throw new InvalidOperationException("Endless scene structure is incomplete.");
            if (runner.transform.Find("Armored Runner") != null) throw new InvalidOperationException("Presentation is already applied.");
            Transform[] joints = ApplyArt(game, runner, world);
            PolishUrbanArt(world);
            Debug.Log("ChainRush presentation 1/3: art applied.");
            var sound = ApplySound(game, runner);
            Debug.Log("ChainRush presentation 2/3: sound connected.");
            ApplyAnimation(game, runner, sound, joints);
            Debug.Log("ChainRush presentation 3/3: animation connected.");
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save presentation scene.");
            AssetDatabase.SaveAssets();
        }

        private static Transform[] ApplyArt(ChainRushGame game, RunnerMotor runner, Transform world)
        {
            Material armor = Material("RunnerArmor", new Color(0.1f, 0.14f, 0.22f), 0f);
            Material trim = Material("RunnerTrim", new Color(0.6f, 0.72f, 0.8f), 0f);
            Material teal = Material("ElectricTeal", new Color(0.12f, 0.95f, 0.92f), 2f);
            Material pink = Material("ElectricPink", new Color(0.95f, 0.08f, 0.42f), 1.6f);
            Material floor = Material("RoofPanels", new Color(0.105f, 0.15f, 0.21f), 0f);
            armor.SetFloat("_Metallic", 0.7f);
            trim.SetFloat("_Metallic", 0.8f);
            floor.SetFloat("_Metallic", 0.35f);
            floor.SetFloat("_Smoothness", 0.65f);
            var tilt = runner.GetComponent<RunnerTilt>();
            if (tilt == null) throw new InvalidOperationException("RunnerTilt is missing on the runner.");
            Transform oldBody = (Transform)new SerializedObject(tilt).FindProperty("body").objectReferenceValue;
            var grapple = runner.GetComponent<GrappleController>();
            var grappleData = new SerializedObject(grapple);
            Transform hand = (Transform)grappleData.FindProperty("ropeOrigin").objectReferenceValue;
            Transform model = Joint("Armored Runner", runner.transform, Vector3.zero);
            Transform torso = Joint("Torso Joint", model, new Vector3(0f, 0.05f, 0f));
            Part("Chest armor", torso, Vector3.zero, new Vector3(0.66f, 0.6f, 0.39f), armor);
            Part("Collar", torso, new Vector3(0f, 0.32f, 0f), new Vector3(0.55f, 0.12f, 0.42f), trim);
            Part("Back reactor", torso, new Vector3(0f, 0.04f, -0.24f), new Vector3(0.37f, 0.42f, 0.16f), trim);
            Part("Reactor spine", torso, new Vector3(0f, 0.04f, -0.335f), new Vector3(0.08f, 0.3f, 0.03f), teal);
            for (int i = -1; i <= 1; i += 2)
                Part("Back circuit", torso, new Vector3(i * 0.22f, 0.06f, -0.21f), new Vector3(0.055f, 0.43f, 0.05f), teal);
            Transform head = Joint("Head Joint", torso, new Vector3(0f, 0.52f, 0.02f));
            Part("Helmet", head, Vector3.zero, new Vector3(0.46f, 0.38f, 0.45f), armor);
            Part("Visor", head, new Vector3(0f, 0.04f, 0.235f), new Vector3(0.42f, 0.09f, 0.04f), teal);
            Part("Helmet rear light", head, new Vector3(0f, 0.08f, -0.24f), new Vector3(0.27f, 0.055f, 0.025f), pink);
            Transform[] joints = new Transform[6];
            joints[0] = model; joints[1] = torso;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Transform arm = Joint(i == 0 ? "Left Arm Joint" : "Right Arm Joint", torso, new Vector3(side * 0.43f, 0.22f, 0f));
                Part("Shoulder", arm, Vector3.zero, new Vector3(0.3f, 0.23f, 0.4f), trim);
                Part("Sleeve", arm, new Vector3(0f, -0.25f, 0f), new Vector3(0.22f, 0.42f, 0.24f), armor);
                Part("Gauntlet", arm, new Vector3(0f, -0.48f, 0.04f), new Vector3(0.28f, 0.24f, 0.32f), trim);
                Part("Wrist signal", arm, new Vector3(0f, -0.48f, -0.135f), new Vector3(0.21f, 0.06f, 0.04f), teal);
                Transform leg = Joint(i == 0 ? "Left Leg Joint" : "Right Leg Joint", model, new Vector3(side * 0.19f, -0.25f, 0f));
                Part("Thigh", leg, new Vector3(0f, -0.2f, 0f), new Vector3(0.27f, 0.4f, 0.28f), armor);
                Part("Knee plate", leg, new Vector3(0f, -0.36f, 0.16f), new Vector3(0.25f, 0.15f, 0.09f), trim);
                Part("Boot", leg, new Vector3(0f, -0.55f, 0.06f), new Vector3(0.28f, 0.3f, 0.41f), armor);
                Part("Heel light", leg, new Vector3(0f, -0.59f, -0.155f), new Vector3(0.2f, 0.055f, 0.025f), teal);
                joints[2 + i] = arm; joints[4 + i] = leg;
            }
            hand.SetParent(joints[3], false);
            hand.localPosition = new Vector3(0f, -0.58f, 0.18f);
            oldBody.gameObject.SetActive(false);
            Assign(tilt, "body", model);
            for (int sector = 0; sector < world.childCount; sector++)
            {
                Transform chunk = world.GetChild(sector);
                Transform geometry = chunk.Find("Course Geometry");
                if (geometry == null) throw new InvalidOperationException("Pooled geometry missing.");
                geometry.Find("Deck").GetComponent<Renderer>().sharedMaterial = floor;
                foreach (TextMesh text in geometry.GetComponentsInChildren<TextMesh>()) text.gameObject.SetActive(false);
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int z = 3; z < 36; z += 8)
                    {
                        Part("Roof vent", chunk, new Vector3(side * 4.8f, 0.18f, z), new Vector3(0.7f, 0.35f, 1.8f), armor);
                        for (int slit = 0; slit < 4; slit++)
                            Part("Vent slit", chunk, new Vector3(side * 4.8f, 0.36f, z - 0.6f + slit * 0.4f), new Vector3(0.53f, 0.025f, 0.055f), trim);
                    }
                    for (int z = 2; z < 40; z += 6)
                        Part("Window bank", chunk, new Vector3(side * 16f, -4f, z), new Vector3(0.15f, 3f, 2f), side < 0 ? pink : teal);
                    Transform billboard = Joint("Hologram sign", chunk, new Vector3(side * 10f, 6f, 24f));
                    Part("Sign backing", billboard, Vector3.zero, new Vector3(5.5f, 2.6f, 0.3f), armor);
                    Part("Sign trim", billboard, new Vector3(0f, 1.2f, -0.2f), new Vector3(5.1f, 0.07f, 0.1f), side < 0 ? pink : teal);
                    var label = new GameObject("District typography").AddComponent<TextMesh>();
                    label.transform.SetParent(billboard, false);
                    label.transform.localPosition = new Vector3(0f, 0f, -0.2f);
                    label.text = side < 0 ? "NEON\nDISTRICT 09" : "CHAIN\nTRANSIT";
                    label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
                    label.fontSize = 48; label.characterSize = 0.38f;
                    label.color = side < 0 ? new Color(1f, 0.18f, 0.5f) : new Color(0.15f, 1f, 0.9f);
                }
            }
            Transform drone = game.Enemies.Target;
            Part("Drone dorsal armor", drone, new Vector3(0f, 0.38f, 0f), new Vector3(0.85f, 0.18f, 0.7f), armor);
            for (int side = -1; side <= 1; side += 2)
                Part("Drone blade light", drone, new Vector3(side * 1.08f, 0.12f, -0.38f), new Vector3(0.72f, 0.05f, 0.07f), pink);
            RenderSettings.fogColor = new Color(0.035f, 0.035f, 0.085f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 210f;
            Camera.main.backgroundColor = RenderSettings.fogColor;
            RenderSettings.ambientLight = new Color(0.34f, 0.38f, 0.54f);
            return joints;
        }

        private static ChainRushAudio ApplySound(ChainRushGame game, RunnerMotor runner)
        {
            var audio = game.gameObject.AddComponent<ChainRushAudio>();
            var music = game.gameObject.AddComponent<AudioSource>();
            var effects = game.gameObject.AddComponent<AudioSource>();
            var ambience = game.gameObject.AddComponent<AudioSource>();
            music.playOnAwake = effects.playOnAwake = ambience.playOnAwake = false;
            music.loop = ambience.loop = true;
            Assign(audio, "game", game, "player", runner, "music", music, "effects", effects, "ambience", ambience);
            Assign(game, "presentationAudio", audio);
            var data = new SerializedObject(game);
            data.FindProperty("enhancedPresentation").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            return audio;
        }

        [MenuItem("ProtoHarness/Chain Rush/Polish Urban Art %#k")]
        public static void PolishCurrentScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit play mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ChainRushEndlessSceneBuilder.EndlessScenePath) throw new InvalidOperationException("Open the endless scene first.");
            Transform world = null;
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == "Endless World") world = root.transform;
            if (world == null) throw new InvalidOperationException("Endless World is missing.");
            PolishUrbanArt(world);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save urban polish.");
            Debug.Log("ChainRush: urban facade and sign scale polished.");
        }

        private static void PolishUrbanArt(Transform world)
        {
            Material armor = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_RunnerArmor.mat");
            if (armor == null) throw new InvalidOperationException("Runner armor material is missing.");
            foreach (TextMesh label in world.GetComponentsInChildren<TextMesh>()) label.characterSize = 0.17f;
            for (int i = 0; i < world.childCount; i++)
            {
                Transform chunk = world.GetChild(i);
                if (chunk.Find("Facade Left") != null) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(side < 0 ? "Facade Left" : "Facade Right", chunk, new Vector3(side * 16.4f, -12f, 20f), new Vector3(0.8f, 24f, 40f), armor);
                    Part("Billboard mast", chunk, new Vector3(side * 10f, 0.5f, 24.25f), new Vector3(0.25f, 11f, 0.25f), armor);
                }
            }
        }

        private static void ApplyAnimation(ChainRushGame game, RunnerMotor runner, ChainRushAudio audio, Transform[] joints)
        {
            var animation = runner.gameObject.AddComponent<RunnerAnimation>();
            Assign(game, "presentationAnimation", animation);
            Assign(animation, "game", game, "player", runner, "grapple", runner.GetComponent<GrappleController>(), "sound", audio,
                "model", joints[0], "torso", joints[1], "leftArm", joints[2], "rightArm", joints[3], "leftLeg", joints[4], "rightLeg", joints[5]);
        }

        private static Transform Joint(string name, Transform parent, Vector3 position)
        {
            var item = new GameObject(name).transform;
            item.SetParent(parent, false); item.localPosition = position;
            return item;
        }

        private static GameObject Part(string name, Transform parent, Vector3 position, Vector3 size, Material material)
            => Primitive(name, PrimitiveType.Cube, parent, position, size, material);
    }
}
