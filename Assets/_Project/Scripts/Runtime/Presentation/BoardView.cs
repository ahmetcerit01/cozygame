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
        public PuzzleGrid Grid => _grid;

        /// <summary>Ambience hooks for <see cref="Environment.AmbientSceneAnimator"/>; null on a rectangular
        /// (non-glass) tray, since only the circular dish has a glass highlight to sweep.</summary>
        public RectTransform GlassShine { get; private set; }
        public RectTransform GlassGlint { get; private set; }

        public void Build(PuzzleGrid grid, PuzzleTheme theme, float boardSize, float gridPadding, bool drawShell = true)
        {
            _grid = grid;
            _theme = theme;

            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(boardSize, boardSize);

            bool circular = theme.boardShape == PuzzleTheme.BoardShape.Circle;

            float gridSpan = boardSize - gridPadding * 2f;
            // A circular dish must clear the grid's diagonal, not just its longest side, or corner
            // cells would poke past the rim; a rectangular tray only needs the longest side to fit.
            float footprint = circular
                ? Mathf.Sqrt(grid.Width * grid.Width + grid.Height * grid.Height)
                : Mathf.Max(grid.Width, grid.Height);
            Pitch = gridSpan / footprint;
            CellSize = Pitch * 0.9f;

            float rim = boardSize * 0.035f;
            if (drawShell)
            {
                if (circular) BuildCircularDish(root, theme, boardSize, rim);
                else BuildRectangularDish(root, theme, boardSize, rim);
            }

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

                // A recessed culture slot: dark rim, lighter floor shifted down a touch, faint top sheen.
                WellVisual.Draw($"Well_{cell.x}_{cell.y}", wells, center, CellSize, theme);

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

        /// <summary>Square tray with rounded corners (the original presentation).</summary>
        private static void BuildRectangularDish(RectTransform root, PuzzleTheme theme, float boardSize, float rim)
        {
            float outerRadius = boardSize * 0.14f;

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
        }

        /// <summary>
        /// Round petri-dish tray, layered to read as translucent glass rather than a flat circle: cast
        /// shadow, glass wall (rim), floor (body), an inner wall shadow where the curved glass would meet
        /// the floor, a bright rim catching the light, and two highlight streaks (one soft, one a tighter
        /// glint). Purely presentational — the grid/mask contents underneath are identical to the rectangular
        /// build; a later art pass can swap these procedural layers for real glass/material assets without
        /// touching layout or gameplay.
        /// </summary>
        private void BuildCircularDish(RectTransform root, PuzzleTheme theme, float boardSize, float rim)
        {
            var shadow = UIFactory.CreateCircleShadow("Shadow", root, new Vector2(boardSize, boardSize), 48f,
                theme.boardShadowColor, new Vector2(0f, -22f));
            shadow.transform.SetAsFirstSibling();

            // A rounded-rect whose corner radius equals half its size degenerates into a perfect circle.
            UIFactory.CreateRoundedImage("Rim", root, new Vector2(boardSize, boardSize), boardSize * 0.5f, theme.boardRimColor);
            float bodySize = boardSize - rim * 2f;
            UIFactory.CreateRoundedImage("Body", root, new Vector2(bodySize, bodySize), bodySize * 0.5f, theme.boardColor);

            // Inner wall shadow: a soft ring just inside the floor's edge, where a real dish's curved glass
            // wall would catch a shadow before flattening out — gives the floor a shallow, concave read.
            var wallShadowTint = Color.Lerp(theme.boardColor, theme.wellInnerShadowColor, 0.55f);
            var innerWallShadow = UIFactory.CreateSpriteImage("InnerWallShadow", root, ProceduralSprites.Ring,
                new Vector2(bodySize * 0.95f, bodySize * 0.95f),
                new Color(wallShadowTint.r, wallShadowTint.g, wallShadowTint.b, 0.35f));
            innerWallShadow.raycastTarget = false;

            // Glass rim catching the light.
            var glassRim = UIFactory.CreateSpriteImage("GlassRim", root, ProceduralSprites.Ring,
                new Vector2(bodySize, bodySize), new Color(1f, 1f, 1f, 0.28f));
            glassRim.raycastTarget = false;

            // Broad soft sheen, like a lit dish lid seen at an angle...
            var shine = UIFactory.CreateSpriteImage("Shine", root, ProceduralSprites.SoftGlow,
                new Vector2(boardSize * 0.5f, boardSize * 0.22f), new Color(1f, 1f, 1f, 0.32f),
                new Vector2(-boardSize * 0.16f, boardSize * 0.22f));
            shine.raycastTarget = false;
            shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            GlassShine = shine.rectTransform;

            // ...plus a small, tighter glint for an actual glass-reflection highlight.
            var glint = UIFactory.CreateSpriteImage("Glint", root, ProceduralSprites.SoftGlow,
                new Vector2(boardSize * 0.15f, boardSize * 0.15f), new Color(1f, 1f, 1f, 0.5f),
                new Vector2(-boardSize * 0.24f, boardSize * 0.32f));
            glint.raycastTarget = false;
            GlassGlint = glint.rectTransform;
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
                FlashCell(cell, color, 0.22f, 0f, 0.52f);
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
