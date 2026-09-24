using System.Collections.Generic;
using UnityEngine;

namespace CozyLab.Puzzle.Data
{
    /// <summary>
    /// Everything that makes a puzzle feel like "viruses" (or pasta, crystals...): colors, wording,
    /// and piece styling knobs. The engine and levels never reference a specific theme.
    /// </summary>
    [CreateAssetMenu(menuName = "CozyLab/Puzzle/Theme", fileName = "Theme_New")]
    public sealed class PuzzleTheme : ScriptableObject
    {
        [Header("Wording")]
        public string themeName = "Virus Samples";
        public string instructionText = "Fit every sample into the culture tray";
        public string successTitle = "CURE FOUND!";
        public string successSubtitle = "All samples contained. The antidote is stable.";
        public string playAgainLabel = "PLAY AGAIN";
        public string undoLabel = "UNDO";
        public string restartLabel = "RESTART";

        [Header("Screen")]
        public Color backgroundColor = new Color(0.906f, 0.945f, 0.937f);
        public Color backgroundAccentColor = new Color(0.839f, 0.910f, 0.902f);
        public Color textPrimaryColor = new Color(0.231f, 0.290f, 0.353f);
        public Color textSecondaryColor = new Color(0.447f, 0.522f, 0.580f);

        [Header("Board")]
        public Color boardShadowColor = new Color(0.180f, 0.290f, 0.310f, 0.22f);
        public Color boardRimColor = new Color(0.792f, 0.886f, 0.886f);
        public Color boardColor = new Color(0.976f, 0.988f, 0.988f);
        public Color wellColor = new Color(0.878f, 0.925f, 0.929f);
        public Color wellInnerShadowColor = new Color(0.769f, 0.843f, 0.851f);
        public Color validPreviewColor = new Color(0.361f, 0.839f, 0.655f, 0.75f);
        public Color invalidPreviewColor = new Color(1f, 0.47f, 0.47f, 0.6f);

        [Header("Pieces")]
        public List<Color> piecePalette = new List<Color>
        {
            new Color(1f, 0.541f, 0.604f),
            new Color(0.435f, 0.839f, 0.722f),
            new Color(0.663f, 0.612f, 0.941f),
            new Color(1f, 0.827f, 0.431f),
            new Color(0.471f, 0.761f, 0.941f),
            new Color(1f, 0.690f, 0.478f),
            new Color(0.604f, 0.804f, 0.471f),
            new Color(0.957f, 0.565f, 0.776f),
        };
        [Tooltip("Body size relative to one cell pitch.")]
        [Range(0.6f, 1f)] public float pieceBodyScale = 0.86f;
        [Tooltip("Corner radius relative to body size.")]
        [Range(0f, 0.5f)] public float pieceCornerRadius = 0.36f;
        [Tooltip("Thickness of the darker 'underside' relative to cell pitch.")]
        [Range(0f, 0.2f)] public float pieceDepth = 0.06f;
        [Range(0f, 1f)] public float pieceShadowAlpha = 0.22f;
        [Range(0f, 1f)] public float pieceHighlightAlpha = 0.5f;
        [Tooltip("Small organelle-like dots inside each cell. Set to 0 for plain pieces.")]
        [Range(0, 4)] public int pieceDetailDots = 2;

        [Header("UI")]
        public Color buttonColor = Color.white;
        public Color buttonTextColor = new Color(0.231f, 0.290f, 0.353f);
        public Color accentColor = new Color(0.353f, 0.722f, 0.690f);
        public Color overlayDimColor = new Color(0.141f, 0.200f, 0.251f, 0.3f);
        public Color cardColor = new Color(0.988f, 0.996f, 0.996f);

        [Header("Combos")]
        public string comboGoodLabel = "GOOD!";
        public string comboGreatLabel = "GREAT!";
        public string comboPerfectLabel = "PERFECT!";
        public Color comboColor = new Color(0.290f, 0.651f, 0.620f);
        public Color comboPerfectColor = new Color(0.949f, 0.573f, 0.365f);

        /// <summary>Label for a <see cref="Core.PlacementStreak"/> tier (1 = good, 2 = great, 3 = perfect).</summary>
        public string GetComboLabel(int tier)
        {
            switch (tier)
            {
                case 1: return comboGoodLabel;
                case 2: return comboGreatLabel;
                case 3: return comboPerfectLabel;
                default: return null;
            }
        }

        public Color GetPieceColor(int index)
        {
            if (piecePalette == null || piecePalette.Count == 0) return Color.white;
            return piecePalette[((index % piecePalette.Count) + piecePalette.Count) % piecePalette.Count];
        }
    }
}
