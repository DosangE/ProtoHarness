using System;
using System.Collections.Generic;
using System.IO;
using ProtoHarness.ChainRush.Track;
using UnityEditor;
using UnityEngine;

namespace ProtoHarness.Editor.ChainRush
{
    // Makes circuit track definitions. The circuit race scene itself was made once (COURSE.md T4) from the procedural
    // scene, which is gone with the endless mode (DECISIONS 2026-10-25 "무한 모드 삭제"); it is now edited in the editor.
    public static class ChainRushCircuitSceneBuilder
    {
        public const string SkywayPath = "Assets/_Project/Data/Circuit_Skyway.asset";
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

        public const string FingerLanesPath = "Assets/_Project/Data/Circuit_FingerLanes.asset";
        // Finger Lanes from the Village Finger outline (rush.webp, 500x256 pixels): the columns where the road's centre
        // runs. (The clock tower's rows no longer place it: the left edge was reshaped, user 2026-10-28.)
        private const int FingerLeftEdgeColumn = 19;
        private const int FingerStartColumn = 81;
        private const int FingerTurnColumn = 83;
        private const int FingerFirstTipColumn = 126;
        private const int FingerLongTipColumn = 154;
        private const int FingerShortTipColumn = 130;
        // Where the nail shortcuts leave the two long fingers (DECISIONS 2026-10-25 "FINGER LANES"): through a narrow gap in
        // the right guard, a tight right quarter turn of FingerNailRadius, across the island, another one onto the next row
        // (user 2026-10-25, the record video at 12.2-13.1 s; tighter than the runner's own 25 m turn, so it takes the swing).
        private const int FingerNailColumn = 132;
        private const int FingerLastNailColumn = 128;
        private const float FingerNailRadius = 15f;
        private const float FingerNailHalfWidth = FingerRoadHalfWidth * 0.5f;
        private const float FingerMetresPerPixel = 1f;
        // The half turns at the runner's own tightest turn (user 2026-10-25: R28 dragged on; still 180). At R22 the bot,
        // which only steers, ran out to 6.6-6.8 m of the 7.2 m road and lost a fifth of its lap time; tighter than this is
        // for the pole swing.
        private const float FingerUTurnRadius = 25f;
        private const float FingerCornerRadius = 30f;
        // 1.2 times the usual road (user 2026-10-25).
        private const float FingerRoadHalfWidth = RoadProfile.DeckHalfWidth * 1.2f;
        // Both jumps are a bump, not a climb (user 2026-10-25, the record video at 24.5-28.4 s): four pieces of FingerBump
        // at the steepest grade a track allows, 0.5 m up and down over 10 m, the kicker's lip on its top (grade to flat,
        // where the road starts to fall away: kicking on a climb, the road still rose and held the runner down). The bump
        // throws the runner by its speed.
        private const float FingerSteepGrade = TrackDefinition.MaxGrade;
        private const float FingerBump = 2.5f;
        // The bump of the stone bridge near the bottom edge's end, then a flat landing before the corner into the square.
        private const float FingerBridgeLanding = 20f;
        private const float FingerBridgeLaunch = 0.3f;
        // The left edge north (user 2026-10-28, "the clock tower's straight is far too long; its bump should throw you like
        // the one before the corner"): FingerTowerApproach from the corner, the clock tower — the road narrows to
        // FingerTowerHalfWidth over FingerTowerTaper (0.2 m a metre, inside TrackDefinition.MaxWidthChange), runs
        // FingerTowerInside through it and widens back — then a bump on the full road and its landing before an S bend, as
        // the bridge's before its corner. The S bend (left then right, FingerJogDegrees at FingerJogRadius) moves the edge
        // west and splits its 290 m straight in two.
        private const float FingerTowerApproach = 53f;
        private const float FingerTowerHalfWidth = 4f;
        private const float FingerTowerTaper = 16f;
        private const float FingerTowerInside = 30f;
        private const float FingerTowerLaunch = FingerBridgeLaunch;
        private const float FingerTowerLanding = FingerBridgeLanding;
        private const float FingerJogRadius = 30f;
        private const float FingerJogDegrees = 45f;
        // The long fingers and the pieces their shortcuts come back to, in FingerLanesSegments.
        private const int FingerLongIndex = 4;
        private const int FingerLongReturnIndex = 6;
        private const int FingerLastLongIndex = 12;
        private const int FingerLastLongReturnIndex = 14;
        // One checkpoint before, between and after the two shortcut stretches.
        private static readonly float[] FingerCheckpoints = { 0.15f, 0.5f, 0.75f };

        public const string PalmCoastPath = "Assets/_Project/Data/Circuit_PalmCoast.asset";
        private const float PalmCornerRadius = 25f;
        private const float PalmRoadHalfWidth = FingerRoadHalfWidth;
        private const float PalmBeachRadius = 60f;
        private const float PalmBeachDegrees = 10f;
        private const float PalmFinishGrade = 0.03f;
        private const float PalmFinishRise = 2f;
        private const float PalmGradeEase = 10f;
        private static readonly float[] PalmCheckpoints = { 0.15f, 0.5f, 0.75f };

        // Palm Coast from the Beach Drive outline (user 2026-10-30, 4 m a pixel): P0 is the start heading north,
        // x east and z north. Corners replace each vertex by its tangent arc; no leg lengths need closure adjustments.
        private static readonly Vector2[] PalmVertices =
        {
            new Vector2(0f, 0f), new Vector2(0f, 248f), new Vector2(152f, 248f), new Vector2(152f, 344f),
            new Vector2(-92f, 344f), new Vector2(-92f, 124f), new Vector2(-188f, 124f), new Vector2(-296f, 232f),
            new Vector2(-360f, 152f), new Vector2(-360f, 72f), new Vector2(-204f, 72f), new Vector2(-204f, -8f),
            new Vector2(-152f, -8f), new Vector2(-152f, -68f), new Vector2(0f, -68f),
        };

        // Makes the Skyway Loop definition (DECISIONS 2026-10-21 "SKYWAY LOOP", reshaped 2026-10-22): the layout of
        // KartRider's Village Highway traced from its track outline at 0.5 m a pixel, heights from a record run.
        // Clockwise from the start line heading north: the bump's half turn, a jog and the climb (0 -> 12 m), the elevated
        // straight (280 m) between two wide corners, the exit descent (12 -> 9 m), the omega curl, the top edge with its
        // kink, the steep descent (9 -> 0 m) and the U-turn to the line. No gaps, no anchors, two laps.
        // An existing asset is written over in place, so its GUID (the lobby's reference) stays.
        [MenuItem("ProtoHarness/Chain Rush/Create Skyway Track")]
        public static void CreateSkywayTrack()
        {
            TrackDefinition.Segment[] segments = SkywaySegments();
            SaveTrack(SkywayPath, "Skyway", segments, SkywayPoles(segments), null, 2);
        }

        // Makes the Finger Lanes definition (DECISIONS 2026-10-25 "FINGER LANES"): KartRider's Village Finger traced from
        // its Rush+ track outline (500x256), the road's centre rows 15 px apart. The U-turns set the scale across the rows
        // (two FingerUTurnRadius a row, R28 being the half turn the bot holds, DECISIONS 2026-10-21 "SKYWAY LOOP"), and the
        // rows' lengths are FingerMetresPerPixel of the outline's (user 2026-10-25, plan A). Clockwise from the start mark on
        // the top row heading east: seven fingers joined by half turns (right, left, ... right), the bottom edge west over a
        // stone bridge's bump, the left edge north through the clock tower's narrow passage, over a bump past it and round
        // an S bend west (2026-10-28), the top-left corner to the line. The road is 1.2 times the usual width; both bumps are kickers that throw a runner
        // by its speed. No gaps, two laps.
        // The nail shortcuts of the two long fingers cut across to the next row; their stretches hold no checkpoint.
        [MenuItem("ProtoHarness/Chain Rush/Create Finger Lanes Track")]
        public static void CreateFingerLanesTrack()
        {
            TrackDefinition.Segment[] segments = FingerLanesSegments();
            double lap = StartOf(segments, segments.Length);
            for (int c = 0; c < FingerCheckpoints.Length; c++)
                foreach ((double from, double to) in FingerShortcutStretches(segments))
                    if (FingerCheckpoints[c] * lap >= from && FingerCheckpoints[c] * lap <= to)
                        throw new InvalidOperationException($"Finger Lanes checkpoint {c} (S {FingerCheckpoints[c] * lap:F1}) is inside a shortcut's stretch [{from:F1}, {to:F1}].");
            (double From, double To)[] stretches = FingerShortcutStretches(segments);
            var shortcuts = new TrackDefinition.Shortcut[stretches.Length];
            for (int i = 0; i < stretches.Length; i++)
                shortcuts[i] = new TrackDefinition.Shortcut((float)stretches[i].From, (float)stretches[i].To, FingerNailRadius,
                    2f * FingerUTurnRadius - 2f * FingerNailRadius, FingerNailHalfWidth);
            SaveTrack(FingerLanesPath, "Finger Lanes", segments, Array.Empty<TrackDefinition.Pole>(), FingerCheckpoints, 2, FingerRoadHalfWidth, shortcuts);
        }

        // Four road pads, no gaps or kickers, two counterclockwise laps. SaveTrack prints every pad's S after validation.
        [MenuItem("ProtoHarness/Chain Rush/Create Palm Coast Track")]
        public static void CreatePalmCoastTrack()
        {
            SaveTrack(PalmCoastPath, "PALM COAST", PalmCoastSegments(), Array.Empty<TrackDefinition.Pole>(), PalmCheckpoints, 2, PalmRoadHalfWidth);
        }

        // Builds the definition on a new instance, checks it (TryValidate) and only then writes it to path: a new asset, or
        // over the existing one in place so its GUID (the lobby's reference) and best records stay. Null checkpoints and a
        // road half width of 0 keep the definition's defaults; no shortcuts leaves none.
        private static void SaveTrack(string path, string label, TrackDefinition.Segment[] segments, TrackDefinition.Pole[] poles,
            float[] checkpoints, int lapCount, float roadHalfWidth = 0f, TrackDefinition.Shortcut[] shortcuts = null)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before creating the track.");

            var existing = AssetDatabase.LoadAssetAtPath<TrackDefinition>(path);
            if (existing == null && File.Exists(path)) throw new InvalidOperationException("The file at " + path + " is not a TrackDefinition.");
            // Built and checked on a new instance first, so a bad layout never reaches the asset.
            var definition = ScriptableObject.CreateInstance<TrackDefinition>();
            var data = new SerializedObject(definition);
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
                element.FindPropertyRelative("launch").floatValue = segments[i].Launch;
                element.FindPropertyRelative("boostZone").boolValue = segments[i].HasBoostZone;
                element.FindPropertyRelative("boostAt").floatValue = segments[i].BoostAt;
            }
            data.FindProperty("anchors").arraySize = 0;
            SerializedProperty poleList = data.FindProperty("poles");
            poleList.arraySize = poles.Length;
            for (int i = 0; i < poles.Length; i++)
            {
                SerializedProperty element = poleList.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("s").floatValue = poles[i].S;
                element.FindPropertyRelative("offset").floatValue = poles[i].Offset;
                element.FindPropertyRelative("reach").floatValue = poles[i].Reach;
            }
            SerializedProperty shortcutList = data.FindProperty("shortcuts");
            shortcutList.arraySize = shortcuts == null ? 0 : shortcuts.Length;
            for (int i = 0; i < shortcutList.arraySize; i++)
            {
                SerializedProperty element = shortcutList.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("fromS").floatValue = shortcuts[i].FromS;
                element.FindPropertyRelative("toS").floatValue = shortcuts[i].ToS;
                element.FindPropertyRelative("radius").floatValue = shortcuts[i].Radius;
                element.FindPropertyRelative("straight").floatValue = shortcuts[i].Straight;
                element.FindPropertyRelative("halfWidth").floatValue = shortcuts[i].HalfWidth;
            }
            if (checkpoints != null)
            {
                SerializedProperty checkpointList = data.FindProperty("checkpointFractions");
                checkpointList.arraySize = checkpoints.Length;
                for (int i = 0; i < checkpoints.Length; i++) checkpointList.GetArrayElementAtIndex(i).floatValue = checkpoints[i];
            }
            data.FindProperty("lapCount").intValue = lapCount;
            if (roadHalfWidth > 0f) data.FindProperty("roadHalfWidth").floatValue = roadHalfWidth;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (!definition.TryValidate(out string error))
            {
                UnityEngine.Object.DestroyImmediate(definition);
                throw new InvalidOperationException(error);
            }
            if (existing == null) AssetDatabase.CreateAsset(definition, path);
            else
            {
                definition.name = existing.name;
                EditorUtility.CopySerialized(definition, existing);
                UnityEngine.Object.DestroyImmediate(definition);
                definition = existing;
                EditorUtility.SetDirty(definition);
            }
            AssetDatabase.SaveAssets();
            if (path == PalmCoastPath)
            {
                var zones = new List<double>(4);
                definition.BuildCenterline(Vector3.zero, 0f).GetBoostZones(zones);
                string positions = string.Join(", ", zones.ConvertAll(s => "S" + s.ToString("0.00")));
                bool valid = definition.TryValidate(out _);
                Debug.Log($"ChainRush: {label} segments={segments.Length} lap={definition.LapLength:F2} laps={definition.LapCount} boostZones: {positions} valid={valid}", definition);
            }
            else Debug.Log("ChainRush: " + label + " track saved; " + definition.Segments.Count + " segments, " + definition.LapLength.ToString("0.0") + " m a lap, " + definition.LapCount + " laps.", definition);
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

        private static TrackDefinition.Segment[] FingerLanesSegments()
        {
            const float u = FingerUTurnRadius, corner = FingerCornerRadius;
            float rows = 7f * 2f * u;
            float longFinger = FingerX(FingerLongTipColumn) - FingerX(FingerTurnColumn);
            float shortFinger = FingerX(FingerShortTipColumn) - FingerX(FingerTurnColumn);
            const float g = FingerSteepGrade, narrow = FingerTowerHalfWidth;
            float bottom = FingerX(FingerLongTipColumn) - corner;
            float bump = 4f * FingerBump;
            float tower = 2f * FingerTowerTaper + FingerTowerInside;
            float jogNorth = 2f * FingerJogRadius * Mathf.Sin(FingerJogDegrees * Mathf.Deg2Rad);
            float jogWest = 2f * FingerJogRadius * (1f - Mathf.Cos(FingerJogDegrees * Mathf.Deg2Rad));
            float afterJog = rows - 2f * corner - FingerTowerApproach - tower - bump - FingerTowerLanding - jogNorth;
            return new[]
            {
                // The top row east from the start mark to the first fingertip, and the seven fingers: half turns right at
                // the tips, left at the turn column; the second and the fourth finger (pieces 4 and 12) are the long ones.
                TrackDefinition.Segment.Straight(FingerX(FingerFirstTipColumn) - FingerX(FingerStartColumn)), TrackDefinition.Segment.Arc(u, 180f),
                TrackDefinition.Segment.Straight(FingerX(FingerFirstTipColumn) - FingerX(FingerTurnColumn)), TrackDefinition.Segment.Arc(u, -180f),
                TrackDefinition.Segment.Straight(longFinger), TrackDefinition.Segment.Arc(u, 180f),
                TrackDefinition.Segment.Straight(longFinger), TrackDefinition.Segment.Arc(u, -180f),
                TrackDefinition.Segment.Straight(shortFinger), TrackDefinition.Segment.Arc(u, 180f),
                TrackDefinition.Segment.Straight(shortFinger), TrackDefinition.Segment.Arc(u, -180f),
                TrackDefinition.Segment.Straight(longFinger), TrackDefinition.Segment.Arc(u, 180f),
                // The bottom edge west to the stone bridge's bump near its end, thrown off it by speed, landing before the
                // corner into the square.
                TrackDefinition.Segment.Straight(bottom - bump - FingerBridgeLanding),
                TrackDefinition.Segment.Straight(FingerBump, g), TrackDefinition.Segment.Kicker(FingerBump, 0f, FingerBridgeLaunch),
                TrackDefinition.Segment.Straight(FingerBump, -g), TrackDefinition.Segment.Straight(FingerBump, 0f),
                TrackDefinition.Segment.Straight(FingerBridgeLanding),
                // Up the left edge through the clock tower: the narrow entrance, the way through (piece 23) and wide again,
                // the bump that throws the runner by speed and its landing, the S bend west, and on to the top-left corner.
                TrackDefinition.Segment.Arc(corner, 90f),
                TrackDefinition.Segment.Straight(FingerTowerApproach),
                TrackDefinition.Segment.Straight(FingerTowerTaper, 0f, narrow), TrackDefinition.Segment.Straight(FingerTowerInside, 0f, narrow),
                TrackDefinition.Segment.Straight(FingerTowerTaper),
                TrackDefinition.Segment.Straight(FingerBump, g), TrackDefinition.Segment.Kicker(FingerBump, 0f, FingerTowerLaunch),
                TrackDefinition.Segment.Straight(FingerBump, -g), TrackDefinition.Segment.Straight(FingerBump, 0f),
                TrackDefinition.Segment.Straight(FingerTowerLanding),
                TrackDefinition.Segment.Arc(FingerJogRadius, -FingerJogDegrees), TrackDefinition.Segment.Arc(FingerJogRadius, FingerJogDegrees),
                TrackDefinition.Segment.Straight(afterJog),
                // The top-left corner and the top row east to the line, longer by the S bend's step west.
                TrackDefinition.Segment.Arc(corner, 90f), TrackDefinition.Segment.Straight(FingerX(FingerStartColumn) - corner + jogWest),
            };
        }

        private static TrackDefinition.Segment[] PalmCoastSegments()
        {
            int count = PalmVertices.Length;
            var turns = new float[count];
            var radii = new float[count];
            var tangents = new float[count];
            for (int i = 1; i < count; i++)
            {
                Vector2 incoming = PalmVertices[i] - PalmVertices[i - 1];
                Vector2 outgoing = PalmVertices[(i + 1) % count] - PalmVertices[i];
                turns[i] = Mathf.DeltaAngle(Mathf.Atan2(incoming.x, incoming.y) * Mathf.Rad2Deg,
                    Mathf.Atan2(outgoing.x, outgoing.y) * Mathf.Rad2Deg);
                radii[i] = i == 5 ? 48f : i == 8 ? 60f : PalmCornerRadius;
                tangents[i] = radii[i] * Mathf.Tan(Mathf.Abs(turns[i]) * Mathf.Deg2Rad * 0.5f);
            }
            // In the centerline's convention right is positive: P7 is -96.3402 degrees, P8 -38.6598, total -360.
            // Even P11 -> P12 keeps 2 m between its R25 tangencies, so no radius reduction is needed.
            float plateau = PalmFinishRise / PalmFinishGrade - PalmGradeEase;
            float hill = plateau + 2f * PalmGradeEase;
            var segments = new List<TrackDefinition.Segment>(48);
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                float length = Vector2.Distance(PalmVertices[i], PalmVertices[next]) - tangents[i] - tangents[next];
                if (length < 0f) throw new InvalidOperationException($"Palm Coast P{i} -> P{next} has overlapping corner tangencies ({length:F3} m).");
                if (i == 0)
                {
                    // The finish's 2 m rise unwinds after the line, easing to -3%, holding it and easing flat again.
                    segments.Add(TrackDefinition.Segment.Straight(PalmGradeEase, -PalmFinishGrade));
                    segments.Add(TrackDefinition.Segment.Straight(plateau, -PalmFinishGrade));
                    segments.Add(TrackDefinition.Segment.Straight(PalmGradeEase));
                    segments.Add(TrackDefinition.Segment.BoostStraight(length - hill, 110f - hill));
                }
                else if (i == 1 || i == 2) segments.Add(TrackDefinition.Segment.BoostStraight(length, length * 0.5f));
                else if (i == 3)
                {
                    // Two mirrored S bends (left-right, right-left) keep both beach endpoints at z 344 and head west.
                    // Their forward run replaces exactly 4 R sin(10 degrees) of the trimmed 194 m leg: no closure fix.
                    float straight = (length - 4f * PalmBeachRadius * Mathf.Sin(PalmBeachDegrees * Mathf.Deg2Rad)) / 3f;
                    if (!(straight > 0f)) throw new InvalidOperationException("Palm Coast's beach is too short for its S bends.");
                    segments.Add(TrackDefinition.Segment.Straight(straight));
                    segments.Add(TrackDefinition.Segment.Arc(PalmBeachRadius, -PalmBeachDegrees));
                    segments.Add(TrackDefinition.Segment.Arc(PalmBeachRadius, PalmBeachDegrees));
                    segments.Add(TrackDefinition.Segment.Straight(straight));
                    segments.Add(TrackDefinition.Segment.Arc(PalmBeachRadius, PalmBeachDegrees));
                    segments.Add(TrackDefinition.Segment.Arc(PalmBeachRadius, -PalmBeachDegrees));
                    segments.Add(TrackDefinition.Segment.Straight(straight));
                }
                else if (i == 13)
                {
                    // Z4 is 15 m past P13's corner exit, on the +3% stretch before the finish, rising exactly 2 m.
                    segments.Add(TrackDefinition.Segment.Straight(PalmGradeEase, PalmFinishGrade));
                    segments.Add(TrackDefinition.Segment.BoostStraight(plateau, 15f - PalmGradeEase, PalmFinishGrade));
                    segments.Add(TrackDefinition.Segment.Straight(PalmGradeEase));
                    segments.Add(TrackDefinition.Segment.Straight(length - hill));
                }
                else if (length > 0f) segments.Add(TrackDefinition.Segment.Straight(length));
                if (next != 0) segments.Add(TrackDefinition.Segment.Arc(radii[next], turns[next]));
            }
            return segments.ToArray();
        }

        // Metres east of the left edge's centreline for an outline column.
        private static float FingerX(int column) => (column - FingerLeftEdgeColumn) * FingerMetresPerPixel;

        // The S stretches the two nail shortcuts will skip: from where one leaves a long finger to where it comes back
        // on the row after the fingertip.
        private static (double From, double To)[] FingerShortcutStretches(TrackDefinition.Segment[] segments)
        {
            foreach (int index in new[] { FingerLongIndex, FingerLongReturnIndex, FingerLastLongIndex, FingerLastLongReturnIndex })
                if (segments[index].IsArc) throw new InvalidOperationException($"Finger Lanes piece {index} is no longer a straight; move the shortcut indices.");
            return new[]
            {
                (StartOf(segments, FingerLongIndex) + (FingerX(FingerNailColumn) - FingerX(FingerTurnColumn)),
                    StartOf(segments, FingerLongReturnIndex) + (FingerX(FingerLongTipColumn) - FingerX(FingerNailColumn))),
                (StartOf(segments, FingerLastLongIndex) + (FingerX(FingerLastNailColumn) - FingerX(FingerTurnColumn)),
                    StartOf(segments, FingerLastLongReturnIndex) + (FingerX(FingerLongTipColumn) - FingerX(FingerLastNailColumn))),
            };
        }

        private static double StartOf(TrackDefinition.Segment[] segments, int index)
        {
            double start = 0d;
            for (int i = 0; i < index; i++) start += segments[i].Length;
            return start;
        }
    }
}
