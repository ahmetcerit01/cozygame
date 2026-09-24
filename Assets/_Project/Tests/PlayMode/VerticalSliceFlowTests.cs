using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CozyLab.Puzzle.Core;
using CozyLab.Puzzle.Flow;
using CozyLab.Puzzle.Interaction;
using CozyLab.Puzzle.Presentation;
using CozyLab.Puzzle.Progression;
using CozyLab.Puzzle.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CozyLab.Puzzle.Tests
{
    /// <summary>
    /// End-to-end smoke tests of the real Game scene with an isolated save file
    /// (the player's own progress is never touched).
    /// </summary>
    public class VerticalSliceFlowTests
    {
        private const string SceneName = "Game";
        private const float CompletionWait = 1.8f;
        private string _savePath;

        [SetUp]
        public void SetUp()
        {
            _savePath = Path.Combine(Path.GetTempPath(), "cozylab_playmode_" + System.Guid.NewGuid().ToString("N") + ".json");
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

        [UnityTest]
        public IEnumerator FreshInstall_SolveLevel1_UnlocksLevel2_AndSurvivesRestart()
        {
            yield return LoadGame();
            var flow = GameFlow.Active;

            // Fresh progress: only Level 1 is open, locked levels are rejected.
            Assert.AreEqual(GameFlow.FlowScreen.LevelSelect, flow.CurrentScreen);
            Assert.IsTrue(flow.LevelSelect.IsVisible);
            Assert.IsTrue(flow.Progression.IsUnlocked(0));
            Assert.IsFalse(flow.Progression.IsUnlocked(1));
            Assert.IsFalse(flow.TryOpenLevel(1), "locked level must not open");
            Assert.AreEqual(LevelDishState.Available, flow.LevelSelect.Dishes[0].State);
            Assert.AreEqual(LevelDishState.Locked, flow.LevelSelect.Dishes[1].State);

            // Play Level 1 through the PLAY button.
            Click(FindButton("PlayButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.Puzzle, flow.CurrentScreen);
            Assert.AreEqual(0, flow.CurrentLevelIndex);
            Assert.IsFalse(flow.LevelSelect.IsVisible);

            yield return SolveCurrentLevel(flow, dragFirstPiece: true);
            yield return new WaitForSecondsRealtime(CompletionWait);

            Assert.IsTrue(flow.PuzzleScreen.Hud.IsSuccessVisible, "CURE FOUND! panel");
            Assert.IsTrue(flow.Progression.IsCompleted(0));
            Assert.IsTrue(flow.Progression.IsUnlocked(1));
            Assert.IsTrue(File.Exists(_savePath), "progress saved to disk");

            // Continue → Level 2 opens.
            Click(FindButton("ContinueButton"));
            yield return null;
            Assert.AreEqual(1, flow.CurrentLevelIndex);
            Assert.AreEqual(GameFlow.FlowScreen.Puzzle, flow.CurrentScreen);
            Assert.AreEqual("lvl_02", flow.PuzzleScreen.CurrentLevel.LevelId);

            // Back to Level Select; Level 2 is highlighted as the next level.
            Click(FindButton("BackButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.LevelSelect, flow.CurrentScreen);
            Assert.AreEqual(1, flow.LevelSelect.SelectedIndex);
            Assert.AreEqual(LevelDishState.Completed, flow.LevelSelect.Dishes[0].State);
            Assert.AreEqual(LevelDishState.Available, flow.LevelSelect.Dishes[1].State);

            // "Close and reopen the game": reload the scene with a brand-new GameFlow reading the same save.
            yield return LoadGame();
            var reopened = GameFlow.Active;
            Assert.AreNotSame(flow, reopened);
            Assert.IsTrue(reopened.Progression.IsCompleted(0));
            Assert.IsTrue(reopened.Progression.IsUnlocked(1));
            Assert.IsFalse(reopened.Progression.IsUnlocked(2));
            Assert.AreEqual(LevelDishState.Completed, reopened.LevelSelect.Dishes[0].State);
            Assert.AreEqual(LevelDishState.Available, reopened.LevelSelect.Dishes[1].State);
            Assert.AreEqual(LevelDishState.Locked, reopened.LevelSelect.Dishes[2].State);
            Assert.AreEqual(1, reopened.LevelSelect.SelectedIndex, "reopening selects the next level to play");
        }

        [UnityTest]
        public IEnumerator CompletedLevel_CanBeReplayedDirectly()
        {
            new JsonFileProgressStore(_savePath).Save(new ProgressData
            {
                highestUnlockedIndex = 1,
                completedLevelIds = new List<string> { "lvl_01" },
            });
            yield return LoadGame();
            var flow = GameFlow.Active;

            // Tap the completed dish, then tap it again to play (replay).
            flow.LevelSelect.Select(1);
            ClickDish(flow, 0);
            Assert.AreEqual(0, flow.LevelSelect.SelectedIndex);
            Assert.AreEqual("REPLAY", FindButton("PlayButton").GetComponentInChildren<Text>().text);
            ClickDish(flow, 0);
            yield return null;
            Assert.AreEqual(0, flow.CurrentLevelIndex, "replay opened level 1");

            yield return SolveCurrentLevel(flow, dragFirstPiece: false);
            yield return new WaitForSecondsRealtime(CompletionWait);

            Assert.IsTrue(flow.PuzzleScreen.Hud.IsSuccessVisible);
            Assert.AreEqual(1, flow.Progression.CompletedCount, "replay does not duplicate completion");
            Assert.AreEqual(1, flow.Progression.HighestUnlockedIndex, "replay does not skip ahead");
        }

        [UnityTest]
        public IEnumerator FinalLevel_ShowsExperimentComplete()
        {
            var done = Enumerable.Range(1, 9).Select(i => $"lvl_{i:D2}").ToList();
            new JsonFileProgressStore(_savePath).Save(new ProgressData { highestUnlockedIndex = 9, completedLevelIds = done });
            yield return LoadGame();
            var flow = GameFlow.Active;

            Assert.AreEqual(9, flow.LevelSelect.SelectedIndex, "level 10 is the current level");
            Assert.IsTrue(flow.TryOpenLevel(9));
            yield return null;

            yield return SolveCurrentLevel(flow, dragFirstPiece: false);
            yield return new WaitForSecondsRealtime(CompletionWait);
            Assert.IsTrue(flow.Progression.IsChapterComplete);
            Assert.AreEqual("FINISH", FindButton("ContinueButton").GetComponentInChildren<Text>().text);

            Click(FindButton("ContinueButton"));
            yield return null;
            Assert.AreEqual(GameFlow.FlowScreen.LevelSelect, flow.CurrentScreen);
            Assert.IsTrue(flow.LevelSelect.IsMilestoneVisible, "experiment complete milestone");

            Click(FindButton("ContinueButton")); // milestone button
            yield return null;
            Assert.IsFalse(flow.LevelSelect.IsMilestoneVisible);
            Assert.AreEqual(LevelDishState.Completed, flow.LevelSelect.Dishes[9].State);
        }

        [UnityTest]
        public IEnumerator Juice_And_PuzzleControls_StillWork()
        {
            new JsonFileProgressStore(_savePath).Save(new ProgressData
            {
                highestUnlockedIndex = 1,
                completedLevelIds = new List<string> { "lvl_01" },
            });
            yield return LoadGame();
            var flow = GameFlow.Active;
            Assert.IsTrue(flow.TryOpenLevel(1)); // Level 2: L, J, O
            yield return null;

            var controller = flow.PuzzleScreen.Controller;
            var session = controller.Session;
            IPieceInputListener input = controller;
            var views = Object.FindObjectsByType<PieceView>(FindObjectsSortMode.None).OrderBy(v => v.PieceId).ToArray();
            var board = Object.FindAnyObjectByType<BoardView>();
            var particles = Object.FindAnyObjectByType<UIParticles>();
            var solution = SolutionFor(flow);

            // Rotation by tap keeps the logical rotation exact.
            int before = session.GetPiece(0).Rotation;
            input.OnPieceTapped(0);
            Assert.AreEqual((before + 1) % 4, session.GetPiece(0).Rotation);
            input.OnPieceTapped(0); input.OnPieceTapped(0); input.OnPieceTapped(0);
            Assert.AreEqual(before, session.GetPiece(0).Rotation);

            // Valid drag → snap, particles, placement.
            var first = solution[0];
            RotateTo(input, session, first);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Drag(input, views[first.PieceIndex], board.PieceCenterWorld(first.Origin, session.GetPiece(first.PieceIndex).Shape));
            Assert.IsTrue(session.GetPiece(first.PieceIndex).IsPlaced, "valid drop placed");
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsTrue(particles.enabled, "placement particles playing");

            // Invalid drag onto the occupied spot → rejected, streak broken, piece returns to the tray.
            var other = solution[1];
            var otherView = views[other.PieceIndex];
            yield return Drag(input, otherView, board.PieceCenterWorld(first.Origin, session.GetPiece(other.PieceIndex).Shape));
            Assert.IsFalse(session.GetPiece(other.PieceIndex).IsPlaced, "invalid drop rejected");
            Assert.AreEqual(0, controller.Streak.Count, "invalid drop breaks the combo");
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsTrue(otherView.Rect.parent.name.StartsWith("Slot_"), "returned to tray");

            // Undo removes the last placement.
            Assert.IsTrue(session.Undo());
            Assert.IsFalse(session.GetPiece(first.PieceIndex).IsPlaced);
            yield return new WaitForSecondsRealtime(0.5f);

            // Two consecutive valid placements → combo label.
            RotateTo(input, session, solution[0]);
            Assert.IsTrue(session.TryPlace(solution[0].PieceIndex, solution[0].Origin));
            RotateTo(input, session, solution[1]);
            Assert.IsTrue(session.TryPlace(solution[1].PieceIndex, solution[1].Origin));
            yield return new WaitForSecondsRealtime(0.25f);
            var comboTexts = Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Where(t => t.name == "Combo" && t.enabled).Select(t => t.text).ToList();
            CollectionAssert.Contains(comboTexts, "GOOD!");

            // Restart clears everything, then finish the level for the completion sequence.
            Click(FindButton("RestartButton"));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(0, session.Grid.FilledCellCount);
            Assert.AreEqual(0, controller.Streak.Count);

            yield return SolveCurrentLevel(flow, dragFirstPiece: false);
            Assert.IsTrue(controller.IsInputLocked, "input locked during the completion sequence");
            yield return new WaitForSecondsRealtime(CompletionWait);
            Assert.IsFalse(controller.IsInputLocked);
            Assert.IsTrue(flow.PuzzleScreen.Hud.IsSuccessVisible);
        }

        // ---------------------------------------------------------------- helpers

        private static IEnumerator LoadGame()
        {
            var op = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;
            Assert.IsNotNull(GameFlow.Active, "GameFlow running");
            Assert.IsNotNull(GameFlow.Active.Progression, "GameFlow started");
        }

        private static List<PuzzleSolver.Placement> SolutionFor(GameFlow flow)
        {
            var level = flow.PuzzleScreen.CurrentLevel;
            var shapes = level.Pieces.Select(p => p.shape.CreateShape()).ToList();
            Assert.IsTrue(PuzzleSolver.TrySolve(level.Width, level.Height, level.BuildRequiredMask(), shapes, out var solution));
            return solution;
        }

        private static void RotateTo(IPieceInputListener input, PuzzleSession session, PuzzleSolver.Placement placement)
        {
            var piece = session.GetPiece(placement.PieceIndex);
            var target = piece.BaseShape.Rotated(placement.Rotation);
            for (int i = 0; i < 4 && !piece.Shape.Equals(target); i++) input.OnPieceTapped(piece.Id);
            Assert.IsTrue(piece.Shape.Equals(target));
        }

        /// <summary>Solves the open level: rotations via taps (input path), optional first piece via drag.</summary>
        private static IEnumerator SolveCurrentLevel(GameFlow flow, bool dragFirstPiece)
        {
            var controller = flow.PuzzleScreen.Controller;
            var session = controller.Session;
            IPieceInputListener input = controller;
            var solution = SolutionFor(flow);

            for (int i = 0; i < solution.Count; i++)
            {
                var placement = solution[i];
                RotateTo(input, session, placement);
                if (i == 0 && dragFirstPiece)
                {
                    yield return new WaitForSecondsRealtime(0.3f);
                    var view = Object.FindObjectsByType<PieceView>(FindObjectsSortMode.None).First(v => v.PieceId == placement.PieceIndex);
                    var board = Object.FindAnyObjectByType<BoardView>();
                    yield return Drag(input, view, board.PieceCenterWorld(placement.Origin, session.GetPiece(placement.PieceIndex).Shape));
                    Assert.IsTrue(session.GetPiece(placement.PieceIndex).IsPlaced, "drag placement");
                }
                else
                {
                    Assert.IsTrue(session.TryPlace(placement.PieceIndex, placement.Origin));
                }
            }
            Assert.IsTrue(session.IsSolved);
        }

        private static IEnumerator Drag(IPieceInputListener input, PieceView view, Vector3 target)
        {
            var start = view.Rect.position;
            input.OnPieceBeginDrag(view.PieceId, Gesture(start));
            for (int i = 1; i <= 8; i++)
            {
                input.OnPieceDrag(view.PieceId, Gesture(Vector3.Lerp(start, target, i / 8f)));
                yield return null;
            }
            input.OnPieceEndDrag(view.PieceId, Gesture(target));
            yield return null;
        }

        private static PieceGesture Gesture(Vector3 screen)
        {
            var e = new PointerEventData(EventSystem.current) { position = screen, pointerId = -1 };
            return new PieceGesture(e);
        }

        private static Button FindButton(string name)
        {
            var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == name && b.gameObject.activeInHierarchy);
            Assert.IsNotNull(button, $"button '{name}' visible");
            return button;
        }

        private static void Click(Button button)
        {
            Assert.IsTrue(button.interactable, $"{button.name} interactable");
            button.onClick.Invoke();
        }

        private static void ClickDish(GameFlow flow, int index)
        {
            var dish = flow.LevelSelect.Dishes[index];
            dish.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
        }
    }
}
