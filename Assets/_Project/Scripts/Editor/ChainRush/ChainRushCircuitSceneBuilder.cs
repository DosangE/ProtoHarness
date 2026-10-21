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
        public const string SkywayPath = "Assets/_Project/Data/Circuit_Skyway.asset";
        private const string SourceScenePath = "Assets/_Project/Scenes/ChainRushProcedural.unity";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/";
        // One grapple gap in the stadium; four is room for edits.
        private const int AnchorCount = 4;
        // The Skyway's elevated stretch (user 2026-10-22): the road widens to this half width (12 -> 30 m across), and
        // swing poles stand along the long straight every SkywayPoleSpacing, left first, SkywayPoleOffset off the
        // centerline inside the road, hookable from SkywayPoleReach plus the runner's poleReach.
        private const float SkywayWideHalfWidth = 15f;
        private const float SkywayStraightLength = 280f;
        // The long straight is this piece of SkywaySegments.
        private const int SkywayStraightIndex = 10;
        private const int SkywayPoleCount = 7;
        private const float SkywayPoleFirst = 20f;
        private const float SkywayPoleSpacing = 40f;
        private const float SkywayPoleOffset = 11f;
        private const float SkywayPoleReach = 10f;

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

        // Makes the Skyway Loop definition (DECISIONS 2026-10-21 "SKYWAY LOOP", reshaped 2026-10-22): the layout of
        // KartRider's Village Highway traced from its track outline at 0.5 m a pixel, heights from a record run.
        // Clockwise from the start line heading north: the bump's half turn, a jog and the climb (0 -> 12 m), the elevated
        // straight (280 m) between two wide corners, the exit descent (12 -> 9 m), the omega curl, the top edge with its
        // kink, the steep descent (9 -> 0 m) and the U-turn to the line. No gaps, no anchors, two laps.
        // An existing asset is written over in place, so its GUID (the lobby's reference) stays.
        [MenuItem("ProtoHarness/Chain Rush/Create Skyway Track")]
        public static void CreateSkywayTrack()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before creating the track.");

            var existing = AssetDatabase.LoadAssetAtPath<TrackDefinition>(SkywayPath);
            if (existing == null && File.Exists(SkywayPath)) throw new InvalidOperationException("The file at " + SkywayPath + " is not a TrackDefinition.");
            // Built and checked on a new instance first, so a bad layout never reaches the asset.
            var definition = ScriptableObject.CreateInstance<TrackDefinition>();
            var data = new SerializedObject(definition);
            TrackDefinition.Segment[] segments = SkywaySegments();
            SerializedProperty list = data.FindProperty("segments");
            list.arraySize = segments.Length;
            for (int i = 0; i < segments.Length; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("length").floatValue = segments[i].IsArc ? 0f : segments[i].Length;
                element.FindPropertyRelative("radius").floatValue = segments[i].Radius;
                element.FindPropertyRelative("degrees").floatValue = segments[i].Degrees;
                element.FindPropertyRelative("endGrade").floatValue = segments[i].EndGrade;
                element.FindPropertyRelative("endHalfWidth").floatValue = segments[i].EndHalfWidth;
                element.FindPropertyRelative("gap").boolValue = segments[i].IsGap;
            }
            data.FindProperty("anchors").arraySize = 0;
            TrackDefinition.Pole[] poles = SkywayPoles(segments);
            SerializedProperty poleList = data.FindProperty("poles");
            poleList.arraySize = poles.Length;
            for (int i = 0; i < poles.Length; i++)
            {
                SerializedProperty element = poleList.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("s").floatValue = poles[i].S;
                element.FindPropertyRelative("offset").floatValue = poles[i].Offset;
                element.FindPropertyRelative("reach").floatValue = poles[i].Reach;
            }
            data.FindProperty("lapCount").intValue = 2;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (!definition.TryValidate(out string error))
            {
                UnityEngine.Object.DestroyImmediate(definition);
                throw new InvalidOperationException(error);
            }
            if (existing == null) AssetDatabase.CreateAsset(definition, SkywayPath);
            else
            {
                definition.name = existing.name;
                EditorUtility.CopySerialized(definition, existing);
                UnityEngine.Object.DestroyImmediate(definition);
                definition = existing;
                EditorUtility.SetDirty(definition);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ChainRush: Skyway track saved; " + definition.Segments.Count + " segments, " + definition.LapLength.ToString("0.0") + " m a lap, " + definition.LapCount + " laps.", definition);
        }

        private static TrackDefinition.Segment[] SkywaySegments() => new[]
        {
            // The start straight north and the bump's half turn (R28: at R22 the bot ran wide to the guard mid-turn).
            TrackDefinition.Segment.Straight(20.5f), TrackDefinition.Segment.Arc(28f, 180f),
            // Down its right leg, the jog east and the climb south (0 -> +15% -> 0, 12 m) along the right side, where the
            // road widens from 6 to SkywayWideHalfWidth (user 2026-10-22). The jog is R26: at R22 the bot ran 5.61 m wide
            // in its second corner (S 256.8), past the 5.6 m guard limit.
            TrackDefinition.Segment.Straight(56f), TrackDefinition.Segment.Arc(26f, -90f),
            TrackDefinition.Segment.Straight(15f, 0.15f), TrackDefinition.Segment.Arc(26f, 90f, 0.15f),
            TrackDefinition.Segment.Straight(24.22f, 0.15f, 10f), TrackDefinition.Segment.Straight(15f, 0f, 13f),
            TrackDefinition.Segment.Straight(15.78f, 0f, SkywayWideHalfWidth),
            // The elevated road, wide: a wide corner west, the long straight with its swing poles, a wide corner north.
            TrackDefinition.Segment.Arc(60f, 90f, 0f, SkywayWideHalfWidth),
            TrackDefinition.Segment.Straight(SkywayStraightLength, 0f, SkywayWideHalfWidth),
            TrackDefinition.Segment.Arc(65f, 90f, 0f, SkywayWideHalfWidth),
            // Its exit down to the upper village (-10%, 12 m -> 9 m) along the left side, narrowing back to 6.
            TrackDefinition.Segment.Straight(15f, -0.1f, 12f), TrackDefinition.Segment.Straight(15f, -0.1f, 9f), TrackDefinition.Segment.Straight(15f, 0f),
            TrackDefinition.Segment.Straight(10.56f),
            // The omega curl: left, round the bulb to the right, left again to head north. R26 / R30 / R28: with R24 / R28 /
            // R22 the bot ran 5.61 m wide in the last left (S 1028.1), past the 5.6 m guard limit.
            TrackDefinition.Segment.Arc(26f, -90f), TrackDefinition.Segment.Arc(30f, 120f), TrackDefinition.Segment.Arc(30f, 105f),
            TrackDefinition.Segment.Arc(28f, -135f),
            // Up to the top-left corner, then the top edge east with its kink.
            TrackDefinition.Segment.Straight(62.5f), TrackDefinition.Segment.Arc(22f, 90f), TrackDefinition.Segment.Straight(7.5f),
            TrackDefinition.Segment.Arc(30f, 45f), TrackDefinition.Segment.Arc(30f, -45f), TrackDefinition.Segment.Straight(83.06f),
            // Down into the dip: the steep descent south (-20%, 9 m -> 0) and the U-turn back north to the line.
            TrackDefinition.Segment.Arc(22f, 90f),
            TrackDefinition.Segment.Straight(15f, -0.2f), TrackDefinition.Segment.Straight(30f, -0.2f), TrackDefinition.Segment.Straight(15f, 0f),
            TrackDefinition.Segment.Arc(31f, -180f),
        };

        // The swing poles along the Skyway's long straight, alternating left and right.
        private static TrackDefinition.Pole[] SkywayPoles(TrackDefinition.Segment[] segments)
        {
            if (segments[SkywayStraightIndex].IsArc || segments[SkywayStraightIndex].Length != SkywayStraightLength)
                throw new InvalidOperationException($"Skyway piece {SkywayStraightIndex} is no longer the {SkywayStraightLength} m straight; move SkywayStraightIndex.");
            double start = 0d;
            for (int i = 0; i < SkywayStraightIndex; i++) start += segments[i].Length;
            var poles = new TrackDefinition.Pole[SkywayPoleCount];
            for (int i = 0; i < poles.Length; i++)
                poles[i] = new TrackDefinition.Pole((float)(start + SkywayPoleFirst + SkywayPoleSpacing * i), i % 2 == 0 ? -SkywayPoleOffset : SkywayPoleOffset, SkywayPoleReach);
            return poles;
        }

        private static Material RequireMaterial(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + name + ".mat");
            if (material == null) throw new InvalidOperationException("Source material is missing: " + MaterialFolder + name + ".mat");
            return material;
        }
    }
}
