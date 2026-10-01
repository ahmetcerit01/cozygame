using System.Collections.Generic;
using UnityEngine;

namespace CozyLab.Puzzle.Presentation.Environment
{
    /// <summary>
    /// The single coordinator for all ambient motion on the gameplay screen: plant sway, sunlight/shadow
    /// drift, the petri glass glint sweep, occasional bubbles/dust, and the placed pieces' idle "breathing".
    /// Deliberately one component with one Update loop — decorative elements never get their own Update.
    /// Everything is tuned to read as almost still over one second but alive over 20-30, and stops for free
    /// when the gameplay screen closes (this component is destroyed with the rest of the puzzle canvas).
    /// </summary>
    public sealed class AmbientSceneAnimator : MonoBehaviour
    {
        private const float PieceRescanInterval = 0.35f;
        private const float BubbleIntervalMin = 5f;
        private const float BubbleIntervalMax = 11f;
        private const float DustIntervalMin = 3f;
        private const float DustIntervalMax = 7f;

        private RectTransform[] _leaves;
        private float[] _leafPhase;

        private RectTransform _sunlight;
        private Vector2 _sunlightBasePos;
        private RectTransform _windowShadow;
        private Vector2 _windowShadowBasePos;
        private RectTransform _leafShadow;
        private Vector2 _leafShadowBasePos;

        private RectTransform _glassShine;
        private Vector2 _glassShineBasePos;
        private RectTransform _glassGlint;
        private Vector2 _glassGlintBasePos;

        private BoardView _board;
        private UIParticles _particles;
        private Color _bubbleColor;
        private Color _dustColor;

        private readonly List<PieceView> _placedPieces = new List<PieceView>(16);
        private float _rescanTimer;
        private float _bubbleTimer;
        private float _dustTimer;

        public void Initialize(RectTransform[] leaves, RectTransform sunlight, RectTransform windowShadow,
            RectTransform leafShadow, RectTransform glassShine, RectTransform glassGlint, BoardView board,
            UIParticles particles, Color bubbleColor, Color dustColor)
        {
            _leaves = leaves;
            if (_leaves != null)
            {
                _leafPhase = new float[_leaves.Length];
                for (int i = 0; i < _leaves.Length; i++) _leafPhase[i] = i * 1.7f; // spread phases so sway is asynchronous
            }

            _sunlight = sunlight;
            if (_sunlight != null) _sunlightBasePos = _sunlight.anchoredPosition;
            _windowShadow = windowShadow;
            if (_windowShadow != null) _windowShadowBasePos = _windowShadow.anchoredPosition;
            _leafShadow = leafShadow;
            if (_leafShadow != null) _leafShadowBasePos = _leafShadow.anchoredPosition;

            _glassShine = glassShine;
            if (_glassShine != null) _glassShineBasePos = _glassShine.anchoredPosition;
            _glassGlint = glassGlint;
            if (_glassGlint != null) _glassGlintBasePos = _glassGlint.anchoredPosition;

            _board = board;
            _particles = particles;
            _bubbleColor = bubbleColor;
            _dustColor = dustColor;

            _bubbleTimer = Random.Range(BubbleIntervalMin, BubbleIntervalMax);
            _dustTimer = Random.Range(DustIntervalMin, DustIntervalMax);
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            AnimateLeaves(t);
            AnimateLighting(t);
            AnimateGlassSweep(t);
            AnimateBreathing(t);
            UpdateTimers(dt);
        }

        /// <summary>Tiny, out-of-phase sway — each leaf bends a fraction of a degree around its own stem pivot.</summary>
        private void AnimateLeaves(float t)
        {
            if (_leaves == null) return;
            for (int i = 0; i < _leaves.Length; i++)
            {
                var leaf = _leaves[i];
                if (leaf == null) continue;
                float sway = Mathf.Sin(t * 0.35f + _leafPhase[i]) * 1.6f;
                leaf.localRotation = Quaternion.Euler(0f, 0f, sway) * leaf.localRotation.normalized;
            }
        }

        /// <summary>Extremely slow positional drift for the sunlight and shadow overlays, each on its own period.</summary>
        private void AnimateLighting(float t)
        {
            if (_sunlight != null)
            {
                float x = Mathf.Sin(t * 0.025f) * 30f;
                float y = Mathf.Cos(t * 0.019f) * 18f;
                _sunlight.anchoredPosition = _sunlightBasePos + new Vector2(x, y);
            }
            if (_windowShadow != null)
            {
                float x = Mathf.Sin(t * 0.014f + 2f) * 22f;
                _windowShadow.anchoredPosition = _windowShadowBasePos + new Vector2(x, 0f);
            }
            if (_leafShadow != null)
            {
                float x = Mathf.Sin(t * 0.011f + 4f) * 16f;
                float y = Mathf.Cos(t * 0.017f + 1f) * 10f;
                _leafShadow.anchoredPosition = _leafShadowBasePos + new Vector2(x, y);
            }
        }

        /// <summary>Very slow glass highlight sweep across the dish, plus a barely-perceptible reflection drift.</summary>
        private void AnimateGlassSweep(float t)
        {
            if (_glassShine != null)
            {
                float x = Mathf.Sin(t * 0.05f) * 24f;
                _glassShine.anchoredPosition = _glassShineBasePos + new Vector2(x, 0f);
            }
            if (_glassGlint != null)
            {
                float x = Mathf.Sin(t * 0.033f + 1.3f) * 14f;
                float y = Mathf.Cos(t * 0.041f + 0.6f) * 10f;
                _glassGlint.anchoredPosition = _glassGlintBasePos + new Vector2(x, y);
            }
        }

        /// <summary>Placed samples breathe by roughly 1-2% scale, each on its own phase from its piece id.</summary>
        private void AnimateBreathing(float t)
        {
            for (int i = 0; i < _placedPieces.Count; i++)
            {
                var piece = _placedPieces[i];
                if (piece == null) continue;
                float phase = piece.PieceId * 2.3f;
                float scale = 1f + Mathf.Sin(t * 0.7f + phase) * 0.015f;
                piece.SetBreath(scale);
            }
        }

        private void UpdateTimers(float dt)
        {
            if (_board == null || _particles == null) return;

            _rescanTimer -= dt;
            if (_rescanTimer <= 0f)
            {
                _rescanTimer = PieceRescanInterval;
                _board.PieceLayer.GetComponentsInChildren(false, _placedPieces);
            }

            _bubbleTimer -= dt;
            if (_bubbleTimer <= 0f)
            {
                _bubbleTimer = Random.Range(BubbleIntervalMin, BubbleIntervalMax);
                EmitBubble();
            }

            _dustTimer -= dt;
            if (_dustTimer <= 0f)
            {
                _dustTimer = Random.Range(DustIntervalMin, DustIntervalMax);
                EmitDustMote();
            }
        }

        private void EmitBubble()
        {
            var cell = new Vector2Int(Random.Range(0, _board.Grid.Width), Random.Range(0, _board.Grid.Height));
            var pos = _board.CellCenterWorld(cell);
            _particles.Emit(pos, new Vector2(0f, 14f), 6f, 10f, 1.4f, _bubbleColor, ProceduralSprites.Ring, buoyancy: 6f);
        }

        private void EmitDustMote()
        {
            var boardRect = (RectTransform)_board.transform;
            var local = new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f)) * boardRect.rect.width;
            var pos = boardRect.TransformPoint(local);
            var drift = new Vector2(Random.Range(-2f, 2f), Random.Range(1f, 3f));
            _particles.Emit(pos, drift, 3f, 5f, 4f, _dustColor, ProceduralSprites.Circle, buoyancy: 0.4f);
        }
    }
}
