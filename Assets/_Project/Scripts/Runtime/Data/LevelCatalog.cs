using System.Collections.Generic;
using UnityEngine;

namespace CozyLab.Puzzle.Data
{
    /// <summary>
    /// Ordered list of levels forming one chapter ("experiment"). Adding levels is purely data:
    /// create a LevelDefinition asset and append it here.
    /// </summary>
    [CreateAssetMenu(menuName = "CozyLab/Puzzle/Level Catalog", fileName = "LevelCatalog_New")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField] private string chapterId = "experiment_01";
        [SerializeField] private string chapterTitle = "Experiment 01";
        [SerializeField] private string chapterSubtitle = "Virus Samples";
        [SerializeField] private string completionTitle = "EXPERIMENT COMPLETE";
        [SerializeField, TextArea] private string completionMessage = "Every sample is contained.";
        [SerializeField] private List<LevelDefinition> levels = new List<LevelDefinition>();

        public string ChapterId => chapterId;
        public string ChapterTitle => chapterTitle;
        public string ChapterSubtitle => chapterSubtitle;
        public string CompletionTitle => completionTitle;
        public string CompletionMessage => completionMessage;
        public int Count => levels.Count;

        public LevelDefinition GetLevel(int index) => index >= 0 && index < levels.Count ? levels[index] : null;

        public int IndexOf(LevelDefinition level) => levels.IndexOf(level);

        /// <summary>Stable ids in play order (used by progression so reordering assets never corrupts saves).</summary>
        public List<string> GetLevelIds()
        {
            var ids = new List<string>(levels.Count);
            foreach (var level in levels) ids.Add(level != null ? level.LevelId : string.Empty);
            return ids;
        }

        public List<string> Validate(bool checkSolvable = true)
        {
            var problems = new List<string>();
            if (levels.Count == 0) problems.Add("Catalog has no levels.");

            var ids = new HashSet<string>();
            for (int i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                if (level == null)
                {
                    problems.Add($"Entry {i} is empty.");
                    continue;
                }
                if (!ids.Add(level.LevelId)) problems.Add($"Duplicate level id '{level.LevelId}' at entry {i}.");
                foreach (var problem in level.Validate(checkSolvable)) problems.Add($"{level.name}: {problem}");
            }
            return problems;
        }
    }
}
