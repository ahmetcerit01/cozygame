using System.Collections.Generic;
using CozyLab.Puzzle.Core;
using CozyLab.Puzzle.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CozyLab.Puzzle.Tests
{
    public class LevelCatalogTests
    {
        private const string CatalogPath = "Assets/_Project/Data/Levels/Experiment01.asset";

        private static LevelCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, CatalogPath);
            return catalog;
        }

        private static List<PolyominoShape> Shapes(LevelDefinition level, bool startRotation)
        {
            var list = new List<PolyominoShape>();
            foreach (var entry in level.Pieces)
            {
                var shape = entry.shape.CreateShape();
                list.Add(startRotation ? shape.Rotated(entry.startRotation) : shape);
            }
            return list;
        }

        [Test]
        public void Catalog_HasTenValidLevelsWithUniqueIds()
        {
            var catalog = LoadCatalog();
            Assert.AreEqual(10, catalog.Count);
            var problems = catalog.Validate(checkSolvable: false);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void EveryCatalogLevel_IsSolvable([NUnit.Framework.Range(0, 9)] int index)
        {
            var level = LoadCatalog().GetLevel(index);
            var problems = level.Validate(checkSolvable: true);
            Assert.IsEmpty(problems, $"{level.name}: {string.Join("; ", problems)}");

            Assert.IsTrue(PuzzleSolver.TrySolve(level.Width, level.Height, level.BuildRequiredMask(), Shapes(level, false),
                out var solution));
            Assert.AreEqual(level.Pieces.Count, solution.Count);

            // Replaying the solver's answer through a real session must solve the puzzle.
            var session = level.CreateSession();
            foreach (var placement in solution)
            {
                var piece = session.GetPiece(placement.PieceIndex);
                var target = piece.BaseShape.Rotated(placement.Rotation);
                for (int i = 0; i < 4 && !piece.Shape.Equals(target); i++) session.TryRotate(piece.Id);
                Assert.IsTrue(session.TryPlace(piece.Id, placement.Origin), $"{level.name} piece {piece.Id}");
            }
            Assert.IsTrue(session.IsSolved, level.name);
        }

        [Test]
        public void TutorialLevelNeedsNoRotation_LaterLevelsDo()
        {
            var catalog = LoadCatalog();
            for (int i = 0; i < catalog.Count; i++)
            {
                var level = catalog.GetLevel(i);
                bool solvableAsDealt = CanSolveWithoutRotation(level);
                if (i == 0) Assert.IsTrue(solvableAsDealt, "Level 1 teaches dragging only.");
                else Assert.IsFalse(solvableAsDealt, $"{level.name} should require at least one rotation.");
            }
        }

        [Test]
        public void Catalog_UsesVariedBoards()
        {
            var catalog = LoadCatalog();
            var sizes = new HashSet<Vector2Int>();
            int irregular = 0;
            int maxPieces = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                var level = catalog.GetLevel(i);
                sizes.Add(new Vector2Int(level.Width, level.Height));
                if (level.BuildRequiredMask() != null) irregular++;
                maxPieces = Mathf.Max(maxPieces, level.Pieces.Count);
            }
            Assert.GreaterOrEqual(sizes.Count, 5, "at least five different board sizes");
            Assert.GreaterOrEqual(irregular, 5, "at least five irregular silhouettes");
            Assert.AreEqual(maxPieces, catalog.GetLevel(catalog.Count - 1).Pieces.Count, "finale has the most pieces");
            Assert.AreEqual(1, Solutions(catalog.GetLevel(catalog.Count - 1)), "finale has a unique solution");
        }

        [Test]
        public void Difficulty_GrowsAcrossTheChapter()
        {
            var catalog = LoadCatalog();
            var first = catalog.GetLevel(0);
            var last = catalog.GetLevel(catalog.Count - 1);
            Assert.Less(first.Pieces.Count, last.Pieces.Count);
            Assert.Less(first.CreateGrid().RequiredCellCount, last.CreateGrid().RequiredCellCount);

            // Piece count never drops by more than one between consecutive levels.
            for (int i = 1; i < catalog.Count; i++)
                Assert.GreaterOrEqual(catalog.GetLevel(i).Pieces.Count, catalog.GetLevel(i - 1).Pieces.Count - 1, $"level {i + 1}");
        }

        private static int Solutions(LevelDefinition level) =>
            PuzzleSolver.CountSolutions(level.Width, level.Height, level.BuildRequiredMask(), Shapes(level, false), 1000);

        private static bool CanSolveWithoutRotation(LevelDefinition level)
        {
            var grid = level.CreateGrid();
            var shapes = Shapes(level, startRotation: true);
            return Search(grid, shapes, new bool[shapes.Count]);
        }

        private static bool Search(PuzzleGrid grid, List<PolyominoShape> shapes, bool[] used)
        {
            Vector2Int? target = null;
            foreach (var c in grid.AllCells())
            {
                if (grid.IsRequired(c) && grid.GetOccupant(c) == PuzzleGrid.NoPiece)
                {
                    target = c;
                    break;
                }
            }
            if (target == null) return true;
            for (int i = 0; i < shapes.Count; i++)
            {
                if (used[i]) continue;
                var origin = target.Value - shapes[i].Cells[0];
                if (grid.CanPlace(shapes[i], origin) != PlacementResult.Valid) continue;
                grid.Place(i, shapes[i], origin);
                used[i] = true;
                if (Search(grid, shapes, used)) return true;
                used[i] = false;
                grid.Remove(i);
            }
            return false;
        }
    }

    public class LevelValidationTests
    {
        private LevelDefinition _level;
        private PieceShapeDefinition _domino;

        [SetUp]
        public void SetUp()
        {
            _domino = ScriptableObject.CreateInstance<PieceShapeDefinition>();
            var so = new SerializedObject(_domino);
            var rows = so.FindProperty("rows");
            rows.arraySize = 1;
            rows.GetArrayElementAtIndex(0).stringValue = "XX";
            so.ApplyModifiedPropertiesWithoutUndo();

            _level = ScriptableObject.CreateInstance<LevelDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_level);
            Object.DestroyImmediate(_domino);
        }

        private void Configure(int width, int height, string[] mask, int dominoes, string id = "test")
        {
            var so = new SerializedObject(_level);
            so.FindProperty("levelId").stringValue = id;
            so.FindProperty("width").intValue = width;
            so.FindProperty("height").intValue = height;
            var maskProp = so.FindProperty("boardMask");
            maskProp.arraySize = mask?.Length ?? 0;
            for (int i = 0; i < maskProp.arraySize; i++) maskProp.GetArrayElementAtIndex(i).stringValue = mask[i];
            var pieces = so.FindProperty("pieces");
            pieces.arraySize = dominoes;
            for (int i = 0; i < dominoes; i++)
            {
                var entry = pieces.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("shape").objectReferenceValue = _domino;
                entry.FindPropertyRelative("startRotation").intValue = 0;
                entry.FindPropertyRelative("colorIndex").intValue = i;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private List<string> Problems() => _level.Validate(checkSolvable: true);

        [Test]
        public void ValidBoard_HasNoProblems()
        {
            Configure(2, 2, null, 2);
            Assert.IsEmpty(Problems());
        }

        [Test]
        public void MaskWithWrongRowCount_IsDetected()
        {
            Configure(2, 2, new[] { "XX" }, 1);
            Assert.That(Problems(), Has.Some.Contains("rows"));
        }

        [Test]
        public void MaskWithWrongRowLength_IsDetected()
        {
            Configure(2, 2, new[] { "XX", "XXX" }, 2);
            Assert.That(Problems(), Has.Some.Contains("columns"));
        }

        [Test]
        public void AreaMismatch_IsDetected()
        {
            Configure(3, 2, null, 2);
            Assert.That(Problems(), Has.Some.Contains("cover"));
        }

        [Test]
        public void EmptyBoard_IsDetected()
        {
            Configure(2, 2, new[] { "..", ".." }, 1);
            Assert.That(Problems(), Has.Some.Contains("no required cells"));
        }

        [Test]
        public void DisconnectedBoard_IsDetected()
        {
            Configure(5, 1, new[] { "XX.XX" }, 2);
            Assert.That(Problems(), Has.Some.Contains("islands"));
        }

        [Test]
        public void OversizedBoard_IsDetected()
        {
            Configure(LevelDefinition.MaxGridSize + 1, 2, null, LevelDefinition.MaxGridSize + 1);
            Assert.That(Problems(), Has.Some.Contains("maximum"));
        }

        [Test]
        public void UnsolvableBoard_IsDetected()
        {
            // A T-shaped board (4 cells) can never be tiled by two dominoes.
            Configure(3, 2, new[] { "XXX", ".X." }, 2);
            Assert.That(Problems(), Has.Some.Contains("No solution"));
        }

        [Test]
        public void MissingPieceShape_IsDetected()
        {
            Configure(2, 1, null, 1);
            var so = new SerializedObject(_level);
            so.FindProperty("pieces").GetArrayElementAtIndex(0).FindPropertyRelative("shape").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Problems(), Has.Some.Contains("no shape"));
        }
    }

    public class SolverCountingTests
    {
        [Test]
        public void IdenticalPieces_AreNotCountedTwice()
        {
            var domino = PolyominoShape.FromRows(new[] { "XX" });
            // 2x2 board: two horizontal or two vertical dominoes = 2 distinct tilings.
            Assert.AreEqual(2, PuzzleSolver.CountSolutions(2, 2, null, new List<PolyominoShape> { domino, domino }, 100));
        }

        [Test]
        public void Counting_StopsAtLimit()
        {
            var mono = PolyominoShape.FromRows(new[] { "X" });
            var domino = PolyominoShape.FromRows(new[] { "XX" });
            var pieces = new List<PolyominoShape> { domino, domino, domino, domino, domino, domino, domino, domino };
            Assert.AreEqual(5, PuzzleSolver.CountSolutions(4, 4, null, pieces, 5));
            Assert.AreEqual(0, PuzzleSolver.CountSolutions(1, 1, null, new List<PolyominoShape> { domino }, 5));
            Assert.AreEqual(1, PuzzleSolver.CountSolutions(1, 1, null, new List<PolyominoShape> { mono }, 5));
        }

        [Test]
        public void MaskedBoards_AreRespected()
        {
            var v3 = PolyominoShape.FromRows(new[] { "X.", "XX" });
            var mask = new[] { true, false, true, true }; // 2x2 with top-right missing
            Assert.IsTrue(PuzzleSolver.TrySolve(2, 2, mask, new List<PolyominoShape> { v3 }, out var solution));
            Assert.AreEqual(1, solution.Count);
        }
    }
}
