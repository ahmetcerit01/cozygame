using System;

namespace CozyLab.Puzzle.Meta
{
    /// <summary>
    /// A Lab object that can be upgraded with Research. Upgrades are purely visual/meta progression:
    /// they never change puzzle rules, hints, undo, rewards or unlocks.
    /// </summary>
    public sealed class EquipmentDefinition
    {
        private readonly int[] _upgradeCosts;

        public string Id { get; }
        public string DisplayName { get; }
        public int MaxLevel => _upgradeCosts.Length + 1;

        /// <param name="upgradeCosts">Cost of level 1→2, 2→3, ...</param>
        public EquipmentDefinition(string id, string displayName, params int[] upgradeCosts)
        {
            Id = id;
            DisplayName = displayName;
            _upgradeCosts = upgradeCosts ?? Array.Empty<int>();
        }

        /// <summary>Cost to go from <paramref name="currentLevel"/> to the next level, or -1 at max level.</summary>
        public int GetUpgradeCost(int currentLevel) =>
            currentLevel >= 1 && currentLevel < MaxLevel ? _upgradeCosts[currentLevel - 1] : -1;
    }

    public enum UpgradeResult
    {
        Upgraded,
        InsufficientResearch,
        MaxLevel,
    }

    /// <summary>The Lab's authored equipment and its (tiny) rule set.</summary>
    public static class LabEquipment
    {
        public static readonly EquipmentDefinition Microscope = new EquipmentDefinition("microscope", "Microscope", 100, 200);

        public const string IncubatorId = "incubator";
        public const string AnalyzerId = "analyzer";

        /// <summary>Lab Level is derived from the Microscope for now (no XP, no extra currency).</summary>
        public static int LabLevelFromMicroscope(int microscopeLevel) =>
            Math.Max(1, Math.Min(microscopeLevel, Microscope.MaxLevel));

        /// <summary>The Incubator wakes up once the first experiment is complete (a puzzle milestone, not a purchase).</summary>
        public static bool IsIncubatorUnlocked(bool firstExperimentComplete) => firstExperimentComplete;

        public const int AnalyzerLabLevel = 3;

        public static bool IsAnalyzerUnlocked(int labLevel) => labLevel >= AnalyzerLabLevel;
    }
}
