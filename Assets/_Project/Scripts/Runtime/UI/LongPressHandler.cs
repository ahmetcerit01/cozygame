using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CozyLab.Puzzle.UI
{
    /// <summary>Fires once when a pointer is held down on this element for <see cref="Duration"/> seconds.</summary>
    public sealed class LongPressHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float Duration = 3f;
        public event Action LongPressed;

        private float _pressedAt = -1f;

        public void OnPointerDown(PointerEventData eventData) => _pressedAt = Time.unscaledTime;
        public void OnPointerUp(PointerEventData eventData) => _pressedAt = -1f;
        public void OnPointerExit(PointerEventData eventData) => _pressedAt = -1f;

        private void Update()
        {
            if (_pressedAt < 0f || Time.unscaledTime - _pressedAt < Duration) return;
            _pressedAt = -1f;
            LongPressed?.Invoke();
        }
    }
}
