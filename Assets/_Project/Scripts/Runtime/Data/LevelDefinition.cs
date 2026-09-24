using System;
using System.Collections.Generic;
using CozyLab.Puzzle.Core;
using UnityEngine;

namespace CozyLab.Puzzle.Data
{
    [Serializable]
    public sealed class LevelPieceEntry
    {
        public PieceShapeDefinition shape;
        [Tooltip("Clockwise quarter turns applied when the level starts.")]
        [Range(0, 3)] public int startRotation;
        [Tooltip("Index into the theme's piece palette.")]
        [Min(0)] public int colorIndex;
    }

    /// <summary>
    /// Data for one puzzle: grid size, which cells must be filled, and the pieces supplied.
    /// Theme-agnostic: the same level can be shown as viruses, pasta, crystals...
    /// </summary>
    [CreateAssetMenu(menuName = "CozyLab/Puzzle/Level", fileName = "Level_New")]
    public sealed class LevelDefinition : ScriptableObject
    {
        /// <summary>Largest supported board side; keeps cells finger-sized on a phone.</summary>
        public const int MaxGridSize = 8;

        [SerializeField] private string levelId = "level_new";
        [SerializeField] private string title = "Sample 01";
        [Tooltip("Optional one-line hint shown under the title (e.g. tutorial levels).")]
        [SerializeField] private string hint = "";
        [SerializeField, Min(1)] private int width = 4;
        [SerializeField, Min(1)] private int height = 4;
        [Tooltip("Optional. Top row first. 'X' = required cell, '.' = not part of the board. Leave empty for a full rectangle.")]
        [SerializeField] private List<string> boardMask = new List<string>();
        [SerializeField] private List<LevelPieceEntry> pieces = new List<LevelPieceEntry>();

        public string LevelId => levelId;
        public string Title => title;
        public string Hint => hint;
        public int Width => width;
        public int Height => height;
        public IReadOnlyList<LevelPieceEntry> Pieces => pieces;

        /// <summary>Row-major mask (index = y * width + x), or null when every cell is required.</summary>
        public bool[] BuildRequiredMask()
        {
            if (boardMask == null || boardMask.Count == 0) return null;

            var mask = new bool[width * height];
            for (int y = 0; y < height; y++)
            {
                string row = y < boardMask.Count ? boardMask[y] ?? string.Empty : string.Empty;
                for (int x = 0; x < width; x++)
                {
                    mask[y * width + x] = x < row.Length && PolyominoShape.IsFilledChar(row[x]);
                }
            }
            return mask;
        }

        public PuzzleGrid CreateGrid() => new PuzzleGrid(width, height, BuildRequiredMask());

        public PuzzleSession CreateSession()
        {
            var setups = new List<PieceSetup>(pieces.Count);
            foreach (var entry in pieces) setups.Add(new PieceSetup(entry.shape.CreateShape(), entry.startRotation));
            return new PuzzleSession(CreateGrid(), setups);
        }

        /// <summary>Checks data consistency. Returns human readable problems (empty list = OK).</summary>
        public List<string> Validate(bool checkSolvable = true)
        {
            var problems = new List<string>();

            if (width > MaxGridSize || height > MaxGridSize)
                problems.Add($"Board is {width}x{height}; the maximum supported size is {MaxGridSize}x{MaxGridSize}.");
            if (string.IsNullOrWhiteSpace(levelId)) problems.Add("Level id is empty.");

            if (boardMask != null && boardMask.Count > 0)
            {
                if (boardMask.Count != height) problems.Add($"Board mask has {boardMask.Count} rows, expected {height}.");
                for (int y = 0; y < boardMask.Count; y++)
                {
                    int len = boardMask[y]?.Length ?? 0;
                    if (len != width) problems.Add($"Board mask row {y} has {len} columns, expected {width}.");
                }
            }

            if (pieces == null || pieces.Count == 0)
            {
                problems.Add("Level has no pieces.");
                return problems;
            }

            var shapes = new List<PolyominoShape>();
            for (int i = 0; i < pieces.Count; i++)
            {
                var entry = pieces[i];
                if (entry == null || entry.shape == null)
                {
                    problems.Add($"Piece {i} has no shape assigned.");
                    continue;
                }
                if (!entry.shape.IsValid(out var error))
                {
                    problems.Add($"Piece {i} ({entry.shape.name}): {error}");
                    continue;
                }
                shapes.Add(entry.shape.CreateShape());
            }
            if (problems.Count > 0) return problems;

            var grid = CreateGrid();
            if (grid.RequiredCellCount == 0)
            {
                problems.Add("Board has no required cells.");
                return problems;
            }
            if (!AreRequiredCellsConnected(grid)) problems.Add("Board cells are split into separate islands.");

            int area = 0;
            foreach (var s in shapes) area += s.Count;
            if (area != grid.RequiredCellCount)
                problems.Add($"Pieces cover {area} cells but the board has {grid.RequiredCellCount} required cells.");

            if (problems.Count == 0 && checkSolvable &&
                !PuzzleSolver.TrySolve(width, height, BuildRequiredMask(), shapes, out _))
            {
                problems.Add("No solution exists for this piece set.");
            }

            return problems;
        }

        private static bool AreRequiredCellsConnected(PuzzleGrid grid)
        {
            Vector2Int? start = null;
            foreach (var cell in grid.AllCells())
            {
                if (grid.IsRequired(cell))
                {
                    start = cell;
                    break;
                }
            }
            if (start == null) return false;

            var visited = new HashSet<Vector2Int> { start.Value };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start.Value);
            var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var d in directions)
                {
                    var next = cell + d;
                    if (grid.IsRequired(next) && visited.Add(next)) queue.Enqueue(next);
                }
            }
            return visited.Count == grid.RequiredCellCount;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Skip the solver here: OnValidate runs on every inspector tweak.
            foreach (var problem in Validate(checkSolvable: false))
                Debug.LogWarning($"[Level] '{name}': {problem}", this);
        }
#endif
    }
}
