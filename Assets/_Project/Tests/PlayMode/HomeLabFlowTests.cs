using System.Collections;
using System.IO;
using System.Linq;
using CozyLab.Puzzle.Flow;
using CozyLab.Puzzle.Meta;
using CozyLab.Puzzle.Progression;
using CozyLab.Puzzle.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using T = CozyLab.Puzzle.Tests.VerticalSliceFlowTests;

namespace CozyLab.Puzzle.Tests
{
    /// <summary>
    /// Home + optional Lab smoke tests on the real Game scene, with an isolated save file.
    /// </summary>
    public class HomeLabFlowTests
    {
        private const float CompletionWait = 1.8f;
        private string _savePath;

        [SetUp]
        public void SetUp()
        {
            _savePath = Path.Combine(Path.GetTempPath(), "cozylab_homelab_" + System.Guid.NewGuid().ToString("N") + ".json");
            GameFlow.StoreOverride = new JsonFileProgressStore(_savePath);
            // These flows assert English copy; the device language (e.g. Turkish) must not leak in.
            Localization.Loc.Initialize(new Localization.MemoryLanguageStore("en"), SystemLanguage.English);
        }

        [TearDown]
        public void TearDown()
        {
            GameFlow.StoreOverride = null;
            foreach (var p in new[] { _savePath, _savePath + ".tmp" })
            {
                if (File.Exists(p)) File.Delete(p);
            }
        }

        /// <summary>Writes a save exactly as the previous (v1) build did.</summary>
        private void SeedV1Save(int completed)
        {
            var ids = string.Join(",", Enumerable.Range(1, completed).Select(i => $"\"lvl_{i:D2}\""));
            File.WriteAllText(_savePath,
                $"{{\"version\":1,\"highestUnlockedIndex\":{Mathf.Min(completed, 9)},\"completedLevelIds\":[{ids}]}}");
        }

        [UnityTest]
        public IEnumerator FreshPlayer_Begin_Solve_EarnsResearch_HomeReflectsProgress_AndPersists()
        {
            yield return T.LoadGame();
            var flow = GameFlow.Active;

            Assert.AreEqual(GameFlow.FlowScreen.Home, flow.CurrentScreen);
            Assert.AreEqual(HomeScreen.PrimaryAction.Begin, flow.Home.CurrentAction);
            Assert.AreEqual("BEGIN", flow.Home.PrimaryLabel);
            Assert.AreEqual(0, flow.Home.Cured);
            Assert.IsTrue(flow.Home.HasAuthoredBackground, "authored Home clean plate assigned");
            Assert.AreEqual("CURRENT SAMPLE", flow.Home.ClipboardHeader);
            Assert.AreEqual(flow.Catalog.GetLevel(0).Title, flow.Home.ClipboardTitle, "clipboard shows the next level");
            Assert.AreEqual("Level 1", flow.Home.ClipboardLevel);
            Assert.IsFalse(flow.NavBar.IsVisible, "Home uses its own tiles, not the tab bar");
            Assert.AreEqual(0, flow.Meta.Research);

            // BEGIN → Level 1 directly.
            T.Click(T.FindButton("PrimaryButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Puzzle, flow.CurrentScreen);
            Assert.AreEqual(0, flow.CurrentLevelIndex);

            yield return T.SolveCurrentLevel(flow, dragFirstPiece: true);
            yield return new WaitForSecondsRealtime(CompletionWait + 0.5f); // card + reward count-up

            Assert.IsTrue(flow.PuzzleScreen.Hud.IsSuccessVisible, "CURE FOUND! preserved");
            Assert.AreEqual(20, flow.LastAward.LevelReward);
            Assert.AreEqual(20, flow.Meta.Research);
            Assert.IsTrue(flow.PuzzleScreen.Hud.IsRewardVisible);
            Assert.AreEqual("+20 RESEARCH", flow.PuzzleScreen.Hud.RewardText);

            // LEVELS → sample rack → ALL → Experiments journal → HOME tab.
            T.Click(T.FindButton("LevelsButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.LevelSelect, flow.CurrentScreen);
            Assert.IsTrue(flow.NavBar.IsVisible, "tab bar back on meta screens");
            T.Click(T.FindButton("AllExperimentsButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Experiments, flow.CurrentScreen);
            T.Click(T.FindButton("NavHome"));
            yield return null;

            Assert.AreEqual(GameFlow.FlowScreen.Home, flow.CurrentScreen);
            Assert.AreEqual(1, flow.Home.Cured, "one colony now cured");
            Assert.IsTrue(flow.Home.IsToastVisible, "brief NEW SAMPLE CURED acknowledgement");
            Assert.AreEqual(HomeScreen.PrimaryAction.Continue, flow.Home.CurrentAction);
            Assert.AreEqual("CONTINUE", flow.Home.PrimaryLabel);
            Assert.AreEqual("Level 2", flow.Home.ClipboardLevel);
            Assert.AreEqual(flow.Catalog.GetLevel(1).Title, flow.Home.ClipboardTitle);

            // Relaunch: progress and Research persist; no second acknowledgement.
            yield return T.LoadGame();
            var reopened = GameFlow.Active;
            Assert.AreEqual(20, reopened.Meta.Research);
            Assert.IsTrue(reopened.Progression.IsCompleted(0));
            Assert.AreEqual(HomeScreen.PrimaryAction.Continue, reopened.Home.CurrentAction);
            Assert.IsFalse(reopened.Home.IsToastVisible);
        }

        [UnityTest]
        public IEnumerator ReturningPlayer_Continue_OpensNextIncompleteLevelDirectly()
        {
            SeedV1Save(5);
            yield return T.LoadGame();
            var flow = GameFlow.Active;

            Assert.AreEqual(100, flow.Meta.Research, "v1 save migrated: 5 × 20");
            Assert.AreEqual(5, flow.Home.Cured);
            Assert.AreEqual("Level 6", flow.Home.ClipboardLevel);

            T.Click(T.FindButton("PrimaryButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Puzzle, flow.CurrentScreen, "Home → Continue → Puzzle");
            Assert.AreEqual(5, flow.CurrentLevelIndex);
            Assert.AreEqual("lvl_06", flow.PuzzleScreen.CurrentLevel.LevelId);
            Assert.IsFalse(flow.LevelSelect.IsVisible);
            Assert.IsFalse(flow.Lab.IsVisible);
        }

        [UnityTest]
        public IEnumerator Experiments_TileOpensSampleRack_JournalAndPuzzle()
        {
            yield return T.LoadGame();
            var flow = GameFlow.Active;
            Assert.IsFalse(flow.NavBar.IsVisible, "no tab bar on Home");

            T.Click(T.FindButton("ExperimentsButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.LevelSelect, flow.CurrentScreen);
            Assert.AreEqual(MetaTab.Experiments, flow.NavBar.Active);
            Assert.AreEqual(10, flow.LevelSelect.Dishes.Count, "one sample per level");
            Assert.AreEqual(LevelDishState.Available, flow.LevelSelect.Dishes[0].State);
            Assert.AreEqual(LevelDishState.Locked, flow.LevelSelect.Dishes[1].State);
            Assert.AreEqual("0 / 10 cured", flow.LevelSelect.ProgressText);

            // ALL → the journal of experiments → Experiment 01 card → back to its rack.
            T.Click(T.FindButton("AllExperimentsButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Experiments, flow.CurrentScreen);
            Assert.AreEqual("0 / 10 CURED", flow.Experiments.ProgressText);
            T.ClickObject("Experiment01"); // tap the card itself
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.LevelSelect, flow.CurrentScreen);

            T.Click(T.FindButton("PlayButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Puzzle, flow.CurrentScreen);
            Assert.IsFalse(flow.NavBar.IsVisible, "no tab bar during gameplay");

            T.Click(T.FindButton("BackButton")); // puzzle LEVELS pill
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.LevelSelect, flow.CurrentScreen);
            T.Click(T.FindButton("NavHome"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Home, flow.CurrentScreen);
        }

        [UnityTest]
        public IEnumerator Home_ShopAndDaily_OpenPlaceholderDialog()
        {
            yield return T.LoadGame();
            var flow = GameFlow.Active;

            T.Click(T.FindButton("ShopButton"));
            yield return null;
            Assert.IsTrue(flow.Dialog.IsVisible);
            T.Click(T.FindButton("DialogOkButton"));
            yield return null;
            Assert.IsFalse(flow.Dialog.IsVisible);

            T.Click(T.FindButton("SettingsButton"));
            yield return null;
            Assert.AreEqual("Settings", flow.Dialog.Title);
            T.Click(T.FindButton("DialogOkButton"));
            yield return null;

            T.Click(T.FindButton("DailyButton"));
            yield return null;
            Assert.IsTrue(flow.Dialog.IsVisible);
            T.Click(T.FindButton("LabButton")); // navigating away closes it
            yield return null;
            Assert.IsFalse(flow.Dialog.IsVisible);
            Assert.AreEqual(GameFlow.FlowScreen.Lab, flow.CurrentScreen);
        }

        [UnityTest]
        public IEnumerator Lab_UpgradeMicroscope_PersistsAfterReload()
        {
            SeedV1Save(6); // migrates to 120 Research
            yield return T.LoadGame();
            var flow = GameFlow.Active;

            Assert.IsTrue(flow.Home.IsLabBadgeVisible, "120 Research affords an upgrade");
            T.Click(T.FindButton("LabButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Lab, flow.CurrentScreen);
            Assert.AreEqual(120, flow.Lab.ShownResearch, "Research visible in the Lab");
            Assert.IsTrue(flow.Lab.IsUpgradeTagVisible);
            Assert.AreEqual("Complete Experiment 01", flow.Lab.IncubatorStatus);
            StringAssert.Contains("Lab Level 3", flow.Lab.AnalyzerStatus);

            T.ClickObject("MicroscopeObject");
            yield return null;
            Assert.IsTrue(flow.Lab.IsSheetOpen);
            Assert.AreEqual("Level 1", flow.Lab.SheetLevelText);
            Assert.IsTrue(flow.Lab.UpgradeButtonInteractable);

            T.Click(T.FindButton("UpgradeButton"));
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(2, flow.Meta.GetLevel(LabEquipment.Microscope));
            Assert.AreEqual(20, flow.Meta.Research, "100 deducted");
            Assert.AreEqual(20, flow.Lab.ShownResearch);
            Assert.AreEqual("Level 2", flow.Lab.SheetLevelText);
            Assert.IsFalse(flow.Lab.UpgradeButtonInteractable, "200 needed, only 20 available");

            // Upgrades never touch puzzle progression.
            Assert.AreEqual(6, flow.Progression.CompletedCount);
            Assert.IsFalse(flow.Progression.IsUnlocked(7));

            T.Click(T.FindButton("NavHome"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Home, flow.CurrentScreen);

            yield return T.LoadGame();
            var reopened = GameFlow.Active;
            Assert.AreEqual(2, reopened.Meta.GetLevel(LabEquipment.Microscope), "upgrade persisted");
            Assert.AreEqual(20, reopened.Meta.Research, "balance persisted, migration not repeated");
            T.Click(T.FindButton("LabButton"));
            yield return null;
            T.ClickObject("MicroscopeObject");
            yield return null;
            Assert.AreEqual("Level 2", reopened.Lab.SheetLevelText);
        }

        [UnityTest]
        public IEnumerator NeverOpeningLab_PlayerFinishesExperiment_FinaleBonusOnce()
        {
            yield return T.LoadGame();
            var flow = GameFlow.Active;

            T.Click(T.FindButton("PrimaryButton")); // BEGIN
            yield return null;

            // Chain through all ten levels with NEXT LEVEL; the Lab is never opened.
            for (int level = 0; level < 10; level++)
            {
                Assert.AreEqual(level, flow.CurrentLevelIndex);
                yield return T.SolveCurrentLevel(flow, dragFirstPiece: false);
                yield return new WaitForSecondsRealtime(CompletionWait);
                Assert.IsTrue(flow.PuzzleScreen.Hud.IsSuccessVisible);
                Assert.IsFalse(flow.Lab.IsVisible);
                T.Click(T.FindButton("ContinueButton")); // NEXT LEVEL / FINISH
                yield return null;
            }

            Assert.IsTrue(flow.Progression.IsChapterComplete);
            Assert.AreEqual(300, flow.Meta.Research, "10 × 20 + 100 exactly");
            Assert.AreEqual(1, flow.Meta.GetLevel(LabEquipment.Microscope), "Lab untouched");
            Assert.AreEqual(GameFlow.FlowScreen.LevelSelect, flow.CurrentScreen);
            Assert.IsTrue(flow.LevelSelect.IsMilestoneVisible, "EXPERIMENT COMPLETE");

            // Finale navigation → Home, which now shows the completed state.
            T.Click(T.FindButton("MilestoneHomeButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Home, flow.CurrentScreen);
            Assert.AreEqual(HomeScreen.PrimaryAction.ViewResults, flow.Home.CurrentAction);
            Assert.AreEqual(10, flow.Home.Cured);
            Assert.AreEqual("EXPERIMENT COMPLETE", flow.Home.ClipboardHeader);

            // Replaying Level 10 grants nothing and shows no second finale.
            Assert.IsTrue(flow.TryOpenLevel(9));
            yield return null;
            yield return T.SolveCurrentLevel(flow, dragFirstPiece: false);
            yield return new WaitForSecondsRealtime(CompletionWait);
            Assert.AreEqual(0, flow.LastAward.Total);
            Assert.IsFalse(flow.PuzzleScreen.Hud.IsRewardVisible);
            T.Click(T.FindButton("ContinueButton"));
            yield return null;
            Assert.IsFalse(flow.LevelSelect.IsMilestoneVisible);
            Assert.AreEqual(300, flow.Meta.Research);

            // The Incubator wakes up once the experiment is complete.
            flow.ShowLab();
            yield return null;
            Assert.AreEqual("Ready for future research", flow.Lab.IncubatorStatus);
        }
    }
}
