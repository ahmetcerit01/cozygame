using System;
using System.Collections.Generic;

namespace CozyLab.Puzzle.Progression
{
    /// <summary>
    /// Serialized save payload (one file for the whole game). Keep it flat and versioned; add fields with safe
    /// defaults and bump <see cref="CurrentVersion"/> when old data needs migrating.
    ///
    /// Version history:
    ///   1 — puzzle progression (unlock frontier + completed level ids).
    ///   2 — adds optional Lab meta progression: Research, claimed rewards, equipment, seen Lab unlocks.
    /// </summary>
    [Serializable]
    public sealed class ProgressData
    {
        public const int CurrentVersion = 2;

        /// <summary>0 = unknown/legacy (no version written). Fresh saves use <see cref="CurrentVersion"/>.</summary>
        public int version;

        // ---- v1: puzzle progression
        /// <summary>Highest level index (0-based) the player may start.</summary>
        public int highestUnlockedIndex;
        /// <summary>Level ids (not indices) so reordering the catalog never corrupts a save.</summary>
        public List<string> completedLevelIds = new List<string>();

        // ---- v2: optional meta progression (never gates puzzles)
        public int research;
        /// <summary>Level ids whose first-clear Research reward was granted.</summary>
        public List<string> claimedLevelRewards = new List<string>();
        /// <summary>Experiment (chapter) ids whose completion Research reward was granted.</summary>
        public List<string> claimedExperimentRewards = new List<string>();
        public List<EquipmentSave> equipment = new List<EquipmentSave>();
        /// <summary>Lab objects whose unlock reveal the player has already seen.</summary>
        public List<string> seenLabUnlocks = new List<string>();

        public static ProgressData CreateFresh() => new ProgressData { version = CurrentVersion };

        /// <summary>Replaces nulls left by older/partial JSON with empty lists.</summary>
        public void Normalize()
        {
            if (completedLevelIds == null) completedLevelIds = new List<string>();
            if (claimedLevelRewards == null) claimedLevelRewards = new List<string>();
            if (claimedExperimentRewards == null) claimedExperimentRewards = new List<string>();
            if (equipment == null) equipment = new List<EquipmentSave>();
            if (seenLabUnlocks == null) seenLabUnlocks = new List<string>();
            if (research < 0) research = 0;
        }
    }

    [Serializable]
    public sealed class EquipmentSave
    {
        public string id;
        public int level;
    }
}
