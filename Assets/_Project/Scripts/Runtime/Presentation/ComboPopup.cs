using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.Presentation
{
    /// <summary>Small floating "GOOD! / GREAT! / PERFECT!" labels. Pooled, a few at a time.</summary>
    public sealed class ComboPopup : MonoBehaviour
    {
        private const int PoolSize = 3;
        private const float Rise = 90f;
        private const float Duration = 0.95f;

        private Text[] _labels;
        private Coroutine[] _routines;
        private int _next;

        public void Initialize(int fontSize)
        {
            _labels = new Text[PoolSize];
            _routines = new Coroutine[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var text = UIFactory.CreateText("Combo", transform, string.Empty, fontSize, Color.white,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                var rt = text.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(600f, fontSize * 1.6f);
                var outline = text.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 1f, 1f, 0.9f);
                outline.effectDistance = new Vector2(3f, -3f);
                var shadow = text.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0.1f, 0.15f, 0.2f, 0.18f);
                shadow.effectDistance = new Vector2(0f, -6f);
                text.enabled = false;
                _labels[i] = text;
            }
        }

        public void Show(string label, Vector3 worldPosition, Color color, float scale = 1f)
        {
            if (_labels == null || string.IsNullOrEmpty(label)) return;

            int index = _next;
            _next = (_next + 1) % PoolSize;
            var text = _labels[index];
            if (_routines[index] != null) StopCoroutine(_routines[index]);

            var rt = text.rectTransform;
            rt.SetAsLastSibling();
            Vector2 start = transform.InverseTransformPoint(worldPosition);
            text.text = label;
            text.enabled = true;

            _routines[index] = Tween.Run(this, Duration, Ease.Linear, t =>
            {
                // Pop in with a soft overshoot, drift up, fade out at the end.
                float pop = Mathf.Clamp01(t / 0.28f);
                float s = Mathf.LerpUnclamped(0.55f, 1f, Ease.OutBack(pop)) * scale;
                rt.localScale = new Vector3(s, s, 1f);
                rt.anchoredPosition = start + new Vector2(0f, Rise * Ease.OutCubic(t));
                float fade = t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.35f;
                text.color = new Color(color.r, color.g, color.b, fade);
            }, () => text.enabled = false);
        }

        public void HideAll()
        {
            if (_labels == null) return;
            for (int i = 0; i < PoolSize; i++)
            {
                if (_routines[i] != null) StopCoroutine(_routines[i]);
                _routines[i] = null;
                _labels[i].enabled = false;
            }
        }
    }
}
