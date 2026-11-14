using System;
using ProtoHarness.ChainRush.Track;
using UnityEngine;

namespace ProtoHarness.ChainRush.Race
{
    public static class StartGrid
    {
        public const float BodyRadius = 0.38f;
        public const float WallClearance = 0.6f;
        public const float MaxSpacing = 2.4f;
        public const float MinSpacing = 1.2f;

        // x = metres behind S zero; y = metres right of the centreline. Always four columns per row.
        public static Vector2 Slot(TrackDefinition definition, int count, int slot)
        {
            CheckCount(count);
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (slot < 0 || slot >= count) throw new ArgumentOutOfRangeException(nameof(slot));
            float width = MinimumHalfWidth(definition);
            float spacing = Mathf.Min(MaxSpacing, (width - BodyRadius - WallClearance) * 2f / 3f);
            if (!(spacing >= MinSpacing)) throw new ArgumentException("StartGrid: the start section cannot fit four runners at least 1.2 m apart.", nameof(definition));
            return new Vector2(slot / 4 * 4f, (slot % 4 - 1.5f) * spacing);
        }

        // Width is piecewise linear. Endpoints and every segment boundary in the window give its exact minimum.
        public static float MinimumHalfWidth(TrackDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            double lap = definition.LapLength;
            if (!(lap > 0d)) throw new ArgumentException("StartGrid: the track needs a positive length.", nameof(definition));
            float minimum = Mathf.Min(definition.HalfWidthAt(-12d), definition.HalfWidthAt(4d));
            double boundary = 0d;
            for (int i = 0; i <= definition.Segments.Count; i++)
            {
                if (definition.IsOpenCourse)
                {
                    if (boundary >= -12d && boundary <= 4d) minimum = Mathf.Min(minimum, definition.HalfWidthAt(boundary));
                }
                else
                {
                    long first = (long)Math.Ceiling((-12d - boundary) / lap);
                    long last = (long)Math.Floor((4d - boundary) / lap);
                    for (long turn = first; turn <= last; turn++)
                        minimum = Mathf.Min(minimum, definition.HalfWidthAt(boundary + turn * lap));
                }
                if (i < definition.Segments.Count) boundary += definition.Segments[i].Length;
            }
            return minimum;
        }

        // Includes racer zero (the player), without changing Unity's random stream.
        public static int[] Assign(int count, int seed)
        {
            CheckCount(count);
            var slots = new int[count];
            for (int i = 0; i < count; i++) slots[i] = i;
            var random = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                int pick = random.Next(i, count);
                int swap = slots[i];
                slots[i] = slots[pick];
                slots[pick] = swap;
            }
            return slots;
        }

        private static void CheckCount(int count)
        {
            if (count < 1 || count > 8) throw new ArgumentOutOfRangeException(nameof(count), "StartGrid: a race needs 1 to 8 runners.");
        }
    }
}
