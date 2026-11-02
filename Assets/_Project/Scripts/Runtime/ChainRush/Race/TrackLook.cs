using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoHarness.ChainRush.Race
{
    // One track's own road and wall materials (user 2026-10-30, Palm Coast "트랙 서킷의 바닥이나 벽도 디자인"): the lobby's
    // track choice carries it to the race (RaceSetup.Look) and CircuitRace lays the road with it instead of the circuit
    // scene's materials. Presentation only: the simulation never reads it. A track without one keeps the scene's.
    // Its lanes (user 2026-11-02, "실제 도로 같은 디자인 … 차선 디테일"): lane lines stand a fixed distance apart from the
    // middle, so where the road narrows its edge sweeps over the outer lines and they end, as a real lane drop does, and
    // where it widens new lines appear once a lane fits.
    [CreateAssetMenu(menuName = "ProtoHarness/ChainRush/Track Look")]
    public sealed class TrackLook : ScriptableObject
    {
        // What the middle of the road shows (LaneLines).
        public enum Centre { None, Line, DoubleLine }

        // The edge lines' middle stands this far inside the road's edge; lanes are counted from there.
        public const float EdgeLineInset = 0.525f;

        [Tooltip("The road surface (CircuitScenery's road pieces).")]
        [SerializeField] private Material road;
        [Tooltip("The guard walls on both edges of the road.")]
        [SerializeField] private Material rail;
        [Tooltip("Lane markings painted on the road (edge and lane lines, arrows before corners); empty paints none.")]
        [SerializeField] private Material markings;
        [Tooltip("The second paint (yellow): the take-off boards before a gap and the double middle line; empty paints those with the markings.")]
        [SerializeField] private Material accent;
        [Tooltip("The middle line is a solid double line in the accent paint (a two-way road's); otherwise a single lane line.")]
        [SerializeField] private bool doubleCentre;
        [Tooltip("Metres between lane lines.")]
        [SerializeField] private float laneWidth = 3.5f;
        [Tooltip("From this half width up the road has a middle line and lanes either side of it; narrower, the lanes are spread round the middle with no middle line.")]
        [SerializeField] private float centreFromHalfWidth;
        [Tooltip("Solid lane lines (a running track's); otherwise dashed, 3 m on and 5 m off.")]
        [SerializeField] private bool solidLanes;

        public Material Road => road;
        public Material Rail => rail;
        public Material Markings => markings;
        public Material Accent => accent;
        public float LaneWidth => laneWidth;
        public bool SolidLanes => solidLanes;

        // The lane lines across a road of halfWidth, as distances right of the middle (each stands mirrored on the left
        // too), nearest first, into offsets; returns what the middle shows. A line stays only while the lane outside it,
        // up to the edge line, is at least half a lane wide.
        public Centre LaneLines(float halfWidth, List<float> offsets)
        {
            if (offsets == null) throw new ArgumentNullException(nameof(offsets));
            if (!(halfWidth > EdgeLineInset)) throw new ArgumentOutOfRangeException(nameof(halfWidth), halfWidth, "The road must be wider than its edge lines.");
            offsets.Clear();
            float usable = halfWidth - EdgeLineInset;
            bool centred = halfWidth >= centreFromHalfWidth;
            float first = centred ? laneWidth : laneWidth * 0.5f;
            for (float d = first; usable - d >= laneWidth * 0.5f; d += laneWidth) offsets.Add(d);
            if (!centred) return Centre.None;
            return doubleCentre ? Centre.DoubleLine : Centre.Line;
        }

        // Whether both materials are set and the lanes make sense; CircuitRace.UseLook refuses a look that is not.
        public bool TryValidate(out string error)
        {
            if (road == null || rail == null) error = $"TrackLook '{name}': a road and a rail material are required.";
            else if (!(laneWidth >= 1f)) error = $"TrackLook '{name}': lanes must be at least 1 m wide, not {laneWidth}.";
            else if (!(centreFromHalfWidth >= 0f)) error = $"TrackLook '{name}': the middle line's half width must not be negative, not {centreFromHalfWidth}.";
            else error = null;
            return error == null;
        }

        private void OnValidate()
        {
            if (!TryValidate(out string error)) Debug.LogError(error, this);
        }
    }
}
