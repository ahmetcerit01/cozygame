using System.Collections.Generic;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Feedback;
using CozyLab.Puzzle.Gameplay;
using CozyLab.Puzzle.Meta;
using CozyLab.Puzzle.Progression;
using CozyLab.Puzzle.UI;
using UnityEngine;

namespace CozyLab.Puzzle.Flow
{
    /// <summary>
    /// Navigation between Home, Experiments, Level Select, Puzzle and Lab, and the only place that writes
    /// progression. Puzzle progression unlocks puzzles; the optional Lab (Research, equipment) never gates them.
    ///
    ///   Launch → Home → CONTINUE → Puzzle
    ///   Home → Experiments → Virus Samples → Level Select → Puzzle
    ///   Home ⇄ Lab
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        public enum FlowScreen
        {
            None,
            LevelSelect,
            Puzzle,
            Home,
            Experiments,
            Lab,
        }

        [SerializeField] private LevelCatalog catalog;
        [SerializeField] private PuzzleTheme theme;
        [SerializeField] private PuzzleAudioSet audioSet;
        [SerializeField] private PuzzleScreen puzzleScreen;
        [SerializeField] private LevelSelectScreen levelSelect;
        [SerializeField] private HomeScreen home;
        [SerializeField] private ExperimentsScreen experiments;
        [SerializeField] private LabScreen lab;
        [SerializeField] private int targetFrameRate = 60;

        /// <summary>Tests can inject an isolated store before the scene loads.</summary>
        public static IProgressStore StoreOverride { get; set; }
        /// <summary>The running flow (used by editor dev tools).</summary>
        public static GameFlow Active { get; private set; }

        public LevelCatalog Catalog => catalog;
        public ProgressRepository Repository { get; private set; }
        public LevelProgression Progression { get; private set; }
        public MetaProgression Meta { get; private set; }
        public PuzzleScreen PuzzleScreen => puzzleScreen;
        public LevelSelectScreen LevelSelect => levelSelect;
        public HomeScreen Home => home;
        public ExperimentsScreen Experiments => experiments;
        public LabScreen Lab => lab;
        public FlowScreen CurrentScreen { get; private set; }
        /// <summary>Index of the level open in the puzzle screen (or last played), -1 if none.</summary>
        public int CurrentLevelIndex { get; private set; } = -1;
        /// <summary>Research granted by the most recent completion (for tests / UI).</summary>
        public ResearchAward LastAward { get; private set; }

        private ExperimentInfo _experiment;
        private PuzzleFeedback _feedback;
        private FlowScreen _levelSelectReturn = FlowScreen.Experiments;
        private int _curedAtLastHome = -1;
        private int _pendingMilestoneResearch;
        private bool _pendingMilestone;

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
            if (catalog == null || theme == null || puzzleScreen == null || levelSelect == null || home == null ||
                experiments == null || lab == null)
            {
                Debug.LogError("[GameFlow] Assign catalog, theme and all screens.", this);
                return;
            }

            if (targetFrameRate > 0) Application.targetFrameRate = targetFrameRate;
#if UNITY_EDITOR
            if (Screen.width > Screen.height)
                Debug.LogWarning("[GameFlow] CozyLab is a portrait game. The Game view is landscape " +
                                 $"({Screen.width}×{Screen.height}); choose a portrait size such as 1290×2796 or 1080×1920 " +
                                 "(or the Device Simulator) to see the real layout.");
#endif
            foreach (var problem in catalog.Validate(checkSolvable: false))
                Debug.LogError($"[GameFlow] Catalog '{catalog.name}': {problem}", catalog);

            ScreenCanvas.EnsureEventSystem();

            // One save file: puzzle progress + optional meta. Old saves are migrated exactly once on load.
            _experiment = new ExperimentInfo(catalog.ChapterId, catalog.GetLevelIds());
            var experimentsForMigration = new List<ExperimentInfo> { _experiment };
            var store = StoreOverride ?? new JsonFileProgressStore(JsonFileProgressStore.DefaultPath);
            Repository = new ProgressRepository(store, data => SaveMigration.MigrateToCurrent(data, experimentsForMigration));
            if (Repository.Migrated)
                Debug.Log($"[GameFlow] Save migrated from v{Repository.LoadedVersion} to v{ProgressData.CurrentVersion}. " +
                          $"Research: {Repository.Data.research}");

            Progression = new LevelProgression(_experiment.LevelIds, Repository);
            Meta = new MetaProgression(Repository);

            _feedback = gameObject.AddComponent<PuzzleFeedback>();
            _feedback.Initialize(audioSet);

            puzzleScreen.Configure(theme, audioSet);
            puzzleScreen.Solved += OnLevelSolved;
            puzzleScreen.ContinueRequested += OnContinue;
            puzzleScreen.LevelSelectRequested += () => ShowLevelSelect();

            levelSelect.Initialize(catalog, Progression, theme);
            levelSelect.PlayRequested += index => TryOpenLevel(index);
            levelSelect.DevResetRequested += ResetProgress;
            levelSelect.BackRequested += () => Navigate(_levelSelectReturn);
            levelSelect.MilestoneHomeRequested += () => ShowHome();
            levelSelect.MilestoneLabRequested += () => ShowLab();

            home.Initialize(theme, catalog.Count, catalog.ChapterSubtitle);
            home.PrimaryClicked += OnHomePrimary;
            home.ExperimentsClicked += () => { Select(); ShowExperiments(); };
            home.LabClicked += () => { Select(); ShowLab(); };

            experiments.Initialize(theme, catalog);
            experiments.HomeClicked += () => ShowHome();
            experiments.ExperimentClicked += () =>
            {
                Select();
                _levelSelectReturn = FlowScreen.Experiments;
                ShowLevelSelect();
            };

            lab.Initialize(theme);
            lab.HomeClicked += () => ShowHome(fromLab: true);
            lab.EquipmentTapped += _ => _feedback.Emit(PuzzleFeedbackEvent.EquipmentTap);
            lab.UpgradeClicked += OnUpgradeClicked;

            _curedAtLastHome = Progression.CompletedCount;
            ShowHome();
        }

        // ---------------------------------------------------------------- navigation

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
            HideMetaScreens();
            levelSelect.Hide();
            CurrentScreen = FlowScreen.Puzzle;
            return true;
        }

        public void ShowHome(bool fromLab = false)
        {
            puzzleScreen.Close();
            levelSelect.Hide();
            experiments.Hide();
            lab.Hide();

            int cured = Progression.CompletedCount;
            int newlyFrom = _curedAtLastHome >= 0 && cured > _curedAtLastHome ? _curedAtLastHome : -1;
            _curedAtLastHome = cured;

            int next = Progression.CurrentLevelIndex;
            var nextLevel = catalog.GetLevel(next);
            home.Show(new HomeState
            {
                ExperimentLabel = catalog.ChapterTitle.ToUpperInvariant(),
                ExperimentTitle = catalog.ChapterSubtitle,
                Cured = cured,
                Total = catalog.Count,
                NextLevelNumber = Progression.IsChapterComplete ? 0 : next + 1,
                NextLevelTitle = nextLevel != null ? nextLevel.Title : string.Empty,
                MicroscopeLevel = Meta.GetLevel(LabEquipment.Microscope),
                NewlyCuredFrom = newlyFrom,
            }, fromLab ? 0.9f : 0.98f);
            CurrentScreen = FlowScreen.Home;
        }

        public void ShowExperiments()
        {
            puzzleScreen.Close();
            levelSelect.Hide();
            home.Hide();
            lab.Hide();
            experiments.Show(Progression.CompletedCount, catalog.Count);
            CurrentScreen = FlowScreen.Experiments;
        }

        public void ShowLab()
        {
            puzzleScreen.Close();
            levelSelect.Hide();
            home.Hide();
            experiments.Hide();

            var state = BuildLabState();
            // First time an unlock is visible: soft reveal + sound, then remember it.
            if (state.IncubatorUnlocked && Meta.MarkUnlockSeen(LabEquipment.IncubatorId)) state.RevealIncubator = true;
            if (state.AnalyzerUnlocked && Meta.MarkUnlockSeen(LabEquipment.AnalyzerId)) state.RevealAnalyzer = true;
            lab.Show(state);
            if (state.RevealIncubator || state.RevealAnalyzer) _feedback.Emit(PuzzleFeedbackEvent.EquipmentUnlock);
            CurrentScreen = FlowScreen.Lab;
        }

        public void ShowLevelSelect(bool showMilestone = false, int milestoneResearch = 0)
        {
            puzzleScreen.Close();
            HideMetaScreens();
            // Highlight what to play next: the level after the one just finished, else the current frontier.
            int select = Progression.CurrentLevelIndex;
            if (CurrentLevelIndex >= 0 && Progression.IsCompleted(CurrentLevelIndex) &&
                Progression.CanPlay(CurrentLevelIndex + 1) && !Progression.IsCompleted(CurrentLevelIndex + 1))
                select = CurrentLevelIndex + 1;
            else if (CurrentLevelIndex >= 0 && !Progression.IsCompleted(CurrentLevelIndex))
                select = CurrentLevelIndex;

            levelSelect.Show(select, showMilestone, milestoneResearch);
            CurrentScreen = FlowScreen.LevelSelect;
        }

        private void Navigate(FlowScreen screen)
        {
            switch (screen)
            {
                case FlowScreen.Home: ShowHome(); break;
                case FlowScreen.Lab: ShowLab(); break;
                case FlowScreen.LevelSelect: ShowLevelSelect(); break;
                default: ShowExperiments(); break;
            }
        }

        private void HideMetaScreens()
        {
            home.Hide();
            experiments.Hide();
            lab.Hide();
        }

        private void OnHomePrimary()
        {
            Select();
            if (Progression.IsChapterComplete)
            {
                // VIEW RESULTS: the (fully cured) Level Select, returning to Home.
                _levelSelectReturn = FlowScreen.Home;
                ShowLevelSelect();
                return;
            }
            // BEGIN / CONTINUE: straight into the next incomplete level.
            _levelSelectReturn = FlowScreen.Experiments;
            TryOpenLevel(Progression.CurrentLevelIndex);
        }

        // ---------------------------------------------------------------- puzzle events

        private void OnLevelSolved(LevelDefinition level)
        {
            // Saved immediately, so quitting during the celebration never loses the unlock or reward.
            int index = catalog.IndexOf(level);
            if (index < 0) return;
            Progression.MarkCompleted(index);

            LastAward = Meta.AwardCompletion(_experiment, level.LevelId);
            puzzleScreen.Hud?.SetPendingResearch(LastAward.LevelReward);
            if (LastAward.Total > 0) _feedback.Emit(PuzzleFeedbackEvent.ResearchReward);

            // The experiment-complete card is shown for the first completion only (the +100 moment).
            _pendingMilestone = LastAward.ExperimentReward > 0;
            _pendingMilestoneResearch = LastAward.ExperimentReward;
        }

        private void OnContinue()
        {
            int next = CurrentLevelIndex + 1;
            if (next < catalog.Count && TryOpenLevel(next)) return;

            // Finished the last level of the chapter.
            bool milestone = _pendingMilestone;
            int research = _pendingMilestoneResearch;
            _pendingMilestone = false;
            _pendingMilestoneResearch = 0;
            ShowLevelSelect(showMilestone: milestone, milestoneResearch: research);
        }

        // ---------------------------------------------------------------- lab

        private LabState BuildLabState()
        {
            int microscope = Meta.GetLevel(LabEquipment.Microscope);
            int labLevel = Meta.LabLevel;
            return new LabState
            {
                Research = Meta.Research,
                MicroscopeLevel = microscope,
                MicroscopeMaxLevel = LabEquipment.Microscope.MaxLevel,
                UpgradeCost = Meta.GetUpgradeCost(LabEquipment.Microscope),
                LabLevel = labLevel,
                IncubatorUnlocked = LabEquipment.IsIncubatorUnlocked(Progression.IsChapterComplete),
                AnalyzerUnlocked = LabEquipment.IsAnalyzerUnlocked(labLevel),
                Cured = Progression.CompletedCount,
                ExperimentComplete = Progression.IsChapterComplete,
            };
        }

        private void OnUpgradeClicked(string equipmentId)
        {
            if (equipmentId != LabEquipment.Microscope.Id) return;
            bool analyzerWasUnlocked = LabEquipment.IsAnalyzerUnlocked(Meta.LabLevel);

            var result = Meta.TryUpgrade(LabEquipment.Microscope);
            if (result != UpgradeResult.Upgraded) return;

            var state = BuildLabState();
            lab.Apply(state, animateResearch: true);
            lab.PlayUpgrade();
            _feedback.Emit(PuzzleFeedbackEvent.EquipmentUpgrade);

            if (!analyzerWasUnlocked && state.AnalyzerUnlocked && Meta.MarkUnlockSeen(LabEquipment.AnalyzerId))
                _feedback.Emit(PuzzleFeedbackEvent.EquipmentUnlock);
        }

        private void Select() => _feedback.Emit(PuzzleFeedbackEvent.UiSelect);

        // ---------------------------------------------------------------- development helpers

        [ContextMenu("Dev/Reset Progress")]
        public void ResetProgress()
        {
            if (Progression == null) return;
            Progression.Reset();
            Meta.NotifyChanged();
            CurrentLevelIndex = -1;
            _curedAtLastHome = 0;
            Debug.Log("[GameFlow] Progress reset (puzzle + meta).");
            RefreshCurrentScreen();
        }

        [ContextMenu("Dev/Unlock All Levels")]
        public void UnlockAllLevels()
        {
            if (Progression == null) return;
            Progression.UnlockAll();
            Debug.Log("[GameFlow] All levels unlocked.");
        }

        public void DevAddResearch(int amount)
        {
            if (Meta == null) return;
            Meta.DevAddResearch(amount);
            RefreshCurrentScreen();
        }

        public void DevClearResearch()
        {
            if (Meta == null) return;
            Meta.DevClearResearch();
            RefreshCurrentScreen();
        }

        public void DevSetMicroscopeLevel(int level)
        {
            if (Meta == null) return;
            Meta.DevSetLevel(LabEquipment.Microscope, level);
            RefreshCurrentScreen();
        }

        public void DevResetMeta()
        {
            if (Meta == null) return;
            Meta.DevResetMeta();
            RefreshCurrentScreen();
        }

        private void RefreshCurrentScreen()
        {
            switch (CurrentScreen)
            {
                case FlowScreen.Home: ShowHome(); break;
                case FlowScreen.Experiments: ShowExperiments(); break;
                case FlowScreen.LevelSelect: ShowLevelSelect(); break;
                case FlowScreen.Lab: lab.Apply(BuildLabState(), animateResearch: true); break;
            }
        }
    }
}
