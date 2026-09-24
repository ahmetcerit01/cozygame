using System;
using System.Collections.Generic;
using UnityEngine;

namespace CozyLab.Puzzle.Core
{
    /// <summary>
    /// Immutable set of grid cells describing a piece.
    /// Coordinates: x = column (right), y = row (down). Cells are always normalized so the
    /// bounding box starts at (0,0), and sorted (row-major) so equal shapes compare equal.
    /// </summary>
    public sealed class PolyominoShape : IEquatable<PolyominoShape>
    {
        private readonly Vector2Int[] _cells;

        public IReadOnlyList<Vector2Int> Cells => _cells;
        public int Count => _cells.Length;
        public int Width { get; }
        public int Height { get; }

        public PolyominoShape(IEnumerable<Vector2Int> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));

            var unique = new HashSet<Vector2Int>(cells);
            if (unique.Count == 0) throw new ArgumentException("A shape needs at least one cell.", nameof(cells));

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var c in unique)
            {
                minX = Math.Min(minX, c.x);
                minY = Math.Min(minY, c.y);
                maxX = Math.Max(maxX, c.x);
                maxY = Math.Max(maxY, c.y);
            }

            _cells = new Vector2Int[unique.Count];
            int i = 0;
            foreach (var c in unique) _cells[i++] = new Vector2Int(c.x - minX, c.y - minY);
            Array.Sort(_cells, (a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

            Width = maxX - minX + 1;
            Height = maxY - minY + 1;
        }

        /// <summary>
        /// Parses rows such as { "X.", "X.", "XX" }. Any character other than '.', ' ', '0' or '_' is a filled cell.
        /// </summary>
        public static PolyominoShape FromRows(IReadOnlyList<string> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var cells = new List<Vector2Int>();
            for (int y = 0; y < rows.Count; y++)
            {
                string row = rows[y] ?? string.Empty;
                for (int x = 0; x < row.Length; x++)
                {
                    if (IsFilledChar(row[x])) cells.Add(new Vector2Int(x, y));
                }
            }
            return new PolyominoShape(cells);
        }

        public static bool IsFilledChar(char c) => c != '.' && c != ' ' && c != '0' && c != '_';

        /// <summary>Rotates 90 degrees clockwise as seen on screen (y points down).</summary>
        public PolyominoShape Rotated90()
        {
            var rotated = new Vector2Int[_cells.Length];
            for (int i = 0; i < _cells.Length; i++)
            {
                var c = _cells[i];
                rotated[i] = new Vector2Int(-c.y, c.x);
            }
            return new PolyominoShape(rotated);
        }

        /// <summary>Rotates clockwise by the given number of quarter turns (any integer, wraps).</summary>
        public PolyominoShape Rotated(int quarterTurns)
        {
            int turns = NormalizeRotation(quarterTurns);
            var shape = this;
            for (int i = 0; i < turns; i++) shape = shape.Rotated90();
            return shape;
        }

        public bool Contains(Vector2Int cell) => Array.IndexOf(_cells, cell) >= 0;

        public static int NormalizeRotation(int quarterTurns) => ((quarterTurns % 4) + 4) % 4;

        public bool Equals(PolyominoShape other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other is null || other._cells.Length != _cells.Length) return false;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] != other._cells[i]) return false;
            }
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as PolyominoShape);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (var c in _cells) hash = hash * 31 + c.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            for (int y = 0; y < Height; y++)
            {
                if (y > 0) sb.Append('/');
                for (int x = 0; x < Width; x++) sb.Append(Contains(new Vector2Int(x, y)) ? 'X' : '.');
            }
            return sb.ToString();
        }
    }
}
