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
            float maxPieceScale, bool drawShell = true)
        {
            var root = (RectTransform)transform;
            root.sizeDelta = traySize;

            if (drawShell) BuildShell(root, traySize, theme);

            BuildSlots(pieces, pitch, traySize, theme, maxPieceScale, root);
        }

        private static void BuildShell(RectTransform root, Vector2 traySize, PuzzleTheme theme)
        {
            // Physical sample-prep tray: a shadow, a slightly deeper frame (its "rim"), and a lighter inset
            // bed, echoing the dish's rim/body language so both surfaces read as the same tactile material.
            UIFactory.CreateShadow("Shadow", root, traySize, 36f, new Color(theme.boardShadowColor.r, theme.boardShadowColor.g,
                theme.boardShadowColor.b, theme.boardShadowColor.a * 0.6f), new Vector2(0f, -14f));
            UIFactory.CreateRoundedImage("Frame", root, traySize, 72f, theme.boardRimColor);

            float trayRim = Mathf.Min(traySize.x, traySize.y) * 0.035f;
            var bedSize = traySize - new Vector2(trayRim, trayRim) * 2f;
            UIFactory.CreateRoundedImage("Bed", root, bedSize, 72f - trayRim,
                Color.Lerp(theme.backgroundColor, Color.white, 0.55f));

            var sheen = UIFactory.CreateRoundedImage("Sheen", root, new Vector2(bedSize.x * 0.55f, trayRim * 1.1f),
                trayRim * 0.5f, new Color(1f, 1f, 1f, 0.22f), new Vector2(-bedSize.x * 0.1f, bedSize.y * 0.5f - trayRim * 0.6f));
            sheen.raycastTarget = false;
        }

        private void BuildSlots(IReadOnlyList<PieceState> pieces, float pitch, Vector2 traySize,
            PuzzleTheme theme, float maxPieceScale, RectTransform root)
        {
            int count = pieces.Count;
            int columns = count <= 2 ? Mathf.Max(count, 1) : count <= 4 ? 2 : count <= 9 ? 3 : 4;
            int rows = Mathf.CeilToInt(count / (float)columns);

            const float padding = 28f;
            var inner = new Vector2(traySize.x - padding * 2f, traySize.y - padding * 2f);
            var slotSize = new Vector2(inner.x / columns, inner.y / rows);

            // One uniform scale so every piece fits its slot in any rotation.
            int maxExtent = 1;
            foreach (var piece in pieces) maxExtent = Mathf.Max(maxExtent, Mathf.Max(piece.BaseShape.Width, piece.BaseShape.Height));
            float fit = Mathf.Min(slotSize.x, slotSize.y) * 0.9f / (maxExtent * pitch);
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

                // The authored cream bed is the resting surface. A faint contact pool avoids
                // adding another pale square tile under every sample.
                float pad = Mathf.Min(slotSize.x, slotSize.y) * 0.78f;
                UIFactory.CreateSpriteImage($"Slot_{i}_Contact", slot, ProceduralSprites.SoftGlow,
                    new Vector2(pad, pad * 0.65f), new Color(0.32f, 0.27f, 0.17f, 0.12f),
                    new Vector2(0f, -pad * 0.08f));

                _slots.Add(slot);
            }
        }

        public RectTransform GetSlot(int pieceId) => _slots[pieceId];
    }
}
