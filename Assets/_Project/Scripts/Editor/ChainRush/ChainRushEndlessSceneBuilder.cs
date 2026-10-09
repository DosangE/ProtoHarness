using System;
using System.IO;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Combat;
using ProtoHarness.ChainRush.Endless;
using ProtoHarness.ChainRush.Track;
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
        public const string TuningPath = "Assets/_Project/Data/EncounterTuning_Default.asset";
        public const string CourseTuningPath = "Assets/_Project/Data/CourseTuning_Default.asset";
        private const string AnchorRootName = "Grapple Anchors";
        private const int AnchorCount = 8;

        [MenuItem("ProtoHarness/Chain Rush/Create Endless Scene %#e")]
        public static void CreateEndlessScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before scene creation.");
            if (File.Exists(EndlessScenePath)) throw new InvalidOperationException("Endless scene already exists; open it instead.");
            var tuning = AssetDatabase.LoadAssetAtPath<EncounterTuning>(TuningPath);
            if (tuning == null) throw new InvalidOperationException("Encounter tuning asset is missing: " + TuningPath);
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
            var world = new GameObject("Endless World").transform;
            var course = world.gameObject.AddComponent<EndlessCourse>();
            Material frame = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Frame.mat");
            Material mint = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Link.mat");
            Material coral = Material("EnemyNeon", new Color(1f, 0.08f, 0.35f), 1.5f);
            Material metal = Material("ChainMetal", new Color(0.55f, 0.65f, 0.72f), 0f);
            metal.SetFloat("_Metallic", 0.85f);
            metal.SetFloat("_Smoothness", 0.7f);
            if (frame == null || mint == null) throw new InvalidOperationException("Source materials are missing.");
            SetUpGeneratedCourse(scene, course, game, player, follow, grapple);
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
            Assign(director, "game", game, "player", player, "course", course, "tuning", tuning, "enemy", enemy, "warning", warning, "impact", impact, "chain", attackChain);
            Assign(game, "endlessCourse", course, "enemies", director);
            AssignArray(game, "targets", Array.Empty<CourseTarget>());
            Assign(grapple, "chainVisual", grappleChain);
            var gameData = new SerializedObject(game);
            gameData.FindProperty("endlessMode").boolValue = true;
            gameData.ApplyModifiedPropertiesWithoutUndo();
            enemy.gameObject.SetActive(false);
            warning.gameObject.SetActive(false);
            impact.gameObject.SetActive(false);
            if (!EditorSceneManager.SaveScene(scene, EndlessScenePath)) throw new IOException("Could not save endless scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("ChainRush: endless scene saved; generated course, 8 pooled anchors, 3 enemy entrances, 1.2s attack window.");
        }

        // T3c: moves an existing endless scene from the pooled 56 m sectors to the generated course. Deletes the
        // Pooled Sector objects (decks, towers, anchors: scene objects, not assets), creates the course tuning
        // asset if it is missing, adds the anchor pool and wires EndlessCourse and the grapple, then saves.
        [MenuItem("ProtoHarness/Chain Rush/Upgrade Endless Scene (T3c)")]
        public static void UpgradeEndlessScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before editing the scene.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(EndlessScenePath);
            ChainRushGame game = null;
            RunnerMotor player = null;
            FollowCamera follow = null;
            EndlessCourse course = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame g)) game = g;
                if (root.TryGetComponent(out RunnerMotor p)) player = p;
                if (root.TryGetComponent(out FollowCamera f)) follow = f;
                if (root.TryGetComponent(out EndlessCourse c)) course = c;
                if (root.name == AnchorRootName) throw new InvalidOperationException("The endless scene already has the generated course.");
            }
            if (game == null || player == null || follow == null || course == null)
                throw new InvalidOperationException("Endless scene does not match the required structure.");
            int removed = 0;
            for (int i = course.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = course.transform.GetChild(i);
                if (!child.name.StartsWith("Pooled Sector ", StringComparison.Ordinal))
                    throw new InvalidOperationException("Unexpected child under Endless World: " + child.name);
                UnityEngine.Object.DestroyImmediate(child.gameObject);
                removed++;
            }
            SetUpGeneratedCourse(scene, course, game, player, follow, player.GetComponent<GrappleController>());
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the upgraded endless scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("ChainRush: endless scene upgraded to the generated course; removed " + removed + " pooled sectors, added " + AnchorCount + " anchors.");
        }

        // Course tuning, road materials and an inactive anchor pool (a root of its own, so the presentation
        // menus that walk Endless World's children never see it), wired into EndlessCourse and the grapple.
        private static void SetUpGeneratedCourse(UnityEngine.SceneManagement.Scene scene, EndlessCourse course, ChainRushGame game,
            RunnerMotor player, FollowCamera follow, GrappleController grapple)
        {
            if (grapple == null) throw new InvalidOperationException("The runner has no GrappleController.");
            var tuning = AssetDatabase.LoadAssetAtPath<CourseTuning>(CourseTuningPath);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<CourseTuning>();
                AssetDatabase.CreateAsset(tuning, CourseTuningPath);
            }
            // The endless decks' floor (ChainRushPresentationBuilder) and the guard materials (AddGuards).
            Material road = Material("RoofPanels", new Color(0.105f, 0.15f, 0.21f), 0f);
            Material rail = Material("Frame", new Color(0.035f, 0.09f, 0.12f), 0f);
            Material light = Material("Link", new Color(0.25f, 0.95f, 0.72f), 1.7f);
            var root = new GameObject(AnchorRootName).transform;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root.gameObject, scene);
            var anchors = new Transform[AnchorCount];
            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = new GameObject("Grapple Anchor " + i).transform;
                anchor.SetParent(root, false);
                Primitive("Anchor core", PrimitiveType.Sphere, anchor, Vector3.zero, Vector3.one * 0.8f, light);
                Ring("Anchor halo", anchor, Vector3.zero, 1.4f, 0.075f, light);
                anchor.gameObject.SetActive(false);
                anchors[i] = anchor;
            }
            Assign(course, "game", game, "player", player, "followCamera", follow, "tuning", tuning,
                "roadMaterial", road, "railMaterial", rail, "lightMaterial", light);
            AssignArray(course, "anchors", anchors);
            AssignArray(grapple, "anchors", anchors);
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
