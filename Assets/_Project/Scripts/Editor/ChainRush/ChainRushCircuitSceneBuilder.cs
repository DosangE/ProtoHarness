using System;
using System.IO;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Combat;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ProtoHarness.Editor.ChainRush.ChainRushSceneBuilder;

namespace ProtoHarness.Editor.ChainRush
{
    // Makes the circuit race scene (COURSE.md T4): a copy of the procedural scene without the enemies and the
    // streamed course, with a CircuitRace on a stadium TrackDefinition, plus that definition's asset.
    // The procedural scene itself is never changed.
    public static class ChainRushCircuitSceneBuilder
    {
        public const string CircuitScenePath = "Assets/_Project/Scenes/ChainRushCircuit.unity";
        public const string DefinitionPath = "Assets/_Project/Data/Circuit_Stadium.asset";
        private const string SourceScenePath = "Assets/_Project/Scenes/ChainRushProcedural.unity";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/";
        // One grapple gap in the stadium; four is room for edits.
        private const int AnchorCount = 4;

        // Scene objects that only the endless modes use.
        private static readonly string[] EnemyObjects = { "Interceptor Drone", "Enemy Warning Ring", "Chain Impact Ring", "Attack Chain" };

        [MenuItem("ProtoHarness/Chain Rush/Create Circuit Scene")]
        public static void CreateCircuitScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before scene creation.");
            if (File.Exists(CircuitScenePath)) throw new InvalidOperationException("Circuit scene already exists; open it instead.");
            if (!File.Exists(SourceScenePath))
                throw new InvalidOperationException("The procedural scene is the source and is missing: " + SourceScenePath);
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or discard the open scene's changes first.");

            var definition = AssetDatabase.LoadAssetAtPath<TrackDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<TrackDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }
            Material road = RequireMaterial("M_Deck");
            Material rail = RequireMaterial("M_Frame");
            Material mint = RequireMaterial("M_Link");

            var scene = EditorSceneManager.OpenScene(SourceScenePath);
            // Save As: from here on the open scene is the new file and the procedural scene stays as it was.
            if (!EditorSceneManager.SaveScene(scene, CircuitScenePath)) throw new IOException("Could not create the circuit scene copy.");
            // Opening a scene unloads unused assets, which killed an asset object held across it before
            // (the T3c tuning reference was saved as null), so load the definition again from disk.
            definition = AssetDatabase.LoadAssetAtPath<TrackDefinition>(DefinitionPath);
            if (definition == null) throw new InvalidOperationException("Track definition asset is missing after opening the scene: " + DefinitionPath);
            if (!definition.TryValidate(out string definitionError)) throw new InvalidOperationException(definitionError);

            ChainRushGame game = null;
            RunnerMotor player = null;
            FollowCamera follow = null;
            Transform oldWorld = null;
            var enemyRoots = new System.Collections.Generic.List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame g)) game = g;
                if (root.TryGetComponent(out RunnerMotor p)) player = p;
                if (root.TryGetComponent(out FollowCamera f)) follow = f;
                if (root.name == "Procedural World") oldWorld = root.transform;
                if (Array.IndexOf(EnemyObjects, root.name) >= 0) enemyRoots.Add(root);
            }
            if (game == null || player == null || oldWorld == null)
                throw new InvalidOperationException("The procedural scene does not match the required source structure.");
            var grapple = player.GetComponent<GrappleController>();
            var director = game.GetComponent<EnemyDirector>();
            if (grapple == null || director == null) throw new InvalidOperationException("The procedural scene is missing the grapple or the enemy director.");

            var world = new GameObject("Circuit World").transform;
            var race = world.gameObject.AddComponent<CircuitRace>();
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
            Assign(race, "game", game, "player", player, "definition", definition, "roadMaterial", road, "railMaterial", rail, "lightMaterial", mint);
            AssignArray(race, "anchors", anchors);
            AssignArray(grapple, "anchors", anchors);

            // The game becomes a finite circuit race: no endless course, no enemies.
            var gameData = new SerializedObject(game);
            gameData.FindProperty("endlessMode").boolValue = false;
            gameData.FindProperty("endlessCourse").objectReferenceValue = null;
            gameData.FindProperty("enemies").objectReferenceValue = null;
            gameData.FindProperty("circuit").objectReferenceValue = race;
            gameData.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.DestroyImmediate(director);
            foreach (GameObject enemyRoot in enemyRoots) UnityEngine.Object.DestroyImmediate(enemyRoot);
            UnityEngine.Object.DestroyImmediate(oldWorld.gameObject);

            // On the start line, facing along the track (the first slot is the line itself).
            Centerline line = definition.BuildCenterline(Vector3.zero, 0f);
            TrackFrame start = line.FrameAt(-definition.StartSlot(0));
            player.transform.position = start.TransformPoint(new Vector3(0f, 1.05f, 0f));
            player.transform.rotation = Quaternion.LookRotation(start.Forward);
            if (follow != null)
            {
                follow.transform.position = player.transform.position + Quaternion.LookRotation(start.Forward) * new Vector3(0f, 5.5f, -10f);
                follow.transform.LookAt(player.transform.position + Vector3.up * 1.5f + start.Forward * 9f);
            }

            if (!EditorSceneManager.SaveScene(scene, CircuitScenePath)) throw new IOException("Could not save the circuit scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("ChainRush: circuit scene saved; " + definition.Segments.Count + " segments, " + definition.LapLength.ToString("0.0") + " m a lap, " + definition.LapCount + " laps.");
        }

        private static Material RequireMaterial(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + name + ".mat");
            if (material == null) throw new InvalidOperationException("Source material is missing: " + MaterialFolder + name + ".mat");
            return material;
        }
    }
}
