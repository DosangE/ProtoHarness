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
        private Material boostMaterial;
        private Material boostArrowMaterial;
        private readonly List<Mesh> markingMeshes = new List<Mesh>(48);
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

        // Dark green pads across almost the full road, 6 m long and 3 cm above it, with bright ">>>" arrows in three
        // lanes pointing the way to go (user 2026-10-30: "초록색 판 띡 하나 놓는게 아닌 화살표"). Visual only, like the
        // corner poles.
        public void PlaceBoostZones()
        {
            if (track.BoostZoneCount == 0) return;
            var zones = new List<double>(track.BoostZoneCount);
            track.GetBoostZones(zones);
            if (boostMaterial == null)
            {
                boostMaterial = new Material(lightMaterial) { name = "Boost pad", color = new Color(0.04f, 0.42f, 0.16f) };
                if (boostMaterial.HasProperty("_EmissionColor")) boostMaterial.SetColor("_EmissionColor", new Color(0.02f, 0.25f, 0.08f));
                boostArrowMaterial = new Material(lightMaterial) { name = "Boost pad arrow", color = new Color(0.55f, 1f, 0.45f) };
                if (boostArrowMaterial.HasProperty("_EmissionColor")) boostArrowMaterial.SetColor("_EmissionColor", new Color(0.4f, 1f, 0.35f));
            }
            for (int i = 0; i < zones.Count; i++)
            {
                TrackFrame frame = track.FrameAt(zones[i]);
                var pad = new GameObject("Boost zone " + i);
                pad.transform.SetParent(parent, false);
                Vector3 forward = frame.Forward + Vector3.up * frame.Grade;
                Quaternion rotation = Quaternion.LookRotation(forward);
                pad.transform.SetPositionAndRotation(frame.Position + rotation * Vector3.up * 0.045f, rotation);
                float half = definition.HalfWidthAt(zones[i]);
                VisualPart(PrimitiveType.Cube, "Pad", pad.transform, boostMaterial, Vector3.zero, new Vector3(half * 1.9f, 0.03f, 6f));
                for (int lane = -1; lane <= 1; lane++)
                    for (int row = -1; row <= 1; row++)
                        Chevron(pad.transform, new Vector3(lane * half * 0.6f, 0.025f, row * 1.8f), 1f, 0f, boostArrowMaterial);
            }
        }

        // A ">" of two flat bars in the pad's plane, its point at tip, pointing along direction (degrees round the
        // pad's up; 0 is forward), the arms span wide either side.
        private static void Chevron(Transform parent, Vector3 tip, float span, float direction, Material material)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Quaternion arm = Quaternion.Euler(0f, direction + side * 135f, 0f);
                Vector3 middle = tip + arm * Vector3.forward * span * 0.5f;
                GameObject bar = VisualPart(PrimitiveType.Cube, "Arrow bar", parent, material, middle, new Vector3(0.35f, 0.02f, span * 1.05f));
                bar.transform.localRotation = arm;
            }
        }

        // Lane markings painted on the main road (a track look's, TrackLook.Markings): a line inside each edge, a dashed
        // middle line, and three white arrows before every corner of 30 degrees or more, pointing the way it turns. Flat
        // strips 2 cm above the road on the centerline's own frames, so they follow every bend and slope; none over a
        // gap. One mesh per stretch of maxPieceLength; no colliders.
        public void PaintMarkings(Material markings)
        {
            if (markings == null) throw new ArgumentNullException(nameof(markings));
            List<(double Start, double End)> gaps = definition.Gaps();
            var vertices = new List<Vector3>(4096);
            var triangles = new List<int>(6144);
            double lap = track.EndS;
            int chunk = 0;
            for (double from = 0d; from < lap; from += maxPieceLength)
            {
                double to = Math.Min(lap, from + maxPieceLength);
                Vector3 origin = track.FrameAt(from).Position;
                vertices.Clear();
                triangles.Clear();
                for (double s = from; s < to - 1e-6; s += 1d)
                {
                    double next = Math.Min(to, s + 1d);
                    if (InGap(gaps, s) || InGap(gaps, next)) continue;
                    float half = definition.HalfWidthAt(s);
                    Strip(s, next, half - 0.65f, half - 0.4f, origin, vertices, triangles);
                    Strip(s, next, -half + 0.4f, -half + 0.65f, origin, vertices, triangles);
                    // Dashes 3 m on, 3 m off.
                    if ((int)(s / 3d) % 2 == 0) Strip(s, next, -0.1f, 0.1f, origin, vertices, triangles);
                }
                PaintCornerArrows(from, to, gaps, origin, vertices, triangles);
                if (vertices.Count == 0) continue;
                var mesh = new Mesh { name = "Road markings " + chunk };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                markingMeshes.Add(mesh);
                var painted = new GameObject("Road markings " + chunk++);
                painted.transform.SetParent(parent, false);
                painted.transform.position = origin;
                painted.AddComponent<MeshFilter>().sharedMesh = mesh;
                painted.AddComponent<MeshRenderer>().sharedMaterial = markings;
            }
        }

        // Three arrows across the road 4, 8 and 12 m before each corner's start, pointing to its inside.
        private void PaintCornerArrows(double from, double to, List<(double Start, double End)> gaps, Vector3 origin, List<Vector3> vertices, List<int> triangles)
        {
            double s = 0d;
            foreach (TrackDefinition.Segment segment in definition.Segments)
            {
                double start = s;
                s += segment.Length;
                if (!segment.IsArc || Mathf.Abs(segment.Degrees) < 30f) continue;
                // Right is positive: an arrow for a right-hand corner points to the right (+x across the road).
                float point = segment.Degrees > 0f ? 1f : -1f;
                for (int k = 1; k <= 3; k++)
                {
                    double at = start - 4d * k;
                    if (at < from || at >= to || at < 0d || InGap(gaps, at)) continue;
                    TrackFrame frame = track.FrameAt(at);
                    for (int arm = -1; arm <= 1; arm += 2)
                    {
                        Vector3 a = new Vector3(-point * 0.8f, 0.02f, arm * 1.4f), b = new Vector3(point * 0.8f, 0.02f, 0f);
                        Vector3 across = Vector3.Cross(Vector3.up, b - a).normalized * 0.25f;
                        Quad(frame.TransformPoint(a - across) - origin, frame.TransformPoint(a + across) - origin,
                            frame.TransformPoint(b + across) - origin, frame.TransformPoint(b - across) - origin, vertices, triangles);
                    }
                }
            }
        }

        // A flat strip from left to right across the road (metres right of the centerline) between two S, 2 cm up.
        private void Strip(double fromS, double toS, float left, float right, Vector3 origin, List<Vector3> vertices, List<int> triangles)
        {
            TrackFrame a = track.FrameAt(fromS), b = track.FrameAt(toS);
            Quad(a.TransformPoint(new Vector3(left, 0.02f, 0f)) - origin, b.TransformPoint(new Vector3(left, 0.02f, 0f)) - origin,
                b.TransformPoint(new Vector3(right, 0.02f, 0f)) - origin, a.TransformPoint(new Vector3(right, 0.02f, 0f)) - origin, vertices, triangles);
        }

        // Corners in order round the quad; both windings, so it shows from above whichever way the corners go round.
        private static void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, List<Vector3> vertices, List<int> triangles)
        {
            int first = vertices.Count;
            vertices.Add(p0);
            vertices.Add(p1);
            vertices.Add(p2);
            vertices.Add(p3);
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
            triangles.Add(first); triangles.Add(first + 3); triangles.Add(first + 2);
        }

        private static bool InGap(List<(double Start, double End)> gaps, double s)
        {
            for (int i = 0; i < gaps.Count; i++) if (s >= gaps[i].Start && s <= gaps[i].End) return true;
            return false;
        }

        // Destroys the road pieces' objects and meshes, the markings' meshes and the pads' materials. The bands, poles,
        // pads and markings' objects go with the parent.
        public void Destroy()
        {
            for (int i = 0; i < pieces.Count; i++) pieces[i].Destroy();
            for (int i = 0; i < markingMeshes.Count; i++) Object.Destroy(markingMeshes[i]);
            if (boostMaterial != null) Object.Destroy(boostMaterial);
            if (boostArrowMaterial != null) Object.Destroy(boostArrowMaterial);
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

        private static GameObject VisualPart(PrimitiveType shape, string name, Transform parent, Material material, Vector3 localPosition, Vector3 localScale)
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
            return part;
        }
    }
}
