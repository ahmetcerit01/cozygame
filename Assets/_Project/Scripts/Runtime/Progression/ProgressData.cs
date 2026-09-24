using System;
using System.Collections.Generic;

namespace CozyLab.Puzzle.Progression
{
    /// <summary>
    /// Serialized save payload. Keep it flat and versioned; add fields with safe defaults and bump
    /// <see cref="CurrentVersion"/> only when old data needs migrating.
    /// </summary>
    [Serializable]
    public sealed class ProgressData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        /// <summary>Highest level index (0-based) the player may start.</summary>
        public int highestUnlockedIndex;
        /// <summary>Level ids (not indices) so reordering the catalog never corrupts a save.</summary>
        public List<string> completedLevelIds = new List<string>();

        public static ProgressData CreateFresh() => new ProgressData();
    }
}
