using System.Collections.Generic;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Feedback;
using CozyLab.Puzzle.Gameplay;
using CozyLab.Puzzle.Localization;
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
    ///   Home: authored desk with CONTINUE + Experiments / Lab / Daily / Shop tiles (no tab bar).
    ///   Tab bar (Experiments · Home · Lab) on the other meta screens; hidden during gameplay.
    ///   Experiments tab → sample rack (Level Select) → Puzzle;  rack "ALL" → Experiments journal
    ///   Puzzle LEVELS → sample rack
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
        [Tooltip("Authored Home art (Assets/Art/Home/Home_CleanPlate.png), handed to the Home screen at startup.")]
        [SerializeField] private Sprite homeBackground;
        [Tooltip("Authored Home UI icons (Assets/Art/UI/Icons), handed to the Home screen at startup.")]
        [SerializeField] private HomeIcons homeIcons;
        [SerializeField] private int targetFrameRate = 60;

#if UNITY_EDITOR
        public const string HomeBackgroundPath = "Assets/Art/Home/Home_CleanPlate.png";
        private const string IconFolder = "Assets/Art/UI/Icons/";

        // Keeps the references filled even if the scene was open (in memory) when the fields were added on disk.
        private void OnValidate()
        {
            if (homeBackground == null) homeBackground = Load(HomeBackgroundPath);
            Fill(ref homeIcons.coin, "ui_icon_coin");
            Fill(ref homeIcons.research, "ui_icon_research");
            Fill(ref homeIcons.add, "ui_icon_add");
            Fill(ref homeIcons.settings, "ui_icon_settings");
            Fill(ref homeIcons.experiments, "ui_icon_experiments");
            Fill(ref homeIcons.lab, "ui_icon_labs");
            Fill(ref homeIcons.daily, "ui_icon_daily");
            Fill(ref homeIcons.shop, "ui_icon_shop");
        }

        private static void Fill(ref Sprite sprite, string icon)
        {
            if (sprite == null) sprite = Load(IconFolder + icon + ".png");
        }

        private static Sprite Load(string path) => UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private void Reset() => OnValidate();
#endif

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
        public MetaNavBar NavBar => _nav;
        public MetaDialog Dialog => _dialog;
        public FlowScreen CurrentScreen { get; private set; }
        /// <summary>Index of the level open in the puzzle screen (or last played), -1 if none.</summary>
        public int CurrentLevelIndex { get; private set; } = -1;
        /// <summary>Research granted by the most recent completion (for tests / UI).</summary>
        public ResearchAward LastAward { get; private set; }

        private ExperimentInfo _experiment;
        private PuzzleFeedback _feedback;
        private MetaNavBar _nav;
        private MetaDialog _dialog;
        private int _curedAtLastHome = -1;
        private int _pendingMilestoneResearch;
        private bool _pendingMilestone;

        private void Awake()
        {
            Active = this;
            // Before any screen builds: pick the language (saved choice, else device language) and share icons.
            Loc.EnsureInitialized();
            CureUI.Icons = homeIcons;
        }

        private static System.Action<RectTransform, float, Color> SpriteIcon(Sprite sprite) =>
            (rt, size, _) => CureUI.SpriteIcon(rt, "Icon", sprite, size * 1.2f, Vector2.zero);

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
            levelSelect.BackRequested += () => { Select(); ShowExperiments(); };
            levelSelect.MilestoneHomeRequested += () => ShowHome();
            levelSelect.MilestoneLabRequested += () => ShowLab();

            // Shared front-end chrome: one tab bar and one placeholder dialog for all meta screens.
            var reference = ScreenCanvas.DefaultReferenceResolution;
            _nav = new GameObject("MetaNavBar").AddComponent<MetaNavBar>();
            _nav.transform.SetParent(transform, false);
            _nav.Initialize(reference);
            _nav.TabClicked += OnTabClicked;
            _dialog = new GameObject("MetaDialog").AddComponent<MetaDialog>();
            _dialog.transform.SetParent(transform, false);
            _dialog.Initialize(reference);

            if (homeBackground == null)
                Debug.LogError("[GameFlow] Assign 'Home Background' (Assets/Art/Home/Home_CleanPlate.png).", this);
            if (homeIcons.coin == null || homeIcons.add == null || homeIcons.experiments == null)
                Debug.LogError("[GameFlow] Assign 'Home Icons' (Assets/Art/UI/Icons).", this);
            home.Initialize(theme, homeBackground, homeIcons);
            home.SettingsClicked += () =>
            {
                Select();
                _dialog.ShowSettings(null);
            };
            home.ExperimentsClicked += () => { Select(); ShowLevelSelect(); };
            home.LabClicked += () => { Select(); ShowLab(); };
            home.PrimaryClicked += OnHomePrimary;
            home.DailyClicked += () =>
            {
                Select();
                _dialog.Show("daily_title", "daily_coming_soon", SpriteIcon(homeIcons.daily));
            };
            home.ShopClicked += () =>
            {
                Select();
                _dialog.Show("shop_title", "shop_coming_soon", SpriteIcon(homeIcons.shop));
            };

            experiments.Initialize(theme, catalog);
            experiments.ExperimentClicked += () =>
            {
                Select();
                ShowLevelSelect();
            };

            lab.Initialize(theme);
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
            _nav.Hide();
            _dialog.Close();
            CurrentScreen = FlowScreen.Puzzle;
            return true;
        }

        public void ShowHome(bool fromLab = false)
        {
            puzzleScreen.Close();
            levelSelect.Hide();
            experiments.Hide();
            lab.Hide();
            _dialog.Close();
            _nav.Hide(); // Home has its own navigation tiles

            int cured = Progression.CompletedCount;
            int newlyFrom = _curedAtLastHome >= 0 && cured > _curedAtLastHome ? _curedAtLastHome : -1;
            _curedAtLastHome = cured;

            int next = Progression.CurrentLevelIndex;
            var nextLevel = catalog.GetLevel(next);
            home.Show(new HomeState
            {
                ExperimentTitle = Loc.ChapterSubtitle(catalog.ChapterId, catalog.ChapterSubtitle),
                Cured = cured,
                Total = catalog.Count,
                NextLevelNumber = Progression.IsChapterComplete ? 0 : next + 1,
                NextLevelTitle = nextLevel != null ? Loc.LevelTitle(nextLevel.LevelId, nextLevel.Title) : new LocString(null, string.Empty),
                MicroscopeLevel = Meta.GetLevel(LabEquipment.Microscope),
                NewlyCuredFrom = newlyFrom,
                Research = Meta.Research,
                UpgradeAvailable = Meta.CanUpgrade(LabEquipment.Microscope),
            }, fromLab ? 0.94f : 0.98f);
            CurrentScreen = FlowScreen.Home;
        }

        public void ShowExperiments()
        {
            puzzleScreen.Close();
            levelSelect.Hide();
            home.Hide();
            lab.Hide();
            ShowNav(MetaTab.Experiments);
            experiments.Show(Progression.CompletedCount, catalog.Count, Meta.Research);
            CurrentScreen = FlowScreen.Experiments;
        }

        public void ShowLab()
        {
            puzzleScreen.Close();
            levelSelect.Hide();
            home.Hide();
            experiments.Hide();
            ShowNav(MetaTab.Lab);

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

            ShowNav(MetaTab.Experiments);
            levelSelect.Show(select, showMilestone, milestoneResearch);
            CurrentScreen = FlowScreen.LevelSelect;
        }

        private void OnTabClicked(MetaTab tab)
        {
            var target = tab == MetaTab.Home ? FlowScreen.Home : tab == MetaTab.Lab ? FlowScreen.Lab : FlowScreen.LevelSelect;
            if (target == CurrentScreen) return;
            Select();
            switch (tab)
            {
                case MetaTab.Home: ShowHome(fromLab: CurrentScreen == FlowScreen.Lab); break;
                case MetaTab.Lab: ShowLab(); break;
                default: ShowLevelSelect(); break;
            }
        }

        private void ShowNav(MetaTab tab)
        {
            _dialog.Close();
            _nav.Show(tab);
            // A quiet nudge that Research can be spent; the Lab itself stays optional.
            _nav.SetBadge(MetaTab.Lab, Meta.CanUpgrade(LabEquipment.Microscope));
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
            // VIEW RESULTS: the fully cured sample rack. BEGIN / CONTINUE: straight into the next incomplete level.
            if (Progression.IsChapterComplete) ShowLevelSelect();
            else TryOpenLevel(Progression.CurrentLevelIndex);
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
            _nav.SetBadge(MetaTab.Lab, Meta.CanUpgrade(LabEquipment.Microscope));
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
                case FlowScreen.Lab:
                    lab.Apply(BuildLabState(), animateResearch: true);
                    _nav.SetBadge(MetaTab.Lab, Meta.CanUpgrade(LabEquipment.Microscope));
                    break;
            }
        }
    }
}
