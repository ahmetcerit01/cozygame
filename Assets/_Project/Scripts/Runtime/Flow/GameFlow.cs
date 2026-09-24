using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Gameplay;
using CozyLab.Puzzle.Progression;
using CozyLab.Puzzle.UI;
using UnityEngine;

namespace CozyLab.Puzzle.Flow
{
    /// <summary>
    /// Navigation between Level Select and the puzzle, and the only place that writes progression.
    /// Launch → Level Select → level → Cure Found → next level … → level 10 → "experiment complete".
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        public enum FlowScreen
        {
            None,
            LevelSelect,
            Puzzle,
        }

        [SerializeField] private LevelCatalog catalog;
        [SerializeField] private PuzzleTheme theme;
        [SerializeField] private PuzzleAudioSet audioSet;
        [SerializeField] private PuzzleScreen puzzleScreen;
        [SerializeField] private LevelSelectScreen levelSelect;
        [SerializeField] private int targetFrameRate = 60;

        /// <summary>Tests can inject an isolated store before the scene loads.</summary>
        public static IProgressStore StoreOverride { get; set; }
        /// <summary>The running flow (used by editor dev tools).</summary>
        public static GameFlow Active { get; private set; }

        public LevelCatalog Catalog => catalog;
        public LevelProgression Progression { get; private set; }
        public PuzzleScreen PuzzleScreen => puzzleScreen;
        public LevelSelectScreen LevelSelect => levelSelect;
        public FlowScreen CurrentScreen { get; private set; }
        /// <summary>Index of the level open in the puzzle screen (or last played), -1 if none.</summary>
        public int CurrentLevelIndex { get; private set; } = -1;

        private void Awake()
        {
            Active = this;
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        private void Start()
        {
            if (catalog == null || theme == null || puzzleScreen == null || levelSelect == null)
            {
                Debug.LogError("[GameFlow] Assign catalog, theme, puzzle screen and level select.", this);
                return;
            }

            if (targetFrameRate > 0) Application.targetFrameRate = targetFrameRate;
            foreach (var problem in catalog.Validate(checkSolvable: false))
                Debug.LogError($"[GameFlow] Catalog '{catalog.name}': {problem}", catalog);

            ScreenCanvas.EnsureEventSystem();

            var store = StoreOverride ?? new JsonFileProgressStore(JsonFileProgressStore.DefaultPath);
            Progression = new LevelProgression(catalog.GetLevelIds(), store);

            puzzleScreen.Configure(theme, audioSet);
            puzzleScreen.Solved += OnLevelSolved;
            puzzleScreen.ContinueRequested += OnContinue;
            puzzleScreen.LevelSelectRequested += () => ShowLevelSelect();

            levelSelect.Initialize(catalog, Progression, theme);
            levelSelect.PlayRequested += index => TryOpenLevel(index);
            levelSelect.DevResetRequested += ResetProgress;

            ShowLevelSelect();
        }

        /// <summary>Opens a level if progression allows it. Locked levels are rejected.</summary>
        public bool TryOpenLevel(int index)
        {
            if (Progression == null || !Progression.CanPlay(index)) return false;
            var level = catalog.GetLevel(index);
            if (level == null) return false;

            bool opened = puzzleScreen.Open(new PuzzleScreenRequest
            {
                Level = level,
                LevelNumber = index + 1,
                IsLastLevel = index == catalog.Count - 1,
            });
            if (!opened) return false;

            CurrentLevelIndex = index;
            levelSelect.Hide();
            CurrentScreen = FlowScreen.Puzzle;
            return true;
        }

        public void ShowLevelSelect(bool showMilestone = false)
        {
            puzzleScreen.Close();
            // Highlight what to play next: the level after the one just finished, else the current frontier.
            int select = Progression.CurrentLevelIndex;
            if (CurrentLevelIndex >= 0 && Progression.IsCompleted(CurrentLevelIndex) &&
                Progression.CanPlay(CurrentLevelIndex + 1) && !Progression.IsCompleted(CurrentLevelIndex + 1))
                select = CurrentLevelIndex + 1;
            else if (CurrentLevelIndex >= 0 && !Progression.IsCompleted(CurrentLevelIndex))
                select = CurrentLevelIndex;

            levelSelect.Show(select, showMilestone);
            CurrentScreen = FlowScreen.LevelSelect;
        }

        private void OnLevelSolved(LevelDefinition level)
        {
            // Saved immediately, so quitting during the celebration never loses the unlock.
            int index = catalog.IndexOf(level);
            if (index >= 0) Progression.MarkCompleted(index);
        }

        private void OnContinue()
        {
            int next = CurrentLevelIndex + 1;
            if (next < catalog.Count && TryOpenLevel(next)) return;

            // Finished the last level of the chapter.
            ShowLevelSelect(showMilestone: CurrentLevelIndex == catalog.Count - 1);
        }

        // ---------------------------------------------------------------- development helpers

        [ContextMenu("Dev/Reset Progress")]
        public void ResetProgress()
        {
            if (Progression == null) return;
            Progression.Reset();
            CurrentLevelIndex = -1;
            Debug.Log("[GameFlow] Progress reset.");
            if (CurrentScreen == FlowScreen.LevelSelect) ShowLevelSelect();
        }

        [ContextMenu("Dev/Unlock All Levels")]
        public void UnlockAllLevels()
        {
            if (Progression == null) return;
            Progression.UnlockAll();
            Debug.Log("[GameFlow] All levels unlocked.");
        }
    }
}
