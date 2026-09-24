using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CozyLab.Puzzle.Interaction
{
    /// <summary>Receives high-level piece gestures. Implemented by the gameplay controller.</summary>
    public interface IPieceInputListener
    {
        void OnPiecePressed(int pieceId, bool pressed);
        void OnPieceTapped(int pieceId);
        void OnPieceBeginDrag(int pieceId, PieceGesture gesture);
        void OnPieceDrag(int pieceId, PieceGesture gesture);
        void OnPieceEndDrag(int pieceId, PieceGesture gesture);
    }

    /// <summary>Device-agnostic snapshot of a pointer event.</summary>
    public readonly struct PieceGesture
    {
        public readonly int PointerId;
        public readonly Vector2 ScreenPosition;
        public readonly Camera EventCamera;
        /// <summary>True for finger input. Only used for cosmetic tweaks (lifting the piece above the finger).</summary>
        public readonly bool IsTouch;

        public PieceGesture(PointerEventData eventData)
        {
            PointerId = eventData.pointerId;
            ScreenPosition = eventData.position;
            EventCamera = eventData.pressEventCamera;
            IsTouch = eventData is ExtendedPointerEventData extended && extended.pointerType == UIPointerType.Touch;
        }
    }

    /// <summary>
    /// Translates uGUI pointer events into piece gestures. The EventSystem + InputSystemUIInputModule
    /// feed mouse (Editor) and touch (iPhone) through this same path, so there is one gameplay input system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PieceDragHandler : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private IPieceInputListener _listener;
        private int _pieceId;

        public void Initialize(int pieceId, IPieceInputListener listener)
        {
            _pieceId = pieceId;
            _listener = listener;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _listener?.OnPiecePressed(_pieceId, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _listener?.OnPiecePressed(_pieceId, false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // uGUI only sends a click when the pointer did not start a drag, so this is a clean "tap".
            if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging) return;
            _listener?.OnPieceTapped(_pieceId);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = true;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _listener?.OnPieceBeginDrag(_pieceId, new PieceGesture(eventData));
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _listener?.OnPieceDrag(_pieceId, new PieceGesture(eventData));
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _listener?.OnPieceEndDrag(_pieceId, new PieceGesture(eventData));
        }
    }
}
