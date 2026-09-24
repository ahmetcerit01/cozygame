using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>Squishes a button slightly while pressed.</summary>
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float pressedScale = 0.93f;
        private Coroutine _routine;
        private Selectable _selectable;

        private void Awake() => _selectable = GetComponent<Selectable>();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.IsInteractable()) return;
            Animate(pressedScale, 0.08f, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData) => Animate(1f, 0.2f, Ease.OutBack);

        private void OnDisable()
        {
            _routine = null;
            transform.localScale = Vector3.one;
        }

        private void Animate(float target, float duration, System.Func<float, float> ease)
        {
            Tween.Stop(this, ref _routine);
            float from = transform.localScale.x;
            _routine = Tween.Run(this, duration, ease, t =>
            {
                float s = Mathf.LerpUnclamped(from, target, t);
                transform.localScale = new Vector3(s, s, 1f);
            });
        }
    }
}
