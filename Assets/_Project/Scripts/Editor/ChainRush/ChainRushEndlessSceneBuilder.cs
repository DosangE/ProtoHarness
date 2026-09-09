using System;
using System.IO;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Combat;
using ProtoHarness.ChainRush.Endless;
using ProtoHarness.ChainRush.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ProtoHarness.Editor.ChainRush.ChainRushSceneBuilder;

namespace ProtoHarness.Editor.ChainRush
{
    public static class ChainRushEndlessSceneBuilder
    {
        public const string EndlessScenePath = "Assets/_Project/Scenes/ChainRushEndless.unity";

        [MenuItem("ProtoHarness/Chain Rush/Create Endless Scene %#e")]
        public static void CreateEndlessScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before scene creation.");
            if (File.Exists(EndlessScenePath)) throw new InvalidOperationException("Endless scene already exists; open it instead.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            // Save a separate copy before changing any objects in the editor.
            if (!EditorSceneManager.SaveScene(scene, EndlessScenePath)) throw new IOException("Could not create endless scene copy.");
            ChainRushGame game = null;
            RunnerMotor player = null;
            FollowCamera follow = null;
            Transform oldWorld = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame g)) game = g;
                if (root.TryGetComponent(out RunnerMotor p)) player = p;
                if (root.TryGetComponent(out FollowCamera f)) follow = f;
                if (root.name == "Skyline Course") oldWorld = root.transform;
            }
            if (game == null || player == null || follow == null || oldWorld == null)
                throw new InvalidOperationException("Prototype scene does not match the required source structure.");
            var grapple = player.GetComponent<GrappleController>();
            Transform source = oldWorld.Find("Sector 02");
            if (source == null) throw new InvalidOperationException("Source Sector 02 is missing.");
            var world = new GameObject("Endless World").transform;
            var course = world.gameObject.AddComponent<EndlessCourse>();
            var chunks = new Transform[8];
            var anchors = new Transform[8];
            Material city = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_City.mat");
            Material frame = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Frame.mat");
            Material mint = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Link.mat");
            Material coral = Material("EnemyNeon", new Color(1f, 0.08f, 0.35f), 1.5f);
            Material metal = Material("ChainMetal", new Color(0.55f, 0.65f, 0.72f), 0f);
            metal.SetFloat("_Metallic", 0.85f);
            metal.SetFloat("_Smoothness", 0.7f);
            if (city == null || frame == null || mint == null) throw new InvalidOperationException("Source materials are missing.");
            for (int i = 0; i < chunks.Length; i++)
            {
                Transform chunk = new GameObject("Pooled Sector " + i).transform;
                chunk.SetParent(world, false);
                chunk.position = Vector3.forward * (-8f + i * 56f);
                Transform geometry = UnityEngine.Object.Instantiate(source, chunk);
                geometry.name = "Course Geometry";
                geometry.localPosition = Vector3.back * 64f;
                foreach (CourseTarget target in geometry.GetComponentsInChildren<CourseTarget>()) target.gameObject.SetActive(false);
                Transform anchor = geometry.Find("Link Anchor 2");
                if (anchor == null) throw new InvalidOperationException("Source link anchor is missing.");
                anchors[i] = anchor;
                chunks[i] = chunk;
                foreach (TextMesh label in geometry.GetComponentsInChildren<TextMesh>()) label.text = (i + 1).ToString("00");
                for (int side = -1; side <= 1; side += 2)
                    for (int tower = 0; tower < 3; tower++)
                    {
                        float height = 18f + (i * 11 + tower * 7) % 27;
                        Vector3 position = chunk.position + new Vector3(side * (20f + tower * 9f), height / 2f - 28f, tower * 18f);
                        Cube("Neon tower", chunk, position, new Vector3(8f, height, 10f), city);
                        for (int row = 0; row < 5; row++)
                            Cube("Facade signal", chunk, position + new Vector3(0f, -height / 2f + row * height / 5f + 2f, -5.02f), new Vector3(6f, 0.12f, 0.05f), side < 0 ? coral : mint);
                    }
            }
            oldWorld.gameObject.SetActive(false);
            var director = game.gameObject.AddComponent<EnemyDirector>();
            var enemy = new GameObject("Interceptor Drone").transform;
            Primitive("Armored body", PrimitiveType.Cube, enemy, Vector3.zero, new Vector3(1.3f, 0.6f, 0.75f), frame);
            Primitive("Target core", PrimitiveType.Sphere, enemy, new Vector3(0f, 0f, -0.45f), Vector3.one * 0.45f, coral);
            for (int side = -1; side <= 1; side += 2)
            {
                Primitive("Swept wing", PrimitiveType.Cube, enemy, new Vector3(side * 1.05f, 0f, 0.15f), new Vector3(0.85f, 0.14f, 1f), metal);
                Primitive("Engine", PrimitiveType.Cylinder, enemy, new Vector3(side * 0.9f, 0.08f, 0.15f), new Vector3(0.6f, 0.12f, 0.6f), coral);
            }
            var warning = new GameObject("Enemy Warning Ring").transform;
            Ring("Warning", warning, Vector3.zero, 1.5f, 0.05f, coral);
            var impact = new GameObject("Chain Impact Ring").transform;
            Ring("Impact", impact, Vector3.zero, 0.65f, 0.1f, mint);
            var grappleData = new SerializedObject(grapple);
            Transform hand = (Transform)grappleData.FindProperty("ropeOrigin").objectReferenceValue;
            ChainVisual attackChain = CreateChain("Attack Chain", game, hand, metal, coral);
            ChainVisual grappleChain = CreateChain("Grapple Chain", game, hand, metal, mint);
            Assign(course, "game", game, "player", player, "followCamera", follow);
            AssignArray(course, "chunks", chunks);
            Assign(director, "game", game, "player", player, "course", course, "enemy", enemy, "warning", warning, "impact", impact, "chain", attackChain);
            Assign(game, "endlessCourse", course, "enemies", director);
            AssignArray(game, "targets", Array.Empty<CourseTarget>());
            Assign(grapple, "chainVisual", grappleChain);
            AssignArray(grapple, "anchors", anchors);
            var gameData = new SerializedObject(game);
            gameData.FindProperty("endlessMode").boolValue = true;
            gameData.ApplyModifiedPropertiesWithoutUndo();
            enemy.gameObject.SetActive(false);
            warning.gameObject.SetActive(false);
            impact.gameObject.SetActive(false);
            if (!EditorSceneManager.SaveScene(scene, EndlessScenePath)) throw new IOException("Could not save endless scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("ChainRush: endless scene saved; 8 pooled sectors, 3 enemy entrances, 1.2s attack window.");
        }

        private static ChainVisual CreateChain(string name, ChainRushGame game, Transform hand, Material metal, Material signal)
        {
            var root = new GameObject(name);
            var visual = root.AddComponent<ChainVisual>();
            var core = root.AddComponent<LineRenderer>();
            core.sharedMaterial = signal;
            core.widthMultiplier = 0.035f;
            core.positionCount = 2;
            core.enabled = false;
            var hook = new GameObject("Hook").transform;
            hook.SetParent(root.transform, false);
            Primitive("Spear", PrimitiveType.Cube, hook, Vector3.zero, new Vector3(0.14f, 0.14f, 0.55f), metal);
            Primitive("Barb", PrimitiveType.Cube, hook, new Vector3(0.12f, 0f, 0.15f), new Vector3(0.3f, 0.12f, 0.12f), signal);
            var links = new Transform[80];
            for (int i = 0; i < links.Length; i++)
            {
                var link = new GameObject("Link " + i).transform;
                link.SetParent(root.transform, false);
                // A closed rectangular link, alternating planes along the chain.
                Primitive("Rail L", PrimitiveType.Cube, link, new Vector3(-0.075f, 0f, 0f), new Vector3(0.04f, 0.04f, 0.25f), metal);
                Primitive("Rail R", PrimitiveType.Cube, link, new Vector3(0.075f, 0f, 0f), new Vector3(0.04f, 0.04f, 0.25f), metal);
                Primitive("End A", PrimitiveType.Cube, link, new Vector3(0f, 0f, -0.105f), new Vector3(0.15f, 0.04f, 0.04f), metal);
                Primitive("End B", PrimitiveType.Cube, link, new Vector3(0f, 0f, 0.105f), new Vector3(0.15f, 0.04f, 0.04f), metal);
                links[i] = link;
                link.gameObject.SetActive(false);
            }
            hook.gameObject.SetActive(false);
            Assign(visual, "game", game, "origin", hand, "hook", hook, "core", core);
            AssignArray(visual, "links", links);
            return visual;
        }
    }
}
