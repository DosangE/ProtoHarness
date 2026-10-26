using System;
using System.Collections.Generic;
using UnityEngine;
using ProtoHarness.ChainRush.Track;
using ProtoHarness.ChainRush.Visuals;
using Object = UnityEngine.Object;

namespace ProtoHarness.ChainRush.Race
{
    // The circuit's scene (COURSE.md T4), made once from a TrackDefinition on its closed centerline: the road in
    // RoadPieces (gaps stay empty, the width follows TrackDefinition.HalfWidthAt), glowing bands on the start line and
    // the checkpoints, and a pole at every arc's center and every swing pole of the definition. The road pieces carry
    // the colliders the runner moves on; the bands and poles are visual only. CircuitRace makes it and keeps the race
    // itself (laps, anchors, snapshots); everything made here hangs under the parent it is given.
    public sealed class CircuitScenery
    {
        private readonly TrackDefinition definition;
        private readonly Centerline track;
        private readonly Transform parent;
        private readonly Material roadMaterial;
        private readonly Material railMaterial;
        private readonly Material lightMaterial;
        private readonly float maxPieceLength;
        private readonly List<RoadPiece> pieces = new List<RoadPiece>(16);
        private readonly List<GameObject> markers = new List<GameObject>(8);
        private readonly List<GameObject> poles = new List<GameObject>(8);
        // Where the main road has no right guard: the shortcuts' crossings (TrackDefinition.ShortcutGuardGaps).
        private readonly List<(double From, double To)> guardGaps;

        // track is definition's closed centerline (TrackDefinition.BuildCenterline).
        public CircuitScenery(TrackDefinition definition, Centerline track, Transform parent, Material road, Material rail, Material light,
            float maxPieceLength)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (track == null) throw new ArgumentNullException(nameof(track));
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (road == null) throw new ArgumentNullException(nameof(road));
            if (rail == null) throw new ArgumentNullException(nameof(rail));
            if (light == null) throw new ArgumentNullException(nameof(light));
            if (!(maxPieceLength > 0f)) throw new ArgumentOutOfRangeException(nameof(maxPieceLength), maxPieceLength, "The longest road piece must be positive.");
            this.definition = definition;
            this.track = track;
            this.parent = parent;
            roadMaterial = road;
            railMaterial = rail;
            lightMaterial = light;
            this.maxPieceLength = maxPieceLength;
            guardGaps = definition.ShortcutGuardGaps();
            guardGaps.Sort((a, b) => a.From.CompareTo(b.From));
        }

        public int PieceCount => pieces.Count;
        public int PoleCount => poles.Count;

        // Road over every stretch between gaps, in pieces of at most maxPieceLength. A stretch of one width is laid in
        // one go; where the width changes, each piece of the definition that tapers is laid on its own, so a road piece
        // never spans a bend in the width. Then each shortcut's road.
        public void LayRoad()
        {
            LayMainRoad();
            IReadOnlyList<TrackDefinition.Shortcut> shortcuts = definition.Shortcuts;
            for (int i = 0; i < shortcuts.Count; i++) LayShortcut(shortcuts[i]);
        }

        private void LayMainRoad()
        {
            IReadOnlyList<TrackDefinition.Segment> segments = definition.Segments;
            double s = 0d;
            double runStart = 0d;
            float runWidth = definition.StartHalfWidth(0);
            for (int i = 0; i < segments.Count; i++)
            {
                double next = s + segments[i].Length;
                float from = definition.StartHalfWidth(i), to = definition.EndHalfWidth(i);
                // A gap, a taper or a new width ends the stretch of one width laid so far.
                if (segments[i].IsGap || from != to || from != runWidth)
                {
                    if (s > runStart) LayRange(runStart, s, runWidth, runWidth);
                    if (segments[i].IsGap) runStart = next;
                    else if (from != to)
                    {
                        LayRange(s, next, from, to);
                        runStart = next;
                    }
                    else runStart = s;
                    runWidth = to;
                }
                s = next;
            }
            if (s > runStart) LayRange(runStart, s, runWidth, runWidth);
        }

        // Thin glowing bands across the road: a wider one on the start line, narrower ones at the checkpoints.
        public void PlaceMarkers(double[] checkpoints)
        {
            if (checkpoints == null) throw new ArgumentNullException(nameof(checkpoints));
            AddMarker("Start line", 0d, 1.2f);
            for (int i = 0; i < checkpoints.Length; i++) AddMarker("Checkpoint " + i, checkpoints[i], 0.5f);
        }

        // A corner pole on every arc's center, and the definition's swing poles (TrackDefinition.Poles), where the swing
        // hooks (Centerline.TryFindPole): a glowing post from
        // SwingLook.PoleDepth below to PoleHeight above it with a cap on top, and a dark band at BiteHeight where the chain
        // bites. Visual only, like the markers.
        public void PlacePoles()
        {
            var points = new List<Vector3>(8);
            track.GetPoles(points);
            float length = SwingLook.PoleHeight + SwingLook.PoleDepth;
            for (int i = 0; i < points.Count; i++)
            {
                var pole = new GameObject("Corner pole " + i);
                pole.transform.SetParent(parent, false);
                pole.transform.position = points[i];
                // The default cylinder is 2 m tall and 1 m across, centred on its origin.
                VisualPart(PrimitiveType.Cylinder, "Post", pole.transform, lightMaterial,
                    Vector3.up * ((SwingLook.PoleHeight - SwingLook.PoleDepth) * 0.5f),
                    new Vector3(SwingLook.PoleThickness, length * 0.5f, SwingLook.PoleThickness));
                VisualPart(PrimitiveType.Sphere, "Cap", pole.transform, lightMaterial,
                    Vector3.up * SwingLook.PoleHeight, Vector3.one * (SwingLook.PoleThickness * 2f));
                VisualPart(PrimitiveType.Cylinder, "Bite band", pole.transform, railMaterial,
                    Vector3.up * SwingLook.BiteHeight, new Vector3(SwingLook.PoleThickness * 1.6f, 0.25f, SwingLook.PoleThickness * 1.6f));
                poles.Add(pole);
            }
        }

        // Destroys the road pieces' objects and meshes. The bands and poles go with the parent.
        public void Destroy()
        {
            for (int i = 0; i < pieces.Count; i++) pieces[i].Destroy();
        }

        // Pieces from from to to, the half width going linearly from fromWidth to toWidth. A piece that a guard gap
        // touches is cut at the gap's ends, the part inside without its right guard.
        private void LayRange(double from, double to, float fromWidth, float toWidth)
        {
            int count = Math.Max(1, (int)Math.Ceiling((to - from) / maxPieceLength - 1e-9));
            for (int i = 0; i < count; i++)
            {
                double start = from + (to - from) * i / count;
                double end = i == count - 1 ? to : from + (to - from) * (i + 1) / count;
                double cutFrom = start;
                for (int g = 0; g <= guardGaps.Count; g++)
                {
                    // The next cut inside the piece, or its end once the gaps are used up.
                    double cut = end;
                    bool open = false;
                    if (g < guardGaps.Count)
                    {
                        if (!(guardGaps[g].To > cutFrom && guardGaps[g].From < end)) continue;
                        if (guardGaps[g].From > cutFrom)
                        {
                            LayPiece(track, cutFrom, guardGaps[g].From, from, to, fromWidth, toWidth, true, true);
                            cutFrom = guardGaps[g].From;
                        }
                        cut = Math.Min(end, guardGaps[g].To);
                        open = true;
                    }
                    if (cut > cutFrom) LayPiece(track, cutFrom, cut, from, to, fromWidth, toWidth, true, !open);
                    cutFrom = cut;
                }
            }
        }

        // One road piece from start to end of the stretch from .. to whose half width goes from fromWidth to toWidth.
        private void LayPiece(Centerline line, double start, double end, double from, double to, float fromWidth, float toWidth, bool leftGuard, bool rightGuard)
        {
            RoadProfile profile = fromWidth == toWidth
                ? new RoadProfile(fromWidth, definition.RoadThickness, leftGuard, rightGuard)
                : new RoadProfile(WidthBetween(fromWidth, toWidth, (start - from) / (to - from)),
                    WidthBetween(fromWidth, toWidth, (end - from) / (to - from)), definition.RoadThickness, leftGuard, rightGuard);
            var piece = new RoadPiece("Circuit road " + pieces.Count, parent, roadMaterial, railMaterial, lightMaterial);
            piece.Build(line, start, end, profile);
            pieces.Add(piece);
        }

        // A shortcut's road on its own line (TrackDefinition.ShortcutLine): it starts where its inner edge leaves the main
        // road and ends where it meets the main road again. Its inner (right) guard stands from there, its outer (left)
        // guard only once the outer edge is off the main road too, so neither crosses the main road.
        private void LayShortcut(TrackDefinition.Shortcut shortcut)
        {
            Centerline line = TrackDefinition.ShortcutLine(track, shortcut);
            TrackDefinition.Junction(shortcut, definition.HalfWidthAt(shortcut.FromS), out _, out _, out float leaveInner, out float leaveOuter);
            TrackDefinition.Junction(shortcut, definition.HalfWidthAt(shortcut.ToS), out _, out _, out float joinInner, out float joinOuter);
            double length = line.EndS;
            float width = shortcut.HalfWidth;
            LayPiece(line, leaveInner, leaveOuter, 0d, length, width, width, false, true);
            double middle = length - joinOuter;
            int count = Math.Max(1, (int)Math.Ceiling((middle - leaveOuter) / maxPieceLength - 1e-9));
            for (int i = 0; i < count; i++)
                LayPiece(line, leaveOuter + (middle - leaveOuter) * i / count, i == count - 1 ? middle : leaveOuter + (middle - leaveOuter) * (i + 1) / count,
                    0d, length, width, width, true, true);
            LayPiece(line, middle, length - joinInner, 0d, length, width, width, false, true);
        }

        private static float WidthBetween(float from, float to, double t) => from + (to - from) * (float)t;

        private void AddMarker(string name, double s, float length)
        {
            TrackFrame frame = track.FrameAt(s);
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = name;
            // Visual only: it must never collide, even in the frame before the collider is destroyed.
            Collider collider = marker.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            marker.GetComponent<Renderer>().sharedMaterial = lightMaterial;
            marker.transform.SetParent(parent, false);
            marker.transform.SetPositionAndRotation(frame.Position + Vector3.up * 0.04f, Quaternion.LookRotation(frame.Forward));
            marker.transform.localScale = new Vector3(definition.HalfWidthAt(s) * 2f, 0.06f, length);
            markers.Add(marker);
        }

        private static void VisualPart(PrimitiveType shape, string name, Transform parent, Material material, Vector3 localPosition, Vector3 localScale)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            // Visual only: it must never collide, even in the frame before the collider is destroyed.
            Collider collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial = material;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
        }
    }
}
