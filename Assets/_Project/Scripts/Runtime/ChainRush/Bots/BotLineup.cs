using System;
using System.Collections.Generic;

namespace ProtoHarness.ChainRush.Bots
{
    // A seeded draw without replacement. Does not change the lobby's profile list or Unity's random stream.
    public static class BotLineup
    {
        public static RunnerProfile[] Pick(IReadOnlyList<RunnerProfile> profiles, RunnerProfile player, int seed, int count = 3)
        {
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            var pool = new List<RunnerProfile>(profiles.Count);
            bool hasPlayer = false;
            for (int i = 0; i < profiles.Count; i++)
            {
                RunnerProfile profile = profiles[i];
                if (profile == null) throw new ArgumentException($"Profile {i} is missing.", nameof(profiles));
                for (int k = 0; k < i; k++)
                    if (profiles[k] == profile) throw new ArgumentException($"Profile {i} is repeated.", nameof(profiles));
                if (profile == player) hasPlayer = true;
                else pool.Add(profile);
            }
            if (!hasPlayer) throw new ArgumentException("The player's profile must be in the lineup pool.", nameof(player));
            if (pool.Count < count) throw new ArgumentException("Not enough other profiles for this lineup.", nameof(profiles));
            var random = new Random(seed);
            var chosen = new RunnerProfile[count];
            for (int i = 0; i < count; i++)
            {
                int pick = random.Next(i, pool.Count);
                RunnerProfile swap = pool[i];
                pool[i] = pool[pick];
                pool[pick] = swap;
                chosen[i] = pool[i];
            }
            return chosen;
        }
    }
}
