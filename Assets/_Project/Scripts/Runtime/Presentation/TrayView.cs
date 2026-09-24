using System.Collections.Generic;
using CozyLab.Puzzle.Core;
using CozyLab.Puzzle.Data;
using UnityEngine;

namespace CozyLab.Puzzle.Presentation
{
    /// <summary>
    /// The sample rack below the board: one slot per piece, pieces shown at a reduced scale.
    /// </summary>
    public sealed class TrayView : MonoBehaviour
    {
        private readonly List<RectTransform> _slots = new List<RectTransform>();

        /// <summary>Scale applied to pieces while they rest in the tray (1 = board size).</summary>
        public float PieceScale { get; private set; } = 0.5f;

        public void Build(IReadOnlyList<PieceState> pieces, float pitch, Vector2 traySize, PuzzleTheme theme,
            float maxPieceScale)
        {
            var root = (RectTransform)transform;
            root.sizeDelta = traySize;

            // Soft rack panel.
            UIFactory.CreateShadow("Shadow", root, traySize, 36f, new Color(theme.boardShadowColor.r, theme.boardShadowColor.g,
                theme.boardShadowColor.b, theme.boardShadowColor.a * 0.6f), new Vector2(0f, -14f));
            UIFactory.CreateRoundedImage("Panel", root, traySize, 72f,
                Color.Lerp(theme.backgroundColor, Color.white, 0.55f));

            int count = pieces.Count;
            int columns = count <= 2 ? Mathf.Max(count, 1) : count <= 4 ? 2 : count <= 9 ? 3 : 4;
            int rows = Mathf.CeilToInt(count / (float)columns);

            const float padding = 28f;
            var inner = new Vector2(traySize.x - padding * 2f, traySize.y - padding * 2f);
            var slotSize = new Vector2(inner.x / columns, inner.y / rows);

            // One uniform scale so every piece fits its slot in any rotation.
            int maxExtent = 1;
            foreach (var piece in pieces) maxExtent = Mathf.Max(maxExtent, Mathf.Max(piece.BaseShape.Width, piece.BaseShape.Height));
            float fit = Mathf.Min(slotSize.x, slotSize.y) * 0.84f / (maxExtent * pitch);
            PieceScale = Mathf.Min(maxPieceScale, fit);

            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                int inRow = Mathf.Min(columns, count - row * columns);
                // Centre partially filled rows.
                float rowOffset = (columns - inRow) * slotSize.x * 0.5f;

                var slot = UIFactory.CreateCentered($"Slot_{i}", root, slotSize, new Vector2(
                    -inner.x * 0.5f + rowOffset + (col + 0.5f) * slotSize.x,
                    inner.y * 0.5f - (row + 0.5f) * slotSize.y));

                // Faint circular "sample pad" under each piece.
                float pad = Mathf.Min(slotSize.x, slotSize.y) * 0.9f;
                UIFactory.CreateSpriteImage("Pad", slot, ProceduralSprites.SoftGlow, new Vector2(pad, pad),
                    new Color(theme.boardRimColor.r, theme.boardRimColor.g, theme.boardRimColor.b, 0.55f));

                _slots.Add(slot);
            }
        }

        public RectTransform GetSlot(int pieceId) => _slots[pieceId];
    }
}
