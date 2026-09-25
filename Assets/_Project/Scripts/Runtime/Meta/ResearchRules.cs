using System.Collections.Generic;
using CozyLab.Puzzle.Progression;

namespace CozyLab.Puzzle.Meta
{
    /// <summary>An experiment (chapter) as the meta layer sees it: an id and its level ids.</summary>
    public readonly struct ExperimentInfo
    {
        public readonly string Id;
        public readonly IReadOnlyList<string> LevelIds;

        public ExperimentInfo(string id, IReadOnlyList<string> levelIds)
        {
            Id = id;
            LevelIds = levelIds;
        }
    }

    /// <summary>Research granted by one completion.</summary>
    public readonly struct ResearchAward
    {
        public readonly int LevelReward;
        public readonly int ExperimentReward;
        public int Total => LevelReward + ExperimentReward;

        public ResearchAward(int levelReward, int experimentReward)
        {
            LevelReward = levelReward;
            ExperimentReward = experimentReward;
        }
    }

    /// <summary>
    /// Research reward rules. Every reward is granted at most once and is recorded as "claimed" in the save,
    /// so replays award nothing. Research never affects puzzles.
    /// </summary>
    public static class ResearchRules
    {
        public const int LevelFirstClear = 20;
        public const int ExperimentFirstCompletion = 100;

        /// <summary>
        /// Grants the rewards earned by <paramref name="levelId"/> being completed (and, if every level of the
        /// experiment is complete, the experiment reward). Mutates <paramref name="data"/>; does not save.
        /// </summary>
        public static ResearchAward Claim(ProgressData data, ExperimentInfo experiment, string levelId)
        {
            int level = 0;
            int bonus = 0;

            if (Contains(experiment.LevelIds, levelId) && data.completedLevelIds.Contains(levelId) &&
                !data.claimedLevelRewards.Contains(levelId))
            {
                data.claimedLevelRewards.Add(levelId);
                level = LevelFirstClear;
            }

            if (IsExperimentComplete(data, experiment) && !data.claimedExperimentRewards.Contains(experiment.Id))
            {
                data.claimedExperimentRewards.Add(experiment.Id);
                bonus = ExperimentFirstCompletion;
            }

            data.research += level + bonus;
            return new ResearchAward(level, bonus);
        }

        public static bool IsExperimentComplete(ProgressData data, ExperimentInfo experiment)
        {
            if (experiment.LevelIds == null || experiment.LevelIds.Count == 0) return false;
            foreach (var id in experiment.LevelIds)
            {
                if (!data.completedLevelIds.Contains(id)) return false;
            }
            return true;
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value) return true;
            }
            return false;
        }
    }
}
