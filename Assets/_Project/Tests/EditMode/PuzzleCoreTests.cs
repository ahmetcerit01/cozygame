using System.Collections.Generic;
using CozyLab.Puzzle.Core;
using CozyLab.Puzzle.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CozyLab.Puzzle.Tests
{
    public class PolyominoShapeTests
    {
        [Test]
        public void FromRows_NormalizesAndMeasures()
        {
            var l = PolyominoShape.FromRows(new[] { "..", "X.", "X.", "XX" });
            Assert.AreEqual(4, l.Count);
            Assert.AreEqual(2, l.Width);
            Assert.AreEqual(3, l.Height);
            Assert.IsTrue(l.Contains(new Vector2Int(0, 0)));
            Assert.IsTrue(l.Contains(new Vector2Int(1, 2)));
        }

        [Test]
        public void Rotate90_IsClockwiseOnScreen()
        {
            // X.      XXX
            // X.  ->  X..
            // XX
            var l = PolyominoShape.FromRows(new[] { "X.", "X.", "XX" });
            var expected = PolyominoShape.FromRows(new[] { "XXX", "X.." });
            Assert.AreEqual(expected, l.Rotated90());
        }

        [Test]
        public void FourRotations_ReturnToStart()
        {
            var t = PolyominoShape.FromRows(new[] { "XXX", ".X." });
            Assert.AreEqual(t, t.Rotated(4));
            Assert.AreEqual(t.Rotated(1), t.Rotated(-3));
        }

        [Test]
        public void Square_IsRotationInvariant()
        {
            var o = PolyominoShape.FromRows(new[] { "XX", "XX" });
            Assert.AreEqual(o, o.Rotated90());
        }
    }

    public class PuzzleGridTests
    {
        private static readonly PolyominoShape Domino = PolyominoShape.FromRows(new[] { "XX" });

        [Test]
        public void CanPlace_RejectsOutOfBounds()
        {
            var grid = new PuzzleGrid(3, 3);
            Assert.AreEqual(PlacementResult.Valid, grid.CanPlace(Domino, new Vector2Int(1, 0)));
            Assert.AreEqual(PlacementResult.OutOfBounds, grid.CanPlace(Domino, new Vector2Int(2, 0)));
            Assert.AreEqual(PlacementResult.OutOfBounds, grid.CanPlace(Domino, new Vector2Int(-1, 1)));
            Assert.AreEqual(PlacementResult.OutOfBounds, grid.CanPlace(Domino, new Vector2Int(0, 3)));
        }

        [Test]
        public void CanPlace_RejectsOverlap()
        {
            var grid = new PuzzleGrid(3, 3);
            grid.Place(0, Domino, new Vector2Int(0, 0));
            Assert.AreEqual(PlacementResult.Overlap, grid.CanPlace(Domino, new Vector2Int(1, 0)));
            Assert.AreEqual(PlacementResult.Valid, grid.CanPlace(Domino, new Vector2Int(1, 1)));
        }

        [Test]
        public void CanPlace_RejectsBlockedCells()
        {
            var mask = new[] { true, false, true, true };
            var grid = new PuzzleGrid(2, 2, mask);
            Assert.AreEqual(3, grid.RequiredCellCount);
            Assert.AreEqual(PlacementResult.BlockedCell, grid.CanPlace(Domino, new Vector2Int(0, 0)));
            Assert.AreEqual(PlacementResult.Valid, grid.CanPlace(Domino, new Vector2Int(0, 1)));
        }

        [Test]
        public void Remove_FreesCells()
        {
            var grid = new PuzzleGrid(2, 1);
            grid.Place(3, Domino, Vector2Int.zero);
            Assert.IsTrue(grid.IsFull);
            Assert.AreEqual(2, grid.Remove(3));
            Assert.IsFalse(grid.IsFull);
            Assert.AreEqual(PuzzleGrid.NoPiece, grid.GetOccupant(Vector2Int.zero));
        }
    }

    public class PuzzleSessionTests
    {
        // 2x2 board solved by two horizontal dominoes.
        private static PuzzleSession CreateTwoDominoSession(int startRotation = 0)
        {
            var domino = PolyominoShape.FromRows(new[] { "XX" });
            return new PuzzleSession(new PuzzleGrid(2, 2),
                new List<PieceSetup> { new PieceSetup(domino, startRotation), new PieceSetup(domino, startRotation) });
        }

        [Test]
        public void FillingBoard_RaisesSolvedOnce()
        {
            var session = CreateTwoDominoSession();
            int solved = 0;
            session.Solved += () => solved++;

            Assert.IsTrue(session.TryPlace(0, new Vector2Int(0, 0)));
            Assert.IsFalse(session.IsSolved);
            Assert.IsTrue(session.TryPlace(1, new Vector2Int(0, 1)));
            Assert.IsTrue(session.IsSolved);
            Assert.AreEqual(1, solved);
        }

        [Test]
        public void TryPlace_RejectsOverlapAndDoublePlacement()
        {
            var session = CreateTwoDominoSession();
            Assert.IsTrue(session.TryPlace(0, new Vector2Int(0, 0)));
            Assert.IsFalse(session.TryPlace(1, new Vector2Int(0, 0)), "overlap");
            Assert.IsFalse(session.TryPlace(0, new Vector2Int(0, 1)), "already placed");
            Assert.IsFalse(session.TryPlace(1, new Vector2Int(1, 1)), "out of bounds");
        }

        [Test]
        public void Undo_RemovesMostRecentPlacement()
        {
            var session = CreateTwoDominoSession();
            session.TryPlace(0, new Vector2Int(0, 1));

            PieceState removed = null;
            session.PieceRemoved += p => removed = p;
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(0, removed.Id);
            Assert.IsFalse(session.GetPiece(0).IsPlaced);
            Assert.AreEqual(0, session.Grid.FilledCellCount);
            Assert.IsFalse(session.Undo(), "nothing left to undo");
        }

        [Test]
        public void Undo_IsLastInFirstOut()
        {
            var domino = PolyominoShape.FromRows(new[] { "XX" });
            var session = new PuzzleSession(new PuzzleGrid(2, 3), new List<PieceSetup>
            {
                new PieceSetup(domino, 0), new PieceSetup(domino, 0), new PieceSetup(domino, 0),
            });
            session.TryPlace(2, new Vector2Int(0, 0));
            session.TryPlace(0, new Vector2Int(0, 1));

            session.Undo();
            Assert.IsFalse(session.GetPiece(0).IsPlaced);
            Assert.IsTrue(session.GetPiece(2).IsPlaced);
            session.Undo();
            Assert.IsFalse(session.GetPiece(2).IsPlaced);
        }

        [Test]
        public void Rotate_OnlyAffectsUnplacedPieces()
        {
            var session = CreateTwoDominoSession();
            Assert.IsTrue(session.TryRotate(0));
            Assert.AreEqual(1, session.GetPiece(0).Rotation);
            Assert.AreEqual(1, session.GetPiece(0).Shape.Width);
            Assert.AreEqual(2, session.GetPiece(0).Shape.Height);

            session.TryPlace(1, Vector2Int.zero);
            Assert.IsFalse(session.TryRotate(1));
        }

        [Test]
        public void Restart_RestoresStartRotationAndClearsGrid()
        {
            var session = CreateTwoDominoSession(startRotation: 1);
            session.TryRotate(0);
            session.TryPlace(0, Vector2Int.zero);
            session.Restart();

            Assert.AreEqual(0, session.Grid.FilledCellCount);
            Assert.IsFalse(session.CanUndo);
            Assert.AreEqual(1, session.GetPiece(0).Rotation);
            Assert.IsFalse(session.GetPiece(0).IsPlaced);
        }

        [Test]
        public void SolvedSession_BlocksUndoAndPlacement()
        {
            var session = CreateTwoDominoSession();
            session.TryPlace(0, new Vector2Int(0, 0));
            session.TryPlace(1, new Vector2Int(0, 1));
            Assert.IsFalse(session.CanUndo);
            Assert.IsFalse(session.Undo());
            Assert.IsFalse(session.TryRotate(0));
        }
    }

    public class PuzzleSolverTests
    {
        [Test]
        public void Solves_4x4_WithIOLJ()
        {
            var shapes = new List<PolyominoShape>
            {
                PolyominoShape.FromRows(new[] { "XXXX" }),
                PolyominoShape.FromRows(new[] { "XX", "XX" }),
                PolyominoShape.FromRows(new[] { "X.", "X.", "XX" }),
                PolyominoShape.FromRows(new[] { ".X", ".X", "XX" }),
            };
            Assert.IsTrue(PuzzleSolver.TrySolve(4, 4, null, shapes, out var solution));
            Assert.AreEqual(4, solution.Count);
        }

        [Test]
        public void Rejects_ImpossibleSet()
        {
            // Checkerboard argument: a T always covers 3 cells of one color and 1 of the other,
            // each O covers 2:2, but a 4x4 board is 8:8, so no tiling exists.
            var shapes = new List<PolyominoShape>
            {
                PolyominoShape.FromRows(new[] { "XXX", ".X." }),
                PolyominoShape.FromRows(new[] { "XX", "XX" }),
                PolyominoShape.FromRows(new[] { "XX", "XX" }),
                PolyominoShape.FromRows(new[] { "XX", "XX" }),
            };
            Assert.IsFalse(PuzzleSolver.TrySolve(4, 4, null, shapes, out _));
        }
    }

    public class LevelAssetTests
    {
        [Test]
        public void AllLevelAssets_AreValidAndSolvable()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(LevelDefinition));
            Assert.IsNotEmpty(guids, "Expected at least one LevelDefinition asset.");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                var problems = level.Validate(checkSolvable: true);
                Assert.IsEmpty(problems, $"{path}: {string.Join("; ", problems)}");
            }
        }
    }
}
