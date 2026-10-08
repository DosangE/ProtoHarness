using System;
using System.IO;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Combat;
using ProtoHarness.ChainRush.Endless;
using ProtoHarness.ChainRush.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ProtoHarness.Editor.ChainRush.ChainRushSceneBuilder;

namespace ProtoHarness.Editor.ChainRush
{
    // Makes the procedural course scene (COURSE.md T3c): a copy of the endless scene whose pooled straight
    // sectors are replaced by a ProceduralCourse with its anchor pool, plus the default CourseTuning asset.
    // The endless scene itself is never changed.
    public static class ChainRushProceduralSceneBuilder
    {
        public const string ProceduralScenePath = "Assets/_Project/Scenes/ChainRushProcedural.unity";
        public const string TuningPath = "Assets/_Project/Data/CourseTuning_Default.asset";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/";
        // 8 is the most grapple modules that fit the 330 m window (HANDOFF.md T3c 2-4); two spare.
        private const int AnchorCount = 10;

        [MenuItem("ProtoHarness/Chain Rush/Create Procedural Scene")]
        public static void CreateProceduralScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before scene creation.");
            if (File.Exists(ProceduralScenePath)) throw new InvalidOperationException("Procedural scene already exists; open it instead.");
            if (!File.Exists(ChainRushEndlessSceneBuilder.EndlessScenePath))
                throw new InvalidOperationException("The endless scene is the source and is missing: " + ChainRushEndlessSceneBuilder.EndlessScenePath);
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or discard the open scene's changes first.");

            var tuning = AssetDatabase.LoadAssetAtPath<CourseTuning>(TuningPath);
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<CourseTuning>();
                AssetDatabase.CreateAsset(tuning, TuningPath);
            }
            Material road = RequireMaterial("M_Deck");
            Material rail = RequireMaterial("M_Frame");
            Material mint = RequireMaterial("M_Link");

            var scene = EditorSceneManager.OpenScene(ChainRushEndlessSceneBuilder.EndlessScenePath);
            // Save As: from here on the open scene is the new file and the endless scene stays as it was.
            if (!EditorSceneManager.SaveScene(scene, ProceduralScenePath)) throw new IOException("Could not create the procedural scene copy.");
            // Opening a scene unloads unused assets, which killed the tuning object made above (its scene
            // reference was saved as null, 2026-10-08), so load the asset again from disk.
            tuning = AssetDatabase.LoadAssetAtPath<CourseTuning>(TuningPath);
            if (tuning == null) throw new InvalidOperationException("Course tuning asset is missing after opening the scene: " + TuningPath);
            ChainRushGame game = null;
            RunnerMotor player = null;
            FollowCamera follow = null;
            Transform oldWorld = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame g)) game = g;
                if (root.TryGetComponent(out RunnerMotor p)) player = p;
                if (root.TryGetComponent(out FollowCamera f)) follow = f;
                if (root.name == "Endless World") oldWorld = root.transform;
            }
            if (game == null || player == null || follow == null || oldWorld == null)
                throw new InvalidOperationException("The endless scene does not match the required source structure.");
            var grapple = player.GetComponent<GrappleController>();
            var director = game.GetComponent<EnemyDirector>();
            if (grapple == null || director == null) throw new InvalidOperationException("The endless scene is missing the grapple or the enemy director.");

            var world = new GameObject("Procedural World").transform;
            var course = world.gameObject.AddComponent<ProceduralCourse>();
            var anchors = new Transform[AnchorCount];
            for (int i = 0; i < anchors.Length; i++)
            {
                Transform anchor = new GameObject("Link Anchor " + i).transform;
                anchor.SetParent(world, false);
                Primitive("Anchor core", PrimitiveType.Sphere, anchor, Vector3.zero, Vector3.one * 0.8f, mint);
                Ring("Anchor halo", anchor, Vector3.zero, 1.4f, 0.075f, mint);
                anchor.gameObject.SetActive(false);
                anchors[i] = anchor;
            }
            Assign(course, "game", game, "player", player, "followCamera", follow, "tuning", tuning,
                "roadMaterial", road, "railMaterial", rail, "lightMaterial", mint);
            AssignArray(course, "anchors", anchors);
            Assign(game, "endlessCourse", course);
            Assign(director, "course", course);
            AssignArray(grapple, "anchors", anchors);
            UnityEngine.Object.DestroyImmediate(oldWorld.gameObject);

            if (!EditorSceneManager.SaveScene(scene, ProceduralScenePath)) throw new IOException("Could not save the procedural scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("ChainRush: procedural scene saved; 24 road pieces, " + AnchorCount + " anchors, tuning " + TuningPath + ".");
        }

        private static Material RequireMaterial(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + name + ".mat");
            if (material == null) throw new InvalidOperationException("Source material is missing: " + MaterialFolder + name + ".mat");
            return material;
        }
    }
}
