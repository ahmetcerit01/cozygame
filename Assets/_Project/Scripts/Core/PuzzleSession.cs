using System;
using System.Collections.Generic;
using UnityEngine;

namespace CozyLab.Puzzle.Core
{
    /// <summary>Everything the engine needs to spawn one piece.</summary>
    public readonly struct PieceSetup
    {
        public readonly PolyominoShape Shape;
        public readonly int StartRotation;

        public PieceSetup(PolyominoShape shape, int startRotation)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            StartRotation = PolyominoShape.NormalizeRotation(startRotation);
        }
    }

    /// <summary>Runtime state of a single piece.</summary>
    public sealed class PieceState
    {
        public int Id { get; }
        public PolyominoShape BaseShape { get; }
        public int StartRotation { get; }
        public int Rotation { get; private set; }
        public PolyominoShape Shape { get; private set; }
        public bool IsPlaced { get; private set; }
        public Vector2Int Origin { get; private set; }

        internal PieceState(int id, PieceSetup setup)
        {
            Id = id;
            BaseShape = setup.Shape;
            StartRotation = setup.StartRotation;
            SetRotation(StartRotation);
        }

        internal void SetRotation(int rotation)
        {
            Rotation = PolyominoShape.NormalizeRotation(rotation);
            Shape = BaseShape.Rotated(Rotation);
        }

        internal void SetPlaced(Vector2Int origin)
        {
            IsPlaced = true;
            Origin = origin;
        }

        internal void SetUnplaced()
        {
            IsPlaced = false;
            Origin = default;
        }
    }

    /// <summary>
    /// One play-through of a level: grid, pieces, undo history, and win detection.
    /// Pure logic; views subscribe to the events.
    /// </summary>
    public sealed class PuzzleSession
    {
        private readonly List<PieceState> _pieces = new List<PieceState>();
        private readonly Stack<int> _placementHistory = new Stack<int>();

        public PuzzleGrid Grid { get; }
        public IReadOnlyList<PieceState> Pieces => _pieces;
        public bool IsSolved { get; private set; }
        public bool CanUndo => !IsSolved && _placementHistory.Count > 0;

        public event Action<PieceState> PiecePlaced;
        public event Action<PieceState> PieceRemoved;
        public event Action<PieceState> PieceRotated;
        public event Action Restarted;
        public event Action Solved;
        /// <summary>Raised after any state change (handy for refreshing UI such as the Undo button).</summary>
        public event Action Changed;

        public PuzzleSession(PuzzleGrid grid, IReadOnlyList<PieceSetup> pieces)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (pieces == null) throw new ArgumentNullException(nameof(pieces));
            for (int i = 0; i < pieces.Count; i++) _pieces.Add(new PieceState(i, pieces[i]));
        }

        public PieceState GetPiece(int id) => _pieces[id];

        public PlacementResult CheckPlacement(int pieceId, Vector2Int origin)
        {
            return Grid.CanPlace(_pieces[pieceId].Shape, origin);
        }

        public bool TryPlace(int pieceId, Vector2Int origin)
        {
            var piece = _pieces[pieceId];
            if (IsSolved || piece.IsPlaced) return false;
            if (Grid.CanPlace(piece.Shape, origin) != PlacementResult.Valid) return false;

            Grid.Place(pieceId, piece.Shape, origin);
            piece.SetPlaced(origin);
            _placementHistory.Push(pieceId);

            PiecePlaced?.Invoke(piece);
            EvaluateSolved();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Rotates an unplaced piece 90 degrees clockwise.</summary>
        public bool TryRotate(int pieceId)
        {
            var piece = _pieces[pieceId];
            if (IsSolved || piece.IsPlaced) return false;

            piece.SetRotation(piece.Rotation + 1);
            PieceRotated?.Invoke(piece);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes the most recently placed piece from the grid.</summary>
        public bool Undo()
        {
            if (!CanUndo) return false;

            var piece = _pieces[_placementHistory.Pop()];
            Grid.Remove(piece.Id);
            piece.SetUnplaced();

            PieceRemoved?.Invoke(piece);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Clears the grid and restores every piece to its starting rotation.</summary>
        public void Restart()
        {
            Grid.Clear();
            _placementHistory.Clear();
            IsSolved = false;
            foreach (var piece in _pieces)
            {
                piece.SetUnplaced();
                piece.SetRotation(piece.StartRotation);
            }

            Restarted?.Invoke();
            Changed?.Invoke();
        }

        private void EvaluateSolved()
        {
            if (IsSolved || !Grid.IsFull) return;
            foreach (var piece in _pieces)
            {
                if (!piece.IsPlaced) return;
            }
            IsSolved = true;
            Solved?.Invoke();
        }
    }
}
