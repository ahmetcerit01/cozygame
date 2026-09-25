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
            Assert.AreEqual("BEGIN EXPERIMENT", flow.Home.PrimaryLabel);
            Assert.AreEqual(0, flow.Home.Dish.Cured);
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

            // LEVELS → Level Select → BACK → Experiments → HOME.
            T.Click(T.FindButton("LevelsButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.LevelSelect, flow.CurrentScreen);
            T.Click(T.FindButton("BackButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Experiments, flow.CurrentScreen);
            T.Click(T.FindButton("HomeButton"));
            yield return null;

            Assert.AreEqual(GameFlow.FlowScreen.Home, flow.CurrentScreen);
            Assert.AreEqual(1, flow.Home.Dish.Cured, "one colony now cured");
            Assert.IsTrue(flow.Home.IsToastVisible, "brief NEW SAMPLE CURED acknowledgement");
            Assert.AreEqual(HomeScreen.PrimaryAction.Continue, flow.Home.CurrentAction);
            Assert.AreEqual("CONTINUE", flow.Home.PrimaryLabel);
            StringAssert.Contains("Level 2", flow.Home.SupportTitle);

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
            Assert.AreEqual(5, flow.Home.Dish.Cured);
            StringAssert.Contains("Level 6", flow.Home.SupportTitle);

            T.Click(T.FindButton("PrimaryButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Puzzle, flow.CurrentScreen, "Home → Continue → Puzzle");
            Assert.AreEqual(5, flow.CurrentLevelIndex);
            Assert.AreEqual("lvl_06", flow.PuzzleScreen.CurrentLevel.LevelId);
            Assert.IsFalse(flow.LevelSelect.IsVisible);
            Assert.IsFalse(flow.Lab.IsVisible);
        }

        [UnityTest]
        public IEnumerator Experiments_OpenExistingLevelSelect_ThenPuzzle()
        {
            yield return T.LoadGame();
            var flow = GameFlow.Active;

            T.ClickObject("ExperimentsObject");
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Experiments, flow.CurrentScreen);
            Assert.AreEqual("0 / 10 CURED", flow.Experiments.ProgressText);

            T.ClickObject("Experiment01"); // tap the dish itself
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.LevelSelect, flow.CurrentScreen);
            Assert.AreEqual(10, flow.LevelSelect.Dishes.Count, "the existing 10-dish Level Select");
            Assert.AreEqual(LevelDishState.Locked, flow.LevelSelect.Dishes[1].State);

            T.Click(T.FindButton("PlayButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Puzzle, flow.CurrentScreen);

            T.Click(T.FindButton("BackButton")); // puzzle LEVELS pill
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.LevelSelect, flow.CurrentScreen);
            T.Click(T.FindButton("BackButton")); // level select BACK
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Experiments, flow.CurrentScreen);
            T.Click(T.FindButton("HomeButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Home, flow.CurrentScreen);
        }

        [UnityTest]
        public IEnumerator Lab_UpgradeMicroscope_PersistsAfterReload()
        {
            SeedV1Save(6); // migrates to 120 Research
            yield return T.LoadGame();
            var flow = GameFlow.Active;

            T.ClickObject("LabObject");
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Lab, flow.CurrentScreen);
            Assert.AreEqual(120, flow.Lab.ShownResearch, "Research visible in the Lab");
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

            T.Click(T.FindButton("HomeButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Home, flow.CurrentScreen);

            yield return T.LoadGame();
            var reopened = GameFlow.Active;
            Assert.AreEqual(2, reopened.Meta.GetLevel(LabEquipment.Microscope), "upgrade persisted");
            Assert.AreEqual(20, reopened.Meta.Research, "balance persisted, migration not repeated");
            T.ClickObject("LabObject");
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
            Assert.AreEqual(10, flow.Home.Dish.Cured);
            Assert.AreEqual("EXPERIMENT COMPLETE", flow.Home.Dish.StatusText);

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
