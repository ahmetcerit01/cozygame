using System.Collections;
using System.Collections.Generic;
using CozyLab.Puzzle.Core;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Feedback;
using CozyLab.Puzzle.Interaction;
using CozyLab.Puzzle.Presentation;
using CozyLab.Puzzle.UI;
using UnityEngine;

namespace CozyLab.Puzzle.Gameplay
{
    /// <summary>
    /// Glue between the pure puzzle session and the views: turns gestures into session calls and
    /// session events into animations and feedback. Owns no rules itself.
    /// </summary>
    public sealed class PuzzleController : MonoBehaviour, IPieceInputListener
    {
        private const float TouchLiftInCells = 0.9f;
        private const float SnapDuration = 0.12f;
        private const float ReturnDuration = 0.3f;
        private const float InvalidShakeDuration = 0.24f;
        private const float InvalidReturnDelay = 0.14f;
        private const float CompletionRevealDelay = 0.75f;

        private PuzzleSession _session;
        private PuzzleTheme _theme;
        private IReadOnlyList<Color> _pieceColors;
        private BoardView _board;
        private TrayView _tray;
        private RectTransform _dragLayer;
        private PuzzleHUD _hud;
        private UIParticles _particles;
        private ComboPopup _combo;
        private PuzzleFeedback _feedback;
        private readonly List<PieceView> _views = new List<PieceView>();
        private readonly PlacementStreak _streak = new PlacementStreak();

        // Active drag state (only one piece can be dragged at a time).
        private int _activePiece = -1;
        private int _activePointer;
        private Vector3 _grabOffset;
        private Vector3 _dragTarget;
        private bool _hasHover;
        private bool _hoverValid;
        private Vector2Int _hoverOrigin;

        private Coroutine _completionRoutine;
        private bool _inputLocked;

        public PuzzleSession Session => _session;
        public PlacementStreak Streak => _streak;
        public bool IsInputLocked => _inputLocked;

        public void Initialize(PuzzleSession session, PuzzleTheme theme, IReadOnlyList<Color> pieceColors, BoardView board,
            TrayView tray, RectTransform dragLayer, PuzzleHUD hud, UIParticles particles, ComboPopup combo,
            PuzzleFeedback feedback)
        {
            _session = session;
            _theme = theme;
            _pieceColors = pieceColors;
            _board = board;
            _tray = tray;
            _dragLayer = dragLayer;
            _hud = hud;
            _particles = particles;
            _combo = combo;
            _feedback = feedback;

            foreach (var piece in session.Pieces)
            {
                var rt = UIFactory.CreateRect($"Piece_{piece.Id}", dragLayer);
                var view = rt.gameObject.AddComponent<PieceView>();
                view.Initialize(piece.Id, piece.Shape, board.Pitch, pieceColors[piece.Id], theme, this);
                view.PlaceInstant(tray.GetSlot(piece.Id), tray.PieceScale);
                _views.Add(view);
            }

            _session.PiecePlaced += HandlePiecePlaced;
            _session.PieceRemoved += HandlePieceRemoved;
            _session.PieceRotated += HandlePieceRotated;
            _session.Restarted += HandleRestarted;
            _session.Solved += HandleSolved;
            _session.Changed += RefreshHud;

            _hud.UndoClicked += () => { if (!_inputLocked) _session.Undo(); };
            _hud.RestartClicked += () => { if (!_inputLocked) _session.Restart(); };

            RefreshHud();
        }

        private void OnDestroy()
        {
            if (_session == null) return;
            _session.PiecePlaced -= HandlePiecePlaced;
            _session.PieceRemoved -= HandlePieceRemoved;
            _session.PieceRotated -= HandlePieceRotated;
            _session.Restarted -= HandleRestarted;
            _session.Solved -= HandleSolved;
            _session.Changed -= RefreshHud;
        }

        // ---------------------------------------------------------------- input

        public void OnPiecePressed(int pieceId, bool pressed)
        {
            if (_inputLocked || _session.IsSolved || _session.GetPiece(pieceId).IsPlaced) return;
            _views[pieceId].SetPressed(pressed);
        }

        public void OnPieceTapped(int pieceId)
        {
            if (_inputLocked || _activePiece == pieceId) return;
            var piece = _session.GetPiece(pieceId);
            if (piece.IsPlaced)
            {
                // Placed pieces are locked for this milestone (Undo / Restart remove them).
                _views[pieceId].PlayNudge();
                return;
            }
            _session.TryRotate(pieceId);
        }

        public void OnPieceBeginDrag(int pieceId, PieceGesture gesture)
        {
            if (_inputLocked || _activePiece >= 0 || _session.IsSolved) return;

            var view = _views[pieceId];
            if (_session.GetPiece(pieceId).IsPlaced)
            {
                view.PlayNudge();
                return;
            }

            _activePiece = pieceId;
            _activePointer = gesture.PointerId;

            view.Rect.SetParent(_dragLayer, worldPositionStays: true);
            view.Rect.SetAsLastSibling();

            var pointer = ScreenToWorld(gesture);
            float scale = Mathf.Max(view.Rect.localScale.x, 0.01f);
            // Keep the grabbed point of the piece under the pointer as it grows to board size.
            _grabOffset = (view.Rect.position - pointer) / scale;
            if (gesture.IsTouch)
            {
                // Lift the piece above the finger so it stays visible.
                _grabOffset += _dragLayer.TransformVector(new Vector3(0f, _board.Pitch * TouchLiftInCells, 0f));
            }

            _dragTarget = pointer + _grabOffset;
            view.BeginDrag(_dragTarget);
            _feedback.Emit(PuzzleFeedbackEvent.Pickup);
            UpdateHover();
        }

        public void OnPieceDrag(int pieceId, PieceGesture gesture)
        {
            if (pieceId != _activePiece || gesture.PointerId != _activePointer) return;
            _dragTarget = ScreenToWorld(gesture) + _grabOffset;
            _views[pieceId].SetDragTarget(_dragTarget);
            UpdateHover();
        }

        public void OnPieceEndDrag(int pieceId, PieceGesture gesture)
        {
            if (pieceId != _activePiece || gesture.PointerId != _activePointer) return;

            _dragTarget = ScreenToWorld(gesture) + _grabOffset;
            UpdateHover();
            _board.ClearPreview();

            var view = _views[pieceId];
            _activePiece = -1;
            view.EndDrag();

            bool overBoard = _hasHover;
            bool placed = _hasHover && _hoverValid && _session.TryPlace(pieceId, _hoverOrigin);
            _hasHover = false;
            if (placed) return;

            if (overBoard)
            {
                // Dropped on the board where it doesn't fit: soft "no", then glide home.
                _streak.Break();
                view.PlayShake(InvalidShakeDuration);
                _feedback.Emit(PuzzleFeedbackEvent.InvalidPlacement);
                ReturnToTray(view, InvalidReturnDelay);
            }
            else
            {
                // Released away from the board: treat as a cancel, not a mistake.
                ReturnToTray(view);
            }
        }

        private void UpdateHover()
        {
            var piece = _session.GetPiece(_activePiece);
            var view = _views[_activePiece];

            var origin = _board.WorldToNearestCell(view.TopLeftCellWorldAt(_dragTarget));

            // Only preview when some part of the piece is over the board.
            bool anyInside = false;
            foreach (var c in piece.Shape.Cells) anyInside |= _session.Grid.InBounds(origin + c);

            _hasHover = anyInside;
            if (!anyInside)
            {
                _board.ClearPreview();
                view.SetInvalidHint(false);
                return;
            }

            _hoverOrigin = origin;
            _hoverValid = _session.CheckPlacement(piece.Id, origin) == PlacementResult.Valid;
            _board.ShowPreview(piece.Shape, origin, _hoverValid);
            view.SetInvalidHint(!_hoverValid);
        }

        private Vector3 ScreenToWorld(PieceGesture gesture)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(_dragLayer, gesture.ScreenPosition, gesture.EventCamera,
                out var world);
            return world;
        }

        // ---------------------------------------------------------------- session events

        private void HandlePiecePlaced(PieceState piece)
        {
            var view = _views[piece.Id];
            var target = _board.PieceCenterWorld(piece.Origin, piece.Shape);
            view.Rect.SetParent(_board.PieceLayer, worldPositionStays: true);
            view.Rect.SetAsLastSibling();

            bool isFinal = AllPiecesPlaced();
            int tier = _streak.RegisterValid(isFinal);
            var color = _pieceColors[piece.Id];

            view.MoveTo(() => target, 1f, SnapDuration, Ease.OutCubic, () =>
            {
                view.PlayWobble();
                PlayPlacementEffects(piece, target, color);
                _feedback.Emit(PuzzleFeedbackEvent.ValidPlacement);
                ShowCombo(tier, target);
            });
        }

        private void HandlePieceRemoved(PieceState piece)
        {
            _streak.Break();
            ReturnToTray(_views[piece.Id]);
        }

        private void HandlePieceRotated(PieceState piece)
        {
            _views[piece.Id].SetShape(piece.Shape, animateRotation: true);
            _feedback.Emit(PuzzleFeedbackEvent.Rotate);
        }

        private void HandleRestarted()
        {
            if (_completionRoutine != null) StopCoroutine(_completionRoutine);
            _completionRoutine = null;
            SetInputLocked(false);

            CancelActiveDrag();
            _streak.Break();
            _hud.HideSuccess();
            _combo.HideAll();
            _particles.Clear();
            _board.ResetEffects();
            foreach (var piece in _session.Pieces)
            {
                var view = _views[piece.Id];
                view.ResetFeedback();
                view.SetShape(piece.Shape, animateRotation: false);
                ReturnToTray(view);
            }
        }

        private void HandleSolved()
        {
            if (_completionRoutine != null) StopCoroutine(_completionRoutine);
            _completionRoutine = StartCoroutine(CompletionSequence());
        }

        // ---------------------------------------------------------------- effects

        private void PlayPlacementEffects(PieceState piece, Vector3 pieceCenter, Color color)
        {
            var light = Color.Lerp(color, Color.white, 0.45f);
            _board.FlashCells(piece.Shape, piece.Origin, light);

            float cell = _board.CellSize;
            var ringColor = new Color(light.r, light.g, light.b, 0.7f);
            foreach (var c in piece.Shape.Cells)
                _particles.Ripple(_board.CellCenterWorld(piece.Origin + c), ringColor, cell * 0.75f, cell * 1.3f, 0.45f);

            _particles.BubbleBurst(pieceCenter, color, 8, _board.Pitch * 1.6f, _board.Pitch * 0.15f, _board.Pitch * 0.3f);
        }

        private void ShowCombo(int tier, Vector3 pieceCenter)
        {
            string label = _theme.GetComboLabel(tier);
            if (string.IsNullOrEmpty(label)) return;
            var color = tier >= PlacementStreak.TierPerfect ? _theme.comboPerfectColor : _theme.comboColor;
            float scale = tier >= PlacementStreak.TierPerfect ? 1.15f : tier == PlacementStreak.TierGreat ? 1.05f : 0.95f;
            var above = _dragLayer.TransformVector(new Vector3(0f, _board.Pitch * 0.35f, 0f));
            _combo.Show(label, pieceCenter + above, color, scale);
        }

        /// <summary>
        /// ~1 s: let the last piece land → wave from the centre + sequential bounce + bubbles → reveal result.
        /// Input is locked for the duration.
        /// </summary>
        private IEnumerator CompletionSequence()
        {
            SetInputLocked(true);
            CancelActiveDrag();

            yield return new WaitForSecondsRealtime(SnapDuration + 0.12f);

            _feedback.Emit(PuzzleFeedbackEvent.Completion);
            var waveColor = Color.Lerp(_theme.accentColor, Color.white, 0.55f);
            _board.PlayCenterWave(waveColor);
            _board.PlaySolvedPulse();

            // Bounce pieces from the centre outward.
            var center = _board.GridRoot.position;
            var order = new List<PieceView>(_views);
            order.Sort((a, b) => (a.Rect.position - center).sqrMagnitude.CompareTo((b.Rect.position - center).sqrMagnitude));
            for (int i = 0; i < order.Count; i++)
            {
                order[i].PlayCelebrate(0.06f + i * 0.08f);
                _particles.BubbleBurst(order[i].Rect.position, _pieceColors[order[i].PieceId], 5,
                    _board.Pitch * 1.8f, _board.Pitch * 0.14f, _board.Pitch * 0.45f);
            }
            float span = _board.Pitch * Mathf.Max(_session.Grid.Width, _session.Grid.Height);
            _particles.Ripple(center, new Color(1f, 1f, 1f, 0.8f), span * 0.4f, span * 1.25f, 0.6f);

            yield return new WaitForSecondsRealtime(CompletionRevealDelay);

            _hud.ShowSuccess(0f);
            SetInputLocked(false);
            _completionRoutine = null;
        }

        // ---------------------------------------------------------------- helpers

        private void SetInputLocked(bool locked)
        {
            _inputLocked = locked;
            _hud.SetInputLocked(locked);
        }

        private bool AllPiecesPlaced()
        {
            foreach (var p in _session.Pieces)
            {
                if (!p.IsPlaced) return false;
            }
            return true;
        }

        private void ReturnToTray(PieceView view, float delay = 0f)
        {
            var slot = _tray.GetSlot(view.PieceId);
            view.Rect.SetParent(_dragLayer, worldPositionStays: true);
            view.Rect.SetAsLastSibling();
            view.MoveTo(() => slot.position, _tray.PieceScale, ReturnDuration, Ease.OutCubic,
                () => view.PlaceInstant(slot, _tray.PieceScale), delay);
        }

        private void CancelActiveDrag()
        {
            if (_activePiece < 0) return;
            _views[_activePiece].EndDrag();
            _activePiece = -1;
            _hasHover = false;
            _board.ClearPreview();
        }

        private void RefreshHud()
        {
            int placed = 0;
            foreach (var piece in _session.Pieces)
            {
                if (piece.IsPlaced) placed++;
            }
            _hud.SetProgress(placed, _session.Pieces.Count);
            _hud.SetUndoInteractable(_session.CanUndo);
        }
    }
}
