using System.Collections.Generic;
using CozyLab.Puzzle.Progression;

namespace CozyLab.Puzzle.Meta
{
    /// <summary>Upgrades older save payloads to <see cref="ProgressData.CurrentVersion"/>.</summary>
    public static class SaveMigration
    {
        /// <summary>
        /// v1 → v2: saves made before Research existed receive the Research they would have earned —
        /// +20 per already-completed level and +100 per already-completed experiment — each marked claimed
        /// so it can never be granted again. Puzzle progression is left untouched.
        /// </summary>
        public static void MigrateToCurrent(ProgressData data, IReadOnlyList<ExperimentInfo> experiments)
        {
            if (data.version < 2)
            {
                data.Normalize();
                foreach (var experiment in experiments)
                {
                    if (experiment.LevelIds == null) continue;
                    // Claim is idempotent per level and grants the experiment bonus once every level is complete.
                    foreach (var levelId in experiment.LevelIds)
                    {
                        if (data.completedLevelIds.Contains(levelId)) ResearchRules.Claim(data, experiment, levelId);
                    }
                }
                data.version = 2;
            }
        }
    }
}
