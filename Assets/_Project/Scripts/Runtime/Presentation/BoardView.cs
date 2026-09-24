using System.Collections.Generic;
using CozyLab.Puzzle.Core;
using CozyLab.Puzzle.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.Presentation
{
    /// <summary>
    /// Draws the board (a stylized culture tray) and converts between world space and grid cells.
    /// Also shows placement previews and small feedback effects. No game rules live here.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private PuzzleGrid _grid;
        private PuzzleTheme _theme;
        private RectTransform _gridRoot;
        private RectTransform _pieceLayer;
        private Image[] _previewCells;
        private Image[] _flashCells;
        private Coroutine _pulseRoutine;
        private readonly Dictionary<int, Coroutine> _flashRoutines = new Dictionary<int, Coroutine>();
        private readonly List<Image> _activePreview = new List<Image>();
        private Color _previewColor;
        private bool _previewValid;

        public float Pitch { get; private set; }
        public float CellSize { get; private set; }
        public RectTransform PieceLayer => _pieceLayer;
        public RectTransform GridRoot => _gridRoot;

        public void Build(PuzzleGrid grid, PuzzleTheme theme, float boardSize, float gridPadding)
        {
            _grid = grid;
            _theme = theme;

            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(boardSize, boardSize);

            float gridSpan = boardSize - gridPadding * 2f;
            Pitch = gridSpan / Mathf.Max(grid.Width, grid.Height);
            CellSize = Pitch * 0.9f;

            float outerRadius = boardSize * 0.14f;
            float rim = boardSize * 0.035f;

            // Tray: shadow, glass rim, inner body.
            var shadow = UIFactory.CreateShadow("Shadow", root, new Vector2(boardSize, boardSize), 48f, theme.boardShadowColor,
                new Vector2(0f, -22f));
            shadow.transform.SetAsFirstSibling();
            UIFactory.CreateRoundedImage("Rim", root, new Vector2(boardSize, boardSize), outerRadius, theme.boardRimColor);
            UIFactory.CreateRoundedImage("Body", root, new Vector2(boardSize - rim * 2f, boardSize - rim * 2f),
                outerRadius - rim, theme.boardColor);

            // Subtle glass highlight along the top of the rim.
            var shine = UIFactory.CreateRoundedImage("Shine", root, new Vector2(boardSize * 0.55f, rim * 0.55f), rim * 0.3f,
                new Color(1f, 1f, 1f, 0.55f), new Vector2(-boardSize * 0.12f, boardSize * 0.5f - rim * 0.5f));
            shine.raycastTarget = false;

            _gridRoot = UIFactory.CreateCentered("Grid", root,
                new Vector2(grid.Width * Pitch, grid.Height * Pitch));

            var wells = UIFactory.CreateStretched("Wells", _gridRoot);
            _pieceLayer = UIFactory.CreateStretched("Pieces", _gridRoot);
            var flashLayer = UIFactory.CreateStretched("Flash", _gridRoot);
            var previewLayer = UIFactory.CreateStretched("Preview", _gridRoot);

            int count = grid.Width * grid.Height;
            _previewCells = new Image[count];
            _flashCells = new Image[count];
            float wellRadius = CellSize * 0.28f;

            foreach (var cell in grid.AllCells())
            {
                if (!grid.IsRequired(cell)) continue;
                var center = CellCenterLocal(cell);
                int index = Index(cell);

                // A recessed well: dark rim with the lighter floor shifted down a touch.
                UIFactory.CreateRoundedImage($"WellShadow_{cell.x}_{cell.y}", wells, new Vector2(CellSize, CellSize),
                    wellRadius, theme.wellInnerShadowColor, center);
                UIFactory.CreateRoundedImage($"Well_{cell.x}_{cell.y}", wells,
                    new Vector2(CellSize - 6f, CellSize - 10f), wellRadius * 0.95f, theme.wellColor,
                    center + new Vector2(0f, -4f));

                var flash = UIFactory.CreateRoundedImage($"Flash_{cell.x}_{cell.y}", flashLayer,
                    new Vector2(CellSize * 1.08f, CellSize * 1.08f), wellRadius * 1.1f, Color.clear, center);
                flash.enabled = false;
                _flashCells[index] = flash;

                // Slightly larger than the piece body so it reads as a halo even under the dragged piece.
                var preview = UIFactory.CreateRoundedImage($"Preview_{cell.x}_{cell.y}", previewLayer,
                    new Vector2(Pitch * 0.98f, Pitch * 0.98f), wellRadius * 1.2f, Color.clear, center);
                preview.enabled = false;
                _previewCells[index] = preview;
            }
        }

        /// <summary>Centre of a cell in the grid root's local space.</summary>
        public Vector2 CellCenterLocal(Vector2Int cell)
        {
            float x = (cell.x - (_grid.Width - 1) * 0.5f) * Pitch;
            float y = ((_grid.Height - 1) * 0.5f - cell.y) * Pitch;
            return new Vector2(x, y);
        }

        public Vector3 CellCenterWorld(Vector2Int cell) => _gridRoot.TransformPoint(CellCenterLocal(cell));

        /// <summary>Nearest cell to a world position. May be outside the grid.</summary>
        public Vector2Int WorldToNearestCell(Vector3 worldPosition)
        {
            Vector2 local = _gridRoot.InverseTransformPoint(worldPosition);
            float fx = local.x / Pitch + (_grid.Width - 1) * 0.5f;
            float fy = (_grid.Height - 1) * 0.5f - local.y / Pitch;
            return new Vector2Int(Mathf.RoundToInt(fx), Mathf.RoundToInt(fy));
        }

        /// <summary>World position where a piece's centre should sit so the shape lands on <paramref name="origin"/>.</summary>
        public Vector3 PieceCenterWorld(Vector2Int origin, PolyominoShape shape)
        {
            var topLeft = CellCenterLocal(origin);
            var offset = new Vector2((shape.Width - 1) * 0.5f * Pitch, -(shape.Height - 1) * 0.5f * Pitch);
            return _gridRoot.TransformPoint(topLeft + offset);
        }

        public void ShowPreview(PolyominoShape shape, Vector2Int origin, bool valid)
        {
            ClearPreview();
            _previewValid = valid;
            // Invalid is intentionally soft: a gentle red tint, no flashing.
            _previewColor = valid ? _theme.validPreviewColor : _theme.invalidPreviewColor * new Color(1f, 1f, 1f, 0.7f);
            foreach (var c in shape.Cells)
            {
                var cell = origin + c;
                if (!_grid.IsRequired(cell)) continue;
                var image = _previewCells[Index(cell)];
                image.enabled = true;
                image.color = _previewColor;
                _activePreview.Add(image);
            }
            ApplyPreviewPulse();
        }

        public void ClearPreview()
        {
            foreach (var image in _activePreview)
            {
                image.enabled = false;
                image.rectTransform.localScale = Vector3.one;
            }
            _activePreview.Clear();
        }

        private void Update()
        {
            if (_activePreview.Count > 0) ApplyPreviewPulse();
        }

        /// <summary>Valid target cells breathe softly so the drop spot feels "alive".</summary>
        private void ApplyPreviewPulse()
        {
            float wave = _previewValid ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f) : 0f;
            float alpha = _previewColor.a * (_previewValid ? Mathf.Lerp(0.6f, 1f, wave) : 1f);
            float scale = _previewValid ? 1f + 0.035f * wave : 1f;
            var color = new Color(_previewColor.r, _previewColor.g, _previewColor.b, alpha);
            foreach (var image in _activePreview)
            {
                image.color = color;
                image.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        /// <summary>Soft glow + ripple in the wells under a newly placed piece.</summary>
        public void FlashCells(PolyominoShape shape, Vector2Int origin, Color color)
        {
            foreach (var c in shape.Cells)
            {
                var cell = origin + c;
                if (!_grid.IsRequired(cell)) continue;
                FlashCell(cell, color, 0.55f, 0f, 0.42f);
            }
        }

        /// <summary>Wave of glows travelling outward from the board centre (used on completion).</summary>
        /// <returns>Seconds until the wave reaches the outermost cell.</returns>
        public float PlayCenterWave(Color color, float secondsPerCell = 0.07f)
        {
            var center = new Vector2((_grid.Width - 1) * 0.5f, (_grid.Height - 1) * 0.5f);
            float maxDelay = 0f;
            foreach (var cell in _grid.AllCells())
            {
                if (!_grid.IsRequired(cell)) continue;
                float delay = Vector2.Distance(cell, center) * secondsPerCell;
                maxDelay = Mathf.Max(maxDelay, delay);
                FlashCell(cell, color, 0.7f, delay, 0.5f);
            }
            return maxDelay;
        }

        private void FlashCell(Vector2Int cell, Color color, float strength, float delay, float duration)
        {
            int index = Index(cell);
            var image = _flashCells[index];
            if (_flashRoutines.TryGetValue(index, out var running) && running != null) StopCoroutine(running);

            var baseColor = new Color(color.r, color.g, color.b, strength);
            image.enabled = delay <= 0f;
            image.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            _flashRoutines[index] = Tween.Run(this, duration, Ease.Linear, t =>
            {
                image.enabled = true;
                // Quick rise, slow fall, slight outward swell.
                float a = t < 0.2f ? t / 0.2f : 1f - Ease.OutQuad((t - 0.2f) / 0.8f);
                image.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * a);
                image.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1.14f, Ease.OutCubic(t));
            }, () => image.enabled = false, delay);
        }

        /// <summary>Gentle "breathing" pulse of the whole tray when solved.</summary>
        public void PlaySolvedPulse()
        {
            Tween.Stop(this, ref _pulseRoutine);
            var root = (RectTransform)transform;
            _pulseRoutine = Tween.Run(this, 0.7f, Ease.Linear, t =>
            {
                float s = 1f + Ease.Wobble(t) * 0.035f;
                root.localScale = new Vector3(s, s, 1f);
            });
        }

        public void ResetEffects()
        {
            Tween.Stop(this, ref _pulseRoutine);
            foreach (var routine in _flashRoutines.Values)
            {
                if (routine != null) StopCoroutine(routine);
            }
            _flashRoutines.Clear();
            foreach (var image in _flashCells)
            {
                if (image != null) image.enabled = false;
            }
            transform.localScale = Vector3.one;
            ClearPreview();
        }

        private int Index(Vector2Int cell) => cell.y * _grid.Width + cell.x;
    }
}
