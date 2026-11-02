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
        // Where the main road has no guard on one side (Left): the shortcuts' crossings on the right
        // (TrackDefinition.ShortcutGuardGaps) and the bays (BayGuardGaps).
        private readonly List<(double From, double To, bool Left)> guardGaps = new List<(double From, double To, bool Left)>(4);
        private readonly List<double> cuts = new List<double>(8);

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
            foreach ((double from, double to) in definition.ShortcutGuardGaps()) guardGaps.Add((from, to, false));
            guardGaps.AddRange(definition.BayGuardGaps());
            guardGaps.Sort((a, b) => a.From.CompareTo(b.From));
        }

        public int PieceCount => pieces.Count;
        public int PoleCount => poles.Count;

        // Road over every stretch between gaps, in pieces of at most maxPieceLength. A stretch of one width is laid in
        // one go; where the width changes, each piece of the definition that tapers is laid on its own, so a road piece
        // never spans a bend in the width. Then each shortcut's road and each bay's.
        public void LayRoad()
        {
            LayMainRoad();
            IReadOnlyList<TrackDefinition.Shortcut> shortcuts = definition.Shortcuts;
            for (int i = 0; i < shortcuts.Count; i++) LayShortcut(shortcuts[i]);
            IReadOnlyList<TrackDefinition.Bay> bays = definition.Bays;
            for (int i = 0; i < bays.Count; i++) LayBay(bays[i]);
        }

        // A bay's road beside the main road (TrackDefinition.Bay): a slab whose inner edge stays on the main road's edge
        // while its outer edge, with its guard, moves out over the taper; then the full width in pieces of at most
        // maxPieceLength, the last closed by a guard square across its end. No guard on the main road's side: the two
        // are one road.
        private void LayBay(TrackDefinition.Bay bay)
        {
            if (bay.StraightDeck)
            {
                LayDeck(bay);
                return;
            }
            float main = definition.HalfWidthAt(bay.FromS);
            float side = bay.Left ? -1f : 1f;
            // A slab cannot be zero wide, so the taper starts from a sliver.
            const float Sliver = 0.05f;
            LayBayPiece(bay.FromS, bay.FromS + bay.Taper, Sliver, bay.Width, main, side, false);
            double from = bay.FromS + bay.Taper;
            int count = Math.Max(1, (int)Math.Ceiling((bay.ToS - from) / maxPieceLength - 1e-9));
            for (int i = 0; i < count; i++)
            {
                double start = from + (bay.ToS - from) * i / count;
                double end = i == count - 1 ? bay.ToS : from + (bay.ToS - from) * (i + 1) / count;
                LayBayPiece(start, end, bay.Width, bay.Width, main, side, i == count - 1);
            }
        }

        private void LayBayPiece(double start, double end, float fromWidth, float toWidth, float main, float side, bool endGuard)
        {
            var profile = new RoadProfile(fromWidth * 0.5f, toWidth * 0.5f, side * (main + fromWidth * 0.5f), side * (main + toWidth * 0.5f),
                definition.RoadThickness, side < 0f, side > 0f, endGuard);
            var piece = new RoadPiece("Circuit road " + pieces.Count, parent, roadMaterial, railMaterial, lightMaterial);
            piece.Build(track, start, end, profile);
            pieces.Add(piece);
        }

        // User 2026-11-02: the highway keeps its own straight beyond both ramps, with transverse end guards.
        private void LayDeck(TrackDefinition.Bay bay)
        {
            Centerline line = bay.DeckLine(track);
            float main = definition.HalfWidthAt(bay.FromS);
            DeckJunctions(bay, out _, out _, out float join, out float leave);
            double joinStart = bay.Extension - join, exitEnd = bay.Extension + bay.ToS - bay.FromS + leave;
            double[] ends = { 0d, joinStart, bay.Extension, bay.Extension + bay.ToS - bay.FromS, exitEnd, line.EndS };
            float half = main + bay.Width * 0.5f, offset = -bay.Width * 0.5f;
            for (int k = 0; k + 1 < ends.Length; k++)
            {
                int count = Math.Max(1, (int)Math.Ceiling((ends[k + 1] - ends[k]) / maxPieceLength));
                for (int i = 0; i < count; i++)
                {
                    double start = ends[k] + (ends[k + 1] - ends[k]) * i / count;
                    double end = i == count - 1 ? ends[k + 1] : ends[k] + (ends[k + 1] - ends[k]) * (i + 1) / count;
                    var profile = new RoadProfile(half, half, offset, offset, definition.RoadThickness, true,
                        k != 1 && k != 3, k == ends.Length - 2 && i == count - 1, k == 0 && i == 0);
                    var piece = new RoadPiece("Circuit highway " + pieces.Count, parent, roadMaterial, railMaterial, lightMaterial);
                    piece.Build(line, start, end, profile);
                    pieces.Add(piece);
                }
            }
        }

        private void DeckJunctions(TrackDefinition.Bay bay, out float joinRamp, out float leaveRamp, out float joinDeck, out float leaveDeck)
        {
            float main = definition.HalfWidthAt(bay.FromS);
            float joinRadius = 1f / Mathf.Abs(track.FrameAt(bay.FromS - 0.01d).Curvature);
            float leaveRadius = 1f / Mathf.Abs(track.FrameAt(bay.ToS + 0.01d).Curvature);
            // Include guard thickness on both roads so neither guard's nose sticks into the shared surface.
            TrackDefinition.Bay.DeckJunction(joinRadius, main, out joinRamp, out joinDeck, RoadMeshBuilder.GuardThickness);
            TrackDefinition.Bay.DeckJunction(leaveRadius, main, out leaveRamp, out leaveDeck, RoadMeshBuilder.GuardThickness);
        }

        // Cut the ramp's left edge at the highway's right edge: one visible/collidable surface, no interior guard.
        private float DeckRampEdge(TrackDefinition.Bay bay, double s)
        {
            TrackFrame deck = track.FrameAt(bay.FromS), ramp = track.FrameAt(s);
            float main = definition.HalfWidthAt(bay.FromS);
            return (main - Vector3.Dot(ramp.Position - deck.Position, deck.Right)) / Vector3.Dot(ramp.Right, deck.Right);
        }

        private void LayDeckRamp(TrackDefinition.Bay bay, double from, double to)
        {
            float main = definition.HalfWidthAt(bay.FromS);
            for (double s = from; s < to - 1e-6; s += RoadMeshBuilder.SampleStep)
            {
                double end = Math.Min(to, s + RoadMeshBuilder.SampleStep);
                float a = Mathf.Max(-main, DeckRampEdge(bay, s)), b = Mathf.Max(-main, DeckRampEdge(bay, end));
                // The tangent tip is zero wide in plan; a millimetre sliver keeps the positive-width contract
                // representable at world coordinates too, rather than collapsing to a degenerate collider face.
                a = Mathf.Min(main - 0.001f, a);
                b = Mathf.Min(main - 0.001f, b);
                var profile = new RoadProfile((main - a) * 0.5f, (main - b) * 0.5f, (main + a) * 0.5f, (main + b) * 0.5f,
                    definition.RoadThickness, false, true, false);
                var piece = new RoadPiece("Circuit ramp " + pieces.Count, parent, roadMaterial, railMaterial, lightMaterial);
                piece.Build(track, s, end, profile);
                pieces.Add(piece);
            }
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

        // Lane markings painted on the main road from a track look (user 2026-10-30; 2026-11-02 "차선 디테일"): a solid
        // line inside each edge, the look's lane lines (TrackLook.LaneLines; dashed 3 m on and 5 m off, or solid), the
        // middle line (double, in the accent paint, where the look has one), take-off boards across the road before and
        // after every gap, and three white arrows before every corner of 30 degrees or more, pointing the way it turns.
        // Flat strips 2 cm above the road on the centerline's own frames, so they follow every bend and slope; none over
        // a gap. One mesh per paint per stretch of maxPieceLength; no colliders.
        public void PaintMarkings(TrackLook look)
        {
            if (look == null) throw new ArgumentNullException(nameof(look));
            if (look.Markings == null) throw new ArgumentException($"TrackLook '{look.name}' has no markings to paint.", nameof(look));
            Material accent = look.Accent != null ? look.Accent : look.Markings;
            List<(double Start, double End)> gaps = definition.Gaps();
            var white = new List<Vector3>(4096);
            var whiteTriangles = new List<int>(6144);
            var yellow = new List<Vector3>(1024);
            var yellowTriangles = new List<int>(1536);
            var lanes = new List<float>(8);
            double lap = track.EndS;
            int chunk = 0;
            for (double from = 0d; from < lap; from += maxPieceLength)
            {
                double to = Math.Min(lap, from + maxPieceLength);
                Vector3 origin = track.FrameAt(from).Position;
                white.Clear();
                whiteTriangles.Clear();
                yellow.Clear();
                yellowTriangles.Clear();
                for (double s = from; s < to - 1e-6; s += 1d)
                {
                    double next = Math.Min(to, s + 1d);
                    if (InGap(gaps, s) || InGap(gaps, next)) continue;
                    if (OnDeck(s)) continue;
                    float half = definition.HalfWidthAt(s);
                    float edge = half - TrackLook.EdgeLineInset;
                    BayAt(s, out float bayLeft, out float bayRight, out double bayEnd);
                    TrackLook.Centre centre = look.LaneLines(half, lanes);
                    bool painted = look.SolidLanes || s % 8d < 3d;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float bay = side < 0 ? bayLeft : bayRight;
                        // A bay under half a lane wide is still the main road's edge.
                        bool widened = bay >= look.LaneWidth * 0.5f;
                        float outer = widened ? half + bay - TrackLook.EdgeLineInset : edge;
                        Strip(s, next, side * outer - 0.125f, side * outer + 0.125f, origin, white, whiteTriangles);
                        if (!painted) continue;
                        // The lane grid runs on outward across the bay, as far as a lane fits; not in its hatched end.
                        float d = 0f;
                        foreach (float lane in lanes)
                        {
                            d = lane;
                            if (d <= outer - look.LaneWidth * 0.5f) Strip(s, next, side * d - 0.075f, side * d + 0.075f, origin, white, whiteTriangles);
                        }
                        if (!widened || s > bayEnd - BayHatch) continue;
                        float step = look.LaneWidth;
                        for (d = lanes.Count > 0 ? d + step : centre == TrackLook.Centre.None ? step * 0.5f : step; outer - d >= step * 0.5f; d += step)
                            Strip(s, next, side * d - 0.075f, side * d + 0.075f, origin, white, whiteTriangles);
                    }
                    if (centre == TrackLook.Centre.DoubleLine)
                    {
                        Strip(s, next, 0.08f, 0.2f, origin, yellow, yellowTriangles);
                        Strip(s, next, -0.2f, -0.08f, origin, yellow, yellowTriangles);
                    }
                    else if (centre == TrackLook.Centre.Line && painted) Strip(s, next, -0.075f, 0.075f, origin, white, whiteTriangles);
                }
                PaintTakeOffBoards(from, to, gaps, origin, yellow, yellowTriangles);
                PaintBayHatches(from, to, origin, white, whiteTriangles, yellow, yellowTriangles);
                PaintCornerArrows(from, to, gaps, origin, white, whiteTriangles);
                Paint("Road markings " + chunk, look.Markings, origin, white, whiteTriangles);
                Paint("Road accent " + chunk, accent, origin, yellow, yellowTriangles);
                chunk++;
            }
            foreach (TrackDefinition.Bay bay in definition.Bays)
                if (bay.StraightDeck) PaintDeck(look, bay);
        }

        private bool OnDeck(double s)
        {
            foreach (TrackDefinition.Bay bay in definition.Bays)
                if (bay.StraightDeck && s >= bay.FromS && s <= bay.ToS) return true;
            return false;
        }

        // The highway's lane grid belongs to its middle, 9 m left of the racing line, including the closed ends.
        private void PaintDeck(TrackLook look, TrackDefinition.Bay bay)
        {
            Centerline line = bay.DeckLine(track);
            float main = definition.HalfWidthAt(bay.FromS), offset = -bay.Width * 0.5f, half = main + bay.Width * 0.5f;
            var lanes = new List<float>();
            TrackLook.Centre centre = look.LaneLines(half, lanes);
            DeckJunctions(bay, out _, out _, out float join, out float leave);
            double exit = bay.Extension + bay.ToS - bay.FromS;
            var white = new List<Vector3>();
            var wt = new List<int>();
            var yellow = new List<Vector3>();
            var yt = new List<int>();
            Vector3 origin = line.FrameAt(0d).Position;
            for (double s = 0d; s < line.EndS - 1e-6; s += 1d)
            {
                double next = Math.Min(line.EndS, s + 1d);
                for (int side = -1; side <= 1; side += 2)
                {
                    float edge = offset + side * (half - TrackLook.EdgeLineInset);
                    // The edge paint opens with the guard at each ramp crossing.
                    if (side < 0 || s < bay.Extension - join || s >= bay.Extension && s < exit || s >= exit + leave)
                        DeckStrip(s, next, edge - 0.125f, edge + 0.125f, white, wt);
                    if (look.SolidLanes || s % 8d < 3d)
                        foreach (float lane in lanes)
                        {
                            float x = offset + side * lane;
                            if (x < GoreInner(s) - 0.1f) DeckStrip(s, next, x - 0.075f, x + 0.075f, white, wt);
                        }
                }
                if (centre == TrackLook.Centre.DoubleLine)
                {
                    DeckStrip(s, next, offset - 0.2f, offset - 0.08f, yellow, yt);
                    DeckStrip(s, next, offset + 0.08f, offset + 0.2f, yellow, yt);
                }
                else if (centre == TrackLook.Centre.Line && (look.SolidLanes || s % 8d < 3d))
                    DeckStrip(s, next, offset - 0.075f, offset + 0.075f, white, wt);
            }
            Gore(bay.Extension - join, bay.Extension);
            Gore(exit, exit + leave);
            Paint("Highway markings", look.Markings, origin, white, wt);
            Paint("Highway accent", look.Accent != null ? look.Accent : look.Markings, origin, yellow, yt);

            float GoreInner(double s)
            {
                if (s >= bay.Extension - join && s < bay.Extension) return main - 3.6f * (float)((bay.Extension - s) / join);
                if (s >= exit && s <= exit + leave) return main - 3.6f * (float)((s - exit) / leave);
                return float.PositiveInfinity;
            }

            void DeckStrip(double a, double b, float left, float right, List<Vector3> v, List<int> t)
            {
                TrackFrame f = line.FrameAt(a), g = line.FrameAt(b);
                Quad(f.TransformPoint(new Vector3(left, 0.025f, 0f)) - origin, g.TransformPoint(new Vector3(left, 0.025f, 0f)) - origin,
                    g.TransformPoint(new Vector3(right, 0.025f, 0f)) - origin, f.TransformPoint(new Vector3(right, 0.025f, 0f)) - origin, v, t);
            }

            void Gore(double a, double b)
            {
                for (double s = a; s < b - 2d; s += 2d)
                {
                    double end = Math.Min(b, s + 1.7d);
                    float inner = GoreInner(s), outer = main - 0.15f;
                    if (inner >= outer) continue;
                    TrackFrame f = line.FrameAt(s), g = line.FrameAt(end);
                    Quad(f.TransformPoint(new Vector3(inner, 0.03f, 0f)) - origin,
                        line.FrameAt(s + 0.5d).TransformPoint(new Vector3(inner, 0.03f, 0f)) - origin,
                        g.TransformPoint(new Vector3(outer, 0.03f, 0f)) - origin,
                        line.FrameAt(end - 0.5d).TransformPoint(new Vector3(outer, 0.03f, 0f)) - origin, yellow, yt);
                }
            }
        }

        // One mesh of the painted strips, unless there are none.
        private void Paint(string name, Material material, Vector3 origin, List<Vector3> vertices, List<int> triangles)
        {
            if (vertices.Count == 0) return;
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            markingMeshes.Add(mesh);
            var painted = new GameObject(name);
            painted.transform.SetParent(parent, false);
            painted.transform.position = origin;
            painted.AddComponent<MeshFilter>().sharedMesh = mesh;
            painted.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        // How far before its end guard a bay's lanes stop and its hatching starts.
        private const double BayHatch = 15d;

        // The bay beside the main road at s on each side (its width, 0 for none) and where the bay there ends.
        private void BayAt(double s, out float left, out float right, out double end)
        {
            left = right = 0f;
            end = double.MaxValue;
            foreach (TrackDefinition.Bay bay in definition.Bays)
            {
                if (bay.StraightDeck) continue;
                float width = bay.WidthAt(s);
                if (!(width > 0f)) continue;
                if (bay.Left) left = width;
                else right = width;
                end = bay.ToS;
            }
        }

        // The last BayHatch metres of each bay: a solid line along the main road's edge and slanting stripes across the
        // bay up to its end guard, in the accent paint, so the dead end shows before it is reached.
        private void PaintBayHatches(double from, double to, Vector3 origin, List<Vector3> white, List<int> whiteTriangles, List<Vector3> accent, List<int> accentTriangles)
        {
            foreach (TrackDefinition.Bay bay in definition.Bays)
            {
                if (bay.StraightDeck) continue;
                float side = bay.Left ? -1f : 1f;
                float main = definition.HalfWidthAt(bay.FromS);
                double start = bay.ToS - BayHatch;
                for (double s = Math.Max(from, start); s < Math.Min(to, bay.ToS - RoadMeshBuilder.GuardThickness) - 1e-6; s += 1d)
                {
                    double next = Math.Min(Math.Min(to, bay.ToS - RoadMeshBuilder.GuardThickness), s + 1d);
                    Strip(s, next, side < 0f ? -main - 0.1f : main - 0.1f, side < 0f ? -main + 0.1f : main + 0.1f, origin, white, whiteTriangles);
                }
                // A stripe every 2 m, slanting 3 m forward from the main road's edge to the bay's outer edge.
                for (double s = start; s < bay.ToS - RoadMeshBuilder.GuardThickness - 0.5d; s += 2d)
                {
                    if (s < from || s >= to) continue;
                    double inner0 = s, inner1 = s + 0.7d;
                    double outer0 = Math.Min(s + 3d, bay.ToS - RoadMeshBuilder.GuardThickness), outer1 = Math.Min(s + 3.7d, bay.ToS - RoadMeshBuilder.GuardThickness);
                    float x0 = side * (main + 0.3f), x1 = side * (main + bay.Width - 0.6f);
                    Quad(track.FrameAt(inner0).TransformPoint(new Vector3(x0, 0.02f, 0f)) - origin, track.FrameAt(inner1).TransformPoint(new Vector3(x0, 0.02f, 0f)) - origin,
                        track.FrameAt(outer1).TransformPoint(new Vector3(x1, 0.02f, 0f)) - origin, track.FrameAt(outer0).TransformPoint(new Vector3(x1, 0.02f, 0f)) - origin,
                        accent, accentTriangles);
                }
            }
        }

        // A take-off board 1.2 m deep across the road at each side of every gap, striped across in 0.8 m blocks, so the
        // edge of the drop shows from afar (user 2026-11-02, "떨어지는 구간 식별이 어렵다").
        private void PaintTakeOffBoards(double from, double to, List<(double Start, double End)> gaps, Vector3 origin, List<Vector3> vertices, List<int> triangles)
        {
            foreach ((double start, double end) in gaps)
            {
                Board(start - 1.4d, start - 0.2d);
                Board(end + 0.2d, end + 1.4d);
            }

            void Board(double a, double b)
            {
                if (a < from || a >= to || a < 0d || b > track.EndS) return;
                float half = definition.HalfWidthAt(a);
                for (float x = -half; x < half - 0.4f; x += 1.6f)
                    Strip(a, b, x, Mathf.Min(half, x + 0.8f), origin, vertices, triangles);
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
            if (OnDeck((fromS + toS) * 0.5d)) return;
            float aLeft = left, bLeft = left;
            foreach (TrackDefinition.Bay bay in definition.Bays)
            {
                if (!bay.StraightDeck) continue;
                DeckJunctions(bay, out float join, out float leave, out _, out _);
                if ((fromS + toS) * 0.5d <= bay.FromS - join || (fromS + toS) * 0.5d >= bay.ToS + leave) continue;
                aLeft = Mathf.Max(left, DeckRampEdge(bay, fromS));
                bLeft = Mathf.Max(left, DeckRampEdge(bay, toS));
                if (aLeft >= right && bLeft >= right) return;
                aLeft = Mathf.Min(aLeft, right);
                bLeft = Mathf.Min(bLeft, right);
            }
            Quad(a.TransformPoint(new Vector3(aLeft, 0.02f, 0f)) - origin, b.TransformPoint(new Vector3(bLeft, 0.02f, 0f)) - origin,
                b.TransformPoint(new Vector3(right, 0.02f, 0f)) - origin, a.TransformPoint(new Vector3(right, 0.02f, 0f)) - origin, vertices, triangles);
        }

        // Corners in order round the quad, wound to face up whichever way they go round. One winding only: both windings
        // on the same vertices averaged the normals to nothing and the paint drew black (found 2026-11-02).
        private static void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, List<Vector3> vertices, List<int> triangles)
        {
            int first = vertices.Count;
            vertices.Add(p0);
            vertices.Add(p1);
            vertices.Add(p2);
            vertices.Add(p3);
            // Unity's front face winds clockwise seen from the side it faces, and its normal is (b - a) x (c - a).
            bool up = Vector3.Cross(p1 - p0, p2 - p0).y > 0f;
            if (up)
            {
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
            }
            else
            {
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
                triangles.Add(first); triangles.Add(first + 3); triangles.Add(first + 2);
            }
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
        // touches is cut at the gap's ends, the part inside without its guard on the gap's side.
        private void LayRange(double from, double to, float fromWidth, float toWidth)
        {
            int count = Math.Max(1, (int)Math.Ceiling((to - from) / maxPieceLength - 1e-9));
            for (int i = 0; i < count; i++)
            {
                double start = from + (to - from) * i / count;
                double end = i == count - 1 ? to : from + (to - from) * (i + 1) / count;
                cuts.Clear();
                cuts.Add(start);
                foreach ((double gapFrom, double gapTo, _) in guardGaps)
                {
                    if (gapFrom > start && gapFrom < end) cuts.Add(gapFrom);
                    if (gapTo > start && gapTo < end) cuts.Add(gapTo);
                }
                foreach (TrackDefinition.Bay bay in definition.Bays)
                {
                    if (!bay.StraightDeck) continue;
                    DeckJunctions(bay, out float join, out float leave, out _, out _);
                    double[] edges = { bay.FromS - join, bay.FromS, bay.ToS, bay.ToS + leave };
                    foreach (double edge in edges) if (edge > start && edge < end) cuts.Add(edge);
                }
                cuts.Add(end);
                cuts.Sort();
                for (int c = 0; c + 1 < cuts.Count; c++)
                {
                    if (!(cuts[c + 1] > cuts[c])) continue;
                    double middle = (cuts[c] + cuts[c + 1]) * 0.5d;
                    bool replaced = false;
                    foreach (TrackDefinition.Bay bay in definition.Bays)
                    {
                        if (!bay.StraightDeck) continue;
                        DeckJunctions(bay, out float join, out float leave, out _, out _);
                        if (middle <= bay.FromS - join || middle >= bay.ToS + leave) continue;
                        if (middle < bay.FromS || middle > bay.ToS) LayDeckRamp(bay, cuts[c], cuts[c + 1]);
                        replaced = true;
                        break;
                    }
                    if (replaced) continue;
                    LayPiece(track, cuts[c], cuts[c + 1], from, to, fromWidth, toWidth, !GuardOpen(middle, true), !GuardOpen(middle, false));
                }
            }
        }

        private bool GuardOpen(double s, bool left)
        {
            foreach ((double from, double to, bool onLeft) in guardGaps)
                if (onLeft == left && s > from && s < to) return true;
            return false;
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
