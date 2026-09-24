using System.Collections.Generic;
using CozyLab.Puzzle.Core;
using UnityEngine;

namespace CozyLab.Puzzle.Data
{
    /// <summary>
    /// Reusable piece shape. Authored as text rows, top row first:
    /// 'X' (or any non-'.' character) = filled, '.' = empty.
    /// </summary>
    [CreateAssetMenu(menuName = "CozyLab/Puzzle/Piece Shape", fileName = "Shape_New")]
    public sealed class PieceShapeDefinition : ScriptableObject
    {
        [SerializeField] private string shapeId = "new_shape";
        [SerializeField] private string displayName = "New Shape";
        [Tooltip("Top row first. 'X' = filled cell, '.' = empty.")]
        [SerializeField] private List<string> rows = new List<string> { "XX", "X." };

        public string ShapeId => shapeId;
        public string DisplayName => displayName;
        public IReadOnlyList<string> Rows => rows;

        public PolyominoShape CreateShape() => PolyominoShape.FromRows(rows);

        public bool IsValid(out string error)
        {
            error = null;
            if (rows == null || rows.Count == 0)
            {
                error = "Shape has no rows.";
                return false;
            }
            foreach (var row in rows)
            {
                if (string.IsNullOrEmpty(row)) continue;
                foreach (char c in row)
                {
                    if (PolyominoShape.IsFilledChar(c)) return true;
                }
            }
            error = "Shape has no filled cells.";
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!IsValid(out var error)) Debug.LogWarning($"[PieceShape] '{name}': {error}", this);
        }
#endif
    }
}
