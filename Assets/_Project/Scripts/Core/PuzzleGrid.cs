using System;
using System.Collections.Generic;
using UnityEngine;

namespace CozyLab.Puzzle.Core
{
    public enum PlacementResult
    {
        Valid,
        OutOfBounds,
        BlockedCell,
        Overlap,
    }

    /// <summary>
    /// Rectangular grid where each cell is either not part of the puzzle (blocked) or a
    /// required cell that can hold exactly one piece. Knows nothing about themes or visuals.
    /// </summary>
    public sealed class PuzzleGrid
    {
        public const int NoPiece = -1;

        private readonly bool[] _required;
        private readonly int[] _occupant;

        public int Width { get; }
        public int Height { get; }
        public int RequiredCellCount { get; }
        public int FilledCellCount { get; private set; }

        /// <param name="requiredMask">Row-major (index = y * width + x). Null means every cell is required.</param>
        public PuzzleGrid(int width, int height, bool[] requiredMask = null)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Grid size must be positive.");
            if (requiredMask != null && requiredMask.Length != width * height)
                throw new ArgumentException("Mask length must equal width * height.", nameof(requiredMask));

            Width = width;
            Height = height;
            _required = new bool[width * height];
            _occupant = new int[width * height];

            int required = 0;
            for (int i = 0; i < _required.Length; i++)
            {
                _required[i] = requiredMask == null || requiredMask[i];
                _occupant[i] = NoPiece;
                if (_required[i]) required++;
            }
            RequiredCellCount = required;
        }

        public bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;

        public bool IsRequired(Vector2Int cell) => InBounds(cell) && _required[Index(cell)];

        public int GetOccupant(Vector2Int cell) => InBounds(cell) ? _occupant[Index(cell)] : NoPiece;

        public bool IsFull => FilledCellCount == RequiredCellCount;

        public PlacementResult CanPlace(PolyominoShape shape, Vector2Int origin)
        {
            if (shape == null) throw new ArgumentNullException(nameof(shape));

            // Report the most "fundamental" problem first so the UI can explain it consistently.
            foreach (var c in shape.Cells)
            {
                if (!InBounds(origin + c)) return PlacementResult.OutOfBounds;
            }
            foreach (var c in shape.Cells)
            {
                if (!_required[Index(origin + c)]) return PlacementResult.BlockedCell;
            }
            foreach (var c in shape.Cells)
            {
                if (_occupant[Index(origin + c)] != NoPiece) return PlacementResult.Overlap;
            }
            return PlacementResult.Valid;
        }

        public void Place(int pieceId, PolyominoShape shape, Vector2Int origin)
        {
            if (pieceId < 0) throw new ArgumentOutOfRangeException(nameof(pieceId));
            var result = CanPlace(shape, origin);
            if (result != PlacementResult.Valid)
                throw new InvalidOperationException($"Cannot place piece {pieceId} at {origin}: {result}.");

            foreach (var c in shape.Cells) _occupant[Index(origin + c)] = pieceId;
            FilledCellCount += shape.Count;
        }

        /// <summary>Removes every cell owned by the piece. Returns the number of cells freed.</summary>
        public int Remove(int pieceId)
        {
            int freed = 0;
            for (int i = 0; i < _occupant.Length; i++)
            {
                if (_occupant[i] != pieceId) continue;
                _occupant[i] = NoPiece;
                freed++;
            }
            FilledCellCount -= freed;
            return freed;
        }

        public void Clear()
        {
            for (int i = 0; i < _occupant.Length; i++) _occupant[i] = NoPiece;
            FilledCellCount = 0;
        }

        public IEnumerable<Vector2Int> AllCells()
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                yield return new Vector2Int(x, y);
        }

        private int Index(Vector2Int cell) => cell.y * Width + cell.x;
    }
}
