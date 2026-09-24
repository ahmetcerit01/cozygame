using System.Collections.Generic;
using UnityEngine;

namespace CozyLab.Puzzle.Core
{
    /// <summary>
    /// Backtracking solver used to validate level data (editor checks and tests).
    /// Always fills the first empty required cell (row-major), so each solution is found once per
    /// piece assignment; identical pieces are only tried in index order to avoid duplicate permutations.
    /// Fine for the small boards this game uses; not meant for runtime hints on huge grids.
    /// </summary>
    public static class PuzzleSolver
    {
        public readonly struct Placement
        {
            public readonly int PieceIndex;
            public readonly int Rotation;
            public readonly Vector2Int Origin;

            public Placement(int pieceIndex, int rotation, Vector2Int origin)
            {
                PieceIndex = pieceIndex;
                Rotation = rotation;
                Origin = origin;
            }
        }

        public static bool TrySolve(int width, int height, bool[] requiredMask, IReadOnlyList<PolyominoShape> pieces,
            out List<Placement> solution)
        {
            var run = new Run(width, height, requiredMask, pieces, limit: 1);
            run.Execute();
            solution = run.FirstSolution ?? new List<Placement>();
            return run.Found > 0;
        }

        /// <summary>
        /// Counts distinct solutions (identical pieces are interchangeable), stopping at <paramref name="limit"/>.
        /// Board symmetries are counted separately (a mirrored layout is a different solution).
        /// </summary>
        public static int CountSolutions(int width, int height, bool[] requiredMask, IReadOnlyList<PolyominoShape> pieces,
            int limit)
        {
            var run = new Run(width, height, requiredMask, pieces, limit);
            run.Execute();
            return run.Found;
        }

        private sealed class Run
        {
            private readonly PuzzleGrid _grid;
            private readonly IReadOnlyList<PolyominoShape> _pieces;
            private readonly List<List<(int rotation, PolyominoShape shape)>> _rotations = new List<List<(int, PolyominoShape)>>();
            private readonly int[] _identicalTo;
            private readonly bool[] _used;
            private readonly List<Placement> _current = new List<Placement>();
            private readonly int _limit;
            private readonly bool _areaMatches;

            public int Found { get; private set; }
            public List<Placement> FirstSolution { get; private set; }

            public Run(int width, int height, bool[] requiredMask, IReadOnlyList<PolyominoShape> pieces, int limit)
            {
                _grid = new PuzzleGrid(width, height, requiredMask);
                _pieces = pieces;
                _limit = Mathf.Max(1, limit);
                _used = new bool[pieces.Count];
                _identicalTo = new int[pieces.Count];

                int area = 0;
                for (int i = 0; i < pieces.Count; i++)
                {
                    area += pieces[i].Count;

                    var unique = new List<(int, PolyominoShape)>();
                    for (int r = 0; r < 4; r++)
                    {
                        var s = pieces[i].Rotated(r);
                        bool seen = false;
                        foreach (var (_, existing) in unique) seen |= existing.Equals(s);
                        if (!seen) unique.Add((r, s));
                    }
                    _rotations.Add(unique);

                    // Pieces are interchangeable if one is a rotation of the other.
                    _identicalTo[i] = i;
                    for (int j = 0; j < i; j++)
                    {
                        bool same = false;
                        foreach (var (_, s) in _rotations[j]) same |= s.Equals(pieces[i]);
                        if (same)
                        {
                            _identicalTo[i] = _identicalTo[j];
                            break;
                        }
                    }
                }
                _areaMatches = area == _grid.RequiredCellCount;
            }

            public void Execute()
            {
                if (_areaMatches) Search();
            }

            private void Search()
            {
                if (Found >= _limit) return;

                // Find the first empty required cell (row-major); some piece must cover it.
                Vector2Int? target = null;
                foreach (var cell in _grid.AllCells())
                {
                    if (_grid.IsRequired(cell) && _grid.GetOccupant(cell) == PuzzleGrid.NoPiece)
                    {
                        target = cell;
                        break;
                    }
                }
                if (target == null)
                {
                    Found++;
                    if (FirstSolution == null) FirstSolution = new List<Placement>(_current);
                    return;
                }

                for (int i = 0; i < _pieces.Count; i++)
                {
                    if (_used[i] || !IsNextOfItsKind(i)) continue;
                    foreach (var (rotation, shape) in _rotations[i])
                    {
                        // The shape's first cell (row-major) must land on the target cell.
                        var origin = target.Value - shape.Cells[0];
                        if (_grid.CanPlace(shape, origin) != PlacementResult.Valid) continue;

                        _grid.Place(i, shape, origin);
                        _used[i] = true;
                        _current.Add(new Placement(i, rotation, origin));

                        Search();

                        _current.RemoveAt(_current.Count - 1);
                        _used[i] = false;
                        _grid.Remove(i);
                        if (Found >= _limit) return;
                    }
                }
            }

            /// <summary>Among identical pieces, only the lowest-index unused one is tried.</summary>
            private bool IsNextOfItsKind(int index)
            {
                for (int j = 0; j < index; j++)
                {
                    if (!_used[j] && _identicalTo[j] == _identicalTo[index]) return false;
                }
                return true;
            }
        }
    }
}
