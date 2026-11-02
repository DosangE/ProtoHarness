using System;
using System.Collections.Generic;
using ProtoHarness.ChainRush.Track;
using UnityEngine;

namespace ProtoHarness.Editor.ChainRush
{
    // The road an exterior keeps clear of: points every step metres along the main road, each shortcut and each bay, with the
    // half width there. Placing asks Clear (outside the road by keepOut plus the thing's radius) or Available (and apart
    // from what is already placed, Reserve); after building, ValidateClearance checks every renderer's world bounds, and
    // what stands under the road on purpose (a viaduct's piers) against the slab above it.
    public sealed class RoadCorridor
    {
        // What spans the road this high above it is over the runner and the camera (start arches, bridges, the tower).
        private const float Overhead = 9f;
        // The nearest a renderer's bounds may come to the road's edge.
        private const float EdgeMargin = 0.7f;
        private readonly float keepOut;
        private readonly float thickness;
        // Across the road under-road parts are checked this often (metres).
        private const float CrossStep = 0.5f;
        private readonly List<Vector3> samples = new List<Vector3>();
        private readonly List<Vector3> rights = new List<Vector3>();
        private readonly List<float> widths = new List<float>();
        private readonly List<(Vector3 Position, float Radius)> occupied = new List<(Vector3, float)>();

        // line is definition's closed centerline (TrackDefinition.BuildCenterline).
        public RoadCorridor(TrackDefinition definition, Centerline line, double step, float keepOut)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (!(step > 0d)) throw new ArgumentOutOfRangeException(nameof(step), step, "The sample step must be positive.");
            if (!(keepOut >= 0f)) throw new ArgumentOutOfRangeException(nameof(keepOut), keepOut, "The keep-out must not be negative.");
            this.keepOut = keepOut;
            thickness = definition.RoadThickness;
            for (double s = 0d; s <= definition.LapLength; s += step)
            {
                TrackFrame frame = line.FrameAt(s);
                samples.Add(frame.Position);
                rights.Add(frame.Right);
                widths.Add(definition.HalfWidthAt(s));
            }
            foreach (TrackDefinition.Shortcut shortcut in definition.Shortcuts)
            {
                Centerline branch = TrackDefinition.ShortcutLine(line, shortcut);
                for (double s = 0d; s <= shortcut.Length; s += step)
                {
                    TrackFrame frame = branch.FrameAt(s);
                    samples.Add(frame.Position);
                    rights.Add(frame.Right);
                    widths.Add(shortcut.HalfWidth);
                }
            }
            // A bay is road too: its middle beside the main road's edge, half its width either side.
            foreach (TrackDefinition.Bay bay in definition.Bays)
            {
                if (bay.StraightDeck)
                {
                    Centerline deck = bay.DeckLine(line);
                    float half = definition.HalfWidthAt(bay.FromS) + bay.Width * 0.5f;
                    for (double s = 0d; s <= deck.EndS; s += step)
                    {
                        TrackFrame frame = deck.FrameAt(s);
                        samples.Add(frame.Position - frame.Right * bay.Width * 0.5f);
                        rights.Add(frame.Right);
                        widths.Add(half);
                    }
                    continue;
                }
                float side = bay.Left ? -1f : 1f, main = definition.HalfWidthAt(bay.FromS);
                for (double s = bay.FromS; s <= bay.ToS; s += step)
                {
                    float width = bay.WidthAt(s);
                    TrackFrame frame = line.FrameAt(s);
                    samples.Add(frame.Position + frame.Right * side * (main + width * 0.5f));
                    rights.Add(frame.Right);
                    widths.Add(width * 0.5f);
                }
            }
        }

        // Whether a thing of radius at position (only its x and z) stays keepOut outside every road's edge.
        public bool Clear(Vector3 position, float radius)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                float limit = widths[i] + keepOut + radius;
                float dx = position.x - samples[i].x, dz = position.z - samples[i].z;
                if (dx * dx + dz * dz < limit * limit) return false;
            }
            return true;
        }

        // Clear, and apart from everything reserved so far.
        public bool Available(Vector3 position, float radius)
        {
            if (!Clear(position, radius)) return false;
            foreach (var other in occupied)
            {
                float limit = radius + other.Radius;
                float dx = position.x - other.Position.x, dz = position.z - other.Position.z;
                if (dx * dx + dz * dz < limit * limit) return false;
            }
            return true;
        }

        public void Reserve(Vector3 position, float radius) => occupied.Add((position, radius));

        // Every renderer's world bounds stay EdgeMargin outside the road's edge, except what lies at or below floor (the
        // land), under the road's slab, or Overhead and more above the road. The world-aligned bounds of a thing turned
        // on a diagonal stretch swell toward the road, so such things stand further out (DECISIONS 2026-10-30).
        // underRoad (may be null) holds what stands under the road on purpose: on a sloping diagonal stretch its swollen
        // bounds reach road points lower than its own (user 2026-11-02, SKYWAY's piers), so instead its every part must
        // stay below the slab wherever the road passes over its own footprint (ValidateUnderRoad).
        // byVertex (may be null) holds meshes that surround the road (the stadium's baked complex), whose bounds always
        // hold it: they are checked vertex by vertex instead (ValidateByVertex).
        public void ValidateClearance(GameObject root, float floor, Transform underRoad = null, Transform byVertex = null)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (underRoad != null && !underRoad.IsChildOf(root.transform))
                throw new ArgumentException($"{underRoad.name} is not part of {root.name}.", nameof(underRoad));
            if (byVertex != null && !byVertex.IsChildOf(root.transform))
                throw new ArgumentException($"{byVertex.name} is not part of {root.name}.", nameof(byVertex));
            float nearest = float.PositiveInfinity;
            var intrusions = new List<string>();
            int under = underRoad == null ? 0 : ValidateUnderRoad(underRoad, intrusions);
            int vertices = byVertex == null ? 0 : ValidateByVertex(byVertex, floor, intrusions);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (underRoad != null && renderer.transform.IsChildOf(underRoad)) continue;
                if (byVertex != null && renderer.transform.IsChildOf(byVertex)) continue;
                Bounds bounds = renderer.bounds;
                if (bounds.max.y <= floor) continue;
                for (int i = 0; i < samples.Count; i++)
                {
                    if (bounds.min.y >= samples[i].y + Overhead || bounds.max.y <= samples[i].y - thickness) continue;
                    float dx = Mathf.Max(bounds.min.x - samples[i].x, 0f, samples[i].x - bounds.max.x);
                    float dz = Mathf.Max(bounds.min.z - samples[i].z, 0f, samples[i].z - bounds.max.z);
                    float margin = Mathf.Sqrt(dx * dx + dz * dz) - widths[i];
                    nearest = Mathf.Min(nearest, margin);
                    if (margin >= EdgeMargin) continue;
                    intrusions.Add($"{renderer.transform.parent.name}/{renderer.name}: margin {margin:F2} m at {samples[i]}");
                    break;
                }
            }
            if (intrusions.Count != 0) throw new InvalidOperationException($"{root.name} intrudes into the road corridor:\n" + string.Join("\n", intrusions));
            string bounded = float.IsInfinity(nearest) ? "no renderer checked by its bounds" : $"nearest renderer bounds {nearest:F2} m outside a road edge";
            Debug.Log($"{root.name} clearance: {bounded} (at least {EdgeMargin} m below {Overhead} m over the road); {under} parts under the slab; {vertices} vertices checked one by one.");
        }

        // No vertex of the group's meshes stands above the road's surface (by more than SurfaceGap) and below Overhead
        // within EdgeMargin of a road's edge; what lies at or below floor is land. A face whose corners all lie outside
        // the corridor but which spans it is not caught. Returns how many vertices it checked.
        private int ValidateByVertex(Transform group, float floor, List<string> intrusions)
        {
            const float SurfaceGap = 0.05f;
            const float Cell = 16f;
            // The samples in a grid of Cell metres, so each vertex looks only at the road points near it.
            var grid = new Dictionary<(int, int), List<int>>();
            float reach = 0f;
            for (int i = 0; i < samples.Count; i++)
            {
                var key = ((int)Mathf.Floor(samples[i].x / Cell), (int)Mathf.Floor(samples[i].z / Cell));
                if (!grid.TryGetValue(key, out List<int> cell)) grid[key] = cell = new List<int>();
                cell.Add(i);
                reach = Mathf.Max(reach, widths[i] + EdgeMargin);
            }
            int cells = Mathf.CeilToInt(reach / Cell);
            int count = 0;
            var points = new List<Vector3>();
            foreach (MeshFilter filter in group.GetComponentsInChildren<MeshFilter>(true))
            {
                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                filter.sharedMesh.GetVertices(points);
                int caught = 0;
                Vector3 first = default;
                foreach (Vector3 local in points)
                {
                    count++;
                    Vector3 p = toWorld.MultiplyPoint3x4(local);
                    if (p.y <= floor) continue;
                    int cx = (int)Mathf.Floor(p.x / Cell), cz = (int)Mathf.Floor(p.z / Cell);
                    bool inside = false;
                    for (int gx = cx - cells; gx <= cx + cells && !inside; gx++)
                        for (int gz = cz - cells; gz <= cz + cells && !inside; gz++)
                        {
                            if (!grid.TryGetValue((gx, gz), out List<int> cell)) continue;
                            foreach (int i in cell)
                            {
                                if (p.y <= samples[i].y + SurfaceGap || p.y >= samples[i].y + Overhead) continue;
                                float dx = p.x - samples[i].x, dz = p.z - samples[i].z;
                                float limit = widths[i] + EdgeMargin;
                                if (dx * dx + dz * dz >= limit * limit) continue;
                                inside = true;
                                break;
                            }
                        }
                    if (!inside) continue;
                    if (caught++ == 0) first = p;
                }
                if (caught != 0) intrusions.Add($"{filter.transform.parent.name}/{filter.name}: {caught} vertices within {EdgeMargin} m of a road edge, first at {first}");
            }
            return count;
        }

        // Each part's top stays at or below the slab's underside (the road's height less its thickness) at every point
        // across the road that falls inside the part's own turned footprint. Returns how many parts it checked.
        private int ValidateUnderRoad(Transform group, List<string> intrusions)
        {
            int count = 0;
            foreach (MeshFilter filter in group.GetComponentsInChildren<MeshFilter>(true))
            {
                count++;
                Transform part = filter.transform;
                Bounds local = filter.sharedMesh.bounds;
                Bounds world = filter.GetComponent<Renderer>().bounds;
                float top = world.max.y;
                for (int i = 0; i < samples.Count; i++)
                {
                    // Only the road points near the part's world bounds can be over its footprint.
                    float dx = Mathf.Max(world.min.x - samples[i].x, 0f, samples[i].x - world.max.x);
                    float dz = Mathf.Max(world.min.z - samples[i].z, 0f, samples[i].z - world.max.z);
                    if (dx * dx + dz * dz > widths[i] * widths[i]) continue;
                    float overlap = float.NegativeInfinity;
                    for (float x = -widths[i]; x <= widths[i]; x += CrossStep)
                    {
                        Vector3 point = samples[i] + rights[i] * x;
                        Vector3 inside = part.InverseTransformPoint(point);
                        if (inside.x < local.min.x || inside.x > local.max.x || inside.z < local.min.z || inside.z > local.max.z) continue;
                        overlap = Mathf.Max(overlap, top - (point.y - thickness));
                    }
                    if (overlap <= 1e-3f) continue;
                    intrusions.Add($"{part.parent.name}/{part.name}: {overlap:F2} m up into the slab at {samples[i]}");
                    break;
                }
            }
            return count;
        }
    }
}
