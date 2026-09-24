using System;
using CozyLab.Puzzle.Core;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.Presentation
{
    /// <summary>
    /// Visual for one piece: a soft, jelly-like blob built from rounded cells and bridges.
    /// Hierarchy: root (position + base scale) → Lift (press/lift scale) → Visual (rotation + wobble).
    /// </summary>
    public sealed class PieceView : MonoBehaviour
    {
        private const float DragFollowSharpness = 28f;
        private const float LiftedScale = 1.07f;
        private const float MaxTiltDegrees = 7f;
        private const float InvalidAlpha = 0.72f;

        private RectTransform _root;
        private RectTransform _lift;
        private RectTransform _visual;
        private RectTransform _shadowLayer;
        private CanvasGroup _shadowGroup;
        private RectTransform _hitArea;
        private CanvasGroup _group;

        private PuzzleTheme _theme;
        private float _pitch;
        private Color _color;
        private PolyominoShape _shape;

        private bool _dragging;
        private Vector3 _dragTarget;
        private Vector3 _lastPosition;
        private float _tilt;
        private float _targetAlpha = 1f;

        private Coroutine _moveRoutine;
        private Coroutine _fxRoutine;
        private Coroutine _liftRoutine;
        private Coroutine _pressRoutine;
        private Coroutine _celebrateRoutine;
        private Coroutine _shakeRoutine;

        public int PieceId { get; private set; }
        public RectTransform Rect => _root;
        public bool IsDragging => _dragging;

        public void Initialize(int pieceId, PolyominoShape shape, float pitch, Color color, PuzzleTheme theme,
            IPieceInputListener listener)
        {
            PieceId = pieceId;
            _pitch = pitch;
            _color = color;
            _theme = theme;

            _root = (RectTransform)transform;
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0.5f, 0.5f);

            _hitArea = UIFactory.CreateStretched("HitArea", _root);
            var hit = UIFactory.AddImage(_hitArea, new Color(1f, 1f, 1f, 0f), raycastTarget: true);
            hit.sprite = null;
            // Generous touch target around the piece.
            _hitArea.offsetMin = new Vector2(-pitch * 0.15f, -pitch * 0.15f);
            _hitArea.offsetMax = new Vector2(pitch * 0.15f, pitch * 0.15f);

            _lift = UIFactory.CreateCentered("Lift", _root, Vector2.zero);
            _visual = UIFactory.CreateCentered("Visual", _lift, Vector2.zero);

            gameObject.AddComponent<PieceDragHandler>().Initialize(pieceId, listener);

            SetShape(shape, animateRotation: false);
        }

        /// <summary>Rebuilds the blob for a shape. When rotating, the new shape spins in from the old orientation.</summary>
        public void SetShape(PolyominoShape shape, bool animateRotation)
        {
            _shape = shape;
            _root.sizeDelta = new Vector2(shape.Width * _pitch, shape.Height * _pitch);
            BuildGraphics();

            Tween.Stop(this, ref _fxRoutine);
            _visual.localScale = Vector3.one;

            if (!animateRotation)
            {
                _visual.localRotation = Quaternion.identity;
                return;
            }

            // Data rotated clockwise. Start the new graphics rotated back (+90 = counter-clockwise in UI space)
            // so it looks identical to the old orientation, then spin to 0.
            _fxRoutine = Tween.Run(this, 0.26f, Ease.Linear, t =>
            {
                _visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(90f, 0f, Ease.OutBackSoft(t)));
                float pulse = 1f + 0.08f * Mathf.Sin(t * Mathf.PI);
                _visual.localScale = new Vector3(pulse, pulse, 1f);
            }, () =>
            {
                _visual.localRotation = Quaternion.identity;
                _visual.localScale = Vector3.one;
            });
        }

        /// <summary>World position of the shape's top-left bounding-box cell centre if the piece centre were at <paramref name="centerWorld"/> at full scale.</summary>
        public Vector3 TopLeftCellWorldAt(Vector3 centerWorld)
        {
            var local = new Vector3(-(_shape.Width - 1) * 0.5f * _pitch, (_shape.Height - 1) * 0.5f * _pitch, 0f);
            var parent = _root.parent;
            return centerWorld + (parent != null ? parent.TransformVector(local) : local);
        }

        // ---------------------------------------------------------------- drag

        public void BeginDrag(Vector3 initialTarget)
        {
            Tween.Stop(this, ref _moveRoutine);
            Tween.Stop(this, ref _shakeRoutine);
            _lift.anchoredPosition = Vector2.zero;
            _dragging = true;
            _dragTarget = initialTarget;
            _lastPosition = _root.position;
            SetLifted(true);
        }

        public void SetDragTarget(Vector3 worldTarget) => _dragTarget = worldTarget;

        public void EndDrag()
        {
            _dragging = false;
            SetInvalidHint(false);
            SetLifted(false);
        }

        /// <summary>Fades the piece while it hovers over a spot where it cannot be dropped.</summary>
        public void SetInvalidHint(bool invalid)
        {
            if (_group == null)
            {
                _group = gameObject.AddComponent<CanvasGroup>();
                _group.alpha = 1f;
            }
            _targetAlpha = invalid ? InvalidAlpha : 1f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_dragging)
            {
                // Exponential smoothing: responsive but never jittery, frame-rate independent.
                float k = 1f - Mathf.Exp(-DragFollowSharpness * dt);
                _root.position = Vector3.Lerp(_root.position, _dragTarget, k);
                float s = Mathf.Lerp(_root.localScale.x, 1f, k);
                _root.localScale = new Vector3(s, s, 1f);

                // Lean slightly into horizontal movement, like a soft blob being carried.
                if (dt > 0f && _root.parent != null)
                {
                    var velocity = _root.parent.InverseTransformVector(_root.position - _lastPosition) / dt;
                    float targetTilt = Mathf.Clamp(-velocity.x * 0.004f, -MaxTiltDegrees, MaxTiltDegrees);
                    _tilt = Mathf.Lerp(_tilt, targetTilt, 1f - Mathf.Exp(-12f * dt));
                }
                _lastPosition = _root.position;
            }
            else if (Mathf.Abs(_tilt) > 0.01f)
            {
                _tilt = Mathf.Lerp(_tilt, 0f, 1f - Mathf.Exp(-14f * dt));
            }
            else
            {
                _tilt = 0f;
            }
            _lift.localRotation = Quaternion.Euler(0f, 0f, _tilt);

            if (_group != null && !Mathf.Approximately(_group.alpha, _targetAlpha))
                _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, dt * 4f);
        }

        // ---------------------------------------------------------------- movement

        /// <summary>Tweens the piece centre to a (possibly moving) world target and to a uniform scale.</summary>
        public void MoveTo(Func<Vector3> worldTarget, float targetScale, float duration, Func<float, float> ease,
            Action onComplete = null, float delay = 0f)
        {
            _dragging = false;
            Tween.Stop(this, ref _moveRoutine);
            Vector3 startPos = _root.position;
            float startScale = _root.localScale.x;
            bool started = false;
            _moveRoutine = Tween.Run(this, duration, ease, t =>
            {
                if (!started)
                {
                    // Capture the start after any delay (the piece may have shaken in place meanwhile).
                    started = true;
                    startPos = _root.position;
                    startScale = _root.localScale.x;
                }
                _root.position = Vector3.LerpUnclamped(startPos, worldTarget(), t);
                float s = Mathf.LerpUnclamped(startScale, targetScale, t);
                _root.localScale = new Vector3(s, s, 1f);
            }, () =>
            {
                _moveRoutine = null;
                onComplete?.Invoke();
            }, delay);
        }

        /// <summary>Instantly parents into a slot/layer at a scale (used on first layout).</summary>
        public void PlaceInstant(RectTransform parent, float scale)
        {
            Tween.Stop(this, ref _moveRoutine);
            _dragging = false;
            _root.SetParent(parent, false);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.anchoredPosition = Vector2.zero;
            _root.localScale = new Vector3(scale, scale, 1f);
        }

        // ---------------------------------------------------------------- feedback

        public void SetPressed(bool pressed)
        {
            if (_dragging) return;
            Tween.Stop(this, ref _pressRoutine);
            Vector3 from = _lift.localScale;
            // A soft squash: a little wider, a little shorter.
            Vector3 to = pressed ? new Vector3(1.05f, 0.92f, 1f) : Vector3.one;
            _pressRoutine = Tween.Run(this, pressed ? 0.09f : 0.22f, pressed ? Ease.OutQuad : Ease.OutBackSoft,
                t => _lift.localScale = Vector3.LerpUnclamped(from, to, t));
        }

        /// <summary>Squash-and-stretch impact, used when a piece lands in the grid.</summary>
        public void PlayWobble(float strength = 0.11f, float duration = 0.4f, float delay = 0f)
        {
            Tween.Stop(this, ref _fxRoutine);
            _visual.localRotation = Quaternion.identity;
            _fxRoutine = Tween.Run(this, duration, Ease.Linear, t =>
            {
                float w = Ease.Wobble(t) * strength;
                _visual.localScale = new Vector3(1f + w, 1f - w * 0.8f, 1f);
            }, () => _visual.localScale = Vector3.one, delay);
        }

        /// <summary>Small side-to-side shake: "can't do that".</summary>
        public void PlayNudge()
        {
            Tween.Stop(this, ref _fxRoutine);
            _visual.localScale = Vector3.one;
            _fxRoutine = Tween.Run(this, 0.3f, Ease.Linear, t =>
            {
                _visual.localRotation = Quaternion.Euler(0f, 0f, Ease.Wobble(t) * 6f);
            }, () => _visual.localRotation = Quaternion.identity);
        }

        /// <summary>Tiny horizontal "no" shake on the spot (invalid drop).</summary>
        public void PlayShake(float duration = 0.24f)
        {
            Tween.Stop(this, ref _shakeRoutine);
            float amplitude = _pitch * 0.07f;
            _shakeRoutine = Tween.Run(this, duration, Ease.Linear, t =>
            {
                _lift.anchoredPosition = new Vector2(Mathf.Sin(t * Mathf.PI * 6f) * amplitude * (1f - t), 0f);
            }, () => _lift.anchoredPosition = Vector2.zero);
        }

        /// <summary>Tiny victory bounce. Runs on the Lift layer so it can overlap a landing wobble.</summary>
        public void PlayCelebrate(float delay)
        {
            Tween.Stop(this, ref _celebrateRoutine);
            _celebrateRoutine = Tween.Run(this, 0.42f, Ease.Linear, t =>
            {
                float hop = Mathf.Sin(t * Mathf.PI);
                float squash = Ease.Wobble(t) * 0.05f;
                _lift.localScale = new Vector3(1f + hop * 0.08f + squash, 1f + hop * 0.08f - squash, 1f);
                _lift.anchoredPosition = new Vector2(0f, hop * _pitch * 0.12f);
            }, () =>
            {
                SetUniform(_lift, 1f);
                _lift.anchoredPosition = Vector2.zero;
            }, delay);
        }

        public void ResetFeedback()
        {
            Tween.Stop(this, ref _fxRoutine);
            Tween.Stop(this, ref _celebrateRoutine);
            Tween.Stop(this, ref _pressRoutine);
            Tween.Stop(this, ref _shakeRoutine);
            _visual.localScale = Vector3.one;
            _visual.localRotation = Quaternion.identity;
            _lift.anchoredPosition = Vector2.zero;
            _tilt = 0f;
            SetUniform(_lift, 1f);
            SetInvalidHint(false);
            SetLifted(false);
        }

        private void SetLifted(bool lifted)
        {
            Tween.Stop(this, ref _liftRoutine);
            Tween.Stop(this, ref _pressRoutine);
            Vector3 fromScale = _lift.localScale;
            Vector3 toScale = Vector3.one * (lifted ? LiftedScale : 1f);
            float depth = _theme.pieceDepth * _pitch;
            Vector2 fromShadow = _shadowLayer != null ? _shadowLayer.anchoredPosition : Vector2.zero;
            // Lifted: the shadow drops further away and spreads, so the piece reads as "in the air".
            Vector2 toShadow = new Vector2(0f, lifted ? -depth * 4.5f : 0f);
            float fromShadowScale = _shadowLayer != null ? _shadowLayer.localScale.x : 1f;
            float toShadowScale = lifted ? 1.1f : 1f;

            // Up: soft elastic settle. Down: quick and clean.
            _liftRoutine = Tween.Run(this, lifted ? 0.28f : 0.14f, lifted ? Ease.OutBackSoft : Ease.OutCubic, t =>
            {
                _lift.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
                if (_shadowLayer != null)
                {
                    _shadowLayer.anchoredPosition = Vector2.LerpUnclamped(fromShadow, toShadow, t);
                    float ss = Mathf.LerpUnclamped(fromShadowScale, toShadowScale, t);
                    _shadowLayer.localScale = new Vector3(ss, ss, 1f);
                }
            });
        }

        private static void SetUniform(Transform t, float s) => t.localScale = new Vector3(s, s, 1f);

        // ---------------------------------------------------------------- graphics

        private void BuildGraphics()
        {
            UIFactory.DestroyChildren(_visual);

            float body = _pitch * _theme.pieceBodyScale;
            float radius = body * _theme.pieceCornerRadius;
            float depth = _theme.pieceDepth * _pitch;
            float bridgeThickness = body * 0.62f;

            var under = Color.Lerp(_color, new Color(0.15f, 0.18f, 0.25f), 0.28f);
            var core = Color.Lerp(_color, Color.white, 0.2f);
            var dot = Color.Lerp(_color, new Color(0.2f, 0.22f, 0.32f), 0.35f);
            dot.a = 0.55f;
            var shadowColor = new Color(0.10f, 0.16f, 0.22f, _theme.pieceShadowAlpha);

            _shadowLayer = UIFactory.CreateCentered("Shadows", _visual, Vector2.zero);
            _shadowGroup = _shadowLayer.gameObject.AddComponent<CanvasGroup>();
            _shadowGroup.blocksRaycasts = false;
            _shadowGroup.interactable = false;
            var underLayer = UIFactory.CreateCentered("Underside", _visual, Vector2.zero);
            var bodyLayer = UIFactory.CreateCentered("Body", _visual, Vector2.zero);
            var detailLayer = UIFactory.CreateCentered("Details", _visual, Vector2.zero);

            var shadowOffset = new Vector2(0f, -depth - _pitch * 0.05f);
            var underOffset = new Vector2(0f, -depth);
            float shadowBlur = _pitch * 0.12f;

            foreach (var cell in _shape.Cells)
            {
                var center = CellLocal(cell);

                UIFactory.CreateShadow("Shadow", _shadowLayer, new Vector2(body, body), shadowBlur, shadowColor,
                    center + shadowOffset);
                UIFactory.CreateRoundedImage("Under", underLayer, new Vector2(body, body), radius, under, center + underOffset);
                UIFactory.CreateRoundedImage("Cell", bodyLayer, new Vector2(body, body), radius, _color, center);

                // Bridges to right / down neighbours make the cells read as one soft blob.
                foreach (var dir in new[] { Vector2Int.right, new Vector2Int(0, 1) })
                {
                    if (!_shape.Contains(cell + dir)) continue;
                    var mid = (center + CellLocal(cell + dir)) * 0.5f;
                    var size = dir.x != 0 ? new Vector2(_pitch, bridgeThickness) : new Vector2(bridgeThickness, _pitch);
                    UIFactory.CreateRoundedImage("UnderBridge", underLayer, size, bridgeThickness * 0.3f, under, mid + underOffset);
                    UIFactory.CreateRoundedImage("Bridge", bodyLayer, size, bridgeThickness * 0.3f, _color, mid);
                }

                // Fill the hole in the middle of any 2x2 block.
                var right = cell + Vector2Int.right;
                var down = cell + new Vector2Int(0, 1);
                var diag = cell + new Vector2Int(1, 1);
                if (_shape.Contains(right) && _shape.Contains(down) && _shape.Contains(diag))
                {
                    var mid = (center + CellLocal(diag)) * 0.5f;
                    UIFactory.CreateRoundedImage("Fill", bodyLayer, new Vector2(body, body) * 0.7f, radius * 0.5f, _color, mid);
                }

                // Gel-like lighter core, highlight and organelle dots.
                UIFactory.CreateRoundedImage("Core", detailLayer, new Vector2(body, body) * 0.68f, radius * 0.7f, core,
                    center + new Vector2(0f, body * 0.02f));
                UIFactory.CreateSpriteImage("Highlight", detailLayer, ProceduralSprites.SoftGlow,
                    new Vector2(body * 0.5f, body * 0.34f), new Color(1f, 1f, 1f, _theme.pieceHighlightAlpha),
                    center + new Vector2(-body * 0.16f, body * 0.22f));

                var rng = new System.Random(PieceId * 7919 + cell.x * 131 + cell.y * 17);
                for (int i = 0; i < _theme.pieceDetailDots; i++)
                {
                    float d = body * (0.1f + (float)rng.NextDouble() * 0.07f);
                    var p = new Vector2(((float)rng.NextDouble() - 0.5f) * body * 0.42f,
                        ((float)rng.NextDouble() - 0.6f) * body * 0.36f);
                    UIFactory.CreateSpriteImage("Dot", detailLayer, ProceduralSprites.Circle, new Vector2(d, d), dot, center + p);
                }
            }
        }

        private Vector2 CellLocal(Vector2Int cell)
        {
            float x = (cell.x - (_shape.Width - 1) * 0.5f) * _pitch;
            float y = ((_shape.Height - 1) * 0.5f - cell.y) * _pitch;
            return new Vector2(x, y);
        }
    }
}
